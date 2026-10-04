using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Player settings kept in PlayerPrefs: volume, the debug HUD, and test-only checkpoints. There is no Easy mode
    /// (GDD update 3): timing windows are the same for every player.
    /// </summary>
    public static class GameSettings
    {
        const string VolumeKey = "ProjectHymn.Volume", DebugKey = "ProjectHymn.DebugHud", CheckpointKey = "ProjectHymn.TestCheckpoints";

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
    }
}
