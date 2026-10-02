using System;
using System.IO;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Feeds <see cref="PlaystyleMetrics"/> during an arena fight and writes one JSON file per fight to
    /// <c>{persistentDataPath}/Playstyle/</c>. These five signals are what class assignment will use later.
    /// </summary>
    public class PlaystyleTracker : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] BeatCombatController combat;
        [SerializeField] ArenaDirector arena;
        [Tooltip("Only one weapon exists in the prototype.")]
        [SerializeField] string weaponName = "Blade";
        [SerializeField] float nearEnemyDistance = 2.5f;
        [SerializeField] float minFightSecondsToLog = 5f;

        bool _sawFirstSection;
        bool _written;

        public PlaystyleMetrics Metrics { get; private set; } = new PlaystyleMetrics();
        public string WeaponName { get => weaponName; set => weaponName = value; }
        public string LastWrittenPath { get; private set; }

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (combat == null) combat = FindAnyObjectByType<BeatCombatController>();
            if (arena == null) arena = FindAnyObjectByType<ArenaDirector>();
        }

        void OnEnable()
        {
            if (combat != null) combat.ActionJudged += OnAction;
            if (conductor != null) conductor.SectionChanged += OnSection;
            if (arena != null)
            {
                arena.EnemyAttackResolved += OnDefense;
                arena.ArenaCleared += OnArenaCleared;
            }
        }

        void OnDisable()
        {
            if (combat != null) combat.ActionJudged -= OnAction;
            if (conductor != null) conductor.SectionChanged -= OnSection;
            if (arena != null)
            {
                arena.EnemyAttackResolved -= OnDefense;
                arena.ArenaCleared -= OnArenaCleared;
            }
            Write(); // leaving Play mode mid-fight still logs it
        }

        void Update()
        {
            if (arena == null || !arena.IsRunning || combat == null) return;
            bool near = arena.NearestEnemyDistance(combat.transform.position) <= nearEnemyDistance;
            Metrics.Tick(Time.deltaTime, near, weaponName);
        }

        void OnAction(BeatActionResult r)
        {
            if (arena != null && !arena.IsRunning) return;
            Metrics.RecordAction(r.Action, r.Effective, r.Judge.Beat);
        }

        void OnSection(BeatInfo beat)
        {
            if (!_sawFirstSection) { _sawFirstSection = true; return; } // the song's first section isn't a change
            Metrics.RecordMeterChange(beat);
        }

        void OnDefense(BeatEnemy enemy, DefenseResult result) => Metrics.RecordDefense(result);

        void OnArenaCleared(long score) => Write();

        void Write()
        {
            if (_written || Metrics.FightSeconds < minFightSecondsToLog) return;
            _written = true;
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "Playstyle");
                Directory.CreateDirectory(dir);
                var utc = DateTime.UtcNow;
                string path = Path.Combine(dir, $"fight_{utc:yyyyMMdd_HHmmss}.json");
                string song = conductor != null && conductor.Song != null ? conductor.Song.name : null;
                File.WriteAllText(path, Metrics.ToJson(song, utc));
                LastWrittenPath = path;
                Debug.Log($"[Playstyle] Fight log written: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Playstyle] Could not write fight log: {e.Message}");
            }
        }
    }
}
