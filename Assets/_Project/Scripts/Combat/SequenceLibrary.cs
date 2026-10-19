using System;
using System.Collections.Generic;

namespace ProjectHymn.Combat
{
    /// <summary>What a sequence's finisher does besides damage.</summary>
    public enum SequenceEffect
    {
        None,
        /// <summary>Rising Verse: the enemy is thrown up and can't attack until it lands (a short stagger).</summary>
        Launch,
        /// <summary>Bulwark: the enemy is shoved back and its next hit comes a beat later.</summary>
        Push,
    }

    /// <summary>One entry of the library: a pattern, the class tree it belongs to and its home meter.</summary>
    public sealed class SequenceEntry
    {
        public readonly string Key, Name, Tree, Meter, Note;
        public readonly NoteButton[] Notes;
        public readonly float Finisher;
        public readonly SequenceEffect Effect;
        public readonly uint Color;

        public SequenceEntry(string key, string name, string tree, string meter, string notes, float finisher, uint color,
                             SequenceEffect effect = SequenceEffect.None, string note = "")
        {
            Key = key; Name = name; Tree = tree; Meter = meter; Finisher = finisher; Color = color; Effect = effect; Note = note;
            var parts = notes.Split('-');
            Notes = new NoteButton[parts.Length];
            for (int i = 0; i < parts.Length; i++) Notes[i] = parts[i] == "2" ? NoteButton.Two : NoteButton.One;
        }

        public int Length => Notes.Length;
        public string NotesText => string.Join("-", Array.ConvertAll(Notes, n => ((int)n).ToString()));
        public bool PlaysIn(string meter) => string.IsNullOrEmpty(Meter) || Meter == meter;
    }

    /// <summary>
    /// The sequence library (GDD update 18). Every sequence sits in one class's tree and has a home meter: it only plays
    /// in that meter, so a 4/4 sequence does nothing in a 3/4 phase. Taking a class learns its tree, and a learned
    /// sequence works for every class (multiclassing grows the library). The finisher scales with length. No two
    /// sequences in one meter share a pattern, so "longest match wins" never has to choose. Names, patterns and
    /// multipliers are drafts. VS-6 adds the Drowned Nave's compound meters: 6/8 (Swell, Barcarolle, Undertow) and 9/8
    /// (Riptide), each counted in eighths. VS-5 Part D fills the gaps the playtest found: 3/4 had one sequence and 6/8
    /// none for the Fighter or Crusader, so most classes could never land a sequence in the Belfry or the Drowned Nave.
    /// Every class now has at least one sequence in every meter the game uses. The slice builds the Minstrel, Fighter, Warden and Crusader trees; Tempest moved to the
    /// Balladeer, a later class, so nobody learns it yet.
    /// </summary>
    public static class SequenceLibrary
    {
        public const string Common = "4/4", Waltz = "3/4", Seven = "7/8", Six = "6/8", Nine = "9/8";
        public const string Minstrel = "minstrel", Fighter = "fighter", Warden = "warden", Crusader = "crusader", Balladeer = "balladeer";

        public static readonly IReadOnlyList<SequenceEntry> All = new[]
        {
            // Minstrel
            new SequenceEntry("CrossingCuts", "Crossing Cuts", Minstrel, Common, "1-2-1", 1.8f, 0xffd27a),
            new SequenceEntry("Powerslice", "Powerslice", Minstrel, Common, "2-2", 1.6f, 0xff7a5a),
            new SequenceEntry("RisingVerse", "Rising Verse", Minstrel, Common, "1-1-2", 1.7f, 0x8ad8ff, SequenceEffect.Launch, "launches the enemy"),
            // Minstrel, VS-6: the Drowned Nave's 6/8, heavy on each big beat (1 and 4); Barcarolle carries on from Swell
            new SequenceEntry("Swell", "Swell", Minstrel, Six, "2-1-1", 1.8f, 0x7ad0e8),
            new SequenceEntry("Barcarolle", "Barcarolle", Minstrel, Six, "2-1-1-2-1-1", 2.4f, 0x9af0ff),
            // Minstrel, VS-5 Part D: one sequence per meter, so every class can play a sequence in every region
            new SequenceEntry("Roundel", "Roundel", Minstrel, Waltz, "1-1-2", 1.7f, 0xffc48a),
            new SequenceEntry("LimpingVerse", "Limping Verse", Minstrel, Seven, "1-1-2", 1.8f, 0xffb0d0),
            new SequenceEntry("TideVerse", "Tide Verse", Minstrel, Nine, "1-1-2", 1.9f, 0x8ae8ff, SequenceEffect.Launch, "launches the enemy"),
            // Fighter (4/4 only, by design)
            new SequenceEntry("Drill", "Drill", Fighter, Common, "1-1-1-1", 1.8f, 0xd8d8d8),
            new SequenceEntry("Hammerfall", "Hammerfall", Fighter, Common, "2-2-2", 2.0f, 0xff9a4a),
            new SequenceEntry("IronCount", "Iron Count", Fighter, Common, "2-1-2-1", 2.0f, 0xc0c8d8),
            new SequenceEntry("StampAndTurn", "Stamp and Turn", Fighter, Waltz, "2-2-1", 1.8f, 0xe0b070),
            new SequenceEntry("Tidehammer", "Tidehammer", Fighter, Six, "2-2", 1.6f, 0xffa07a),
            new SequenceEntry("SevenHammers", "Seven Hammers", Fighter, Seven, "2-2-1", 1.9f, 0xffd0a0),
            new SequenceEntry("Ninefold", "Ninefold", Fighter, Nine, "2-2-2", 2.0f, 0xd8a8ff),
            // Warden
            new SequenceEntry("Bulwark", "Bulwark", Warden, Common, "2-1-1", 1.7f, 0x9ab8ff, SequenceEffect.Push, "the bash pushes the enemy back a beat"),
            new SequenceEntry("Counterpoint", "Counterpoint", Warden, Common, "1-2-2-1", 2.0f, 0x7ac8ff),
            new SequenceEntry("LanternStep", "Lantern Step", Warden, Waltz, "1-2-2", 1.8f, 0xffe6a0),
            new SequenceEntry("Undertow", "Undertow", Warden, Six, "1-1-2", 1.8f, 0x5ab0d8, SequenceEffect.Push, "the last blow pulls the enemy off its feet and back a beat"),
            new SequenceEntry("HobbleGuard", "Hobble Guard", Warden, Seven, "1-2-1", 1.8f, 0xa0d0ff),
            new SequenceEntry("Breakwater", "Breakwater", Warden, Nine, "1-2-2", 1.9f, 0x70c0e0, SequenceEffect.Push, "the shoulder throws the enemy back a beat"),
            // Crusader
            new SequenceEntry("Brandish", "Brandish", Crusader, Common, "2-1", 1.5f, 0xffc860),
            new SequenceEntry("Judgement", "Judgement", Crusader, Common, "2-2-1-2", 2.2f, 0xfff0b0),
            new SequenceEntry("Crusade", "Crusade", Crusader, Seven, "2-1-2-1-2-1-2", 2.6f, 0xffffff),
            new SequenceEntry("Riptide", "Riptide", Crusader, Nine, "2-1-1-2-1-1-2-1-1", 3.0f, 0x40d8ff),
            new SequenceEntry("Processional", "Processional", Crusader, Waltz, "2-1-2", 1.9f, 0xffe0a0),
            new SequenceEntry("Keelhaul", "Keelhaul", Crusader, Six, "2-1-2", 1.9f, 0x60c8e8),
            // Balladeer (a later class): Tempest moved here
            new SequenceEntry("Tempest", "Tempest", Balladeer, Common, "1-1-1-2-1-1-1", 2.5f, 0x7affc8),
        };

        /// <summary>The classes whose trees are in the build, in the Songbook's order.</summary>
        public static readonly string[] Trees = { Minstrel, Fighter, Warden, Crusader, Balladeer };

        public static SequenceEntry Find(string key)
        {
            foreach (var e in All) if (e.Key == key) return e;
            return null;
        }

        public static List<SequenceEntry> Tree(string classId)
        {
            var list = new List<SequenceEntry>();
            foreach (var e in All) if (e.Tree == classId) list.Add(e);
            return list;
        }

        public static List<string> TreeKeys(string classId) => Tree(classId).ConvertAll(e => e.Key);

        /// <summary>The finisher range the GDD gives for a length: 2 notes 1.5-1.6 ... 8-9 notes 3.0.</summary>
        public static (float min, float max) FinisherBand(int notes)
        {
            if (notes <= 2) return (1.5f, 1.6f);
            if (notes == 3) return (1.7f, 2.0f);       // 1.7-1.8 in the rule; the draft table gives Hammerfall (2-2-2) 2.0
            if (notes == 4) return (1.8f, 2.2f);       // 2.0 in the rule; the draft table runs 1.8 (Drill) to 2.2 (Judgement)
            if (notes <= 6) return (2.2f, 2.4f);
            if (notes == 7) return (2.5f, 2.6f);       // 2.5 in the rule; Crusade, the 7/8 showpiece, is 2.6
            return (3.0f, 3.0f);
        }
    }
}
