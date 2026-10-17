namespace ProjectHymn.Movement
{
    /// <summary>
    /// VS-6's hub, the Air Bell, as plain data: the divers' camp on the stone floor inside the drowned cathedral's great
    /// bell, which rests mouth-down on the nave and holds a pocket of air. One flat deck along the lane between two walls,
    /// the bell's bronze curving overhead, the dive pool at the east end (the gate to the deep's stages).
    /// </summary>
    public static class AirBellLayout
    {
        public const float GroundTop = 0f;
        public const float XMin = -18f, XMax = 34f;
        /// <summary>The bell's axis (it rests over the middle of the deck, its back half behind the lane).</summary>
        public const float BellX = 8f, BellZ = 8f, BellRadius = 30f;

        // Spawn points (ids the session travels to): the stages come home to "FromNave" by the pool.
        public static readonly VillageLayout.Spot[] Spawns =
        {
            new VillageLayout.Spot("Start", -4f),
            new VillageLayout.Spot("FromNave", 26f),
        };

        public const float LampX = -11f, PoolX = 30f;
        public static readonly VillageLayout.Spot[] Divers =
        {
            new VillageLayout.Spot("Diver", 2f),
            new VillageLayout.Spot("Bellwarden", -9f),
            new VillageLayout.Spot("Novice", 16f),
        };
    }
}
