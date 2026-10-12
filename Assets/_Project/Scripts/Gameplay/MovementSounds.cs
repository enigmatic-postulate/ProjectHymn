using ProjectHymn.Core;
using ProjectHymn.Movement;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The player's movement sounds (VS-4): a footstep on every beat while running (the run is animated to land its
    /// footfalls on the beat), a jump and a brighter double-jump, and a landing (heavier from a high fall).
    /// </summary>
    public class MovementSounds : MonoBehaviour
    {
        PlatformerController _body;
        Conductor _conductor;

        void Awake() => _body = GetComponent<PlatformerController>();

        void OnEnable()
        {
            if (_body == null) _body = GetComponent<PlatformerController>();
            if (_body != null) { _body.Jumped += OnJump; _body.Landed += OnLand; }
            _conductor = FindAnyObjectByType<Conductor>();
            if (_conductor != null) _conductor.BeatStarted += OnBeat;
        }

        void OnDisable()
        {
            if (_body != null) { _body.Jumped -= OnJump; _body.Landed -= OnLand; }
            if (_conductor != null) _conductor.BeatStarted -= OnBeat;
        }

        void OnBeat(BeatInfo b)
        {
            if (_body == null || !_body.isActiveAndEnabled || !_body.Grounded || _body.State != LocoState.Run) return;
            Sfx.Play(Sfx.Step, 0.55f, 0.06f);
        }

        void OnJump(JumpKind k) => Sfx.Play(k == JumpKind.Air ? Sfx.AirJump : Sfx.Jump, 0.7f, 0.03f);

        void OnLand(float speed)
        {
            if (speed < 3f) return;
            Sfx.Play(speed > 11f ? Sfx.LandHard : Sfx.Land, Mathf.Clamp01(speed / 12f) * 0.8f + 0.2f, 0.05f);
        }
    }
}
