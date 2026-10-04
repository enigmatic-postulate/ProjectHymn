namespace ProjectHymn.Movement
{
    /// <summary>
    /// Default movement numbers for vertical slice 1. The PlatformerController starts from these, and the level
    /// reachability tests use them, so a change here is checked against every cliff in the slice.
    /// </summary>
    public static class SliceTuning
    {
        public const float RunSpeed = 6.5f;
        public const float JumpHeight = 2.0f;        // ground jump apex, metres
        public const float AirJumpHeight = 1.6f;     // extra height from the double jump
        public const float Gravity = 28f;
        public const float FallMultiplier = 1.6f;
        public const float MaxFallSpeed = 18f;

        /// <summary>A ledge can be grabbed when its top is between these heights above the feet.</summary>
        public const float GrabMin = 1.1f;
        public const float GrabMax = 2.05f;
        /// <summary>While hanging, the feet are this far below the ledge top (hands on the edge, arms up).</summary>
        public const float HangDrop = 1.95f;

        public static JumpArc Arc => new JumpArc(Gravity, FallMultiplier, MaxFallSpeed);
        public static float JumpSpeed => JumpRules.SpeedForHeight(JumpHeight, Gravity);
        public static float AirJumpSpeed => JumpRules.SpeedForHeight(AirJumpHeight, Gravity);
    }
}
