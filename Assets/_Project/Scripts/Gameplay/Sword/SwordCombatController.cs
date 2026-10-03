using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>One judged note, for the HUD and effects.</summary>
    public readonly struct NoteResult
    {
        public readonly NoteButton Button;
        public readonly JudgeResult Judge;
        public readonly ChainStep Step;
        public readonly SwordMove Move;
        public readonly SwordSequence Sequence;   // null when the note isn't part of a sequence

        public NoteResult(NoteButton button, JudgeResult judge, ChainStep step, SwordMove move, SwordSequence sequence)
        {
            Button = button; Judge = judge; Step = step; Move = move; Sequence = sequence;
        }

        public bool OnBeat => !Step.Broke;
        public Judgement Effective => Step.Broke ? Judgement.OffBeat : Judge.Judgement;
    }

    /// <summary>The blade reached its hit key.</summary>
    public readonly struct ImpactInfo
    {
        public readonly NoteResult Note;
        public readonly float Damage;          // 0 for single notes and off-beat notes
        public readonly int TargetsHit;
        public readonly Vector3 Point;
        public readonly bool CompletedSequence;

        public ImpactInfo(NoteResult note, float damage, int targetsHit, Vector3 point, bool completed)
        {
            Note = note; Damage = damage; TargetsHit = targetsHit; Point = point; CompletedSequence = completed;
        }

        public bool DealtDamage => Damage > 0f && TargetsHit > 0;
    }

    /// <summary>
    /// The M2 sword prototype's combat loop:
    /// press 1 or 2 → judge against the Conductor → the rhythm chain decides the move (a step of 1-2-1, 2-2,
    /// 1-1-1-2-1-1-1, or a lone cut) → the RhythmAnimator plays it with its hit on the beat → at the hit, damage is
    /// applied only if the note was on the beat and continued a chain (single notes do nothing) → finishing a
    /// sequence multiplies that last hit. Between notes the next move of the rhythm is anticipated so the swings flow.
    /// </summary>
    public class SwordCombatController : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] RhythmAnimator animator;
        [SerializeField] List<WeaponSet> weapons = new List<WeaponSet>();
        [SerializeField] TimingWindows windows = TimingWindows.Default;
        [SerializeField] LayerMask hittable = ~0;
        [Tooltip("Turn toward the nearest target within this distance when an attack starts.")]
        [SerializeField] float autoFaceRange = 3.5f;

        PlayerInputReader _input;
        PlayerMotor _motor;
        int _weaponIndex;
        bool _lightToggle;
        readonly RhythmChain _chain = new RhythmChain();
        readonly Dictionary<int, NoteResult> _pending = new Dictionary<int, NoteResult>();
        readonly Collider[] _hits = new Collider[16];

        public GrooveMeter Groove { get; } = new GrooveMeter();
        public ScoreKeeper Score { get; } = new ScoreKeeper();
        public RhythmChain Chain => _chain;
        public Conductor Conductor => conductor;
        public RhythmAnimator Animator => animator;
        public WeaponSet Weapon => weapons.Count > 0 ? weapons[_weaponIndex] : null;

        public event Action<NoteResult> NoteJudged;
        public event Action<ImpactInfo> Impacted;
        public event Action<SwordSequence> SequenceCompleted;
        public event Action<WeaponSet> WeaponChanged;

        void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<PlayerMotor>();
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (animator == null) animator = GetComponentInChildren<RhythmAnimator>();
        }

        void Start() => SelectWeapon(0);

        void OnEnable()
        {
            if (animator != null) animator.Impact += OnImpact;
            if (conductor != null) conductor.SongStarted += OnSongStarted;
        }

        void OnDisable()
        {
            if (animator != null) animator.Impact -= OnImpact;
            if (conductor != null) conductor.SongStarted -= OnSongStarted;
        }

        void OnSongStarted(SongData s)
        {
            _chain.Reset();
            _pending.Clear();
        }

        public void SelectWeapon(int index)
        {
            if (weapons.Count == 0) return;
            _weaponIndex = ((index % weapons.Count) + weapons.Count) % weapons.Count;
            var w = Weapon;
            var patterns = new List<SequencePattern>();
            foreach (var s in w.sequences) if (s != null && s.IsValid) patterns.Add(s.ToPattern());
            _chain.SetSequences(patterns);
            _pending.Clear();
            if (animator != null) animator.SetWeapon(w);
            WeaponChanged?.Invoke(w);
        }

        void Update()
        {
            if (_input == null || conductor == null || !conductor.IsPlaying) return;
            Groove.Tick(Time.deltaTime);
            if (_motor != null && animator != null) _motor.Attacking = animator.IsAttacking;
            if (_input.SwapWeaponPressed) SelectWeapon(_weaponIndex + 1);
            if (_input.OffsetNudge != 0 && animator != null) animator.AnimationOffsetMs += 10f * _input.OffsetNudge;
            if (_input.StrikePressed) Press(NoteButton.One, conductor.InputSongTime);
            if (_input.SmashPressed) Press(NoteButton.Two, conductor.InputSongTime);
        }

        /// <summary>A note at a given (calibrated) song time. Public for tests and tools.</summary>
        public void Press(NoteButton button, double pressTime)
        {
            var w = Weapon;
            if (w == null || animator == null) return;
            var map = conductor.Map;
            var judge = BeatJudge.Judge(map, pressTime, windows);
            var step = _chain.Register(judge.Beat.Index, button, judge.IsOnBeat);

            SwordSequence seq = step.Sequence >= 0 && step.Sequence < _chain.Sequences.Count
                ? _chain.Sequences[step.Sequence].Source as SwordSequence : null;
            SwordMove move = seq != null && step.Step < seq.moves.Count ? seq.moves[step.Step] : LoneMove(button);
            var note = new NoteResult(button, judge, step, move, seq);

            Groove.Add(note.Effective);
            Score.AddJudgement(note.Effective, Groove.ScoreMultiplier);

            FaceNearestTarget();
            double spb = judge.Beat.SecondsPerBeat;
            int id = animator.Play(move, judge.Beat.Time, spb, note.OnBeat);
            if (id != 0) _pending[id] = note;

            // Wind up the rhythm's next move so it lands on the next beat without a seam.
            if (note.OnBeat && _chain.Predict(out int ps, out int pstep, out _))
            {
                var nextSeq = _chain.Sequences[ps].Source as SwordSequence;
                if (nextSeq != null && pstep < nextSeq.moves.Count)
                    animator.Anticipate(nextSeq.moves[pstep], map.GetBeat(judge.Beat.Index + 1).Time, spb);
            }
            else animator.ClearAnticipation();

            NoteJudged?.Invoke(note);
        }

        SwordMove LoneMove(NoteButton button)
        {
            var w = Weapon;
            if (button == NoteButton.Two) return w.heavy;
            _lightToggle = !_lightToggle;
            return _lightToggle || w.lightB == null ? w.lightA : w.lightB;
        }

        void OnImpact(int id)
        {
            if (!_pending.TryGetValue(id, out var note)) return;
            _pending.Remove(id);
            var move = note.Move;
            var facing = _motor != null ? _motor.Facing : Vector2.up;
            Vector3 center = transform.position + new Vector3(facing.x, 0f, facing.y) * move.reach + Vector3.up * 1.05f;
            bool completed = note.Step.Completed && note.Sequence != null;

            // GDD M2: damage only from rhythm — on the beat, and not the first note of a chain.
            float damage = 0f;
            if (note.OnBeat && note.Step.DealsDamage)
            {
                damage = move.damage * Weapon.damageScale * BeatJudge.DamageMultiplier(note.Judge.Judgement) * Groove.DamageMultiplier;
                if (completed) damage *= note.Sequence.finisherMultiplier;
            }

            int hit = 0;
            Vector3 point = center;
            int count = Physics.OverlapSphereNonAlloc(center, move.radius, _hits, hittable, QueryTriggerInteraction.Collide);
            var done = new HashSet<IBeatDamageable>();
            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (c.transform.IsChildOf(transform)) continue;
                var target = c.GetComponentInParent<IBeatDamageable>();
                if (target == null || !done.Add(target)) continue;
                point = c.ClosestPoint(center);
                target.TakeHit(new HitData(damage, facing, completed ? "finisher" : move.effect, completed, point));
                hit++;
            }
            if (damage > 0f && hit > 0)
            {
                if (completed) Score.AddPhrase(note.Sequence.notes.Count, Groove.ScoreMultiplier);
            }
            Impacted?.Invoke(new ImpactInfo(note, damage, hit, point, completed));
            if (completed) SequenceCompleted?.Invoke(note.Sequence);
        }

        void FaceNearestTarget()
        {
            if (_motor == null) return;
            int count = Physics.OverlapSphereNonAlloc(transform.position, autoFaceRange, _hits, hittable, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            Vector3? at = null;
            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (c.transform.IsChildOf(transform) || c.GetComponentInParent<IBeatDamageable>() == null) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; at = c.transform.position; }
            }
            if (at.HasValue) _motor.FaceToward(at.Value);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var f = _motor != null ? _motor.Facing : Vector2.up;
            Gizmos.DrawWireSphere(transform.position + new Vector3(f.x, 0, f.y) * 1.25f + Vector3.up * 1.05f, 0.9f);
        }
    }
}
