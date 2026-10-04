using System;
using System.Text;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The end-of-stage tally (Core Combat slice): time against par, the rank it would earn, the attack tally, defence,
    /// hits taken, enemies and efficiency. A debug screen for now: nothing here affects progress until Rank &amp; Class.
    /// </summary>
    public class StageResultsPanel : MonoBehaviour
    {
        Action _continue;
        float _openedAt;

        public static void Show(string stage, StageResult result, PlaystyleLog log, StageTargets targets, Action onContinue)
        {
            var p = new GameObject("Stage Results").AddComponent<StageResultsPanel>();
            p._continue = onContinue;
            p.Build(stage, result, log, targets);
        }

        static string Clock(double t) => $"{(int)(t / 60.0)}:{t % 60.0:00.0}";

        void Build(string stage, StageResult r, PlaystyleLog log, StageTargets t)
        {
            UiState.Push();
            _openedAt = Time.unscaledTime;
            var c = UiKit.Canvas("Results Canvas", 260, transform);
            var panel = UiKit.Panel("Results", c.transform, new Vector2(1180f, 860f));
            var col = UiKit.Column(panel, 6f, 30);
            UiKit.Line(col, $"{stage.ToUpperInvariant()} CLEARED", 44, UiKit.Gold, 62f);
            UiKit.Line(col, "Debug results: nothing here affects progress yet (ranks and classes come in the next slice).", 22, UiKit.Dim, 34f);

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
            UiKit.Line(col, sb.ToString(), 28, UiKit.Text, 520f, TextAnchor.UpperLeft);
            UiKit.Select(UiKit.Button(col, "Continue", Continue, 32, 64f));
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
