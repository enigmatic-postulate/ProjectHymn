using NUnit.Framework;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    public class SaveDataTests
    {
        static SaveData Fresh() => SaveData.NewGame("knight", new[] { "CrossingCuts" }, new[] { new ItemCount("potion", 1) });

        [Test]
        public void NewGameStartsWithClassSequencesAndItems()
        {
            var s = Fresh();
            Assert.AreEqual("knight", s.classId);
            Assert.IsTrue(s.Knows("CrossingCuts"));
            Assert.IsFalse(s.Knows("Tempest"));
            Assert.AreEqual(1, s.Count("potion"));
            Assert.AreEqual("potion", s.quickItem);
        }

        [Test]
        public void SpendingNeedsEnoughNotes()
        {
            var s = Fresh();
            s.AddNotes(50);
            Assert.IsFalse(s.TrySpend(60));
            Assert.AreEqual(50, s.notes);
            Assert.IsTrue(s.TrySpend(50));
            Assert.AreEqual(0, s.notes);
            s.AddNotes(-10);
            Assert.AreEqual(0, s.notes);
        }

        [Test]
        public void BuyingItemsAndScrolls()
        {
            var s = Fresh();
            s.AddNotes(400);
            Assert.AreEqual(PurchaseResult.Bought, s.Buy("potion", 60));
            Assert.AreEqual(2, s.Count("potion"));
            Assert.AreEqual(PurchaseResult.Bought, s.Buy("scroll_powerslice", 250, "Powerslice"));
            Assert.IsTrue(s.Knows("Powerslice"));
            Assert.AreEqual(0, s.Count("scroll_powerslice"), "scrolls are learned, not stored");
            Assert.AreEqual(PurchaseResult.AlreadyKnown, s.Buy("scroll_powerslice", 250, "Powerslice"));
            Assert.AreEqual(PurchaseResult.TooPoor, s.Buy("tonic", 120));
            Assert.AreEqual(90, s.notes);
        }

        [Test]
        public void StacksCapAtNine()
        {
            var s = Fresh();
            Assert.AreEqual(8, s.AddItem("potion", 20));
            Assert.AreEqual(SaveData.MaxStack, s.Count("potion"));
            s.AddNotes(1000);
            Assert.AreEqual(PurchaseResult.Invalid, s.Buy("potion", 60));
            Assert.AreEqual(1000, s.notes);
        }

        [Test]
        public void UsingTheLastItemMovesTheQuickSlot()
        {
            var s = Fresh();
            s.AddItem("tonic");
            Assert.AreEqual("potion", s.quickItem);
            Assert.IsTrue(s.UseItem("potion"));
            Assert.AreEqual("tonic", s.quickItem);
            Assert.IsFalse(s.UseItem("potion"));
            Assert.IsTrue(s.UseItem("tonic"));
            Assert.AreEqual("", s.quickItem);
        }

        [Test]
        public void QuickSlotCycles()
        {
            var s = Fresh();
            s.AddItem("tonic");
            Assert.AreEqual("tonic", s.CycleQuickItem());
            Assert.AreEqual("potion", s.CycleQuickItem());
        }

        [Test]
        public void PickupsLoreAndStagesAreOnce()
        {
            var s = Fresh();
            Assert.IsTrue(s.MarkCollected("cliff_cache_1"));
            Assert.IsFalse(s.MarkCollected("cliff_cache_1"));
            Assert.IsTrue(s.IsCollected("cliff_cache_1"));
            Assert.IsTrue(s.AddLore("lore_hymn"));
            Assert.IsFalse(s.AddLore("lore_hymn"));
            s.ClearStage("Cliffside"); s.ClearStage("Cliffside");
            Assert.AreEqual(1, s.stagesCleared.Count);
        }

        [Test]
        public void NormalizeRepairsBadFiles()
        {
            var s = new SaveData { notes = -5, items = null, sequences = null, quickItem = "ghost" };
            s.Normalize();
            Assert.AreEqual(0, s.notes);
            Assert.AreEqual(0, s.items.Count);
            Assert.AreEqual("", s.quickItem);
        }

        [Test]
        public void RewardRollStaysInRange()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                int n = Rewards.RollNotes(20, 40, rng);
                Assert.IsTrue(n >= 20 && n <= 40, n.ToString());
            }
        }
    }
}
