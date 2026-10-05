using ProjectHymn.Combat;
using ProjectHymn.Progression;
using ProjectHymn.UI;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Stage rules. Defeated enemies drop Notes. The stage keeps a timer against its par and a playstyle log (the GDD's
    /// attack tally). There are no checkpoints: dying or falling restarts the whole stage, timer included (test
    /// checkpoints bring the old respawns back, F7 or Settings). Reaching the goal ranks the run, applies it to the save
    /// (experience, level-downs, the class check, weapon unlocks), shows the results, offers the class choice when a
    /// class check gives one, and walks the player back into the village (which autosaves).
    /// </summary>
    public class StageDirector : MonoBehaviour
    {
        [SerializeField] string stageId = "Cliffside";
        [SerializeField] Vector2Int notesPerEnemy = new Vector2Int(20, 40);
        [SerializeField] SliceGoal goal;
        [SerializeField] string returnSpawn = "FromCliffside";
        [SerializeField] float restartDelay = 0.8f;

        [Header("Targets (GDD update 3; placeholders until playtested)")]
        [SerializeField] float parSeconds = 75f;
        [Tooltip("Attacks a perfect run needs (the efficiency score compares against it).")]
        [SerializeField] int perfectAttacks = 14;
        [Tooltip("Hits a run may take and still earn S (0 on the first stage).")]
        [SerializeField] int hitsAllowedForS = 0;
        [SerializeField] int heartsAllowedForA = 2;

        readonly System.Random _rng = new System.Random();
        DamagePopups _popups;
        PlatformerController _player;
        PlayerHealth _health;
        PlayerGuard _guard;
        SwordCombatController _combat;
        int _enemies;
        bool _finished, _restarting;

        public PlaystyleLog Log { get; } = new PlaystyleLog();
        public double Seconds { get; private set; }
        public StageTargets Targets => new StageTargets(parSeconds, _enemies, perfectAttacks, hitsAllowedForS, heartsAllowedForA);

        void Start()
        {
            _popups = FindAnyObjectByType<DamagePopups>();
            foreach (var e in BeatEnemy.All) { e.Died += OnEnemyDied; _enemies++; }
            if (goal == null) goal = FindAnyObjectByType<SliceGoal>();
            if (goal != null) goal.Reached += OnGoal;

            _player = FindAnyObjectByType<PlatformerController>();
            if (_player != null)
            {
                _health = _player.GetComponent<PlayerHealth>();
                _guard = _player.GetComponent<PlayerGuard>();
                _combat = _player.GetComponent<SwordCombatController>();
                _player.FellOut += OnFellOut;
                if (_health != null) { _health.Died += OnPlayerDied; _health.Hurt += OnHurt; }
                if (_guard != null) _guard.Defended += OnDefended;
                if (_combat != null) { _combat.NoteJudged += OnNote; _combat.SequenceCompleted += OnSequence; }
            }
            ApplyCheckpointRule();
        }

        void OnDestroy()
        {
            foreach (var e in BeatEnemy.All) e.Died -= OnEnemyDied;
            if (goal != null) goal.Reached -= OnGoal;
            if (_player != null) _player.FellOut -= OnFellOut;
            if (_health != null) { _health.Died -= OnPlayerDied; _health.Hurt -= OnHurt; }
            if (_guard != null) _guard.Defended -= OnDefended;
            if (_combat != null) { _combat.NoteJudged -= OnNote; _combat.SequenceCompleted -= OnSequence; }
        }

        /// <summary>The design has no checkpoints; test checkpoints keep the old respawns for tuning.</summary>
        void ApplyCheckpointRule()
        {
            bool test = GameSettings.TestCheckpoints;
            if (_player != null) _player.RespawnOnFall = test;
            if (_health != null) _health.RespawnOnDeath = test;
            foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                cp.gameObject.SetActive(test);
        }

        void Update()
        {
            if (!_finished && !_restarting) Seconds += Time.deltaTime;
            if (F7Pressed() && !UiState.Blocking)
            {
                GameSettings.TestCheckpoints = !GameSettings.TestCheckpoints;
                ApplyCheckpointRule();
                GameHud.Toast(GameSettings.TestCheckpoints ? "Test checkpoints ON (testing only)" : "Checkpoints OFF: dying restarts the stage");
            }
        }

        static bool F7Pressed()
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.f7Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F7);
#endif
        }

        // ---------------- Tally ----------------

        void OnNote(NoteResult n) { if (!_finished) Log.AddNote(n.Effective); }
        void OnSequence(SwordSequence s) { if (!_finished) Log.AddSequence(); }
        void OnDefended(DefenseResult r, Vector3 from) { if (!_finished) Log.AddDefense(r); }
        void OnHurt(int hearts) { if (!_finished) Log.AddHurt(hearts); }

        void OnEnemyDied(BeatEnemy e)
        {
            if (!_finished) Log.AddKill();
            int n = Rewards.RollNotes(notesPerEnemy.x, notesPerEnemy.y, _rng);
            GameSession.Instance.AddNotes(n);
            if (_popups != null) _popups.Show(e.transform.position + Vector3.up * 2.2f, $"+{n} ♪", new Color(1f, 0.85f, 0.4f), 1.2f);
        }

        // ---------------- Restart ----------------

        void OnPlayerDied() { if (!GameSettings.TestCheckpoints) Restart(); }
        void OnFellOut() => Restart();

        /// <summary>Restart the whole stage from the beginning (dying, falling, or R).</summary>
        public void Restart()
        {
            if (_restarting || _finished) return;
            _restarting = true;
            GameHud.Toast("Restarting the stage");
            Invoke(nameof(Reload), restartDelay);
        }

        void Reload()
        {
            var s = GameSession.Instance;
            s.Travel(s.SceneName, "Start");
        }

        // ---------------- Goal ----------------

        void OnGoal()
        {
            if (_finished || _restarting) return;
            _finished = true;
            var targets = Targets;
            var result = StageScoring.Score(Seconds, Log, targets);
            var s = GameSession.Instance;
            var outcome = s.RecordClear(stageId, result, Log);
            StageResultsPanel.Show(stageId, result, Log, targets, outcome, () =>
            {
                if (outcome.Offer != null)
                {
                    string current = s.Data.classId;
                    var options = ClassMenu.ClassOptions(outcome.Offer.Choices, current, outcome.Offer.Recommended);
                    ClassMenu.Open($"Level {outcome.Offer.Level}: class check", outcome.Offer.Reason, options, outcome.Offer.Recommended, id =>
                    {
                        if (id != current) s.SetClass(id);
                        ReturnToHub();
                    });
                }
                else ReturnToHub();
            });
        }

        void ReturnToHub()
        {
            var s = GameSession.Instance;
            if (s.Db != null) s.Travel(s.Db.hubScene, returnSpawn, saveOnArrival: true);
        }
    }
}
