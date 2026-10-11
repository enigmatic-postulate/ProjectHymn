using System;
using System.Collections.Generic;

namespace ProjectHymn.Movement
{
    /// <summary>
    /// The floors of a stage (the ground and the one-way platforms) and how an enemy hops between them (the brawl stage,
    /// Starlit Spring). Pure C#: which floor a point stands on, and where a hop from one floor to another lands. A hop
    /// is one jump up, or a drop down, of limited reach; a floor too far away is reached in steps.
    /// </summary>
    public sealed class FloorMap
    {
        public readonly struct Floor
        {
            public readonly string Name;
            public readonly float XMin, XMax, Top;
            public readonly bool OneWay;
            public Floor(string name, float xMin, float xMax, float top, bool oneWay)
            {
                Name = name; XMin = xMin; XMax = xMax; Top = top; OneWay = oneWay;
            }
            public bool IsValid => Name != null;
            public float Width => XMax - XMin;
            public bool Spans(float x, float margin = 0f) => x >= XMin + margin && x <= XMax - margin;
        }

        /// <summary>How far in from a floor's ends an enemy stands.</summary>
        public float EdgeMargin = 0.8f;
        /// <summary>The highest a hop climbs (a little under the player's double jump) and the furthest it goes sideways.</summary>
        public float MaxRise = 3.0f, MaxReach = 6f;
        /// <summary>A landing keeps this far from the player (enemies fight at arm's length, not on top of you).</summary>
        public float KeepAway = 1.4f;

        public readonly IReadOnlyList<Floor> Floors;

        public FloorMap(IReadOnlyList<Floor> floors) { Floors = floors ?? Array.Empty<Floor>(); }

        /// <summary>
        /// The floor under a point: the highest one spanning <paramref name="x"/> whose top is at or below
        /// <paramref name="y"/> (plus a little tolerance). Default when there is none (over the void).
        /// </summary>
        public Floor FloorAt(float x, float y, float tolerance = 0.35f)
        {
            Floor best = default;
            foreach (var f in Floors)
                if (f.Spans(x) && f.Top <= y + tolerance && (!best.IsValid || f.Top > best.Top)) best = f;
            return best;
        }

        public int IndexOf(Floor f)
        {
            for (int i = 0; i < Floors.Count; i++) if (Floors[i].Name == f.Name) return i;
            return -1;
        }

        /// <summary>Whether a hop can go straight from one floor to the other (one jump up, or a drop).</summary>
        public bool CanHop(Floor from, Floor to)
        {
            if (!from.IsValid || !to.IsValid || from.Name == to.Name) return false;
            if (to.Top - from.Top > MaxRise) return false;
            float gap = Math.Max(0f, Math.Max(to.XMin - from.XMax, from.XMin - to.XMax));
            return gap <= MaxReach;
        }

        /// <summary>
        /// The next floor on the way from <paramref name="from"/> to <paramref name="target"/>: the target itself if one
        /// hop reaches it, else a floor that can be hopped to and then reaches the target (one step in between is enough
        /// for any stage so far). Default when there is no way.
        /// </summary>
        public Floor NextStep(Floor from, Floor target)
        {
            if (CanHop(from, target)) return target;
            Floor best = default;
            float bestCost = float.MaxValue;
            foreach (var f in Floors)
            {
                if (!CanHop(from, f) || !CanHop(f, target)) continue;
                float cost = Math.Abs(Center(f) - Center(from)) + Math.Abs(Center(target) - Center(f));
                if (cost < bestCost) { bestCost = cost; best = f; }
            }
            return best;
        }

        static float Center(Floor f) => (f.XMin + f.XMax) * 0.5f;

        /// <summary>
        /// Where a hop onto <paramref name="to"/> lands: toward <paramref name="playerX"/> (when the player stands on it,
        /// <see cref="KeepAway"/> short of them, on the enemy's side), within the floor's margins and within reach of where
        /// the enemy jumps from.
        /// </summary>
        public float LandingX(Floor to, float fromX, float playerX, bool playerOnIt)
        {
            float lo = to.XMin + EdgeMargin, hi = to.XMax - EdgeMargin;
            if (lo > hi) lo = hi = (to.XMin + to.XMax) * 0.5f;
            float want;
            if (playerOnIt)
            {
                float side = fromX <= playerX ? -1f : 1f;
                want = playerX + side * KeepAway;
                if (want < lo || want > hi) want = playerX - side * KeepAway;    // no room on our side: the far side
            }
            else want = Clamp(fromX, lo, hi);                                  // the nearest point of the floor
            want = Clamp(want, fromX - MaxReach, fromX + MaxReach);
            return Clamp(want, lo, hi);
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>The bounds an enemy keeps to on a floor (its ends, less the margin).</summary>
        public (float min, float max) Bounds(Floor f)
        {
            float lo = f.XMin + EdgeMargin, hi = f.XMax - EdgeMargin;
            return lo <= hi ? (lo, hi) : ((f.XMin + f.XMax) * 0.5f, (f.XMin + f.XMax) * 0.5f);
        }

        /// <summary>Height along a hop at 0..1: a jump arc that clears both floors by <paramref name="clearance"/>.</summary>
        public static float HopHeight(float fromY, float toY, float t, float clearance = 1.1f)
        {
            float peak = Math.Max(fromY, toY) + clearance;
            // A parabola through (0, fromY), (tp, peak), (1, toY), with the apex placed so both ends meet.
            float a = (float)Math.Sqrt(peak - fromY), b = (float)Math.Sqrt(peak - toY);
            float tp = a / (a + b);
            float y = t <= tp
                ? peak - (peak - fromY) * Sq((tp - t) / tp)
                : peak - (peak - toY) * Sq((t - tp) / (1f - tp));
            return y;
        }

        static float Sq(float x) => x * x;
    }
}
