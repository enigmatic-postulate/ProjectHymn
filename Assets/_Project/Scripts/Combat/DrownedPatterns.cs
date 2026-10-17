using System.Collections.Generic;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// The Drowned Nave's enemy attacks (VS-6), in 6/8 where a beat is an eighth: a bar is six beats, its two big beats
    /// fall on one and four. Every attack counts in for one bar (the scheduler's one-bar lead) and lands on a downbeat.
    /// </summary>
    public static class DrownedPatterns
    {
        /// <summary>The Bell Jelly: one sting on the downbeat, or one on each big beat.</summary>
        public static List<EnemyAttackPattern> Jelly() => new List<EnemyAttackPattern>
        {
            new EnemyAttackPattern("Toll", new[] { 0.0 }, 6, 6, 1),
            new EnemyAttackPattern("Double Toll", new[] { 0.0, 3.0 }, 6, 6, 1),
        };

        /// <summary>The Drowned Chorister: three bolts and a heavier note on eighths one to four; a held note along the floor.</summary>
        public static List<EnemyAttackPattern> Chorister() => new List<EnemyAttackPattern>
        {
            new EnemyAttackPattern("Antiphon", new[] { 0.0, 1.0, 2.0, 3.0 }, 6, 6, 1),
            new EnemyAttackPattern("Sustain", new[] { 0.0 }, 6, 6, 1).WithUnblockable(),
        };

        /// <summary>The Barnacled Knight: a heavy cut on the downbeat, or a cut on each big beat (the knight's flurry in 6/8).</summary>
        public static List<EnemyAttackPattern> Knight() => new List<EnemyAttackPattern>
        {
            new EnemyAttackPattern("Heavy Cut", new[] { 0.0 }, 6, 6, 2),
            new EnemyAttackPattern("Lilt", new[] { 0.0, 3.0 }, 6, 6, 1),
        };
    }
}
