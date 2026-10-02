using System;
using System.Collections.Generic;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// When an enemy attacks, in musical terms: which measures (every N, with a phase) and which beat
    /// positions inside them. Offsets may be fractional (0.5 = the "and" after beat 1). Offsets that
    /// don't exist in the current meter (beat 4 of a 3/4 bar) are skipped.
    /// </summary>
    [Serializable]
    public struct EnemyPattern
    {
        [Min(1)] public int everyMeasures;
        [Tooltip("Which measure of the cycle acts (0 .. everyMeasures-1).")]
        [Min(0)] public int measurePhase;
        [Tooltip("Beat positions within the measure, 0 = downbeat. 1 and 3 = beats 2 and 4 (the backbeat).")]
        public float[] beatOffsets;

        public EnemyPattern(int everyMeasures, int measurePhase, params float[] beatOffsets)
        {
            this.everyMeasures = everyMeasures;
            this.measurePhase = measurePhase;
            this.beatOffsets = beatOffsets;
        }
    }

    /// <summary>One planned enemy attack with its one-measure telegraph.</summary>
    public readonly struct ScheduledAttack
    {
        public readonly double Time;            // song time the attack lands
        public readonly double TelegraphStart;  // one measure earlier
        public readonly double BeatPosition;    // absolute (fractional) beat position
        public readonly long Measure;
        public readonly int BeatsPerMeasure;

        public ScheduledAttack(double time, double telegraphStart, double beatPosition, long measure, int beatsPerMeasure)
        {
            Time = time; TelegraphStart = telegraphStart; BeatPosition = beatPosition;
            Measure = measure; BeatsPerMeasure = beatsPerMeasure;
        }

        public bool IsOnBeat => Math.Abs(BeatPosition - Math.Round(BeatPosition)) < 1e-6;

        /// <summary>0 at telegraph start, 1 when the attack lands.</summary>
        public double Progress(double now) =>
            Time <= TelegraphStart ? 1.0 : Math.Clamp((now - TelegraphStart) / (Time - TelegraphStart), 0.0, 1.0);
    }

    /// <summary>Plans enemy attacks on the tempo map. Pure C#.</summary>
    public static class EnemyRhythm
    {
        /// <summary>The first attack strictly after <paramref name="afterBeatPos"/>, or null if none within the search range.</summary>
        public static ScheduledAttack? Next(TempoMap map, EnemyPattern pattern, double afterBeatPos, int searchMeasures = 64)
        {
            if (pattern.beatOffsets == null || pattern.beatOffsets.Length == 0) return null;
            int every = Math.Max(1, pattern.everyMeasures);
            var offsets = new List<float>(pattern.beatOffsets);
            offsets.Sort();

            long firstMeasure = map.GetBeat((long)Math.Floor(afterBeatPos)).Measure;
            for (long k = 0; k <= searchMeasures; k++)
            {
                long measure = firstMeasure + k;
                if (Mod(measure - pattern.measurePhase, every) != 0) continue;

                long downbeat = map.MeasureStartBeat(measure);
                int beatsPerMeasure = map.GetBeat(downbeat).BeatsPerMeasure;
                foreach (var off in offsets)
                {
                    if (off < 0 || off >= beatsPerMeasure) continue;
                    double pos = downbeat + off;
                    if (pos <= afterBeatPos + 1e-6) continue;
                    return new ScheduledAttack(map.BeatToTime(pos), map.BeatToTime(pos - beatsPerMeasure), pos, measure, beatsPerMeasure);
                }
            }
            return null;
        }

        /// <summary>
        /// The first attack whose full one-measure telegraph is still ahead of <paramref name="now"/>,
        /// so a newly spawned enemy never attacks without warning.
        /// </summary>
        public static ScheduledAttack? FirstFullyTelegraphed(TempoMap map, EnemyPattern pattern, double now)
        {
            double pos = map.TimeToBeat(now);
            for (int guard = 0; guard < 256; guard++)
            {
                var a = Next(map, pattern, pos);
                if (a == null) return null;
                if (a.Value.TelegraphStart >= now - 1e-6) return a;
                pos = a.Value.BeatPosition;
            }
            return null;
        }

        static long Mod(long x, long n) => ((x % n) + n) % n;
    }

    public enum DefenseResult { Hit, Parried, Dodged, OutOfRange }

    /// <summary>
    /// Parry rule (GDD: Guard parries an enemy hit). Judged against the enemy's hit time rather than the
    /// player's beat grid, so eighth-note attacks are parryable too. Heavy attacks need tighter timing.
    /// Pure C#.
    /// </summary>
    public static class ParryJudge
    {
        public static double Window(TimingWindows w, bool heavy) => heavy ? w.Great : w.Good;

        public static bool IsParry(double guardPressTime, double hitTime, TimingWindows w, bool heavy) =>
            Math.Abs(guardPressTime - hitTime) <= Window(w, heavy);

        /// <summary>
        /// Decide what happens to an attack once its parry window has closed.
        /// Priority: out of range, then parry, then i-frame dodge, otherwise hit.
        /// </summary>
        public static DefenseResult Resolve(bool inRangeAtHit, bool invulnerableAtHit, double lastGuardPress,
                                            double hitTime, TimingWindows w, bool heavy)
        {
            if (!inRangeAtHit) return DefenseResult.OutOfRange;
            if (IsParry(lastGuardPress, hitTime, w, heavy)) return DefenseResult.Parried;
            if (invulnerableAtHit) return DefenseResult.Dodged;
            return DefenseResult.Hit;
        }
    }
}
