using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;

namespace ProjectHymn.Tests
{
    public class TempoMapTests
    {
        static TempoMap Map(bool loop, params TempoSection[] s) => new TempoMap(new List<TempoSection>(s), loop);

        [Test]
        public void ConstantTempo_BeatTimes()
        {
            var m = Map(false, new TempoSection(4, 120f, 4)); // 0.5 s per beat
            Assert.AreEqual(0.0, m.GetBeat(0).Time, 1e-9);
            Assert.AreEqual(1.5, m.GetBeat(3).Time, 1e-9);
            Assert.AreEqual(3.0, m.TimeToBeat(1.5), 1e-9);
            Assert.AreEqual(8.0, m.LengthSeconds, 1e-9);
        }

        [Test]
        public void MeterChange_MeasuresAndDownbeats()
        {
            // 2 bars of 4/4 at 120, then 2 bars of 3/4 at 120
            var m = Map(false, new TempoSection(2, 120f, 4), new TempoSection(2, 120f, 3));
            var b8 = m.GetBeat(8);
            Assert.AreEqual(2, b8.Measure);
            Assert.AreEqual(0, b8.BeatInMeasure);
            Assert.AreEqual(3, b8.BeatsPerMeasure);
            Assert.AreEqual(1, b8.SectionIndex);
            var b11 = m.GetBeat(11);
            Assert.AreEqual(3, b11.Measure);
            Assert.IsTrue(b11.IsDownbeat);
            Assert.AreEqual(14, m.LengthBeats);
        }

        [Test]
        public void TempoChange_TimeIsContinuous()
        {
            // 1 bar 4/4 @ 120 (2 s), then 1 bar 7/8 @ 240 (0.25 s per beat)
            var m = Map(false, new TempoSection(1, 120f, 4), new TempoSection(1, 240f, 7, 8));
            Assert.AreEqual(2.0, m.GetBeat(4).Time, 1e-9);
            Assert.AreEqual(2.25, m.GetBeat(5).Time, 1e-9);
            Assert.AreEqual(5.0, m.TimeToBeat(2.25), 1e-9);
            Assert.AreEqual(3.75, m.LengthSeconds, 1e-9);
        }

        [Test]
        public void Loop_WrapsButIndicesKeepCounting()
        {
            var m = Map(true, new TempoSection(1, 120f, 4)); // 4 beats, 2 s
            var b = m.GetBeat(6);
            Assert.AreEqual(3.0, b.Time, 1e-9);
            Assert.AreEqual(1, b.Measure);
            Assert.AreEqual(2, b.BeatInMeasure);
            Assert.AreEqual(6.0, m.TimeToBeat(3.0), 1e-9);
        }

        [Test]
        public void CountIn_NegativeBeats()
        {
            var m = Map(false, new TempoSection(4, 120f, 4));
            var b = m.GetBeat(-1);
            Assert.AreEqual(-0.5, b.Time, 1e-9);
            Assert.AreEqual(-1, b.Measure);
            Assert.AreEqual(3, b.BeatInMeasure);
        }
    }

    public class BeatJudgeTests
    {
        readonly TempoMap _map = new TempoMap(new List<TempoSection> { new TempoSection(8, 120f, 4) }, false);
        readonly TimingWindows _w = TimingWindows.Default;

        [TestCase(1.00, Judgement.Perfect)]
        [TestCase(1.04, Judgement.Perfect)]
        [TestCase(0.93, Judgement.Great)]
        [TestCase(1.12, Judgement.Good)]
        [TestCase(1.20, Judgement.Miss)]
        public void Classifies(double t, Judgement expected)
        {
            Assert.AreEqual(expected, BeatJudge.Judge(_map, t, _w).Judgement);
        }

        [Test]
        public void AttributesEarlyInputToUpcomingBeat()
        {
            var r = BeatJudge.Judge(_map, 0.97, _w); // 30 ms early for beat 2
            Assert.AreEqual(2, r.Beat.Index);
            Assert.AreEqual(-30, r.OffsetMs, 1e-6);
        }

        [Test]
        public void EasyModeWidensWindows()
        {
            var easy = _w; easy.scale = 1.5f;
            Assert.AreEqual(Judgement.Good, BeatJudge.Judge(_map, 1.18, easy).Judgement);
        }
    }

    public class ComboEngineTests
    {
        const BeatAction S = BeatAction.Strike, H = BeatAction.Smash, R = BeatAction.Rest, D = BeatAction.Step;
        TempoMap _map;
        ComboEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _map = new TempoMap(new List<TempoSection> { new TempoSection(4, 120f, 4), new TempoSection(4, 120f, 3) }, false);
            _engine = new ComboEngine();
            _engine.SetPhrases(new[]
            {
                new PhrasePattern("Triplet Cut", new[] { S, S, H }),
                new PhrasePattern("Syncopate", new[] { S, R, H }),
                new PhrasePattern("Backbeat", new[] { R, H, R, H }),
                new PhrasePattern("Off-step", new[] { D, S }),
                new PhrasePattern("Waltz", new[] { S, S, S }, 3),
            });
        }

        string Play(long beat, BeatAction a)
        {
            _engine.Register(_map.GetBeat(beat), a, out var p);
            return p?.Name;
        }

        [Test]
        public void MatchesConsecutivePattern()
        {
            Assert.IsNull(Play(0, S));
            Assert.IsNull(Play(1, S));
            Assert.AreEqual("Triplet Cut", Play(2, H));
        }

        [Test]
        public void RestsAreImpliedBySkippedBeats()
        {
            Play(0, S);
            Assert.AreEqual("Syncopate", Play(2, H));
        }

        [Test]
        public void PrefersLongestMatch()
        {
            // R H R H also ends in "...R H" which Syncopate (S R H) doesn't match, so Backbeat wins
            Play(1, H);
            Assert.AreEqual("Backbeat", Play(3, H));
        }

        [Test]
        public void MissBreaksPhrase()
        {
            Play(0, S);
            _engine.RegisterMiss(_map.GetBeat(1));
            Assert.IsNull(Play(2, H));
        }

        [Test]
        public void DoubleInputOnOneBeatBreaksPhrase()
        {
            Play(0, S);
            Play(1, S);
            var outcome = _engine.Register(_map.GetBeat(1), S, out _);
            Assert.AreEqual(RegisterOutcome.DoubleInput, outcome);
            Assert.IsNull(Play(2, H));
        }

        [Test]
        public void CompletedPhraseIsConsumed()
        {
            var e = new ComboEngine();
            e.SetPhrases(new[] { new PhrasePattern("A", new[] { S, H }), new PhrasePattern("B", new[] { H, S }) });
            e.Register(_map.GetBeat(0), S, out var p1);
            e.Register(_map.GetBeat(1), H, out var p2);
            e.Register(_map.GetBeat(2), S, out var p3);
            Assert.IsNull(p1);
            Assert.AreEqual("A", p2?.Name);
            Assert.IsNull(p3); // beat 1 was already spent on A, so B can't reuse it
        }

        [Test]
        public void MeterGatedPhraseOnlyInMatchingMeter()
        {
            Play(0, S); Play(1, S);
            Assert.IsNull(Play(2, S));                  // 4/4: no Waltz
            Play(16, S); Play(17, S);
            Assert.AreEqual("Waltz", Play(18, S));      // beats 16+ are 3/4
        }

        [Test]
        public void FullMeasureCompletesBar()
        {
            int bars = 0;
            _engine.BarCompleted += _ => bars++;
            Play(0, S); Play(1, H); Play(2, S); Play(3, D);
            Assert.AreEqual(1, bars);
        }

        [Test]
        public void BarWithARestDoesNotComplete()
        {
            int bars = 0;
            _engine.BarCompleted += _ => bars++;
            Play(0, S); Play(1, H); Play(3, S);
            Assert.AreEqual(0, bars);
        }

        [Test]
        public void ThreeFourBarNeedsThreeBeats()
        {
            int bars = 0;
            _engine.BarCompleted += _ => bars++;
            Play(16, S); Play(17, S); Play(18, H);
            Assert.AreEqual(1, bars);
        }
    }

    public class GrooveMeterTests
    {
        [Test]
        public void TiersRiseAndFall()
        {
            var g = new GrooveMeter();
            for (int i = 0; i < 5; i++) g.Add(Judgement.Perfect); // 30
            Assert.AreEqual(1, g.Tier);
            Assert.AreEqual(1.5f, g.Multiplier);
            g.Add(Judgement.Miss); // 21
            Assert.AreEqual(0, g.Tier);
        }

        [Test]
        public void DecaysWhenIdle()
        {
            var g = new GrooveMeter();
            for (int i = 0; i < 10; i++) g.Add(Judgement.Perfect); // 60
            g.Tick(2.5f);   // idle passes 2 s delay → decays 12.5
            Assert.Less(g.Value, 60f);
        }
    }
}
