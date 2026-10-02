using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Everything the HUD and feedback systems need about one judged action.</summary>
    public readonly struct BeatActionResult
    {
        public readonly BeatAction Action;
        public readonly JudgeResult Judge;
        public readonly RegisterOutcome Outcome;
        public readonly PhrasePattern? Phrase;
        public readonly float Damage;

        public BeatActionResult(BeatAction action, JudgeResult judge, RegisterOutcome outcome, PhrasePattern? phrase, float damage)
        {
            Action = action; Judge = judge; Outcome = outcome; Phrase = phrase; Damage = damage;
        }

        /// <summary>The judgement after anti-mash rules (a second input on one beat counts as a miss).</summary>
        public Judgement Effective => Outcome == RegisterOutcome.Accepted ? Judge.Judgement : Judgement.Miss;
    }

    /// <summary>
    /// The rhythm combat loop: input → judge against the Conductor → combo engine → groove → damage.
    /// </summary>
    public class BeatCombatController : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] List<PhraseData> equippedPhrases = new List<PhraseData>();
        [SerializeField] TimingWindows windows = TimingWindows.Default;

        [Header("Damage")]
        [SerializeField] float strikeDamage = 10f;
        [SerializeField] float smashDamage = 18f;
        [SerializeField] float downbeatBonus = 1.25f;
        [SerializeField] float finisherDamage = 60f;

        [Header("Hit area")]
        [SerializeField] float attackRange = 1.1f;
        [SerializeField] float attackRadius = 0.8f;
        [SerializeField] LayerMask hittable = ~0;

        PlayerInputReader _input;
        PlayerMotor _motor;
        bool _finisherQueued;
        readonly List<Collider2D> _hits = new List<Collider2D>();

        public ComboEngine Combo { get; } = new ComboEngine();
        public GrooveMeter Groove { get; } = new GrooveMeter();
        public Conductor Conductor => conductor;
        public bool FinisherQueued => _finisherQueued;

        public event Action<BeatActionResult> ActionJudged;
        public event Action<PhrasePattern> PhraseTriggered;
        public event Action<BeatInfo> FinisherReady;
        public event Action<float> FinisherFired;

        void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<PlayerMotor>();
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();

            var patterns = new List<PhrasePattern>();
            foreach (var p in equippedPhrases) if (p != null && p.IsValid) patterns.Add(PhrasePattern.From(p));
            Combo.SetPhrases(patterns);
            Combo.BarCompleted += b => { _finisherQueued = true; FinisherReady?.Invoke(b); };
        }

        void OnEnable() { if (conductor != null) conductor.MeasureStarted += OnMeasureStarted; }
        void OnDisable() { if (conductor != null) conductor.MeasureStarted -= OnMeasureStarted; }

        void Update()
        {
            if (conductor == null || !conductor.IsPlaying || _input == null) return;
            Groove.Tick(Time.deltaTime);

            if (_input.StrikePressed) Act(BeatAction.Strike);
            if (_input.SmashPressed) Act(BeatAction.Smash);
            if (_input.StepPressed) Act(BeatAction.Step);
            if (_input.GuardPressed) Act(BeatAction.Guard);
        }

        public void Act(BeatAction action)
        {
            var judge = BeatJudge.Judge(conductor.Map, conductor.InputSongTime, windows);
            PhrasePattern? phrase = null;
            RegisterOutcome outcome;

            if (judge.IsHit) outcome = Combo.Register(judge.Beat, action, out phrase);
            else { Combo.RegisterMiss(judge.Beat); outcome = RegisterOutcome.Missed; }

            var effective = outcome == RegisterOutcome.Accepted ? judge.Judgement : Judgement.Miss;
            Groove.Add(effective);

            float damage = 0f;
            switch (action)
            {
                case BeatAction.Strike:
                case BeatAction.Smash:
                    damage = ComputeDamage(action, effective, judge.Beat, phrase);
                    DealDamage(damage, phrase?.Source is PhraseData pd ? pd.effectId : null, false);
                    break;
                case BeatAction.Step:
                    if (_motor != null) _motor.Dash(effective != Judgement.Miss);
                    break;
                case BeatAction.Guard:
                    // Parry window = this beat. Enemy attacks check IsGuarding(beat) when they land.
                    _guardBeat = effective != Judgement.Miss ? judge.Beat.Index : long.MinValue;
                    break;
            }

            if (phrase.HasValue) PhraseTriggered?.Invoke(phrase.Value);
            ActionJudged?.Invoke(new BeatActionResult(action, judge, outcome, phrase, damage));
        }

        long _guardBeat = long.MinValue;

        /// <summary>True if the player pressed Guard on time for the given beat. Enemies call this when their hit lands.</summary>
        public bool IsGuarding(long beatIndex) => _guardBeat == beatIndex && _input != null && _input.GuardHeld;

        float ComputeDamage(BeatAction action, Judgement j, BeatInfo beat, PhrasePattern? phrase)
        {
            float dmg = action == BeatAction.Smash ? smashDamage : strikeDamage;
            dmg *= BeatJudge.DamageMultiplier(j);
            if (j == Judgement.Miss) return dmg; // weak, no bonuses
            dmg *= Groove.Multiplier;
            if (beat.IsDownbeat) dmg *= downbeatBonus;
            if (phrase?.Source is PhraseData pd) dmg *= pd.damageMultiplier;
            return dmg;
        }

        void OnMeasureStarted(BeatInfo beat)
        {
            if (!_finisherQueued) return;
            _finisherQueued = false;
            // Bigger meters need more inputs to fill, so they pay more.
            float dmg = finisherDamage * (beat.BeatsPerMeasure / 4f) * Groove.Multiplier;
            DealDamage(dmg, "finisher", true);
            FinisherFired?.Invoke(dmg);
        }

        void DealDamage(float damage, string effectId, bool finisher)
        {
            var facing = _motor != null ? _motor.Facing : Vector2.right;
            Vector2 center = (Vector2)transform.position + facing * attackRange;
            float radius = finisher ? attackRadius * 2.5f : attackRadius;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = hittable, useTriggers = true };
            int count = Physics2D.OverlapCircle(center, radius, filter, _hits);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].attachedRigidbody != null && _hits[i].attachedRigidbody.gameObject == gameObject) continue;
                if (_hits[i].TryGetComponent<IBeatDamageable>(out var target))
                    target.TakeHit(new HitData(damage, facing, effectId, finisher));
            }
        }

        void OnDrawGizmosSelected()
        {
            var facing = Application.isPlaying && _motor != null ? _motor.Facing : Vector2.right;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere((Vector2)transform.position + facing * attackRange, attackRadius);
        }
    }
}
