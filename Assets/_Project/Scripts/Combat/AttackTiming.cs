using System;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// Frame data for one move, in seconds. Startup and active frames never change; recovery is what
    /// timing accuracy shortens. Authored per move so animations can be built to match.
    /// </summary>
    [Serializable]
    public struct FrameData
    {
        [Min(0f)] public float startup;
        [Min(0f)] public float active;
        [Min(0f)] public float recovery;

        public FrameData(float startup, float active, float recovery)
        {
            this.startup = startup; this.active = active; this.recovery = recovery;
        }

        public float Total => startup + active + recovery;
    }

    /// <summary>
    /// How long an action locks the player out, and when it can be cancelled into the next one.
    /// </summary>
    public readonly struct ActionLock
    {
        public readonly double Start;        // song time the action began
        public readonly double ActiveEnd;    // hit frames done
        public readonly double RecoveryEnd;  // fully recovered: any action allowed
        public readonly double CancelOpen;   // from here the next action may cancel recovery (+inf = no cancel)
        public readonly bool Flourish;       // Perfect: play the flourish variant of the move
        public readonly bool Winded;         // off-beat without enough stamina: slower recovery

        public ActionLock(double start, double activeEnd, double recoveryEnd, double cancelOpen, bool flourish, bool winded)
        {
            Start = start; ActiveEnd = activeEnd; RecoveryEnd = recoveryEnd; CancelOpen = cancelOpen;
            Flourish = flourish; Winded = winded;
        }

        public static ActionLock None => new ActionLock(double.MinValue, double.MinValue, double.MinValue, double.PositiveInfinity, false, false);

        public bool HasCancel => !double.IsPositiveInfinity(CancelOpen);

        /// <summary>The earliest time a new action is allowed.</summary>
        public double FreeAt => Math.Min(RecoveryEnd, CancelOpen);

        public bool CanAct(double time) => time >= FreeAt;
    }

    /// <summary>
    /// GDD rule: accuracy changes animation flow more than damage. On-beat actions recover faster and
    /// open a cancel window lined up with the next beat, so the follow-up naturally lands on time.
    /// Off-beat actions play their full recovery with no cancel, so chains end on their own. Pure C#.
    /// </summary>
    public static class AttackTiming
    {
        public const float WindedRecoveryScale = 1.75f;

        public static float RecoveryScale(Judgement j) => j switch
        {
            Judgement.Perfect => 0.4f,
            Judgement.Great => 0.6f,
            _ => 1.0f,
        };

        /// <param name="inputTime">Song time of the press (calibration-corrected).</param>
        /// <param name="nextBeatTime">Song time of the beat after the one this input was judged against.</param>
        public static ActionLock Compute(FrameData frames, Judgement j, bool winded, double inputTime,
                                         double nextBeatTime, TimingWindows windows)
        {
            double activeEnd = inputTime + frames.startup + frames.active;
            double recoveryScale = RecoveryScale(j) * (winded ? WindedRecoveryScale : 1f);
            double recoveryEnd = activeEnd + frames.recovery * recoveryScale;

            double cancelOpen = j switch
            {
                // Perfect/Great: cancel opens as early as the next beat's Good window.
                Judgement.Perfect or Judgement.Great => nextBeatTime - windows.Good,
                // Good: a later cancel, from the next beat's Great window.
                Judgement.Good => nextBeatTime - windows.Great,
                _ => double.PositiveInfinity,
            };
            // Can never cancel before the hit frames have played.
            if (!double.IsPositiveInfinity(cancelOpen)) cancelOpen = Math.Max(cancelOpen, activeEnd);

            return new ActionLock(inputTime, activeEnd, recoveryEnd, cancelOpen, j == Judgement.Perfect, winded);
        }
    }
}
