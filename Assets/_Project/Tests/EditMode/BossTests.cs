using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Movement;

namespace ProjectHymn.Tests
{
    public class BossTests
    {
        /// <summary>The summit theme's shape: 8 bars of 4/4, 8 of 3/4, 8 of 7/8 (eighth-note beats), looping.</summary>
        static TempoMap Summit() => new TempoMap(new List<TempoSection>
        {
            new TempoSection(8, 150f, 4), new TempoSection(8, 150f, 3), new TempoSection(8, 300f, 7, 8),
        }, true);

        static readonly List<BossMove> Moves = new List<BossMove>
        {
            new BossMove(new EnemyAttackPattern("Gavel", new[] { 0.0 }, 4, 3, 2), 0),
            new BossMove(new EnemyAttackPattern("Four", new[] { 0.0, 1.0, 2.0, 3.0 }, 4, 2, 1), 4, 1),
            new BossMove(new EnemyAttackPattern("Waltz", new[] { 0.0, 1.0, 2.0 }, 3, 3, 1), 3),
            new BossMove(new EnemyAttackPattern("Limp", new[] { 0.0, 2.0, 4.0 }, 7, 3, 1), 7),
        };

        [Test]
        public void SectionsStartWhereTheMeterChanges()
        {
            var m = Summit();
            Assert.AreEqual(0, BossPlanner.SectionStart(m, 20));
            Assert.AreEqual(32, BossPlanner.SectionStart(m, 40));       // 8 bars of 4/4 = 32 beats
            Assert.AreEqual(56, BossPlanner.SectionStart(m, 70));       // + 8 bars of 3/4 = 24 beats
            Assert.AreEqual(112, BossPlanner.SectionStart(m, 112 + 5)); // the loop starts again after 56 eighths
        }

        [Test]
        public void CueWarnsInTheBarBeforeAMeterChange()
        {
            var m = Summit();
            var calm = BossPlanner.Cue(m, 20.0);
            Assert.IsFalse(calm.Changing);
            var warn = BossPlanner.Cue(m, 29.0);                          // last bar of 4/4: beats 28..31
            Assert.IsTrue(warn.Changing);
            Assert.AreEqual("4/4", warn.Current);
            Assert.AreEqual("3/4", warn.Next);
            Assert.AreEqual(3, warn.BeatsLeft);
            var toSeven = BossPlanner.Cue(m, 53.5);                       // last bar of 3/4: beats 53..55
            Assert.AreEqual("7/8", toSeven.Next);
            var back = BossPlanner.Cue(m, 105.0);                         // last bar of 7/8: beats 105..111
            Assert.AreEqual("4/4", back.Next);
            Assert.AreEqual(7, back.BeatsLeft);
        }

        [Test]
        public void ACountInNeverStraddlesAMeterChange()
        {
            var m = Summit();
            // From beat 27 a one-bar count-in would run 29-31 in 4/4 and land on 32 in 3/4: it waits for the new section.
            var slot = BossPlanner.Slot(m, 27.0, 0);
            Assert.AreEqual(m.GetBeat((long)slot.TelegraphBeat).SectionIndex, m.GetBeat((long)slot.FirstHitBeat).SectionIndex);
            Assert.AreEqual(35.0, slot.FirstHitBeat, 1e-9);              // telegraph 32..34 in 3/4, hit on the next downbeat
            Assert.AreEqual(32.0, slot.TelegraphBeat, 1e-9);
        }

        [Test]
        public void MovesFitTheMeterTheyLandIn()
        {
            var m = Summit();
            int turn = 0;
            var four = BossPlanner.Plan(m, Moves, 0, ref turn, 1.0, 0);
            Assert.AreEqual("Gavel", four.Pattern.Name);                 // phase 0 in 4/4: only the any-meter Gavel
            turn = 0;
            var waltz = BossPlanner.Plan(m, Moves, 0, ref turn, 33.0, 0);
            Assert.AreEqual(3, m.GetBeat((long)waltz.FirstHitBeat).BeatsPerMeasure);
            Assert.IsTrue(waltz.Pattern.Name == "Gavel" || waltz.Pattern.Name == "Waltz");
            turn = 1;
            var limp = BossPlanner.Plan(m, Moves, 0, ref turn, 57.0, 0);
            Assert.AreEqual(7, m.GetBeat((long)limp.FirstHitBeat).BeatsPerMeasure);
            Assert.AreEqual("Limp", limp.Pattern.Name);
            Assert.AreEqual(7.0, limp.FirstHitBeat - limp.TelegraphBeat, 1e-9);   // a 7/8 bar of count-in
        }

        [Test]
        public void LaterPhasesAddMoves()
        {
            var m = Summit();
            var seen = new HashSet<string>();
            int turn = 0;
            for (int i = 0; i < 6; i++) seen.Add(BossPlanner.Plan(m, Moves, 1, ref turn, 1.0, 0).Pattern.Name);
            Assert.IsTrue(seen.Contains("Four"));
            Assert.IsTrue(seen.Contains("Gavel"));
            Assert.IsFalse(seen.Contains("Waltz"));
        }

        [Test]
        public void HitsStayInsideTheSection()
        {
            var m = Summit();
            int turn = 0;
            for (double now = 0; now < 112; now += 1.7)
            {
                var a = BossPlanner.Plan(m, Moves, 2, ref turn, now, 0);
                int s = m.GetBeat((long)a.FirstHitBeat).SectionIndex;
                Assert.AreEqual(s, m.GetBeat((long)a.TelegraphBeat).SectionIndex, $"from {now}");
                Assert.AreEqual(s, m.GetBeat((long)a.LastHitBeat).SectionIndex, $"from {now}");
            }
        }

        [TestCase(900f, 900f, 0)]
        [TestCase(601f, 900f, 0)]
        [TestCase(599f, 900f, 1)]
        [TestCase(301f, 900f, 1)]
        [TestCase(299f, 900f, 2)]
        public void PhasesFollowHealthInThirds(float hp, float max, int phase)
        {
            Assert.AreEqual(phase, BossPlanner.PhaseFor(hp, max, 3));
        }

        [TestCase(200f, 0)]
        [TestCase(101f, 0)]
        [TestCase(99f, 1)]
        [TestCase(1f, 1)]
        public void TheSliceBossHasTwoPhases(float hp, int phase)
        {
            Assert.AreEqual(phase, BossPlanner.PhaseFor(hp, 200f));
        }

        [Test]
        public void ShockwavesCantBeGuarded()
        {
            // An unblockable hit resolves with no guard press (the guard passes NaN): a perfect press would not save you.
            var w = TimingWindows.Default;
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, double.NaN, 10.0, w));
            Assert.AreEqual(DefenseResult.Dodged, ParryJudge.Resolve(true, double.NaN, 10.0, w, 9.9, 0.25));
            Assert.IsTrue(new EnemyAttackPattern("Shockwave", new[] { 0.0 }, 4, 3, 2).WithUnblockable().Unblockable);
        }

        [Test]
        public void SummitIsAStageWithABoss()
        {
            var s = StageLayout.Summit;
            int bosses = 0;
            foreach (var mk in s.MarkersOf(CliffsideLayout.MarkerKind.Boss)) bosses++;
            Assert.AreEqual(1, bosses);
            Assert.AreEqual(1, s.EnemyCount);
        }
    }
}
