using System;
using System.Collections.Generic;

namespace ProjectHymn.Progression
{
    /// <summary>One stop on the road out of Saltmere.</summary>
    public sealed class RouteStop
    {
        public readonly string Id, Scene, Name, Meter, Requires, Blurb;
        /// <summary>A side road (no verse; opens along the way) rather than the story path.</summary>
        public readonly bool Side;
        /// <summary>Unranked (the Practice Yard).</summary>
        public readonly bool Practice;

        public RouteStop(string id, string scene, string name, string meter, string requires, string blurb, bool side = false, bool practice = false)
        {
            Id = id; Scene = scene; Name = name; Meter = meter; Requires = requires ?? ""; Blurb = blurb; Side = side; Practice = practice;
        }

        /// <summary>The verse this stop holds (1-based), or 0.</summary>
        public int Verse => Story.VerseOf(Id);
    }

    /// <summary>
    /// The way through the game (VS-5 Part B): the story path runs Practice Yard (optional), Cliffside, Belfry Ridge, the
    /// Summit, each opening when the one before it is cleared; side roads open along the way (the Arena after Cliffside,
    /// Starlit Spring after Belfry Ridge). Free play opens everything. Plain data and rules, tested; the gate's stage map
    /// draws it.
    /// </summary>
    public static class StageRoute
    {
        public static readonly RouteStop[] Path =
        {
            new RouteStop("Yard", "Yard", "Practice Yard", "4/4", "",
                "The basics with a metronome on screen: the beat, notes and sequences, guarding, parrying, dodging and one real bout. Unranked, and you can skip it.",
                practice: true),
            new RouteStop("Cliffside", "Cliffside", "Cliffside", "4/4", "",
                "The sea cliffs west of the village, where the first verse went. Bone knights on the tiers, wisps over the gaps."),
            new RouteStop("Belfry", "Belfry", "Belfry Ridge", "3/4", "Cliffside",
                "The ridge road to the old belfry, fought in threes. The dead dance there, and the bell keeps their time."),
            new RouteStop("Summit", "Summit", "The Summit", "4/4 · 3/4 · 7/8", "Belfry",
                "The Last Cantor's mountain. He counts in four, then three, then seven, and the ground itself strikes when he leaps."),
        };

        public static readonly RouteStop[] SideRoads =
        {
            new RouteStop("Arena", "Arena", "The Arena", "4/4", "Cliffside",
                "Three waves and no climb. No verse here, just the fight.", side: true),
            new RouteStop("Spring", "Spring", "Starlit Spring", "4/4", "Belfry",
                "An island among the stars with three platforms to fight across. Fall off and it costs a heart.", side: true),
        };

        public static IEnumerable<RouteStop> All
        {
            get
            {
                foreach (var r in Path) yield return r;
                foreach (var r in SideRoads) yield return r;
            }
        }

        public static RouteStop Find(string id)
        {
            foreach (var r in All) if (r.Id == id) return r;
            return null;
        }

        public static bool IsOpen(SaveData s, RouteStop r, bool freePlay = false) =>
            r != null && (freePlay || string.IsNullOrEmpty(r.Requires) || (s != null && s.HasCleared(r.Requires)));

        /// <summary>Cleared (the Yard counts as done once finished, or once Cliffside is cleared without it).</summary>
        public static bool IsDone(SaveData s, RouteStop r)
        {
            if (s == null || r == null) return false;
            return r.Practice ? s.tutorialDone || s.HasCleared("Cliffside") : s.HasCleared(r.Id);
        }

        /// <summary>The story stop to play next: the first open one on the path not yet done; null when the story is done.</summary>
        public static RouteStop Next(SaveData s, bool freePlay = false)
        {
            foreach (var r in Path)
                if (IsOpen(s, r, freePlay) && !IsDone(s, r)) return r;
            return null;
        }

        /// <summary>Why a stop is closed ("Clear Cliffside to open the road."), or empty when open.</summary>
        public static string LockReason(SaveData s, RouteStop r, bool freePlay = false)
        {
            if (IsOpen(s, r, freePlay)) return "";
            var need = Find(r.Requires);
            return $"Clear {(need != null ? need.Name : r.Requires)} to open the road.";
        }

        /// <summary>The short status under a stop: "best A", "not cleared", "done", "locked".</summary>
        public static string Status(SaveData s, RouteStop r, bool freePlay = false)
        {
            if (!IsOpen(s, r, freePlay)) return "locked";
            if (r.Practice) return s != null && s.tutorialDone ? "done" : "optional";
            return s != null && s.TryBestRank(r.Id, out StageRank rank) ? $"best {rank}" : "not cleared";
        }

        /// <summary>A clear time as m:ss.s.</summary>
        public static string Time(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int m = (int)(seconds / 60.0);
            return $"{m}:{seconds - m * 60.0:00.0}";
        }
    }
}
