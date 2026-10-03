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
        [Tooltip("Arena floor on the ground plane: x = world X, y = world Z.")]
        [SerializeField] Rect bounds = new Rect(-7.5f, -4f, 15f, 8f);
        [SerializeField] float fightLengthSeconds = 120f;
        [SerializeField, Min(0)] int countInMeasures = 2;
        [Tooltip("Enemy HP grows by this much each time the wave list loops.")]
        [SerializeField] float hpGrowthPerLoop = 0.25f;
        [SerializeField] float minSpawnDistance = 4f;

        [Header("Look")]
        [Tooltip("Toon material for enemies that have no model (coloured capsule).")]
        [SerializeField] Material fallbackMaterial;
        [Tooltip("Unlit materials for the floating HP bars.")]
        [SerializeField] Material hpBarBackMaterial;
        [SerializeField] Material hpBarFillMaterial;

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
        /// <summary>How many times the player went down this run (each costs a rank step).</summary>
        public int TimesDowned { get; private set; }
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
            Vector2 playerPos = combat != null ? Ground.ToPlane(combat.transform.position) : Vector2.zero;

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
            var pos = new Vector2(bounds.center.x, bounds.yMax - 1.8f);
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
            Vector2 playerPos = combat != null ? Ground.ToPlane(combat.transform.position) : Vector2.zero;
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
            go.transform.position = Ground.ToWorld(position, def.hoverHeight);

            // Model (faces +Z, feet at y = 0), or a coloured capsule placeholder.
            GameObject model;
            if (def.model != null)
            {
                model = Instantiate(def.model, go.transform, false);
                model.transform.localScale = Vector3.one * def.modelScale;
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(model.GetComponent<Collider>());
                model.transform.SetParent(go.transform, false);
                model.transform.localScale = new Vector3(def.size.x, def.size.y * 0.5f, def.size.x);
                model.transform.localPosition = new Vector3(0f, def.size.y * 0.5f, 0f);
                var mr = model.GetComponent<MeshRenderer>();
                if (fallbackMaterial != null) mr.sharedMaterial = fallbackMaterial;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", def.color);
                mr.SetPropertyBlock(mpb);
            }
            model.name = "Model";
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = Mathf.Max(0.2f, def.size.x * 0.5f);
            col.height = Mathf.Max(col.radius * 2f, def.size.y);
            col.center = new Vector3(0f, def.size.y * 0.5f, 0f);

            // Floating HP bar above the head, always facing the camera.
            var barRoot = new GameObject("HpBar", typeof(Billboard));
            barRoot.transform.SetParent(go.transform, false);
            barRoot.transform.localPosition = new Vector3(0f, def.size.y + 0.45f, 0f);
            float barWidth = Mathf.Clamp(def.size.x * 1.2f, 0.7f, 2.2f);
            MakeBarPart(barRoot.transform, "Back", hpBarBackMaterial, new Vector3(barWidth + 0.06f, 0.16f, 0.02f), 0.01f);
            var fill = MakeBarPart(barRoot.transform, "Fill", hpBarFillMaterial, new Vector3(barWidth, 0.1f, 0.02f), -0.01f);

            go.AddComponent<ToonTint>();
            var enemy = go.AddComponent<BeatEnemy>();
            enemy.Init(def, conductor, combat != null ? combat.transform : null, combat, playerHealth, feedback, this, hpMult, model.transform, fill);
            enemy.Died += OnEnemyDied;
            enemy.AttackResolved += (e, r) => EnemyAttackResolved?.Invoke(e, r);
            enemy.GuardBlocked += e => EnemyGuarded?.Invoke(e);
            enemy.GuardBroken += e => EnemyGuardBroken?.Invoke(e);
            _enemies.Add(enemy);
            return enemy;
        }

        static Transform MakeBarPart(Transform parent, string name, Material mat, Vector3 size, float depth)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(part.GetComponent<Collider>());
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localScale = size;
            part.transform.localPosition = new Vector3(0f, 0f, depth);
            var mr = part.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return part.transform;
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
            TimesDowned++;
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
                best = Mathf.Min(best, Vector2.Distance(from, Ground.ToPlane(e.transform.position)));
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(Ground.ToWorld(bounds.center), new Vector3(bounds.width, 0.1f, bounds.height));
        }
    }
}
