using System.Collections;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Slice 2's ground enemy: a shield knight that patrols its ledge, closes in when it sees the player and fights
    /// on the beat with two attacks (a heavy overhead cut, and a two-beat flurry where each hit must be parried).
    /// The shield takes hits from the front (they only chip it) except while the knight recovers or is staggered;
    /// jumping over it and striking its back does full damage. It turns around only between attacks, a beat late.
    /// The body is posed in code from pivots the builder makes.
    /// </summary>
    public class ShieldKnight : BeatEnemy
    {
        [Header("Body")]
        [SerializeField] Transform body;
        [SerializeField] Transform torso;
        [SerializeField] Transform swordArm;
        [SerializeField] Transform shieldArm;
        [SerializeField] float cameraYaw = 14f;

        [Header("Clip rig (art pass; replaces the primitive pivots when set)")]
        [SerializeField] Transform rigRoot;
        [SerializeField] BakedClip idleClip, guardClip, walkClip, cutClip, flurryAClip, flurryBClip, blockHitClip, staggerClip, deathClip;
        [Tooltip("Normalized impact frames of the attack clips.")]
        [SerializeField] float cutHit = 0.54f, flurryAHit = 0.44f, flurryBHit = 0.2f;
        [Tooltip("Beats of visible wind-up before each hit (the glow and count run the whole bar).")]
        [SerializeField] float windBeats = 1f;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 1.6f;
        [SerializeField] float patrolHalfWidth = 2.5f;
        [SerializeField] float keepDistance = 1.5f;
        [Tooltip("The knight never leaves this stretch of the lane (its ledge).")]
        [SerializeField] Vector2 bounds = new Vector2(-1000f, 1000f);
        [SerializeField] float turnDelayBeats = 0.75f;

        [Header("Fighting")]
        [SerializeField] float hitReach = 2.2f;
        [SerializeField] float hitHeight = 1.6f;
        [Tooltip("Damage share from the front while the shield is up.")]
        [SerializeField, Range(0f, 1f)] float shieldChip = 0.12f;
        [SerializeField, Range(0f, 1f)] float recoverFrontShare = 0.6f;
        [SerializeField] float backstabMultiplier = 1.3f;

        int _face = -1;
        ClipPose _pose;
        float _lastX, _blockHitT = 9f, _staggerT = 9f;
        int _patrolDir = -1;
        float _behindFor, _shieldKick, _lunge;
        float _armAngle = -30f, _torsoBase;

        protected override void Awake()
        {
            if (patterns.Count == 0)
            {
                patterns.Add(new EnemyAttackPattern("Heavy Cut", new[] { 0.0 }, 4, 2, 2));
                patterns.Add(new EnemyAttackPattern("Flurry", new[] { 0.0, 1.0 }, 4, 2, 1));
            }
            base.Awake();
            if (torso != null) _torsoBase = torso.localPosition.y;
            if (rigRoot != null) _pose = new ClipPose(rigRoot.GetComponentsInChildren<Transform>(true), "root");
            _lastX = transform.position.x;
            ApplyFacing();
        }

        protected override void Tick(bool live)
        {
            float dt = Time.deltaTime;
            var phase = Phase;
            bool attacking = phase == AttackPhase.Telegraph || phase == AttackPhase.Active;
            var pos = transform.position;

            if (live && Player != null && !IsStaggered)
            {
                float px = Player.position.x;
                if (Aggro)
                {
                    // Turn to the player only between attacks, and only once they've been behind for a moment.
                    bool behind = Mathf.Sign(px - pos.x) != _face && Mathf.Abs(px - pos.x) > 0.2f;
                    _behindFor = behind && !attacking ? _behindFor + dt : 0f;
                    if (_behindFor > turnDelayBeats * (float)SecondsPerBeat) { _face = -_face; _behindFor = 0f; }
                    if (!attacking)
                    {
                        float want = px - _face * keepDistance;
                        pos.x = Mathf.MoveTowards(pos.x, want, walkSpeed * dt);
                    }
                    else if (phase == AttackPhase.Telegraph && Windup > 0.8f)
                        pos.x += _face * 1.2f * dt;           // step into the cut
                }
                else
                {
                    pos.x += _patrolDir * walkSpeed * 0.6f * dt;
                    if (pos.x < Home.x - patrolHalfWidth) _patrolDir = 1;
                    if (pos.x > Home.x + patrolHalfWidth) _patrolDir = -1;
                    _face = _patrolDir;
                }
                pos.x = Mathf.Clamp(pos.x, bounds.x, bounds.y);
                transform.position = pos;
            }
            ApplyFacing();
            Pose(dt, phase);
        }

        void ApplyFacing()
        {
            if (body == null) return;
            var want = Quaternion.AngleAxis(_face * cameraYaw, Vector3.up) * Quaternion.LookRotation(new Vector3(_face, 0f, 0f));
            body.rotation = Quaternion.RotateTowards(body.rotation, want, 720f * Time.deltaTime);
        }

        void Pose(float dt, AttackPhase phase)
        {
            if (_pose != null) { ClipPoseUpdate(dt, phase); return; }
            const float Rest = -30f, Raised = -170f, Struck = -40f;
            float target = Rest;
            float lean = 0f;
            const float Lead = 0.08f;   // the swing starts just before the beat so the blade passes horizontal on it
            int last = -1;
            if (phase == AttackPhase.Telegraph || phase == AttackPhase.Active || phase == AttackPhase.Recover)
                for (int i = 0; i < Plan.HitCount; i++) if (SinceHit(i) >= -Lead) last = i;
            if (phase == AttackPhase.Telegraph && last < 0)
            {
                float w = Smooth(Windup);
                target = Mathf.Lerp(Rest, Raised, w);
                lean = -10f * w;
            }
            else if (last >= 0)
            {
                float s = SinceHit(last) + Lead;
                float k0 = Smooth(s / (2f * Lead));
                target = Mathf.Lerp(Raised, Struck, k0);
                lean = Mathf.Lerp(-10f, 18f, k0);
                int next = last + 1;
                if (next < Plan.HitCount)
                {
                    float sn = SinceHit(next);                       // negative: beats until the next hit
                    if (sn > -0.7f) { float k = Smooth((sn + 0.7f) / 0.55f); target = Mathf.Lerp(Struck, -155f, k); lean = Mathf.Lerp(18f, -8f, k); }
                }
                else if (s > 0.4f)
                {
                    float k = Smooth((s - 0.4f) / 1.2f);
                    target = Mathf.Lerp(Struck, Rest, k);
                    lean = Mathf.Lerp(18f, 0f, k);
                }
            }
            // Attack poses are already continuous in beat time (so strikes land exactly on the beat); ease only outside them.
            bool attackPose = phase == AttackPhase.Active || phase == AttackPhase.Recover || (phase == AttackPhase.Telegraph && Windup > 0.1f);
            _armAngle = attackPose ? target : Mathf.Lerp(_armAngle, target, 1f - Mathf.Exp(-12f * dt));
            if (swordArm != null) swordArm.localRotation = Quaternion.Euler(_armAngle, 0f, 0f);

            _shieldKick = Mathf.Max(0f, _shieldKick - dt * 5f);
            float shieldX = IsStaggered ? -10f : phase == AttackPhase.Recover ? -45f : -80f;
            float shieldZ = IsStaggered ? 35f * Mathf.Sin(Time.time * 18f) : 0f;
            if (shieldArm != null) shieldArm.localRotation = Quaternion.Euler(shieldX + _shieldKick * 25f, 0f, shieldZ);

            float bob = Mathf.Exp(-BeatPhase * 6f) * 0.04f;
            if (torso != null)
            {
                torso.localRotation = Quaternion.Euler(lean, 0f, IsStaggered ? 8f * Mathf.Sin(Time.time * 22f) : 0f);
                var tp = torso.localPosition; tp.y = _torsoBase - bob; torso.localPosition = tp;
            }
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static float Loop(BakedClip c) => c == null ? 0f : (float)(Time.timeAsDouble / Mathf.Max(0.1f, c.length));

        /// <summary>
        /// Clip-driven pose (art pass): a guard stance or walk, each attack clip's impact frame placed exactly on its
        /// hit beat (the wind-up plays over the beat before it), a shield jolt when blocked and a reel when staggered.
        /// </summary>
        void ClipPoseUpdate(float dt, AttackPhase phase)
        {
            float x = transform.position.x;
            bool walking = Mathf.Abs(x - _lastX) > 0.6f * dt;
            _lastX = x;
            _blockHitT += dt;
            _staggerT += dt;

            _pose.Reset();
            var stance = Aggro && guardClip != null ? guardClip : idleClip;
            _pose.Layer(walking && walkClip != null ? walkClip : stance, Loop(walking && walkClip != null ? walkClip : stance), 1f, true);

            if (Plan.IsValid && !IsStaggered)
            {
                int n = Plan.HitCount;
                for (int i = 0; i < n; i++)
                {
                    var clip = n == 1 ? cutClip : i == 0 ? flurryAClip : flurryBClip;
                    float hit = n == 1 ? cutHit : i == 0 ? flurryAHit : flurryBHit;
                    if (clip == null) continue;
                    float tau = SinceHit(i);
                    float post = i == n - 1 ? 1.4f : 0.6f;
                    float pre = i == 0 ? windBeats : 0.7f;
                    float u = ClipTiming.Warp(tau, -pre, post, 0f, hit, 1f);
                    float wIn = i == 0 ? Smooth((tau + pre + 0.5f) / 0.5f) : Smooth((tau + pre) / 0.15f);
                    float wOut = i == n - 1 ? 1f - Smooth((tau - post + 0.5f) / 0.5f) : 1f;
                    float w = Mathf.Min(wIn, wOut);
                    if (i == 0 && tau < -pre) { u = 0f; w = Smooth(Windup * 1.2f) * 0.85f; }   // settle into the ready frame through the bar
                    _pose.Layer(clip, u, w);
                }
            }
            if (blockHitClip != null && _blockHitT < 0.5f) _pose.Layer(blockHitClip, Mathf.Lerp(0.05f, 0.6f, _blockHitT / 0.5f), 1f - Smooth((_blockHitT - 0.3f) / 0.2f));
            if (staggerClip != null && IsStaggered) _pose.Layer(staggerClip, Mathf.Clamp01(_staggerT / 0.7f), 1f);
            _pose.Apply();
        }

        bool PlayerInFront => Player != null && Mathf.Sign(Player.position.x - transform.position.x) == _face;

        protected override bool InHitRange()
        {
            if (Player == null) return false;
            float ahead = (Player.position.x - transform.position.x) * _face;
            return ahead > -0.3f && ahead < hitReach && Mathf.Abs(Player.position.y - transform.position.y) < hitHeight;
        }

        protected override Vector3 HitOrigin => transform.position + new Vector3(_face * 0.6f, 1.1f, 0f);

        protected override float DamageScale(HitData hit)
        {
            if (IsStaggered) return 1f;
            if (!PlayerInFront) return backstabMultiplier;
            return Phase == AttackPhase.Recover ? recoverFrontShare : shieldChip;
        }

        protected override void OnStruck(HitData hit, float scale)
        {
            if (scale < 0.5f) { _shieldKick = 1f; _blockHitT = 0f; }
        }

        protected override void OnStaggered()
        {
            _staggerT = 0f;
            if (Player == null) return;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x - _face * 0.5f, bounds.x, bounds.y);
            transform.position = p;
        }

        protected override void OnReset()
        {
            _face = -1;
            _patrolDir = -1;
            _armAngle = -30f;
            if (body != null) body.localRotation = Quaternion.identity;
            ApplyFacing();
        }

        protected override void OnDied() => StartCoroutine(Fall());

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
                yield return new WaitForSeconds(1.5f);
                var start0 = transform.position;
                for (t = 0f; t < 1f; t += Time.deltaTime / 0.8f) { transform.position = start0 + Vector3.down * 1.2f * t; yield return null; }
                gameObject.SetActive(false);
                yield break;
            }
            var start = body != null ? body.localRotation : Quaternion.identity;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.5f;
                if (body != null) body.localRotation = start * Quaternion.Euler(0f, 0f, -85f * Smooth(t) * _face);
                yield return null;
            }
            yield return new WaitForSeconds(1.2f);
            t = 0f;
            var p = transform.position;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.8f;
                transform.position = p + Vector3.down * 1.2f * t;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
