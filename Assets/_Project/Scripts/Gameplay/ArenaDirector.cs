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

    /// <summary>One room of a stage (GDD M6): a stretch of floor, its waves, its camera and the door out.</summary>
    [Serializable]
    public class StageRoom
    {
        public string name = "Room";
        [Tooltip("Room floor on the ground plane: x = world X, y = world Z.")]
        public Rect bounds = new Rect(0f, -2f, 16f, 4f);
        [Tooltip("Angled 3D view (arenas, bosses). Off = the low side-lane camera.")]
        public bool arenaCamera;
        [Tooltip("Lane camera depth (world Z of the pivot).")]
        public float laneZ;
        public List<ArenaWave> waves = new List<ArenaWave>();
        [Tooltip("Fight the director's boss in this room (after its waves, if any).")]
        public bool boss;
        [Tooltip("Opens on the downbeat after the room is cleared; closes behind the player.")]
        public StageDoor exitDoor;
        [Tooltip("Optional bell plate that must also be pressed before the door opens.")]
        public StageSwitch requiredSwitch;
        [Tooltip("Adaptive song phase to loop while in this room (-1 = leave the music alone).")]
        public int musicPhase = -1;
        [Tooltip("Optional: switch to this song when entering (boss music). Restarts the count-in.")]
        public SongData song;
    }

    /// <summary>
    /// Runs one instanced arena (GDD M2 exit: one arena is fun for 2 minutes). Waves spawn on a downbeat
    /// after a short count-in; the next wave comes a measure after the last enemy falls; the list loops
    /// with tougher enemies. When the fight clock runs out the arena is cleared and the score shown.
    /// If the player goes down, the current wave restarts two measures later at full HP.
    /// Stage mode (GDD M6, when Rooms are set): rooms are fought in order; a cleared room's door opens on the
    /// next downbeat, walking into the next room closes it behind the player, moves the camera (starting on the
    /// next beat), loops that room's music phase, and the room's first wave comes in on the following downbeat.
    /// There is no time limit; the run ends when the last room (often the boss) is cleared.
    /// </summary>
    public class ArenaDirector : MonoBehaviour
    {
        enum State { Waiting, Fighting, Between, Downed, Travel, Cleared }

        [SerializeField] Conductor conductor;
        [SerializeField] BeatCombatController combat;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] CombatFeedback feedback;

        [SerializeField] List<ArenaWave> waves = new List<ArenaWave>();
        [Tooltip("Boss arena: when set, the boss is fought instead of the waves, with no time limit.")]
        [SerializeField] BossDefinition boss;
        [Header("Stage (M6)")]
        [Tooltip("When set, the arena is a stage of rooms fought in order instead of one timed floor.")]
        [SerializeField] List<StageRoom> rooms = new List<StageRoom>();
        [SerializeField] CameraRig cameraRig;
        [Tooltip("Measures of count-in after a song change (boss music).")]
        [SerializeField, Min(0)] int songChangeCountIn = 1;
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
        [Tooltip("Blob shadow under each enemy (M6): a flat disc mesh and a see-through dark material.")]
        [SerializeField] Mesh shadowMesh;
        [SerializeField] Material shadowMaterial;

        State _state = State.Waiting;
        long _actOnMeasure = long.MaxValue;
        int _waveIndex, _loop;
        double _fightSeconds;
        readonly List<BeatEnemy> _enemies = new List<BeatEnemy>();
        BossController _bossCtl;
        int _room;
        long _openDoorOnMeasure = long.MaxValue;
        int _countIn;

        public bool IsStage => rooms != null && rooms.Count > 0;
        public StageRoom CurrentRoom => IsStage ? rooms[Mathf.Clamp(_room, 0, rooms.Count - 1)] : null;
        public int RoomNumber => _room + 1;
        public int RoomCount => IsStage ? rooms.Count : 0;
        public bool IsTravelling => _state == State.Travel;
        /// <summary>The floor enemies are kept on: the current room in a stage.</summary>
        public Rect Bounds => IsStage ? CurrentRoom.bounds : bounds;
        public IReadOnlyList<BeatEnemy> Enemies => _enemies;
        public bool IsRunning => _state == State.Fighting || _state == State.Between || _state == State.Travel;
        public bool IsCleared => _state == State.Cleared;
        public int WaveNumber => _waveIndex + 1;
        public double FightSeconds => _fightSeconds;
        public float TimeLeft => Mathf.Max(0f, fightLengthSeconds - (float)_fightSeconds);
        public bool IsBossFight => boss != null && (!IsStage || CurrentRoom.boss);
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
        /// <summary>Stage: room index, room name.</summary>
        public event Action<int, string> RoomEntered;
        public event Action<int> RoomCleared;
        public event Action<int> DoorOpened;
        /// <summary>The room is clear but its bell plate hasn't been pressed yet.</summary>
        public event Action<int> SwitchNeeded;

        void Awake()
        {
            _countIn = countInMeasures;
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (cameraRig == null) cameraRig = FindAnyObjectByType<CameraRig>();
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
                if (!IsBossFight && !IsStage && _fightSeconds >= fightLengthSeconds) { ClearArena(); return; }
            }

            _enemies.RemoveAll(e => e == null);
            if (IsStage) { UpdateStage(); return; }
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
            if (_state == State.Cleared || (waves.Count == 0 && !IsBossFight && !IsStage)) return;

            if (_state == State.Travel)
            {
                var sw = CurrentRoom.requiredSwitch;
                if (beat.Measure >= _openDoorOnMeasure && (sw == null || sw.IsPressed))
                {
                    _openDoorOnMeasure = long.MaxValue;
                    var door = CurrentRoom.exitDoor;
                    if (door != null) door.Open((float)beat.SecondsPerBeat);
                    DoorOpened?.Invoke(_room);
                }
                return;
            }

            if (_actOnMeasure == long.MaxValue)
            {
                if (beat.Measure < 0) return; // still in the song's count-in
                _actOnMeasure = beat.Measure + _countIn;
            }
            if (beat.Measure < _actOnMeasure) return;

            if (_state == State.Downed && playerHealth != null) playerHealth.Revive();
            if (_state == State.Waiting || _state == State.Between || _state == State.Downed)
            {
                if (IsStage) SpawnRoomWave();
                else if (IsBossFight) SpawnBoss();
                else SpawnWave();
                _actOnMeasure = long.MaxValue - 1; // nothing scheduled until this wave ends
            }
        }

        // ---------------- Stage (M6) ----------------

        void UpdateStage()
        {
            var room = CurrentRoom;
            if (_state == State.Fighting)
            {
                if (IsBossFight && _bossCtl != null)
                {
                    if (_bossCtl.Enemy.IsDead)
                    {
                        BossDefeated?.Invoke(_bossCtl);
                        _bossCtl = null;
                        FinishRoom();
                    }
                    return;
                }
                if (AliveCount() > 0) return;
                WaveCleared?.Invoke(WaveNumber);
                _waveIndex++;
                bool bossNext = room.boss && boss != null && _bossCtl == null && _waveIndex == room.waves.Count;
                if (_waveIndex < room.waves.Count || bossNext)
                {
                    _state = State.Between;
                    _actOnMeasure = conductor.CurrentBeat.Measure + 2;
                }
                else FinishRoom();
            }
            else if (_state == State.Travel && _room + 1 < rooms.Count && combat != null)
            {
                var door = room.exitDoor;
                var next = rooms[_room + 1];
                float x = Ground.ToPlane(combat.transform.position).x;
                if ((door == null || door.IsOpen) && x > next.bounds.xMin + 0.8f) EnterRoom(_room + 1);
            }
        }

        void SpawnRoomWave()
        {
            var room = CurrentRoom;
            if (_waveIndex >= room.waves.Count)
            {
                if (room.boss && boss != null) { SpawnBoss(); return; }
                FinishRoom();
                return;
            }
            var wave = room.waves[_waveIndex];
            Vector2 playerPos = combat != null ? Ground.ToPlane(combat.transform.position) : Vector2.zero;
            for (int i = 0; i < wave.enemies.Length; i++)
                if (wave.enemies[i] != null) Spawn(wave.enemies[i], SpawnPoint(playerPos, i), 1f);
            _state = State.Fighting;
            WaveStarted?.Invoke(WaveNumber, wave.name);
        }

        void FinishRoom()
        {
            RoomCleared?.Invoke(_room);
            if (_room >= rooms.Count - 1) { ClearArena(); return; }
            _state = State.Travel;
            _openDoorOnMeasure = conductor.CurrentBeat.Measure + 1; // the next downbeat
            var sw = CurrentRoom.requiredSwitch;
            if (sw != null && !sw.IsPressed) SwitchNeeded?.Invoke(_room);
        }

        void EnterRoom(int index)
        {
            var prev = CurrentRoom;
            _room = index;
            _waveIndex = 0;
            var room = CurrentRoom;
            if (prev.exitDoor != null) prev.exitDoor.Close();

            var beat = conductor.CurrentBeat;
            if (room.song != null && room.song != conductor.Song)
            {
                conductor.Play(room.song);
                if (combat != null) combat.ResetForNewSong();
                _countIn = songChangeCountIn;
                _actOnMeasure = long.MaxValue;
                if (cameraRig != null) cameraRig.Blend(ShotFor(room), 1.2f);
            }
            else
            {
                if (room.musicPhase >= 0) conductor.RequestPhase(room.musicPhase);
                _actOnMeasure = beat.Measure + 1;
                if (cameraRig != null)
                {
                    // Start moving on the next beat, take one beat to get there.
                    double next = conductor.Map.GetBeat(beat.Index + 1).Time;
                    cameraRig.Blend(ShotFor(room), (float)beat.SecondsPerBeat, (float)Math.Max(0.0, next - conductor.SongTime));
                }
            }
            _state = State.Between;
            RoomEntered?.Invoke(_room, room.name);
        }

        public static CameraShot ShotFor(StageRoom room) =>
            room.arenaCamera ? CameraShot.Arena(room.bounds) : CameraShot.Lane(room.bounds, room.laneZ);

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
            var b = Bounds;
            var pos = new Vector2(b.center.x, b.yMax - 1.8f);
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
            // Points around the arena edge, picked to be away from the player. Lane rooms (M6) spawn at the
            // left and right ends so enemies come at the player along the lane.
            var bounds = Bounds;
            bool lane = IsStage && !CurrentRoom.arenaCamera;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float t = UnityEngine.Random.value;
                int side = lane ? ((i + attempt) % 3 == 2 ? 3 : 1) : (i + attempt) % 4;
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

            if (shadowMesh != null && shadowMaterial != null)
            {
                var sh = new GameObject("Shadow", typeof(MeshFilter), typeof(MeshRenderer), typeof(BlobShadow));
                sh.transform.SetParent(go.transform, false);
                sh.GetComponent<MeshFilter>().sharedMesh = shadowMesh;
                var smr = sh.GetComponent<MeshRenderer>();
                smr.sharedMaterial = shadowMaterial;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
                sh.GetComponent<BlobShadow>().Setup(model.transform, Mathf.Max(0.6f, def.size.x * 1.3f));
            }

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
            if (rooms == null) return;
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.6f);
            foreach (var r in rooms) Gizmos.DrawWireCube(Ground.ToWorld(r.bounds.center), new Vector3(r.bounds.width, 0.1f, r.bounds.height));
        }
    }
}
