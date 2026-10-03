using System;
using System.Collections;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Free real-time movement on the ground plane (never beat-locked), the Step dash and the M6 jump.
    /// 3D physics: the Rigidbody slides on X/Z, height and tilt are locked. The visual model turns to face the
    /// move direction. A jump lifts only the visual (the body stays on the ground plane), lasts one beat and
    /// gives no invulnerability (GDD M6). While high enough, the player passes over <see cref="Hurdle"/>s.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float stepDistance = 2.5f;
        [SerializeField] float stepDuration = 0.12f;
        [SerializeField] float rollDistance = 1.2f;
        [SerializeField] float rollDuration = 0.3f;
        [Tooltip("The model to turn toward the facing direction (usually the 'Visual' child).")]
        [SerializeField] Transform visual;
        [SerializeField] float turnSpeed = 1080f;

        [Header("Jump (M6)")]
        [SerializeField] float jumpHeight = 1.3f;
        [Tooltip("Off-beat or winded jumps only reach this share of the full height.")]
        [SerializeField, Range(0.2f, 1f)] float weakJumpScale = 0.6f;
        [Tooltip("Above this height the player clears hurdles.")]
        [SerializeField] float hurdleClearHeight = 0.45f;
        [SerializeField] float slamSeconds = 0.08f;
        [Tooltip("Air control: share of normal move speed while airborne.")]
        [SerializeField, Range(0f, 1f)] float airControl = 0.9f;

        Rigidbody _body;
        Collider _collider;
        PlayerInputReader _input;
        bool _dashing;
        bool _airborne, _slamming, _ignoringHurdles;
        float _airTime, _airDuration, _peak, _slamTime, _slamFrom;

        /// <summary>Facing on the ground plane (x = world X, y = world Z).</summary>
        public Vector2 Facing { get; private set; } = new Vector2(0f, -1f);
        /// <summary>Armor can slow or speed the player (1 = normal).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public bool Invulnerable { get; private set; }
        public Transform Visual { get => visual; set => visual = value; }
        public bool IsAirborne => _airborne;
        /// <summary>Current jump height of the visual above the ground.</summary>
        public float Height { get; private set; }
        /// <summary>0 at take-off, 1 at landing.</summary>
        public float AirProgress => _airborne && _airDuration > 0f ? Mathf.Clamp01(_airTime / _airDuration) : 0f;

        /// <summary>The player touched down. Arg: true when the landing was a slam (Stomp).</summary>
        public event Action<bool> Landed;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
            _body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _collider = GetComponent<Collider>();
            _input = GetComponent<PlayerInputReader>();
        }

        void OnDisable() { if (_body != null) SetVelocity(Vector2.zero); }

        void FixedUpdate()
        {
            UpdateHurdles();
            if (_dashing || _input == null) return;
            var move = _input.Move;
            if (move.sqrMagnitude > 0.01f) Facing = move.normalized;
            float speed = moveSpeed * SpeedMultiplier * (_airborne ? airControl : 1f);
            SetVelocity(move * speed);
        }

        void Update()
        {
            if (_airborne) TickAir(Time.deltaTime);
            if (visual != null)
            {
                visual.rotation = Ground.TurnToward(visual.rotation, Facing, turnSpeed, Time.deltaTime);
                visual.localPosition = new Vector3(0f, Height, 0f);
            }
        }

        void SetVelocity(Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = Ground.Dir(v);
#else
            _body.velocity = Ground.Dir(v);
#endif
        }

        // ---------------- Step ----------------

        /// <summary>On-beat Step: fast, long, invulnerable. Off-beat: a slow roll with no i-frames.</summary>
        public void Dash(bool onBeat) => Dash(onBeat, 1f);

        /// <summary>Step with a distance scale (the Lunge Phrase dashes further).</summary>
        public void Dash(bool onBeat, float distanceScale)
        {
            if (_dashing || !isActiveAndEnabled) return;
            StartCoroutine(DashRoutine(onBeat, distanceScale));
        }

        IEnumerator DashRoutine(bool onBeat, float distanceScale)
        {
            _dashing = true;
            Invulnerable = onBeat;
            float dist = (onBeat ? stepDistance : rollDistance) * distanceScale;
            float dur = onBeat ? stepDuration : rollDuration;
            var dir = _input != null && _input.Move.sqrMagnitude > 0.01f ? _input.Move.normalized : Facing;
            Facing = dir;
            SetVelocity(dir * (dist / dur));
            yield return new WaitForSeconds(dur);
            SetVelocity(Vector2.zero);
            Invulnerable = false;
            _dashing = false;
        }

        // ---------------- Jump ----------------

        /// <summary>Jump for <paramref name="seconds"/> (one beat). Full height on the beat, lower otherwise.</summary>
        public void Jump(float seconds, bool full)
        {
            if (_airborne || !isActiveAndEnabled) return;
            _airborne = true;
            _slamming = false;
            _airTime = 0f;
            _airDuration = Mathf.Max(0.18f, seconds);
            _peak = jumpHeight * (full ? 1f : weakJumpScale);
        }

        /// <summary>Drop straight down (Smash in the air / Stomp).</summary>
        public void SlamDown()
        {
            if (!_airborne || _slamming) return;
            _slamming = true;
            _slamTime = 0f;
            _slamFrom = Height;
        }

        void TickAir(float dt)
        {
            if (_slamming)
            {
                _slamTime += dt;
                float k = Mathf.Clamp01(_slamTime / Mathf.Max(0.01f, slamSeconds));
                Height = Mathf.Lerp(_slamFrom, 0f, k * k);
                if (k >= 1f) Land(true);
                return;
            }
            _airTime += dt;
            float t = Mathf.Clamp01(_airTime / _airDuration);
            Height = 4f * _peak * t * (1f - t);
            if (t >= 1f) Land(false);
        }

        void Land(bool slam)
        {
            _airborne = false;
            _slamming = false;
            Height = 0f;
            Landed?.Invoke(slam);
        }

        void UpdateHurdles()
        {
            if (_collider == null) return;
            bool clear = _airborne && Height >= hurdleClearHeight;
            if (clear == _ignoringHurdles) return;
            _ignoringHurdles = clear;
            foreach (var h in Hurdle.All)
                if (h != null && h.Collider != null) Physics.IgnoreCollision(_collider, h.Collider, clear);
        }
    }
}
