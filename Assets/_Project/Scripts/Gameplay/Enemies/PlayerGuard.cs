using System;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The player's guard (vertical slice 2). Pressing guard raises the blade; enemies judge the latest press against
    /// their hit's own time: Perfect or Great parries and staggers them, Good blocks, anything else lets the hit land.
    /// One press answers one hit.
    /// </summary>
    public class PlayerGuard : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] RhythmAnimator animator;
        [SerializeField] TimingWindows windows = TimingWindows.Default;

        PlayerInputReader _input;
        ICombatBody _body;
        SwordCombatController _combat;

        /// <summary>Calibrated song time of the latest unanswered guard press (NaN = none).</summary>
        public double LastPressTime { get; private set; } = double.NaN;
        public TimingWindows Windows => windows;

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
            if (_input == null || conductor == null || !conductor.IsPlaying || !_input.GuardPressed) return;
            if (_body != null && !_body.CanAttack) return;
            LastPressTime = conductor.InputSongTime;
            var w = _combat != null ? _combat.Weapon : animator != null ? animator.Weapon : null;
            if (animator != null && w != null && w.guard != null) animator.PlayNow(w.guard, conductor.CurrentBeat.SecondsPerBeat);
            Raised?.Invoke();
        }

        /// <summary>Called by an enemy once a hit's parry window has closed.</summary>
        public DefenseResult Defend(bool inRange, double hitTime, Vector3 from)
        {
            var r = ParryJudge.Resolve(inRange, LastPressTime, hitTime, windows);
            if (r == DefenseResult.Parried || r == DefenseResult.Blocked) LastPressTime = double.NaN;
            if (r != DefenseResult.OutOfRange) Defended?.Invoke(r, from);
            return r;
        }
    }
}
