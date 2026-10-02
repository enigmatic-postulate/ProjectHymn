using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;

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
        [TestCase(1.20, Judgement.OffBeat)]
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
        public void OffBeatBreaksPhrase()
        {
            Play(0, S);
            _engine.RegisterOffBeat(_map.GetBeat(1));
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
            Assert.AreEqual(1.5f, g.ScoreMultiplier);
            Assert.AreEqual(1.0833f, g.DamageMultiplier);
            g.Add(Judgement.OffBeat); // 21
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

    public class DamageTableTests
    {
        [TestCase(Judgement.Perfect, 1.15f)]
        [TestCase(Judgement.Great, 1.10f)]
        [TestCase(Judgement.Good, 1.0f)]
        [TestCase(Judgement.OffBeat, 1.0f)]
        public void SmallDifferencesOnly(Judgement j, float expected)
        {
            Assert.AreEqual(expected, BeatJudge.DamageMultiplier(j));
        }
    }

    public class StaminaMeterTests
    {
        [Test]
        public void OffBeatActionsSpend()
        {
            var s = new StaminaMeter();
            Assert.IsTrue(s.Spend(BeatAction.Smash));
            Assert.AreEqual(75f, s.Value);
        }

        [Test]
        public void EmptyStaminaLeavesPlayerWinded()
        {
            var s = new StaminaMeter();
            for (int i = 0; i < 4; i++) s.Spend(BeatAction.Smash); // 100 -> 0
            Assert.IsFalse(s.Spend(BeatAction.Strike));
            Assert.AreEqual(0f, s.Value);
        }

        [Test]
        public void RegenWaitsForDelay()
        {
            var s = new StaminaMeter();
            s.Spend(BeatAction.Smash);           // 75
            s.Tick(0.5f);                        // still inside 0.6 s delay
            Assert.AreEqual(75f, s.Value);
            s.Tick(0.5f);                        // past delay: +17.5
            Assert.AreEqual(92.5f, s.Value);
            s.NotifyAction();                    // an on-beat action pauses regen again
            s.Tick(0.3f);
            Assert.AreEqual(92.5f, s.Value);
        }

        [Test]
        public void GuardIsFree()
        {
            var s = new StaminaMeter();
            Assert.IsTrue(s.Spend(BeatAction.Guard));
            Assert.AreEqual(100f, s.Value);
        }
    }

    public class AttackTimingTests
    {
        static readonly FrameData Strike = new FrameData(0.05f, 0.07f, 0.30f);
        readonly TimingWindows _w = TimingWindows.Default;

        [Test]
        public void PerfectRecoversFasterThanOffBeat()
        {
            var p = AttackTiming.Compute(Strike, Judgement.Perfect, false, 1.0, 1.5, _w);
            var o = AttackTiming.Compute(Strike, Judgement.OffBeat, false, 1.0, 1.5, _w);
            Assert.AreEqual(1.0 + 0.12 + 0.12, p.RecoveryEnd, 1e-6);
            Assert.AreEqual(1.0 + 0.12 + 0.30, o.RecoveryEnd, 1e-6);
            Assert.IsTrue(p.Flourish);
            Assert.IsFalse(o.Flourish);
        }

        [Test]
        public void OnBeatOpensCancelBeforeNextBeat()
        {
            var smash = new FrameData(0.08f, 0.08f, 0.42f);       // 0.58 s, longer than a 0.5 s beat
            var g = AttackTiming.Compute(smash, Judgement.Good, false, 1.0, 1.5, _w);
            Assert.AreEqual(1.5 - 0.090, g.CancelOpen, 1e-6);     // Good: cancel from next beat's Great window
            Assert.IsTrue(g.CanAct(1.42));
            Assert.IsFalse(g.CanAct(1.40));
            var pf = AttackTiming.Compute(smash, Judgement.Perfect, false, 1.0, 1.5, _w);
            Assert.AreEqual(1.5 - 0.135, pf.CancelOpen, 1e-6);    // Perfect: from next beat's Good window
        }

        [Test]
        public void OffBeatHasNoCancelSoNextBeatIsMissed()
        {
            var smash = new FrameData(0.08f, 0.08f, 0.42f);
            var o = AttackTiming.Compute(smash, Judgement.OffBeat, false, 1.2, 1.5, _w);
            Assert.IsFalse(o.HasCancel);
            Assert.IsFalse(o.CanAct(1.5 + 0.135));               // locked through the whole next-beat window
        }

        [Test]
        public void CancelNeverBeforeActiveFramesEnd()
        {
            var l = AttackTiming.Compute(Strike, Judgement.Perfect, false, 1.0, 1.1, _w);
            Assert.AreEqual(l.ActiveEnd, l.CancelOpen, 1e-9);
        }

        [Test]
        public void WindedRecoversSlower()
        {
            var w = AttackTiming.Compute(Strike, Judgement.OffBeat, true, 0.0, 0.5, _w);
            Assert.AreEqual(0.12 + 0.30 * 1.75, w.RecoveryEnd, 1e-6);
            Assert.IsTrue(w.Winded);
        }
    }

    public class ScoreKeeperTests
    {
        [Test]
        public void GrooveScalesScore()
        {
            var s = new ScoreKeeper();
            s.AddJudgement(Judgement.Perfect, 1f);
            s.AddJudgement(Judgement.Perfect, 3f);
            s.AddJudgement(Judgement.OffBeat, 1f);
            Assert.AreEqual(300 + 900 + 10, s.Total);
        }

        [Test]
        public void PhraseAndFinisherBonuses()
        {
            var s = new ScoreKeeper();
            s.AddPhrase(3, 2f);      // 150*3*2
            s.AddFinisher(7, 1f);    // 250*7
            Assert.AreEqual(900 + 1750, s.Total);
        }
    }

    public class EnemyRhythmTests
    {
        // 4 bars 4/4 @120 (beats 0-15, 0.5 s each), then 2 bars 3/4 @120 (beats 16-21)
        readonly TempoMap _map = new TempoMap(new List<TempoSection> { new TempoSection(4, 120f, 4), new TempoSection(2, 120f, 3) }, false);

        [Test]
        public void MeasureStartBeatAcrossMeterChange()
        {
            Assert.AreEqual(0L, _map.MeasureStartBeat(0));
            Assert.AreEqual(12L, _map.MeasureStartBeat(3));
            Assert.AreEqual(16L, _map.MeasureStartBeat(4));
            Assert.AreEqual(19L, _map.MeasureStartBeat(5));
        }

        [Test]
        public void MeasureStartBeatLoops()
        {
            var m = new TempoMap(new List<TempoSection> { new TempoSection(2, 120f, 4) }, true);
            Assert.AreEqual(8L, m.MeasureStartBeat(2));
            Assert.AreEqual(12L, m.MeasureStartBeat(3));
        }

        [Test]
        public void SlimeAttacksEveryDownbeatWithOneMeasureTelegraph()
        {
            var slime = new EnemyPattern(1, 0, 0f);
            var a = EnemyRhythm.Next(_map, slime, 0.0).Value;
            Assert.AreEqual(4.0, a.BeatPosition, 1e-9);
            Assert.AreEqual(2.0, a.Time, 1e-9);
            Assert.AreEqual(0.0, a.TelegraphStart, 1e-9);
            Assert.IsTrue(a.IsOnBeat);
        }

        [Test]
        public void ImpHitsTheBackbeat()
        {
            var imp = new EnemyPattern(1, 0, 1f, 3f);
            var a = EnemyRhythm.Next(_map, imp, 0.0).Value;
            var b = EnemyRhythm.Next(_map, imp, a.BeatPosition).Value;
            Assert.AreEqual(1.0, a.BeatPosition, 1e-9);
            Assert.AreEqual(3.0, b.BeatPosition, 1e-9);
        }

        [Test]
        public void KnightEveryOtherMeasure()
        {
            var knight = new EnemyPattern(2, 1, 0f);
            var a = EnemyRhythm.Next(_map, knight, 0.0).Value;
            var b = EnemyRhythm.Next(_map, knight, a.BeatPosition).Value;
            Assert.AreEqual(1L, a.Measure);
            Assert.AreEqual(3L, b.Measure);
        }

        [Test]
        public void BatEighthNoteBurst()
        {
            var bat = new EnemyPattern(1, 0, 2f, 2.5f, 3f);
            var a = EnemyRhythm.Next(_map, bat, 0.0).Value;
            var b = EnemyRhythm.Next(_map, bat, a.BeatPosition).Value;
            Assert.AreEqual(2.5, b.BeatPosition, 1e-9);
            Assert.IsFalse(b.IsOnBeat);
            Assert.AreEqual(1.25, b.Time, 1e-9);
        }

        [Test]
        public void SkipsOffsetsMissingInShorterMeter()
        {
            var imp = new EnemyPattern(1, 0, 1f, 3f);
            var a = EnemyRhythm.Next(_map, imp, 16.5).Value;   // 3/4 bar starting at beat 16: only offset 1 exists
            var b = EnemyRhythm.Next(_map, imp, a.BeatPosition).Value;
            Assert.AreEqual(17.0, a.BeatPosition, 1e-9);
            Assert.AreEqual(20.0, b.BeatPosition, 1e-9);        // next 3/4 bar's beat 2, not beat 19
            Assert.AreEqual(3, b.BeatsPerMeasure);
        }

        [Test]
        public void NewSpawnGetsAFullTelegraph()
        {
            var slime = new EnemyPattern(1, 0, 0f);
            var a = EnemyRhythm.FirstFullyTelegraphed(_map, slime, 0.7).Value; // mid-way through bar 0
            Assert.IsTrue(a.TelegraphStart >= 0.7);
            Assert.AreEqual(8.0, a.BeatPosition, 1e-9);
        }
    }

    public class ParryJudgeTests
    {
        readonly TimingWindows _w = TimingWindows.Default;

        [Test]
        public void ParryWithinGoodWindow()
        {
            Assert.AreEqual(DefenseResult.Parried, ParryJudge.Resolve(true, false, 1.10, 1.0, _w, false));
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, false, 1.20, 1.0, _w, false));
        }

        [Test]
        public void HeavyNeedsTighterTiming()
        {
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, false, 1.11, 1.0, _w, true));
            Assert.AreEqual(DefenseResult.Parried, ParryJudge.Resolve(true, false, 1.08, 1.0, _w, true));
        }

        [Test]
        public void DodgeAndRange()
        {
            Assert.AreEqual(DefenseResult.Dodged, ParryJudge.Resolve(true, true, -10, 1.0, _w, false));
            Assert.AreEqual(DefenseResult.OutOfRange, ParryJudge.Resolve(false, false, 1.0, 1.0, _w, false));
        }
    }

    public class PlaystyleMetricsTests
    {
        static BeatInfo Beat(long measure, int bpm = 4) => new BeatInfo(measure * bpm, measure, 0, bpm, 4, 120, 0, 0);

        [Test]
        public void BlockAndDodgeRatesIgnoreOutOfRange()
        {
            var m = new PlaystyleMetrics();
            m.RecordDefense(DefenseResult.Parried);
            m.RecordDefense(DefenseResult.Dodged);
            m.RecordDefense(DefenseResult.Hit);
            m.RecordDefense(DefenseResult.Hit);
            m.RecordDefense(DefenseResult.OutOfRange);
            Assert.AreEqual(0.25, m.BlockRate, 1e-9);
            Assert.AreEqual(0.25, m.DodgeRate, 1e-9);
            Assert.AreEqual(1, m.AttacksAvoidedByPosition);
        }

        [Test]
        public void AggressivenessBlendsRateAndProximity()
        {
            var m = new PlaystyleMetrics();
            for (int i = 0; i < 30; i++) m.RecordAction(BeatAction.Strike, Judgement.Great, Beat(0));
            m.Tick(30, true, "Blade");   // 30 attacks in 30 s near enemies
            m.Tick(30, false, "Blade");  // 30 more seconds away
            Assert.AreEqual(30.0, m.AttacksPerMinute, 1e-9);
            Assert.AreEqual(0.5 * 0.5 + 0.5 * 0.5, m.Aggressiveness, 1e-9);
            Assert.AreEqual("Blade", m.PreferredWeapon);
        }

        [Test]
        public void MeterAdaptationComparesPostChangeAccuracy()
        {
            var m = new PlaystyleMetrics();
            for (int i = 0; i < 8; i++) m.RecordAction(BeatAction.Strike, Judgement.Perfect, Beat(0)); // 8/8 before
            m.RecordMeterChange(Beat(4, 3));
            m.RecordAction(BeatAction.Strike, Judgement.Perfect, Beat(4, 3));
            m.RecordAction(BeatAction.Strike, Judgement.OffBeat, Beat(5, 3));
            m.RecordAction(BeatAction.Strike, Judgement.Perfect, Beat(6, 3));  // outside the 2-bar window
            // after change: 1/2 = 0.5; overall 10/11
            Assert.AreEqual(0.5 / (10.0 / 11.0), m.MeterAdaptation, 1e-9);
            Assert.AreEqual(2.0 / 3.0, m.MeterAccuracy("3/4"), 1e-9);
        }

        [Test]
        public void JsonHasAllFiveSignals()
        {
            var m = new PlaystyleMetrics();
            var json = m.ToJson("BattleTheme1", new System.DateTime(2026, 10, 2, 0, 0, 0, System.DateTimeKind.Utc));
            foreach (var k in new[] { "meterAdaptation", "blockRate", "aggressiveness", "preferredWeapon", "dodgeRate" })
                Assert.IsTrue(json.Contains("\"" + k + "\""));
            Assert.IsTrue(json.Contains("\"meterAdaptation\": null"));
        }
    }

    public class AdaptiveTimelineTests
    {
        // Placeholder Battle Theme 2: 4 bars 4/4 @150, 4 bars 3/4 @150, 4 bars 7/8 @165
        static readonly List<TempoSection> Sections = new List<TempoSection>
        {
            new TempoSection(4, 150f, 4), new TempoSection(4, 150f, 3), new TempoSection(4, 165f, 7, 8),
        };
        static readonly List<SongPhase> Phases = new List<SongPhase>
        {
            new SongPhase("Overture", 0, 1), new SongPhase("Waltz", 1, 1), new SongPhase("Finale", 2, 1),
        };
        const double Bar44 = 4 * 0.4;   // 1.6 s
        const double Bar34 = 3 * 0.4;   // 1.2 s

        [Test]
        public void LoopsCurrentPhaseUntilAsked()
        {
            var t = new AdaptiveTimeline(Sections, Phases);
            t.Extend(20.0);
            foreach (var b in t.Blocks) Assert.AreEqual(0, b.Phase);
            Assert.AreEqual(0, t.PhaseAt(15.0));
            Assert.AreEqual(4, t.Map.GetBeat(16 * 3).BeatsPerMeasure);   // still 4/4 three loops later
            Assert.AreEqual(1, t.Map.SectionCount);                        // identical loops merged into one segment
        }

        [Test]
        public void PhaseChangeLandsOnNextBlockBoundary()
        {
            var t = new AdaptiveTimeline(Sections, Phases);
            t.Extend(1.0);                    // block 0: 0 - 6.4 s
            t.RequestPhase(1);
            t.Extend(10.0);                   // block 1 is the Waltz
            Assert.AreEqual(1, t.Blocks[1].Phase);
            Assert.AreEqual(4 * Bar44, t.Blocks[1].StartTime, 1e-9);
            Assert.AreEqual(0, t.PhaseAt(6.3));
            Assert.AreEqual(1, t.PhaseAt(6.5));
            var b = t.Map.GetBeat(16);        // first beat after the switch
            Assert.IsTrue(b.IsDownbeat);
            Assert.AreEqual(3, b.BeatsPerMeasure);
            Assert.AreEqual(6.4, b.Time, 1e-9);
        }

        [Test]
        public void SourceRangesPointIntoTheOriginalSong()
        {
            var t = new AdaptiveTimeline(Sections, Phases);
            t.RequestPhase(2);
            t.Extend(7.0);
            var fin = t.Blocks[1];
            Assert.AreEqual(2, fin.Phase);
            Assert.AreEqual(4 * Bar44 + 4 * Bar34, fin.SourceStart, 1e-9); // 11.2 s into the linear song
            Assert.AreEqual(fin.SourceStart + 4 * 7 * 60.0 / 165.0, fin.SourceEnd, 1e-9);
            Assert.AreEqual(fin.SourceEnd - fin.SourceStart, fin.Duration, 1e-9);
        }

        [Test]
        public void NextPhaseChangeIsVisibleAheadOfTime()
        {
            var t = new AdaptiveTimeline(Sections, Phases);
            t.RequestPhase(1);
            t.Extend(7.0);
            var next = t.NextPhaseChangeAfter(5.0);
            Assert.IsTrue(next.HasValue);
            Assert.AreEqual(1, next.Value.Phase);
            Assert.AreEqual(6.4, next.Value.StartTime, 1e-9);
        }

        [Test]
        public void EnemiesScheduleCorrectlyAcrossTheSwitch()
        {
            var t = new AdaptiveTimeline(Sections, Phases);
            t.RequestPhase(1);
            t.Extend(12.0);
            var imp = new EnemyPattern(1, 0, 1f, 3f);                      // beat 4 doesn't exist in 3/4
            var a = EnemyRhythm.Next(t.Map, imp, 15.0).Value;              // first attack in the Waltz
            Assert.AreEqual(17.0, a.BeatPosition, 1e-9);
            var b = EnemyRhythm.Next(t.Map, imp, a.BeatPosition).Value;
            Assert.AreEqual(20.0, b.BeatPosition, 1e-9);
        }
    }

    public class ProgressionTests
    {
        [Test]
        public void NewGameHasStarterLoadout()
        {
            var d = SaveData.NewGame();
            Assert.AreEqual(3, d.equippedPhrases.Count);
            Assert.AreEqual("Blade", d.weaponId);
            Assert.IsTrue(d.ownedItems.Contains("CantorRobe"));
        }

        [Test]
        public void BuyingNeedsNotesAndOnlyOnce()
        {
            var r = new ProgressionRules(SaveData.NewGame());
            Assert.IsFalse(r.BuyItem("DrumHammer", 800));
            r.AddNotes(1000);
            Assert.IsTrue(r.BuyItem("DrumHammer", 800));
            Assert.AreEqual(200, r.Data.notes);
            Assert.IsFalse(r.BuyItem("DrumHammer", 0));
            Assert.IsTrue(r.EquipWeapon("DrumHammer"));
            Assert.IsFalse(r.EquipArmor("BrassPlate"));
        }

        [Test]
        public void SixPhraseLimitAndAtLeastOne()
        {
            var r = new ProgressionRules(SaveData.NewGame());
            foreach (var p in new[] { "Backbeat", "Waltz", "OddTime" }) r.UnlockPhrase(p); // 6 equipped
            Assert.AreEqual(6, r.Data.equippedPhrases.Count);
            r.UnlockPhrase("Extra");                                                      // learned, not equipped
            Assert.IsFalse(r.IsEquipped("Extra"));
            Assert.IsFalse(r.TogglePhrase("Extra"));                                      // full
            Assert.IsTrue(r.TogglePhrase("Waltz"));                                       // unequip
            Assert.IsTrue(r.TogglePhrase("Extra"));
            var one = new ProgressionRules(new SaveData { unlockedPhrases = { "A" }, equippedPhrases = { "A" } });
            Assert.IsFalse(one.TogglePhrase("A"));
        }

        [Test]
        public void ArenaRecordAwardsNotesPhraseAndRemix()
        {
            var r = new ProgressionRules(SaveData.NewGame());
            var first = r.RecordArena("arena1", Rank.S, 40000, "Backbeat");
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual("Backbeat", first.PhraseUnlocked);
            Assert.AreEqual(40000 / 200 + 300, first.NotesEarned);
            Assert.IsTrue(first.RemixUnlocked);
            var again = r.RecordArena("arena1", Rank.B, 10000, "Backbeat");
            Assert.IsFalse(again.FirstClear);
            Assert.IsNull(again.PhraseUnlocked);
            Assert.IsFalse(again.NewBestRank);
            Assert.AreEqual((int)Rank.S, r.Arena("arena1").bestRank);
            Assert.AreEqual(40000L, r.Arena("arena1").bestScore);
        }

        [Test]
        public void LoreIsFoundOnce()
        {
            var r = new ProgressionRules(SaveData.NewGame());
            Assert.IsTrue(r.FindLore("verse1", 100));
            Assert.IsFalse(r.FindLore("verse1", 100));
            Assert.AreEqual(100, r.Data.notes);
        }

        [TestCase(0.95, 85f, 0, Rank.S)]
        [TestCase(0.95, 60f, 0, Rank.A)]
        [TestCase(0.82, 55f, 0, Rank.A)]
        [TestCase(0.70, 90f, 0, Rank.B)]
        [TestCase(0.55, 10f, 0, Rank.C)]
        [TestCase(0.30, 10f, 0, Rank.D)]
        [TestCase(0.95, 85f, 2, Rank.B)]
        [TestCase(0.30, 10f, 3, Rank.D)]
        public void RankFromAccuracyGrooveAndDowns(double acc, float peak, int downs, Rank expected)
        {
            Assert.AreEqual(expected, RankCalculator.Calculate(acc, peak, downs));
        }
    }
}
