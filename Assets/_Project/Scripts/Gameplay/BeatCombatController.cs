using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Everything feedback and HUD need about one judged action.</summary>
    public readonly struct BeatActionResult
    {
        public readonly BeatAction Action;
        public readonly JudgeResult Judge;
        public readonly RegisterOutcome Outcome;
        public readonly PhrasePattern? Phrase;
        public readonly float Damage;
        public readonly bool Winded;
        public readonly ActionLock Lock;
        public readonly int Points;

        public BeatActionResult(BeatAction action, JudgeResult judge, RegisterOutcome outcome, PhrasePattern? phrase,
                                float damage, bool winded, ActionLock actionLock, int points)
        {
            Action = action; Judge = judge; Outcome = outcome; Phrase = phrase; Damage = damage;
            Winded = winded; Lock = actionLock; Points = points;
        }

        /// <summary>The judgement after anti-mash rules (a second input on one beat counts as off-beat).</summary>
        public Judgement Effective => Outcome == RegisterOutcome.Accepted ? Judge.Judgement : Judgement.OffBeat;
        public bool IsAttack => Action == BeatAction.Strike || Action == BeatAction.Smash;
    }

    /// <summary>
    /// The rhythm combat loop (GDD M1 rules):
    /// press → judge against the Conductor → on-beat: combo + groove, free; off-beat: costs stamina,
    /// breaks the chain → recovery lock whose length and cancel window depend on the judgement → damage.
    /// Presses during recovery are buffered briefly and run as soon as the lock allows, still judged
    /// at the moment they were pressed.
    /// </summary>
    public class BeatCombatController : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] List<PhraseData> equippedPhrases = new List<PhraseData>();
        [SerializeField] TimingWindows windows = TimingWindows.Default;

        [Header("Frame data (seconds) — animations will be authored to match")]
        [SerializeField] FrameData strikeFrames = new FrameData(0.05f, 0.07f, 0.30f);
        [SerializeField] FrameData smashFrames = new FrameData(0.08f, 0.08f, 0.42f);
        [SerializeField] FrameData stepFrames = new FrameData(0.02f, 0.12f, 0.18f);
        [SerializeField] FrameData guardFrames = new FrameData(0.03f, 0.20f, 0.10f);
        [Tooltip("A press this close before the lock ends is remembered and runs when it ends.")]
        [SerializeField, Range(0f, 0.3f)] float inputBuffer = 0.15f;

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
        ActionLock _lock = ActionLock.None;
        bool _hasBuffered;
        BeatAction _bufferedAction;
        double _bufferedPressTime;
        long _guardBeat = long.MinValue;
        readonly List<Collider2D> _hits = new List<Collider2D>();

        public ComboEngine Combo { get; } = new ComboEngine();
        public GrooveMeter Groove { get; } = new GrooveMeter();
        public StaminaMeter Stamina { get; } = new StaminaMeter();
        public ScoreKeeper Score { get; } = new ScoreKeeper();
        public Conductor Conductor => conductor;
        public bool FinisherQueued => _finisherQueued;
        public ActionLock CurrentLock => _lock;

        public event Action<BeatActionResult> ActionJudged;
        public event Action<PhrasePattern> PhraseTriggered;
        public event Action<BeatInfo> FinisherReady;
        public event Action<float> FinisherFired;
        /// <summary>A press arrived too early in a recovery to be buffered and was dropped.</summary>
        public event Action<BeatAction> InputDropped;

        void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<PlayerMotor>();
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();

            var patterns = new List<PhrasePattern>();
            foreach (var p in equippedPhrases) if (p != null && p.IsValid) patterns.Add(PhrasePattern.From(p));
            if (patterns.Count == 0) Debug.LogWarning("[BeatCombatController] No Phrases equipped; combos won't trigger.", this);
            Combo.SetPhrases(patterns);
            Combo.BarCompleted += b => { _finisherQueued = true; FinisherReady?.Invoke(b); };
        }

        void OnEnable() { if (conductor != null) conductor.MeasureStarted += OnMeasureStarted; }
        void OnDisable() { if (conductor != null) conductor.MeasureStarted -= OnMeasureStarted; }

        void Update()
        {
            if (conductor == null || !conductor.IsPlaying || _input == null) return;
            Groove.Tick(Time.deltaTime);
            Stamina.Tick(Time.deltaTime);

            double now = conductor.InputSongTime;
            if (_input.StrikePressed) Press(BeatAction.Strike, now);
            if (_input.SmashPressed) Press(BeatAction.Smash, now);
            if (_input.StepPressed) Press(BeatAction.Step, now);
            if (_input.GuardPressed) Press(BeatAction.Guard, now);

            if (_hasBuffered && _lock.CanAct(now))
            {
                _hasBuffered = false;
                Execute(_bufferedAction, _bufferedPressTime, now);
            }
        }

        /// <summary>Entry point for a press at a given (calibrated) song time. Public for tests and AI.</summary>
        public void Press(BeatAction action, double pressTime)
        {
            if (_lock.CanAct(pressTime)) { Execute(action, pressTime, pressTime); return; }

            if (_lock.FreeAt - pressTime <= inputBuffer)
            {
                _hasBuffered = true; // latest press wins
                _bufferedAction = action;
                _bufferedPressTime = pressTime;
            }
            else InputDropped?.Invoke(action);
        }

        void Execute(BeatAction action, double pressTime, double startTime)
        {
            var map = conductor.Map;
            var judge = BeatJudge.Judge(map, pressTime, windows);
            PhrasePattern? phrase = null;
            RegisterOutcome outcome;

            if (judge.IsOnBeat) outcome = Combo.Register(judge.Beat, action, out phrase);
            else { Combo.RegisterOffBeat(judge.Beat); outcome = RegisterOutcome.OffBeat; }

            var effective = outcome == RegisterOutcome.Accepted ? judge.Judgement : Judgement.OffBeat;
            bool onBeat = effective != Judgement.OffBeat;

            // On-beat is free; off-beat pays stamina, and comes out Winded when there isn't enough.
            bool winded = false;
            if (onBeat) Stamina.NotifyAction();
            else winded = !Stamina.Spend(action);

            Groove.Add(effective);
            int points = Score.AddJudgement(effective, Groove.ScoreMultiplier);
            if (phrase.HasValue) points += Score.AddPhrase(phrase.Value.Pattern.Length, Groove.ScoreMultiplier);

            double nextBeatTime = map.GetBeat(judge.Beat.Index + 1).Time;
            _lock = AttackTiming.Compute(FramesFor(action), effective, winded, startTime, nextBeatTime, windows);

            float damage = 0f;
            switch (action)
            {
                case BeatAction.Strike:
                case BeatAction.Smash:
                    damage = ComputeDamage(action, effective, judge.Beat, phrase);
                    DealDamage(damage, phrase?.Source is PhraseData pd ? pd.effectId : null, false);
                    break;
                case BeatAction.Step:
                    if (_motor != null) _motor.Dash(onBeat);
                    break;
                case BeatAction.Guard:
                    // Parry window = this beat. Enemy attacks check IsGuarding(beat) when they land.
                    _guardBeat = onBeat ? judge.Beat.Index : long.MinValue;
                    break;
            }

            if (phrase.HasValue) PhraseTriggered?.Invoke(phrase.Value);
            ActionJudged?.Invoke(new BeatActionResult(action, judge, outcome, phrase, damage, winded, _lock, points));
        }

        /// <summary>True if the player pressed Guard on time for the given beat. Enemies call this when their hit lands.</summary>
        public bool IsGuarding(long beatIndex) => _guardBeat == beatIndex && _input != null && _input.GuardHeld;

        FrameData FramesFor(BeatAction a) => a switch
        {
            BeatAction.Smash => smashFrames,
            BeatAction.Step => stepFrames,
            BeatAction.Guard => guardFrames,
            _ => strikeFrames,
        };

        float ComputeDamage(BeatAction action, Judgement j, BeatInfo beat, PhrasePattern? phrase)
        {
            float dmg = action == BeatAction.Smash ? smashDamage : strikeDamage;
            dmg *= BeatJudge.DamageMultiplier(j);
            if (j == Judgement.OffBeat) return dmg; // a normal action-game hit: no rhythm bonuses
            dmg *= Groove.DamageMultiplier;
            if (beat.IsDownbeat) dmg *= downbeatBonus;
            if (phrase?.Source is PhraseData pd) dmg *= pd.damageMultiplier;
            return dmg;
        }

        void OnMeasureStarted(BeatInfo beat)
        {
            if (!_finisherQueued) return;
            _finisherQueued = false;
            // Bigger meters need more inputs to fill, so they pay more.
            float dmg = finisherDamage * (beat.BeatsPerMeasure / 4f) * Groove.DamageMultiplier;
            Score.AddFinisher(beat.BeatsPerMeasure, Groove.ScoreMultiplier);
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
