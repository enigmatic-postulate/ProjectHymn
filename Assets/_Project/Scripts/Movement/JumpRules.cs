using System;

namespace ProjectHymn.Movement
{
    public enum JumpKind { None, Ground, Air }

    /// <summary>
    /// The platformer's jump decisions, as plain C# so they can be unit-tested (vertical slice 1).
    /// - Coyote time: a ground jump still works for a moment after running off a ledge.
    /// - Jump buffer: a press shortly before landing jumps on touchdown.
    /// - Air jumps (the double jump) refill on landing or when grabbing a ledge. They need a fresh press; a buffered
    ///   press never spends one, so pressing just before landing gives a ground jump instead.
    /// - Variable height: letting go of jump while rising cuts the upward speed once.
    /// Jumping is free: it is never judged against the beat (only attacks are).
    /// </summary>
    public sealed class JumpRules
    {
        public float CoyoteTime = 0.1f;
        public float BufferTime = 0.12f;
        public int AirJumps = 1;
        /// <summary>Upward speed is multiplied by this when jump is released early.</summary>
        public float ReleaseCut = 0.5f;
        /// <summary>After a jump, ignore "grounded" this long (the controller may still touch the floor for a frame).</summary>
        public float GroundLockout = 0.08f;

        float _sinceGrounded = float.PositiveInfinity;
        float _buffer;
        float _lockout;
        int _airUsed;
        bool _jumpedSinceGrounded;
        bool _cutAvailable;

        public int AirJumpsLeft => Math.Max(0, AirJumps - _airUsed);
        public bool Grounded { get; private set; }

        /// <summary>Forget everything (respawn).</summary>
        public void Reset()
        {
            _sinceGrounded = float.PositiveInfinity;
            _buffer = 0f;
            _lockout = 0f;
            _airUsed = 0;
            _jumpedSinceGrounded = false;
            _cutAvailable = false;
            Grounded = false;
        }

        /// <summary>Refill air jumps without landing (ledge grab).</summary>
        public void RefillAirJumps()
        {
            _airUsed = 0;
            _jumpedSinceGrounded = true;     // no coyote jump out of a hang: the controller handles hang jumps itself
            _sinceGrounded = float.PositiveInfinity;
        }

        /// <summary>
        /// Call once per frame. <paramref name="grounded"/> is the controller's floor contact, <paramref name="pressed"/>
        /// is true on the frame jump went down. Returns which jump to perform this frame, if any.
        /// </summary>
        public JumpKind Tick(float dt, bool grounded, bool pressed)
        {
            if (_lockout > 0f) { _lockout -= dt; grounded = false; }
            Grounded = grounded;
            if (grounded)
            {
                _sinceGrounded = 0f;
                _airUsed = 0;
                _jumpedSinceGrounded = false;
                _cutAvailable = false;
            }
            else _sinceGrounded += dt;

            if (pressed) _buffer = BufferTime;
            else _buffer -= dt;
            if (_buffer <= 0f) return JumpKind.None;

            if (!_jumpedSinceGrounded && (grounded || _sinceGrounded <= CoyoteTime))
            {
                Consume();
                return JumpKind.Ground;
            }
            if (pressed && !grounded && _airUsed < AirJumps)
            {
                _airUsed++;
                Consume();
                return JumpKind.Air;
            }
            return JumpKind.None;
        }

        void Consume()
        {
            _buffer = 0f;
            _jumpedSinceGrounded = true;
            _sinceGrounded = float.PositiveInfinity;
            _lockout = GroundLockout;
            _cutAvailable = true;
            Grounded = false;
        }

        /// <summary>
        /// Variable jump height: returns the vertical speed to use this frame. When jump is not held while rising after
        /// a jump, the speed is cut once.
        /// </summary>
        public float ApplyRelease(float verticalSpeed, bool held)
        {
            if (!_cutAvailable) return verticalSpeed;
            if (verticalSpeed <= 0f) { _cutAvailable = false; return verticalSpeed; }
            if (held) return verticalSpeed;
            _cutAvailable = false;
            return verticalSpeed * ReleaseCut;
        }

        // ---------------- Arc maths (tuning and the level reachability test) ----------------

        /// <summary>Launch speed that peaks at <paramref name="height"/> under <paramref name="gravity"/> (positive).</summary>
        public static float SpeedForHeight(float height, float gravity) => (float)Math.Sqrt(2.0 * gravity * Math.Max(0f, height));

        public static float ApexHeight(float speed, float gravity) => speed * speed / (2f * gravity);
    }
}
