using K = ProjectHymn.Movement.CliffsideLayout.Kind;
using M = ProjectHymn.Movement.CliffsideLayout.MarkerKind;
using Block = ProjectHymn.Movement.CliffsideLayout.Block;
using Marker = ProjectHymn.Movement.CliffsideLayout.Marker;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// The second stage (vertical slice milestone VS-2), "Belfry Ridge": a dusk climb along the ridge above the bay to the
    /// old cantors' belfry, fought in 3/4 to "Waltz of the Belfry". Bone dancers waltz along the ledges (they glide on
    /// each downbeat and attack in threes), a wisp holds the broken span, and the lead dancer waits in the belfry yard.
    /// Plain data, like <see cref="CliffsideLayout"/>; the tests check the route against the jump arcs.
    /// </summary>
    public static class BelfryLayout
    {
        public static readonly Block[] Route =
        {
            new Block("Ridge Foot",      -10f, 16f,  0f,   K.Ground),
            new Block("First Step",       16f, 28f,  1.8f, K.Cliff),     // 1.8 m: a plain jump only just fails, double jump or grab
            new Block("Second Step",      31f, 40f,  3.4f, K.Cliff),     // across a 3 m gap
            new Block("Bell Wall",        40f, 52f,  6.4f, K.Cliff),     // 3.0 m: double jump and grab
            new Block("Belfry Approach",  57f, 66f,  6.4f, K.Cliff),     // the broken span: a 5 m gap with the wisp over it
            new Block("Rope Post 1",      69f, 71f,  8.2f, K.Pillar),
            new Block("Rope Post 2",      74f, 76f, 10.0f, K.Pillar),
            new Block("Belfry Yard",      79f, 98f, 12.0f, K.Cliff),
        };

        public static readonly Block[] Props =
        {
            new Block("Cairn",       6f,  7.6f, 0.7f, K.Pillar),
            new Block("West Wall", -12f, -10f, 20f,  K.Wall),
            new Block("East Wall",  98f, 100f, 24f,  K.Wall),
        };

        public static readonly Marker[] Markers =
        {
            new Marker("Spawn",           M.Spawn,      -5f,  0f),
            new Marker("Checkpoint 1",    M.Checkpoint, -5f,  0f),
            new Marker("Checkpoint 2",    M.Checkpoint, 33f,  3.4f),
            new Marker("Checkpoint 3",    M.Checkpoint, 60f,  6.4f),
            new Marker("Checkpoint 4",    M.Checkpoint, 82f, 12.0f),
            new Marker("Bone Dancer",     M.Dancer,     23f,  1.8f),
            new Marker("Bone Dancer 2",   M.Dancer,     46f,  6.4f),
            new Marker("Span Wisp",       M.FloatingTarget, 54.5f, 7.4f),
            new Marker("Lead Dancer",     M.Dancer,     88f, 12.0f),
            new Marker("Goal",            M.Goal,       94.5f, 12.0f),
            new Marker("Lore lore_belfry", M.Pickup,    -8.5f, 0f),
            new Marker("Climb View",      M.CameraZone, 36f,  0f, 26f),
            new Marker("Gap View",        M.CameraZone, 58f,  0f, 18f),
            new Marker("Summit View",     M.CameraZone, 88f,  0f, 22f),
        };

        public static StageLayout Stage => new StageLayout
        {
            Id = "Belfry", Name = "Belfry Ridge", Scene = "Belfry", Meter = 3,
            ParSeconds = 45f, PerfectAttacks = 20,
            Route = Route, Props = Props, Markers = Markers,
        };
    }
}
