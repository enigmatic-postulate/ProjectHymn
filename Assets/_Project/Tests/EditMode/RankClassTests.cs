using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    /// <summary>Levels and classes, GDD update 18 (Slice 8).</summary>
    public class RankClassTests
    {
        static readonly ClassSpec[] Classes =
        {
            new ClassSpec(ProgressionRules.Minstrel, "Minstrel", ClassBracket.Base, PlayLean.Balanced, ProgressionRules.Sword, ProgressionRules.Greatsword),
            new ClassSpec(ProgressionRules.Fighter, "Fighter", ClassBracket.Low, PlayLean.Balanced, ProgressionRules.Sword, ProgressionRules.Greatsword),
            new ClassSpec(ProgressionRules.Warden, "Warden", ClassBracket.High, PlayLean.Guard, ProgressionRules.Sword),
            new ClassSpec(ProgressionRules.Crusader, "Crusader", ClassBracket.High, PlayLean.Dodge, ProgressionRules.Greatsword),
        };

        static readonly ProgressionRules Rules = ProgressionRules.Default;

        static SaveData NewSave(int level = 1, int xp = 0, string cls = ProgressionRules.Minstrel)
        {
            var s = SaveData.NewGame(cls, null);
            s.level = level; s.xp = xp;
            s.UnlockClass(cls);
            return s;
        }

        static StageResult Clear(StageRank rank, bool clean = true, double seconds = 70.0) =>
            new StageResult(seconds, rank, rank, new List<string>(), 1.0, clean);

        static PlaystyleLog Log(int perfects = 10, int offBeat = 0, int parries = 0, int blocks = 0, int dodges = 0, int enemies = 0, int hurt = 0)
        {
            var log = new PlaystyleLog { MaxHearts = 5 };
            for (int i = 0; i < perfects; i++) log.AddNote(Judgement.Perfect);
            for (int i = 0; i < offBeat; i++) log.AddNote(Judgement.OffBeat);
            for (int i = 0; i < parries; i++) log.AddDefense(DefenseResult.Parried);
            for (int i = 0; i < blocks; i++) log.AddDefense(DefenseResult.Blocked);
            for (int i = 0; i < dodges; i++) log.AddDefense(DefenseResult.Dodged);
            for (int i = 0; i < enemies; i++) log.AddKill();
            for (int i = 0; i < hurt; i++) log.AddHurt(1);
            return log;
        }

        // ---------------- Experience and levels ----------------

        [Test]
        public void ExperienceByRankAndEnemies()
        {
            var s = NewSave();
            var o = Rules.Apply(s, "Cliffside", Clear(StageRank.S), Log(enemies: 3), Classes);
            Assert.AreEqual(360, o.XpGained);
            Assert.AreEqual(3, s.level);              // 100 to level 2, 200 to level 3, 60 toward level 4
            Assert.AreEqual(60, s.xp);
        }

        [Test]
        public void AFailEarnsNothing()
        {
            var s = NewSave(level: 3, xp: 50);
            var o = Rules.Apply(s, "Cliffside", Clear(StageRank.C, clean: false), Log(enemies: 2), Classes);
            Assert.IsTrue(o.Failed);
            Assert.AreEqual(0, o.XpGained);
            Assert.AreEqual(3, s.level);
            Assert.AreEqual(50, s.xp);
            Assert.IsFalse(s.HasCleared("Cliffside"));
            Assert.AreEqual(1, s.history.Count);
        }

        [Test]
        public void LevelsCapAtTen()
        {
            var s = NewSave(level: 9, xp: 850);
            Rules.Apply(s, "Cliffside", Clear(StageRank.S), Log(enemies: 10), Classes);
            Assert.AreEqual(10, s.level);
            Assert.AreEqual(0, s.xp);
            var o = Rules.Apply(s, "Cliffside", Clear(StageRank.S), Log(enemies: 10), Classes);
            Assert.AreEqual(0, o.XpGained);
            Assert.IsTrue(o.AtCap);
        }

        // ---------------- Level-downs ----------------

        [Test]
        public void ALevelDownIsOneLevelAndKeepsExperience()
        {
            var s = NewSave(level: 7, xp: 120, cls: ProgressionRules.Warden);
            var d = Rules.LoseLevel(s, "died", Classes);
            Assert.AreEqual(6, s.level);
            Assert.AreEqual(120, s.xp);
            Assert.AreEqual(ProgressionRules.Warden, s.classId);
            Assert.IsFalse(d.LostClass);
        }

        [Test]
        public void BelowLevelFiveYouReturnToTheMinstrel()
        {
            var s = NewSave(level: 5, cls: ProgressionRules.Fighter);
            var d = Rules.LoseLevel(s, "lost half your hearts", Classes);
            Assert.AreEqual(4, s.level);
            Assert.AreEqual(ProgressionRules.Minstrel, s.classId);
            Assert.IsTrue(d.LostClass);
            Assert.IsTrue(s.HasUnlockedClass(ProgressionRules.Fighter));     // unlocks are kept
        }

        [Test]
        public void LevelNeverDropsBelowOne()
        {
            var s = NewSave();
            Rules.LoseLevel(s, "died", Classes);
            Assert.AreEqual(1, s.level);
        }

        // ---------------- Class unlocks and changes ----------------

        [Test]
        public void GuardingOnCleanClearsUnlocksTheWarden()
        {
            var s = NewSave();
            var o = Rules.Apply(s, "Cliffside", Clear(StageRank.B), Log(perfects: 10, parries: 3, blocks: 2, dodges: 1, enemies: 2), Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Warden }, o.ClassesUnlocked);
            Assert.IsTrue(s.HasUnlockedClass(ProgressionRules.Warden));
        }

        [Test]
        public void DodgingUnlocksTheCrusader()
        {
            var s = NewSave();
            var o = Rules.Apply(s, "Cliffside", Clear(StageRank.A), Log(perfects: 10, dodges: 5, blocks: 1, enemies: 2), Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Crusader }, o.ClassesUnlocked);
        }

        [Test]
        public void UnlocksNeedCleanClearsAndPlayOnTheBeat()
        {
            var s = NewSave();
            var notClean = Rules.Apply(s, "Cliffside", Clear(StageRank.B, clean: false), Log(parries: 6, enemies: 2), Classes);
            Assert.AreEqual(0, notClean.ClassesUnlocked.Count);
            Assert.AreEqual(0, s.totals.cleanClears);
            var offBeat = Rules.Apply(s, "Cliffside", Clear(StageRank.B), Log(perfects: 4, offBeat: 6, parries: 6, enemies: 2), Classes);
            Assert.AreEqual(0, offBeat.ClassesUnlocked.Count);      // 40% on the beat
        }

        [Test]
        public void UnlocksAddUpOverCleanClears()
        {
            var s = NewSave();
            Assert.AreEqual(0, Rules.Apply(s, "Cliffside", Clear(StageRank.B), Log(parries: 2, blocks: 1, enemies: 2), Classes).ClassesUnlocked.Count);
            var o = Rules.Apply(s, "Cliffside", Clear(StageRank.B), Log(parries: 2, blocks: 1, enemies: 2), Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Warden }, o.ClassesUnlocked);   // 6 attacks over two clears
        }

        [Test]
        public void AMinstrelWithNeitherUnlockedBecomesAFighterPastLevelFive()
        {
            var s = NewSave(level: 5, xp: 400);
            var o = Rules.Apply(s, "Cliffside", Clear(StageRank.S), Log(enemies: 2), Classes);
            Assert.AreEqual(6, s.level);
            Assert.AreEqual(ProgressionRules.Fighter, s.classId);
            Assert.IsTrue(s.HasUnlockedClass(ProgressionRules.Fighter));
            StringAssert.Contains("Fighter", o.ClassChangeReason);
        }

        [Test]
        public void LevelUpsFromSixOfferEveryUnlockedClass()
        {
            var s = NewSave(level: 5, xp: 400);
            var o = Rules.Apply(s, "Cliffside", Clear(StageRank.S), Log(perfects: 10, parries: 4, blocks: 2, enemies: 2), Classes);
            Assert.AreEqual(ProgressionRules.Minstrel, s.classId);                  // the Warden unlocked first: no fallback
            Assert.IsNotNull(o.Offer);
            CollectionAssert.AreEquivalent(new[] { ProgressionRules.Minstrel, ProgressionRules.Warden }, o.Offer.Choices);
            Assert.AreEqual(ProgressionRules.Warden, o.Offer.Recommended);
        }

        [Test]
        public void NoOfferBeforeSixOrWithOneClass()
        {
            var s = NewSave(level: 2, xp: 150);
            s.UnlockClass(ProgressionRules.Warden);
            Assert.IsNull(Rules.Apply(s, "Cliffside", Clear(StageRank.S), Log(), Classes).Offer);   // level 3
            var f = NewSave(level: 7, xp: 650, cls: ProgressionRules.Fighter);
            f.unlockedClasses.Clear(); f.UnlockClass(ProgressionRules.Fighter);
            Assert.IsNull(Rules.Apply(f, "Cliffside", Clear(StageRank.S), Log(), Classes).Offer);   // only the Fighter
        }

        [Test]
        public void TheCrusaderBringsTheGreatswordAndKeepsNoShield()
        {
            var s = NewSave(level: 6);
            var granted = ProgressionRules.SetClass(s, ProgressionRules.Crusader, Classes);
            Assert.AreEqual(ProgressionRules.Greatsword, granted);
            Assert.AreEqual(ProgressionRules.Greatsword, s.weaponId);
            Assert.IsTrue(s.HasWeapon(ProgressionRules.Greatsword));
            ProgressionRules.SetClass(s, ProgressionRules.Warden, Classes);       // shield and lighter weapons
            Assert.AreEqual(ProgressionRules.Sword, s.weaponId);
        }

        // ---------------- Weapons and the shop ----------------

        [Test]
        public void FirstBPutsTheGreatswordInTheShop()
        {
            var s = NewSave();
            var c = Rules.Apply(s, "Cliffside", Clear(StageRank.C, clean: false), Log(), Classes);
            Assert.AreEqual(0, c.WeaponsUnlocked.Count);
            var b = Rules.Apply(s, "Cliffside", Clear(StageRank.B), Log(), Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Greatsword }, b.WeaponsUnlocked);
            Assert.IsFalse(s.HasWeapon(ProgressionRules.Greatsword));
            Assert.AreEqual(PurchaseResult.TooPoor, s.BuyWeapon(ProgressionRules.Greatsword, 300));
            s.AddNotes(300);
            Assert.AreEqual(PurchaseResult.Bought, s.BuyWeapon(ProgressionRules.Greatsword, 300));
            Assert.IsTrue(s.HasWeapon(ProgressionRules.Greatsword));
            Assert.AreEqual(0, Rules.Apply(s, "Cliffside", Clear(StageRank.A), Log(), Classes).WeaponsUnlocked.Count);
        }

        [Test]
        public void StageRecordsKeepTheBestRank()
        {
            var s = NewSave();
            Assert.IsTrue(s.RecordStage("Cliffside", StageRank.B, 88.0));
            Assert.IsFalse(s.RecordStage("Cliffside", StageRank.B, 90.0));
            Assert.IsTrue(s.RecordStage("Cliffside", StageRank.A, 80.0));
            Assert.IsTrue(s.TryBestRank("Cliffside", out var best));
            Assert.AreEqual(StageRank.A, best);
            Assert.AreEqual(3, s.Record("Cliffside").clears);
        }

        [Test]
        public void HistoryKeepsTheLastThirtyRuns()
        {
            var s = NewSave();
            for (int i = 0; i < 40; i++) s.AddHistory("Arena", StageRank.B, i);
            Assert.AreEqual(SaveData.HistoryLength, s.history.Count);
            Assert.AreEqual(39.0, s.history[s.history.Count - 1].seconds, 1e-9);
        }

        [Test]
        public void OldSavesLoadAsALevelOneMinstrel()
        {
            var old = new SaveData { version = 2, classId = ProgressionRules.Warden, level = 8, xp = 300, notes = 120 };
            old.weapons.Add(ProgressionRules.Greatsword);
            old.Normalize();
            Assert.AreEqual(ProgressionRules.Minstrel, old.classId);
            Assert.AreEqual(1, old.level);
            Assert.AreEqual(0, old.xp);
            Assert.AreEqual(120, old.notes);
            Assert.IsFalse(old.HasWeapon(ProgressionRules.Greatsword));
            CollectionAssert.AreEqual(new[] { ProgressionRules.Minstrel }, old.unlockedClasses);
            Assert.AreEqual(SaveData.CurrentVersion, old.version);
        }
    }
}
