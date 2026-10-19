using K = ProjectHymn.Movement.CliffsideLayout.Kind;
using M = ProjectHymn.Movement.CliffsideLayout.MarkerKind;
using Block = ProjectHymn.Movement.CliffsideLayout.Block;
using Marker = ProjectHymn.Movement.CliffsideLayout.Marker;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// The boss stage (vertical slice milestone VS-3), "The Summit": a short stair up to the crown of the headland, a wide
    /// flat arena where the Bone Cantor conducts "Hymn of the Summit" through 4/4, 3/4 and 7/8. The way down (the goal)
    /// opens when the Cantor falls. Plain data like the other stages.
    /// </summary>
    public static class SummitLayout
    {
        public const float ArenaXMin = 26f, ArenaXMax = 68f, ArenaTop = 5.4f;

        public static readonly Block[] Route =
        {
            new Block("Summit Path", -10f, 10f, 0f,   K.Ground),
            new Block("Stair 1",      12f, 18f, 1.8f, K.Cliff),
            new Block("Stair 2",      20f, 26f, 3.6f, K.Cliff),
            new Block("Crown",   ArenaXMin, ArenaXMax, ArenaTop, K.Cliff),
        };

        public static readonly Block[] Props =
        {
            new Block("West Wall", -12f, -10f, 20f, K.Wall),
            new Block("East Wall",  68f,  70f, 26f, K.Wall),
        };

        public static readonly Marker[] Markers =
        {
            // A short run up the stair, the Cantor just past the top, the way down close behind him. Par 1:00 (recalibrated
            // 2026-10-17 from GDD update 18's 0:15: three verses and 600 HP).
            new Marker("Spawn",            M.Spawn,       4f, 0f),
            new Marker("Checkpoint 1",     M.Checkpoint,  4f, 0f),
            new Marker("Checkpoint 2",     M.Checkpoint, 29f, ArenaTop),
            new Marker("Bone Cantor",      M.Boss,       38f, ArenaTop),
            new Marker("Goal",             M.Goal,       48f, ArenaTop),
            new Marker("Lore lore_summit", M.Pickup,     -8.5f, 0f),
            new Marker("Climb View",       M.CameraZone, 12f, 0f, 30f),
            new Marker("Summit View",      M.CameraZone, 42f, 0f, 32f),
        };

        public static StageLayout Stage => new StageLayout
        {
            Id = "Summit", Name = "The Summit", Scene = "Summit", Meter = 4,
            ParSeconds = 45f, PerfectAttacks = 28, HitsForS = 3, HitsForA = 6,   // ~7 s up the stair and ~27 s of boss (950 HP)
            Route = Route, Props = Props, Markers = Markers,
        };
    }
}
