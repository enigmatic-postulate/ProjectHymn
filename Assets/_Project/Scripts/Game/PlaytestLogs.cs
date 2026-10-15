using System;
using System.Collections.Generic;
using System.IO;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Where playtest run logs go (VS-5 Part C): one JSON file per stage attempt in the "Playtests" folder next to the
    /// saves (on Windows, %USERPROFILE%\AppData\LocalLow\DefaultCompany\Project Hymn\Playtests). F9 opens the folder;
    /// Shift+F9 turns the "how did that feel?" question on or off. Project Hymn → Playtest Summary in the editor reads the
    /// folder (and any logs dropped into Logs/Playtest/inbox) and writes the report.
    /// </summary>
    public static class PlaytestLogs
    {
        const string LogKey = "ProjectHymn.PlaytestLogs", FeelKey = "ProjectHymn.AskFeel";

        public static string Folder => Path.Combine(Application.persistentDataPath, "Playtests");

        /// <summary>Write a log for each run (on unless turned off).</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(LogKey, 1) == 1;
            set => PlayerPrefs.SetInt(LogKey, value ? 1 : 0);
        }

        /// <summary>Ask "how did that feel?" after each stage (on unless turned off).</summary>
        public static bool AskFeel
        {
            get => PlayerPrefs.GetInt(FeelKey, 1) == 1;
            set => PlayerPrefs.SetInt(FeelKey, value ? 1 : 0);
        }

        public static void Write(RunLog run, DateTime started)
        {
            if (run == null || !Enabled || SmokeRun.Active) return;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, RunLog.FileName(started, run.stage)), JsonUtility.ToJson(run, true));
            }
            catch (Exception e) { Debug.LogWarning($"[Project Hymn] Could not write the playtest log: {e.Message}"); }
        }

        /// <summary>Every log in a folder (unreadable files are skipped).</summary>
        public static List<RunLog> ReadAll(string folder)
        {
            var list = new List<RunLog>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return list;
            foreach (var path in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    var r = JsonUtility.FromJson<RunLog>(File.ReadAllText(path));
                    if (r != null && !string.IsNullOrEmpty(r.stage)) list.Add(r);
                }
                catch (Exception) { }
            }
            return list;
        }

        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            Application.OpenURL("file:///" + Folder.Replace('\\', '/'));
        }
    }
}
