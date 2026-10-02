using System;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Drives a boss through its phases (GDD M3). When HP drops to the next phase's threshold, HP is held
    /// there and the Conductor is asked to move the music to the next song phase; the boss changes how
    /// it fights only when that phase is actually heard, so its new pattern always matches the new meter.
    /// The final phase can only be ended by a Bar Finisher.
    /// </summary>
    [RequireComponent(typeof(BeatEnemy))]
    public class BossController : MonoBehaviour
    {
        BossDefinition _boss;
        BeatEnemy _enemy;
        Conductor _conductor;
        ArenaDirector _arena;
        int _phase = -1;
        bool _requested;
        bool _finisherHintShown;

        public int Phase => _phase;
        public BossDefinition Definition => _boss;
        public BeatEnemy Enemy => _enemy;
        public bool TransitionPending => _requested;
        public string PhaseName => _boss != null && _phase >= 0 && _phase < _boss.phases.Count ? _boss.phases[_phase].name : null;

        /// <summary>Phase index, phase.</summary>
        public event Action<int, BossPhase> PhaseStarted;
        /// <summary>HP gate reached; the music will move at the next loop boundary.</summary>
        public event Action<int> PhaseRequested;
        /// <summary>Final phase, HP at its floor: only a Bar Finisher can end it now.</summary>
        public event Action FinisherNeeded;

        public void Init(BossDefinition boss, Conductor conductor, ArenaDirector arena)
        {
            _boss = boss;
            _conductor = conductor;
            _arena = arena;
            _enemy = GetComponent<BeatEnemy>();
            _enemy.SetDisplayName(boss.displayName);
            if (_conductor != null)
            {
                _conductor.PhaseChanged += OnMusicPhase;
                if (_conductor.IsAdaptive && _conductor.CurrentPhase != 0) _conductor.RequestPhase(0);
            }
            EnterPhase(0);
        }

        void OnDestroy()
        {
            if (_conductor != null) _conductor.PhaseChanged -= OnMusicPhase;
        }

        void Update()
        {
            if (_boss == null || _enemy == null || _enemy.IsDead) return;

            int next = _phase + 1;
            if (!_requested && next < _boss.phases.Count && _enemy.Hp <= _boss.phases[next].enterAtHpFraction * _enemy.MaxHp + 0.01f)
            {
                _requested = true;
                if (_conductor != null && _conductor.IsAdaptive && next < _conductor.PhaseCount)
                {
                    _conductor.RequestPhase(next);
                    PhaseRequested?.Invoke(next);
                }
                else
                {
                    EnterPhase(next); // no adaptive music: switch at once
                }
            }

            var cur = _boss.phases[_phase];
            if (cur.finisherOnly && !_finisherHintShown && _enemy.Hp <= _enemy.HpFloor + 0.01f)
            {
                _finisherHintShown = true;
                FinisherNeeded?.Invoke();
            }
        }

        void OnMusicPhase(int musicPhase, BeatInfo beat)
        {
            if (_enemy == null || _enemy.IsDead) return;
            // Only follow the music into the phase we asked for: after a retry, a stale queued switch is ignored.
            if (musicPhase != _phase && musicPhase == _conductor.RequestedPhase && musicPhase < _boss.phases.Count)
                EnterPhase(musicPhase);
        }

        void EnterPhase(int index)
        {
            if (index < 0 || index >= _boss.phases.Count) return;
            _phase = index;
            _requested = false;
            _finisherHintShown = false;
            var p = _boss.phases[index];

            _enemy.ApplyProfile(p.pattern, p.damage, p.attackRange, p.heavy, p.guarded, p.guardBreakEffect, p.color);

            // Hold HP at the next phase's gate until the music gets there; the last phase needs a finisher.
            if (index + 1 < _boss.phases.Count) _enemy.HpFloor = _boss.phases[index + 1].enterAtHpFraction * _enemy.MaxHp;
            else _enemy.HpFloor = p.finisherOnly ? 1f : 0f;

            if (_arena != null && p.summons != null)
                foreach (var s in p.summons) if (s != null) _arena.SpawnAdd(s);

            PhaseStarted?.Invoke(index, p);
        }
    }
}
