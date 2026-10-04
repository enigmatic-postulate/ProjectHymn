using System;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>Settings rows shared by the title and pause menus: volume, Easy timing, animation offset, debug HUD, calibration.</summary>
    public static class SettingsPanel
    {
        public static Button Build(Transform column, Action back, Action calibrate)
        {
            UiKit.Line(column, "SETTINGS", 40, UiKit.Gold, 60f);
            Button vol = null, easy = null, offset = null, debug = null;
            vol = UiKit.Button(column, "", () => { GameSettings.Volume = GameSettings.Volume >= 0.99f ? 0f : GameSettings.Volume + 0.1f; Refresh(); });
            easy = UiKit.Button(column, "", () => { GameSettings.EasyTiming = !GameSettings.EasyTiming; Refresh(); });
            offset = UiKit.Button(column, "", () => { Nudge(10f); Refresh(); });
            var offsetDown = UiKit.Button(column, "   Animation offset −10 ms", () => { Nudge(-10f); Refresh(); });
            debug = UiKit.Button(column, "", () => { GameSettings.DebugHud = !GameSettings.DebugHud; Refresh(); });
            if (calibrate != null) UiKit.Button(column, "Calibrate audio latency (tap J / X on 16 clicks)", calibrate);
            UiKit.Line(column, "Volume steps by 10% and wraps to 0. A positive offset shows animation earlier.", 22, UiKit.Dim, 40f);
            UiKit.Button(column, "Back", back);
            void Refresh()
            {
                UiKit.SetLabel(vol, $"Volume   <color=#ffd56a>{Mathf.RoundToInt(GameSettings.Volume * 100f)}%</color>");
                UiKit.SetLabel(easy, $"Easy timing (windows +50%)   <color=#ffd56a>{(GameSettings.EasyTiming ? "on" : "off")}</color>");
                UiKit.SetLabel(offset, $"Animation offset   <color=#ffd56a>{Offset():+0;-0;0} ms</color>   (+10)");
                UiKit.SetLabel(debug, $"Rhythm debug HUD   <color=#ffd56a>{(GameSettings.DebugHud ? "on" : "off")}</color>");
            }
            Refresh();
            return vol;
        }

        static float Offset() => PlayerPrefs.GetFloat(RhythmAnimator.OffsetPrefKey, 0f);

        static void Nudge(float ms)
        {
            var anim = UnityEngine.Object.FindAnyObjectByType<RhythmAnimator>();
            if (anim != null) anim.AnimationOffsetMs += ms;
            else PlayerPrefs.SetFloat(RhythmAnimator.OffsetPrefKey, Mathf.Clamp(Offset() + ms, -250f, 250f));
        }
    }
}
