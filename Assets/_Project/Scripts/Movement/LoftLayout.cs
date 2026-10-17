using K = ProjectHymn.Movement.CliffsideLayout.Kind;
using M = ProjectHymn.Movement.CliffsideLayout.MarkerKind;
using Block = ProjectHymn.Movement.CliffsideLayout.Block;
using Marker = ProjectHymn.Movement.CliffsideLayout.Marker;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// VS-6's second stage, "Choir Loft": up through the drowned cathedral's galleries in 6/8. A stair to the first
    /// gallery where a chorister sings down the length of it, a high step to the second, three organ pipes to climb (jellies
    /// drift among them), the upper loft and its singer, and the bell gallery across the last gap. Plain data, tested
    /// against the jump arcs.
    /// </summary>
    public static class LoftLayout
    {
        public static readonly Block[] Route =
        {
            new Block("Loft Stair",    -10f, 12f,  0f,    K.Ground),
            new Block("Gallery 1",      12f, 30f,  2.0f,  K.Cliff),     // 2.0 m: double jump, or jump and grab
            new Block("Gallery 2",      30f, 44f,  4.6f,  K.Cliff),     // 2.6 m: jump and grab
            new Block("Organ Pipe 1",   47f, 49f,  6.4f,  K.Pillar),
            new Block("Organ Pipe 2",   52f, 54f,  8.2f,  K.Pillar),
            new Block("Organ Pipe 3",   57f, 59f, 10.0f,  K.Pillar),
            new Block("Upper Loft",     62f, 80f, 11.0f,  K.Cliff),
            new Block("Bell Gallery",   85f, 106f, 11.0f, K.Cliff),     // a 5 m gap with a jelly over it
        };

        public static readonly Block[] Props =
        {
            new Block("Lectern",     4f,  5.4f, 1.1f, K.Pillar),
            new Block("West Wall", -12f, -10f, 22f,  K.Wall),
            new Block("East Wall", 106f, 108f, 28f,  K.Wall),
        };

        public static readonly Marker[] Markers =
        {
            new Marker("Spawn",             M.Spawn,          -5f,   0f),
            new Marker("Checkpoint 1",      M.Checkpoint,     -5f,   0f),
            new Marker("Checkpoint 2",      M.Checkpoint,     15f,   2.0f),
            new Marker("Checkpoint 3",      M.Checkpoint,     33f,   4.6f),
            new Marker("Checkpoint 4",      M.Checkpoint,     65f,  11.0f),
            new Marker("Gallery Chorister", M.Chorister,      26f,   2.0f),
            new Marker("Gallery Knight",    M.Target,         39f,   4.6f),
            new Marker("Pipe Jelly",        M.FloatingTarget, 55.5f, 10.2f),
            new Marker("Loft Chorister",    M.Chorister,      76f,  11.0f),
            new Marker("Bell Gap Jelly",    M.FloatingTarget, 82.5f, 12.0f),
            new Marker("Bell Knight",       M.Target,         95f,  11.0f),
            new Marker("Goal",              M.Goal,          102f,  11.0f),
            new Marker("Lore lore_loft",    M.Pickup,         -8.5f, 0f),
            new Marker("Climb View",        M.CameraZone,     28f,   0f, 30f),
            new Marker("Gap View",          M.CameraZone,     54f,   0f, 16f),
            new Marker("Summit View",       M.CameraZone,     90f,   0f, 34f),
        };

        public static StageLayout Stage => new StageLayout
        {
            Id = "Loft", Name = "Choir Loft", Scene = "Loft", Meter = 6, Drowned = true,
            HubScene = "AirBell", HubSpawn = "FromNave",
            ParSeconds = 60f, PerfectAttacks = 24,
            Route = Route, Props = Props, Markers = Markers,
        };
    }
}
