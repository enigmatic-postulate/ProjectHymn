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
    }
}
