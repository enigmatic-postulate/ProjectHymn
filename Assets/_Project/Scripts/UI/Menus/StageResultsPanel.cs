using System;
using System.Text;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The end-of-stage screen (GDD update 18): time against par, the rank and what capped it (a C is a fail), the attack
    /// tally, defence, hits taken, enemies and efficiency, then what the run did: level-downs during it, a clean clear,
    /// experience and level-ups, classes unlocked, the Fighter fallback, the level-up class change, and the shop.
    /// </summary>
    public class StageResultsPanel : MonoBehaviour
    {
        Action _continue;
        float _openedAt;

        public static void Show(string stage, StageResult result, PlaystyleLog log, StageTargets targets, RunOutcome outcome, Action onContinue)
        {
            var p = new GameObject("Stage Results").AddComponent<StageResultsPanel>();
            p._continue = onContinue;
            p.Build(stage, result, log, targets, outcome);
        }

        static string ClassName(string id)
        {
            var db = GameSession.Instance.Db;
            var c = db != null ? db.Class(id) : null;
            return c != null && c.id == id ? c.displayName : id;
        }

        static string WeaponName(string id)
        {
            var db = GameSession.Instance.Db;
            var w = db != null ? db.Weapon(id) : null;
            return w != null && w.id == id ? w.displayName : id;
        }

        static string Progress(RunOutcome o)
        {
            var sb = new StringBuilder();
            foreach (var d in o.LevelDowns)
                sb.Append($"<color=#ff7a6a>Level down during the run: {d.LevelBefore} → {d.LevelAfter}</color> ({d.Reason})" +
                          (d.LostClass ? $", back to the {ClassName(d.ClassAfter)}" : "") + "\n");
            if (o.Failed)
            {
                sb.Append("<color=#ff7a6a>A C is a fail: replay the stage.</color> No experience; a clean clear (every enemy, never below half your hearts) can't fail on time.\n");
                return sb.ToString();
            }
            if (o.NewBest) sb.Append("<color=#ffd56a>New best for this stage.</color>   ");
            if (o.Clean) sb.Append("<color=#a0ffa0>Clean clear</color> (counts toward unlocking classes)   ");
            sb.Append("\n");
            if (o.AtCap && o.LevelsGained.Count == 0) sb.Append($"Level {o.LevelAfter} (the slice's cap)\n");
            else
            {
                sb.Append($"+{o.XpGained} experience   ");
                if (o.LevelsGained.Count > 0) sb.Append($"<color=#a0ffa0>Level up: {o.LevelsGained[0] - 1} → {o.LevelAfter}</color>   ");
                sb.Append(o.AtCap ? "<color=#8a84a0>(level cap)</color>\n" : $"<color=#8a84a0>{o.XpIntoLevel} / {o.XpToNext} to level {o.LevelAfter + 1}</color>\n");
            }
            foreach (var c in o.ClassesUnlocked)
                sb.Append($"<color=#ffd56a>Unlocked the {ClassName(c)}</color> (offered at level-ups from level 6)\n");
            if (!string.IsNullOrEmpty(o.ClassChangeReason))
                sb.Append($"<color=#ffb070>Class: {ClassName(o.ClassBefore)} → {ClassName(o.ClassAfter)}</color> ({o.ClassChangeReason})\n");
            if (o.SequencesLearned.Count > 0)
                sb.Append($"<color=#ffd56a>Sequences learned: {Sequences.Names(o.SequencesLearned)}</color> (any class can play them)\n");
            if (o.Offer != null)
                sb.Append($"<color=#ffd56a>Level {o.Offer.Level}:</color> change to any class you have unlocked, or keep yours. Choose next.\n");
            foreach (var w in o.WeaponsUnlocked)
                sb.Append($"<color=#ffd56a>The {WeaponName(w)} is in the shop</color> (buy it with Notes)\n");
            return sb.ToString();
        }

        static string Clock(double t) => $"{(int)(t / 60.0)}:{t % 60.0:00.0}";

        void Build(string stage, StageResult r, PlaystyleLog log, StageTargets t, RunOutcome o)
        {
            UiState.Push();
            _openedAt = Time.unscaledTime;
            var c = UiKit.Canvas("Results Canvas", 260, transform);
            var panel = UiKit.Panel("Results", c.transform, new Vector2(1180f, 960f));
            var col = UiKit.Column(panel, 6f, 30);
            bool failed = o != null && o.Failed;
            UiKit.Line(col, failed ? $"{stage.ToUpperInvariant()}: FAILED" : $"{stage.ToUpperInvariant()} CLEARED", 44, failed ? new Color(1f, 0.48f, 0.42f) : UiKit.Gold, 62f);

            var sb = new StringBuilder();
            sb.Append($"Time  <color=#ffd56a>{Clock(r.Seconds)}</color>   par {Clock(t.ParSeconds)}   time rank <color=#ffd56a>{r.TimeRank}</color>\n");
            sb.Append($"Rank  <size=40><color=#ffd56a>{r.Rank}</color></size>");
            if (r.Caps.Count > 0) sb.Append($"   <color=#b0a8c8>capped: {string.Join("; ", r.Caps)}</color>");
            sb.Append("\n\n");
            sb.Append($"Hits   <color=#a0ffff>True Perfect {log.Hits(Judgement.TruePerfect)}</color>   <color=#ffd040>Perfect {log.Hits(Judgement.Perfect)}</color>   " +
                      $"Great {log.Hits(Judgement.Great)}   <color=#80ccff>Good {log.Hits(Judgement.Good)}</color>   <color=#8a84a0>Off-beat {log.OffBeat}</color>\n");
            sb.Append($"Defence   parries {log.Parries} (count as Perfect)   blocks {log.Blocks}   dodges {log.Dodges} (count as Great)\n");
            sb.Append($"Hits taken {log.HitsTaken}   hearts lost {log.HeartsLost}   enemies {log.EnemiesDefeated}/{t.Enemies}   sequences {log.SequencesCompleted}\n");
            sb.Append($"On the beat {log.OnBeatShare:P0}   guard {log.GuardShare:P0} / dodge {log.DodgeShare:P0} of {log.EnemyAttacks} enemy attacks\n");
            sb.Append($"Efficiency {r.Efficiency:P0}   ({t.PerfectAttacks} attacks for a perfect run, {log.Attacks} used)\n");
            sb.Append($"Mastery score {log.MasteryScore:0}   (a True Perfect counts as 3 Perfects)");
            UiKit.Line(col, sb.ToString(), 26, UiKit.Text, 470f, TextAnchor.UpperLeft);
            if (o != null) UiKit.Line(col, Progress(o), 26, UiKit.Text, 200f, TextAnchor.UpperLeft);
            string label = failed ? "Replay" : o != null && o.Offer != null ? "Level up: choose your class" : "Continue";
            UiKit.Select(UiKit.Button(col, label, Continue, 32, 64f));
        }

        void Continue()
        {
            if (Time.unscaledTime - _openedAt < 0.4f) return;      // the press that finished the stage doesn't skip this
            UiState.Pop();
            var cb = _continue;
            _continue = null;
            Destroy(gameObject);
            cb?.Invoke();
        }

        void Update()
        {
            if (UiInput.Cancel) Continue();
        }

        void OnDestroy() { if (_continue != null) UiState.Pop(); }
    }
}
