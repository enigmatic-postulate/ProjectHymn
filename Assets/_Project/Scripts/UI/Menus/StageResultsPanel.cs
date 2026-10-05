using System;
using System.Text;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The end-of-stage screen: time against par, the rank and what capped it, the attack tally, defence, hits taken,
    /// enemies and efficiency (Core Combat), then what the clear did (Rank &amp; Class): experience and levels, level-downs
    /// and why, the class check, and weapons unlocked.
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
            if (o.NewBest) sb.Append("<color=#ffd56a>New best for this stage.</color>   ");
            if (o.LevelDownReasons.Count > 0)
            {
                sb.Append($"<color=#ff7a6a>Level down: {o.LevelBefore} → {o.LevelAfter}</color> ({string.Join("; ", o.LevelDownReasons)}). No experience this run, none lost.\n");
            }
            else
            {
                sb.Append($"+{o.XpGained} experience   ");
                if (o.LevelsGained.Count > 0) sb.Append($"<color=#a0ffa0>Level up: {o.LevelBefore} → {o.LevelAfter}</color>   ");
                sb.Append($"<color=#8a84a0>{o.XpIntoLevel} / {o.XpToNext} to level {o.LevelAfter + 1}</color>\n");
            }
            if (!string.IsNullOrEmpty(o.ClassChangeReason))
                sb.Append($"<color=#ffb070>Class: {ClassName(o.ClassBefore)} → {ClassName(o.ClassAfter)}</color> ({o.ClassChangeReason})\n");
            else if (o.Offer != null)
                sb.Append($"<color=#ffd56a>Class check at level {o.Offer.Level}:</color> {o.Offer.Reason}. Choose next.\n");
            else if (!string.IsNullOrEmpty(o.CheckNote))
                sb.Append($"Class check: {o.CheckNote}.\n");
            foreach (var w in o.WeaponsUnlocked)
                sb.Append($"<color=#ffd56a>Unlocked: {WeaponName(w)}</color> (equip it at the armsmaster)\n");
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
            UiKit.Line(col, $"{stage.ToUpperInvariant()} CLEARED", 44, UiKit.Gold, 62f);

            var sb = new StringBuilder();
            sb.Append($"Time  <color=#ffd56a>{Clock(r.Seconds)}</color>   par {Clock(t.ParSeconds)}   time rank <color=#ffd56a>{r.TimeRank}</color>\n");
            sb.Append($"Rank  <size=40><color=#ffd56a>{r.Rank}</color></size>");
            if (r.Caps.Count > 0) sb.Append($"   <color=#b0a8c8>capped: {string.Join("; ", r.Caps)}</color>");
            sb.Append("\n\n");
            sb.Append($"Hits   <color=#a0ffff>True Perfect {log.Hits(Judgement.TruePerfect)}</color>   <color=#ffd040>Perfect {log.Hits(Judgement.Perfect)}</color>   " +
                      $"Great {log.Hits(Judgement.Great)}   <color=#80ccff>Good {log.Hits(Judgement.Good)}</color>   <color=#8a84a0>Off-beat {log.OffBeat}</color>\n");
            sb.Append($"Defence   parries {log.Parries} (count as Perfect)   blocks {log.Blocks}   dodges {log.Dodges} (count as Great)\n");
            sb.Append($"Hits taken {log.HitsTaken}   hearts lost {log.HeartsLost}   enemies {log.EnemiesDefeated}/{t.Enemies}   sequences {log.SequencesCompleted}\n");
            sb.Append($"At {t.TargetTiming} or better: {log.ShareAtLeast(t.TargetTiming):P0} of {log.TallyTotal} actions\n");
            sb.Append($"Efficiency {r.Efficiency:P0}   ({t.PerfectAttacks} attacks for a perfect run, {log.Attacks} used)\n");
            sb.Append($"Mastery score {log.MasteryScore:0}   (a True Perfect counts as 3 Perfects)");
            UiKit.Line(col, sb.ToString(), 26, UiKit.Text, 470f, TextAnchor.UpperLeft);
            if (o != null) UiKit.Line(col, Progress(o), 26, UiKit.Text, 170f, TextAnchor.UpperLeft);
            UiKit.Select(UiKit.Button(col, o != null && o.Offer != null ? "Choose your class" : "Continue", Continue, 32, 64f));
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
