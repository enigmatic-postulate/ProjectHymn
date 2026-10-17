using NUnit.Framework;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    /// <summary>The story around the run (VS-5 Part A): chapters, villagers' lines, verse reveals and the ending.</summary>
    public class StoryTests
    {
        static SaveData Cleared(params string[] stages)
        {
            var s = SaveData.NewGame(ProgressionRules.Minstrel, null);
            foreach (var st in stages) s.ClearStage(st);
            return s;
        }

        [Test]
        public void TheChapterCountsVersesHomeInOrder()
        {
            Assert.AreEqual(0, Story.Chapter(Cleared()));
            Assert.AreEqual(0, Story.Chapter(Cleared("Yard", "Arena", "Spring")), "side stages hold no verse");
            Assert.AreEqual(1, Story.Chapter(Cleared("Cliffside")));
            Assert.AreEqual(2, Story.Chapter(Cleared("Cliffside", "Belfry")));
            Assert.AreEqual(3, Story.Chapter(Cleared("Cliffside", "Belfry", "Summit")));
            Assert.AreEqual(0, Story.Chapter(Cleared("Belfry")), "out of order (free play) doesn't skip ahead");
            Assert.AreEqual(0, Story.Chapter(null));
        }

        [Test]
        public void EveryVillagerHasLinesForEveryChapter()
        {
            foreach (var v in Story.Villagers)
                for (int ch = 0; ch <= Story.Verses; ch++)
                {
                    var lines = Story.LinesFor(v, ch);
                    Assert.IsTrue(lines.Length > 0, $"{v}, chapter {ch}");
                    foreach (var l in lines)
                    {
                        Assert.IsFalse(string.IsNullOrWhiteSpace(l), $"{v}, chapter {ch}");
                        Assert.IsTrue(l.Length <= 160, $"{v}, chapter {ch}: too long for the box ({l.Length}): {l}");
                    }
                }
        }

        [Test]
        public void LinesChangeAsVersesComeHome()
        {
            foreach (var v in Story.Villagers)
                Assert.AreNotEqual(Story.LinesFor(v, 0)[0], Story.LinesFor(v, 3)[0], v);
            Assert.AreEqual(Story.LinesFor("Elder", 3), Story.LinesFor("Elder", 9), "past the last chapter keeps the last lines");
            Assert.AreEqual(0, Story.LinesFor("Nobody", 0).Length);
            Assert.AreEqual("lore_village", Story.LoreFrom("Elder"));
            Assert.AreEqual("", Story.LoreFrom("Fisher"));
        }

        [Test]
        public void EachNewVerseIsCelebratedOnce()
        {
            var s = Cleared();
            Assert.AreEqual(0, Story.VerseToReveal(s));
            s.ClearStage("Cliffside");
            Assert.AreEqual(1, Story.VerseToReveal(s));
            Story.MarkRevealed(s, 1);
            Assert.AreEqual(0, Story.VerseToReveal(s));
            s.ClearStage("Belfry");
            Assert.AreEqual(2, Story.VerseToReveal(s));
            Story.MarkRevealed(s, 2);
            Assert.AreEqual(0, Story.VerseToReveal(s));
        }

        [Test]
        public void AnOldSaveGetsOneMomentNotThree()
        {
            var s = Cleared("Cliffside", "Belfry", "Summit");
            Assert.AreEqual(3, Story.VerseToReveal(s));
            Story.MarkRevealed(s, 3);
            Assert.IsTrue(s.HasStory("verse1") && s.HasStory("verse2") && s.HasStory("verse3"));
            Assert.AreEqual(0, Story.VerseToReveal(s));
        }

        [Test]
        public void TheEndingPlaysOnceAfterTheSummit()
        {
            var s = Cleared("Cliffside", "Belfry");
            Assert.IsFalse(Story.EndingDue(s, "Belfry"));
            Assert.IsFalse(Story.EndingDue(s, "Summit"), "not cleared yet (a failed rank)");
            s.ClearStage("Summit");
            Assert.IsTrue(Story.EndingDue(s, "Summit"));
            Assert.IsFalse(Story.EndingDue(s, "Arena"));
            s.MarkStory(Story.EndingSeen);
            Assert.IsFalse(Story.EndingDue(s, "Summit"));
        }

        [Test]
        public void StoryFlagsSurviveNormalize()
        {
            var s = Cleared();
            s.story = null;
            s.Normalize();
            Assert.IsNotNull(s.story);
            Assert.IsTrue(s.MarkStory("ending"));
            Assert.IsFalse(s.MarkStory("ending"));
            Assert.IsFalse(s.MarkStory(""));
        }

        [Test]
        public void CardsAndVerseNames()
        {
            Assert.AreEqual(4, Story.Intro.Length);
            Assert.AreEqual(3, Story.Ending.Length);
            foreach (var c in Story.Intro) Assert.IsTrue(!string.IsNullOrEmpty(c.Image) && c.Text.Length > 20 && c.Text.Length <= 200, c.Image);
            foreach (var c in Story.Ending) Assert.IsTrue(!string.IsNullOrEmpty(c.Image) && c.Text.Length > 20 && c.Text.Length <= 200, c.Image);
            foreach (var c in Story.DeepIntro) Assert.IsTrue(!string.IsNullOrEmpty(c.Image) && c.Text.Length > 20 && c.Text.Length <= 200, c.Image);
            foreach (var c in Story.DeepEnding) Assert.IsTrue(!string.IsNullOrEmpty(c.Image) && c.Text.Length > 20 && c.Text.Length <= 200, c.Image);
            Assert.AreEqual(Story.Verses, Story.VerseStages.Length);
            Assert.AreEqual(Story.Verses, Story.VerseNames.Length);
            Assert.AreEqual("The Verse of the Tide", Story.VerseTitle(1));
            Assert.AreEqual("", Story.VerseTitle(0));
            Assert.AreEqual(2, Story.VerseOf("Belfry"));
            Assert.AreEqual(0, Story.VerseOf("Arena"));
            for (int v = 1; v <= Story.Verses; v++) Assert.IsTrue(Story.RevealLine(v).Length > 0);
            Assert.IsTrue(Story.Credits.Length > 10);
        }

        [Test]
        public void ChapterTwoHoldsVersesFourToSix()
        {
            var s = Cleared("Cliffside", "Belfry", "Summit");
            Assert.AreEqual(3, Story.Chapter(s));
            Story.MarkRevealed(s, 3);
            s.ClearStage("Nave");
            Assert.AreEqual(4, Story.Chapter(s));
            Assert.AreEqual(4, Story.VerseToReveal(s), "the Air Bell celebrates it");
            Assert.AreEqual(1, Story.RegionOf(4));
            Assert.AreEqual(0, Story.RegionOf(3));
            Assert.AreEqual(4, Story.VerseOf("Nave"));
            Assert.AreEqual(6, Story.VerseOf("Apse"));
            Assert.AreEqual("The Verse of the Deep", Story.VerseTitle(6));
            Assert.AreEqual(3, Story.Chapter(Cleared("Cliffside", "Belfry", "Summit", "Loft")), "the deep counts in order too");
        }

        [Test]
        public void EachChapterHasItsOwnEnding()
        {
            var s = Cleared("Cliffside", "Belfry", "Summit");
            Assert.IsTrue(Story.EndingDue(s, "Summit"));
            s.MarkStory(Story.EndingFlag("Summit"));
            Assert.IsFalse(Story.EndingDue(s, "Summit"));
            Assert.IsFalse(Story.EndingDue(s, "Apse"), "not cleared yet");
            s.ClearStage("Nave"); s.ClearStage("Loft"); s.ClearStage("Apse");
            Assert.IsTrue(Story.EndingDue(s, "Apse"));
            Assert.IsFalse(Story.EndingDue(s, "Loft"));
            s.MarkStory(Story.EndingFlag("Apse"));
            Assert.IsFalse(Story.EndingDue(s, "Apse"));
            Assert.AreEqual(Story.DeepEnding, Story.EndingCards("Apse"));
            Assert.AreEqual(Story.Ending, Story.EndingCards("Summit"));
            Assert.AreEqual("AirBell", Story.EndingHub("Apse"));
            Assert.AreEqual("Village", Story.EndingHub("Summit"));
            Assert.AreEqual("", Story.EndingFlag("Arena"));
        }

        [Test]
        public void TheDiversSpeakFromChapterThree()
        {
            foreach (var d in Story.Divers)
            {
                Assert.AreEqual(3, Story.FirstChapterOf(d));
                for (int ch = 0; ch <= Story.Verses; ch++)
                {
                    var lines = Story.LinesFor(d, ch);
                    Assert.IsTrue(lines.Length > 0, $"{d}, chapter {ch}");
                    foreach (var l in lines) Assert.IsTrue(l.Length <= 160, $"{d}, chapter {ch}: too long ({l.Length}): {l}");
                }
                Assert.AreEqual(Story.LinesFor(d, 0), Story.LinesFor(d, 3), d + ": before the summit they say their first lines");
                Assert.AreNotEqual(Story.LinesFor(d, 3)[0], Story.LinesFor(d, 6)[0], d + ": their lines change as deep verses come home");
            }
            Assert.AreEqual(0, Story.FirstChapterOf("Elder"));
        }
    }
}
