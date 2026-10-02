using System;
using UnityEngine;

namespace ProjectHymn.Core
{
    public enum Judgement { Miss = 0, Good = 1, Great = 2, Perfect = 3 }

    /// <summary>Timing windows in seconds either side of the beat. Defaults match the GDD.</summary>
    [Serializable]
    public struct TimingWindows
    {
        [Min(0f)] public float perfect;
        [Min(0f)] public float great;
        [Min(0f)] public float good;
        [Tooltip("1 = normal. Easy mode uses 1.5.")]
        [Min(0.1f)] public float scale;

        public static TimingWindows Default => new TimingWindows { perfect = 0.045f, great = 0.090f, good = 0.135f, scale = 1f };

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

        public bool IsHit => Judgement != Judgement.Miss;
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
            if (absOffset <= w.Perfect) return Judgement.Perfect;
            if (absOffset <= w.Great) return Judgement.Great;
            if (absOffset <= w.Good) return Judgement.Good;
            return Judgement.Miss;
        }

        public static float DamageMultiplier(Judgement j) => j switch
        {
            Judgement.Perfect => 1.5f,
            Judgement.Great => 1.2f,
            Judgement.Good => 1.0f,
            _ => 0.4f,
        };
    }
}
