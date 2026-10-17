using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// VS-6's boss, the Drowned Precentor: the cathedral's choirmaster, drowned at his lectern, who still sets the pace for
    /// "Canticle of the Precentor". He fights like the Bone Cantor (every attack counted in for a bar of the meter it lands
    /// in, never across a change; he lifts his hand through the bar before one), but his song bends the same bar three
    /// ways: 6/8, a 3/4 hemiola, then 9/8 (<see cref="DrownedPatterns.Precentor"/>). Three verses by health: the second
    /// adds the Great Bell, a toll along the floor no guard stops; the third, twin bells and strings across two bars.
    /// </summary>
    public class DrownedPrecentor : BoneCantor
    {
        protected override List<BossMove> Moves() => DrownedPatterns.Precentor();

        protected override string SecondVerseToast =>
            "The Precentor's second verse: the barcarolle, the riptide, and the Great Bell (no guard holds it: jump or dodge)";

        protected override string FallToast => "The Precentor lowers his hand. The nave falls quiet. The way up is open.";

        protected override string ThirdVerseToast =>
            "The Precentor's last verse: twin bells, the hemiola run, twos against nine. Count every eighth.";

        protected override Color RingColor => new Color(0.45f, 0.95f, 1f, 0.9f);

        /// <summary>
        /// The felt pulse of the attack under way (or of now): a dotted quarter (three eighths) in 6/8 and 9/8, the quarter
        /// itself through the hemiola, so his swings keep their speed as the meter turns.
        /// </summary>
        protected override float PoseScale
        {
            get
            {
                var map = Map;
                if (map == null) return 3f;
                double at = Plan.IsValid ? Plan.FirstHitBeat : BeatNow;
                var info = map.GetBeat((long)System.Math.Floor(at));
                return info.BeatsPerMeasure == 3 || info.BeatsPerMeasure == 4 ? 1f : 3f;
            }
        }
    }
}
