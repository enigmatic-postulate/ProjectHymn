using K = ProjectHymn.Movement.CliffsideLayout.Kind;
using M = ProjectHymn.Movement.CliffsideLayout.MarkerKind;
using Block = ProjectHymn.Movement.CliffsideLayout.Block;
using Marker = ProjectHymn.Movement.CliffsideLayout.Marker;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// VS-6's boss stage, "The Sunken Apse": up the chancel steps from the crossing to the apse floor, where the Drowned
    /// Precentor conducts "Canticle of the Precentor" through 6/8, a 3/4 hemiola and 9/8. The way up (the goal) opens
    /// when he falls. Plain data like the other stages.
    /// </summary>
    public static class ApseLayout
    {
        public const float ArenaXMin = 26f, ArenaXMax = 68f, ArenaTop = 5.2f;

        public static readonly Block[] Route =
        {
            new Block("Crossing",       -10f, 10f, 0f,   K.Ground),
            new Block("Chancel Step 1",  12f, 18f, 1.7f, K.Cliff),
            new Block("Chancel Step 2",  20f, 26f, 3.4f, K.Cliff),
            new Block("Apse Floor", ArenaXMin, ArenaXMax, ArenaTop, K.Cliff),
        };

        public static readonly Block[] Props =
        {
            new Block("West Wall", -12f, -10f, 20f, K.Wall),
            new Block("East Wall",  68f,  70f, 26f, K.Wall),
        };

        public static readonly Marker[] Markers =
        {
            new Marker("Spawn",            M.Spawn,       4f, 0f),
            new Marker("Checkpoint 1",     M.Checkpoint,  4f, 0f),
            new Marker("Checkpoint 2",     M.Checkpoint, 29f, ArenaTop),
            new Marker("Drowned Precentor", M.Boss,      40f, ArenaTop),
            new Marker("Goal",             M.Goal,       52f, ArenaTop),
            new Marker("Lore lore_apse",   M.Pickup,     -8.5f, 0f),
            new Marker("Chancel View",     M.CameraZone, 12f, 0f, 30f),
            new Marker("Apse View",        M.CameraZone, 44f, 0f, 32f),
        };

        public static StageLayout Stage => new StageLayout
        {
            Id = "Apse", Name = "The Sunken Apse", Scene = "Apse", Meter = 6, Drowned = true,
            HubScene = "AirBell", HubSpawn = "FromNave",
            ParSeconds = 70f, PerfectAttacks = 45, HitsForS = 3, HitsForA = 6,   // the par now matches the fight (1750 HP)
            Route = Route, Props = Props, Markers = Markers,
        };
    }
}
