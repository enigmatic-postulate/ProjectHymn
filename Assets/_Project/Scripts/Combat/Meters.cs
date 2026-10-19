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
        /// <summary>Beats in the felt pulse of this meter (3 in 6/8, 9/8 and 7/8, 1 otherwise): what one note step spans.</summary>
        public static int PulseBeats(string meter)
        {
            switch (meter)
            {
                case "6/8":
                case "9/8":
                case "12/8":
                case "7/8": return 3;
                default: return 1;
            }
        }

        /// <summary>
        /// Whether a gap of <paramref name="gap"/> beats carries a chain on: anything up to one felt pulse. In 4/4 and
        /// 3/4 that is the next beat, as before. In a compound meter it is any gap up to the dotted quarter, because a
        /// player attacking at a comfortable pace in the Drowned Nave's 6/8 (an eighth is 0.27 s) lands two or three
        /// eighths apart without aiming at either, and the first playtest of the pulse rule still recorded no sequences
        /// there: a chain should not depend on guessing which subdivision the game had in mind.
        /// </summary>
        public static bool IsPulseGap(string meter, long gap) => gap >= 1 && gap <= PulseBeats(meter);

        /// <summary>The beat gaps that keep a chain going in this meter (1 to the felt pulse).</summary>
        public static int[] PulseGaps(string meter)
        {
            int pulse = PulseBeats(meter);
            var gaps = new int[pulse];
            for (int i = 0; i < pulse; i++) gaps[i] = i + 1;
            return gaps;
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
