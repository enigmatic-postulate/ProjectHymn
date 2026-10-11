using K = ProjectHymn.Movement.CliffsideLayout.Kind;
using M = ProjectHymn.Movement.CliffsideLayout.MarkerKind;
using Block = ProjectHymn.Movement.CliffsideLayout.Block;
using Marker = ProjectHymn.Movement.CliffsideLayout.Marker;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// Starlit Spring: a brawler arena in the night sky (Slice 11). A floating island seen from a fixed camera: the ground
    /// spans it with open edges (falling off costs a heart and puts you back in the middle), two low side daises and one
    /// high crown, all one-way (jump up through them; Down + Jump drops through). Enemies come in three waves, some on
    /// the ground, some on the platforms; they jump up and drop down after you. The way out opens after the last wave.
    /// </summary>
    public static class SpringLayout
    {
        public const float IslandHalf = 13f;
        public const float DaisTop = 2.6f, CrownTop = 5.0f;

        public static readonly Block[] Route =
        {
            new Block("Island", -IslandHalf, IslandHalf, 0f, K.Ground),
        };

        public static readonly Block[] Platforms =
        {
            new Block("West Dais", -10.2f, -5.4f, DaisTop, K.Platform),
            new Block("East Dais",   5.4f, 10.2f, DaisTop, K.Platform),
            new Block("Crown",      -2.7f,  2.7f, CrownTop, K.Platform),
        };

        public static readonly Marker[] Markers =
        {
            new Marker("Spawn",                     M.Spawn,      0f, 0f),
            new Marker("Checkpoint 1",              M.Checkpoint, 0f, 0f),
            // Wave 1: one on the ground, one up on the west dais.
            new Marker("Wave 1 Knight",             M.Target,     7f, 0f),
            new Marker("Wave 1 Knight West Dais",   M.Target,    -7.8f, DaisTop),
            // Wave 2: a dancer on the ground, a knight on the east dais, a wisp (it flies wherever you are).
            new Marker("Wave 2 Bone Dancer",        M.Dancer,    -6f, 0f),
            new Marker("Wave 2 Knight East Dais",   M.Target,     7.8f, DaisTop),
            new Marker("Wave 2 Wisp",               M.FloatingTarget, 3f, 1.4f),
            // Wave 3: the lead dancer holds the crown; knights on the ground either side.
            new Marker("Wave 3 Lead Bone Dancer Crown", M.Dancer,  0f, CrownTop),
            new Marker("Wave 3 Knight West",        M.Target,    -9f, 0f),
            new Marker("Wave 3 Knight East",        M.Target,     9f, 0f),
            new Marker("Goal",                      M.Goal,      11.4f, 0f),
            new Marker("Spring View",               M.CameraZone, 0f, 0f, 80f),
        };

        public static StageLayout Stage => new StageLayout
        {
            Id = "Spring", Name = "Starlit Spring", Scene = "Spring", Meter = 4,
            ParSeconds = 90f, PerfectAttacks = 34,          // placeholders until playtest
            Route = Route, Platforms = Platforms, Markers = Markers, OpenEdges = true,
        };
    }
}
