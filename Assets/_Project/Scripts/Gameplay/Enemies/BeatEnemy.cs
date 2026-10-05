using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
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
        [Header("Stagger")]
        [SerializeField] protected int staggerBeats = 2;
        [SerializeField] protected float staggerDamageMultiplier = 1.5f;
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
        int _patternIndex;
        protected Vector3 Home { get; private set; }
        float _hp, _flash;
        MaterialPropertyBlock _mpb;
        Color[][] _baseColors, _baseEmission;
        TextMesh _count;
        Transform _barRoot, _barFill;

        public string DisplayName => displayName;
        public float Hp => _hp;
        public float MaxHp => maxHp;
        public bool IsDead { get; private set; }
        public bool Aggro { get; private set; }
        public double BeatNow { get; private set; }
        public bool IsStaggered => BeatNow < _staggerUntilBeat;

        public event Action<BeatEnemy> Died;
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
        }

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); EnemyThreats.Clear(this); }
        protected virtual void OnDestroy() { if (_health != null) _health.Died -= OnPlayerDied; }

        // ---------------- Per frame ----------------

        void Update()
        {
            if (IsDead) return;
            UpdateFlash();
            if (conductor == null || !conductor.IsPlaying || conductor.Map == null || Player == null) { Tick(false); return; }

            double now = conductor.SongTime;
            BeatNow = conductor.Map.TimeToBeat(now);
            var p = Player.position;
            var me = transform.position;
            float dx = Mathf.Abs(p.x - Home.x), dy = Mathf.Abs(p.y - Home.y);
            if (!Aggro && Mathf.Abs(p.x - me.x) <= aggroRange && dy <= aggroHeight) Aggro = true;
            else if (Aggro && (dx > leashRange || dy > aggroHeight * 2f)) { Aggro = false; CancelPlan(); }

            if (Aggro && !Plan.IsValid && BeatNow >= _staggerUntilBeat && CanPlan)
            {
                Plan = NextPlan(BeatNow, _notBeforeBeat);
                if (!Plan.IsValid) { Tick(true); UpdateOverhead(); return; }
                var pattern = Plan.Pattern;
                _hitsDone = 0;
                _hitStarted = false;
                for (int i = 0; i < Plan.HitCount; i++)
                    EnemyThreats.Add(this, (long)Math.Round(Plan.HitBeat(i)), pattern.Damage >= 2 ? 2 : 1);
                OnPlanned(Plan);
            }

            if (Plan.IsValid) RunPlan(now);
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
                if (result == DefenseResult.Hit && _health != null) _health.Damage(Plan.Pattern.Damage, HitOrigin);
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

        /// <summary>Class perks: a block or parry that hits back (Fighter, Warden).</summary>
        void Counter(DefenseResult result)
        {
            float dmg = result == DefenseResult.Parried ? _guard.ParryDamage : result == DefenseResult.Blocked ? _guard.BlockDamage : 0f;
            if (dmg <= 0f) return;
            var dir = Player != null ? (Vector2)(new Vector2(transform.position.x - Player.position.x, 0f)).normalized : Vector2.right;
            TakeHit(new HitData(dmg, dir, "counter", false, transform.position + Vector3.up * 1.4f));
        }

        void Stagger()
        {
            _staggerUntilBeat = BeatNow + staggerBeats;
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
            _flash = Mathf.Max(_flash, 0.8f);
            if (!Aggro) Aggro = true;
            if (_hp <= 0f) Die();
        }

        void Die()
        {
            IsDead = true;
            _hp = 0f;
            CancelPlan();
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (_barRoot != null) _barRoot.gameObject.SetActive(false);
            if (_count != null) _count.gameObject.SetActive(false);
            Died?.Invoke(this);
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

            _barRoot = new GameObject("HP Bar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = Vector3.up * barHeight;
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(_barRoot, false);
            back.transform.localScale = new Vector3(1.0f, 0.09f, 1f);
            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(_barRoot, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            _barFill = fill.transform;
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                var bm = new Material(unlit); bm.color = new Color(0.08f, 0.06f, 0.1f); SetBase(bm, bm.color);
                var fm = new Material(unlit); fm.color = new Color(0.95f, 0.3f, 0.25f); SetBase(fm, fm.color);
                back.GetComponent<MeshRenderer>().sharedMaterial = bm;
                fill.GetComponent<MeshRenderer>().sharedMaterial = fm;
            }
            foreach (var r in _barRoot.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            else if (Plan.IsValid)
            {
                var ph = Plan.PhaseAt(BeatNow);
                if (ph == AttackPhase.Telegraph || ph == AttackPhase.Active)
                {
                    int n = Plan.BeatsUntilNextHit(BeatNow);
                    if (n >= 1 && n <= 3) { text = n.ToString(); color = Color.Lerp(new Color(1f, 0.85f, 0.4f), warnColor, (3 - n) / 2f); }
                    else if (n == 0 || ph == AttackPhase.Active) { text = Plan.Pattern.Unblockable ? "JUMP" : "!"; color = warnColor; }
                    if (Plan.Pattern.Unblockable && text != "JUMP" && text != "") color = new Color(0.75f, 0.55f, 1f);
                    scale = 1f + 0.35f * Mathf.Exp(-BeatPhase * 6f);
                }
            }
            _count.text = text;
            _count.color = color;
            _count.transform.localScale = Vector3.one * scale;
            _count.transform.position = transform.position + Vector3.up * (barHeight + 0.45f);
            if (_barRoot != null) _barRoot.position = transform.position + Vector3.up * barHeight;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f);
            Gizmos.DrawWireCube(Application.isPlaying ? Home : transform.position, new Vector3(aggroRange * 2f, aggroHeight * 2f, 0.1f));
        }
    }
}
