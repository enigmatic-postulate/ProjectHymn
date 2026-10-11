using System.Collections.Generic;
using ProjectHymn.Combat;

namespace ProjectHymn.Gameplay
{
    /// <summary>The sequence library as the player has it: names for messages, and what plays in a meter.</summary>
    public static class Sequences
    {
        /// <summary>"Bulwark, Counterpoint and Lantern Step" from save keys.</summary>
        public static string Names(IEnumerable<string> keys)
        {
            var names = new List<string>();
            foreach (var k in keys) { var e = SequenceLibrary.Find(k); names.Add(e != null ? e.Name : k); }
            if (names.Count <= 1) return names.Count == 1 ? names[0] : "";
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }

        public static bool Knows(string key) =>
            !GameSession.Exists || GameSession.Instance.FreePlay || GameSession.Instance.Data.Knows(key);

        /// <summary>The sequences the player knows that play in <paramref name="meter"/> ("3/4").</summary>
        public static List<SequenceEntry> KnownIn(string meter)
        {
            var list = new List<SequenceEntry>();
            foreach (var e in SequenceLibrary.All) if (e.PlaysIn(meter) && Knows(e.Key)) list.Add(e);
            return list;
        }
    }
}
