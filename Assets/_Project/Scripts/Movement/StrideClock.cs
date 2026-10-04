using System;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// Keeps the run cycle on the music (vertical slice 1). Footfalls land on beats: the number of steps per beat is
    /// the power of two closest (in tempo terms) to a natural running cadence, so at 130 BPM one step per beat, at 90 BPM
    /// two, and the cycle phase is read straight from the song's beat position. Plain C# for tests.
    /// </summary>
    public static class StrideClock
    {
        /// <summary>Natural cadence in steps per second the beat-locked stride stays close to.</summary>
        public const double DefaultCadence = 2.8;

        /// <summary>1, 2, 4 ... steps per beat (or 0.5 for very fast songs), whichever gives a cadence closest to <paramref name="cadence"/>.</summary>
        public static double StepsPerBeat(double secondsPerBeat, double cadence = DefaultCadence)
        {
            if (secondsPerBeat <= 0.0) return 1.0;
            double ideal = cadence * secondsPerBeat;              // steps per beat at the natural cadence
            double exp = Math.Round(Math.Log(ideal, 2.0));        // nearest power of two in log space
            exp = Math.Max(-1.0, Math.Min(2.0, exp));
            return Math.Pow(2.0, exp);
        }

        /// <summary>
        /// Run-cycle phase in [0, 1). A cycle is two steps: right foot down at 0, left foot down at 0.5.
        /// <paramref name="beatPosition"/> is the song position in beats (beat index plus phase).
        /// </summary>
        public static float Phase(double beatPosition, double stepsPerBeat)
        {
            double cycles = beatPosition * stepsPerBeat * 0.5;
            double p = cycles - Math.Floor(cycles);
            return (float)p;
        }

        /// <summary>Stride length per step that matches <paramref name="runSpeed"/> at the beat-locked cadence (for the leg swing amount).</summary>
        public static float StrideLength(float runSpeed, double secondsPerBeat, double stepsPerBeat) =>
            (float)(runSpeed * secondsPerBeat / Math.Max(0.25, stepsPerBeat));
    }
}
