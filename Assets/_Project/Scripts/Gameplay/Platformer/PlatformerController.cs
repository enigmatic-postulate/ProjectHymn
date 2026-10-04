using System;
using ProjectHymn.Movement;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Side-scrolling movement for vertical slice 1: run along a lane (world X, locked at <see cref="laneZ"/>), jump,
    /// double jump, grab and climb ledges, respawn at the last checkpoint after a fall. Movement is free (never
    /// judged against the beat); on-beat attacks in the air hang the player briefly so combos can be played airborne.
    /// Implements <see cref="ICombatBody"/> for the sword combat and <see cref="ILocomotionSource"/> for the animator.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-10)]
    public class PlatformerController : MonoBehaviour, ICombatBody, ILocomotionSource
    {
        [Header("Run")]
        [SerializeField] float runSpeed = SliceTuning.RunSpeed;
        [SerializeField] float groundAccel = 70f;
        [SerializeField] float groundDecel = 90f;
        [SerializeField] float airAccel = 40f;
        [Tooltip("While a move plays on the ground, running slows to this share.")]
        [SerializeField, Range(0f, 1f)] float attackMoveScale = 0.45f;

        [Header("Jump")]
        [SerializeField] float jumpHeight = SliceTuning.JumpHeight;
        [SerializeField] float airJumpHeight = SliceTuning.AirJumpHeight;
        [SerializeField] float gravity = SliceTuning.Gravity;
        [SerializeField] float fallMultiplier = SliceTuning.FallMultiplier;
        [SerializeField] float maxFallSpeed = SliceTuning.MaxFallSpeed;
        [SerializeField] float coyoteTime = 0.1f;
        [SerializeField] float jumpBuffer = 0.12f;
        [Tooltip("Rising speed is multiplied by this when jump is let go early.")]
        [SerializeField, Range(0.1f, 1f)] float releaseCut = 0.5f;

        [Header("Ledges")]
        [SerializeField] float grabMin = SliceTuning.GrabMin;
        [SerializeField] float grabMax = SliceTuning.GrabMax;
        [SerializeField] float hangDrop = SliceTuning.HangDrop;
        [SerializeField] float climbSeconds = 0.38f;
        [SerializeField] float hangInputDelay = 0.12f;
        [SerializeField] LayerMask ground = ~0;

        [Header("Air attacks")]
        [Tooltip("An on-beat attack in the air stops the fall this long.")]
        [SerializeField] float airHangSeconds = 0.16f;
        [SerializeField] int airHangsPerJump = 3;

        [Header("Lane and look")]
        [SerializeField] float laneZ;
        [SerializeField] Transform visual;
        [Tooltip("Turn the model this far toward the camera so the face reads (degrees).")]
        [SerializeField] float cameraYaw = 14f;
        [SerializeField] float turnSpeed = 1440f;

        [Header("Respawn")]
        [SerializeField] float killY = CliffsideLayout.KillY;

        CharacterController _cc;
        PlayerInputReader _input;
        readonly JumpRules _rules = new JumpRules();
        Vector3 _vel;
        int _face = 1;
        LocoState _state = LocoState.Idle;
        float _stateTime, _grabCooldown, _airHang;
        int _airHangsLeft;
        float _ledgeTop, _wallX;
        Vector3 _climbFrom, _climbTo;
        Vector3 _respawn;
        Checkpoint _checkpoint;

        public LocoState State => _state;
        public float StateTime => _stateTime;
        public float Speed01 => Mathf.Clamp01(Mathf.Abs(_vel.x) / Mathf.Max(0.1f, runSpeed));
        public float VerticalSpeed => _vel.y;
        public float ClimbProgress => _state == LocoState.Climb ? Mathf.Clamp01(_stateTime / climbSeconds) : 0f;
        public bool Grounded => _cc != null && _cc.enabled && _cc.isGrounded;
        public Vector3 LedgePoint => new Vector3(_wallX, _ledgeTop, laneZ);
        /// <summary>Feet below the ledge top while hanging (set from the character's arm reach).</summary>
        public float HangDrop { get => hangDrop; set => hangDrop = value; }
        public Vector2 Facing => new Vector2(_face, 0f);
        public bool Attacking { get; set; }
        public bool CanAttack => _state != LocoState.Hang && _state != LocoState.Climb;
        public float RunSpeed => runSpeed;
        public Checkpoint CurrentCheckpoint => _checkpoint;

        public event Action<JumpKind> Jumped;
        /// <summary>Landed; argument is the downward speed at touchdown.</summary>
        public event Action<float> Landed;
        public event Action LedgeGrabbed;
        public event Action Respawned;
        public event Action<Checkpoint> CheckpointReached;

        JumpArc Arc => new JumpArc(gravity, fallMultiplier, maxFallSpeed);

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _rules.CoyoteTime = coyoteTime;
            _rules.BufferTime = jumpBuffer;
            _rules.ReleaseCut = releaseCut;
            _rules.AirJumps = 1;
            _respawn = transform.position;
            _airHangsLeft = airHangsPerJump;
            SnapVisual();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _stateTime += dt;
            _grabCooldown -= dt;

            switch (_state)
            {
                case LocoState.Hang: UpdateHang(); break;
                case LocoState.Climb: UpdateClimb(); break;
                default: UpdateMove(dt); break;
            }

            if (transform.position.y < killY) Respawn();
            TurnVisual(dt);
        }

        // ---------------- Running and jumping ----------------

        void UpdateMove(float dt)
        {
            float inX = _input != null ? _input.Move.x : 0f;
            if (Mathf.Abs(inX) < 0.2f) inX = 0f;
            bool wasGrounded = _cc.isGrounded;
            var jump = _rules.Tick(dt, wasGrounded, _input != null && _input.JumpPressed);

            float want = inX * runSpeed * (Attacking && wasGrounded ? attackMoveScale : 1f);
            float accel = !wasGrounded ? airAccel : Mathf.Abs(want) > 0.01f ? groundAccel : groundDecel;
            _vel.x = Mathf.MoveTowards(_vel.x, want, accel * dt);
            if (inX != 0f && !Attacking) _face = inX > 0f ? 1 : -1;

            if (_airHang > 0f) { _airHang -= dt; _vel.y = Mathf.Max(_vel.y, 0f) * 0.5f; }
            else if (wasGrounded && _vel.y <= 0f) _vel.y = -2f;           // keep contact on slopes and steps
            else _vel.y = Arc.Step(_vel.y, dt);

            if (jump == JumpKind.Ground)
            {
                _vel.y = JumpRules.SpeedForHeight(jumpHeight, gravity);
                SetState(LocoState.Jump);
                Jumped?.Invoke(jump);
            }
            else if (jump == JumpKind.Air)
            {
                _vel.y = JumpRules.SpeedForHeight(airJumpHeight, gravity);
                _airHang = 0f;
                if (inX != 0f) _vel.x = want;                              // the double jump can change direction
                SetState(LocoState.AirJump);
                Jumped?.Invoke(jump);
            }
            _vel.y = _rules.ApplyRelease(_vel.y, _input != null && _input.JumpHeld);

            float fallSpeed = -_vel.y;
            var flags = _cc.Move(new Vector3(_vel.x, _vel.y, 0f) * dt);
            if ((flags & CollisionFlags.Above) != 0 && _vel.y > 0f) _vel.y = 0f;
            if ((flags & CollisionFlags.Sides) != 0 && Mathf.Abs(_vel.x) > 0.1f && SideBlocked()) _vel.x = 0f;
            float dz = laneZ - transform.position.z;
            if (Mathf.Abs(dz) > 0.0005f) _cc.Move(new Vector3(0f, 0f, dz));

            bool grounded = _cc.isGrounded;
            if (grounded)
            {
                bool airborne = _state == LocoState.Jump || _state == LocoState.AirJump || _state == LocoState.Fall;
                if (airborne)
                {
                    _airHangsLeft = airHangsPerJump;
                    _airHang = 0f;
                    Landed?.Invoke(fallSpeed);
                    if (fallSpeed > 9f) { SetState(LocoState.Land); return; }
                }
                if (_state == LocoState.Land && _stateTime < 0.14f) return;
                var ground = Mathf.Abs(_vel.x) > 0.4f ? LocoState.Run : LocoState.Idle;
                if (_state != ground) SetState(ground);
                return;
            }

            if (_state == LocoState.Idle || _state == LocoState.Run || _state == LocoState.Land) SetState(LocoState.Fall);
            else if (_state == LocoState.Jump && _vel.y < 0f) SetState(LocoState.Fall);
            else if (_state == LocoState.AirJump && _vel.y < -2f && _stateTime > 0.45f) SetState(LocoState.Fall);
            TryGrabLedge();
        }

        bool SideBlocked() =>
            Physics.Raycast(transform.position + Vector3.up * (_cc.height * 0.5f), new Vector3(Mathf.Sign(_vel.x), 0f, 0f),
                _cc.radius + 0.08f, ground, QueryTriggerInteraction.Ignore);

        // ---------------- Ledges ----------------

        bool TryGrabLedge()
        {
            if (_grabCooldown > 0f || _vel.y > 3f || Attacking || _input == null) return false;
            if (_input.Move.y < -0.5f || _input.Move.x * _face < -0.2f) return false;   // holding down or away: no grab
            var p = transform.position;
            float r = _cc.radius;
            var dir = new Vector3(_face, 0f, 0f);

            // The top of the lip, just past the wall in front.
            var probe = p + dir * (r + 0.3f) + Vector3.up * (grabMax + 0.4f);
            if (!Physics.Raycast(probe, Vector3.down, out var top, grabMax + 0.4f - grabMin, ground, QueryTriggerInteraction.Ignore)) return false;
            if (top.normal.y < 0.7f || top.collider.attachedRigidbody != null || top.collider.GetComponentInParent<IBeatDamageable>() != null) return false;
            float rel = top.point.y - p.y;
            if (rel < grabMin || rel > grabMax) return false;

            // The face of the wall, just under the lip.
            if (!Physics.Raycast(new Vector3(p.x, top.point.y - 0.08f, p.z), dir, out var wall, r + 0.7f, ground, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(wall.normal.x) < 0.7f) return false;

            // Room to stand on top.
            var stand = new Vector3(wall.point.x + _face * (r + 0.15f), top.point.y + 0.02f, laneZ);
            if (Physics.CheckCapsule(stand + Vector3.up * (r + 0.05f), stand + Vector3.up * (_cc.height - r), r * 0.9f, ground, QueryTriggerInteraction.Ignore)) return false;

            _ledgeTop = top.point.y;
            _wallX = wall.point.x;
            _climbTo = stand;
            _vel = Vector3.zero;
            _airHang = 0f;
            _rules.RefillAirJumps();
            _airHangsLeft = airHangsPerJump;
            Teleport(new Vector3(_wallX - _face * (r + 0.03f), _ledgeTop - hangDrop, laneZ), keepDisabled: true);
            SetState(LocoState.Hang);
            LedgeGrabbed?.Invoke();
            return true;
        }

        void UpdateHang()
        {
            if (_stateTime < hangInputDelay || _input == null) return;
            float x = _input.Move.x * _face, y = _input.Move.y;
            if (_input.JumpPressed && x < -0.5f)
            {
                // Kick off the wall, away from it.
                LeaveHang();
                _face = -_face;
                _vel = new Vector3(_face * runSpeed * 0.8f, JumpRules.SpeedForHeight(jumpHeight * 0.8f, gravity), 0f);
                SetState(LocoState.Jump);
                Jumped?.Invoke(JumpKind.Ground);
            }
            else if (_input.JumpPressed || y > 0.5f || x > 0.5f)
            {
                _climbFrom = transform.position;
                SetState(LocoState.Climb);
            }
            else if (y < -0.5f)
            {
                LeaveHang();
                SetState(LocoState.Fall);
            }
        }

        void LeaveHang()
        {
            _grabCooldown = 0.3f;
            _vel = Vector3.zero;
            _cc.enabled = true;
        }

        void UpdateClimb()
        {
            float k = ClimbProgress;
            // Pull up first, then step over the lip.
            float up = Smooth(Mathf.Clamp01(k / 0.6f));
            float over = Smooth(Mathf.Clamp01((k - 0.45f) / 0.55f));
            transform.position = new Vector3(Mathf.Lerp(_climbFrom.x, _climbTo.x, over), Mathf.Lerp(_climbFrom.y, _climbTo.y, up), laneZ);
            if (k < 1f) return;
            Teleport(_climbTo, keepDisabled: false);
            _vel = Vector3.zero;
            SetState(LocoState.Idle);
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        // ---------------- Combat hooks ----------------

        public void FaceToward(Vector3 point)
        {
            float dx = point.x - transform.position.x;
            if (Mathf.Abs(dx) < 0.05f || !CanAttack) return;
            _face = dx > 0f ? 1 : -1;
            SnapVisual();
        }

        public void AttackStarted(bool onBeat)
        {
            if (!onBeat || _cc.isGrounded || !CanAttack || _airHangsLeft <= 0 || _vel.y > 1.5f) return;
            _airHangsLeft--;
            _airHang = airHangSeconds;
        }

        /// <summary>Knocked back by a hit: lets go of ledges and flies a little up and away.</summary>
        public void Knockback(float directionX, float speed = 6f, float lift = 5f)
        {
            if (_state == LocoState.Hang || _state == LocoState.Climb) LeaveHang();
            _vel = new Vector3(Mathf.Sign(directionX) * speed, lift, 0f);
            _airHang = 0f;
            SetState(LocoState.Fall);
        }

        // ---------------- Checkpoints and respawn ----------------

        void OnTriggerEnter(Collider other)
        {
            var cp = other.GetComponentInParent<Checkpoint>();
            if (cp != null && cp != _checkpoint)
            {
                _checkpoint = cp;
                _respawn = cp.SpawnPoint;
                cp.Activate();
                CheckpointReached?.Invoke(cp);
            }
            var goal = other.GetComponentInParent<SliceGoal>();
            if (goal != null) goal.Reach(this);
        }

        public void SetRespawn(Vector3 point) { _respawn = point; _checkpoint = null; }

        public void Respawn()
        {
            Teleport(_respawn, keepDisabled: false);
            _vel = Vector3.zero;
            _airHang = 0f;
            _rules.Reset();
            SetState(LocoState.Idle);
            Respawned?.Invoke();
        }

        void Teleport(Vector3 position, bool keepDisabled)
        {
            _cc.enabled = false;
            transform.position = position;
            if (!keepDisabled) _cc.enabled = true;
        }

        void SetState(LocoState s)
        {
            _state = s;
            _stateTime = 0f;
        }

        // ---------------- Model facing ----------------

        /// <summary>Face along the lane, turned a little toward the camera (which looks along +Z).</summary>
        Quaternion FacingRotation => Quaternion.AngleAxis(_face * cameraYaw, Vector3.up) * Quaternion.LookRotation(new Vector3(_face, 0f, 0f), Vector3.up);

        void SnapVisual() { if (visual != null) visual.rotation = FacingRotation; }

        void TurnVisual(float dt)
        {
            if (visual == null) return;
            visual.rotation = Quaternion.RotateTowards(visual.rotation, FacingRotation, turnSpeed * dt);
        }

        void OnDrawGizmosSelected()
        {
            var c = GetComponent<CharacterController>();
            if (c == null) return;
            var p = transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(p + Vector3.up * grabMin + Vector3.right * _face * 0.6f, p + Vector3.up * grabMax + Vector3.right * _face * 0.6f);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(p.x - 5f, killY, laneZ), new Vector3(p.x + 5f, killY, laneZ));
        }
    }
}
