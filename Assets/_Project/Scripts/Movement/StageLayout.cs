using System.Collections.Generic;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// One stage as plain data (Cliffside, Belfry Ridge, the Summit): the route along the lane, props, secrets, markers, and what the
    /// stage asks for (par, perfect-run attacks, the meter its music is in). The stage builder turns one into a scene;
    /// the tests check every route against the jump arcs.
    /// </summary>
    public sealed class StageLayout
    {
        public string Id, Name, Scene;
        /// <summary>Beats per bar of the stage's music (4 = common time, 3 = waltz).</summary>
        public int Meter = 4;
        public float ParSeconds = 75f;
        public int PerfectAttacks = 14;
        public CliffsideLayout.Block[] Route = new CliffsideLayout.Block[0];
        public CliffsideLayout.Block[] Props = new CliffsideLayout.Block[0];
        public CliffsideLayout.Block[] Secrets = new CliffsideLayout.Block[0];
        public CliffsideLayout.Marker[] Markers = new CliffsideLayout.Marker[0];

        public IEnumerable<CliffsideLayout.Marker> MarkersOf(CliffsideLayout.MarkerKind kind)
        {
            foreach (var m in Markers) if (m.Kind == kind) yield return m;
        }

        public CliffsideLayout.Marker First(CliffsideLayout.MarkerKind kind)
        {
            foreach (var m in MarkersOf(kind)) return m;
            return default;
        }

        /// <summary>The route block a point stands over (default when none).</summary>
        public CliffsideLayout.Block BlockAt(float x)
        {
            foreach (var b in Route) if (x > b.XMin && x < b.XMax) return b;
            return default;
        }

        /// <summary>Every enemy the stage places (targets, floating targets, dancers).</summary>
        public int EnemyCount
        {
            get
            {
                int n = 0;
                foreach (var m in Markers)
                    if (m.Kind == CliffsideLayout.MarkerKind.Target || m.Kind == CliffsideLayout.MarkerKind.FloatingTarget ||
                        m.Kind == CliffsideLayout.MarkerKind.Dancer || m.Kind == CliffsideLayout.MarkerKind.Boss) n++;
                return n;
            }
        }

        public static StageLayout Cliffside => CliffsideLayout.Stage;
        public static StageLayout Belfry => BelfryLayout.Stage;
        public static StageLayout Summit => SummitLayout.Stage;
        public static StageLayout Arena => ArenaLayout.Stage;
        public static StageLayout Yard => YardLayout.Stage;
        public static IReadOnlyList<StageLayout> All => new[] { Yard, Cliffside, Belfry, Summit, Arena };
    }
}
