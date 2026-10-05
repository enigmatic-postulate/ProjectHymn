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
        public void OneBarCountInInWaltzTimeIsThreeBeats()
        {
            var a = EnemyScheduler.Plan(Map34(), Cut, 0.5, 0, oneBarLead: true);
            Assert.AreEqual(6.0, a.FirstHitBeat, 1e-9);           // 0.5 + 3 = 3.5 → next downbeat: 6
            Assert.AreEqual(3.0, a.TelegraphBeat, 1e-9);          // the count-in is the bar before: 3-2-1
            var b = EnemyScheduler.Plan(Map34(), Cut, 0.0, 0, oneBarLead: true);
            Assert.AreEqual(3.0, b.FirstHitBeat, 1e-9);
            Assert.AreEqual(0.0, b.TelegraphBeat, 1e-9);
        }

        [Test]
        public void OneBarCountInInCommonTimeMatchesTheOldLead()
        {
            var a = EnemyScheduler.Plan(Map44(), Cut, 1.3, 0, oneBarLead: true);
            var b = EnemyScheduler.Plan(Map44(), Cut, 1.3, 0);
            Assert.AreEqual(b.FirstHitBeat, a.FirstHitBeat, 1e-9);
            Assert.AreEqual(b.TelegraphBeat, a.TelegraphBeat, 1e-9);
        }

        [Test]
        public void WaltzPatternHitsEveryBeatOfTheBar()
        {
            var waltz = new EnemyAttackPattern("Waltz", new[] { 0.0, 1.0, 2.0 }, 3, 3, 1);
            var a = EnemyScheduler.Plan(Map34(), waltz, 0.2, 0, oneBarLead: true);
            Assert.AreEqual(6.0, a.FirstHitBeat, 1e-9);
            Assert.AreEqual(8.0, a.LastHitBeat, 1e-9);           // 1-2-3 of one bar
            Assert.AreEqual(11.0, a.EndBeat, 1e-9);              // a bar of recovery
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
            Assert.AreEqual(DefenseResult.Parried, ParryJudge.Resolve(true, 10.03, 10.0, W));     // Perfect parries
            Assert.AreEqual(DefenseResult.Parried, ParryJudge.Resolve(true, 9.96, 10.0, W));
            Assert.AreEqual(DefenseResult.Blocked, ParryJudge.Resolve(true, 9.92, 10.0, W));      // Great only blocks now
            Assert.AreEqual(DefenseResult.Blocked, ParryJudge.Resolve(true, 10.12, 10.0, W));     // Good blocks
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, 10.2, 10.0, W));
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, double.NaN, 10.0, W));
        }

        [Test]
        public void OnlyPerfectParriesWithAnyBlockBonus()
        {
            // A class perk (the Fighter) widens the block window, never the parry.
            Assert.AreEqual(DefenseResult.Blocked, ParryJudge.Resolve(true, 10.17, 10.0, W, blockBonus: 0.05));
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, 10.17, 10.0, W));
            Assert.AreEqual(DefenseResult.Blocked, ParryJudge.Resolve(true, 10.06, 10.0, W, blockBonus: 0.05));
        }

        [Test]
        public void DodgeFramesCoverTheHit()
        {
            Assert.AreEqual(DefenseResult.Dodged, ParryJudge.Resolve(true, double.NaN, 10.0, W, dodgeStart: 9.9, dodgeSeconds: 0.25));
            Assert.AreEqual(DefenseResult.Dodged, ParryJudge.Resolve(true, double.NaN, 10.0, W, dodgeStart: 10.03, dodgeSeconds: 0.25));  // a touch late
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, double.NaN, 10.0, W, dodgeStart: 9.6, dodgeSeconds: 0.25));      // frames over
            Assert.AreEqual(DefenseResult.Hit, ParryJudge.Resolve(true, double.NaN, 10.0, W, dodgeStart: 10.1, dodgeSeconds: 0.25));     // too late
            // A guard that blocks or parries counts first.
            Assert.AreEqual(DefenseResult.Parried, ParryJudge.Resolve(true, 10.0, 10.0, W, dodgeStart: 9.9, dodgeSeconds: 0.25));
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
        public void HealsUpToMaxButNotTheDead()
        {
            var v = new Vitality(5);
            v.Damage(3, 0);
            Assert.AreEqual(2, v.Heal(2));
            Assert.AreEqual(4, v.Hp);
            Assert.AreEqual(1, v.Heal(5));
            v.Damage(9, 10);
            Assert.AreEqual(0, v.Heal(2));
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

namespace ProjectHymn.Tests
{
    public class ClipTimingTests
    {
        [Test]
        public void ImpactFrameLandsOnTheBeat()
        {
            Assert.AreEqual(0.4, ProjectHymn.Combat.ClipTiming.Warp(0f, -0.6f, 0.8f, 0f, 0.4f, 1f), 1e-6);
            Assert.AreEqual(0.0, ProjectHymn.Combat.ClipTiming.Warp(-0.6f, -0.6f, 0.8f, 0f, 0.4f, 1f), 1e-6);
            Assert.AreEqual(0.2, ProjectHymn.Combat.ClipTiming.Warp(-0.3f, -0.6f, 0.8f, 0f, 0.4f, 1f), 1e-6);
            Assert.AreEqual(0.7, ProjectHymn.Combat.ClipTiming.Warp(0.4f, -0.6f, 0.8f, 0f, 0.4f, 1f), 1e-6);
            Assert.AreEqual(1.0, ProjectHymn.Combat.ClipTiming.Warp(5f, -0.6f, 0.8f, 0f, 0.4f, 1f), 1e-6);
            Assert.AreEqual(0.0, ProjectHymn.Combat.ClipTiming.Warp(-5f, -0.6f, 0.8f, 0f, 0.4f, 1f), 1e-6);
        }

        [Test]
        public void SpanFollowsTheClipAtTheReferenceTempo()
        {
            // 1 s clip, hit at 0.4: 0.4 s of wind-up = 0.8 beats at 120 BPM, 0.6 s follow-through = 1.2 beats.
            ProjectHymn.Combat.ClipTiming.Span(1f, 0f, 0.4f, 1f, 0.5, 0.2f, 2f, 0.2f, 2f, out float pre, out float post);
            Assert.AreEqual(0.8, pre, 1e-5);
            Assert.AreEqual(1.2, post, 1e-5);
            ProjectHymn.Combat.ClipTiming.Span(1f, 0f, 0.4f, 1f, 0.5, 0.2f, 0.5f, 0.2f, 1f, out pre, out post);
            Assert.AreEqual(0.5, pre, 1e-5);
            Assert.AreEqual(1.0, post, 1e-5);
        }

        [Test]
        public void LoopWraps()
        {
            Assert.AreEqual(0.184, ProjectHymn.Combat.ClipTiming.Loop(0.5f, 0.684f), 1e-5);
            Assert.AreEqual(0.684, ProjectHymn.Combat.ClipTiming.Loop(0f, 0.684f), 1e-5);
        }
    }
}
