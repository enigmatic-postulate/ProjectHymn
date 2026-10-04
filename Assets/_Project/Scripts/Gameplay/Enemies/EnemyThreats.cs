using System.Collections.Generic;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The beats on which enemy hits are scheduled to land, so the HUD's beat track can mark them ("!" a hit,
    /// "!!" a heavy one) as they count in.
    /// </summary>
    public static class EnemyThreats
    {
        static readonly List<(object owner, long beat, int weight)> Hits = new List<(object, long, int)>();

        public static void Add(object owner, long beat, int weight) => Hits.Add((owner, beat, weight));

        public static void Clear(object owner) => Hits.RemoveAll(h => ReferenceEquals(h.owner, owner));

        public static void Remove(object owner, long beat) => Hits.RemoveAll(h => ReferenceEquals(h.owner, owner) && h.beat == beat);

        /// <summary>Heaviest hit landing on this beat (0 = none).</summary>
        public static int At(long beat)
        {
            int w = 0;
            foreach (var h in Hits) if (h.beat == beat && h.weight > w) w = h.weight;
            return w;
        }
    }
}
