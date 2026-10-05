using System;
using System.Collections;
using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// VS-2's waltz enemy: a skeleton with two blades that moves and fights in threes. Between attacks it glides one
    /// step toward the player on each downbeat and sways on beats 2 and 3, so its movement counts the bar. Its attacks
    /// are telegraphed for one bar (3-2-1 in 3/4) and come in three shapes: the Waltz (a hit on every beat of the bar,
    /// one, two, three), the Hesitation (one and three, holding on two) and the Turn (a single heavy spin on the
    /// downbeat). No shield: every blade lands, and it takes extra damage while it recovers.
    /// </summary>
    public class BoneDancer : BeatEnemy
    {
        [Serializable]
        public class HitClip
        {
            public BakedClip clip;
            [Tooltip("Normalized impact frame (placed exactly on the hit beat).")]
            public float hit = 0.45f;
        }

        [Serializable]
        public class PatternClips
        {
            public List<HitClip> hits = new List<HitClip>();
        }

        [Header("Body")]
        [SerializeField] Transform body;
        [SerializeField] float cameraYaw = 14f;

        [Header("Clip rig (art pass)")]
        [SerializeField] Transform rigRoot;
        [SerializeField] BakedClip idleClip, stepClip, staggerClip, deathClip, tauntClip;
        [Tooltip("One entry per attack pattern, in the same order; one clip per hit.")]
        [SerializeField] List<PatternClips> patternClips = new List<PatternClips>();

        [Header("Movement")]
        [Tooltip("Metres glided on each downbeat while closing in.")]
        [SerializeField] float stepLength = 1.3f;
        [SerializeField] float keepDistance = 1.4f;
        [SerializeField] Vector2 bounds = new Vector2(-1000f, 1000f);

        [Header("Fighting")]
        [SerializeField] float hitReach = 2.3f;
        [SerializeField] float hitHeight = 1.6f;
        [Tooltip("Damage share while it isn't recovering or staggered (it parries a little with the off blade).")]
        [SerializeField, Range(0f, 1f)] float guardShare = 0.75f;

        int _face = -1;
        ClipPose _pose;
        long _stepBar = long.MinValue;
        float _stepFrom, _stepTo;
        float _staggerT = 9f, _spin;

        public static List<EnemyAttackPattern> DefaultPatterns() => new List<EnemyAttackPattern>
        {
            new EnemyAttackPattern("Waltz", new[] { 0.0, 1.0, 2.0 }, 3, 3, 1),
            new EnemyAttackPattern("Turn", new[] { 0.0 }, 3, 3, 2),
            new EnemyAttackPattern("Hesitation", new[] { 0.0, 2.0 }, 3, 3, 1),
        };

        protected override void Awake()
        {
            if (patterns.Count == 0) patterns.AddRange(DefaultPatterns());
            base.Awake();
            if (rigRoot != null) _pose = new ClipPose(rigRoot.GetComponentsInChildren<Transform>(true), "root");
            ApplyFacing(true);
        }

        protected override void Tick(bool live)
        {
            float dt = Time.deltaTime;
            var phase = Phase;
            bool attacking = phase == AttackPhase.Telegraph || phase == AttackPhase.Active;
            var pos = transform.position;

            if (live && Player != null && !IsStaggered && conductor != null && conductor.Map != null)
            {
                float px = Player.position.x;
                if (!attacking && Aggro) _face = px >= pos.x ? 1 : -1;      // turns only between attacks

                // Waltz step: one glide per bar, on the downbeat, toward the player (or along its patrol when idle).
                double beat = BeatNow;
                long index = (long)Math.Floor(beat);
                var info = conductor.Map.GetBeat(index);
                float frac = (float)(beat - index);
                if (info.IsDownbeat && info.Measure != _stepBar && !attacking)
                {
                    _stepBar = info.Measure;
                    _stepFrom = pos.x;
                    float want = Aggro ? px - _face * keepDistance : Home.x + (info.Measure % 2 == 0 ? 1f : -1f) * stepLength * 0.6f;
                    _stepTo = Mathf.Clamp(Mathf.MoveTowards(pos.x, want, stepLength), bounds.x, bounds.y);
                    if (!Aggro) _face = _stepTo >= pos.x ? 1 : -1;
                }
                if (info.IsDownbeat && info.Measure == _stepBar && !attacking)
                    pos.x = Mathf.Lerp(_stepFrom, _stepTo, Smooth(frac));
                else if (phase == AttackPhase.Telegraph && Windup > 0.85f)
                    pos.x += _face * 0.9f * dt;                                 // lean into the first hit
                pos.x = Mathf.Clamp(pos.x, bounds.x, bounds.y);
                transform.position = pos;
            }
            ApplyFacing(false);
            Pose(dt, phase);
        }

        void ApplyFacing(bool snap)
        {
            if (body == null) return;
            var want = Quaternion.AngleAxis(_face * cameraYaw, Vector3.up) * Quaternion.LookRotation(new Vector3(_face, 0f, 0f));
            body.rotation = snap ? want : Quaternion.RotateTowards(body.rotation, want, 720f * Time.deltaTime);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static float Loop(BakedClip c) => c == null ? 0f : (float)(Time.timeAsDouble / Mathf.Max(0.1f, c.length));

        int PatternIndex => Plan.IsValid ? patterns.IndexOf(Plan.Pattern) : -1;

        void Pose(float dt, AttackPhase phase)
        {
            _staggerT += dt;
            if (_pose == null) { PrimitivePose(dt, phase); return; }

            _pose.Reset();
            var beat = conductor != null && conductor.IsPlaying ? conductor.CurrentBeat : default;
            bool stepping = beat.BeatsPerMeasure > 0 && beat.IsDownbeat && !(phase == AttackPhase.Telegraph || phase == AttackPhase.Active) && Aggro;
            var stance = stepping && stepClip != null ? stepClip : idleClip;
            _pose.Layer(stance, Loop(stance), 1f, true);
            // Sway into beats 2 and 3 with the taunt's first frames (a flourish that reads as "and two, three").
            if (tauntClip != null && Aggro && !stepping && phase != AttackPhase.Active)
                _pose.Layer(tauntClip, 0.15f + 0.1f * BeatPhase, 0.25f * Mathf.Exp(-BeatPhase * 3f));

            int p = PatternIndex;
            if (p >= 0 && p < patternClips.Count && !IsStaggered)
            {
                var hits = patternClips[p].hits;
                int n = Plan.HitCount;
                for (int i = 0; i < n && i < hits.Count; i++)
                {
                    var hc = hits[i];
                    if (hc == null || hc.clip == null) continue;
                    float tau = SinceHit(i);
                    float post = i == n - 1 ? 1.3f : 0.55f;
                    float pre = i == 0 ? 1f : 0.6f;
                    float u = ClipTiming.Warp(tau, -pre, post, 0f, hc.hit, 1f);
                    float wIn = i == 0 ? Smooth((tau + pre + 0.5f) / 0.5f) : Smooth((tau + pre) / 0.15f);
                    float wOut = i == n - 1 ? 1f - Smooth((tau - post + 0.5f) / 0.5f) : 1f - Smooth((tau - post + 0.1f) / 0.1f);
                    float w = Mathf.Min(wIn, wOut);
                    if (i == 0 && tau < -pre) { u = 0f; w = Smooth(Windup * 1.2f) * 0.85f; }
                    _pose.Layer(hc.clip, u, w);
                }
            }
            if (staggerClip != null && IsStaggered) _pose.Layer(staggerClip, Mathf.Clamp01(_staggerT / 0.7f), 1f);
            _pose.Apply();
        }

        /// <summary>Placeholder look (no art): a bob on each beat, a lean through the count-in, a spin on each hit.</summary>
        void PrimitivePose(float dt, AttackPhase phase)
        {
            if (body == null) return;
            float lean = 0f;
            if (phase == AttackPhase.Telegraph) lean = -12f * Smooth(Windup);
            float spinTarget = 0f;
            if (phase == AttackPhase.Active || phase == AttackPhase.Recover)
                for (int i = 0; i < Plan.HitCount; i++)
                {
                    float s = SinceHit(i);
                    if (s > -0.15f && s < 0.35f) spinTarget = 360f * Smooth((s + 0.15f) / 0.5f);
                }
            _spin = spinTarget > 0f ? spinTarget : Mathf.MoveTowards(_spin, 0f, 720f * dt);
            if (body.childCount > 0)
            {
                var t = body.GetChild(0);
                t.localRotation = Quaternion.Euler(lean, _spin, 0f);
                t.localPosition = new Vector3(0f, -0.06f * Mathf.Exp(-BeatPhase * 5f), 0f);
            }
        }

        protected override bool InHitRange()
        {
            if (Player == null) return false;
            float ahead = (Player.position.x - transform.position.x) * _face;
            bool spin = Plan.IsValid && Plan.Pattern != null && Plan.Pattern.Name == "Turn";
            return (spin ? Mathf.Abs(ahead) < hitReach : ahead > -0.3f && ahead < hitReach) &&
                   Mathf.Abs(Player.position.y - transform.position.y) < hitHeight;
        }

        protected override Vector3 HitOrigin => transform.position + new Vector3(_face * 0.6f, 1.1f, 0f);

        protected override float DamageScale(HitData hit)
        {
            if (IsStaggered || Phase == AttackPhase.Recover) return 1f;
            return guardShare;
        }

        protected override void OnStaggered()
        {
            _staggerT = 0f;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x - _face * 0.6f, bounds.x, bounds.y);
            transform.position = p;
        }

        protected override void OnReset()
        {
            _face = -1;
            _stepBar = long.MinValue;
            ApplyFacing(true);
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
                yield return new WaitForSeconds(1.2f);
            }
            else
            {
                var start = body != null ? body.localRotation : Quaternion.identity;
                while (t < 1f)
                {
                    t += Time.deltaTime / 0.5f;
                    if (body != null) body.localRotation = start * Quaternion.Euler(0f, 0f, -85f * Smooth(t) * _face);
                    yield return null;
                }
                yield return new WaitForSeconds(1f);
            }
            var p0 = transform.position;
            for (t = 0f; t < 1f; t += Time.deltaTime / 0.8f) { transform.position = p0 + Vector3.down * 1.2f * t; yield return null; }
            gameObject.SetActive(false);
        }
    }
}
