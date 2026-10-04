using System;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Settings rows shared by the title and pause menus: volume, audio offset (fine, 1 ms), animation offset, debug HUD,
    /// test checkpoints, calibration.
    /// </summary>
    public static class SettingsPanel
    {
        public static Button Build(Transform column, Action back, Action calibrate)
        {
            UiKit.Line(column, "SETTINGS", 40, UiKit.Gold, 60f);
            Button vol = null, audio = null, offset = null, debug = null, checkpoints = null;
            vol = UiKit.Button(column, "", () => { GameSettings.Volume = GameSettings.Volume >= 0.99f ? 0f : GameSettings.Volume + 0.1f; Refresh(); });
            audio = UiKit.Button(column, "", () => { NudgeAudio(1f); Refresh(); });
            UiKit.Button(column, "   Audio offset \u22121 ms", () => { NudgeAudio(-1f); Refresh(); });
            offset = UiKit.Button(column, "", () => { Nudge(10f); Refresh(); });
            var offsetDown = UiKit.Button(column, "   Animation offset −10 ms", () => { Nudge(-10f); Refresh(); });
            debug = UiKit.Button(column, "", () => { GameSettings.DebugHud = !GameSettings.DebugHud; Refresh(); });
            checkpoints = UiKit.Button(column, "", () => { GameSettings.TestCheckpoints = !GameSettings.TestCheckpoints; Refresh(); });
            if (calibrate != null) UiKit.Button(column, "Calibrate audio latency (tap J / X on 16 clicks)", calibrate);
            UiKit.Line(column, "Audio offset: how late you hear the music (set by calibration; fine-tune in 1 ms steps). Animation offset: positive shows animation earlier.", 22, UiKit.Dim, 56f);
            UiKit.Button(column, "Back", back);
            void Refresh()
            {
                UiKit.SetLabel(vol, $"Volume   <color=#ffd56a>{Mathf.RoundToInt(GameSettings.Volume * 100f)}%</color>");
                UiKit.SetLabel(audio, $"Audio offset   <color=#ffd56a>{AudioOffset():+0;-0;0} ms</color>   (+1)");
                UiKit.SetLabel(offset, $"Animation offset   <color=#ffd56a>{Offset():+0;-0;0} ms</color>   (+10)");
                UiKit.SetLabel(debug, $"Rhythm debug HUD   <color=#ffd56a>{(GameSettings.DebugHud ? "on" : "off")}</color>");
                UiKit.SetLabel(checkpoints, $"Checkpoints (testing only)   <color=#ffd56a>{(GameSettings.TestCheckpoints ? "on" : "off")}</color>");
            }
            Refresh();
            return vol;
        }

        static float Offset() => PlayerPrefs.GetFloat(RhythmAnimator.OffsetPrefKey, 0f);

        static float AudioOffset() => PlayerPrefs.GetFloat(ProjectHymn.Core.Conductor.CalibrationPrefKey, 0f);

        static void NudgeAudio(float ms)
        {
            float v = Mathf.Clamp(AudioOffset() + ms, -300f, 300f);
            var conductor = UnityEngine.Object.FindAnyObjectByType<ProjectHymn.Core.Conductor>();
            if (conductor != null) conductor.SaveCalibration(v);
            else PlayerPrefs.SetFloat(ProjectHymn.Core.Conductor.CalibrationPrefKey, v);
        }

        static void Nudge(float ms)
        {
            var anim = UnityEngine.Object.FindAnyObjectByType<RhythmAnimator>();
            if (anim != null) anim.AnimationOffsetMs += ms;
            else PlayerPrefs.SetFloat(RhythmAnimator.OffsetPrefKey, Mathf.Clamp(Offset() + ms, -250f, 250f));
        }
    }
}
