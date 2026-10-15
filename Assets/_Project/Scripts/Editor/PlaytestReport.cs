using System;
using System.IO;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEditor;
using UnityEngine;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Project Hymn → Playtest Summary (VS-5 Part C): gathers the run logs from this machine's playtest folder and from
    /// Logs/Playtest/inbox (logs other playtesters sent; drop their .json files there), copies them all into
    /// Logs/Playtest/runs, and writes Logs/Playtest/summary.md: per stage, how runs ended, clear times against par with a
    /// suggested par, ranks, why runs missed S, timing, where players got hurt, died and fell, and how it felt.
    /// </summary>
    public static class PlaytestReport
    {
        const string Root = "Logs/Playtest";

        [MenuItem("Project Hymn/Playtest Summary", priority = 60)]
        public static void Build()
        {
            string runs = Path.Combine(Root, "runs"), inbox = Path.Combine(Root, "inbox");
            Directory.CreateDirectory(runs);
            Directory.CreateDirectory(inbox);
            int copied = Copy(PlaytestLogs.Folder, runs) + Copy(inbox, runs);
            var logs = PlaytestLogs.ReadAll(runs);
            string md = PlaytestSummary.Report(logs, DateTime.Now);
            string path = Path.Combine(Root, "summary.md");
            File.WriteAllText(path, md);
            Debug.Log($"[Project Hymn] Playtest summary: {logs.Count} runs ({copied} new) → {Path.GetFullPath(path)}");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Copy logs not already in <paramref name="to"/>; returns how many were new.</summary>
        static int Copy(string from, string to)
        {
            if (!Directory.Exists(from)) return 0;
            int n = 0;
            foreach (var f in Directory.GetFiles(from, "*.json"))
            {
                string dest = Path.Combine(to, Path.GetFileName(f));
                if (File.Exists(dest)) continue;
                File.Copy(f, dest);
                n++;
            }
            return n;
        }
    }
}
