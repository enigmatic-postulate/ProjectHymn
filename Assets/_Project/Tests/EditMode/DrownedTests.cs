using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;

namespace ProjectHymn.Tests
{
    /// <summary>The Drowned Nave's compound meters and enemies (VS-6).</summary>
    public class DrownedTests
    {
        // The stages' song: 6/8, eighth = 225 (a beat is an eighth note).
        static TempoMap Nave() => new TempoMap(new List<TempoSection> { new TempoSection(24, 225f, 6, 8) }, true);

        // The boss's song: 6/8, then a 3/4 hemiola at quarter = 112.5 (the same bar), then 9/8.
        static TempoMap Precentor() => new TempoMap(new List<TempoSection>
        {
            new TempoSection(8, 225f, 6, 8), new TempoSection(8, 112.5f, 3), new TempoSection(8, 225f, 9, 8),
        }, true);

        [Test]
        public void SixEightIsCountedInEighths()
        {
            var map = Nave();
            var b = map.GetBeat(6);
            Assert.IsTrue(b.IsDownbeat, "a bar is six eighths");
            Assert.AreEqual(6, b.BeatsPerMeasure);
            Assert.AreEqual("6/8", new TempoSection(1, 225f, 6, 8).Signature);
            Assert.AreEqual(1.6, map.BeatToTime(6), 1e-9, "a bar lasts 1.6 s");
        }

        [Test]
        public void TheHemiolaKeepsTheBarLength()
        {
            var map = Precentor();
            Assert.AreEqual(12.8, map.BeatToTime(48), 1e-6, "eight bars of 6/8");
            Assert.AreEqual(25.6, map.BeatToTime(48 + 24), 1e-6, "eight bars of 3/4 at 112.5 take as long");
            Assert.AreEqual(3, map.GetBeat(48).BeatsPerMeasure);
            Assert.AreEqual(9, map.GetBeat(72).BeatsPerMeasure);
        }

        [Test]
        public void AttacksCountInForOneBarOfSixEight()
        {
            var map = Nave();
            var toll = DrownedPatterns.Jelly()[1];          // Double Toll: the two big beats
            CollectionAssert.AreEqual(new[] { 0.0, 3.0 }, toll.Hits);
            var plan = EnemyScheduler.Plan(map, toll, 2.5, double.NegativeInfinity, oneBarLead: true);
            Assert.IsTrue(map.GetBeat((long)plan.FirstHitBeat).IsDownbeat);
            Assert.AreEqual(6.0, plan.FirstHitBeat - plan.TelegraphBeat, 1e-9, "a bar of count-in: six eighths");
            Assert.AreEqual(plan.FirstHitBeat + 3.0, plan.HitBeat(1), 1e-9, "the second sting on beat four");
        }

        [Test]
        public void TheChoristerSingsThreeBoltsAndANote()
        {
            var p = DrownedPatterns.Chorister();
            Assert.AreEqual("Antiphon", p[0].Name);
            CollectionAssert.AreEqual(new[] { 0.0, 1.0, 2.0, 3.0 }, p[0].Hits);
            Assert.IsFalse(p[0].Unblockable, "every bolt can be guarded");
            Assert.AreEqual("Sustain", p[1].Name);
            Assert.IsTrue(p[1].Unblockable, "the floor ring is jumped or dodged");
            var k = DrownedPatterns.Knight();
            CollectionAssert.AreEqual(new[] { 0.0, 3.0 }, k[1].Hits, "the knight's flurry falls on the big beats");
            foreach (var x in new[] { DrownedPatterns.Jelly(), DrownedPatterns.Chorister(), k })
                foreach (var a in x) Assert.AreEqual(6, a.LeadBeats, a.Name + ": a bar of count-in");
        }

        static Dictionary<string, BossMove> ByName(List<BossMove> moves)
        {
            var d = new Dictionary<string, BossMove>();
            foreach (var m in moves) d[m.Pattern.Name] = m;
            return d;
        }

        /// <summary>Plan the Precentor's attacks back to back, three times through the canticle, in one verse; returns the patterns used.</summary>
        static List<string> FightThrough(int phase)
        {
            var map = Precentor();
            var moves = DrownedPatterns.Precentor();
            var meterOf = ByName(moves);
            var used = new List<string>();
            int turn = 0;
            double now = 0;
            for (int guard = 0; guard < 200 && now < 432; guard++)        // three times through the canticle
            {
                var plan = BossPlanner.Plan(map, moves, phase, ref turn, now, now);
                Assert.IsTrue(plan.IsValid, "an attack at beat " + now);
                var name = plan.Pattern.Name;
                var move = meterOf[name];
                long first = (long)System.Math.Round(plan.FirstHitBeat);
                var info = map.GetBeat(first);
                Assert.IsTrue(info.IsDownbeat, name + " lands on a downbeat");
                Assert.IsTrue(move.Meter == 0 || move.Meter == info.BeatsPerMeasure, name + " fits " + info.Signature);
                Assert.LessOrEqual(move.MinPhase, phase, name + " belongs to a later verse");
                long tele = (long)System.Math.Floor(plan.TelegraphBeat + 1e-6);
                Assert.AreEqual(info.SectionIndex, map.GetBeat(tele).SectionIndex, name + " is counted in, in the meter it lands in");
                for (int i = 0; i < plan.HitCount; i++)
                    Assert.AreEqual(info.SectionIndex, map.GetBeat((long)System.Math.Round(plan.HitBeat(i))).SectionIndex, name + " never crosses a change");
                used.Add(name);
                now = plan.HitBeat(plan.HitCount - 1) + plan.Pattern.RecoverBeats;
            }
            return used;
        }

        [Test]
        public void ThePrecentorFightsInEachMeterOfHisSong()
        {
            var first = FightThrough(0);
            CollectionAssert.Contains(first, "Rocking", "6/8: the two big beats");
            CollectionAssert.Contains(first, "Hemiola", "3/4: three quarters across the same bar");
            CollectionAssert.Contains(first, "Long Rock", "9/8: the three big beats");
            CollectionAssert.DoesNotContain(first, "Great Bell", "the bell waits for the second verse");
            var second = FightThrough(1);
            CollectionAssert.Contains(second, "Great Bell");
            CollectionAssert.Contains(second, "Riptide");
            CollectionAssert.DoesNotContain(second, "Twin Bells", "twin bells wait for the third verse");
            var third = FightThrough(2);
            CollectionAssert.Contains(third, "Twin Bells");
            CollectionAssert.Contains(third, "Hemiola Run");
            CollectionAssert.Contains(third, "Cross Current");
        }

        [Test]
        public void TheGreatBellCannotBeGuardedAndTheHemiolaKeepsTheBar()
        {
            var d = ByName(DrownedPatterns.Precentor());
            Assert.IsTrue(d["Great Bell"].Pattern.Unblockable);
            Assert.AreEqual(1, d["Great Bell"].MinPhase);
            foreach (var m in d.Values)
            {
                if (m.Pattern.Name == "Great Bell" || m.Pattern.Name == "Twin Bells") { Assert.IsTrue(m.Pattern.Unblockable, m.Pattern.Name); continue; }
                Assert.IsFalse(m.Pattern.Unblockable, m.Pattern.Name + " can be guarded");
                if (m.Meter != 0) Assert.Less(m.Pattern.Span, 2 * m.Meter, m.Pattern.Name + " fits inside two bars");
            }
            // The hemiola's three quarters take exactly as long as a bar of the 6/8 around it.
            var map = Precentor();
            Assert.AreEqual(map.BeatToTime(48) - map.BeatToTime(42), map.BeatToTime(51) - map.BeatToTime(48), 1e-6);
        }

        /// <summary>Play a run of notes on the given beats, in one meter; returns the sequences completed.</summary>
        static List<string> Play(string meter, (long beat, NoteButton note)[] notes)
        {
            var chain = new RhythmChain();
            var done = new List<string>();
            chain.SetSequences(new[]
            {
                new SequencePattern("Swell", new[] { NoteButton.Two, NoteButton.One, NoteButton.One }, null, "6/8"),
                new SequencePattern("Undertow", new[] { NoteButton.One, NoteButton.One, NoteButton.Two }, null, "6/8"),
                new SequencePattern("CrossingCuts", new[] { NoteButton.One, NoteButton.Two, NoteButton.One }, null, "4/4"),
            });
            chain.SequenceCompleted += p => done.Add(p.Name);
            foreach (var (beat, note) in notes) chain.Register(beat, note, onBeat: true, meter);
            return done;
        }

        [Test]
        public void SixEightSequencesPlayOnTheBigBeats()
        {
            // A beat is an eighth at 225, so consecutive eighths are 0.27 s apart: nobody plays a three-note sequence in
            // half a second. The chain carries across a dotted quarter, so the big beats (1 and 4) count.
            var onPulse = Play("6/8", new[] { (0L, NoteButton.Two), (3L, NoteButton.One), (6L, NoteButton.One) });
            CollectionAssert.AreEqual(new[] { "Swell" }, onPulse, "one, four, one of the next bar");
            var onEighths = Play("6/8", new[] { (0L, NoteButton.Two), (1L, NoteButton.One), (2L, NoteButton.One) });
            CollectionAssert.AreEqual(new[] { "Swell" }, onEighths, "straight eighths still chain for anyone that fast");
            var tooSlow = Play("6/8", new[] { (0L, NoteButton.Two), (4L, NoteButton.One), (8L, NoteButton.One) });
            Assert.AreEqual(0, tooSlow.Count, "four eighths apart is not a pulse of this meter");
        }

        [Test]
        public void SimpleMetersStillWantConsecutiveBeats()
        {
            var onBeats = Play("4/4", new[] { (0L, NoteButton.One), (1L, NoteButton.Two), (2L, NoteButton.One) });
            CollectionAssert.AreEqual(new[] { "CrossingCuts" }, onBeats);
            var skipped = Play("4/4", new[] { (0L, NoteButton.One), (2L, NoteButton.Two), (4L, NoteButton.One) });
            Assert.AreEqual(0, skipped.Count, "a gap of two quarters in 4/4 starts a new chain");
            Assert.AreEqual(1, Meters.PulseBeats("4/4"));
            Assert.AreEqual(3, Meters.PulseBeats("6/8"));
            Assert.AreEqual(3, Meters.PulseBeats("9/8"));
            Assert.IsTrue(Meters.IsPulseGap("7/8", 2), "7/8 groups as 2+2+3");
            Assert.IsTrue(Meters.IsPulseGap("7/8", 3));
            Assert.IsFalse(Meters.IsPulseGap("3/4", 3));
            Assert.IsTrue(Meters.IsCompound("9/8"));
            Assert.IsFalse(Meters.IsCompound("7/8"), "seven is odd, not compound");
        }
    }
}
