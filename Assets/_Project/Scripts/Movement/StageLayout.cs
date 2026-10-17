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
        /// <summary>Beats per bar of the stage's music (4 = common time, 3 = waltz, 6 = the Drowned Nave's 6/8).</summary>
        public int Meter = 4;
        /// <summary>The region's hub the stage returns to, and the spawn there (VS-6: the Drowned Nave goes home to the Air Bell).</summary>
        public string HubScene = "Village", HubSpawn = "FromCliffside";
        /// <summary>The Drowned Nave (VS-6): underwater ruins, 6/8, drowned enemies.</summary>
        public bool Drowned;
        public float ParSeconds = 75f;
        public int PerfectAttacks = 14;
        public CliffsideLayout.Block[] Route = new CliffsideLayout.Block[0];
        public CliffsideLayout.Block[] Props = new CliffsideLayout.Block[0];
        public CliffsideLayout.Block[] Secrets = new CliffsideLayout.Block[0];
        /// <summary>One-way platforms (jump up through them, drop down with Down + Jump): the brawl stage's floors above the ground.</summary>
        public CliffsideLayout.Block[] Platforms = new CliffsideLayout.Block[0];
        /// <summary>No walls at the ends: falling off the edge costs a heart and respawns you (the brawl stage).</summary>
        public bool OpenEdges;
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
                        m.Kind == CliffsideLayout.MarkerKind.Dancer || m.Kind == CliffsideLayout.MarkerKind.Boss ||
                        m.Kind == CliffsideLayout.MarkerKind.Chorister) n++;
                return n;
            }
        }

        public static StageLayout Cliffside => CliffsideLayout.Stage;
        public static StageLayout Belfry => BelfryLayout.Stage;
        public static StageLayout Summit => SummitLayout.Stage;
        public static StageLayout Arena => ArenaLayout.Stage;
        public static StageLayout Yard => YardLayout.Stage;
        public static StageLayout Spring => SpringLayout.Stage;
        public static StageLayout Nave => NaveLayout.Stage;
        public static StageLayout Loft => LoftLayout.Stage;
        public static IReadOnlyList<StageLayout> All => new[] { Yard, Cliffside, Belfry, Summit, Arena, Spring, Nave, Loft };

        /// <summary>Every floor an enemy (or the player) can stand on: the route and the platforms.</summary>
        public FloorMap Floors()
        {
            var floors = new List<FloorMap.Floor>();
            foreach (var b in Route) floors.Add(new FloorMap.Floor(b.Name, b.XMin, b.XMax, b.Top, false));
            foreach (var b in Platforms) floors.Add(new FloorMap.Floor(b.Name, b.XMin, b.XMax, b.Top, true));
            return new FloorMap(floors);
        }
    }
}
