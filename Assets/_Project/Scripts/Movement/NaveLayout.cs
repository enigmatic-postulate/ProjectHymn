using K = ProjectHymn.Movement.CliffsideLayout.Kind;
using M = ProjectHymn.Movement.CliffsideLayout.MarkerKind;
using Block = ProjectHymn.Movement.CliffsideLayout.Block;
using Marker = ProjectHymn.Movement.CliffsideLayout.Marker;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// VS-6's first stage, "Kelp Nave": the flooded length of the drowned cathedral, fought in 6/8 to "Barcarolle of the
    /// Drowned Nave". From the narthex over a fallen pew and along the aisle, up the choir steps, across the broken floor
    /// (a bell jelly drifts over the hole), through the transept where a chorister sings, and up two toppled column drums
    /// to the crossing. Plain data like the other stages; the tests check the route against the jump arcs.
    /// </summary>
    public static class NaveLayout
    {
        public static readonly Block[] Route =
        {
            new Block("Narthex",        -10f, 14f,  0f,   K.Ground),
            new Block("Fallen Pew",      17f, 24f,  1.6f, K.Cliff),     // across a 3 m gap, 1.6 m up
            new Block("Aisle",           24f, 40f,  1.6f, K.Cliff),
            new Block("Choir Step 1",    40f, 44f,  3.4f, K.Cliff),
            new Block("Choir Step 2",    44f, 48f,  5.2f, K.Cliff),
            new Block("Transept",        53f, 70f,  5.2f, K.Cliff),     // the broken floor: a 5 m gap with a bell jelly over it
            new Block("Column Drum 1",   73f, 75f,  7.0f, K.Pillar),
            new Block("Column Drum 2",   78f, 80f,  8.8f, K.Pillar),
            new Block("Crossing",        83f, 104f, 8.8f, K.Cliff),
        };

        public static readonly Block[] Props =
        {
            new Block("Font",        5f,  6.6f, 0.9f, K.Pillar),
            new Block("West Wall", -12f, -10f, 20f,  K.Wall),
            new Block("East Wall", 104f, 106f, 26f,  K.Wall),
        };

        public static readonly Marker[] Markers =
        {
            new Marker("Spawn",              M.Spawn,          -5f,   0f),
            new Marker("Checkpoint 1",       M.Checkpoint,     -5f,   0f),
            new Marker("Checkpoint 2",       M.Checkpoint,     27f,   1.6f),
            new Marker("Checkpoint 3",       M.Checkpoint,     56f,   5.2f),
            new Marker("Checkpoint 4",       M.Checkpoint,     86f,   8.8f),
            new Marker("Barnacled Knight",   M.Target,         33f,   1.6f),
            new Marker("Broken Floor Jelly", M.FloatingTarget, 50.5f, 6.2f),
            new Marker("Transept Chorister", M.Chorister,      66f,   5.2f),
            new Marker("Drum Jelly",         M.FloatingTarget, 76.5f, 8.4f),
            new Marker("Crossing Knight",    M.Target,         93f,   8.8f),
            new Marker("Goal",               M.Goal,          100f,   8.8f),
            new Marker("Lore lore_nave",     M.Pickup,         -8.5f, 0f),
            new Marker("Climb View",         M.CameraZone,     40f,   0f, 24f),
            new Marker("Gap View",           M.CameraZone,     52f,   0f, 14f),
            new Marker("Summit View",        M.CameraZone,     90f,   0f, 26f),
        };

        public static StageLayout Stage => new StageLayout
        {
            Id = "Nave", Name = "Kelp Nave", Scene = "Nave", Meter = 6, Drowned = true,
            HubScene = "AirBell", HubSpawn = "FromNave",
            ParSeconds = 60f, PerfectAttacks = 22,
            Route = Route, Props = Props, Markers = Markers,
        };
    }
}
