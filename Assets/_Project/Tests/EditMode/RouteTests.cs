using System;
using NUnit.Framework;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    /// <summary>The road through the game and the save slots (VS-5 Part B).</summary>
    public class RouteTests
    {
        static SaveData Fresh() => SaveData.NewGame(ProgressionRules.Minstrel, null);

        static bool Open(SaveData s, string id, bool free = false) => StageRoute.IsOpen(s, StageRoute.Find(id), free);

        [Test]
        public void TheStoryPathOpensInOrder()
        {
            var s = Fresh();
            Assert.IsTrue(Open(s, "Yard") && Open(s, "Cliffside"));
            Assert.IsFalse(Open(s, "Belfry") || Open(s, "Summit") || Open(s, "Arena") || Open(s, "Spring"));
            s.ClearStage("Cliffside");
            Assert.IsTrue(Open(s, "Belfry") && Open(s, "Arena"), "Cliffside opens Belfry Ridge and the Arena");
            Assert.IsFalse(Open(s, "Summit") || Open(s, "Spring"));
            s.ClearStage("Belfry");
            Assert.IsTrue(Open(s, "Summit") && Open(s, "Spring"), "Belfry Ridge opens the Summit and Starlit Spring");
        }

        [Test]
        public void FreePlayOpensEverything()
        {
            foreach (var r in StageRoute.All) Assert.IsTrue(StageRoute.IsOpen(Fresh(), r, freePlay: true), r.Id);
        }

        [Test]
        public void NextIsTheFirstOpenStopNotDone()
        {
            var s = Fresh();
            Assert.AreEqual("Yard", StageRoute.Next(s).Id, "a new player is pointed at the Yard");
            s.tutorialDone = true;
            Assert.AreEqual("Cliffside", StageRoute.Next(s).Id);
            s.ClearStage("Cliffside");
            Assert.AreEqual("Belfry", StageRoute.Next(s).Id);
            s.ClearStage("Belfry");
            s.ClearStage("Summit");
            Assert.AreEqual("Nave", StageRoute.Next(s).Id, "VS-6: the summit opens the way down");
            s.ClearStage("Nave");
            Assert.AreEqual("Loft", StageRoute.Next(s).Id);
            s.ClearStage("Loft");
            Assert.AreEqual("Apse", StageRoute.Next(s).Id);
            s.ClearStage("Apse");
            Assert.IsNull(StageRoute.Next(s), "the story is done");
        }

        [Test]
        public void SkippingTheYardCountsItDone()
        {
            var s = Fresh();
            s.ClearStage("Cliffside");
            Assert.IsTrue(StageRoute.IsDone(s, StageRoute.Find("Yard")));
            Assert.AreEqual("Belfry", StageRoute.Next(s).Id);
        }

        [Test]
        public void StatusAndLockReasons()
        {
            var s = Fresh();
            Assert.AreEqual("optional", StageRoute.Status(s, StageRoute.Find("Yard")));
            Assert.AreEqual("not cleared", StageRoute.Status(s, StageRoute.Find("Cliffside")));
            Assert.AreEqual("locked", StageRoute.Status(s, StageRoute.Find("Belfry")));
            Assert.AreEqual("Clear Cliffside to open the road.", StageRoute.LockReason(s, StageRoute.Find("Belfry")));
            Assert.AreEqual("Clear Belfry Ridge to open the road.", StageRoute.LockReason(s, StageRoute.Find("Spring")));
            Assert.AreEqual("", StageRoute.LockReason(s, StageRoute.Find("Cliffside")));
            s.RecordStage("Cliffside", StageRank.A, 70.0);
            Assert.AreEqual("best A", StageRoute.Status(s, StageRoute.Find("Cliffside")));
            Assert.AreEqual("1:10.0", StageRoute.Time(70.0));
        }

        [Test]
        public void TheRouteMatchesTheStory()
        {
            Assert.AreEqual(1, StageRoute.Find("Cliffside").Verse);
            Assert.AreEqual(3, StageRoute.Find("Summit").Verse);
            Assert.AreEqual(0, StageRoute.Find("Arena").Verse);
            foreach (var r in StageRoute.All)
            {
                Assert.IsTrue(r.Blurb.Length > 10 && r.Blurb.Length < 200, r.Id);
                Assert.IsTrue(string.IsNullOrEmpty(r.Requires) || StageRoute.Find(r.Requires) != null, r.Id);
            }
            Assert.IsNull(StageRoute.Find("Nowhere"));
        }

        [Test]
        public void SlotNamesAndSummaries()
        {
            Assert.AreEqual("hymn_save_1.json", SaveSlots.FileName(1));
            Assert.AreEqual("hymn_save_3.json", SaveSlots.FileName(9), "clamped");
            Assert.AreEqual("Empty", SaveSlots.Summary(null));
            var s = Fresh();
            s.level = 4;
            s.playSeconds = 3727;
            s.ClearStage("Cliffside");
            Assert.AreEqual("Level 4 Fighter · 1 of 6 verses · 1:02:07", SaveSlots.Summary(s, "Fighter"));
            s.MarkStory(Story.EndingSeen);
            StringAssert.Contains("the Hymn restored", SaveSlots.Summary(s, "Fighter"));
            Assert.AreEqual("4:05", SaveSlots.PlayTime(245));
        }

        [Test]
        public void ContinueLoadsTheNewestSlot()
        {
            var t = new DateTime(2026, 10, 14, 20, 0, 0);
            Assert.AreEqual(0, SaveSlots.Latest(new DateTime?[] { null, null, null }));
            Assert.AreEqual(2, SaveSlots.Latest(new DateTime?[] { t, t.AddHours(1), null }));
            Assert.AreEqual(3, SaveSlots.Latest(new DateTime?[] { t, null, t.AddMinutes(5) }));
        }

        [Test]
        public void TheDrownedNaveOpensAfterTheSummit()
        {
            var s = Fresh();
            Assert.IsTrue(StageRoute.RegionOpen(s, 0));
            Assert.IsFalse(StageRoute.RegionOpen(s, 1), "the deep waits for the summit");
            Assert.IsTrue(StageRoute.RegionOpen(s, 1, freePlay: true));
            s.ClearStage("Cliffside"); s.ClearStage("Belfry"); s.ClearStage("Summit");
            Assert.IsTrue(StageRoute.RegionOpen(s, 1));
            Assert.IsTrue(StageRoute.IsOpen(s, StageRoute.Find("Nave")));
            Assert.IsFalse(StageRoute.IsOpen(s, StageRoute.Find("Loft")));
            foreach (var r in StageRoute.DeepPath)
            {
                Assert.AreEqual(1, r.Region, r.Id);
                Assert.AreEqual(1, StageRoute.RegionOfScene(r.Scene), r.Id);
                Assert.Greater(r.Verse, 3, r.Id + " holds a deep verse");
            }
            Assert.AreEqual(0, StageRoute.RegionOfScene("Village"));
            Assert.AreEqual(1, StageRoute.RegionOfScene("AirBell"));
            Assert.AreEqual(0, StageRoute.RegionOfScene("Belfry"));
            Assert.AreEqual(-1, StageRoute.RegionOfScene("Title"));
        }
    }
}
