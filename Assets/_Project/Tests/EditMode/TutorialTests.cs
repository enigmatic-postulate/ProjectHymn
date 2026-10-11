using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    /// <summary>The Practice Yard's lessons (Slice 10).</summary>
    public class TutorialTests
    {
        const NoteButton A = NoteButton.One, B = NoteButton.Two;

        static TutorialPlan Started()
        {
            var p = new TutorialPlan();
            p.Begin();
            return p;
        }

        [Test]
        public void TheLessonsInOrder()
        {
            var p = Started();
            CollectionAssert.AreEqual(new[]
            {
                LessonKind.Calibrate, LessonKind.Beat, LessonKind.TwoNotes, LessonKind.Sequences, LessonKind.Guard,
                LessonKind.Parry, LessonKind.Dodge, LessonKind.Launch, LessonKind.Bout,
            }, p.Lessons.ConvertAll(l => l.Kind));
            Assert.AreEqual(LessonKind.Calibrate, p.Current.Kind);
            Assert.IsTrue(p.Calibrated());
            Assert.AreEqual(LessonKind.Beat, p.Current.Kind);
        }

        [Test]
        public void TheBeatNeedsEightInARow()
        {
            var p = Started();
            p.Calibrated();
            for (int i = 0; i < 7; i++) Assert.IsFalse(p.Note(A, true, Judgement.Great));
            Assert.IsFalse(p.Note(A, false, Judgement.OffBeat), "off the beat");
            Assert.AreEqual(0, p.Count, "the streak starts over");
            Assert.IsFalse(p.Note(B, true, Judgement.Perfect), "heavy notes don't count here");
            for (int i = 0; i < 7; i++) p.Note(A, true, Judgement.Good);
            Assert.IsTrue(p.Note(A, true, Judgement.TruePerfect));
            Assert.AreEqual(LessonKind.TwoNotes, p.Current.Kind);
        }

        [Test]
        public void TwoNotesNeedsBothButtons()
        {
            var p = Started();
            p.SkipTo(LessonKind.TwoNotes);
            for (int i = 0; i < 6; i++) Assert.IsFalse(p.Note(A, true, Judgement.Great));
            Assert.AreEqual("light 4/4   heavy 0/4", p.ProgressText);
            for (int i = 0; i < 3; i++) Assert.IsFalse(p.Note(B, true, Judgement.Great));
            Assert.IsFalse(p.Note(B, false, Judgement.OffBeat));
            Assert.IsTrue(p.Note(B, true, Judgement.Good));
            Assert.AreEqual(LessonKind.Sequences, p.Current.Kind);
        }

        [Test]
        public void SequencesNeedCrossingCutsAndPowerslice()
        {
            var p = Started();
            p.SkipTo(LessonKind.Sequences);
            Assert.IsFalse(p.Sequence("CrossingCuts", false));
            Assert.IsFalse(p.Sequence("RisingVerse", true));
            Assert.IsFalse(p.Sequence("CrossingCuts", true));
            Assert.IsTrue(p.Sequence("Powerslice", false));
        }

        [Test]
        public void GuardParryAndDodge()
        {
            var p = Started();
            p.SkipTo(LessonKind.Guard);
            Assert.IsFalse(p.Defense(DefenseResult.Hit));
            Assert.IsFalse(p.Defense(DefenseResult.Blocked));
            Assert.IsFalse(p.Defense(DefenseResult.Parried));
            Assert.IsTrue(p.Defense(DefenseResult.Blocked));
            Assert.AreEqual(LessonKind.Parry, p.Current.Kind);
            Assert.IsFalse(p.Defense(DefenseResult.Blocked), "a block isn't a parry");
            p.Defense(DefenseResult.Parried);
            Assert.IsTrue(p.Defense(DefenseResult.Parried));
            Assert.AreEqual(LessonKind.Dodge, p.Current.Kind);
            Assert.IsFalse(p.Defense(DefenseResult.Parried));
            p.Defense(DefenseResult.Dodged);
            Assert.IsTrue(p.Defense(DefenseResult.Dodged));
            Assert.AreEqual(LessonKind.Launch, p.Current.Kind);
        }

        [Test]
        public void LaunchThenTheBoutFinishesTheYard()
        {
            var p = Started();
            p.SkipTo(LessonKind.Launch);
            Assert.IsFalse(p.Sequence("RisingVerse", false), "it has to land");
            Assert.IsTrue(p.Sequence("RisingVerse", true));
            Assert.IsFalse(p.Done);
            Assert.IsTrue(p.BoutWon());
            Assert.IsTrue(p.Done);
            Assert.IsNull(p.Current);
        }

        [Test]
        public void ActionsOutsideTheirLessonDontCount()
        {
            var p = Started();
            Assert.IsFalse(p.BoutWon());
            Assert.IsFalse(p.Defense(DefenseResult.Parried));
            Assert.IsFalse(p.Note(A, true, Judgement.Perfect));
            Assert.AreEqual(LessonKind.Calibrate, p.Current.Kind);
        }

        [Test]
        public void PromptsRepeatFromTheDownbeat()
        {
            const string cc = "1-2-1-.";
            Assert.AreEqual(A, TutorialPlan.PromptAt(cc, 0, 5, 4));
            Assert.AreEqual(B, TutorialPlan.PromptAt(cc, 1, 5, 4));
            Assert.AreEqual(A, TutorialPlan.PromptAt(cc, 2, 5, 4));
            Assert.IsNull(TutorialPlan.PromptAt(cc, 3, 5, 4));
            const string two = "1-2-1-.-2-2-.-.";          // two bars
            Assert.AreEqual(A, TutorialPlan.PromptAt(two, 0, 6, 4));
            Assert.AreEqual(B, TutorialPlan.PromptAt(two, 0, 7, 4));
            Assert.IsNull(TutorialPlan.PromptAt(two, 2, 7, 4));
            Assert.IsNull(TutorialPlan.PromptAt("", 0, 0, 4));
        }

        [Test]
        public void TheYardsRewardLevelsWithoutClassOffers()
        {
            var rules = ProgressionRules.Default;
            var s = SaveData.NewGame(ProgressionRules.Minstrel, null);
            Assert.AreEqual(1, rules.GrantXp(s, 150));
            Assert.AreEqual(2, s.level);
            Assert.AreEqual(50, s.xp);
            s.level = 5; s.xp = 0;
            Assert.AreEqual(0, rules.GrantXp(s, 1000), "level 6 comes from a clear, with its class rules");
            Assert.AreEqual(5, s.level);
        }

        [Test]
        public void TheYardIsRememberedInTheSave()
        {
            var s = SaveData.NewGame(ProgressionRules.Minstrel, null);
            Assert.IsFalse(s.tutorialDone);
            s.tutorialDone = true;
            s.Normalize();
            Assert.IsTrue(s.tutorialDone);
        }
    }
}
