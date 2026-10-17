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
        /// <summary>The region it lies in: 0 = the road from Saltmere, 1 = the Drowned Nave (VS-6).</summary>
        public readonly int Region;

        public RouteStop(string id, string scene, string name, string meter, string requires, string blurb, bool side = false, bool practice = false, int region = 0)
        {
            Id = id; Scene = scene; Name = name; Meter = meter; Requires = requires ?? ""; Blurb = blurb; Side = side; Practice = practice; Region = region;
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

        /// <summary>The Drowned Nave's story path (VS-6), under Saltmere's harbour: it opens once the Summit is cleared.</summary>
        public static readonly RouteStop[] DeepPath =
        {
            new RouteStop("Nave", "Nave", "Kelp Nave", "6/8", "Summit",
                "The cathedral's flooded nave, rocking in sixes. Barnacled knights, bell jellies over the choir steps, a chorister in the transept.",
                region: 1),
            new RouteStop("Loft", "Loft", "Choir Loft", "6/8", "Nave",
                "Up the organ pipes to the bell gallery. The choristers sing from the galleries, and their held notes ring along the floor.",
                region: 1),
            new RouteStop("Apse", "Apse", "The Sunken Apse", "6/8 · 3/4 · 9/8", "Loft",
                "The Drowned Precentor keeps the apse. He rocks in six, bends the bar into three, stretches it to nine, and tolls the great bell.",
                region: 1),
        };

        /// <summary>The regions' names, map titles and hub scenes.</summary>
        public static readonly string[] RegionNames = { "Saltmere", "the Drowned Nave" };
        public static readonly string[] RegionTitles = { "THE ROAD FROM SALTMERE", "THE DROWNED NAVE" };
        public static readonly string[] RegionHubs = { "Village", "AirBell" };
        public const int Regions = 2;

        /// <summary>A region's story path.</summary>
        public static RouteStop[] PathOf(int region) => region == 1 ? DeepPath : Path;

        /// <summary>A region's side roads (the Drowned Nave has none yet).</summary>
        public static IEnumerable<RouteStop> SideRoadsOf(int region)
        {
            foreach (var r in SideRoads) if (r.Region == region) yield return r;
        }

        /// <summary>A region is open once its first story stop is (the Drowned Nave after the Summit).</summary>
        public static bool RegionOpen(SaveData s, int region, bool freePlay = false) =>
            region <= 0 || (region < Regions && IsOpen(s, PathOf(region)[0], freePlay));

        /// <summary>The region a scene belongs to (a stage or a hub), or -1.</summary>
        public static int RegionOfScene(string scene)
        {
            for (int i = 0; i < Regions; i++) if (RegionHubs[i] == scene) return i;
            foreach (var r in All) if (r.Scene == scene) return r.Region;
            return -1;
        }

        public static IEnumerable<RouteStop> All
        {
            get
            {
                foreach (var r in Path) yield return r;
                foreach (var r in SideRoads) yield return r;
                foreach (var r in DeepPath) yield return r;
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

        /// <summary>The story stop to play next: the first open one on the paths (Saltmere's, then the deep's) not yet done; null when the story is done.</summary>
        public static RouteStop Next(SaveData s, bool freePlay = false)
        {
            foreach (var r in Path)
                if (IsOpen(s, r, freePlay) && !IsDone(s, r)) return r;
            foreach (var r in DeepPath)
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
