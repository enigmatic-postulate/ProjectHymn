using System.Collections;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Free real-time movement on the ground (never beat-locked) and a Step dash. 3D physics on X/Z; the model
    /// ("visual") turns toward the move direction, or snaps toward the target when an attack starts.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMotor : MonoBehaviour, ICombatBody
    {
        [SerializeField] float moveSpeed = 4.5f;
        [SerializeField] float stepDistance = 2.2f;
        [SerializeField] float stepDuration = 0.14f;
        [SerializeField] Transform visual;
        [SerializeField] float turnSpeed = 900f;
        [Tooltip("While attacking, movement is slowed to this share.")]
        [SerializeField, Range(0f, 1f)] float attackMoveScale = 0.35f;

        Rigidbody _body;
        PlayerInputReader _input;
        bool _dashing;

        /// <summary>Facing on the ground (x = world X, y = world Z).</summary>
        public Vector2 Facing { get; private set; } = new Vector2(0f, 1f);
        public bool Attacking { get; set; }
        public bool CanAttack => true;
        public void AttackStarted(bool onBeat) { }

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
            _body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _input = GetComponent<PlayerInputReader>();
        }

        void FixedUpdate()
        {
            if (_dashing || _input == null) return;
            var move = _input.Move;
            if (move.sqrMagnitude > 0.01f && !Attacking) Facing = move.normalized;
            SetVelocity(move * moveSpeed * (Attacking ? attackMoveScale : 1f));
        }

        void Update()
        {
            if (_input != null && _input.StepPressed) Dash();
            if (visual == null) return;
            var want = Quaternion.LookRotation(new Vector3(Facing.x, 0f, Facing.y), Vector3.up);
            visual.rotation = Quaternion.RotateTowards(visual.rotation, want, turnSpeed * Time.deltaTime);
        }

        /// <summary>Turn to face a point at once (an attack aimed at the nearest target).</summary>
        public void FaceToward(Vector3 point)
        {
            var d = new Vector2(point.x - transform.position.x, point.z - transform.position.z);
            if (d.sqrMagnitude < 0.0001f) return;
            Facing = d.normalized;
            if (visual != null) visual.rotation = Quaternion.LookRotation(new Vector3(Facing.x, 0f, Facing.y), Vector3.up);
        }

        void SetVelocity(Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = new Vector3(v.x, 0f, v.y);
#else
            _body.velocity = new Vector3(v.x, 0f, v.y);
#endif
        }

        public void Dash()
        {
            if (_dashing || !isActiveAndEnabled) return;
            StartCoroutine(DashRoutine());
        }

        IEnumerator DashRoutine()
        {
            _dashing = true;
            var dir = _input != null && _input.Move.sqrMagnitude > 0.01f ? _input.Move.normalized : Facing;
            SetVelocity(dir * (stepDistance / stepDuration));
            yield return new WaitForSeconds(stepDuration);
            SetVelocity(Vector2.zero);
            _dashing = false;
        }
    }
}
