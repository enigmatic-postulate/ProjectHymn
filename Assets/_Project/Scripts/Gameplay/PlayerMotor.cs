using System.Collections;
using UnityEngine;


namespace ProjectHymn.Gameplay
{
    /// <summary>Free real-time movement (never beat-locked) plus the Step dash.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float stepDistance = 2.5f;
        [SerializeField] float stepDuration = 0.12f;
        [SerializeField] float rollDistance = 1.2f;
        [SerializeField] float rollDuration = 0.3f;

        Rigidbody2D _body;
        PlayerInputReader _input;
        bool _dashing;

        public Vector2 Facing { get; private set; } = Vector2.right;
        /// <summary>Armor can slow or speed the player (1 = normal).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public bool Invulnerable { get; private set; }

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _input = GetComponent<PlayerInputReader>();
        }

        void FixedUpdate()
        {
            if (_dashing || _input == null) return;
            var move = _input.Move;
            if (move.sqrMagnitude > 0.01f) Facing = move.normalized;
            SetVelocity(move * moveSpeed * SpeedMultiplier);
        }

        void SetVelocity(Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = v;
#else
            _body.velocity = v;
#endif
        }

        /// <summary>On-beat Step: fast, long, invulnerable. Off-beat: a slow roll with no i-frames.</summary>
        public void Dash(bool onBeat)
        {
            if (_dashing) return;
            StartCoroutine(DashRoutine(onBeat));
        }

        IEnumerator DashRoutine(bool onBeat)
        {
            _dashing = true;
            Invulnerable = onBeat;
            float dist = onBeat ? stepDistance : rollDistance;
            float dur = onBeat ? stepDuration : rollDuration;
            var dir = _input != null && _input.Move.sqrMagnitude > 0.01f ? _input.Move.normalized : Facing;
            SetVelocity(dir * (dist / dur));
            yield return new WaitForSeconds(dur);
            SetVelocity(Vector2.zero);
            Invulnerable = false;
            _dashing = false;
        }
    }
}
