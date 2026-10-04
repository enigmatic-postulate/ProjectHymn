using System;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// How an authored animation clip is stretched onto the beat (art pass). A sword move spans
    /// <c>firstBeat .. lastBeat</c> beats around its hit (0 = contact, on the beat); the clip's wind-up
    /// (<c>start .. hit</c>, normalized clip time) is squeezed or stretched into the beats before the hit and the
    /// follow-through (<c>hit .. end</c>) into the beats after, so the clip's impact frame always lands on the beat
    /// whatever the tempo. Pure C#.
    /// </summary>
    public static class ClipTiming
    {
        /// <summary>Normalized clip time for a move position <paramref name="tau"/> (beats from the hit).</summary>
        public static float Warp(float tau, float firstBeat, float lastBeat, float start, float hit, float end)
        {
            if (tau <= 0f)
            {
                if (firstBeat >= 0f) return hit;
                float k = Clamp01((tau - firstBeat) / (0f - firstBeat));
                return start + (hit - start) * k;
            }
            if (lastBeat <= 0f) return hit;
            float k2 = Clamp01(tau / lastBeat);
            return hit + (end - hit) * k2;
        }

        /// <summary>
        /// Beats to give a clip's wind-up and follow-through so that at the reference tempo it plays at about its
        /// authored speed, within limits that keep it readable at any tempo.
        /// </summary>
        public static void Span(float lengthSeconds, float start, float hit, float end, double referenceSpb,
                                float minPre, float maxPre, float minPost, float maxPost, out float preBeats, out float postBeats)
        {
            double spb = Math.Max(0.05, referenceSpb);
            preBeats = Clamp((float)(lengthSeconds * Math.Max(0f, hit - start) / spb), minPre, maxPre);
            postBeats = Clamp((float)(lengthSeconds * Math.Max(0f, end - hit) / spb), minPost, maxPost);
        }

        /// <summary>A looping clip's normalized time for a cycle phase, with the clip's own phase offset.</summary>
        public static float Loop(float phase, float offset)
        {
            double u = (double)phase + offset;
            return (float)(u - Math.Floor(u));
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        static float Clamp(float x, float a, float b) => x < a ? a : x > b ? b : x;
    }
}
