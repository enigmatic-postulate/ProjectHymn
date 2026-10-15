using System;
using System.Collections.Generic;
using ProjectHymn.Core;

namespace ProjectHymn.Progression
{
    /// <summary>Something that happened in a run, at <see cref="t"/> seconds: hurt, death, fall, kill, parry, goal.</summary>
    [Serializable]
    public class RunEvent
    {
        public float t, x, y;
        public string kind = "";
        public string detail = "";
    }

    /// <summary>Where the player was, sampled twice a second (time per stretch of the stage, where runs stall).</summary>
    [Serializable]
    public struct RunSample
    {
        public float t, x, y;
    }

    /// <summary>
    /// One attempt at a stage, written to a JSON file when it ends (VS-5 Part C): who played (class, level, weapon), how it
    /// ended (cleared with a rank, failed on a C, died, fell, restarted or quit), the attack tally, the defence counts, why
    /// it missed S, where the player was over time, what happened when, and an optional 1 to 5 "how did that feel". Plain
    /// C# with public fields so Unity's JsonUtility writes it; <see cref="PlaytestSummary"/> reads a folder of them.
    /// </summary>
    [Serializable]
    public class RunLog
    {
        public const int CurrentVersion = 1;
        public const float SampleEvery = 0.5f;

        public int version = CurrentVersion;
        public string build = "";
        public string stage = "";
        /// <summary>When the run started, local time, "yyyy-MM-dd HH:mm:ss".</summary>
        public string started = "";
        public int slot;
        public bool freePlay;
        /// <summary>Finished or graded with a playtest key (F12 forced rank, Shift+F12 finish): left out of the summary's numbers.</summary>
        public bool debug;
        public string classId = "";
        public int level;
        public string weapon = "";

        /// <summary>"clear", "failed" (a C), "died", "fell", "restart" (R or the menu) or "quit".</summary>
        public string outcome = "";
        public float seconds;
        public float par;
        public string rank = "";
        public string timeRank = "";
        public List<string> caps = new List<string>();
        /// <summary>Why this run wasn't an S (empty for an S or a run that didn't finish).</summary>
        public List<string> missedS = new List<string>();
        public float efficiency;
        public bool clean;

        public int enemies, enemiesDefeated;
        public int attacks, offBeat, good, great, perfect, truePerfect, sequences;
        public int parries, blocks, dodges, hitsTaken, heartsLost, maxHearts;
        /// <summary>1 (rough) to 5 (great); 0 = not asked or skipped.</summary>
        public int feel;

        public List<RunEvent> events = new List<RunEvent>();
        public List<RunSample> track = new List<RunSample>();

        /// <summary>Hits on the beat (Good or better) as a share of all hits; -1 when nothing was pressed.</summary>
        public float OnBeat => attacks == 0 ? -1f : (attacks - offBeat) / (float)attacks;

        public bool Cleared => outcome == "clear";

        /// <summary>A file name that sorts by time: "20261015-185400_Cliffside.json".</summary>
        public static string FileName(DateTime started, string stage) =>
            $"{started:yyyyMMdd-HHmmss}_{(string.IsNullOrEmpty(stage) ? "Stage" : stage)}.json";

        public void Event(float t, string kind, float x, float y, string detail = "") =>
            events.Add(new RunEvent { t = t, kind = kind, x = x, y = y, detail = detail ?? "" });

        public void Sample(float t, float x, float y) => track.Add(new RunSample { t = t, x = x, y = y });

        /// <summary>Copy the attack tally and defence counts.</summary>
        public void Fill(PlaystyleLog log)
        {
            if (log == null) return;
            attacks = log.Attacks;
            offBeat = log.Hits(Judgement.OffBeat);
            good = log.Hits(Judgement.Good);
            great = log.Hits(Judgement.Great);
            perfect = log.Hits(Judgement.Perfect);
            truePerfect = log.Hits(Judgement.TruePerfect);
            sequences = log.SequencesCompleted;
            parries = log.Parries; blocks = log.Blocks; dodges = log.Dodges;
            hitsTaken = log.HitsTaken; heartsLost = log.HeartsLost; maxHearts = log.MaxHearts;
            enemiesDefeated = log.EnemiesDefeated;
        }

        /// <summary>Record a finished run's grade: rank, time rank, caps, and why it wasn't an S.</summary>
        public void Grade(StageResult r, float parSeconds)
        {
            seconds = (float)r.Seconds;
            par = parSeconds;
            rank = r.Rank.ToString();
            timeRank = r.TimeRank.ToString();
            caps = new List<string>(r.Caps ?? new List<string>());
            efficiency = (float)r.Efficiency;
            clean = r.Clean;
            outcome = r.Failed ? "failed" : "clear";
            missedS = WhyNotS(r, parSeconds);
        }

        /// <summary>The reasons a run missed S: over par (by how much), and each gate that capped it.</summary>
        public static List<string> WhyNotS(StageResult r, float parSeconds)
        {
            var why = new List<string>();
            if (r.Rank == StageRank.S) return why;
            if (r.TimeRank != StageRank.S && parSeconds > 0f)
                why.Add($"time: {r.Seconds:0.0}s against par {parSeconds:0}s (+{(r.Seconds - parSeconds) / parSeconds * 100.0:0}%)");
            if (r.Caps != null) foreach (var c in r.Caps) why.Add(c);
            if (why.Count == 0) why.Add("rank capped");
            return why;
        }
    }
}
