using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    [Serializable]
    public class ArenaWave
    {
        public string name = "Wave";
        public EnemyDefinition[] enemies = new EnemyDefinition[0];
    }

    /// <summary>
    /// Runs one instanced arena (GDD M2 exit: one arena is fun for 2 minutes). Waves spawn on a downbeat
    /// after a short count-in; the next wave comes a measure after the last enemy falls; the list loops
    /// with tougher enemies. When the fight clock runs out the arena is cleared and the score shown.
    /// If the player goes down, the current wave restarts two measures later at full HP.
    /// </summary>
    public class ArenaDirector : MonoBehaviour
    {
        enum State { Waiting, Fighting, Between, Downed, Cleared }

        [SerializeField] Conductor conductor;
        [SerializeField] BeatCombatController combat;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] CombatFeedback feedback;

        [SerializeField] List<ArenaWave> waves = new List<ArenaWave>();
        [Tooltip("Boss arena: when set, the boss is fought instead of the waves, with no time limit.")]
        [SerializeField] BossDefinition boss;
        [SerializeField] Rect bounds = new Rect(-7.5f, -4f, 15f, 8f);
        [SerializeField] float fightLengthSeconds = 120f;
        [SerializeField, Min(0)] int countInMeasures = 2;
        [Tooltip("Enemy HP grows by this much each time the wave list loops.")]
        [SerializeField] float hpGrowthPerLoop = 0.25f;
        [SerializeField] float minSpawnDistance = 4f;

        [Header("Look")]
        [SerializeField] Sprite circleSprite;
        [SerializeField] Sprite squareSprite;
        [SerializeField] Material spriteMaterial;

        State _state = State.Waiting;
        long _actOnMeasure = long.MaxValue;
        int _waveIndex, _loop;
        double _fightSeconds;
        readonly List<BeatEnemy> _enemies = new List<BeatEnemy>();
        BossController _bossCtl;

        public Rect Bounds => bounds;
        public IReadOnlyList<BeatEnemy> Enemies => _enemies;
        public bool IsRunning => _state == State.Fighting || _state == State.Between;
        public bool IsCleared => _state == State.Cleared;
        public int WaveNumber => _waveIndex + 1;
        public double FightSeconds => _fightSeconds;
        public float TimeLeft => Mathf.Max(0f, fightLengthSeconds - (float)_fightSeconds);
        public bool IsBossFight => boss != null;
        public BossController Boss => _bossCtl;

        public event Action<int, string> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action PlayerDowned;
        public event Action<long> ArenaCleared;
        public event Action<BeatEnemy, DefenseResult> EnemyAttackResolved;
        public event Action<BeatEnemy> EnemyKilled;
        public event Action<BeatEnemy> EnemyGuarded;
        public event Action<BeatEnemy> EnemyGuardBroken;
        public event Action<BossController> BossSpawned;
        public event Action<BossController> BossDefeated;

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (combat == null) combat = FindAnyObjectByType<BeatCombatController>();
            if (playerHealth == null && combat != null) playerHealth = combat.GetComponent<PlayerHealth>();
            if (feedback == null) feedback = FindAnyObjectByType<CombatFeedback>();
        }

        void OnEnable()
        {
            if (conductor != null) conductor.MeasureStarted += OnMeasureStarted;
            if (playerHealth != null) playerHealth.Downed += OnPlayerDowned;
        }

        void OnDisable()
        {
            if (conductor != null) conductor.MeasureStarted -= OnMeasureStarted;
            if (playerHealth != null) playerHealth.Downed -= OnPlayerDowned;
        }

        void Update()
        {
            if (_state == State.Cleared || _state == State.Waiting) return;

            if (IsRunning)
            {
                _fightSeconds += Time.deltaTime;
                if (!IsBossFight && _fightSeconds >= fightLengthSeconds) { ClearArena(); return; }
            }

            _enemies.RemoveAll(e => e == null);
            if (IsBossFight)
            {
                if (_state == State.Fighting && _bossCtl != null && _bossCtl.Enemy.IsDead)
                {
                    BossDefeated?.Invoke(_bossCtl);
                    ClearArena();
                }
                return;
            }
            if (_state == State.Fighting && AliveCount() == 0)
            {
                WaveCleared?.Invoke(WaveNumber);
                _waveIndex++;
                if (waves.Count > 0 && _waveIndex % waves.Count == 0) _loop++;
                _state = State.Between;
                _actOnMeasure = conductor.CurrentBeat.Measure + 2; // one empty measure, then the next downbeat
            }
        }

        void OnMeasureStarted(BeatInfo beat)
        {
            if (_state == State.Cleared || (waves.Count == 0 && !IsBossFight)) return;
            if (_actOnMeasure == long.MaxValue)
            {
                if (beat.Measure < 0) return; // still in the song's count-in
                _actOnMeasure = beat.Measure + countInMeasures;
            }
            if (beat.Measure < _actOnMeasure) return;

            if (_state == State.Downed && playerHealth != null) playerHealth.Revive();
            if (_state == State.Waiting || _state == State.Between || _state == State.Downed)
            {
                if (IsBossFight) SpawnBoss(); else SpawnWave();
                _actOnMeasure = long.MaxValue - 1; // nothing scheduled until this wave ends
            }
        }

        void SpawnWave()
        {
            var wave = waves[_waveIndex % waves.Count];
            float hpMult = 1f + _loop * hpGrowthPerLoop;
            Vector2 playerPos = combat != null ? (Vector2)combat.transform.position : Vector2.zero;

            for (int i = 0; i < wave.enemies.Length; i++)
            {
                var def = wave.enemies[i];
                if (def == null) continue;
                Spawn(def, SpawnPoint(playerPos, i), hpMult);
            }
            _state = State.Fighting;
            string label = _loop > 0 ? $"{wave.name} +{_loop}" : wave.name;
            WaveStarted?.Invoke(WaveNumber, label);
        }

        void SpawnBoss()
        {
            if (boss.body == null) { Debug.LogError($"[ArenaDirector] Boss '{boss.name}' has no body EnemyDefinition.", this); return; }
            var pos = new Vector2(bounds.center.x, bounds.yMax - 1.5f);
            var e = Spawn(boss.body, pos, 1f);
            _bossCtl = e.gameObject.AddComponent<BossController>();
            _bossCtl.Init(boss, conductor, this);
            _state = State.Fighting;
            WaveStarted?.Invoke(1, boss.displayName);
            BossSpawned?.Invoke(_bossCtl);
        }

        /// <summary>Summon an extra enemy at the arena edge (boss adds).</summary>
        public BeatEnemy SpawnAdd(EnemyDefinition def)
        {
            if (def == null) return null;
            Vector2 playerPos = combat != null ? (Vector2)combat.transform.position : Vector2.zero;
            return Spawn(def, SpawnPoint(playerPos, UnityEngine.Random.Range(0, 4)), 1f);
        }

        Vector2 SpawnPoint(Vector2 playerPos, int i)
        {
            // Points around the arena edge, picked to be away from the player.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float t = UnityEngine.Random.value;
                int side = (i + attempt) % 4;
                Vector2 p = side switch
                {
                    0 => new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, t), bounds.yMax - 0.6f),
                    1 => new Vector2(bounds.xMax - 0.6f, Mathf.Lerp(bounds.yMin, bounds.yMax, t)),
                    2 => new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, t), bounds.yMin + 0.6f),
                    _ => new Vector2(bounds.xMin + 0.6f, Mathf.Lerp(bounds.yMin, bounds.yMax, t)),
                };
                if (Vector2.Distance(p, playerPos) >= minSpawnDistance) return p;
            }
            return new Vector2(bounds.xMax - 0.6f, bounds.center.y);
        }

        BeatEnemy Spawn(EnemyDefinition def, Vector2 position, float hpMult)
        {
            var go = new GameObject(def.displayName);
            go.transform.position = position;

            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = def.shape == EnemyShape.Circle ? circleSprite : squareSprite;
            if (spriteMaterial != null) body.sharedMaterial = spriteMaterial;
            body.color = def.color;
            body.sortingOrder = 5;
            var spriteSize = body.sprite != null ? (Vector2)body.sprite.bounds.size : Vector2.one;
            go.transform.localScale = new Vector3(def.size.x / Mathf.Max(0.001f, spriteSize.x), def.size.y / Mathf.Max(0.001f, spriteSize.y), 1f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = spriteSize;

            // HP bar above the enemy, unscaled by the body.
            var barRoot = new GameObject("HpBar");
            barRoot.transform.SetParent(go.transform, false);
            var inv = new Vector3(1f / go.transform.localScale.x, 1f / go.transform.localScale.y, 1f);
            barRoot.transform.localScale = inv;
            barRoot.transform.localPosition = new Vector3(0f, spriteSize.y * 0.5f + 0.25f * inv.y, 0f);
            var bar = new GameObject("Fill");
            bar.transform.SetParent(barRoot.transform, false);
            var barSr = bar.AddComponent<SpriteRenderer>();
            barSr.sprite = squareSprite;
            if (spriteMaterial != null) barSr.sharedMaterial = spriteMaterial;
            barSr.color = new Color(1f, 0.35f, 0.35f);
            barSr.sortingOrder = 6;
            var barSprite = squareSprite != null ? (Vector2)squareSprite.bounds.size : Vector2.one;
            bar.transform.localScale = new Vector3(def.size.x / barSprite.x, 0.08f / barSprite.y, 1f);

            var enemy = go.AddComponent<BeatEnemy>();
            enemy.Init(def, conductor, combat != null ? combat.transform : null, combat, playerHealth, feedback, this, hpMult, body, bar.transform);
            enemy.Died += OnEnemyDied;
            enemy.AttackResolved += (e, r) => EnemyAttackResolved?.Invoke(e, r);
            enemy.GuardBlocked += e => EnemyGuarded?.Invoke(e);
            enemy.GuardBroken += e => EnemyGuardBroken?.Invoke(e);
            _enemies.Add(enemy);
            return enemy;
        }

        void OnEnemyDied(BeatEnemy e)
        {
            if (combat != null) combat.Score.AddBonus(e.Definition.scoreValue, combat.Groove.ScoreMultiplier);
            EnemyKilled?.Invoke(e);
        }

        void OnPlayerDowned()
        {
            if (_state == State.Cleared) return;
            DespawnAll();
            _state = State.Downed;
            _actOnMeasure = conductor.CurrentBeat.Measure + 2;
            PlayerDowned?.Invoke();
        }

        void ClearArena()
        {
            DespawnAll();
            _state = State.Cleared;
            ArenaCleared?.Invoke(combat != null ? combat.Score.Total : 0);
        }

        void DespawnAll()
        {
            foreach (var e in _enemies) if (e != null) Destroy(e.gameObject);
            _enemies.Clear();
            _bossCtl = null;
        }

        int AliveCount()
        {
            int n = 0;
            foreach (var e in _enemies) if (e != null && !e.IsDead) n++;
            return n;
        }

        /// <summary>Distance from a point to the closest living enemy (infinity if none).</summary>
        public float NearestEnemyDistance(Vector2 from)
        {
            float best = float.PositiveInfinity;
            foreach (var e in _enemies)
            {
                if (e == null || e.IsDead) continue;
                best = Mathf.Min(best, Vector2.Distance(from, e.transform.position));
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
