using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Player settings kept in PlayerPrefs: master, music and effects volume, the debug HUD, and test-only checkpoints. There is no Easy mode
    /// (GDD update 3): timing windows are the same for every player.
    /// </summary>
    public static class GameSettings
    {
        const string VolumeKey = "ProjectHymn.Volume", DebugKey = "ProjectHymn.DebugHud", CheckpointKey = "ProjectHymn.TestCheckpoints";
        const string MusicKey = "ProjectHymn.MusicVolume", SfxKey = "ProjectHymn.SfxVolume";

        /// <summary>The music's share of the master volume (VS-4).</summary>
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.85f);
            set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
        }

        /// <summary>Sound effects' share of the master volume: hits, guards, steps, cues, menus (VS-4).</summary>
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.9f);
            set => PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.9f);
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); }
        }

        /// <summary>
        /// Testing only: keep the old checkpoints. The design has none (dying or falling restarts the stage), but a full
        /// restart on every death makes tuning slow. Toggle with F7 or in Settings.
        /// </summary>
        public static bool TestCheckpoints
        {
            get => PlayerPrefs.GetInt(CheckpointKey, 0) == 1;
            set => PlayerPrefs.SetInt(CheckpointKey, value ? 1 : 0);
        }

        /// <summary>Show the prototype rhythm HUD (beat track, sequences, chain, score).</summary>
        public static bool DebugHud
        {
            get => PlayerPrefs.GetInt(DebugKey, 0) == 1;
            set => PlayerPrefs.SetInt(DebugKey, value ? 1 : 0);
        }

        public static void Apply() => AudioListener.volume = Volume;

        /// <summary>One step of the volume controls (10%), rounded to the step so repeated presses land on even values.</summary>
        public const float VolumeStep = 0.1f;

        /// <summary>A volume moved <paramref name="steps"/> steps, kept within 0 to 100% (no wrap-around).</summary>
        public static float Stepped(float value, int steps) => Mathf.Clamp01(Mathf.Round(value / VolumeStep + steps) * VolumeStep);
    }
}
