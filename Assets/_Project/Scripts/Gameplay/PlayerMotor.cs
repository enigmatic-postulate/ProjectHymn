using System.Collections;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Free real-time movement on the ground plane (never beat-locked) plus the Step dash. 3D physics:
    /// the Rigidbody slides on X/Z, height and tilt are locked. The visual model turns to face the move direction.
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

        Rigidbody _body;
        PlayerInputReader _input;
        bool _dashing;

        /// <summary>Facing on the ground plane (x = world X, y = world Z).</summary>
        public Vector2 Facing { get; private set; } = new Vector2(0f, -1f);
        /// <summary>Armor can slow or speed the player (1 = normal).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public bool Invulnerable { get; private set; }
        public Transform Visual { get => visual; set => visual = value; }

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
            _body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _input = GetComponent<PlayerInputReader>();
        }

        void OnDisable() { if (_body != null) SetVelocity(Vector2.zero); }

        void FixedUpdate()
        {
            if (_dashing || _input == null) return;
            var move = _input.Move;
            if (move.sqrMagnitude > 0.01f) Facing = move.normalized;
            SetVelocity(move * moveSpeed * SpeedMultiplier);
        }

        void Update()
        {
            if (visual != null) visual.rotation = Ground.TurnToward(visual.rotation, Facing, turnSpeed, Time.deltaTime);
        }

        void SetVelocity(Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = Ground.Dir(v);
#else
            _body.velocity = Ground.Dir(v);
#endif
        }

        /// <summary>On-beat Step: fast, long, invulnerable. Off-beat: a slow roll with no i-frames.</summary>
        public void Dash(bool onBeat)
        {
            if (_dashing || !isActiveAndEnabled) return;
            StartCoroutine(DashRoutine(onBeat));
        }

        IEnumerator DashRoutine(bool onBeat)
        {
            _dashing = true;
            Invulnerable = onBeat;
            float dist = onBeat ? stepDistance : rollDistance;
            float dur = onBeat ? stepDuration : rollDuration;
            var dir = _input != null && _input.Move.sqrMagnitude > 0.01f ? _input.Move.normalized : Facing;
            Facing = dir;
            SetVelocity(dir * (dist / dur));
            yield return new WaitForSeconds(dur);
            SetVelocity(Vector2.zero);
            Invulnerable = false;
            _dashing = false;
        }
    }
}
