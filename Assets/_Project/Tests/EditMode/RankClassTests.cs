using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    public class RankClassTests
    {
        static readonly ClassSpec[] Classes =
        {
            new ClassSpec(ProgressionRules.Minstrel, "Minstrel", ClassBracket.Base, 0, PlayLean.Balanced, ProgressionRules.Sword, ProgressionRules.Greatsword),
            new ClassSpec(ProgressionRules.Fighter, "Fighter", ClassBracket.Low, 0, PlayLean.Balanced, ProgressionRules.Sword, ProgressionRules.Greatsword),
            new ClassSpec(ProgressionRules.Warden, "Warden", ClassBracket.High, 6, PlayLean.Guard, ProgressionRules.Sword),
            new ClassSpec(ProgressionRules.Crusader, "Crusader", ClassBracket.High, 6, PlayLean.Dodge, ProgressionRules.Sword, ProgressionRules.Greatsword),
        };

        static SaveData NewSave(int level = 1, int xp = 0, string cls = ProgressionRules.Minstrel)
        {
            var s = SaveData.NewGame(cls, null);
            s.level = level; s.xp = xp;
            return s;
        }

        static StageResult Result(StageRank rank, double seconds = 70.0) =>
            new StageResult(seconds, rank, rank, new List<string>(), 1.0);

        /// <summary>A run of <paramref name="perfects"/> Perfect hits (mastery 100), defence mix, enemies.</summary>
        static PlaystyleLog Log(int perfects = 10, int greats = 0, int offBeat = 0, int parries = 0, int blocks = 0, int dodges = 0, int enemies = 0, int hearts = 0)
        {
            var log = new PlaystyleLog();
            for (int i = 0; i < perfects; i++) log.AddNote(Judgement.Perfect);
            for (int i = 0; i < greats; i++) log.AddNote(Judgement.Great);
            for (int i = 0; i < offBeat; i++) log.AddNote(Judgement.OffBeat);
            for (int i = 0; i < parries; i++) log.AddDefense(DefenseResult.Parried);
            for (int i = 0; i < blocks; i++) log.AddDefense(DefenseResult.Blocked);
            for (int i = 0; i < dodges; i++) log.AddDefense(DefenseResult.Dodged);
            for (int i = 0; i < enemies; i++) log.AddKill();
            if (hearts > 0) log.AddHurt(hearts);
            return log;
        }

        static readonly ProgressionRules Rules = ProgressionRules.Default;

        [Test]
        public void ExperienceByRankAndEnemies()
        {
            var s = NewSave();
            var o = Rules.Apply(s, "Cliffside", Result(StageRank.S), Log(enemies: 3), Classes);
            Assert.AreEqual(560, o.XpGained);
            // 100 to level 2, 200 to level 3, 260 left toward level 4 (needs 300)
            Assert.AreEqual(3, s.level);
            Assert.AreEqual(260, s.xp);
            CollectionAssert.AreEqual(new[] { 2, 3 }, o.LevelsGained);
        }

        [Test]
        public void CheckLevels()
        {
            Assert.IsTrue(Rules.IsCheckLevel(6));
            Assert.IsTrue(Rules.IsCheckLevel(8));
            Assert.IsTrue(Rules.IsCheckLevel(10));
            Assert.IsFalse(Rules.IsCheckLevel(7));
            Assert.IsFalse(Rules.IsCheckLevel(12));
            Assert.IsTrue(Rules.IsCheckLevel(15));
        }

        [Test]
        public void LevelDownIsOneLevelPerTriggerAndKeepsExperience()
        {
            var s = NewSave(level: 5, xp: 120);
            var o = Rules.Apply(s, "Cliffside", Result(StageRank.B), Log(perfects: 4, offBeat: 6, hearts: 3), Classes);
            Assert.AreEqual(2, o.LevelDownReasons.Count);
            Assert.AreEqual(3, s.level);
            Assert.AreEqual(120, s.xp);
            Assert.AreEqual(0, o.XpGained);
        }

        [Test]
        public void LevelNeverDropsBelowOne()
        {
            var s = NewSave();
            Rules.Apply(s, "Cliffside", Result(StageRank.C), Log(perfects: 0, offBeat: 8, hearts: 4), Classes);
            Assert.AreEqual(1, s.level);
        }

        [Test]
        public void LevelDownBelowAClassMinimumCostsTheClass()
        {
            var s = NewSave(level: 6, cls: ProgressionRules.Warden);
            var o = Rules.Apply(s, "Cliffside", Result(StageRank.A), Log(hearts: 3), Classes);
            Assert.AreEqual(5, s.level);
            Assert.AreEqual(ProgressionRules.Minstrel, s.classId);
            StringAssert.Contains("Minstrel", o.ClassChangeReason);
        }

        [Test]
        public void SOrAWithHighMasteryOffersTheClassThatFitsAndKeeping()
        {
            var offer = Rules.Check(ProgressionRules.Minstrel, StageRank.A, 75.0, PlayLean.Guard, 6, Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Warden, ProgressionRules.Minstrel }, offer.Choices);
            Assert.AreEqual(ProgressionRules.Warden, offer.Recommended);

            offer = Rules.Check(ProgressionRules.Minstrel, StageRank.S, 75.0, PlayLean.Dodge, 6, Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Crusader, ProgressionRules.Minstrel }, offer.Choices);
        }

        [Test]
        public void VeryHighMasteryOffersEveryHighClass()
        {
            var offer = Rules.Check(ProgressionRules.Minstrel, StageRank.S, 90.0, PlayLean.Dodge, 8, Classes);
            CollectionAssert.AreEquivalent(new[] { ProgressionRules.Warden, ProgressionRules.Crusader, ProgressionRules.Minstrel }, offer.Choices);
            Assert.AreEqual(ProgressionRules.Crusader, offer.Recommended);
        }

        [Test]
        public void LowMasteryOffersNothingNew()
        {
            var offer = Rules.Check(ProgressionRules.Minstrel, StageRank.S, 60.0, PlayLean.Guard, 6, Classes);
            Assert.IsFalse(offer.HasChoice);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Minstrel }, offer.Choices);
        }

        [Test]
        public void BHoldsAndCDropsTheMinstrelToFighter()
        {
            var b = Rules.Check(ProgressionRules.Minstrel, StageRank.B, 95.0, PlayLean.Guard, 6, Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Minstrel }, b.Choices);
            var c = Rules.Check(ProgressionRules.Minstrel, StageRank.C, 95.0, PlayLean.Guard, 6, Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Fighter }, c.Choices);
        }

        [Test]
        public void MinstrelWithoutUpgradeByLevelTenBecomesFighter()
        {
            var b = Rules.Check(ProgressionRules.Minstrel, StageRank.B, 95.0, PlayLean.Guard, 10, Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Fighter }, b.Choices);
        }

        [Test]
        public void FighterStaysUnlessSOrA()
        {
            foreach (var rank in new[] { StageRank.B, StageRank.C })
            {
                var offer = Rules.Check(ProgressionRules.Fighter, rank, 95.0, PlayLean.Guard, 8, Classes);
                CollectionAssert.AreEqual(new[] { ProgressionRules.Fighter }, offer.Choices);
            }
            var up = Rules.Check(ProgressionRules.Fighter, StageRank.A, 50.0, PlayLean.Guard, 8, Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Minstrel, ProgressionRules.Fighter }, up.Choices);
            var high = Rules.Check(ProgressionRules.Fighter, StageRank.S, 80.0, PlayLean.Guard, 8, Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Warden, ProgressionRules.Minstrel, ProgressionRules.Fighter }, high.Choices);
        }

        [Test]
        public void CCostsAHighClass()
        {
            var c = Rules.Check(ProgressionRules.Crusader, StageRank.C, 95.0, PlayLean.Dodge, 8, Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Minstrel }, c.Choices);
        }

        [Test]
        public void ReachingACheckLevelWithAChoiceLeavesTheOfferForThePlayer()
        {
            var s = NewSave(level: 5, xp: 400);          // 100 more reaches level 6
            var o = Rules.Apply(s, "Cliffside", Result(StageRank.S), Log(perfects: 10, blocks: 4), Classes);
            Assert.AreEqual(6, s.level);
            Assert.IsNotNull(o.Offer);
            Assert.AreEqual(ProgressionRules.Warden, o.Offer.Recommended);
            Assert.AreEqual(ProgressionRules.Minstrel, s.classId);       // unchanged until chosen
            ProgressionRules.SetClass(s, o.Offer.Recommended, Classes);
            Assert.AreEqual(ProgressionRules.Warden, s.classId);
        }

        [Test]
        public void ForcedChangeAtACheckIsApplied()
        {
            var s = NewSave(level: 5, xp: 400);
            var o = Rules.Apply(s, "Cliffside", Result(StageRank.C), Log(perfects: 10), Classes);
            Assert.AreEqual(6, s.level);
            Assert.IsNull(o.Offer);
            Assert.AreEqual(ProgressionRules.Fighter, s.classId);
            Assert.AreEqual(ProgressionRules.Fighter, o.ClassAfter);
        }

        [Test]
        public void FirstBUnlocksTheGreatswordOnce()
        {
            var s = NewSave();
            var c = Rules.Apply(s, "Cliffside", Result(StageRank.C), Log(), Classes);
            Assert.AreEqual(0, c.WeaponsUnlocked.Count);
            var b = Rules.Apply(s, "Cliffside", Result(StageRank.B), Log(), Classes);
            CollectionAssert.AreEqual(new[] { ProgressionRules.Greatsword }, b.WeaponsUnlocked);
            var a = Rules.Apply(s, "Cliffside", Result(StageRank.A), Log(), Classes);
            Assert.AreEqual(0, a.WeaponsUnlocked.Count);
            Assert.IsTrue(s.HasWeapon(ProgressionRules.Greatsword));
        }

        [Test]
        public void ClassChangeKeepsAWeaponTheClassCanHold()
        {
            var s = NewSave();
            s.UnlockWeapon(ProgressionRules.Greatsword);
            s.weaponId = ProgressionRules.Greatsword;
            ProgressionRules.SetClass(s, ProgressionRules.Warden, Classes);     // sword and shield only
            Assert.AreEqual(ProgressionRules.Sword, s.weaponId);
            ProgressionRules.SetClass(s, ProgressionRules.Crusader, Classes);
            Assert.AreEqual(ProgressionRules.Sword, s.weaponId);
        }

        [Test]
        public void StageRecordsKeepTheBestRank()
        {
            var s = NewSave();
            Assert.IsTrue(s.RecordStage("Cliffside", StageRank.B, 88.0));
            Assert.IsFalse(s.RecordStage("Cliffside", StageRank.C, 70.0));
            Assert.IsTrue(s.RecordStage("Cliffside", StageRank.B, 85.0));
            Assert.IsTrue(s.TryBestRank("Cliffside", out var best));
            Assert.AreEqual(StageRank.B, best);
            Assert.AreEqual(3, s.Record("Cliffside").clears);
            Assert.IsTrue(s.HasCleared("Cliffside"));
        }

        [Test]
        public void LeanFollowsTheDefence()
        {
            Assert.AreEqual(PlayLean.Guard, ProgressionRules.LeanOf(Log(parries: 2, blocks: 2, dodges: 3)));
            Assert.AreEqual(PlayLean.Dodge, ProgressionRules.LeanOf(Log(blocks: 1, dodges: 3)));
            Assert.AreEqual(PlayLean.Balanced, ProgressionRules.LeanOf(Log()));
        }

        [Test]
        public void OldSavesMigrateToTheMinstrel()
        {
            var knight = new SaveData { version = 1, classId = "knight" };
            knight.Normalize();
            Assert.AreEqual(ProgressionRules.Minstrel, knight.classId);
            Assert.AreEqual(ProgressionRules.Sword, knight.weaponId);
            Assert.AreEqual(1, knight.level);

            var barbarian = new SaveData { version = 1, classId = "barbarian" };
            barbarian.Normalize();
            Assert.AreEqual(ProgressionRules.Minstrel, barbarian.classId);
            Assert.AreEqual(ProgressionRules.Greatsword, barbarian.weaponId);
            Assert.IsTrue(barbarian.HasWeapon(ProgressionRules.Sword));
            Assert.AreEqual(SaveData.CurrentVersion, barbarian.version);
        }
    }
}
