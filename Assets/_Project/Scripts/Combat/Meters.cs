namespace ProjectHymn.Combat
{
    /// <summary>
    /// How a meter is felt, for the chain rule (VS-5 Part D). A beat is the tempo map's beat: a quarter in 4/4 and 3/4,
    /// an eighth in 6/8, 7/8 and 9/8. Players don't chain notes on eighths in a compound meter — at the Drowned Nave's
    /// 225 an eighth is 0.27 s, so a three-note sequence would have to be played in half a second — they chain on the
    /// felt pulse: the dotted quarter (three eighths) in 6/8 and 9/8, and the 2+2+3 groups in 7/8. A chain carries on
    /// across any of its meter's pulse gaps, so playing the big beats chains, and playing straight eighths still chains
    /// for anyone fast enough.
    /// </summary>
    public static class Meters
    {
        static readonly int[] Simple = { 1 };
        static readonly int[] Compound = { 1, 3 };
        static readonly int[] Seven = { 1, 2, 3 };

        /// <summary>The beat gaps that keep a chain going in this meter.</summary>
        public static int[] PulseGaps(string meter)
        {
            switch (meter)
            {
                case "6/8":
                case "9/8":
                case "12/8": return Compound;
                case "7/8": return Seven;
                default: return Simple;
            }
        }

        /// <summary>Beats in the felt pulse of this meter (3 in 6/8 and 9/8, 1 otherwise): what one note step spans.</summary>
        public static int PulseBeats(string meter)
        {
            var gaps = PulseGaps(meter);
            return gaps[gaps.Length - 1];
        }

        /// <summary>Whether a gap of <paramref name="gap"/> beats carries a chain on in this meter.</summary>
        public static bool IsPulseGap(string meter, long gap)
        {
            if (gap <= 0) return false;
            foreach (var g in PulseGaps(meter)) if (gap == g) return true;
            return false;
        }

        /// <summary>
        /// How many beats ahead an enemy must flare its warning aura to give the player <paramref name="minSeconds"/> of
        /// notice. A lead counted in beats shrinks as the music speeds up: the Summit's 7/8 runs at eighth = 300, so one
        /// beat is 0.2 s there against 0.4 s in its 4/4 — the warning all but vanished exactly where the fight was
        /// fastest. The lead is a length of time, floored here and converted back to beats.
        /// </summary>
        public static float TelegraphBeats(float galvanizeBeats, double secondsPerBeat, float minSeconds)
        {
            if (galvanizeBeats <= 0f) return galvanizeBeats;
            if (secondsPerBeat <= 0.0 || minSeconds <= 0f) return galvanizeBeats;
            float needed = (float)(minSeconds / secondsPerBeat);
            return galvanizeBeats > needed ? galvanizeBeats : needed;
        }

        /// <summary>A compound meter (its beat is an eighth grouped in threes).</summary>
        public static bool IsCompound(string meter) => meter == "6/8" || meter == "9/8" || meter == "12/8";
    }
}
