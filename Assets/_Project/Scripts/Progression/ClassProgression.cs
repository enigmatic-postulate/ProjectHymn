using System;
using System.Collections.Generic;

namespace ProjectHymn.Progression
{
    /// <summary>Where a class sits: the Fighter is the automatic progression, the Minstrel the base, the rest high.</summary>
    public enum ClassBracket { Low, Base, High }

    /// <summary>How a run defended: mostly guarding (parries and blocks), mostly dodging, or neither.</summary>
    public enum PlayLean { Balanced, Guard, Dodge }

    /// <summary>The parts of a class the progression rules need (Unity's ClassDefinition maps onto it).</summary>
    public sealed class ClassSpec
    {
        public string Id;
        public string Name;
        public ClassBracket Bracket;
        /// <summary>The play style that unlocks the class (high classes).</summary>
        public PlayLean Lean;
        /// <summary>Weapon ids the class can equip, in order of preference (set by weight: see the class assets).</summary>
        public IReadOnlyList<string> Weapons = Array.Empty<string>();

        public ClassSpec(string id, string name, ClassBracket bracket, PlayLean lean = PlayLean.Balanced, params string[] weapons)
        {
            Id = id; Name = name; Bracket = bracket; Lean = lean; Weapons = weapons ?? Array.Empty<string>();
        }
    }

    /// <summary>The class change a level-up offers. One choice means nothing to choose.</summary>
    public sealed class ClassOffer
    {
        public int Level;
        public readonly List<string> Choices = new List<string>();
        public string Recommended;
        public string Reason;
        public bool HasChoice => Choices.Count > 1;
    }

    /// <summary>One level lost (dying, or losing half your hearts in a stage).</summary>
    public readonly struct LevelDown
    {
        public readonly string Reason;
        public readonly int LevelBefore, LevelAfter;
        public readonly string ClassBefore, ClassAfter;
        public bool LostClass => ClassBefore != ClassAfter;
        public LevelDown(string reason, int before, int after, string classBefore, string classAfter)
        {
            Reason = reason; LevelBefore = before; LevelAfter = after; ClassBefore = classBefore; ClassAfter = classAfter;
        }
    }

    /// <summary>What one stage run did to the save, for the results screen.</summary>
    public sealed class RunOutcome
    {
        public StageRank Rank;
        public bool Failed, Clean, NewBest;
        public int XpGained;
        public int LevelBefore, LevelAfter;
        public readonly List<int> LevelsGained = new List<int>();
        /// <summary>Level-downs that happened during the run (they apply when they happen, not at the goal).</summary>
        public readonly List<LevelDown> LevelDowns = new List<LevelDown>();
        public string ClassBefore, ClassAfter;
        /// <summary>Why the class changed without a choice (the Fighter fallback), or empty.</summary>
        public string ClassChangeReason = "";
        /// <summary>A level-up's class change, when there is more than one class to pick from.</summary>
        public ClassOffer Offer;
        public readonly List<string> ClassesUnlocked = new List<string>();
        /// <summary>Weapons this run made available in the shop.</summary>
        public readonly List<string> WeaponsUnlocked = new List<string>();
        public int XpIntoLevel, XpToNext;
        public bool AtCap;
    }

    /// <summary>
    /// Levels and classes (GDD update 18, Slice 8). Pure C#; every number is a placeholder until the whole game is balanced.
    /// <list type="bullet">
    /// <item>A clear (B or better) earns experience by rank plus some per enemy; a fail (C) earns none. Level n needs
    /// 100 × n; the slice caps at 10.</item>
    /// <item>Level-downs: dying, and losing half your hearts in a stage, each drop one level when they happen (a run that
    /// loses half its hearts and then dies costs one level, not two). Experience is never lost. Off-beat play only costs
    /// damage. Below level 5 you return to the Minstrel.</item>
    /// <item>Classes unlock from how you play, counted over clean clears: the Warden from parries and blocks as a share of
    /// the enemy attacks that reached you, the Crusader from dodges as a share of them; both need about 70% of hits on the
    /// beat. Rank doesn't gate class.</item>
    /// <item>From level 6, every level-up offers a change to any unlocked class (or keeping yours). A Minstrel who levels
    /// past 5 with neither the Warden nor the Crusader unlocked becomes a Fighter automatically.</item>
    /// <item>The first B or better puts the greatsword in the shop. Choosing a class that can't hold any weapon you own
    /// (the Crusader before you bought the greatsword) gives you its weapon.</item>
    /// </list>
    /// </summary>
    public sealed class ProgressionRules
    {
        public const string Minstrel = "minstrel", Fighter = "fighter", Warden = "warden", Crusader = "crusader";
        public const string Sword = "sword", Greatsword = "greatsword";

        /// <summary>Experience for a clear at S, A, B (C fails and earns none).</summary>
        public int[] XpByRank = { 300, 250, 200, 0 };
        public int XpPerEnemy = 20;
        public int XpPerLevelStep = 100;
        public int LevelCap = 10;
        /// <summary>Level-ups from this level on offer a class change.</summary>
        public int ClassChangeFrom = 6;
        /// <summary>Below this level you are a Minstrel.</summary>
        public int MinstrelBelow = 5;
        public double UnlockShare = 0.5;
        public double UnlockOnBeat = 0.7;
        /// <summary>Enemy attacks (over clean clears) before a play style counts.</summary>
        public int UnlockMinAttacks = 6;
        public StageRank GreatswordAt = StageRank.B;

        public static ProgressionRules Default => new ProgressionRules();

        public int XpToNext(int level) => XpPerLevelStep * Math.Max(1, level);

        public static ClassSpec Find(IReadOnlyList<ClassSpec> classes, string id)
        {
            if (classes != null) foreach (var c in classes) if (c != null && c.Id == id) return c;
            return null;
        }

        public static PlayLean LeanOf(PlaystyleLog log)
        {
            int guard = log.Parries + log.Blocks;
            if (guard > log.Dodges) return PlayLean.Guard;
            if (log.Dodges > guard) return PlayLean.Dodge;
            return PlayLean.Balanced;
        }

        // ---------------- Weapons and class changes ----------------

        /// <summary>The weapon the class should hold: the current one if it may, else its first owned weapon (or none).</summary>
        public static string WeaponFor(SaveData s, ClassSpec cls)
        {
            if (cls == null || cls.Weapons.Count == 0) return s.HasWeapon(s.weaponId) ? s.weaponId : Sword;
            foreach (var w in cls.Weapons) if (w == s.weaponId && s.HasWeapon(w)) return w;
            foreach (var w in cls.Weapons) if (s.HasWeapon(w)) return w;
            return null;
        }

        /// <summary>
        /// Set the class and a weapon it can hold. If it can hold none you own (the Crusader before the greatsword), it
        /// gives you its first weapon. Returns the weapon granted, or null.
        /// </summary>
        public static string SetClass(SaveData s, string classId, IReadOnlyList<ClassSpec> classes)
        {
            s.classId = classId;
            s.UnlockClass(classId);
            var cls = Find(classes, classId);
            string w = WeaponFor(s, cls);
            string granted = null;
            if (w == null && cls != null && cls.Weapons.Count > 0)
            {
                w = cls.Weapons[0];
                s.UnlockWeapon(w);
                granted = w;
            }
            s.weaponId = w ?? Sword;
            return granted;
        }

        // ---------------- Level-downs ----------------

        /// <summary>Drop one level now (dying, or losing half your hearts). Below level 5 you return to the Minstrel.</summary>
        public LevelDown LoseLevel(SaveData s, string reason, IReadOnlyList<ClassSpec> classes)
        {
            int before = s.level;
            string cls = s.classId;
            s.level = Math.Max(1, s.level - 1);
            if (s.level < MinstrelBelow && s.classId != Minstrel) SetClass(s, Minstrel, classes);
            return new LevelDown(reason, before, s.level, cls, s.classId);
        }

        // ---------------- Class unlocks ----------------

        /// <summary>Classes the play totals have earned (and not yet unlocked).</summary>
        public List<string> EarnedClasses(SaveData s)
        {
            var list = new List<string>();
            var t = s.totals;
            if (t.EnemyAttacks < UnlockMinAttacks || t.OnBeatShare < UnlockOnBeat) return list;
            if (t.GuardShare >= UnlockShare && !s.HasUnlockedClass(Warden)) list.Add(Warden);
            if (t.DodgeShare >= UnlockShare && !s.HasUnlockedClass(Crusader)) list.Add(Crusader);
            return list;
        }

        // ---------------- A stage run ----------------

        /// <summary>
        /// A run reached the goal. A fail (C) only records the attempt. A clear records the best rank, opens the
        /// greatsword in the shop on the first B or better, counts a clean clear's play toward class unlocks, then
        /// adds experience (capped) and handles level-ups: the Fighter fallback past level 5, and from level 6 a class
        /// change offer when more than one class is unlocked. <paramref name="levelDowns"/> are the run's level-downs
        /// (already applied), passed through for the results screen.
        /// </summary>
        public RunOutcome Apply(SaveData s, string stage, StageResult result, PlaystyleLog log, IReadOnlyList<ClassSpec> classes,
                                IEnumerable<LevelDown> levelDowns = null)
        {
            var o = new RunOutcome
            {
                Rank = result.Rank, Failed = result.Failed, Clean = result.Clean,
                LevelBefore = s.level, ClassBefore = s.classId,
            };
            if (levelDowns != null) o.LevelDowns.AddRange(levelDowns);
            if (o.LevelDowns.Count > 0) { o.LevelBefore = o.LevelDowns[0].LevelBefore; o.ClassBefore = o.LevelDowns[0].ClassBefore; }
            s.AddHistory(stage, result.Rank, result.Seconds);

            if (!result.Failed)
            {
                o.NewBest = s.RecordStage(stage, result.Rank, result.Seconds);
                if (result.Rank <= GreatswordAt && s.OfferWeapon(Greatsword)) o.WeaponsUnlocked.Add(Greatsword);

                if (result.Clean)
                {
                    s.totals.Add(log);
                    foreach (var c in EarnedClasses(s)) { s.UnlockClass(c); o.ClassesUnlocked.Add(c); }
                }

                o.XpGained = XpByRank[(int)result.Rank] + XpPerEnemy * log.EnemiesDefeated;
                if (s.level >= LevelCap) o.XpGained = 0;
                s.xp += o.XpGained;
                while (s.level < LevelCap && s.xp >= XpToNext(s.level))
                {
                    s.xp -= XpToNext(s.level);
                    s.level++;
                    o.LevelsGained.Add(s.level);
                    if (s.level == MinstrelBelow + 1 && s.classId == Minstrel &&
                        !s.HasUnlockedClass(Warden) && !s.HasUnlockedClass(Crusader))
                    {
                        SetClass(s, Fighter, classes);
                        o.ClassChangeReason = $"passed level {MinstrelBelow} with neither the Warden nor the Crusader unlocked: now a Fighter";
                    }
                }
                if (s.level >= LevelCap) s.xp = 0;

                if (o.LevelsGained.Count > 0 && s.level >= ClassChangeFrom)
                {
                    var offer = new ClassOffer { Level = s.level, Recommended = s.classId };
                    offer.Choices.Add(s.classId);
                    foreach (var id in s.unlockedClasses) if (id != s.classId && Find(classes, id) != null) offer.Choices.Add(id);
                    var lean = LeanOf(log);
                    foreach (var id in offer.Choices)
                    {
                        var c = Find(classes, id);
                        if (o.ClassesUnlocked.Contains(id) || (c != null && c.Lean != PlayLean.Balanced && c.Lean == lean)) { offer.Recommended = id; break; }
                    }
                    offer.Reason = "level up: change to any class you have unlocked, or keep yours";
                    if (offer.HasChoice) o.Offer = offer;
                }
            }

            o.LevelAfter = s.level;
            o.ClassAfter = s.classId;
            o.XpIntoLevel = s.xp;
            o.XpToNext = XpToNext(s.level);
            o.AtCap = s.level >= LevelCap;
            return o;
        }
    }
}
