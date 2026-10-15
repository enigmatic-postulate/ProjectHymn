using System;

namespace ProjectHymn.Progression
{
    /// <summary>
    /// Save slots (VS-5 Part B): three files side by side, the one-file save of earlier builds becoming slot 1. Names and
    /// the one-line summary a slot shows on the title screen. Plain C#, tested; GameSession does the file work.
    /// </summary>
    public static class SaveSlots
    {
        public const int Count = 3;
        /// <summary>The single save file before slots; moved into slot 1 the first time the game starts.</summary>
        public const string LegacyFile = "hymn_save.json";

        public static string FileName(int slot) => $"hymn_save_{Clamp(slot)}.json";

        public static int Clamp(int slot) => Math.Max(1, Math.Min(Count, slot));

        /// <summary>"Level 4 Fighter · 1 of 3 verses · 1:02:07"; a class id is shown as given when no name is passed.</summary>
        public static string Summary(SaveData d, string className = null)
        {
            if (d == null) return "Empty";
            int verses = Story.Chapter(d);
            string cls = string.IsNullOrEmpty(className) ? d.classId : className;
            string story = d.HasStory(Story.EndingSeen) ? "the Hymn restored" : $"{verses} of {Story.Verses} verses";
            return $"Level {d.level} {cls} · {story} · {PlayTime(d.playSeconds)}";
        }

        /// <summary>Play time as h:mm:ss (or m:ss under an hour).</summary>
        public static string PlayTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
        }

        /// <summary>The newest of up to <see cref="Count"/> save times (null = empty slot); 0 when every slot is empty.</summary>
        public static int Latest(DateTime?[] savedAt)
        {
            int best = 0;
            DateTime when = DateTime.MinValue;
            for (int i = 0; i < savedAt.Length && i < Count; i++)
                if (savedAt[i].HasValue && savedAt[i].Value > when) { when = savedAt[i].Value; best = i + 1; }
            return best;
        }
    }
}
