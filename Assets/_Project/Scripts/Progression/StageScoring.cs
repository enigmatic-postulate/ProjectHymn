using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;

namespace ProjectHymn.Progression
{
    public enum StageRank { S, A, B, C }

    /// <summary>
    /// Everything the player did in one stage run, counted by the GDD's attack tally (update 3): each hit by its own
    /// judgement, a parry as a Perfect, a block or a dodge as a Great. Pure C#; the Core Combat slice shows it on a debug
    /// results screen, and Rank &amp; Class will build the ranks and the class checks on it.
    /// </summary>
    public sealed class PlaystyleLog
    {
        readonly int[] _hits = new int[5];          // indexed by Judgement (OffBeat..TruePerfect)

        public int Parries { get; private set; }
        public int Blocks { get; private set; }
        public int Dodges { get; private set; }
        /// <summary>Enemy hits that landed on the player.</summary>
        public int HitsTaken { get; private set; }
        public int HeartsLost { get; private set; }
        public int EnemiesDefeated { get; private set; }
        public int SequencesCompleted { get; private set; }

        public int Hits(Judgement j) => _hits[(int)j];
        /// <summary>Every note pressed (each one swings and hits).</summary>
        public int Attacks { get { int n = 0; foreach (var h in _hits) n += h; return n; } }
        public int OffBeat => _hits[(int)Judgement.OffBeat];

        public void AddNote(Judgement j) => _hits[(int)j]++;
        public void AddSequence() => SequencesCompleted++;
        public void AddKill() => EnemiesDefeated++;

        public void AddDefense(DefenseResult r)
        {
            switch (r)
            {
                case DefenseResult.Parried: Parries++; break;
                case DefenseResult.Blocked: Blocks++; break;
                case DefenseResult.Dodged: Dodges++; break;
            }
        }

        public void AddHurt(int hearts)
        {
            HitsTaken++;
            HeartsLost += Math.Max(0, hearts);
        }

        /// <summary>
        /// Every scored action in the tally's timing categories: hits by judgement, parries as Perfect, blocks and
        /// dodges as Great.
        /// </summary>
        public int TallyCount(Judgement category)
        {
            int n = _hits[(int)category];
            if (category == Judgement.Perfect) n += Parries;
            if (category == Judgement.Great) n += Blocks + Dodges;
            return n;
        }

        public int TallyTotal => Attacks + Parries + Blocks + Dodges;

        /// <summary>Share of tallied actions at <paramref name="target"/> or better (0..1).</summary>
        public double ShareAtLeast(Judgement target)
        {
            int total = TallyTotal;
            if (total == 0) return 0.0;
            int n = 0;
            for (var j = target; j <= Judgement.TruePerfect; j++) n += TallyCount(j);
            return n / (double)total;
        }

        /// <summary>
        /// Mastery score, 0..100 (placeholder weighting until Rank &amp; Class): Perfect-equivalents per tallied action,
        /// where a True Perfect counts as 3 Perfects, a Great as 2/3 and a Good as 1/3. Mostly-Perfect play scores near 100;
        /// True Perfects can only push it up to 100, never past it.
        /// </summary>
        public double MasteryScore
        {
            get
            {
                double tp = TallyCount(Judgement.TruePerfect);
                double units = 3.0 * tp + TallyCount(Judgement.Perfect) + TallyCount(Judgement.Great) + TallyCount(Judgement.Good) + TallyCount(Judgement.OffBeat);
                if (units <= 0.0) return 0.0;
                double perfectEq = 3.0 * tp + TallyCount(Judgement.Perfect) + TallyCount(Judgement.Great) * (2.0 / 3.0) + TallyCount(Judgement.Good) * (1.0 / 3.0);
                return 100.0 * perfectEq / units;
            }
        }
    }

    /// <summary>What a stage asks for alongside its par time (GDD update 3).</summary>
    public readonly struct StageTargets
    {
        public readonly double ParSeconds;
        public readonly int Enemies;
        /// <summary>Attacks a perfect run needs; the efficiency score compares against it.</summary>
        public readonly int PerfectAttacks;
        /// <summary>Hits a run may take and still earn S (0 on the first stage).</summary>
        public readonly int HitsAllowedForS;
        /// <summary>Hearts a run may lose and still earn S or A (placeholder; calibrated later).</summary>
        public readonly int HeartsAllowedForA;
        /// <summary>Share of actions that must be at <see cref="TargetTiming"/> or better for S or A (placeholder).</summary>
        public readonly double TimingShareForA;
        public readonly Judgement TargetTiming;

        public StageTargets(double parSeconds, int enemies, int perfectAttacks, int hitsAllowedForS = 0,
                            int heartsAllowedForA = 2, double timingShareForA = 0.5, Judgement targetTiming = Judgement.Great)
        {
            ParSeconds = parSeconds; Enemies = enemies; PerfectAttacks = perfectAttacks; HitsAllowedForS = hitsAllowedForS;
            HeartsAllowedForA = heartsAllowedForA; TimingShareForA = timingShareForA; TargetTiming = targetTiming;
        }
    }

    /// <summary>A stage clear: its time rank, the gates that capped it, the final rank and the efficiency.</summary>
    public readonly struct StageResult
    {
        public readonly double Seconds;
        public readonly StageRank TimeRank;
        public readonly StageRank Rank;
        public readonly IReadOnlyList<string> Caps;
        /// <summary>Perfect-play attack count over attacks used, 0..1 (1 = no wasted attacks).</summary>
        public readonly double Efficiency;

        public StageResult(double seconds, StageRank timeRank, StageRank rank, IReadOnlyList<string> caps, double efficiency)
        {
            Seconds = seconds; TimeRank = timeRank; Rank = rank; Caps = caps; Efficiency = efficiency;
        }
    }

    /// <summary>
    /// Stage ranks (GDD update 3): time-based against a hard par time (par or faster = S; each step of 10% of par slower
    /// drops one rank; C is the lowest and still clears), then gated by how cleanly the stage was fought. Pure C#.
    /// </summary>
    public static class StageScoring
    {
        public static StageRank TimeRank(double seconds, double parSeconds)
        {
            if (parSeconds <= 0.0 || seconds <= parSeconds) return StageRank.S;
            double step = 0.1 * parSeconds;
            int steps = (int)Math.Ceiling((seconds - parSeconds) / step - 1e-9);
            return (StageRank)Math.Min((int)StageRank.C, steps);
        }

        public static StageResult Score(double seconds, PlaystyleLog log, StageTargets t)
        {
            var caps = new List<string>();
            var rank = TimeRank(seconds, t.ParSeconds);

            if (log.EnemiesDefeated < t.Enemies && rank < StageRank.B)
            {
                rank = StageRank.B;
                caps.Add($"enemies {log.EnemiesDefeated}/{t.Enemies}: S and A need every enemy");
            }
            if (log.HeartsLost > t.HeartsAllowedForA && rank < StageRank.B)
            {
                rank = StageRank.B;
                caps.Add($"{log.HeartsLost} hearts lost: too much damage for S or A");
            }
            double share = log.ShareAtLeast(t.TargetTiming);
            if (log.TallyTotal > 0 && share < t.TimingShareForA && rank < StageRank.B)
            {
                rank = StageRank.B;
                caps.Add($"{share:P0} of actions at {t.TargetTiming} or better: most must be");
            }
            if (log.HitsTaken > t.HitsAllowedForS && rank == StageRank.S)
            {
                rank = StageRank.A;
                caps.Add(t.HitsAllowedForS == 0 ? "a hit taken: S needs a clean run here" : $"{log.HitsTaken} hits taken: S allows {t.HitsAllowedForS}");
            }

            double efficiency = t.PerfectAttacks <= 0 ? 0.0 : Math.Min(1.0, t.PerfectAttacks / (double)Math.Max(1, log.Attacks));
            return new StageResult(seconds, TimeRank(seconds, t.ParSeconds), rank, caps, efficiency);
        }
    }
}
