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

    /// <summary>Instruments (GDD): weapons change how the verbs behave.</summary>
    public enum WeaponType
    {
        Blade,      // balanced
        DrumHammer, // Smash hits twice: again half a beat later
        LuteBow,    // Strike fires a projectile (reaches the Piccolo Bat; enables ranged Phrases)
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
        [SerializeField] int parryPoints = 400;

        [Header("Weapon")]
        [SerializeField] WeaponType weapon = WeaponType.Blade;
        [Tooltip("Lute Bow shot model (flies along the ground at hand height).")]
        [SerializeField] GameObject projectilePrefab;
        [SerializeField] float projectileSpeed = 12f;

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
        double _lastGuardPress = double.NegativeInfinity;
        readonly Collider[] _hits = new Collider[32];
        const float HitHeight = 0.8f; // attacks are checked around the player's chest
        readonly List<(double time, float damage, string effect)> _echoes = new List<(double, float, string)>();

        public ComboEngine Combo { get; } = new ComboEngine();
        public GrooveMeter Groove { get; } = new GrooveMeter();
        public StaminaMeter Stamina { get; } = new StaminaMeter();
        public ScoreKeeper Score { get; } = new ScoreKeeper();
        public Conductor Conductor => conductor;
        public bool FinisherQueued => _finisherQueued;
        public ActionLock CurrentLock => _lock;
        public WeaponType Weapon => weapon;

        // Run stats for the arena rank (GDD: accuracy and Groove peak).
        public int TotalActions { get; private set; }
        public int OnBeatActions { get; private set; }
        public float PeakGroove { get; private set; }
        public double Accuracy => TotalActions == 0 ? 0 : (double)OnBeatActions / TotalActions;

        public void SetWeapon(WeaponType w) => weapon = w;

        /// <summary>Replace the equipped Phrases (loadout from the save).</summary>
        public void SetPhrases(IEnumerable<PhraseData> phrases)
        {
            equippedPhrases = new List<PhraseData>();
            var patterns = new List<PhrasePattern>();
            foreach (var p in phrases)
            {
                if (p == null || !p.IsValid) continue;
                equippedPhrases.Add(p);
                patterns.Add(PhrasePattern.From(p));
            }
            Combo.SetPhrases(patterns);
            Combo.Reset();
        }

        public IReadOnlyList<PhraseData> EquippedPhrases => equippedPhrases;

        /// <summary>Easy mode and settings: scale every timing window.</summary>
        public void SetTimingScale(float scale) => windows.scale = Mathf.Max(0.1f, scale);
        public TimingWindows Windows => windows;
        /// <summary>Calibrated song time of the last Guard press, on-beat or not. Enemies judge parries against it.</summary>
        public double LastGuardPressTime => _lastGuardPress;

        public event Action<BeatActionResult> ActionJudged;
        public event Action<PhrasePattern> PhraseTriggered;
        public event Action<BeatInfo> FinisherReady;
        public event Action<float> FinisherFired;
        /// <summary>An enemy attack was parried (enemy name).</summary>
        public event Action<string> Parried;
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
            if (conductor == null || !conductor.IsPlaying || conductor.IsPaused || _input == null) return;
            ProcessEchoes();
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
            TotalActions++;
            if (onBeat) OnBeatActions++;
            PeakGroove = Mathf.Max(PeakGroove, Groove.Value);
            int points = Score.AddJudgement(effective, Groove.ScoreMultiplier);
            if (phrase.HasValue) points += Score.AddPhrase(phrase.Value.Pattern.Length, Groove.ScoreMultiplier);

            double nextBeatTime = map.GetBeat(judge.Beat.Index + 1).Time;
            _lock = AttackTiming.Compute(FramesFor(action), effective, winded, startTime, nextBeatTime, windows);

            float damage = 0f;
            switch (action)
            {
                case BeatAction.Strike:
                case BeatAction.Smash:
                    damage = ComputeDamage(action, effective, judge.Beat, phrase) * WeaponScale(action);
                    var effect = phrase?.Source is PhraseData pd ? pd.effectId : null;
                    if (weapon == WeaponType.LuteBow && action == BeatAction.Strike && RadiusScale(effect) <= 1f)
                    {
                        FireProjectile(damage, effect);
                    }
                    else
                    {
                        DealDamage(damage, effect, false, RadiusScale(effect));
                        if (weapon == WeaponType.DrumHammer && action == BeatAction.Smash)
                            _echoes.Add((judge.Beat.Time + 0.5 * judge.Beat.SecondsPerBeat, damage * 0.55f, effect));
                    }
                    break;
                case BeatAction.Step:
                    if (_motor != null) _motor.Dash(onBeat);
                    break;
                case BeatAction.Guard:
                    // Enemies compare this press with their hit time (ParryJudge), so off-beat attacks can be parried too.
                    _lastGuardPress = pressTime;
                    break;
            }

            if (phrase.HasValue) PhraseTriggered?.Invoke(phrase.Value);
            ActionJudged?.Invoke(new BeatActionResult(action, judge, outcome, phrase, damage, winded, _lock, points));
        }

        /// <summary>Called by an enemy when the player parried it: rewards like a Perfect hit plus a bonus.</summary>
        public void RegisterParry(string enemyName)
        {
            Groove.Add(Judgement.Perfect);
            Score.AddBonus(parryPoints, Groove.ScoreMultiplier);
            Parried?.Invoke(enemyName);
        }

        /// <summary>Called when the player takes a hit.</summary>
        public void RegisterHurt() => Groove.Hurt();

        float WeaponScale(BeatAction a) => weapon switch
        {
            WeaponType.DrumHammer => a == BeatAction.Smash ? 1.2f : 0.9f,
            WeaponType.LuteBow => a == BeatAction.Strike ? 0.85f : 1f,
            _ => 1f,
        };

        /// <summary>Drum Hammer: the second hit of a Smash lands half a beat later, on the music's clock.</summary>
        void ProcessEchoes()
        {
            if (_echoes.Count == 0) return;
            double now = conductor.SongTime;
            for (int i = _echoes.Count - 1; i >= 0; i--)
            {
                if (now < _echoes[i].time) continue;
                var e = _echoes[i];
                _echoes.RemoveAt(i);
                DealDamage(e.damage, e.effect, false, RadiusScale(e.effect));
            }
        }

        void FireProjectile(float damage, string effect)
        {
            var facing = _motor != null ? _motor.Facing : Vector2.up;
            var pos = Ground.ToWorld(Ground.ToPlane(transform.position) + facing * 0.6f, HitHeight);
            var go = projectilePrefab != null ? Instantiate(projectilePrefab, pos, Ground.Facing(facing)) : new GameObject("Note Arrow");
            go.transform.position = pos;
            var p = go.GetComponent<Projectile>();
            if (p == null) p = go.AddComponent<Projectile>();
            p.Launch(facing * projectileSpeed, new HitData(damage, facing, effect, false), gameObject, hittable);
        }

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
            DealDamage(dmg, "finisher", true, 2.5f);
            FinisherFired?.Invoke(dmg);
        }

        /// <summary>Area phrases hit wider: Backbeat shockwave, Waltz spin.</summary>
        static float RadiusScale(string effect) => effect switch
        {
            "shockwave" => 2f,
            "spin" => 2.5f,
            _ => 1f,
        };

        void DealDamage(float damage, string effectId, bool finisher, float radiusScale)
        {
            var facing = _motor != null ? _motor.Facing : Vector2.up;
            // Wide hits are centred on the player; normal hits in front of them.
            var plane = Ground.ToPlane(transform.position);
            var center = Ground.ToWorld(radiusScale > 1f ? plane : plane + facing * attackRange, HitHeight);
            float radius = attackRadius * radiusScale;
            int count = Physics.OverlapSphereNonAlloc(center, radius, _hits, hittable, QueryTriggerInteraction.Collide);
            var done = new HashSet<IBeatDamageable>();
            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<IBeatDamageable>();
                if (target != null && done.Add(target))
                    target.TakeHit(new HitData(damage, facing, effectId, finisher));
            }
        }

        void OnDrawGizmosSelected()
        {
            var facing = Application.isPlaying && _motor != null ? _motor.Facing : Vector2.up;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Ground.ToWorld(Ground.ToPlane(transform.position) + facing * attackRange, HitHeight), attackRadius);
        }
    }
}
