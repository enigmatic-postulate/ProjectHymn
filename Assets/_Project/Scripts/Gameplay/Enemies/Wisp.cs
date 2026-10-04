using System.Collections;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Slice 2's flying enemy: a wisp hovering over the gap. It drifts in a figure-eight on the beat, then over a bar
    /// it pulls back, glows up and locks on, and dives at the player to land on the downbeat. It is only open to hits
    /// right after a dive (while it recovers) or when a parry staggers it; otherwise blades pass through it.
    /// </summary>
    public class Wisp : BeatEnemy
    {
        [SerializeField] Transform core;
        [SerializeField] Transform orbit;
        [SerializeField] float hitRadius = 1.3f;
        [SerializeField] float driftWidth = 0.9f;
        [SerializeField] float driftHeight = 0.35f;
        [Tooltip("The dive starts this many beats before the hit.")]
        [SerializeField] float diveBeats = 0.35f;

        Vector3 _diveFrom, _target;
        bool _locked, _diving;
        Vector3 _knock;

        protected override void Awake()
        {
            if (patterns.Count == 0) patterns.Add(new EnemyAttackPattern("Dive", new[] { 0.0 }, 4, 2, 1));
            base.Awake();
        }

        bool Vulnerable => IsStaggered || Phase == AttackPhase.Recover || Phase == AttackPhase.Active;

        protected override void Tick(bool live)
        {
            float dt = Time.deltaTime;
            double b = BeatNow;
            var phase = Phase;
            var home = Home + new Vector3(Mathf.Sin((float)(b * Mathf.PI / 4.0)) * driftWidth, Mathf.Sin((float)(b * Mathf.PI / 2.0)) * driftHeight, 0f);
            var desired = home;
            float follow = 6f;

            if (live && Player != null && Plan.IsValid && !IsStaggered)
            {
                var chest = Player.position + Vector3.up * 1.1f;
                if (phase == AttackPhase.Telegraph)
                {
                    float w = Windup;
                    var away = (transform.position - chest); away.z = 0f;
                    away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.up;
                    if (!_locked) _target = chest;
                    if (w > 0.75f) _locked = true;     // last beat: committed to where the player was
                    double hitBeat = Plan.FirstHitBeat;
                    float toHit = (float)(hitBeat - b);
                    if (toHit <= diveBeats)
                    {
                        if (!_diving) { _diving = true; _diveFrom = transform.position; }
                        float k = 1f - Mathf.Clamp01(toHit / diveBeats);
                        transform.position = Vector3.Lerp(_diveFrom, _target, k * k);
                        Spin(dt, 6f);
                        return;
                    }
                    desired = home + away * (0.8f * w) + Vector3.up * (0.6f * w) + Random.insideUnitSphere * (0.06f * w);
                    follow = 10f;
                }
                else if (phase == AttackPhase.Active || phase == AttackPhase.Recover)
                {
                    // Spent: hangs where the dive ended, sinking a little, then drifts home.
                    float s = SinceHit(Plan.HitCount - 1);
                    desired = _target + Vector3.down * 0.25f * Mathf.Clamp01(s);
                    if (s > Plan.Pattern.RecoverBeats - 1f) desired = home;
                    follow = s < 0.3f ? 30f : 3f;
                }
            }
            if (phase != AttackPhase.Telegraph) { _locked = false; _diving = false; }
            if (IsStaggered) desired = transform.position + _knock * dt;
            _knock = Vector3.Lerp(_knock, Vector3.zero, 1f - Mathf.Exp(-4f * dt));
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-follow * dt));
            Spin(dt, Vulnerable ? 0.6f : 1.5f);
        }

        void Spin(float dt, float speed)
        {
            if (orbit != null) orbit.Rotate(0f, 180f * speed * dt, 0f, Space.Self);
            if (core != null)
            {
                float pulse = 1f + 0.12f * Mathf.Exp(-BeatPhase * 5f) + (Vulnerable ? -0.15f : 0f);
                core.localScale = Vector3.one * pulse;
            }
        }

        protected override bool InHitRange()
        {
            if (Player == null) return false;
            return Vector3.Distance(transform.position, Player.position + Vector3.up * 1.0f) < hitRadius;
        }

        protected override Vector3 HitOrigin => transform.position;

        protected override float DamageScale(HitData hit) => Vulnerable ? 1f : 0f;

        protected override void OnStaggered()
        {
            if (Player == null) return;
            var d = transform.position - (Player.position + Vector3.up);
            d.z = 0f;
            _knock = (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.up) * 5f + Vector3.up * 2f;
        }

        protected override void OnReset() { _locked = _diving = false; _knock = Vector3.zero; }

        protected override void OnDied() => StartCoroutine(Burst());

        IEnumerator Burst()
        {
            float t = 0f;
            var s = transform.localScale;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                transform.localScale = s * (1f + t * 0.8f) * (1f - t);
                Spin(Time.deltaTime, 8f);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
