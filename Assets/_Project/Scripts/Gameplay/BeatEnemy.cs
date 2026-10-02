using System;
using System.Collections;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// An enemy that fights on the beat (GDD M2). Attacks are planned on the tempo map from its
    /// <see cref="EnemyDefinition.pattern"/>, telegraphed for one full measure (colour ramp, swell and a
    /// sound cue), then land on their beat. Each landed attack is resolved once its parry window has
    /// closed: out of range, parried with Guard, dodged with an on-beat Step, or a hit.
    /// </summary>
    public class BeatEnemy : MonoBehaviour, IBeatDamageable
    {
        struct Landed
        {
            public ScheduledAttack Attack;
            public bool InRange;
            public bool StepIFrames;
            public bool Grace;          // player still in post-hit invulnerability: no effect, not counted
            public double ResolveAt;
        }

        static readonly Color WarnColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color StaggerColor = new Color(0.55f, 0.55f, 0.6f);

        EnemyDefinition _def;
        Conductor _conductor;
        Transform _player;
        BeatCombatController _combat;
        PlayerHealth _health;
        PlayerMotor _motor;
        CombatFeedback _fx;
        ArenaDirector _arena;
        SpriteRenderer _body;
        Transform _hpBar;
        Vector3 _baseScale, _hpBarScale;

        readonly List<ScheduledAttack> _upcoming = new List<ScheduledAttack>();
        readonly List<Landed> _landed = new List<Landed>();
        double _lastPlannedBeat = double.NaN;
        double _lastCuedBeat = double.NegativeInfinity;
        double _resumeAt = double.NegativeInfinity;   // stagger: no attacks planned before this
        double _guardRestoreAt = double.NegativeInfinity;
        double _lungeUntil;
        bool _guardUp;
        bool _flashing;
        Vector2 _knockback;
        float _orbitAngle;

        public EnemyDefinition Definition => _def;
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsStaggered => _conductor != null && _conductor.SongTime < _resumeAt;
        public bool GuardUp => _def != null && _def.guarded && _guardUp;

        public event Action<BeatEnemy> Died;
        public event Action<BeatEnemy, DefenseResult> AttackResolved;
        public event Action<BeatEnemy> GuardBlocked;
        public event Action<BeatEnemy> GuardBroken;

        public void Init(EnemyDefinition def, Conductor conductor, Transform player, BeatCombatController combat,
                         PlayerHealth health, CombatFeedback fx, ArenaDirector arena, float hpMultiplier,
                         SpriteRenderer body, Transform hpBar)
        {
            _def = def; _conductor = conductor; _player = player; _combat = combat; _health = health; _fx = fx; _arena = arena;
            _motor = player != null ? player.GetComponent<PlayerMotor>() : null;
            _body = body; _hpBar = hpBar;
            _baseScale = transform.localScale;
            _hpBarScale = hpBar != null ? hpBar.localScale : Vector3.one;
            MaxHp = Hp = def.maxHp * hpMultiplier;
            _guardUp = def.guarded;
            _orbitAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            if (IsDead || _def == null || _conductor == null || !_conductor.IsPlaying) return;
            double now = _conductor.SongTime;

            if (_def.guarded && !_guardUp && now >= _guardRestoreAt) _guardUp = true;

            Plan(now);
            CueTelegraphs(now);
            Land(now);
            Resolve(now);
            Move(now, Time.deltaTime);
            UpdateVisuals(now);
        }

        // ---------- Rhythm ----------

        void Plan(double now)
        {
            if (now < _resumeAt) return;
            var map = _conductor.Map;

            if (double.IsNaN(_lastPlannedBeat))
            {
                var first = EnemyRhythm.FirstFullyTelegraphed(map, _def.pattern, now);
                if (first == null) return;
                _upcoming.Add(first.Value);
                _lastPlannedBeat = first.Value.BeatPosition;
            }

            // Keep planning until the next attack's telegraph is still in the future (handles bursts).
            for (int i = 0; i < 8 && (_upcoming.Count == 0 || _upcoming[_upcoming.Count - 1].TelegraphStart <= now); i++)
            {
                var next = EnemyRhythm.Next(map, _def.pattern, _lastPlannedBeat);
                if (next == null) break;
                _upcoming.Add(next.Value);
                _lastPlannedBeat = next.Value.BeatPosition;
            }
        }

        void CueTelegraphs(double now)
        {
            foreach (var a in _upcoming)
            {
                if (a.BeatPosition <= _lastCuedBeat || now < a.TelegraphStart) continue;
                _lastCuedBeat = a.BeatPosition;
                if (_fx != null) _fx.PlayTelegraph(_def.heavy);
            }
        }

        void Land(double now)
        {
            while (_upcoming.Count > 0 && now >= _upcoming[0].Time)
            {
                var a = _upcoming[0];
                _upcoming.RemoveAt(0);

                float dist = _player != null ? Vector2.Distance(transform.position, _player.position) : float.MaxValue;
                bool stepIFrames = _motor != null && _motor.Invulnerable;
                bool grace = _health != null && _health.Invulnerable && !stepIFrames;
                _landed.Add(new Landed
                {
                    Attack = a,
                    InRange = dist <= _def.attackRange + 0.35f, // + player radius
                    StepIFrames = stepIFrames,
                    Grace = grace,
                    ResolveAt = a.Time + ParryJudge.Window(_combat.Windows, _def.heavy),
                });
                _lungeUntil = now + 0.12;
            }
        }

        void Resolve(double now)
        {
            for (int i = _landed.Count - 1; i >= 0; i--)
            {
                var l = _landed[i];
                if (now < l.ResolveAt) continue;
                _landed.RemoveAt(i);

                if (l.Grace || (_health != null && _health.IsDown)) continue;

                var result = ParryJudge.Resolve(l.InRange, l.StepIFrames, _combat.LastGuardPressTime,
                                                l.Attack.Time, _combat.Windows, _def.heavy);
                switch (result)
                {
                    case DefenseResult.Parried:
                        _combat.RegisterParry(_def.displayName);
                        if (_fx != null) _fx.PlayParry();
                        Stagger(1);
                        break;
                    case DefenseResult.Hit:
                        if (_health != null) _health.TakeHit(_def.damage);
                        if (_fx != null) _fx.PlayHurt();
                        break;
                }
                AttackResolved?.Invoke(this, result);
            }
        }

        /// <summary>Cancel planned attacks and stay quiet for some measures.</summary>
        public void Stagger(int measures)
        {
            var beat = _conductor.CurrentBeat;
            double measureSeconds = beat.BeatsPerMeasure * beat.SecondsPerBeat;
            _resumeAt = Math.Max(_resumeAt, _conductor.SongTime + measureSeconds * measures);
            _upcoming.Clear();
            _lastPlannedBeat = double.NaN;
        }

        // ---------- Movement ----------

        void Move(double now, float dt)
        {
            if (_player == null) return;
            Vector2 pos = transform.position;
            Vector2 p = _player.position;
            Vector2 to = p - pos;
            float dist = to.magnitude;
            Vector2 dir = dist > 1e-4f ? to / dist : Vector2.right;
            double prog = _upcoming.Count > 0 ? _upcoming[0].Progress(now) : 0;
            float speed = _def.moveSpeed;
            Vector2 move = Vector2.zero;

            if (!IsStaggered)
            {
                switch (_def.movement)
                {
                    case EnemyMovement.Hopper:
                    {
                        // Hops forward right after each beat, then settles.
                        float hop = Mathf.Exp(-_conductor.BeatPhase * 5f) * 2f;
                        float stop = _def.attackRange * 0.7f;
                        if (dist > stop) move = dir * speed * hop;
                        break;
                    }
                    case EnemyMovement.Skirmisher:
                    {
                        float want = prog > 0.7 ? _def.attackRange * 0.7f : _def.preferredDistance;
                        float k = prog > 0.7 ? 2.5f : 1f;
                        move = dir * Mathf.Clamp(dist - want, -1f, 1f) * speed * k;
                        break;
                    }
                    case EnemyMovement.Juggernaut:
                    {
                        float stop = _def.attackRange * 0.8f;
                        if (dist > stop) move = dir * speed;
                        break;
                    }
                    case EnemyMovement.Flyer:
                    {
                        _orbitAngle += dt * 1.2f;
                        Vector2 target = prog > 0.75
                            ? p - dir * 0.3f // swoop in for the peck
                            : p + new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle)) * _def.preferredDistance;
                        Vector2 d = target - pos;
                        float k = prog > 0.75 ? 4f : 1.5f;
                        move = Vector2.ClampMagnitude(d * 3f, speed * k);
                        break;
                    }
                }
            }

            // Keep a little space between enemies.
            if (_arena != null)
            {
                foreach (var other in _arena.Enemies)
                {
                    if (other == null || other == this || other.IsDead) continue;
                    Vector2 away = pos - (Vector2)other.transform.position;
                    float d = away.magnitude;
                    if (d > 0.001f && d < 0.9f) move += away / d * (0.9f - d) * 4f;
                }
            }

            _knockback = Vector2.MoveTowards(_knockback, Vector2.zero, 20f * dt);
            pos += (move + _knockback) * dt;
            if (_arena != null)
            {
                var b = _arena.Bounds;
                pos.x = Mathf.Clamp(pos.x, b.xMin + 0.3f, b.xMax - 0.3f);
                pos.y = Mathf.Clamp(pos.y, b.yMin + 0.3f, b.yMax - 0.3f);
            }
            transform.position = pos;
        }

        // ---------- Visuals ----------

        void UpdateVisuals(double now)
        {
            if (_body == null || _flashing) return;

            Color baseColor = _def.color;
            if (GuardUp) baseColor = Color.Lerp(baseColor, Color.black, 0.25f);

            if (IsStaggered) { _body.color = StaggerColor; transform.localScale = _baseScale; return; }

            float prog = _upcoming.Count > 0 ? (float)_upcoming[0].Progress(now) : 0f;
            var c = Color.Lerp(baseColor, WarnColor, prog * prog);
            if (prog > 0.85f && Mathf.FloorToInt(Time.unscaledTime * 20f) % 2 == 0) c = Color.white; // last moment flash
            _body.color = c;

            float swell = 1f + 0.15f * prog;
            if (now < _lungeUntil) swell = 1.3f;
            transform.localScale = _baseScale * swell;
        }

        // ---------- Taking damage ----------

        public void TakeHit(HitData hit)
        {
            if (IsDead) return;
            float dmg = hit.Damage;
            bool breaker = hit.IsFinisher || hit.EffectId == "guard_break";

            if (GuardUp)
            {
                if (breaker)
                {
                    _guardUp = false;
                    var beat = _conductor.CurrentBeat;
                    _guardRestoreAt = _conductor.SongTime + 2 * beat.BeatsPerMeasure * beat.SecondsPerBeat;
                    Stagger(1);
                    GuardBroken?.Invoke(this);
                }
                else
                {
                    dmg *= 0.15f;
                    GuardBlocked?.Invoke(this);
                }
            }

            if (hit.EffectId == "stagger") Stagger(1);

            Hp -= dmg;
            _knockback += hit.Direction * (hit.IsFinisher ? 7f : 2.5f);
            if (_hpBar != null)
            {
                var s = _hpBarScale;
                s.x *= Mathf.Clamp01(Hp / MaxHp);
                _hpBar.localScale = s;
            }

            if (Hp <= 0f) { Die(); return; }
            StartCoroutine(FlashRoutine());
        }

        void Die()
        {
            IsDead = true;
            _upcoming.Clear();
            _landed.Clear();
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            Died?.Invoke(this);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator FlashRoutine()
        {
            _flashing = true;
            if (_body != null) _body.color = Color.white;
            yield return new WaitForSecondsRealtime(0.06f);
            _flashing = false;
        }

        IEnumerator DeathRoutine()
        {
            float t = 0f;
            var start = transform.localScale;
            var c = _body != null ? _body.color : Color.white;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - t / 0.25f;
                transform.localScale = start * (1f + (1f - k) * 0.5f);
                if (_body != null) { c.a = k; _body.color = c; }
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
