using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Player settings kept in PlayerPrefs: volume, Easy timing windows, and the debug HUD.</summary>
    public static class GameSettings
    {
        const string VolumeKey = "ProjectHymn.Volume", EasyKey = "ProjectHymn.EasyTiming", DebugKey = "ProjectHymn.DebugHud";

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.9f);
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); }
        }

        /// <summary>Easy mode: every timing window 50% wider.</summary>
        public static bool EasyTiming
        {
            get => PlayerPrefs.GetInt(EasyKey, 0) == 1;
            set => PlayerPrefs.SetInt(EasyKey, value ? 1 : 0);
        }

        public static float WindowScale => EasyTiming ? 1.5f : 1f;

        /// <summary>Show the prototype rhythm HUD (beat track, sequences, chain, score).</summary>
        public static bool DebugHud
        {
            get => PlayerPrefs.GetInt(DebugKey, 0) == 1;
            set => PlayerPrefs.SetInt(DebugKey, value ? 1 : 0);
        }

        public static void Apply() => AudioListener.volume = Volume;
    }
}
