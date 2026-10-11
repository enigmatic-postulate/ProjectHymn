using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;
using ProjectHymn.UI;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Stage rules (GDD update 18). The stage keeps a timer against its par and a playstyle log (the attack tally).
    /// There are no checkpoints: dying or falling restarts the whole stage, timer included (test checkpoints bring the old
    /// respawns back, F7 or Settings). Level-downs happen as they're earned: losing half your hearts drops a level, and so
    /// does dying (one level per run, not two). Reaching the goal ranks the run: a C is a fail and the stage restarts; B
    /// or better clears it, applies it to the save (experience, class unlocks, level-ups, the shop), offers a class change
    /// when a level-up from 6 has one, and walks the player back into the village (which autosaves).
    /// </summary>
    public class StageDirector : MonoBehaviour
    {
        [SerializeField] string stageId = "Cliffside";
        [SerializeField] string stageName = "";
        [SerializeField] Vector2Int notesPerEnemy = new Vector2Int(20, 40);
        [SerializeField] SliceGoal goal;
        [SerializeField] string returnSpawn = "FromCliffside";
        [SerializeField] float restartDelay = 0.8f;

        [Header("Targets (GDD update 18; placeholders until playtested)")]
        [SerializeField] float parSeconds = 75f;
        [Tooltip("Attacks a perfect run needs (the efficiency score compares against it).")]
        [SerializeField] int perfectAttacks = 14;
        [Tooltip("Enemies in the stage (0 = count those present at the start; the Arena counts every wave).")]
        [SerializeField] int enemyTotal;
        [Tooltip("Hits a run may take and still earn S (0 on the first stage).")]
        [SerializeField] int hitsAllowedForS = 0;
        [Tooltip("Hits a run may take and still earn A.")]
        [SerializeField] int hitsAllowedForA = 3;

        readonly System.Random _rng = new System.Random();
        readonly List<LevelDown> _levelDowns = new List<LevelDown>();
        DamagePopups _popups;
        PlatformerController _player;
        PlayerHealth _health;
        PlayerGuard _guard;
        SwordCombatController _combat;
        int _enemies;
        bool _finished, _restarting, _lostLevelThisRun;

        public PlaystyleLog Log { get; } = new PlaystyleLog();
        public double Seconds { get; private set; }
        public StageTargets Targets => new StageTargets(parSeconds, _enemies, perfectAttacks, hitsAllowedForS, hitsAllowedForA);

        void Start()
        {
            _popups = FindAnyObjectByType<DamagePopups>();
            _enemies = enemyTotal > 0 ? enemyTotal : BeatEnemy.All.Count;
            BeatEnemy.AnyDied += OnEnemyDied;
            if (goal == null) goal = FindAnyObjectByType<SliceGoal>();
            if (goal != null) goal.Reached += OnGoal;

            _player = FindAnyObjectByType<PlatformerController>();
            if (_player != null)
            {
                _health = _player.GetComponent<PlayerHealth>();
                _guard = _player.GetComponent<PlayerGuard>();
                _combat = _player.GetComponent<SwordCombatController>();
                _player.FellOut += OnFellOut;
                if (_health != null) { _health.Died += OnPlayerDied; _health.Hurt += OnHurt; Log.MaxHearts = _health.MaxHp; }
                if (_guard != null) _guard.Defended += OnDefended;
                if (_combat != null) { _combat.NoteJudged += OnNote; _combat.SequenceCompleted += OnSequence; }
            }
            ApplyCheckpointRule();
            AnnounceMeter();
        }

        /// <summary>Name the stage and its time signature as it starts (and warn a Fighter off-common-time).</summary>
        void AnnounceMeter()
        {
            var conductor = FindAnyObjectByType<Conductor>();
            var song = conductor != null ? conductor.Song : null;
            if (song == null || song.sections == null || song.sections.Count == 0) return;
            var first = song.sections[0];
            string name = string.IsNullOrEmpty(stageName) ? stageId : stageName;
            string text = $"{name}  ·  {first.Signature} time  ·  par {(int)(parSeconds / 60f)}:{parSeconds % 60f:00}";
            var cls = GameSession.Instance.Class;
            if (cls != null && cls.commonTimeOnly && first.beatsPerMeasure != 4)
                text += $"\n{cls.displayName}: sequences only in 4/4. Lone notes still hit here.";
            else if (Sequences.KnownIn(first.Signature).Count == 0)
                text += $"\nNone of your sequences play in {first.Signature}. Lone notes still hit here.";
            GameHud.Toast(text, 3.5f);
        }

        void OnDestroy()
        {
            BeatEnemy.AnyDied -= OnEnemyDied;
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
            if (UiState.Blocking) return;
            if (KeyPressed(7))
            {
                GameSettings.TestCheckpoints = !GameSettings.TestCheckpoints;
                ApplyCheckpointRule();
                GameHud.Toast(GameSettings.TestCheckpoints ? "Test checkpoints ON (testing only: no level-downs from deaths)" : "Checkpoints OFF: dying restarts the stage");
            }
            if (KeyPressed(12) && ShiftHeld() && !_finished && !_restarting)
            {
                GameHud.Toast("Debug: stage finished");
                Finish();
            }
        }

        static bool KeyPressed(int f)
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            return f == 7 ? kb.f7Key.wasPressedThisFrame : kb.f12Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(f == 7 ? KeyCode.F7 : KeyCode.F12);
#endif
        }

        static bool ShiftHeld()
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

        // ---------------- Tally ----------------

        void OnNote(NoteResult n) { if (!_finished) Log.AddNote(n.Effective); }
        void OnSequence(SwordSequence s) { if (!_finished) Log.AddSequence(); }
        void OnDefended(DefenseResult r, Vector3 from) { if (!_finished) Log.AddDefense(r); }

        void OnHurt(int hearts)
        {
            if (_finished) return;
            Log.AddHurt(hearts);
            if (Log.HalfHeartsLost) LevelDownOnce("lost half your hearts");
        }

        void OnEnemyDied(BeatEnemy e)
        {
            if (!_finished) Log.AddKill();
            int n = Rewards.RollNotes(notesPerEnemy.x, notesPerEnemy.y, _rng);
            GameSession.Instance.AddNotes(n);
            if (_popups != null) _popups.Show(e.transform.position + Vector3.up * 2.2f, $"+{n} Notes", new Color(1f, 0.85f, 0.4f), 1.2f);
        }

        // ---------------- Level-downs and restarts ----------------

        /// <summary>One level per run: half your hearts and then a death cost one level, not two. Test checkpoints skip it.</summary>
        void LevelDownOnce(string reason)
        {
            if (_lostLevelThisRun || GameSettings.TestCheckpoints) return;
            _lostLevelThisRun = true;
            var d = GameSession.Instance.LoseLevel(reason);
            _levelDowns.Add(d);
            string cls = d.LostClass ? $", back to the {GameSession.Instance.ClassName(d.ClassAfter)}" : "";
            GameHud.Toast($"Level down: {d.LevelBefore} → {d.LevelAfter} ({reason}){cls}", 3f);
        }

        void OnPlayerDied()
        {
            if (GameSettings.TestCheckpoints) return;
            LevelDownOnce("died");
            Restart();
        }

        void OnFellOut()
        {
            LevelDownOnce("fell");
            Restart();
        }

        /// <summary>Restart the whole stage from the beginning (dying, falling, a failed rank, or R).</summary>
        public void Restart()
        {
            if (_restarting) return;
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

        void OnGoal() => Finish();

        void Finish()
        {
            if (_finished || _restarting) return;
            _finished = true;
            var s = GameSession.Instance;
            var targets = Targets;
            if (s.DebugRank.HasValue)
            {
                // Debug: set the clear time to land on the forced rank.
                float[] factor = { 0.95f, 1.05f, 1.15f, 1.5f };
                Seconds = parSeconds * factor[(int)s.DebugRank.Value];
            }
            var result = StageScoring.Score(Seconds, Log, targets);
            if (s.DebugRank.HasValue && result.Rank != s.DebugRank.Value)
                result = new StageResult(result.Seconds, result.TimeRank, s.DebugRank.Value, result.Caps, result.Efficiency, result.Clean);
            var outcome = s.RecordClear(stageId, result, Log, _levelDowns);
            StageResultsPanel.Show(string.IsNullOrEmpty(stageName) ? stageId : stageName, result, Log, targets, outcome, () =>
            {
                if (outcome.Failed) { Restart(); return; }
                if (outcome.Offer != null)
                {
                    string current = s.Data.classId;
                    var options = ClassMenu.ClassOptions(outcome.Offer.Choices, current, outcome.Offer.Recommended);
                    ClassMenu.Open($"Level {outcome.Offer.Level}: change class?", outcome.Offer.Reason, options, outcome.Offer.Recommended, id =>
                    {
                        if (id != current)
                        {
                            var learned = new List<string>();
                            string granted = s.SetClass(id, learned);
                            var w = granted != null && s.Db != null ? s.Db.Weapon(granted) : null;
                            var lines = new List<string>();
                            if (w != null) lines.Add($"The {s.ClassName(id)} takes up the {w.displayName}");
                            if (learned.Count > 0) lines.Add($"Learned: {Sequences.Names(learned)}");
                            if (lines.Count > 0) GameHud.Toast(string.Join("\n", lines), 3.5f);
                        }
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
