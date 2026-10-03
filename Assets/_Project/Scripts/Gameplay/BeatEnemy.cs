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

        static readonly Color WarnGlow = new Color(1f, 0.12f, 0.08f) * 2.2f;   // HDR: bloom picks it up
        static readonly Color StaggerTint = new Color(0.55f, 0.55f, 0.62f);
        static readonly Color GuardTint = new Color(0.72f, 0.72f, 0.82f);

        EnemyDefinition _def;

        // Current fighting profile: starts from the definition, a boss swaps it per phase.
        EnemyPattern _pattern;
        float _damage, _attackRange;
        bool _heavy, _guarded;
        string _guardBreakEffect = "guard_break";
        Color _color;
        string _name;
        Conductor _conductor;
        Transform _player;
        BeatCombatController _combat;
        PlayerHealth _health;
        PlayerMotor _motor;
        CombatFeedback _fx;
        ArenaDirector _arena;
        ToonTint _tint;
        Transform _model;
        Transform _hpBar;
        Vector3 _modelScale, _hpBarScale;

        readonly List<ScheduledAttack> _upcoming = new List<ScheduledAttack>();
        readonly List<Landed> _landed = new List<Landed>();
        double _lastPlannedBeat = double.NaN;
        double _lastCuedBeat = double.NegativeInfinity;
        double _resumeAt = double.NegativeInfinity;   // stagger: no attacks planned before this
        double _guardRestoreAt = double.NegativeInfinity;
        double _lungeUntil;
        bool _guardUp;
        Vector2 _knockback;
        float _orbitAngle;

        public EnemyDefinition Definition => _def;
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsStaggered => _conductor != null && _conductor.SongTime < _resumeAt;
        public bool GuardUp => _def != null && _guarded && _guardUp;
        public string DisplayName => _name;
        public float AttackRange => _attackRange;
        /// <summary>HP can't drop below this except from a Bar Finisher (boss phase gates, finisher-only kills).</summary>
        public float HpFloor { get; set; }

        public event Action<BeatEnemy> Died;
        public event Action<BeatEnemy, DefenseResult> AttackResolved;
        public event Action<BeatEnemy> GuardBlocked;
        public event Action<BeatEnemy> GuardBroken;

        public void Init(EnemyDefinition def, Conductor conductor, Transform player, BeatCombatController combat,
                         PlayerHealth health, CombatFeedback fx, ArenaDirector arena, float hpMultiplier,
                         Transform model, Transform hpBar)
        {
            _def = def; _conductor = conductor; _player = player; _combat = combat; _health = health; _fx = fx; _arena = arena;
            _motor = player != null ? player.GetComponent<PlayerMotor>() : null;
            _model = model; _hpBar = hpBar;
            _tint = GetComponent<ToonTint>();
            if (_tint == null) _tint = gameObject.AddComponent<ToonTint>();
            _tint.Refresh();
            _modelScale = model != null ? model.localScale : Vector3.one;
            _hpBarScale = hpBar != null ? hpBar.localScale : Vector3.one;
            MaxHp = Hp = def.maxHp * hpMultiplier;
            _name = def.displayName;
            ApplyProfile(def.pattern, def.damage, def.attackRange, def.heavy, def.guarded, "guard_break", def.color);
            _orbitAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        /// <summary>Swap how this enemy fights (boss phases). Re-plans attacks from the next full telegraph.</summary>
        public void ApplyProfile(EnemyPattern pattern, float damage, float attackRange, bool heavy, bool guarded,
                                 string guardBreakEffect, Color color)
        {
            _pattern = pattern;
            _damage = damage;
            _attackRange = attackRange;
            _heavy = heavy;
            _guarded = guarded;
            _guardUp = guarded;
            _guardBreakEffect = string.IsNullOrEmpty(guardBreakEffect) ? "guard_break" : guardBreakEffect;
            _color = color;
            _upcoming.Clear();
            _lastPlannedBeat = double.NaN;
        }

        public void SetDisplayName(string name) => _name = name;

        void Update()
        {
            if (IsDead || _def == null || _conductor == null || !_conductor.IsPlaying) return;
            double now = _conductor.SongTime;

            if (_guarded && !_guardUp && now >= _guardRestoreAt) _guardUp = true;

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
                var first = EnemyRhythm.FirstFullyTelegraphed(map, _pattern, now);
                if (first == null) return;
                _upcoming.Add(first.Value);
                _lastPlannedBeat = first.Value.BeatPosition;
            }

            // Keep planning until the next attack's telegraph is still in the future (handles bursts).
            for (int i = 0; i < 8 && (_upcoming.Count == 0 || _upcoming[_upcoming.Count - 1].TelegraphStart <= now); i++)
            {
                var next = EnemyRhythm.Next(map, _pattern, _lastPlannedBeat);
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
                if (_fx != null) _fx.PlayTelegraph(_heavy);
            }
        }

        void Land(double now)
        {
            while (_upcoming.Count > 0 && now >= _upcoming[0].Time)
            {
                var a = _upcoming[0];
                _upcoming.RemoveAt(0);

                float dist = _player != null ? Ground.Distance(transform.position, _player.position) : float.MaxValue;
                bool stepIFrames = _motor != null && _motor.Invulnerable;
                bool grace = _health != null && _health.Invulnerable && !stepIFrames;
                _landed.Add(new Landed
                {
                    Attack = a,
                    InRange = dist <= _attackRange + 0.35f, // + player radius
                    StepIFrames = stepIFrames,
                    Grace = grace,
                    ResolveAt = a.Time + ParryJudge.Window(_combat.Windows, _heavy),
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
                                                l.Attack.Time, _combat.Windows, _heavy);
                switch (result)
                {
                    case DefenseResult.Parried:
                        _combat.RegisterParry(_name);
                        if (_fx != null) _fx.PlayParry();
                        Stagger(1);
                        break;
                    case DefenseResult.Hit:
                        if (_health != null) _health.TakeHit(_damage);
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
            Vector2 pos = Ground.ToPlane(transform.position);
            Vector2 p = Ground.ToPlane(_player.position);
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
                        float stop = _attackRange * 0.7f;
                        if (dist > stop) move = dir * speed * hop;
                        break;
                    }
                    case EnemyMovement.Skirmisher:
                    {
                        float want = prog > 0.7 ? _attackRange * 0.7f : _def.preferredDistance;
                        float k = prog > 0.7 ? 2.5f : 1f;
                        move = dir * Mathf.Clamp(dist - want, -1f, 1f) * speed * k;
                        break;
                    }
                    case EnemyMovement.Juggernaut:
                    {
                        float stop = _attackRange * 0.8f;
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
                    Vector2 away = pos - Ground.ToPlane(other.transform.position);
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
            transform.position = Ground.ToWorld(pos, transform.position.y);
            if (_model != null && dist > 0.05f) _model.rotation = Ground.TurnToward(_model.rotation, dir, 540f, dt);
        }

        // ---------- Visuals ----------

        void UpdateVisuals(double now)
        {
            if (_tint == null) return;

            // Boss phases recolour by tinting the model; regular enemies keep their own colours.
            Color tint = _color == _def.color ? Color.white : Color.Lerp(Color.white, _color, 0.45f);
            if (GuardUp) tint *= GuardTint;
            if (IsStaggered)
            {
                _tint.SetTint(StaggerTint);
                _tint.SetGlow(Color.black);
                if (_model != null) _model.localScale = _modelScale;
                return;
            }
            _tint.SetTint(tint);

            // Telegraph: the enemy glows red more and more over the warning measure, blinking at the end.
            float prog = _upcoming.Count > 0 ? (float)_upcoming[0].Progress(now) : 0f;
            var glow = WarnGlow * (prog * prog);
            if (prog > 0.85f && Mathf.FloorToInt(Time.unscaledTime * 20f) % 2 == 0) glow = Color.white * 1.5f;
            _tint.SetGlow(glow);

            if (_model != null)
            {
                float swell = 1f + 0.12f * prog;
                if (now < _lungeUntil) swell = 1.25f;
                // Squash and stretch: taller as it winds up.
                _model.localScale = new Vector3(_modelScale.x * (2f - swell), _modelScale.y * swell, _modelScale.z * (2f - swell));
            }
        }

        // ---------- Taking damage ----------

        public void TakeHit(HitData hit)
        {
            if (IsDead) return;
            float dmg = hit.Damage;
            bool breaker = hit.IsFinisher || hit.EffectId == _guardBreakEffect;

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

            Hp = hit.IsFinisher ? Hp - dmg : Mathf.Max(Hp - dmg, Mathf.Min(Hp, HpFloor));
            _knockback += hit.Direction * (hit.IsFinisher ? 7f : 2.5f);
            if (_tint != null) _tint.Flash(Color.white, 0.12f);
            if (_hpBar != null)
            {
                float k = Mathf.Clamp01(Hp / MaxHp);
                var s = _hpBarScale;
                s.x *= k;
                _hpBar.localScale = s;
                var lp = _hpBar.localPosition;
                lp.x = -(1f - k) * _hpBarScale.x * 0.5f; // drain from the right
                _hpBar.localPosition = lp;
            }

            if (Hp <= 0f) Die();
        }

        void Die()
        {
            IsDead = true;
            _upcoming.Clear();
            _landed.Clear();
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Died?.Invoke(this);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            if (_tint != null) { _tint.SetGlow(Color.white * 2f); _tint.SetFlash(Color.white, 1f); }
            if (_hpBar != null) _hpBar.parent.gameObject.SetActive(false);
            float t = 0f;
            var start = _model != null ? _model.localScale : Vector3.one;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - t / 0.25f;
                if (_model != null) _model.localScale = new Vector3(start.x * (1f + (1f - k) * 0.6f), start.y * k, start.z * (1f + (1f - k) * 0.6f));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
