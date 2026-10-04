using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;

namespace ProjectHymn.Tests
{
    public class EnemySchedulerTests
    {
        static TempoMap Map44() => new TempoMap(new List<TempoSection> { new TempoSection(16, 120f, 4) }, false);
        static TempoMap Map34() => new TempoMap(new List<TempoSection> { new TempoSection(16, 120f, 3) }, false);
        static readonly EnemyAttackPattern Cut = new EnemyAttackPattern("Cut", new[] { 0.0 }, 4, 2, 2);
        static readonly EnemyAttackPattern Flurry = new EnemyAttackPattern("Flurry", new[] { 0.0, 1.0 }, 4, 2, 1);

        [Test]
        public void FirstHitLandsOnADownbeatAFullBarAhead()
        {
            var a = EnemyScheduler.Plan(Map44(), Cut, nowBeat: 1.3, notBeforeBeat: 0);
            Assert.AreEqual(8.0, a.FirstHitBeat, 1e-9);           // 1.3 + 4 = 5.3 → next downbeat is beat 8
            Assert.GreaterOrEqual((float)a.TelegraphBeat, 1.3f);
        }

        [Test]
        public void ExactlyOnTimeKeepsTheDownbeat()
        {
            var a = EnemyScheduler.Plan(Map44(), Cut, 4.0, 0);
            Assert.AreEqual(8.0, a.FirstHitBeat, 1e-9);
        }

        [Test]
        public void RespectsRecovery()
        {
            var a = EnemyScheduler.Plan(Map44(), Cut, 0, notBeforeBeat: 9);
            Assert.AreEqual(16.0, a.FirstHitBeat, 1e-9);
        }

        [Test]
        public void FollowsTheMeter()
        {
            var a = EnemyScheduler.Plan(Map34(), Cut, 0.5, 0);
            Assert.AreEqual(6.0, a.FirstHitBeat, 1e-9);           // 4.5 → downbeats every 3 beats: 6
        }

        [Test]
        public void FlurryPhasesAndCountIn()
        {
            var a = EnemyScheduler.Plan(Map44(), Flurry, 0, 0);
            Assert.AreEqual(4.0, a.FirstHitBeat, 1e-9);
            Assert.AreEqual(5.0, a.LastHitBeat, 1e-9);
            Assert.AreEqual(7.0, a.EndBeat, 1e-9);
            Assert.AreEqual(AttackPhase.Waiting, a.PhaseAt(-0.5));
            Assert.AreEqual(AttackPhase.Telegraph, a.PhaseAt(1.0));
            Assert.AreEqual(AttackPhase.Active, a.PhaseAt(4.5));
            Assert.AreEqual(AttackPhase.Recover, a.PhaseAt(6.0));
            Assert.AreEqual(AttackPhase.Done, a.PhaseAt(7.0));
            Assert.AreEqual(3, a.BeatsUntilNextHit(1.0));
            Assert.AreEqual(1, a.BeatsUntilNextHit(3.2));
            Assert.AreEqual(1, a.BeatsUntilNextHit(4.5));
            Assert.AreEqual(-1, a.BeatsUntilNextHit(5.5));
            Assert.AreEqual(0.5, a.TelegraphProgress(2.0), 1e-9);
        }
    }

    public class ParryJudgeTests
    {
        static readonly TimingWindows W = TimingWindows.Default;

        [Test]
        public void GradesByDistanceFromTheHit()
        {
            Assert.AreEqual(DefenseResult.Parried, ParryJudge.Resolve(true, 10.03, 10.0, W));
            Assert.AreEqual(DefenseResult.Parried, ParryJudge.Resolve(true, 9.92, 10.0, W));
            Assert.AreEqual(DefenseResult.Blocked, ParryJudge.Resolve(true, 10.12, 10.0, W));
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, 10.2, 10.0, W));
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, double.NaN, 10.0, W));
        }

        [Test]
        public void OutOfRangeWins()
        {
            Assert.AreEqual(DefenseResult.OutOfRange, ParryJudge.Resolve(false, 10.0, 10.0, W));
        }

        [Test]
        public void ResolvesAfterTheGoodWindow()
        {
            Assert.AreEqual(10.135, ParryJudge.ResolveTime(10.0, W), 1e-6);
        }
    }

    public class VitalityTests
    {
        [Test]
        public void GraceIgnoresRepeatHits()
        {
            var v = new Vitality(5) { GraceSeconds = 0.8 };
            Assert.IsTrue(v.Damage(2, 1.0));
            Assert.IsFalse(v.Damage(1, 1.5));
            Assert.IsTrue(v.Damage(1, 2.0));
            Assert.AreEqual(2, v.Hp);
        }

        [Test]
        public void DiesAndRefills()
        {
            var v = new Vitality(2);
            v.Damage(5, 0);
            Assert.IsTrue(v.IsDead);
            Assert.IsFalse(v.Damage(1, 10));
            v.Refill();
            Assert.AreEqual(2, v.Hp);
            Assert.IsTrue(v.Damage(1, 0.1), "refill clears grace");
        }
    }
}
