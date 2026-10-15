using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The build check's smoke run (Slice 12): when the player starts with <c>-hymnSmoke</c>, it opens every scene in the
    /// build in turn (free play), lets each run for a few seconds with the music going, records every error and
    /// exception, writes them to the file after <c>-hymnSmokeOut</c> and quits (exit code 0 clean, 1 otherwise). Does
    /// nothing in normal play.
    /// </summary>
    public class SmokeRun : MonoBehaviour
    {
        const float SecondsPerScene = 6f;

        readonly List<string> _problems = new List<string>();
        string _scene = "";
        string _out = "smoke-report.txt";

        /// <summary>This run is the build check's smoke run (playtest logs are not written).</summary>
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-hymnSmoke");
            if (i < 0) return;
            Active = true;
            var go = new GameObject("Smoke Run");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<SmokeRun>();
            int o = Array.IndexOf(args, "-hymnSmokeOut");
            if (o >= 0 && o + 1 < args.Length) s._out = args[o + 1];
        }

        void Awake() => Application.logMessageReceived += OnLog;
        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string where = "";
            foreach (var line in (stack ?? "").Split('\n'))
                if (line.Contains("ProjectHymn")) { where = "  @ " + line.Trim(); break; }
            _problems.Add($"[{_scene}] [{type}] {message}{where}");
        }

        IEnumerator Start()
        {
            var lines = new List<string> { $"Project Hymn smoke run, {DateTime.Now:yyyy-MM-dd HH:mm:ss}" };
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                _scene = Path.GetFileNameWithoutExtension(path);
                int before = _problems.Count;
                var op = SceneManager.LoadSceneAsync(i);
                while (op != null && !op.isDone) yield return null;
                float t = 0f;
                int frames = 0;
                while (t < SecondsPerScene) { t += Time.unscaledDeltaTime; frames++; yield return null; }
                lines.Add($"{_scene}: {frames} frames, {_problems.Count - before} errors");
            }
            lines.Add(_problems.Count == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            if (_problems.Count > 0) { lines.Add(""); lines.Add($"Problems ({_problems.Count}, first 80):"); lines.AddRange(_problems.GetRange(0, Math.Min(80, _problems.Count))); }
            try { File.WriteAllLines(_out, lines); } catch (Exception e) { Debug.Log("Smoke report not written: " + e.Message); }
            Application.Quit(_problems.Count == 0 ? 0 : 1);
        }
    }
}
