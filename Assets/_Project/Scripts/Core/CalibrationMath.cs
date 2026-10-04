using System;
using System.Collections.Generic;

namespace ProjectHymn.Core
{
    /// <summary>
    /// Turns a set of tap offsets (seconds from the click; positive = late) into a calibration. The median ignores the odd
    /// fumbled tap; the spread (median distance from the median) tells the player whether the result is trustworthy.
    /// Pure C#.
    /// </summary>
    public static class CalibrationMath
    {
        public readonly struct Result
        {
            public readonly double OffsetMs;
            public readonly double SpreadMs;
            public readonly int Taps;
            public Result(double offsetMs, double spreadMs, int taps) { OffsetMs = offsetMs; SpreadMs = spreadMs; Taps = taps; }

            /// <summary>Taps this consistent leave room for True Perfect (±15 ms).</summary>
            public bool Consistent => SpreadMs <= 15.0;
        }

        public static Result Summarize(IReadOnlyList<double> offsetsSeconds)
        {
            if (offsetsSeconds == null || offsetsSeconds.Count == 0) return new Result(0, 0, 0);
            double median = Median(offsetsSeconds);
            var dev = new List<double>(offsetsSeconds.Count);
            foreach (var o in offsetsSeconds) dev.Add(Math.Abs(o - median));
            return new Result(median * 1000.0, Median(dev) * 1000.0, offsetsSeconds.Count);
        }

        static double Median(IReadOnlyList<double> values)
        {
            var v = new List<double>(values);
            v.Sort();
            int n = v.Count;
            return n % 2 == 1 ? v[n / 2] : (v[n / 2 - 1] + v[n / 2]) * 0.5;
        }
    }
}
