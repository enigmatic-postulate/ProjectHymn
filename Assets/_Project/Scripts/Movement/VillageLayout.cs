namespace ProjectHymn.Movement
{
    /// <summary>
    /// The village hub as plain data: one flat street along the lane between two walls, the buildings behind it, a
    /// floating ledge with a lore piece, and the gate to the cliffs. The editor's hub builder turns this into the scene;
    /// the tests check the ledge against the jump arcs.
    /// </summary>
    public static class VillageLayout
    {
        public const float GroundTop = 0f;
        public const float XMin = -24f, XMax = 64f;

        public readonly struct Spot
        {
            public readonly string Name;
            public readonly float X;
            public Spot(string name, float x) { Name = name; X = x; }
        }

        // Spawn points (ids the session travels to)
        public static readonly Spot[] Spawns =
        {
            new Spot("Start", -4f),
            new Spot("Inn", -10f),
            new Spot("Trainer", 28f),
            new Spot("FromCliffside", 55f),
        };

        // Buildings and the people in front of them
        public const float InnX = -10f, ShopX = 8f, TrainerX = 28f, GateX = 59f;
        public static readonly Spot[] Villagers =
        {
            new Spot("Elder", -1f),
            new Spot("Fisher", 18f),
            new Spot("Lookout", 44f),
        };

        /// <summary>A floating stone ledge (double jump up) holding a lore piece.</summary>
        public const float LedgeXMin = 48f, LedgeXMax = 51f, LedgeTop = 3.0f, LedgeThickness = 0.6f;
    }
}
