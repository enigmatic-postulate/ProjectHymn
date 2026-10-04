using System;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The player's defence (GDD update 3). Guard raises the blade; enemies judge the latest press against their hit's own
    /// time: only Perfect timing parries and staggers them, Great or Good blocks, anything else lets the hit land. One press
    /// answers one hit. Dodge (Shift / RT) rolls with invulnerable frames; a hit those frames cover is dodged. Presses are
    /// timed by their input event, not the frame they were read in.
    /// </summary>
    public class PlayerGuard : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] RhythmAnimator animator;
        [SerializeField] TimingWindows windows = TimingWindows.Default;
        [Tooltip("Seconds of invulnerability a dodge gives (the Crusader's are longer).")]
        [SerializeField] float dodgeInvulnerable = 0.25f;
        [Tooltip("Extra block window either side, in seconds (a class perk: the Fighter's wider guard).")]
        [SerializeField] float blockBonus = 0f;

        PlayerInputReader _input;
        ICombatBody _body;
        SwordCombatController _combat;

        /// <summary>Calibrated song time of the latest unanswered guard press (NaN = none).</summary>
        public double LastPressTime { get; private set; } = double.NaN;
        public TimingWindows Windows => windows;
        /// <summary>Calibrated song time the latest dodge began (NaN = none).</summary>
        public double LastDodgeTime { get; private set; } = double.NaN;
        public float DodgeInvulnerable { get => dodgeInvulnerable; set => dodgeInvulnerable = value; }
        public float BlockBonus { get => blockBonus; set => blockBonus = value; }
        public event Action Dodged;

        /// <summary>An enemy hit was resolved against the guard. Args: result, where the hit came from.</summary>
        public event Action<DefenseResult, Vector3> Defended;
        public event Action Raised;

        void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _body = GetComponent<ICombatBody>();
            _combat = GetComponent<SwordCombatController>();
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (animator == null) animator = GetComponentInChildren<RhythmAnimator>();
        }

        void Update()
        {
            if (_input == null) return;
            if (_input.DodgePressed) TryDodge();
            if (conductor == null || !conductor.IsPlaying || !_input.GuardPressed) return;
            if (_body != null && !_body.CanAttack) return;
            LastPressTime = conductor.InputSongTimeAt(_input.GuardRealtime);
            var w = _combat != null ? _combat.Weapon : animator != null ? animator.Weapon : null;
            if (animator != null && w != null && w.guard != null) animator.PlayNow(w.guard, conductor.CurrentBeat.SecondsPerBeat);
            Raised?.Invoke();
        }

        void TryDodge()
        {
            var platformer = GetComponent<PlatformerController>();
            float dir = Mathf.Abs(_input.Move.x) > 0.2f ? Mathf.Sign(_input.Move.x) : 0f;
            if (platformer == null || !platformer.TryDodge(dir)) return;
            if (animator != null) animator.CancelAttack();
            LastDodgeTime = conductor != null && conductor.IsPlaying ? conductor.InputSongTimeAt(_input.DodgeRealtime) : double.NaN;
            Dodged?.Invoke();
        }

        /// <summary>Called by an enemy once a hit's parry window has closed.</summary>
        public DefenseResult Defend(bool inRange, double hitTime, Vector3 from)
        {
            var r = ParryJudge.Resolve(inRange, LastPressTime, hitTime, Windows, LastDodgeTime, dodgeInvulnerable, blockBonus);
            if (r == DefenseResult.Parried || r == DefenseResult.Blocked) LastPressTime = double.NaN;
            if (r != DefenseResult.OutOfRange) Defended?.Invoke(r, from);
            return r;
        }
    }
}
