using System;
using ProjectHymn.Core;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// One enemy attack in musical terms (vertical slice 2): the hits, in beats from the first one (a heavy cut is
    /// {0}, a two-beat flurry {0, 1}), how many beats it is telegraphed ahead of the first hit, and how long the enemy
    /// recovers after the last hit. Pure C#.
    /// </summary>
    [Serializable]
    public sealed class EnemyAttackPattern
    {
        public string Name = "Cut";
        public double[] Hits = { 0.0 };
        public int LeadBeats = 4;
        public int RecoverBeats = 2;
        public int Damage = 1;
        /// <summary>Land the first hit on a downbeat (otherwise on any beat).</summary>
        public bool OnDownbeat = true;

        public EnemyAttackPattern() { }
        public EnemyAttackPattern(string name, double[] hits, int leadBeats, int recoverBeats, int damage, bool onDownbeat = true)
        {
            Name = name; Hits = hits; LeadBeats = leadBeats; RecoverBeats = recoverBeats; Damage = damage; OnDownbeat = onDownbeat;
        }

        public double Span => Hits == null || Hits.Length == 0 ? 0.0 : Hits[Hits.Length - 1];
    }

    public enum AttackPhase { Waiting, Telegraph, Active, Recover, Done }

    /// <summary>A planned attack placed on the song's beat grid (absolute beat positions).</summary>
    public readonly struct PlannedAttack
    {
        public readonly EnemyAttackPattern Pattern;
        public readonly double TelegraphBeat;    // wind-up starts
        public readonly double FirstHitBeat;
        public readonly double LastHitBeat;
        public readonly double EndBeat;          // recovery over

        public PlannedAttack(EnemyAttackPattern pattern, double firstHitBeat)
        {
            Pattern = pattern;
            FirstHitBeat = firstHitBeat;
            TelegraphBeat = firstHitBeat - Math.Max(0, pattern.LeadBeats);
            LastHitBeat = firstHitBeat + pattern.Span;
            EndBeat = LastHitBeat + Math.Max(0, pattern.RecoverBeats);
        }

        public bool IsValid => Pattern != null;
        public int HitCount => Pattern?.Hits?.Length ?? 0;
        public double HitBeat(int i) => FirstHitBeat + Pattern.Hits[i];

        public AttackPhase PhaseAt(double beat)
        {
            if (beat < TelegraphBeat) return AttackPhase.Waiting;
            if (beat < FirstHitBeat) return AttackPhase.Telegraph;
            if (beat <= LastHitBeat) return AttackPhase.Active;
            if (beat < EndBeat) return AttackPhase.Recover;
            return AttackPhase.Done;
        }

        /// <summary>0..1 through the telegraph (1 at the first hit).</summary>
        public double TelegraphProgress(double beat) =>
            FirstHitBeat <= TelegraphBeat ? 1.0 : Math.Max(0.0, Math.Min(1.0, (beat - TelegraphBeat) / (FirstHitBeat - TelegraphBeat)));

        /// <summary>Whole beats left until the next hit at or after <paramref name="beat"/> (for a 3-2-1 count-in), or -1.</summary>
        public int BeatsUntilNextHit(double beat)
        {
            for (int i = 0; i < HitCount; i++)
            {
                double h = HitBeat(i);
                if (h >= beat - 1e-6) return (int)Math.Ceiling(h - beat - 1e-6);
            }
            return -1;
        }
    }

    /// <summary>Places enemy attacks on the tempo map so every one is fully telegraphed. Pure C#.</summary>
    public static class EnemyScheduler
    {
        /// <summary>
        /// Plan <paramref name="pattern"/> so its whole telegraph lies at or after <paramref name="nowBeat"/> and its
        /// wind-up does not start before <paramref name="notBeforeBeat"/> (recovery, stagger). The first hit lands on a
        /// whole beat (a downbeat if the pattern asks for one).
        /// </summary>
        public static PlannedAttack Plan(TempoMap map, EnemyAttackPattern pattern, double nowBeat, double notBeforeBeat)
        {
            double earliest = Math.Max(nowBeat, notBeforeBeat) + Math.Max(0, pattern.LeadBeats);
            long b = (long)Math.Ceiling(earliest - 1e-6);
            if (pattern.OnDownbeat)
            {
                for (int guard = 0; guard < 64 && !map.GetBeat(b).IsDownbeat; guard++) b++;
            }
            return new PlannedAttack(pattern, b);
        }
    }

    public enum DefenseResult { Hit, Blocked, Parried, OutOfRange, Dodged }

    /// <summary>
    /// The player's defence against an enemy hit, judged against the hit's own time (GDD update 3):
    /// a guard with Perfect timing parries (the enemy is staggered); Great or Good timing blocks; a dodge whose
    /// invulnerable frames cover the hit dodges it; anything else is a hit. Pure C#.
    /// </summary>
    public static class ParryJudge
    {
        /// <param name="guardPressTime">Calibrated song time of the latest guard press (NaN if none).</param>
        /// <param name="dodgeStart">Calibrated song time the latest dodge began (NaN if none).</param>
        /// <param name="dodgeSeconds">How long that dodge is invulnerable.</param>
        /// <param name="blockBonus">Extra seconds of block window either side (a class perk, e.g. the Fighter).</param>
        public static DefenseResult Resolve(bool inRange, double guardPressTime, double hitTime, TimingWindows w,
                                            double dodgeStart = double.NaN, double dodgeSeconds = 0.0, double blockBonus = 0.0)
        {
            if (!inRange) return DefenseResult.OutOfRange;
            if (!double.IsNaN(guardPressTime))
            {
                double d = Math.Abs(guardPressTime - hitTime);
                if (d <= w.Perfect) return DefenseResult.Parried;
                if (d <= w.Good + blockBonus) return DefenseResult.Blocked;
            }
            if (Dodges(dodgeStart, dodgeSeconds, hitTime, w)) return DefenseResult.Dodged;
            return DefenseResult.Hit;
        }

        /// <summary>The dodge's invulnerable frames cover the hit (a dodge may start up to a Perfect window late).</summary>
        public static bool Dodges(double dodgeStart, double dodgeSeconds, double hitTime, TimingWindows w) =>
            !double.IsNaN(dodgeStart) && hitTime >= dodgeStart - w.Perfect && hitTime <= dodgeStart + dodgeSeconds;

        /// <summary>When an attack can be resolved: once a late guard press could no longer count.</summary>
        public static double ResolveTime(double hitTime, TimingWindows w) => hitTime + w.Good;
    }

    /// <summary>Hit points with a short grace period after each hit. Pure C#.</summary>
    public sealed class Vitality
    {
        public int Max { get; private set; }
        public int Hp { get; private set; }
        public double GraceSeconds = 0.8;
        double _graceUntil = double.NegativeInfinity;

        public Vitality(int max) { Max = Math.Max(1, max); Hp = Max; }

        public bool IsDead => Hp <= 0;
        public bool InGrace(double now) => now < _graceUntil;

        /// <summary>Apply damage at time <paramref name="now"/>. Returns false if ignored (grace, already dead).</summary>
        public bool Damage(int amount, double now)
        {
            if (IsDead || amount <= 0 || InGrace(now)) return false;
            Hp = Math.Max(0, Hp - amount);
            _graceUntil = now + GraceSeconds;
            return true;
        }

        public void Refill() { Hp = Max; _graceUntil = double.NegativeInfinity; }

        /// <summary>Restore up to <paramref name="amount"/> (not past Max, not when dead). Returns how much was restored.</summary>
        public int Heal(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            int before = Hp;
            Hp = Math.Min(Max, Hp + amount);
            return Hp - before;
        }
    }
}
