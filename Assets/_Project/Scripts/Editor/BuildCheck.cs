using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// The build check (Slice 12, part A): run by Tools/BuildCheck.cmd in batch mode. Rebuilds every scene with
    /// Project Hymn → Build Hub &amp; Loop, collects every error and exception logged while doing it, builds the Windows
    /// player into Builds/Windows, and writes Logs/BuildCheck/build-report.txt. Exits 0 when clean, 1 otherwise.
    /// Also on the menu (Project Hymn → Build Check (rebuild + Windows build)) for a run inside the editor.
    /// </summary>
    public static class BuildCheck
    {
        const string OutDir = "Logs/BuildCheck";
        const string PlayerPath = "Builds/Windows/ProjectHymn.exe";

        static readonly List<string> Problems = new List<string>();
        static int _warnings;

        [MenuItem("Project Hymn/Build Check (rebuild + Windows build)", priority = 50)]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        /// <summary>Batch entry point: -executeMethod ProjectHymn.EditorTools.BuildCheck.Run</summary>
        public static void Run() => Execute(exitWhenDone: true);

        static void Execute(bool exitWhenDone)
        {
            Directory.CreateDirectory(OutDir);
            Problems.Clear();
            _warnings = 0;
            var report = new List<string> { $"Project Hymn build check, {DateTime.Now:yyyy-MM-dd HH:mm:ss}, Unity {Application.unityVersion}" };
            bool ok = true;
            Application.logMessageReceived += OnLog;
            try
            {
                // 1. Rebuild every scene from code.
                var t0 = DateTime.Now;
                try { HubBuilder.BuildAll(); }
                catch (Exception e) { Problems.Add($"Build Hub & Loop threw: {e}"); }
                report.Add($"Rebuild: {(DateTime.Now - t0).TotalSeconds:0}s, {Problems.Count} errors, {_warnings} warnings while building scenes");
                if (Problems.Count > 0) ok = false;

                // 2. The Windows player.
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                report.Add($"Scenes in the build ({scenes.Length}): {string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}");
                Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath));
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = PlayerPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                };
                var result = BuildPipeline.BuildPlayer(options);
                var s = result.summary;
                report.Add($"Windows build: {s.result}, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024f * 1024f):0.0} MB, {s.totalTime.TotalSeconds:0}s");
                if (s.result != BuildResult.Succeeded) ok = false;
                foreach (var step in result.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) Problems.Add($"[build: {step.name}] {m.content}");
            }
            catch (Exception e)
            {
                ok = false;
                Problems.Add($"Build check threw: {e}");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }

            report.Add(ok && Problems.Count == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            if (Problems.Count > 0)
            {
                report.Add("");
                report.Add($"Problems ({Problems.Count}, first 80):");
                report.AddRange(Problems.Distinct().Take(80));
            }
            File.WriteAllLines(Path.Combine(OutDir, "build-report.txt"), report);
            Debug.Log("[Project Hymn] Build check: " + string.Join(" | ", report.Take(5)));
            if (exitWhenDone) EditorApplication.Exit(ok && Problems.Count == 0 ? 0 : 1);
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning) { _warnings++; return; }
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.StartsWith("[Project Hymn] Build check")) return;
            string first = stack?.Split('\n').FirstOrDefault(l => l.Contains("ProjectHymn")) ?? "";
            Problems.Add($"[{type}] {message}{(first.Length > 0 ? "  @ " + first.Trim() : "")}");
        }
    }
}
