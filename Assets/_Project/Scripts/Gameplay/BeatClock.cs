using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// "Where is the beat now?" for things that follow the music without judging it (VS-5): glowing props, lanterns and
    /// the dialogue's word crawl. Reads the scene's Conductor when one plays, else the village's <see cref="LayeredMusic"/>,
    /// else a steady 100 BPM on the unscaled clock, so nothing stalls when no music plays (or the game is paused).
    /// </summary>
    public static class BeatClock
    {
        static Conductor _conductor;
        static float _lookedAt = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _conductor = null; _lookedAt = -10f; }

        static Conductor Find()
        {
            if (_conductor == null && Time.unscaledTime - _lookedAt > 1f)
            {
                _lookedAt = Time.unscaledTime;
                _conductor = Object.FindAnyObjectByType<Conductor>();
            }
            return _conductor;
        }

        /// <summary>Beats elapsed (whole beats plus the phase into the current one).</summary>
        public static double Beat
        {
            get
            {
                var c = Find();
                if (c != null && c.IsPlaying && !c.IsPaused) return c.CurrentBeat.Index + c.BeatPhase;
                var m = LayeredMusic.Active;
                if (m != null) { double b = m.Beat; if (b >= 0.0) return b; }
                return Time.unscaledTime * 100.0 / 60.0;
            }
        }

        /// <summary>Beats per second at the moment.</summary>
        public static double BeatsPerSecond
        {
            get
            {
                var c = Find();
                if (c != null && c.IsPlaying && !c.IsPaused && c.CurrentBeat.Bpm > 0) return c.CurrentBeat.Bpm / 60.0;
                var m = LayeredMusic.Active;
                if (m != null && m.Beat >= 0.0) return m.bpm / 60.0;
                return 100.0 / 60.0;
            }
        }

        /// <summary>1 on each beat, falling away before the next (the <c>_HymnBeat</c> curve).</summary>
        public static float Pulse(float sharpness = 3f)
        {
            double b = Beat;
            float phase = (float)(b - System.Math.Floor(b));
            return Mathf.Pow(1f - phase, sharpness);
        }
    }
}
