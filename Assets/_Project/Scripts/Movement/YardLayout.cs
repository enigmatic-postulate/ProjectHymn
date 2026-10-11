using K = ProjectHymn.Movement.CliffsideLayout.Kind;
using M = ProjectHymn.Movement.CliffsideLayout.MarkerKind;
using Block = ProjectHymn.Movement.CliffsideLayout.Block;
using Marker = ProjectHymn.Movement.CliffsideLayout.Marker;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// The Practice Yard (Slice 10): the optional getting-started stage, first on the village gate. One flat yard: a
    /// training dummy near the start, a training knight with a blunted blade (switched on for the guard, parry and dodge
    /// lessons), and a real bone knight for the final bout; the way out opens when he falls. Unranked.
    /// </summary>
    public static class YardLayout
    {
        public const string TrainingKnight = "Yard Knight", BoutKnight = "Bout Knight";

        public static readonly Block[] Route =
        {
            new Block("Yard", -12f, 40f, 0f, K.Ground),
        };

        public static readonly Block[] Props =
        {
            new Block("West Wall", -14f, -12f, 16f, K.Wall),
            new Block("East Wall",  40f,  42f, 16f, K.Wall),
        };

        public static readonly Marker[] Markers =
        {
            new Marker("Spawn",          M.Spawn,      -6f, 0f),
            new Marker("Checkpoint 1",   M.Checkpoint, -6f, 0f),
            new Marker("Training Dummy", M.Dummy,       2f, 0f),
            new Marker(TrainingKnight,   M.Target,     12f, 0f),
            new Marker(BoutKnight,       M.Target,     24f, 0f),
            new Marker("Goal",           M.Goal,       35f, 0f),
            new Marker("Yard View",      M.CameraZone, 14f, 0f, 56f),
        };

        public static StageLayout Stage => new StageLayout
        {
            Id = "Yard", Name = "Practice Yard", Scene = "Yard", Meter = 4,
            ParSeconds = 600f, PerfectAttacks = 20,          // unranked; numbers only satisfy the layout checks
            Route = Route, Props = Props, Markers = Markers,
        };
    }
}
