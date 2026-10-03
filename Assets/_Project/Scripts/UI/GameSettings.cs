using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.UI
{
    /// <summary>Applies saved settings to whatever is in the scene: volumes, metronome, beat cue, easy timing, calibration.</summary>
    public static class GameSettings
    {
        public static SettingsData Data => SaveSystem.Current.settings;

        public static void Apply()
        {
            var s = Data;
            AudioListener.volume = Mathf.Clamp01(s.masterVolume);
            HymnLook.Apply(s.celLook);

            var conductor = Object.FindAnyObjectByType<Conductor>();
            if (conductor != null)
            {
                conductor.MusicVolume = s.musicVolume;
                conductor.MetronomeEnabled = s.metronome;
                if (Mathf.Abs(s.calibrationMs) > 0.01f) conductor.CalibrationSeconds = s.calibrationMs / 1000.0;
            }

            var fx = Object.FindAnyObjectByType<CombatFeedback>();
            if (fx != null) fx.SfxVolume = s.sfxVolume;

            var hud = Object.FindAnyObjectByType<HymnHud>();
            if (hud != null) hud.ShowBeatCue = s.showBeatCue;

            var combat = Object.FindAnyObjectByType<BeatCombatController>();
            if (combat != null) combat.SetTimingScale(s.easyTiming ? 1.5f : 1f);
        }

        public static void SaveAndApply()
        {
            SaveSystem.Save();
            Apply();
        }
    }

    /// <summary>The settings screen, built on demand inside a parent panel.</summary>
    public static class SettingsMenu
    {
        public static void Build(Transform panel, System.Action onBack)
        {
            UIFactory.Label(panel, "SETTINGS", 48, color: UIFactory.Gold);
            var s = GameSettings.Data;

            UnityEngine.UI.Button first = null;
            first = VolumeRow(panel, "Master volume", () => s.masterVolume, v => s.masterVolume = v);
            VolumeRow(panel, "Music volume", () => s.musicVolume, v => s.musicVolume = v);
            VolumeRow(panel, "Effects volume", () => s.sfxVolume, v => s.sfxVolume = v);
            ToggleRow(panel, "Metronome click", () => s.metronome, v => s.metronome = v);
            ToggleRow(panel, "Visual beat cue (accessibility)", () => s.showBeatCue, v => s.showBeatCue = v);
            ToggleRow(panel, "Easy timing (+50% windows)", () => s.easyTiming, v => s.easyTiming = v);
            LookRow(panel, () => s.celLook, v => s.celLook = v);
            UIFactory.Label(panel, $"Audio latency: {s.calibrationMs:+0;-0;0} ms — press C in an arena to calibrate", 22, color: UIFactory.DimText);
            UIFactory.Button(panel, "Back", onBack);
            UIFactory.Focus(first);
        }

        static UnityEngine.UI.Button VolumeRow(Transform panel, string name, System.Func<float> get, System.Action<float> set)
        {
            UnityEngine.UI.Button b = null;
            string Label() => $"{name}:  < {Mathf.RoundToInt(get() * 100)}% >";
            b = UIFactory.Button(panel, Label(), () =>
            {
                // Each press steps up by 10%, wrapping to 0 after 100%.
                float v = get() + 0.1f;
                if (v > 1.001f) v = 0f;
                set(Mathf.Round(v * 10f) / 10f);
                UIFactory.SetButtonLabel(b, Label());
                GameSettings.SaveAndApply();
            }, 28, 56);
            return b;
        }

        /// <summary>M6: the Dragon Valor look (default) or the M5 cel look.</summary>
        static void LookRow(Transform panel, System.Func<bool> getCel, System.Action<bool> setCel)
        {
            UnityEngine.UI.Button b = null;
            string Label() => $"Look:  < {(getCel() ? "Cel (M5)" : "Valor")} >";
            b = UIFactory.Button(panel, Label(), () =>
            {
                setCel(!getCel());
                UIFactory.SetButtonLabel(b, Label());
                GameSettings.SaveAndApply();
            }, 28, 56);
        }

        static void ToggleRow(Transform panel, string name, System.Func<bool> get, System.Action<bool> set)
        {
            UnityEngine.UI.Button b = null;
            string Label() => $"{name}:  {(get() ? "ON" : "OFF")}";
            b = UIFactory.Button(panel, Label(), () =>
            {
                set(!get());
                UIFactory.SetButtonLabel(b, Label());
                GameSettings.SaveAndApply();
            }, 28, 56);
        }
    }
}
