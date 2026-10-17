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

        /// <summary>
        /// The Drowned Precentor (the region's boss) conducts "Canticle of the Precentor": 6/8, then the same bar bent into a
        /// 3/4 hemiola (a beat is a quarter there), then 9/8. Each move fits one meter (0 = any): in 6/8 he strikes on the big
        /// beats, in 3/4 on all three quarters, in 9/8 on all three big beats. His second verse (below two thirds) adds
        /// the barcarolle's long-short swing, the suspension, the 9/8 riptide and the Great Bell, a toll no guard stops;
        /// his third (below a third) adds twin bells, the hemiola run across two bars, twos against the 9/8's threes
        /// (the cross current) and the undertow.
        /// </summary>
        public static List<BossMove> Precentor() => new List<BossMove>
        {
            new BossMove(new EnemyAttackPattern("Toll", new[] { 0.0 }, 6, 6, 2), 0),
            new BossMove(new EnemyAttackPattern("Rocking", new[] { 0.0, 3.0 }, 6, 6, 1), 6),
            new BossMove(new EnemyAttackPattern("Hemiola", new[] { 0.0, 1.0, 2.0 }, 3, 3, 1), 3),
            new BossMove(new EnemyAttackPattern("Long Rock", new[] { 0.0, 3.0, 6.0 }, 9, 6, 1), 9),
            new BossMove(new EnemyAttackPattern("Barcarolle", new[] { 0.0, 2.0, 3.0, 5.0 }, 6, 6, 1), 6, 1),
            new BossMove(new EnemyAttackPattern("Suspension", new[] { 0.0, 2.0 }, 3, 3, 1), 3, 1),
            new BossMove(new EnemyAttackPattern("Great Bell", new[] { 0.0 }, 6, 6, 2).WithUnblockable(), 0, 1),
            new BossMove(new EnemyAttackPattern("Riptide", new[] { 0.0, 2.0, 3.0, 5.0, 6.0, 8.0 }, 9, 6, 1), 9, 1),
            // Third verse: two bells in a bar, the hemiola run over two bars, twos against the 9/8's threes, the undertow.
            new BossMove(new EnemyAttackPattern("Twin Bells", new[] { 0.0, 3.0 }, 6, 6, 2).WithUnblockable(), 6, 2),
            new BossMove(new EnemyAttackPattern("Hemiola Run", new[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0 }, 3, 2, 1), 3, 2),
            new BossMove(new EnemyAttackPattern("Cross Current", new[] { 0.0, 2.0, 4.0, 6.0, 8.0 }, 9, 4, 1), 9, 2),
            new BossMove(new EnemyAttackPattern("Undertow", new[] { 0.0, 1.0, 2.0, 3.0, 5.0 }, 6, 4, 1), 6, 2),
        };
    }
}
