using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    public class StageRankTests
    {
        [TestCase(70.0, StageRank.S)]
        [TestCase(75.0, StageRank.S)]
        [TestCase(75.1, StageRank.A)]
        [TestCase(82.5, StageRank.A)]
        [TestCase(82.6, StageRank.B)]
        [TestCase(90.0, StageRank.B)]
        [TestCase(97.5, StageRank.C)]
        [TestCase(200.0, StageRank.C)]       // C is the lowest: anything slower is a fail too
        public void CliffsideTimeRanks(double seconds, StageRank expected)
        {
            Assert.AreEqual(expected, StageScoring.TimeRank(seconds, 75.0));
        }

        static PlaystyleLog Clean(int perfects, int enemies)
        {
            var log = new PlaystyleLog();
            for (int i = 0; i < perfects; i++) log.AddNote(Judgement.Perfect);
            for (int i = 0; i < enemies; i++) log.AddKill();
            return log;
        }

        [Test]
        public void CleanFastRunIsS()
        {
            var r = StageScoring.Score(70.0, Clean(14, 2), new StageTargets(75.0, 2, 14));
            Assert.AreEqual(StageRank.S, r.Rank);
            Assert.AreEqual(0, r.Caps.Count);
            Assert.AreEqual(1.0, r.Efficiency, 1e-9);
        }

        [Test]
        public void SAndANeedEveryEnemy()
        {
            var r = StageScoring.Score(70.0, Clean(14, 1), new StageTargets(75.0, 2, 14));
            Assert.AreEqual(StageRank.B, r.Rank);
            Assert.AreEqual(StageRank.S, r.TimeRank);
        }

        [Test]
        public void OneHitRulesOutSOnTheFirstStage()
        {
            var log = Clean(14, 2);
            log.AddHurt(1);
            Assert.AreEqual(StageRank.A, StageScoring.Score(70.0, log, new StageTargets(75.0, 2, 14)).Rank);
            // Later stages allow a few hits.
            Assert.AreEqual(StageRank.S, StageScoring.Score(70.0, log, new StageTargets(75.0, 2, 14, hitsAllowedForS: 2)).Rank);
        }

        [Test]
        public void TooManyHitsCapAtB()
        {
            var hurt = Clean(14, 2);
            hurt.MaxHearts = 5;
            for (int i = 0; i < 4; i++) hurt.AddHurt(1);          // 4 hits (A allows 3)
            Assert.AreEqual(StageRank.B, StageScoring.Score(70.0, hurt, new StageTargets(75.0, 2, 14)).Rank);
        }

        [Test]
        public void OffBeatPlayNoLongerGatesTheLetter()
        {
            var sloppy = Clean(4, 2);
            for (int i = 0; i < 10; i++) sloppy.AddNote(Judgement.OffBeat);
            Assert.AreEqual(StageRank.S, StageScoring.Score(70.0, sloppy, new StageTargets(75.0, 2, 14)).Rank);
        }

        [Test]
        public void ACIsAFail()
        {
            var log = Clean(1, 1);
            log.MaxHearts = 5;
            var r = StageScoring.Score(120.0, log, new StageTargets(75.0, 2, 14));
            Assert.AreEqual(StageRank.C, r.Rank);
            Assert.IsTrue(r.Failed);
            Assert.IsFalse(r.Clean);
        }

        [Test]
        public void ACleanClearCantFailOnTime()
        {
            var log = Clean(14, 2);
            log.MaxHearts = 5;
            log.AddHurt(2);                                        // 2 of 5: under half
            var r = StageScoring.Score(300.0, log, new StageTargets(75.0, 2, 14));
            Assert.IsTrue(r.Clean);
            Assert.AreEqual(StageRank.B, r.Rank);
            Assert.IsFalse(r.Failed);

            log.AddHurt(1);                                        // 3 of 5: half lost, not clean
            var r2 = StageScoring.Score(300.0, log, new StageTargets(75.0, 2, 14));
            Assert.IsFalse(r2.Clean);
            Assert.IsTrue(r2.Failed);
        }

        [Test]
        public void HalfHeartsRoundsUp()
        {
            var log = new PlaystyleLog { MaxHearts = 5 };
            log.AddHurt(2);
            Assert.IsFalse(log.HalfHeartsLost);
            log.AddHurt(1);
            Assert.IsTrue(log.HalfHeartsLost);
            var six = new PlaystyleLog { MaxHearts = 6 };
            six.AddHurt(3);
            Assert.IsTrue(six.HalfHeartsLost);
        }
    }

    public class PlaystyleLogTests
    {
        [Test]
        public void TallyCountsParriesAsPerfectAndBlocksAndDodgesAsGreat()
        {
            var log = new PlaystyleLog();
            log.AddNote(Judgement.Perfect);
            log.AddDefense(DefenseResult.Parried);
            log.AddDefense(DefenseResult.Blocked);
            log.AddDefense(DefenseResult.Dodged);
            log.AddDefense(DefenseResult.Hit);          // a hit is counted by AddHurt, not here
            Assert.AreEqual(2, log.TallyCount(Judgement.Perfect));
            Assert.AreEqual(2, log.TallyCount(Judgement.Great));
            Assert.AreEqual(4, log.TallyTotal);
            Assert.AreEqual(1.0, log.ShareAtLeast(Judgement.Great), 1e-9);
        }

        [Test]
        public void TruePerfectCountsAsThreePerfectsInMastery()
        {
            var allPerfect = new PlaystyleLog();
            for (int i = 0; i < 4; i++) allPerfect.AddNote(Judgement.Perfect);
            Assert.AreEqual(100.0, allPerfect.MasteryScore, 1e-9);

            var mixed = new PlaystyleLog();
            mixed.AddNote(Judgement.OffBeat);
            mixed.AddNote(Judgement.Perfect);
            double without = mixed.MasteryScore;               // 1 of 2 = 50
            mixed.AddNote(Judgement.TruePerfect);              // +3 Perfect-equivalents
            Assert.AreEqual(50.0, without, 1e-9);
            Assert.AreEqual(80.0, mixed.MasteryScore, 1e-9);   // 4 of 5
            Assert.AreEqual(3, mixed.Attacks);
        }
    }

    public class CalibrationMathTests
    {
        [Test]
        public void MedianIgnoresAFumbledTap()
        {
            var taps = new List<double> { 0.040, 0.042, 0.038, 0.041, 0.039, 0.200 };
            var r = CalibrationMath.Summarize(taps);
            Assert.AreEqual(40.5, r.OffsetMs, 1e-6);
            Assert.IsTrue(r.Consistent);
        }

        [Test]
        public void WideSpreadIsFlagged()
        {
            var r = CalibrationMath.Summarize(new List<double> { 0.0, 0.05, -0.05, 0.06, -0.04 });
            Assert.IsFalse(r.Consistent);
        }
    }
}
