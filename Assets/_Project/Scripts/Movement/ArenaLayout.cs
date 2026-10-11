using K = ProjectHymn.Movement.CliffsideLayout.Kind;
using M = ProjectHymn.Movement.CliffsideLayout.MarkerKind;
using Block = ProjectHymn.Movement.CliffsideLayout.Block;
using Marker = ProjectHymn.Movement.CliffsideLayout.Marker;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// The Arena (GDD update 18, Slice 8): a short ring fight in waves to Battle Theme 1, reached from the village and
    /// replayable, for experience between stages. Enemy markers named "Wave N ..." join in wave N once wave N-1 falls; the
    /// way out opens after the last wave. Par is a placeholder until playtest.
    /// </summary>
    public static class ArenaLayout
    {
        public static readonly Block[] Route =
        {
            new Block("Ring", -12f, 32f, 0f, K.Ground),
        };

        public static readonly Block[] Props =
        {
            new Block("West Wall", -14f, -12f, 16f, K.Wall),
            new Block("East Wall",  32f,  34f, 16f, K.Wall),
        };

        public static readonly Marker[] Markers =
        {
            new Marker("Spawn",               M.Spawn,      -6f, 0f),
            new Marker("Checkpoint 1",        M.Checkpoint, -6f, 0f),
            new Marker("Wave 1 Knight",       M.Target,      8f, 0f),
            new Marker("Wave 2 Knight",       M.Target,     14f, 0f),
            new Marker("Wave 2 Wisp",         M.FloatingTarget, 4f, 1.0f),
            new Marker("Wave 3 Bone Dancer",  M.Dancer,      6f, 0f),
            new Marker("Wave 3 Knight",       M.Target,     18f, 0f),
            new Marker("Goal",                M.Goal,       28f, 0f),
            new Marker("Summit View",         M.CameraZone, 10f, 0f, 46f),
        };

        public static StageLayout Stage => new StageLayout
        {
            Id = "Arena", Name = "Arena", Scene = "Arena", Meter = 4,
            ParSeconds = 60f, PerfectAttacks = 24,
            Route = Route, Props = Props, Markers = Markers,
        };

        /// <summary>The wave a marker belongs to ("Wave 2 Knight" → 2), or 0.</summary>
        public static int WaveOf(string markerName)
        {
            if (markerName == null || !markerName.StartsWith("Wave ")) return 0;
            int end = markerName.IndexOf(' ', 5);
            return int.TryParse(end > 5 ? markerName.Substring(5, end - 5) : markerName.Substring(5), out int n) ? n : 0;
        }
    }
}
