using System;
using System.Collections.Generic;

namespace ProjectHymn.Progression
{
    /// <summary>Where a class sits: the Fighter is the low (D rank) bracket, the Minstrel the base, the rest high.</summary>
    public enum ClassBracket { Low, Base, High }

    /// <summary>How a run defended: mostly guarding (parries and blocks), mostly dodging, or neither.</summary>
    public enum PlayLean { Balanced, Guard, Dodge }

    /// <summary>The parts of a class the progression rules need (Unity's ClassDefinition maps onto it).</summary>
    public sealed class ClassSpec
    {
        public string Id;
        public string Name;
        public ClassBracket Bracket;
        /// <summary>A level-down below this costs the class (back to the Minstrel).</summary>
        public int MinLevel;
        /// <summary>The play style that earns the class at a class check (high classes).</summary>
        public PlayLean Lean;
        /// <summary>Weapon ids the class can equip, in order of preference.</summary>
        public IReadOnlyList<string> Weapons = Array.Empty<string>();

        public ClassSpec(string id, string name, ClassBracket bracket, int minLevel = 0, PlayLean lean = PlayLean.Balanced, params string[] weapons)
        {
            Id = id; Name = name; Bracket = bracket; MinLevel = minLevel; Lean = lean; Weapons = weapons ?? Array.Empty<string>();
        }
    }

    /// <summary>The classes on offer at a class check. One choice means nothing to choose.</summary>
    public sealed class ClassOffer
    {
        public int Level;
        public readonly List<string> Choices = new List<string>();
        public string Recommended;
        public string Reason;
        public bool HasChoice => Choices.Count > 1;
    }

    /// <summary>What one stage clear did to the save, for the results screen.</summary>
    public sealed class RunOutcome
    {
        public StageRank Rank;
        public bool NewBest;
        public int XpGained;
        public int LevelBefore, LevelAfter;
        public readonly List<int> LevelsGained = new List<int>();
        public readonly List<string> LevelDownReasons = new List<string>();
        public string ClassBefore, ClassAfter;
        /// <summary>Why the class changed without a choice (empty when it didn't).</summary>
        public string ClassChangeReason = "";
        /// <summary>A class check with a real choice; the player picks before leaving the results.</summary>
        public ClassOffer Offer;
        /// <summary>The class check's verdict when it gave no choice (kept, held or changed), for the screen.</summary>
        public string CheckNote = "";
        public readonly List<string> WeaponsUnlocked = new List<string>();
        public int XpIntoLevel, XpToNext;
    }

    /// <summary>
    /// Rank &amp; Class (GDD update 3, slice 6). Pure C#; every number is a placeholder until playtested.
    /// <list type="bullet">
    /// <item>A stage clear earns experience by rank plus a little per enemy. Levels need 100 × level experience.</item>
    /// <item>Level-downs: losing too many hearts, or attacking off the beat too often, each drop one level. Experience
    /// is never lost, and a run that drops a level earns none.</item>
    /// <item>Class checks at levels 6, 8 and 10 (then every 5): S or A lets a class upgrade, B holds it, C can't
    /// progress it and costs one (a Minstrel becomes a Fighter; a high class falls back to the Minstrel). The mastery
    /// score decides which high classes are offered, and the run's lean (guarding or dodging) which one fits.
    /// A Minstrel who reaches the last check (10) without an upgrade becomes a Fighter; only S or A moves a Fighter up.</item>
    /// <item>A level-down below a class's minimum level costs the class.</item>
    /// <item>The first B or better unlocks the greatsword.</item>
    /// </list>
    /// </summary>
    public sealed class ProgressionRules
    {
        public const string Minstrel = "minstrel", Fighter = "fighter", Warden = "warden", Crusader = "crusader";
        public const string Sword = "sword", Greatsword = "greatsword";

        /// <summary>Experience for a clear at S, A, B, C.</summary>
        public int[] XpByRank = { 500, 400, 300, 200 };
        public int XpPerEnemy = 20;
        public int XpPerLevelStep = 100;
        public int[] CheckLevels = { 6, 8, 10 };
        /// <summary>After the last listed check level, a check every this many levels.</summary>
        public int CheckEvery = 5;
        public int HeartsLostForLevelDown = 3;
        public double OffBeatShareForLevelDown = 0.4;
        public int MinAttacksForOffBeat = 6;
        /// <summary>Mastery for the high class that fits the run's lean.</summary>
        public double MasteryForHigh = 70.0;
        /// <summary>Mastery for every high class to be offered.</summary>
        public double MasteryForEveryHigh = 85.0;
        public StageRank GreatswordAt = StageRank.B;

        public static ProgressionRules Default => new ProgressionRules();

        public int XpToNext(int level) => XpPerLevelStep * Math.Max(1, level);
        public int FinalCheckLevel => CheckLevels.Length > 0 ? CheckLevels[CheckLevels.Length - 1] : 10;

        public bool IsCheckLevel(int level)
        {
            foreach (int l in CheckLevels) if (l == level) return true;
            int last = FinalCheckLevel;
            return CheckEvery > 0 && level > last && (level - last) % CheckEvery == 0;
        }

        public static PlayLean LeanOf(PlaystyleLog log)
        {
            int guard = log.Parries + log.Blocks;
            if (guard > log.Dodges) return PlayLean.Guard;
            if (log.Dodges > guard) return PlayLean.Dodge;
            return PlayLean.Balanced;
        }

        public static ClassSpec Find(IReadOnlyList<ClassSpec> classes, string id)
        {
            if (classes != null) foreach (var c in classes) if (c != null && c.Id == id) return c;
            return null;
        }

        /// <summary>The weapon the class should hold: the current one if it may, else its first unlocked weapon.</summary>
        public static string WeaponFor(SaveData s, ClassSpec cls)
        {
            if (cls == null || cls.Weapons.Count == 0) return s.HasWeapon(s.weaponId) ? s.weaponId : Sword;
            foreach (var w in cls.Weapons) if (w == s.weaponId && s.HasWeapon(w)) return w;
            foreach (var w in cls.Weapons) if (s.HasWeapon(w)) return w;
            return cls.Weapons[0];
        }

        /// <summary>Set the class (and a weapon it can hold).</summary>
        public static void SetClass(SaveData s, string classId, IReadOnlyList<ClassSpec> classes)
        {
            s.classId = classId;
            s.weaponId = WeaponFor(s, Find(classes, classId));
        }

        // ---------------- A stage clear ----------------

        public RunOutcome Apply(SaveData s, string stage, StageResult result, PlaystyleLog log, IReadOnlyList<ClassSpec> classes)
        {
            var o = new RunOutcome { Rank = result.Rank, LevelBefore = s.level, ClassBefore = s.classId };
            o.NewBest = s.RecordStage(stage, result.Rank, result.Seconds);

            if (result.Rank <= GreatswordAt && s.UnlockWeapon(Greatsword)) o.WeaponsUnlocked.Add(Greatsword);

            if (log.HeartsLost >= HeartsLostForLevelDown)
                o.LevelDownReasons.Add($"lost {log.HeartsLost} hearts");
            if (log.Attacks >= MinAttacksForOffBeat && log.OffBeat / (double)log.Attacks >= OffBeatShareForLevelDown)
                o.LevelDownReasons.Add($"{log.OffBeat / (double)log.Attacks:P0} of attacks off the beat");

            if (o.LevelDownReasons.Count > 0)
            {
                s.level = Math.Max(1, s.level - o.LevelDownReasons.Count);
                var cls = Find(classes, s.classId);
                if (cls != null && s.level < cls.MinLevel)
                {
                    SetClass(s, Minstrel, classes);
                    o.ClassChangeReason = $"fell below level {cls.MinLevel}: back to the Minstrel";
                }
            }
            else
            {
                o.XpGained = XpByRank[(int)result.Rank] + XpPerEnemy * log.EnemiesDefeated;
                s.xp += o.XpGained;
                while (s.xp >= XpToNext(s.level))
                {
                    s.xp -= XpToNext(s.level);
                    s.level++;
                    o.LevelsGained.Add(s.level);
                }
                int check = 0;
                foreach (int l in o.LevelsGained) if (IsCheckLevel(l)) check = l;
                if (check > 0)
                {
                    var offer = Check(s.classId, result.Rank, log.MasteryScore, LeanOf(log), check, classes);
                    if (offer.HasChoice) o.Offer = offer;
                    else
                    {
                        string to = offer.Choices.Count > 0 ? offer.Choices[0] : s.classId;
                        if (to != s.classId) { SetClass(s, to, classes); o.ClassChangeReason = offer.Reason; }
                        else o.CheckNote = offer.Reason;
                    }
                }
            }

            o.LevelAfter = s.level;
            o.ClassAfter = s.classId;
            o.XpIntoLevel = s.xp;
            o.XpToNext = XpToNext(s.level);
            return o;
        }

        // ---------------- A class check ----------------

        public ClassOffer Check(string current, StageRank rank, double mastery, PlayLean lean, int level, IReadOnlyList<ClassSpec> classes)
        {
            var offer = new ClassOffer { Level = level, Recommended = current };
            var cur = Find(classes, current);
            var bracket = cur != null ? cur.Bracket : ClassBracket.Base;

            if (rank <= StageRank.A)
            {
                var highs = new List<ClassSpec>();
                if (classes != null)
                    foreach (var c in classes)
                    {
                        if (c == null || c.Bracket != ClassBracket.High || c.MinLevel > level) continue;
                        bool fits = lean == PlayLean.Balanced || c.Lean == PlayLean.Balanced || c.Lean == lean;
                        if (mastery >= MasteryForEveryHigh || (mastery >= MasteryForHigh && fits)) highs.Add(c);
                    }
                foreach (var c in highs)
                    if (c.Id != current && (lean != PlayLean.Balanced && c.Lean == lean)) { offer.Recommended = c.Id; break; }
                foreach (var c in highs) if (c.Id != current) offer.Choices.Add(c.Id);
                if (bracket == ClassBracket.Low) offer.Choices.Add(Minstrel);
                offer.Choices.Add(current);
                if (offer.Recommended == current && offer.Choices.Count > 1) offer.Recommended = offer.Choices[0];
                offer.Reason = highs.Count > 0
                    ? $"{rank} rank and mastery {mastery:0}: a class can rise"
                    : bracket == ClassBracket.Low
                        ? $"{rank} rank: the Fighter can move up"
                        : $"{rank} rank, but mastery {mastery:0} is below {MasteryForHigh:0}: no new class yet";
                return offer;
            }

            if (rank == StageRank.B)
            {
                if (bracket == ClassBracket.Base && level >= FinalCheckLevel)
                {
                    offer.Choices.Add(Fighter);
                    offer.Reason = $"the Minstrel reached level {level} without an S or A: now a Fighter";
                }
                else
                {
                    offer.Choices.Add(current);
                    offer.Reason = "B rank holds the class";
                }
                return offer;
            }

            // C
            switch (bracket)
            {
                case ClassBracket.Base:
                    offer.Choices.Add(Fighter);
                    offer.Reason = "a C clear at a class check: the Minstrel becomes a Fighter";
                    break;
                case ClassBracket.High:
                    offer.Choices.Add(Minstrel);
                    offer.Reason = "a C clear at a class check costs the class";
                    break;
                default:
                    offer.Choices.Add(current);
                    offer.Reason = "C rank: the Fighter stays a Fighter";
                    break;
            }
            return offer;
        }
    }
}
