using System.Collections;
using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// VS-6's drifting enemy for the Drowned Nave's 6/8: a glowing jellyfish bell that pulses on the two big beats of every
    /// bar (one and four), so its body counts the lilt. It hangs above the player's head, out of easy reach. Its attacks
    /// are telegraphed for one bar: it rises and swells over the player, then drops to sting and springs back up. The Toll
    /// stings once on the downbeat; the Double Toll stings on one and four. After its last sting it sinks low and pulses
    /// slowly while it recovers: the time to cut it down. High up, blades only graze it (half damage).
    /// </summary>
    public class BellJelly : BeatEnemy
    {
        [SerializeField] Transform bell;
        [SerializeField] Transform tentacleRoot;
        [SerializeField] LineRenderer[] tentacles = new LineRenderer[0];
        [Tooltip("Height of the bell above the player's chest while it waits.")]
        [SerializeField] float hover = 2.6f;
        [SerializeField] float driftSpeed = 1.8f;
        [SerializeField] float hitRadius = 1.35f;
        [Tooltip("Beats (eighths) the drop to a sting takes.")]
        [SerializeField] float diveBeats = 0.8f;
        [SerializeField] Vector2 bounds = new Vector2(-1000f, 1000f);
        [SerializeField] float tentacleLength = 1.3f;

        Vector3 _knock;
        float _sink;

        public static List<EnemyAttackPattern> DefaultPatterns() => DrownedPatterns.Jelly();

        protected override void Awake()
        {
            if (patterns.Count == 0) patterns.AddRange(DefaultPatterns());
            base.Awake();
        }

        bool Low => IsStaggered || Phase == AttackPhase.Active || Phase == AttackPhase.Recover;

        Vector3 Chest => Player != null ? Player.position + Vector3.up * 1.15f : transform.position;

        protected override void Tick(bool live)
        {
            float dt = Time.deltaTime;
            double b = BeatNow;
            var phase = Phase;
            // The lilt: a contraction on each big beat (every three eighths), springing open again.
            float pulse = Mathf.Repeat((float)(b / 3.0), 1f);
            float squeeze = Mathf.Exp(-pulse * 5f);

            var pos = transform.position;
            var desired = pos;
            float follow = 3f;
            if (live && Player != null && Aggro && !IsStaggered)
            {
                float targetX = Mathf.Clamp(Player.position.x, bounds.x, bounds.y);
                var high = new Vector3(Mathf.MoveTowards(pos.x, targetX, driftSpeed * dt), Chest.y + hover, Home.z);
                desired = high + Vector3.up * (0.25f * squeeze);
                if (phase == AttackPhase.Telegraph)
                {
                    desired = new Vector3(Mathf.MoveTowards(pos.x, targetX, driftSpeed * 1.6f * dt), Chest.y + hover + 0.5f * Smooth(Windup), Home.z);
                    float toHit = -SinceHit(0);
                    if (toHit <= diveBeats) desired = Dive(high, toHit);
                    follow = toHit <= diveBeats ? 40f : 6f;
                }
                else if (phase == AttackPhase.Active || phase == AttackPhase.Recover)
                {
                    // Between stings it springs back up; after the last it sinks low and pulses slowly (open to blades).
                    int last = Plan.HitCount - 1;
                    int next = -1;
                    for (int i = 0; i < Plan.HitCount; i++) if (SinceHit(i) < 0f) { next = i; break; }
                    if (next >= 0)
                    {
                        float toHit = -SinceHit(next);
                        float since = SinceHit(next - 1);
                        desired = toHit <= diveBeats ? Dive(high, toHit) : Vector3.Lerp(Sting, high, Smooth(since / 1.2f));
                        follow = 30f;
                    }
                    else
                    {
                        float since = SinceHit(last);
                        _sink = Mathf.MoveTowards(_sink, 1f, dt);
                        desired = new Vector3(pos.x, Chest.y + 0.5f - 0.3f * _sink, Home.z);
                        if (since > Plan.Pattern.RecoverBeats - 2f) desired = high;
                        follow = since < 0.4f ? 20f : 2.5f;
                    }
                }
            }
            else if (live && !Aggro)
            {
                desired = Home + new Vector3(Mathf.Sin((float)(b * Mathf.PI / 12.0)) * 0.8f, 0.25f * squeeze, 0f);
            }
            if (phase != AttackPhase.Active && phase != AttackPhase.Recover) _sink = 0f;
            if (IsStaggered) desired = pos + _knock * dt;
            _knock = Vector3.Lerp(_knock, Vector3.zero, 1f - Mathf.Exp(-4f * dt));
            desired.x = Mathf.Clamp(desired.x, bounds.x, bounds.y);
            transform.position = Vector3.Lerp(pos, desired, 1f - Mathf.Exp(-follow * dt));

            if (bell != null)
            {
                float slow = Phase == AttackPhase.Recover ? 0.5f : 1f;
                bell.localScale = new Vector3(1f + 0.12f * squeeze * slow, 1f - 0.16f * squeeze * slow, 1f + 0.12f * squeeze * slow);
            }
            UpdateTentacles(squeeze);
        }

        Vector3 _stingAt;
        Vector3 Sting => _stingAt == Vector3.zero ? Chest : _stingAt;

        /// <summary>Dropping to the sting: from high over the player to their chest as the hit lands.</summary>
        Vector3 Dive(Vector3 high, float toHit)
        {
            if (toHit > 0.15f) _stingAt = Chest + Vector3.up * 0.2f;
            float k = 1f - Mathf.Clamp01(toHit / diveBeats);
            return Vector3.Lerp(high, Sting, k * k);
        }

        void UpdateTentacles(float squeeze)
        {
            if (tentacles == null) return;
            float t = Time.time;
            var root = tentacleRoot != null ? tentacleRoot.position : transform.position;
            var vel = (transform.position - _lastPos) / Mathf.Max(Time.deltaTime, 1e-4f);
            _lastPos = transform.position;
            for (int i = 0; i < tentacles.Length; i++)
            {
                var lr = tentacles[i];
                if (lr == null) continue;
                int n = lr.positionCount;
                float a = i / (float)tentacles.Length * Mathf.PI * 2f;
                var start = root + new Vector3(Mathf.Cos(a) * 0.38f, 0f, Mathf.Sin(a) * 0.38f);
                for (int k = 0; k < n; k++)
                {
                    float u = k / (float)(n - 1);
                    float sway = Mathf.Sin(t * 2.2f + i * 1.3f + u * 3f) * 0.18f * u;
                    var trail = -vel * 0.06f * u * u;
                    var p = start + new Vector3(sway, -u * tentacleLength * (1f + 0.15f * squeeze), Mathf.Cos(t * 1.7f + i) * 0.08f * u) + trail;
                    lr.SetPosition(k, p);
                }
            }
        }

        Vector3 _lastPos;

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        protected override bool InHitRange() => Player != null && Vector3.Distance(transform.position, Chest) < hitRadius;

        protected override Vector3 HitOrigin => transform.position;

        protected override float DamageScale(HitData hit) => Low ? 1f : 0.5f;

        protected override void Shove(float dx)
        {
            var p = transform.position;
            p.x = Mathf.Clamp(p.x + dx, bounds.x, bounds.y);
            transform.position = p;
        }

        protected override void OnStaggered()
        {
            if (Player == null) return;
            var d = transform.position - Chest;
            d.z = 0f;
            _knock = (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.up) * 4f + Vector3.up * 2f;
        }

        protected override void OnReset() { _knock = Vector3.zero; _sink = 0f; _stingAt = Vector3.zero; }

        // The aura reads in front of a glowing bell, like the wisp's.
        protected override bool AuraInFront => true;
        protected override float AuraSize => 2.0f;
        protected override Vector3 AuraCenter => bell != null ? bell.position : transform.position;

        protected override void OnDied() => StartCoroutine(Fade());

        IEnumerator Fade()
        {
            var s = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
            {
                transform.localScale = new Vector3(s.x * (1f + 0.4f * t), s.y * (1f - t), s.z * (1f + 0.4f * t));
                transform.position += Vector3.down * 0.6f * Time.deltaTime;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
