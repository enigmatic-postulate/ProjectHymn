using System;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// The platformer's vertical motion as plain numbers: gravity, a heavier fall, and a terminal speed. Shared by
    /// <c>PlatformerController</c> and by the tests that prove each cliff in the slice level can be reached.
    /// </summary>
    [Serializable]
    public struct JumpArc
    {
        public float Gravity;          // positive, m/s²
        public float FallMultiplier;   // gravity scale while falling
        public float MaxFallSpeed;     // m/s, positive

        public JumpArc(float gravity, float fallMultiplier, float maxFallSpeed)
        {
            Gravity = gravity; FallMultiplier = fallMultiplier; MaxFallSpeed = maxFallSpeed;
        }

        public static JumpArc Default => new JumpArc(28f, 1.6f, 18f);

        /// <summary>Advance vertical speed by one step.</summary>
        public float Step(float vy, float dt)
        {
            float g = vy > 0f ? Gravity : Gravity * FallMultiplier;
            vy -= g * dt;
            return vy < -MaxFallSpeed ? -MaxFallSpeed : vy;
        }

        /// <summary>
        /// The highest point reached with a ground jump of <paramref name="jumpSpeed"/>, then (if given) an air jump of
        /// <paramref name="airJumpSpeed"/> fired at the apex. Metres above take-off.
        /// </summary>
        public float MaxHeight(float jumpSpeed, float airJumpSpeed = 0f)
        {
            float h = JumpRules.ApexHeight(jumpSpeed, Gravity);
            if (airJumpSpeed > 0f) h += JumpRules.ApexHeight(airJumpSpeed, Gravity);
            return h;
        }

        /// <summary>
        /// Horizontal distance covered at <paramref name="runSpeed"/> before falling back to <paramref name="landingHeight"/>
        /// (relative to take-off; negative = lower), using a ground jump and optionally an air jump at the apex.
        /// Returns 0 if the landing height is never reached on the way down.
        /// </summary>
        public float Distance(float runSpeed, float jumpSpeed, float airJumpSpeed, float landingHeight, float dt = 1f / 240f)
        {
            float y = 0f, vy = jumpSpeed, t = 0f, apex = 0f;
            bool airUsed = airJumpSpeed <= 0f;
            for (int i = 0; i < 100000; i++)
            {
                float prevVy = vy;
                vy = Step(vy, dt);
                if (!airUsed && prevVy > 0f && vy <= 0f) { vy = airJumpSpeed; airUsed = true; }
                y += vy * dt;
                t += dt;
                if (y > apex) apex = y;
                if (vy < 0f && y <= landingHeight) return apex >= landingHeight ? runSpeed * t : 0f;
            }
            return 0f;
        }
    }
}
