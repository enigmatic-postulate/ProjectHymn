using System;
using UnityEngine;

namespace ProjectHymn.Core
{
    /// <summary>OffBeat is not a failure: the action still happens and still hits, for 25% damage and no combo.</summary>
    public enum Judgement { OffBeat = 0, Good = 1, Great = 2, Perfect = 3, TruePerfect = 4 }

    /// <summary>Timing windows in seconds either side of the beat. Defaults match the GDD.</summary>
    [Serializable]
    public struct TimingWindows
    {
        [Tooltip("GDD update 3: \u00b115 ms, 200% damage.")]
        [Min(0f)] public float truePerfect;
        [Min(0f)] public float perfect;
        [Min(0f)] public float great;
        [Min(0f)] public float good;
        [Tooltip("1 = normal. There is no Easy mode any more; kept for tools and tests.")]
        [Min(0.1f)] public float scale;

        public static TimingWindows Default => new TimingWindows { truePerfect = 0.015f, perfect = 0.045f, great = 0.090f, good = 0.135f, scale = 1f };

        /// <summary>Assets saved before True Perfect existed read 0 here: fall back to the GDD value.</summary>
        public float TruePerfect => (truePerfect > 0f ? truePerfect : 0.015f) * scale;
        public float Perfect => perfect * scale;
        public float Great => great * scale;
        public float Good => good * scale;
    }

    public readonly struct JudgeResult
    {
        public readonly Judgement Judgement;
        public readonly BeatInfo Beat;           // the beat the input was attributed to
        public readonly double OffsetSeconds;    // negative = early, positive = late

        public JudgeResult(Judgement judgement, BeatInfo beat, double offsetSeconds)
        {
            Judgement = judgement; Beat = beat; OffsetSeconds = offsetSeconds;
        }

        public bool IsOnBeat => Judgement != Judgement.OffBeat;
        public double OffsetMs => OffsetSeconds * 1000.0;
    }

    /// <summary>Judges an input time against the nearest beat. Pure C#.</summary>
    public static class BeatJudge
    {
        public static JudgeResult Judge(TempoMap map, double inputSongTime, TimingWindows windows)
        {
            long floor = (long)Math.Floor(map.TimeToBeat(inputSongTime));
            var a = map.GetBeat(floor);
            var b = map.GetBeat(floor + 1);
            double da = inputSongTime - a.Time;
            double db = inputSongTime - b.Time;
            var beat = Math.Abs(da) <= Math.Abs(db) ? a : b;
            double offset = Math.Abs(da) <= Math.Abs(db) ? da : db;
            return new JudgeResult(Classify(Math.Abs(offset), windows), beat, offset);
        }

        public static Judgement Classify(double absOffset, TimingWindows w)
        {
            if (absOffset <= w.TruePerfect) return Judgement.TruePerfect;
            if (absOffset <= w.Perfect) return Judgement.Perfect;
            if (absOffset <= w.Great) return Judgement.Great;
            if (absOffset <= w.Good) return Judgement.Good;
            return Judgement.OffBeat;
        }

        /// <summary>
        /// GDD update 3: every note hits. True Perfect 200% (stacks with finishers), Perfect 115%, Great 110%,
        /// Good 100%, off the beat 25%.
        /// </summary>
        public static float DamageMultiplier(Judgement j) => j switch
        {
            Judgement.TruePerfect => 2.0f,
            Judgement.Perfect => 1.15f,
            Judgement.Great => 1.10f,
            Judgement.Good => 1.0f,
            _ => 0.25f,
        };

        /// <summary>Perfect or better (True Perfect counts as a Perfect wherever a rule only knows Perfect).</summary>
        public static bool AtLeastPerfect(Judgement j) => j >= Judgement.Perfect;
    }
}
