using System.Collections;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.UI;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The summit boss (VS-3): a giant skeleton cantor who conducts "Hymn of the Summit" through 4/4, 3/4 and 7/8 and
    /// fights in whatever meter the music is in. Every attack is counted in for one bar of the meter it lands in, and
    /// never straddles a meter change (<see cref="BossPlanner"/>); through the bar before a change he raises his staff
    /// and conducts it (the HUD names the next meter). Three phases by health: the first fights with the gavel and the
    /// simplest pattern of each meter; the second adds four-on-the-floor, the hesitation and the shockwave (a ground
    /// slam no guard can stop: jump it or dodge it); the third adds the full 7/8 run. Each phase change holds him for a
    /// bar. He takes reduced damage from the front until he recovers after an attack.
    /// </summary>
    public class BoneCantor : BeatEnemy
    {
        [System.Serializable]
        public class NamedClips
        {
            public string pattern;
            public List<BoneDancer.HitClip> hits = new List<BoneDancer.HitClip>();
        }

        [Header("Body")]
        [SerializeField] Transform body;
        [SerializeField] float cameraYaw = 16f;

        [Header("Clip rig (art pass)")]
        [SerializeField] Transform rigRoot;
        [SerializeField] BakedClip idleClip, walkClip, staggerClip, deathClip, conductClip;
        [SerializeField] List<NamedClips> attackClips = new List<NamedClips>();

        [Header("Movement")]
        [SerializeField] float walkSpeed = 1.3f;
        [SerializeField] float keepDistance = 2.4f;
        [SerializeField] Vector2 bounds = new Vector2(-1000f, 1000f);

        [Header("Fighting")]
        [SerializeField] float hitReach = 3.3f;
        [SerializeField] float hitHeight = 2.4f;
        [Tooltip("Shockwave: reaches this far along the ground either side; a jump clears it.")]
        [SerializeField] float shockReach = 7f;
        [SerializeField] float shockClearHeight = 0.6f;
        [SerializeField, Range(0f, 1f)] float frontShare = 0.7f;

        readonly List<BossMove> _moves = new List<BossMove>();
        int _turn, _phase, _face = -1;
        ClipPose _pose;
        float _lastX, _staggerT = 9f, _phaseT = 9f, _lean;
        bool _conducting;
        Transform _ring;

        public int BossPhase => _phase;
        public bool Conducting => _conducting;

        public static List<BossMove> DefaultMoves() => new List<BossMove>
        {
            new BossMove(new EnemyAttackPattern("Gavel", new[] { 0.0 }, 4, 3, 2), 0),
            new BossMove(new EnemyAttackPattern("Down-Up", new[] { 0.0, 2.0 }, 4, 2, 1), 4),
            new BossMove(new EnemyAttackPattern("Waltz", new[] { 0.0, 1.0, 2.0 }, 3, 3, 1), 3),
            new BossMove(new EnemyAttackPattern("Limp", new[] { 0.0, 2.0, 4.0 }, 7, 6, 1), 7),
            new BossMove(new EnemyAttackPattern("Four on the Floor", new[] { 0.0, 1.0, 2.0, 3.0 }, 4, 2, 1), 4, 1),
            new BossMove(new EnemyAttackPattern("Hesitation", new[] { 0.0, 2.0 }, 3, 3, 1), 3, 1),
            new BossMove(new EnemyAttackPattern("Shockwave", new[] { 0.0 }, 4, 3, 2).WithUnblockable(), 0, 1),
            new BossMove(new EnemyAttackPattern("Limp Run", new[] { 0.0, 2.0, 4.0, 6.0 }, 7, 6, 1), 7, 2),
        };

        protected override void Awake()
        {
            _moves.AddRange(DefaultMoves());
            base.Awake();
            if (rigRoot != null) _pose = new ClipPose(rigRoot.GetComponentsInChildren<Transform>(true), "root");
            _lastX = transform.position.x;
            ApplyFacing(true);
            BuildRing();
        }

        protected override void Start()
        {
            base.Start();
            BossHud.Track(this);
        }

        protected override PlannedAttack NextPlan(double nowBeat, double notBeforeBeat)
        {
            var map = Map;
            if (map == null) return default;
            return BossPlanner.Plan(map, _moves, _phase, ref _turn, nowBeat, notBeforeBeat);
        }

        protected override void Tick(bool live)
        {
            float dt = Time.deltaTime;
            _staggerT += dt;
            _phaseT += dt;
            var phase = Phase;
            bool attacking = phase == AttackPhase.Telegraph || phase == AttackPhase.Active;

            // Phase changes: hold for a bar and flourish.
            int want = BossPlanner.PhaseFor(Hp, MaxHp);
            if (live && want > _phase && !IsDead)
            {
                _phase = want;
                _phaseT = 0f;
                int bar = conductor != null && conductor.IsPlaying ? conductor.CurrentBeat.BeatsPerMeasure : 4;
                Hold(bar);
                GameHud.Toast(_phase == 1
                    ? "The Cantor quickens: four on the floor, and shockwaves (jump or dodge them)"
                    : "The Cantor's last verse: the full seven");
            }

            _conducting = live && Map != null && !attacking && BossPlanner.Cue(Map, BeatNow).Changing;

            var pos = transform.position;
            if (live && Player != null && Aggro && !IsStaggered && !attacking && _phaseT > 0.5f)
            {
                float px = Player.position.x;
                _face = px >= pos.x ? 1 : -1;
                float target = px - _face * keepDistance;
                if (!_conducting) pos.x = Mathf.MoveTowards(pos.x, target, walkSpeed * dt);
            }
            else if (live && phase == AttackPhase.Telegraph && Windup > 0.85f)
                pos.x += _face * 1.0f * dt;
            pos.x = Mathf.Clamp(pos.x, bounds.x, bounds.y);
            transform.position = pos;
            ApplyFacing(false);
            Pose(dt, phase);
            UpdateRing(dt);
        }

        void ApplyFacing(bool snap)
        {
            if (body == null) return;
            var want = Quaternion.AngleAxis(_face * cameraYaw, Vector3.up) * Quaternion.LookRotation(new Vector3(_face, 0f, 0f));
            body.rotation = snap ? want : Quaternion.RotateTowards(body.rotation, want, 540f * Time.deltaTime);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float Loop(BakedClip c) => c == null ? 0f : (float)(Time.timeAsDouble / Mathf.Max(0.1f, c.length));

        NamedClips ClipsFor(string pattern)
        {
            foreach (var c in attackClips) if (c != null && c.pattern == pattern) return c;
            return null;
        }

        void Pose(float dt, AttackPhase phase)
        {
            if (_pose == null) { PrimitivePose(dt, phase); return; }
            float x = transform.position.x;
            bool walking = Mathf.Abs(x - _lastX) > 0.4f * dt;
            _lastX = x;
            _pose.Reset();
            var stance = walking && walkClip != null ? walkClip : idleClip;
            _pose.Layer(stance, Loop(stance), 1f, true);
            if (conductClip != null && (_conducting || _phaseT < 1.2f))
                _pose.Layer(conductClip, Mathf.Repeat(BeatPhase * 0.5f + 0.1f, 1f), 0.8f);

            var named = Plan.IsValid ? ClipsFor(Plan.Pattern.Name) : null;
            if (named != null && !IsStaggered)
            {
                int n = Plan.HitCount;
                for (int i = 0; i < n; i++)
                {
                    var hc = named.hits.Count > 0 ? named.hits[i % named.hits.Count] : null;
                    if (hc == null || hc.clip == null) continue;
                    float tau = SinceHit(i);
                    float gap = i + 1 < n ? (float)(Plan.HitBeat(i + 1) - Plan.HitBeat(i)) : 2f;
                    float post = i == n - 1 ? 1.4f : Mathf.Min(0.6f, gap * 0.6f);
                    float pre = i == 0 ? 1f : Mathf.Min(0.6f, gap * 0.6f);
                    float u = ClipTiming.Warp(tau, -pre, post, 0f, hc.hit, 1f);
                    float wIn = i == 0 ? Smooth((tau + pre + 0.5f) / 0.5f) : Smooth((tau + pre) / 0.12f);
                    float wOut = i == n - 1 ? 1f - Smooth((tau - post + 0.5f) / 0.5f) : 1f - Smooth((tau - post + 0.1f) / 0.1f);
                    float w = Mathf.Min(wIn, wOut);
                    if (i == 0 && tau < -pre) { u = 0f; w = Smooth(Windup * 1.2f) * 0.85f; }
                    _pose.Layer(hc.clip, u, w);
                }
            }
            if (staggerClip != null && IsStaggered) _pose.Layer(staggerClip, Mathf.Clamp01(_staggerT / 0.7f), 1f);
            _pose.Apply();
        }

        /// <summary>Placeholder look: lean back through the count-in, slam forward on each hit, raise the staff to conduct.</summary>
        void PrimitivePose(float dt, AttackPhase phase)
        {
            if (body == null || body.childCount == 0) return;
            float target = 0f;
            if (phase == AttackPhase.Telegraph) target = -14f * Smooth(Windup);
            if (phase == AttackPhase.Active || phase == AttackPhase.Recover)
                for (int i = 0; i < Plan.HitCount; i++)
                {
                    float s = SinceHit(i);
                    if (s > -0.12f && s < 0.5f) target = 22f * (1f - Smooth(s / 0.5f));
                }
            if (_conducting) target = -8f + 6f * Mathf.Sin(BeatPhase * Mathf.PI * 2f);
            _lean = Mathf.Lerp(_lean, target, 1f - Mathf.Exp(-18f * dt));
            var t = body.GetChild(0);
            t.localRotation = Quaternion.Euler(_lean, 0f, IsStaggered ? 6f * Mathf.Sin(Time.time * 20f) : 0f);
        }

        // ---------------- Shockwave ring ----------------

        void BuildRing()
        {
            // A sprite lying on the ground (the built-in sprite shader is in every build; a shader found by name isn't).
            var ring = new GameObject("Shockwave", typeof(SpriteRenderer));
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var sr = ring.GetComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Ring;
            sr.color = new Color(0.8f, 0.6f, 1f, 0.9f);
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.SetActive(false);
            _ring = ring.transform;
        }

        float _ringT = 9f;

        void UpdateRing(float dt)
        {
            if (_ring == null) return;
            _ringT += dt;
            bool on = _ringT < 0.45f;
            if (_ring.gameObject.activeSelf != on) _ring.gameObject.SetActive(on);
            if (!on) return;
            float k = _ringT / 0.45f;
            float d = Mathf.Lerp(0.5f, shockReach * 2f, Smooth(k));
            _ring.localScale = new Vector3(d, Mathf.Min(d, 3f), 1f);
        }

        protected override void OnHitLanded(int index)
        {
            if (Plan.IsValid && Plan.Pattern.Unblockable) _ringT = 0f;
        }

        // ---------------- Combat ----------------

        protected override bool InHitRange()
        {
            if (Player == null) return false;
            float dx = Player.position.x - transform.position.x;
            float dy = Player.position.y - transform.position.y;
            if (Plan.IsValid && Plan.Pattern.Unblockable)
                return Mathf.Abs(dx) < shockReach && dy < shockClearHeight && dy > -1f;
            float ahead = dx * _face;
            return ahead > -0.5f && ahead < hitReach && Mathf.Abs(dy) < hitHeight;
        }

        protected override Vector3 HitOrigin => transform.position + new Vector3(_face * 1.0f, 1.6f, 0f);

        protected override float DamageScale(HitData hit) => Phase == AttackPhase.Recover || IsStaggered ? 1f : frontShare;

        protected override void OnStaggered() => _staggerT = 0f;

        protected override void OnReset()
        {
            _face = -1;
            _phase = 0;
            _turn = 0;
            ApplyFacing(true);
        }

        protected override void OnDied()
        {
            GameHud.Toast("The Bone Cantor falls silent. The way down is open.", 3.5f);
            StartCoroutine(Fall());
        }

        IEnumerator Fall()
        {
            float t = 0f;
            if (_pose != null && deathClip != null)
            {
                while (t < 1f)
                {
                    t += Time.deltaTime / Mathf.Max(0.3f, deathClip.length);
                    _pose.Reset();
                    _pose.Layer(deathClip, t);
                    _pose.Apply();
                    yield return null;
                }
                yield return new WaitForSeconds(2f);
            }
            else
            {
                var start = body != null ? body.localRotation : Quaternion.identity;
                while (t < 1f)
                {
                    t += Time.deltaTime / 0.8f;
                    if (body != null) body.localRotation = start * Quaternion.Euler(0f, 0f, -85f * Smooth(t) * _face);
                    yield return null;
                }
                yield return new WaitForSeconds(1.5f);
            }
            var p0 = transform.position;
            for (t = 0f; t < 1f; t += Time.deltaTime / 1.2f) { transform.position = p0 + Vector3.down * 2f * t; yield return null; }
            gameObject.SetActive(false);
        }
    }
}
