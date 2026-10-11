using System;
using System.Collections;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;
using ProjectHymn.UI;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Runs the Practice Yard (Slice 10), the optional getting-started stage. The mentor introduces each lesson (the
    /// game pauses while he talks), the metronome strip shows the beat, prompts notes and tracks the task, and the
    /// <see cref="TutorialPlan"/> decides when a lesson is passed. The training knight's blows are blunted; the first
    /// red and violet glow each pause the game for a callout. The final bout is a real knight; the way out opens when he
    /// falls. Unranked: no level-downs, a one-time experience reward, and the save remembers the Yard was done.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        [SerializeField] TrainingDummy dummy;
        [SerializeField] BeatEnemy trainingKnight;
        [SerializeField] BeatEnemy boutKnight;
        [SerializeField] SliceGoal goal;
        [SerializeField] string returnSpawn = "FromCliffside";
        [Tooltip("Experience the first finish pays.")]
        [SerializeField] int rewardXp = 150;
        [SerializeField] string mentor = "Mentor";

        readonly TutorialPlan _plan = new TutorialPlan();
        Conductor _conductor;
        SwordCombatController _combat;
        PlayerHealth _health;
        CalibrationPanel _calibration;
        MetronomeStrip _strip;
        bool _paused, _talking, _redShown, _violetShown, _finished;

        public TutorialPlan Plan => _plan;

        void Start()
        {
            _conductor = FindAnyObjectByType<Conductor>();
            var player = FindAnyObjectByType<PlatformerController>();
            if (player != null)
            {
                _combat = player.GetComponent<SwordCombatController>();
                _health = player.GetComponent<PlayerHealth>();
            }
            _calibration = FindAnyObjectByType<CalibrationPanel>();
            if (_conductor != null) _conductor.MetronomeEnabled = true;          // the click, in the Yard only
            _strip = MetronomeStrip.Create(_conductor, _combat);

            if (_combat != null) { _combat.NoteJudged += OnNote; _combat.SequenceCompleted += OnSequence; }
            if (dummy != null) dummy.Struck += OnDummyStruck;
            if (trainingKnight != null) { trainingKnight.Training = true; trainingKnight.HitResolved += OnDefense; trainingKnight.gameObject.SetActive(false); }
            if (boutKnight != null) { boutKnight.Died += OnBoutWon; boutKnight.gameObject.SetActive(false); }
            if (_calibration != null) _calibration.Finished += OnCalibrated;
            if (goal != null) goal.Reached += OnGoal;
            if (_health != null) _health.Revived += OnRevived;
            _plan.Started += OnLesson;
            StartCoroutine(Welcome());
        }

        void OnDestroy()
        {
            if (_combat != null) { _combat.NoteJudged -= OnNote; _combat.SequenceCompleted -= OnSequence; }
            if (dummy != null) dummy.Struck -= OnDummyStruck;
            if (trainingKnight != null) trainingKnight.HitResolved -= OnDefense;
            if (boutKnight != null) boutKnight.Died -= OnBoutWon;
            if (_calibration != null) _calibration.Finished -= OnCalibrated;
            if (goal != null) goal.Reached -= OnGoal;
            if (_health != null) _health.Revived -= OnRevived;
            if (_paused) { Time.timeScale = 1f; UiState.Pop(); }
        }

        // ---------------- Flow ----------------

        IEnumerator Welcome()
        {
            // Let the music start (the Conductor schedules it a moment ahead) before pausing it for the mentor.
            while (_conductor != null && !_conductor.IsPlaying) yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
            var s = GameSession.Instance;
            var options = new List<ClassMenu.Option>
            {
                new ClassMenu.Option("start", "Start the lessons", "About five minutes: the beat, notes and sequences, guarding, parrying, dodging, and a real fight to finish."),
                new ClassMenu.Option("bout", "Skip to the final bout", "Straight to the bone knight."),
                new ClassMenu.Option("leave", "Back to the village", "Come back any time from the village gate."),
            };
            Hold(true);
            ClassMenu.Open("Practice Yard", s.Data.tutorialDone ? "You've done this before. Again?" : "New to the beat? Start here.", options, "start", id =>
            {
                Hold(false);
                if (id == "leave") { Leave(); return; }
                if (id == "bout") { _plan.Begin(); _plan.SkipTo(LessonKind.Bout); return; }
                _plan.Begin();
            }, () => { Hold(false); _plan.Begin(); });
        }

        void OnLesson(Lesson l)
        {
            if (l == null) { Complete(); return; }
            _strip.Prompt = "";
            ShowTask();
            Configure(l.Kind);
            Say(l.Intro, () =>
            {
                _strip.Prompt = l.Prompt;
                ShowTask();
                if (l.Kind == LessonKind.Calibrate)
                {
                    if (_calibration != null) _calibration.Begin();
                    else _plan.Calibrated();
                }
            });
        }

        /// <summary>Which enemies are out, and how, for a lesson.</summary>
        void Configure(LessonKind k)
        {
            bool knight = k == LessonKind.Guard || k == LessonKind.Parry || k == LessonKind.Dodge;
            if (trainingKnight != null)
            {
                trainingKnight.gameObject.SetActive(knight);
                trainingKnight.ForceUnblockable = k == LessonKind.Dodge;
            }
            if (boutKnight != null) boutKnight.gameObject.SetActive(k == LessonKind.Bout);
            if (k == LessonKind.Bout && _health != null) _health.HealFull();
        }

        void ShowTask()
        {
            var l = _plan.Current;
            if (l == null) { _strip.SetTask("Practice Yard: done. The way out is open.", ""); return; }
            _strip.SetTask($"<color=#ffd56a>{_plan.Index + 1}/{_plan.Lessons.Count}  {l.Title}</color>   {l.Task}", _plan.ProgressText);
        }

        void Passed(bool passed, string title)
        {
            if (passed) GameHud.Toast($"Lesson passed: {title}", 1.8f);
            else ShowTask();
        }

        void Complete()
        {
            if (_finished) return;
            _finished = true;
            Configure(LessonKind.Calibrate);              // every training enemy away
            ShowTask();
            var s = GameSession.Instance;
            bool first = !s.Data.tutorialDone;
            s.Data.tutorialDone = true;
            string reward = "";
            if (first && !s.FreePlay)
            {
                var rules = s.Db != null ? s.Db.Rules : ProgressionRules.Default;
                int levels = rules.GrantXp(s.Data, rewardXp);
                reward = levels > 0 ? $" (+{rewardXp} experience: level {s.Data.level})" : $" (+{rewardXp} experience)";
            }
            s.SaveProgress();
            s.NotifyChanged();
            Say(new[]
            {
                "That's everything I can teach you in a yard. The rest you learn out there, on the cliffs." + reward,
                "One last thing: out there there's no strip along the bottom. Just the music. Listen to it.",
                "The gate at the east end is open. Off you go.",
            }, null);
        }

        void OnGoal() => Leave();

        void Leave()
        {
            var s = GameSession.Instance;
            if (s.Db != null) s.Travel(s.Db.hubScene, returnSpawn, saveOnArrival: true);
        }

        void OnRevived() { if (_health != null) _health.HealFull(); }

        // ---------------- What the player did ----------------

        void OnNote(NoteResult n)
        {
            if (_talking) return;
            var title = _plan.Current?.Title;
            Passed(_plan.Note(n.Button, n.OnBeat, n.Effective), title);
        }

        void OnSequence(SwordSequence q)
        {
            if (_talking || q == null) return;
            var title = _plan.Current?.Title;
            Passed(_plan.Sequence(q.key, false), title);
        }

        void OnDummyStruck(HitData hit)
        {
            if (_talking || hit.Effect != SequenceEffect.Launch) return;
            var title = _plan.Current?.Title;
            Passed(_plan.Sequence("RisingVerse", true), title);
        }

        void OnDefense(BeatEnemy e, DefenseResult r)
        {
            if (_talking) return;
            var title = _plan.Current?.Title;
            Passed(_plan.Defense(r), title);
        }

        void OnBoutWon(BeatEnemy e)
        {
            var title = _plan.Current?.Title;
            Passed(_plan.BoutWon(), title);
        }

        void OnCalibrated(bool saved)
        {
            if (_plan.Current == null || _plan.Current.Kind != LessonKind.Calibrate) return;
            if (!saved) GameHud.Toast("Calibration skipped (C in a stage, or Settings, to do it later)", 2.6f);
            Passed(_plan.Calibrated(), "Tune your ears");
        }

        // ---------------- The glow callouts ----------------

        void Update()
        {
            if (_talking || _finished || trainingKnight == null || !trainingKnight.gameObject.activeInHierarchy) return;
            if (trainingKnight.Galvanize <= 0f) return;
            var k = _plan.Current?.Kind;
            if (k == LessonKind.Guard && !_redShown)
            {
                _redShown = true;
                Say(new[] { "There! The red glow. His blow lands on the next beat: press L on it." }, null);
            }
            else if (k == LessonKind.Dodge && !_violetShown)
            {
                _violetShown = true;
                Say(new[] { "Violet! That one can't be guarded. Roll through it: Shift, on the beat it lands." }, null);
            }
        }

        // ---------------- Talking (the game holds while the mentor speaks) ----------------

        void Say(string[] lines, Action then)
        {
            if (lines == null || lines.Length == 0) { then?.Invoke(); return; }
            _talking = true;
            Hold(true);
            DialogueBox.Show(mentor, lines, () =>
            {
                Hold(false);
                _talking = false;
                then?.Invoke();
            });
        }

        void Hold(bool on)
        {
            if (on == _paused) return;
            _paused = on;
            Time.timeScale = on ? 0f : 1f;
            if (_conductor != null) { if (on) _conductor.Pause(); else _conductor.Resume(); }
        }
    }
}
