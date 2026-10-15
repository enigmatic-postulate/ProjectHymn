using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Movement;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Base for the slice 2 enemies. Attacks come from <see cref="patterns"/> and are placed on the song's beat grid
    /// so each is telegraphed a full bar before its first hit lands on a downbeat: the body glows up, a 3-2-1
    /// count hangs over its head and the HUD beat track marks the beats. Every hit is judged against the player's
    /// guard when its window closes: a parry staggers the enemy (its remaining hits are cancelled and it takes
    /// extra damage for a couple of beats), a block shrugs it off, otherwise the player is hurt.
    /// Subclasses move and pose the body (<see cref="Tick"/>) and decide range and damage taken.
    /// </summary>
    public abstract class BeatEnemy : MonoBehaviour, IBeatDamageable
    {
        public static readonly List<BeatEnemy> All = new List<BeatEnemy>();

        [SerializeField] protected Conductor conductor;
        [SerializeField] protected string displayName = "Enemy";
        [SerializeField] protected float maxHp = 180f;
        [SerializeField] protected List<EnemyAttackPattern> patterns = new List<EnemyAttackPattern>();
        [Tooltip("The enemy notices the player within this distance (and this height difference).")]
        [SerializeField] protected float aggroRange = 7f;
        [SerializeField] protected float aggroHeight = 2.5f;
        [SerializeField] protected float leashRange = 14f;
        [Tooltip("Telegraph each attack for one bar of the current meter (3 beats in 3/4) instead of the pattern's lead.")]
        [SerializeField] protected bool oneBarCountIn = true;
        [Tooltip("The red galvanize aura flares this many beats before each hit (one beat for every enemy for now; varied per enemy later).")]
        [SerializeField] protected float galvanizeBeats = 1f;
        [Header("Stagger")]
        [SerializeField] protected int staggerBeats = 2;
        [SerializeField] protected float staggerDamageMultiplier = 1.5f;
        [Header("Sequence effects")]
        [Tooltip("Rising Verse: how high the enemy is thrown and for how many beats it's in the air (staggered).")]
        [SerializeField] protected float launchHeight = 1.3f;
        [SerializeField] protected float launchBeats = 1.5f;
        [Tooltip("Bulwark: how far the push shoves the enemy back (its next hit also comes a beat later).")]
        [SerializeField] protected float pushDistance = 1.2f;
        [Header("Floors (the brawl stage)")]
        [Tooltip("Jump up onto platforms and drop down after the player (needs a StageFloors in the scene).")]
        [SerializeField] protected bool changesFloors;
        [Tooltip("Beats the player must stay on another floor before the enemy follows, and how long a hop takes.")]
        [SerializeField] protected float hopDelayBeats = 1.5f;
        [SerializeField] protected float hopBeats = 1f;
        [Header("Look")]
        [SerializeField] protected Renderer[] flashRenderers = new Renderer[0];
        [SerializeField] protected Color warnColor = new Color(1f, 0.2f, 0.1f);
        [SerializeField] protected float barHeight = 2.3f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        protected Transform Player { get; private set; }
        protected PlatformerController Platformer { get; private set; }
        PlayerGuard _guard;
        PlayerHealth _health;
        DamagePopups _popups;

        protected PlannedAttack Plan { get; private set; }
        int _hitsDone;
        bool _hitStarted;
        bool _hitInRange;
        double _notBeforeBeat = double.NegativeInfinity;
        double _staggerUntilBeat = double.NegativeInfinity;
        double _launchFrom = double.NegativeInfinity, _launchUntil = double.NegativeInfinity;
        float _lift;          // launch height applied in LateUpdate, taken off again at the start of the next Update
        FloorMap.Floor _floor;
        double _cuedPlan = double.NaN;        // the plan whose wind-up was sounded (its first hit beat)
        bool _hopping;
        double _hopStart, _awaySince = double.NaN;
        Vector3 _hopFrom, _hopTo;
        FloorMap.Floor _hopTarget, _playerFloor;
        int _patternIndex;
        protected Vector3 Home { get; private set; }
        float _hp, _flash;
        MaterialPropertyBlock _mpb;
        Color[][] _baseColors, _baseEmission;
        TextMesh _count;
        SpriteRenderer _aura, _auraRing;
        Transform _barRoot, _barFill;

        public string DisplayName => displayName;
        public float Hp => _hp;
        public float MaxHp => maxHp;
        public bool IsDead { get; private set; }
        public bool Aggro { get; private set; }
        public double BeatNow { get; private set; }
        public bool IsStaggered => BeatNow < _staggerUntilBeat;

        /// <summary>
        /// The Practice Yard's training knight: its blows cost no hearts (the player sees "BLUNT" instead) and it can't be
        /// brought below 1 health.
        /// </summary>
        public bool Training { get; set; }
        /// <summary>Every attack can't be guarded, only dodged (the dodge lesson's violet blows).</summary>
        public bool ForceUnblockable { get; set; }

        public event Action<BeatEnemy> Died;
        /// <summary>Any enemy fell (stage directors count kills with it, including enemies that join later, as in the Arena).</summary>
        public static event Action<BeatEnemy> AnyDied;
        public event Action<BeatEnemy, DefenseResult> HitResolved;

        protected virtual void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            Home = transform.position;
            _hp = maxHp;
            _mpb = new MaterialPropertyBlock();
            _baseColors = new Color[flashRenderers.Length][];
            _baseEmission = new Color[flashRenderers.Length][];
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                var mats = flashRenderers[i] != null ? flashRenderers[i].sharedMaterials : new Material[0];
                _baseColors[i] = new Color[mats.Length];
                _baseEmission[i] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    _baseColors[i][m] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
                    _baseEmission[i][m] = mat != null && mat.IsKeywordEnabled("_EMISSION") && mat.HasProperty(EmissionId) ? mat.GetColor(EmissionId) : Color.black;
                }
            }
            BuildOverhead();
        }

        protected virtual void Start()
        {
            var pc = FindAnyObjectByType<PlatformerController>();
            if (pc != null)
            {
                Player = pc.transform;
                Platformer = pc;
                _guard = pc.GetComponent<PlayerGuard>();
                _health = pc.GetComponent<PlayerHealth>();
                if (_health != null) _health.Died += OnPlayerDied;
            }
            _popups = FindAnyObjectByType<DamagePopups>();
            if (changesFloors && StageFloors.Current != null)
            {
                var map = StageFloors.Current.Map;
                _floor = map.FloorAt(transform.position.x, transform.position.y);
                if (_floor.IsValid) { var (lo, hi) = map.Bounds(_floor); OnFloorChanged(lo, hi); }
            }
        }

        /// <summary>The enemy reached another floor: keep to its bounds (knights and dancers clamp their walk to them).</summary>
        protected virtual void OnFloorChanged(float minX, float maxX) { }

        /// <summary>Mid-hop between floors (no attacks while in the air).</summary>
        public bool Hopping => _hopping;

        // ---------------- Hopping between floors ----------------

        /// <summary>Follow the player to another floor once they have stayed there a moment: one jump up, or a drop.</summary>
        void ConsiderHop()
        {
            var floors = StageFloors.Current;
            if (floors == null || !_floor.IsValid || Platformer == null) return;
            var map = floors.Map;
            if (Platformer.Grounded) _playerFloor = map.FloorAt(Player.position.x, Player.position.y);
            if (!_playerFloor.IsValid || _playerFloor.Name == _floor.Name) { _awaySince = double.NaN; return; }
            if (double.IsNaN(_awaySince)) { _awaySince = BeatNow; return; }
            if (BeatNow - _awaySince < hopDelayBeats) return;
            var next = map.NextStep(_floor, _playerFloor);
            if (!next.IsValid) return;
            float x = map.LandingX(next, transform.position.x, Player.position.x, next.Name == _playerFloor.Name);
            _hopFrom = transform.position;
            _hopTo = new Vector3(x, next.Top, transform.position.z);
            _hopTarget = next;
            _hopStart = BeatNow;
            _hopping = true;
            CancelPlan();
        }

        void UpdateHop()
        {
            float t = Mathf.Clamp01((float)((BeatNow - _hopStart) / Mathf.Max(0.25f, hopBeats)));
            float x = Mathf.Lerp(_hopFrom.x, _hopTo.x, t * t * (3f - 2f * t));
            transform.position = new Vector3(x, FloorMap.HopHeight(_hopFrom.y, _hopTo.y, t), _hopFrom.z);
            if (t < 1f) return;
            _hopping = false;
            _floor = _hopTarget;
            _awaySince = double.NaN;
            Home = _hopTo;
            var (lo, hi) = StageFloors.Current != null ? StageFloors.Current.Map.Bounds(_floor) : (_hopTo.x, _hopTo.x);
            OnFloorChanged(lo, hi);
            _notBeforeBeat = Math.Max(_notBeforeBeat, BeatNow + 0.5);       // land, then count in the next attack
        }

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); EnemyThreats.Clear(this); }
        protected virtual void OnDestroy() { if (_health != null) _health.Died -= OnPlayerDied; }

        // ---------------- Per frame ----------------

        void Update()
        {
            if (_lift != 0f) { transform.position -= Vector3.up * _lift; _lift = 0f; }
            if (IsDead) return;
            UpdateFlash();
            if (conductor == null || !conductor.IsPlaying || conductor.Map == null || Player == null) { Tick(false); return; }

            double now = conductor.SongTime;
            BeatNow = conductor.Map.TimeToBeat(now);
            var p = Player.position;
            var me = transform.position;
            float dx = Mathf.Abs(p.x - Home.x), dy = Mathf.Abs(p.y - Home.y);
            if (changesFloors && !Aggro && Mathf.Abs(p.x - me.x) <= aggroRange) Aggro = true;     // a brawl: any height
            if (!Aggro && Mathf.Abs(p.x - me.x) <= aggroRange && dy <= aggroHeight) Aggro = true;
            else if (!changesFloors && Aggro && (dx > leashRange || dy > aggroHeight * 2f)) { Aggro = false; CancelPlan(); }

            if (_hopping) { UpdateHop(); Tick(false); UpdateOverhead(); return; }
            if (changesFloors && Aggro && !Plan.IsValid && BeatNow >= _staggerUntilBeat && !IsDead) ConsiderHop();
            if (_hopping) { Tick(false); UpdateOverhead(); return; }

            if (Aggro && !Plan.IsValid && BeatNow >= _staggerUntilBeat && CanPlan)
            {
                Plan = NextPlan(BeatNow, _notBeforeBeat);
                if (!Plan.IsValid) { Tick(true); UpdateOverhead(); return; }
                if (ForceUnblockable && !Plan.Pattern.Unblockable)
                    Plan = new PlannedAttack(Plan.Pattern.UnblockableCopy(), Plan.FirstHitBeat, (int)Math.Round(Plan.FirstHitBeat - Plan.TelegraphBeat));
                var pattern = Plan.Pattern;
                _hitsDone = 0;
                _hitStarted = false;
                for (int i = 0; i < Plan.HitCount; i++)
                    EnemyThreats.Add(this, (long)Math.Round(Plan.HitBeat(i)), pattern.Damage >= 2 ? 2 : 1);
                OnPlanned(Plan);
            }

            if (Plan.IsValid) { CueWindUp(); RunPlan(now); }
            Tick(true);
            UpdateOverhead();
        }

        void RunPlan(double now)
        {
            var map = conductor.Map;
            while (Plan.IsValid && _hitsDone < Plan.HitCount)
            {
                double hitBeat = Plan.HitBeat(_hitsDone);
                double hitTime = map.BeatToTime(hitBeat);
                if (now < hitTime) break;
                if (!_hitStarted)
                {
                    _hitStarted = true;
                    _hitInRange = InHitRange();
                    OnHitLanded(_hitsDone);
                }
                if (_guard != null && now < ParryJudge.ResolveTime(hitTime, _guard.Windows)) break;

                var result = _guard != null ? _guard.Defend(_hitInRange, hitTime, HitOrigin, Plan.Pattern.Unblockable)
                                            : _hitInRange ? DefenseResult.Hit : DefenseResult.OutOfRange;
                EnemyThreats.Remove(this, (long)Math.Round(hitBeat));
                if (result == DefenseResult.Hit && _health != null)
                {
                    if (!Training) _health.Damage(Plan.Pattern.Damage, HitOrigin);
                    else if (_popups != null) _popups.Show(Player.position + Vector3.up * 2.2f, "BLUNT", new Color(1f, 0.55f, 0.45f), 0.9f);
                }
                HitResolved?.Invoke(this, result);
                OnHitResolved(_hitsDone, result);
                _hitsDone++;
                _hitStarted = false;
                if (_guard != null) Counter(result);
                if (IsDead) return;
                if (result == DefenseResult.Parried) { Stagger(); return; }
                if (result == DefenseResult.Blocked && _guard != null && _guard.BlockSkipsNote && _hitsDone < Plan.HitCount)
                {
                    EnemyThreats.Remove(this, (long)Math.Round(Plan.HitBeat(_hitsDone)));      // the Warden's block: skip a note
                    _hitsDone++;
                    if (_popups != null) _popups.Show(transform.position + Vector3.up * 2.4f, "SKIP", new Color(0.7f, 0.85f, 1f), 0.8f);
                }
            }
            if (Plan.IsValid && Plan.PhaseAt(BeatNow) == AttackPhase.Done)
            {
                _notBeforeBeat = Plan.EndBeat;
                Plan = default;
            }
        }

        /// <summary>
        /// VS-4: a short swell as the enemy galvanizes before an attack's first blow (lower and rougher when it can't be
        /// guarded), louder the closer it is, so the ear hears what the red or violet glow shows.
        /// </summary>
        void CueWindUp()
        {
            if (!Aggro || IsStaggered || Player == null || Plan.FirstHitBeat == _cuedPlan) return;
            double until = Plan.FirstHitBeat - BeatNow;
            if (until > galvanizeBeats || until < 0) return;
            _cuedPlan = Plan.FirstHitBeat;
            float dist = Mathf.Abs(Player.position.x - transform.position.x);
            float vol = Mathf.Lerp(0.8f, 0.25f, Mathf.InverseLerp(2f, 14f, dist));
            Sfx.Play(Plan.Pattern.Unblockable ? Sfx.WindUpViolet : Sfx.WindUp, vol, 0.03f);
        }

        /// <summary>Class perks: a block or parry that hits back (Fighter, Warden).</summary>
        void Counter(DefenseResult result)
        {
            float dmg = result == DefenseResult.Parried ? _guard.ParryDamage : result == DefenseResult.Blocked ? _guard.BlockDamage : 0f;
            if (dmg <= 0f) return;
            var dir = Player != null ? (Vector2)(new Vector2(transform.position.x - Player.position.x, 0f)).normalized : Vector2.right;
            TakeHit(new HitData(dmg, dir, "counter", false, transform.position + Vector3.up * 1.4f));
        }

        void LateUpdate()
        {
            if (IsDead || BeatNow >= _launchUntil || launchBeats <= 0f) return;
            float t = Mathf.Clamp01((float)((BeatNow - _launchFrom) / launchBeats));
            _lift = 4f * launchHeight * t * (1f - t);
            transform.position += Vector3.up * _lift;
        }

        void Stagger(double beats = -1)
        {
            _staggerUntilBeat = BeatNow + (beats > 0 ? beats : staggerBeats);
            _notBeforeBeat = _staggerUntilBeat;
            CancelPlan();
            _flash = 1f;
            OnStaggered();
        }

        void CancelPlan()
        {
            Plan = default;
            _hitStarted = false;
            EnemyThreats.Clear(this);
        }

        // ---------------- Taking hits ----------------

        public void TakeHit(HitData hit)
        {
            if (IsDead) return;
            float scale = hit.EffectId == "counter" ? 1f : DamageScale(hit);       // a class counter goes round the shield
            float dmg = hit.Damage * scale * (IsStaggered ? staggerDamageMultiplier : 1f);
            OnStruck(hit, scale);
            if (_popups != null)
            {
                var at = hit.Point == default ? transform.position + Vector3.up * 1.6f : hit.Point + Vector3.up * 0.3f;
                if (hit.Damage <= 0f) _popups.Show(at, "–", new Color(0.6f, 0.6f, 0.68f), 0.7f);
                else if (scale <= 0f) _popups.Show(at, "NO EFFECT", new Color(0.65f, 0.7f, 0.85f), 0.6f);
                else _popups.Show(at, dmg.ToString("0"), scale < 0.5f ? new Color(0.7f, 0.74f, 0.85f) : IsStaggered ? new Color(1f, 0.6f, 0.25f) : Color.white,
                                  scale < 0.5f ? 0.75f : hit.IsFinisher || IsStaggered ? 1.4f : 1f);
            }
            if (dmg <= 0f) return;
            _hp -= dmg;
            if (Training && _hp < 1f) _hp = maxHp;                 // the training knight shrugs it off and stands up again
            _flash = Mathf.Max(_flash, 0.8f);
            if (!Aggro) Aggro = true;
            if (_hp <= 0f) Die();
            else if (hit.Effect != SequenceEffect.None && scale >= 0.5f) ApplyEffect(hit);     // a shield stops the effect too
        }

        // ---------------- Sequence effects (the library) ----------------

        void ApplyEffect(HitData hit)
        {
            var at = transform.position + Vector3.up * 2.6f;
            if (hit.Effect == SequenceEffect.Launch)
            {
                if (!CanBeLaunched) { if (_popups != null) _popups.Show(at, "STEADY", new Color(0.7f, 0.74f, 0.85f), 0.8f); return; }
                Stagger(Math.Max(staggerBeats, launchBeats + 0.5));
                _launchFrom = BeatNow;
                _launchUntil = BeatNow + launchBeats;
                if (_popups != null) _popups.Show(at, "LAUNCH", new Color(0.55f, 0.85f, 1f), 1f);
            }
            else if (hit.Effect == SequenceEffect.Push)
            {
                float dir = Mathf.Abs(hit.Direction.x) > 0.01f ? Mathf.Sign(hit.Direction.x)
                          : Player != null ? Mathf.Sign(transform.position.x - Player.position.x) : 1f;
                Shove(dir * pushDistance);
                DelayOneBeat();
                if (_popups != null) _popups.Show(at, "PUSHED", new Color(0.6f, 0.75f, 1f), 0.9f);
            }
        }

        /// <summary>The next hit comes a beat later: the current attack slips a beat (if no hit has started), or the next waits one.</summary>
        void DelayOneBeat()
        {
            if (Plan.IsValid && !_hitStarted && _hitsDone < Plan.HitCount)
            {
                int lead = (int)Math.Round(Plan.FirstHitBeat - Plan.TelegraphBeat) + 1;
                var moved = new PlannedAttack(Plan.Pattern, Plan.FirstHitBeat + 1, lead);
                EnemyThreats.Clear(this);
                Plan = moved;
                for (int i = _hitsDone; i < Plan.HitCount; i++)
                    EnemyThreats.Add(this, (long)Math.Round(Plan.HitBeat(i)), Plan.Pattern.Damage >= 2 ? 2 : 1);
            }
            else if (!Plan.IsValid) _notBeforeBeat = Math.Max(_notBeforeBeat, BeatNow + 1);
        }

        /// <summary>Whether a launch can lift this enemy (a boss stands firm).</summary>
        protected virtual bool CanBeLaunched => true;
        /// <summary>Move the body along the lane (a push). Subclasses clamp to their bounds.</summary>
        protected virtual void Shove(float dx) => transform.position += new Vector3(dx, 0f, 0f);

        void Die()
        {
            IsDead = true;
            _hp = 0f;
            _launchUntil = double.NegativeInfinity;
            CancelPlan();
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (_barRoot != null) _barRoot.gameObject.SetActive(false);
            if (_count != null) _count.gameObject.SetActive(false);
            ClearWarning();
            Died?.Invoke(this);
            AnyDied?.Invoke(this);
            OnDied();
        }

        /// <summary>The player died: enemies still standing go back to full health at home.</summary>
        void OnPlayerDied()
        {
            if (IsDead) return;
            _hp = maxHp;
            Aggro = false;
            CancelPlan();
            _staggerUntilBeat = _notBeforeBeat = double.NegativeInfinity;
            _launchUntil = double.NegativeInfinity;
            _lift = 0f;
            _hopping = false;
            _awaySince = double.NaN;
            _patternIndex = 0;
            transform.position = Home;
            OnReset();
        }

        // ---------------- For subclasses ----------------

        /// <summary>The next attack, placed on the beat grid. Default: the patterns in turn, with the count-in rule.</summary>
        protected virtual PlannedAttack NextPlan(double nowBeat, double notBeforeBeat)
        {
            if (patterns.Count == 0) return default;
            var pattern = patterns[_patternIndex % patterns.Count];
            _patternIndex++;
            return EnemyScheduler.Plan(conductor.Map, pattern, nowBeat, notBeforeBeat, oneBarCountIn);
        }

        /// <summary>False holds attacks (a boss mid-transition).</summary>
        protected virtual bool CanPlan => true;
        protected TempoMap Map => conductor != null ? conductor.Map : null;
        /// <summary>Cancel the current attack and wait <paramref name="beats"/> before the next (a phase change).</summary>
        protected void Hold(double beats)
        {
            CancelPlan();
            _notBeforeBeat = BeatNow + beats;
        }

        /// <summary>Move and pose the body each frame. <paramref name="live"/> is false while the music is stopped.</summary>
        protected abstract void Tick(bool live);
        /// <summary>Whether the player is where the current hit lands (checked on the hit beat).</summary>
        protected abstract bool InHitRange();
        /// <summary>Multiplier for damage from the player's hit (0 = no effect, small = shield chip).</summary>
        protected virtual float DamageScale(HitData hit) => 1f;
        protected virtual Vector3 HitOrigin => transform.position + Vector3.up;
        protected virtual void OnPlanned(PlannedAttack a) { }
        protected virtual void OnHitLanded(int index) { }
        protected virtual void OnHitResolved(int index, DefenseResult result) { }
        protected virtual void OnStaggered() { }
        protected virtual void OnStruck(HitData hit, float scale) { }
        protected virtual void OnDied() { }
        protected virtual void OnReset() { }

        protected AttackPhase Phase => Plan.IsValid ? Plan.PhaseAt(BeatNow) : AttackPhase.Waiting;

        /// <summary>
        /// 0..1 while the enemy galvanizes: from <see cref="galvanizeBeats"/> before a hit until just after it (GDD update
        /// 18: a red aura right as it attacks, replacing the 3-2-1 count). 0 otherwise.
        /// </summary>
        public float Galvanize
        {
            get
            {
                if (!Plan.IsValid || IsStaggered || galvanizeBeats <= 0f) return 0f;
                for (int i = 0; i < Plan.HitCount; i++)
                {
                    double h = Plan.HitBeat(i);
                    double until = h - BeatNow;
                    if (until < -0.15) continue;
                    if (until > galvanizeBeats) return 0f;
                    return until <= 0 ? 1f : Mathf.Clamp01(1f - (float)(until / galvanizeBeats) * 0.6f);
                }
                return 0f;
            }
        }
        /// <summary>0..1 through the wind-up (1 = the hit).</summary>
        protected float Windup => Plan.IsValid ? (float)Plan.TelegraphProgress(BeatNow) : 0f;
        /// <summary>Beats since the most recent hit of the plan (negative before the first).</summary>
        protected float SinceHit(int index) => Plan.IsValid && index < Plan.HitCount ? (float)(BeatNow - Plan.HitBeat(index)) : -99f;
        protected double SecondsPerBeat => conductor != null && conductor.IsPlaying ? conductor.CurrentBeat.SecondsPerBeat : 0.46;
        protected float BeatPhase => conductor != null && conductor.IsPlaying ? conductor.BeatPhase : Mathf.Repeat(Time.time * 2.17f, 1f);

        // ---------------- Telegraph look ----------------

        void UpdateFlash()
        {
            float warn = 0f;
            if (Plan.IsValid)
            {
                var ph = Plan.PhaseAt(BeatNow);
                if (ph == AttackPhase.Telegraph)
                {
                    // Brighten through the bar, pulsing on each beat.
                    float w = Windup;
                    warn = w * w * (0.6f + 0.4f * Mathf.Exp(-BeatPhase * 6f));
                }
                else if (ph == AttackPhase.Active) warn = 1f;
            }
            warn = Mathf.Max(warn, Galvanize);
            if (IsStaggered) warn = 0f;
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 5f);
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                var r = flashRenderers[i];
                if (r == null) continue;
                for (int m = 0; m < _baseColors[i].Length; m++)
                {
                    _mpb.Clear();
                    var c = _baseColors[i][m];
                    if (IsStaggered) c = Color.Lerp(c, new Color(0.55f, 0.55f, 0.65f), 0.5f + 0.3f * Mathf.Sin(Time.time * 30f));
                    c = Color.Lerp(c, warnColor, warn * 0.55f);
                    c = Color.Lerp(c, Color.white, _flash);
                    _mpb.SetColor(BaseColorId, c);
                    _mpb.SetColor(EmissionId, _baseEmission[i][m] + warnColor * (warn * 1.5f) + Color.white * (_flash * 0.6f));
                    r.SetPropertyBlock(_mpb, m);
                }
            }
        }

        void BuildOverhead()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var countGo = new GameObject("Count", typeof(TextMesh));
            countGo.transform.SetParent(transform, false);
            countGo.transform.localPosition = Vector3.up * (barHeight + 0.45f);
            _count = countGo.GetComponent<TextMesh>();
            _count.anchor = TextAnchor.MiddleCenter;
            _count.alignment = TextAlignment.Center;
            _count.fontSize = 72;
            _count.characterSize = 0.04f;
            _count.fontStyle = FontStyle.Bold;
            if (font != null) { _count.font = font; countGo.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            _count.text = "";

            // The galvanize aura: a soft glow behind the body that flares one beat before each hit.
            var auraGo = new GameObject("Galvanize Aura", typeof(SpriteRenderer));
            auraGo.transform.SetParent(transform, false);
            _aura = auraGo.GetComponent<SpriteRenderer>();
            _aura.sprite = UI.ProceduralSprites.Glow;
            _aura.sortingOrder = AuraInFront ? 20 : -5;
            _aura.enabled = false;
            if (AuraInFront)
            {
                // A bright body that glows on its own (the wisp) would swallow a soft glow behind it: these draw a red
                // ring and wash in front of the body instead.
                var ringGo = new GameObject("Galvanize Ring", typeof(SpriteRenderer));
                ringGo.transform.SetParent(transform, false);
                _auraRing = ringGo.GetComponent<SpriteRenderer>();
                _auraRing.sprite = UI.ProceduralSprites.Ring;
                _auraRing.sortingOrder = 21;
                _auraRing.enabled = false;
            }

            _barRoot = new GameObject("HP Bar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = Vector3.up * barHeight;
            // Sprites, not quads with a shader looked up by name: a shader only referenced from code is stripped from
            // builds (the bars rendered magenta on the web). Sprites use the built-in sprite shader every build includes.
            var back = new GameObject("Back", typeof(SpriteRenderer));
            back.transform.SetParent(_barRoot, false);
            back.transform.localScale = new Vector3(1.0f, 0.09f, 1f);
            var bsr = back.GetComponent<SpriteRenderer>();
            bsr.sprite = UI.ProceduralSprites.White; bsr.color = new Color(0.08f, 0.06f, 0.1f); bsr.sortingOrder = 10;
            var fill = new GameObject("Fill", typeof(SpriteRenderer));
            fill.transform.SetParent(_barRoot, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            _barFill = fill.transform;
            var fsr = fill.GetComponent<SpriteRenderer>();
            fsr.sprite = UI.ProceduralSprites.White; fsr.color = new Color(0.95f, 0.3f, 0.25f); fsr.sortingOrder = 11;
            _barRoot.gameObject.SetActive(false);
        }

        static void SetBase(Material m, Color c) { if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c); }

        void UpdateOverhead()
        {
            // Keep the overhead UI upright and facing the camera regardless of how the body turns.
            var cam = Camera.main;
            var rot = cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
            if (_barRoot != null)
            {
                bool show = Aggro || _hp < maxHp;
                if (_barRoot.gameObject.activeSelf != show) _barRoot.gameObject.SetActive(show);
                _barRoot.rotation = rot;
                float k = Mathf.Clamp01(_hp / maxHp);
                _barFill.localScale = new Vector3(0.96f * k, 0.06f, 1f);
                _barFill.localPosition = new Vector3(-0.48f * (1f - k), 0f, -0.01f);
            }
            if (_count == null) return;
            _count.transform.rotation = rot;
            string text = "";
            var color = Color.white;
            float scale = 1f;
            if (IsStaggered) { text = "STAGGER"; color = new Color(1f, 0.75f, 0.3f); scale = 0.7f; }
            UpdateAura(rot);
            _count.text = text;
            _count.color = color;
            _count.transform.localScale = Vector3.one * scale;
            _count.transform.position = transform.position + Vector3.up * (barHeight + 0.45f);
            if (_barRoot != null) _barRoot.position = transform.position + Vector3.up * barHeight;
        }

        /// <summary>Draw the galvanize aura in front of the body with a ring (bodies that glow on their own, like the wisp).</summary>
        protected virtual bool AuraInFront => false;
        /// <summary>The aura's size relative to the body's height.</summary>
        protected virtual float AuraSize => 1f;
        /// <summary>Where the aura centres.</summary>
        protected virtual Vector3 AuraCenter => transform.position + Vector3.up * (barHeight * 0.45f);

        void UpdateAura(Quaternion rot)
        {
            if (_aura == null) return;
            float g = Galvanize;
            bool on = g > 0f && !IsDead;
            if (_aura.enabled != on) _aura.enabled = on;
            if (_auraRing != null && _auraRing.enabled != on) _auraRing.enabled = on;
            if (!on) return;
            bool unblockable = Plan.IsValid && Plan.Pattern.Unblockable;
            var c = unblockable ? new Color(0.72f, 0.45f, 1f) : new Color(1f, 0.16f, 0.08f);
            float pulse = 1f + 0.18f * Mathf.Exp(-BeatPhase * 5f);
            float size = barHeight * AuraSize * (0.9f + 0.5f * g) * pulse;
            // Behind the body normally; in front (toward the camera) for glowing bodies.
            var toward = rot * Vector3.forward * (AuraInFront ? -0.9f : 0.6f);
            c.a = AuraInFront ? 0.22f + 0.33f * g : 0.35f + 0.5f * g;
            _aura.color = c;
            _aura.transform.position = AuraCenter + toward;
            _aura.transform.rotation = rot;
            _aura.transform.localScale = Vector3.one * size;
            if (_auraRing != null)
            {
                var rc = c;
                rc.a = 0.55f + 0.45f * g;
                _auraRing.color = rc;
                _auraRing.transform.position = AuraCenter + toward * 1.05f;
                _auraRing.transform.rotation = rot;
                _auraRing.transform.localScale = Vector3.one * (size * 0.8f);
            }
        }

        /// <summary>Death clears the attack warning at once: no aura, no red tint left on the body as it falls.</summary>
        void ClearWarning()
        {
            if (_aura != null) _aura.enabled = false;
            if (_auraRing != null) _auraRing.enabled = false;
            _flash = 0f;
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                var r = flashRenderers[i];
                if (r == null) continue;
                for (int m = 0; m < _baseColors[i].Length; m++)
                {
                    _mpb.Clear();
                    _mpb.SetColor(BaseColorId, _baseColors[i][m]);
                    _mpb.SetColor(EmissionId, _baseEmission[i][m]);
                    r.SetPropertyBlock(_mpb, m);
                }
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f);
            Gizmos.DrawWireCube(Application.isPlaying ? Home : transform.position, new Vector3(aggroRange * 2f, aggroHeight * 2f, 0.1f));
        }
    }
}
