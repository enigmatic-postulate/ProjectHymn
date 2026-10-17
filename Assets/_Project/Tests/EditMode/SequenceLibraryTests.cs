using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    /// <summary>The sequence library (GDD update 18): home meters, class trees, length-scaled finishers.</summary>
    public class SequenceLibraryTests
    {
        const NoteButton A = NoteButton.One, B = NoteButton.Two;

        static RhythmChain ChainOf(params string[] keys)
        {
            var chain = new RhythmChain();
            chain.SetSequences(keys.Select(k => SequenceLibrary.Find(k)).Select(e => new SequencePattern(e.Name, e.Notes, e, e.Meter)));
            return chain;
        }

        [Test]
        public void NoTwoSequencesShareAPatternInOneMeter()
        {
            foreach (var g in SequenceLibrary.All.GroupBy(e => e.Meter))
            {
                var seen = new HashSet<string>();
                foreach (var e in g) Assert.IsTrue(seen.Add(e.NotesText), $"{e.Name} repeats a {g.Key} pattern");
            }
            Assert.AreEqual(SequenceLibrary.All.Count, SequenceLibrary.All.Select(e => e.Key).Distinct().Count());
        }

        [Test]
        public void FinishersScaleWithLength()
        {
            foreach (var e in SequenceLibrary.All)
            {
                var (min, max) = SequenceLibrary.FinisherBand(e.Length);
                Assert.IsTrue(e.Finisher >= min - 1e-4f && e.Finisher <= max + 1e-4f, $"{e.Name} x{e.Finisher} outside {min}-{max}");
            }
        }

        [Test]
        public void TheTreesInTheSlice()
        {
            CollectionAssert.AreEqual(new[] { "CrossingCuts", "Powerslice", "RisingVerse", "Swell", "Barcarolle" }, SequenceLibrary.TreeKeys(SequenceLibrary.Minstrel));
            Assert.IsTrue(SequenceLibrary.Tree(SequenceLibrary.Fighter).All(e => e.Meter == SequenceLibrary.Common), "the Fighter's tree is 4/4 by design");
            Assert.AreEqual(SequenceLibrary.Waltz, SequenceLibrary.Find("LanternStep").Meter);
            Assert.AreEqual(SequenceLibrary.Seven, SequenceLibrary.Find("Crusade").Meter);
            Assert.AreEqual(SequenceLibrary.Balladeer, SequenceLibrary.Find("Tempest").Tree);
            Assert.AreEqual(SequenceEffect.Launch, SequenceLibrary.Find("RisingVerse").Effect);
            Assert.AreEqual(SequenceEffect.Push, SequenceLibrary.Find("Bulwark").Effect);
            Assert.AreEqual(5, SequenceLibrary.Tree(SequenceLibrary.Minstrel).Count);
            Assert.AreEqual(3, SequenceLibrary.Tree(SequenceLibrary.Fighter).Count);
            Assert.AreEqual(4, SequenceLibrary.Tree(SequenceLibrary.Warden).Count);
            Assert.AreEqual(4, SequenceLibrary.Tree(SequenceLibrary.Crusader).Count);
            Assert.AreEqual(SequenceLibrary.Six, SequenceLibrary.Find("Barcarolle").Meter);
            Assert.AreEqual(SequenceLibrary.Nine, SequenceLibrary.Find("Riptide").Meter);
            Assert.AreEqual(SequenceEffect.Push, SequenceLibrary.Find("Undertow").Effect);
            Assert.AreEqual(ProgressionRules.Minstrel, SequenceLibrary.Minstrel);
            Assert.AreEqual(ProgressionRules.Crusader, SequenceLibrary.Crusader);
        }

        [Test]
        public void ASequenceOnlyPlaysInItsHomeMeter()
        {
            var chain = ChainOf("CrossingCuts", "LanternStep");
            chain.Register(0, A, true, "3/4");
            chain.Register(1, B, true, "3/4");
            var s = chain.Register(2, A, true, "3/4");
            Assert.IsFalse(s.Completed, "Crossing Cuts is a 4/4 sequence");

            chain.Register(10, A, true, "3/4");
            chain.Register(11, B, true, "3/4");
            Assert.IsTrue(chain.Register(12, B, true, "3/4").Completed, "Lantern Step in 3/4");

            chain.Register(20, A, true, "4/4");
            chain.Register(21, B, true, "4/4");
            Assert.IsFalse(chain.Register(22, B, true, "4/4").Completed, "Lantern Step does nothing in 4/4");
        }

        [Test]
        public void AMeterChangeStartsTheMatchAfresh()
        {
            var chain = ChainOf("CrossingCuts");
            chain.Register(0, A, true, "4/4");
            chain.Register(1, B, true, "4/4");
            var s = chain.Register(2, A, true, "3/4");      // the bar changed meter mid-pattern
            Assert.IsFalse(s.Completed);
            Assert.AreEqual(3, s.ChainLength, "the chain itself goes on");
        }

        [Test]
        public void TheLongestMatchCarriesOnAfterAShorterFinisher()
        {
            var chain = ChainOf("Brandish", "IronCount", "Bulwark");
            chain.Register(0, B, true, "4/4");
            var brandish = chain.Register(1, A, true, "4/4");
            Assert.IsTrue(brandish.Completed);
            Assert.AreEqual("Brandish", chain.Sequences[brandish.Sequence].Name);

            chain.Register(2, B, true, "4/4");
            var iron = chain.Register(3, A, true, "4/4");
            Assert.IsTrue(iron.Completed);
            Assert.AreEqual("Iron Count", chain.Sequences[iron.Sequence].Name);
            Assert.AreEqual(3, iron.Step);

            chain.Register(10, B, true, "4/4");
            chain.Register(11, A, true, "4/4");
            var bulwark = chain.Register(12, A, true, "4/4");
            Assert.AreEqual("Bulwark", chain.Sequences[bulwark.Sequence].Name);
            Assert.IsTrue(bulwark.Completed);
        }

        [Test]
        public void PowersliceStillRepeatsWithoutTheLongerOnes()
        {
            var chain = ChainOf("Powerslice");
            chain.Register(0, B, true, "4/4");
            Assert.IsTrue(chain.Register(1, B, true, "4/4").Completed);
            chain.Register(2, B, true, "4/4");
            Assert.IsTrue(chain.Register(3, B, true, "4/4").Completed);
        }

        [Test]
        public void HammerfallFollowsPowerslice()
        {
            var chain = ChainOf("Powerslice", "Hammerfall");
            chain.Register(0, B, true, "4/4");
            Assert.IsTrue(chain.Register(1, B, true, "4/4").Completed);
            var h = chain.Register(2, B, true, "4/4");
            Assert.IsTrue(h.Completed);
            Assert.AreEqual("Hammerfall", chain.Sequences[h.Sequence].Name);
        }

        [Test]
        public void CrusadeIsSevenEighthsOnly()
        {
            var chain = ChainOf("Crusade", "Brandish");
            var notes = new[] { B, A, B, A, B, A, B };
            ChainStep last = default;
            for (int i = 0; i < notes.Length; i++) last = chain.Register(i, notes[i], true, "7/8");
            Assert.IsTrue(last.Completed);
            Assert.AreEqual("Crusade", chain.Sequences[last.Sequence].Name);
        }

        [Test]
        public void BarcarolleCarriesOnFromSwellInSixEight()
        {
            var chain = ChainOf("Swell", "Barcarolle", "Undertow");
            var notes = new[] { B, A, A, B, A, A };
            var steps = new List<ChainStep>();
            for (int i = 0; i < notes.Length; i++) steps.Add(chain.Register(i, notes[i], true, "6/8"));
            Assert.IsTrue(steps[2].Completed, "Swell finishes on the third note");
            Assert.AreEqual("Swell", chain.Sequences[steps[2].Sequence].Name);
            Assert.IsTrue(steps[5].Completed, "and Barcarolle on the sixth");
            Assert.AreEqual("Barcarolle", chain.Sequences[steps[5].Sequence].Name);

            var other = ChainOf("Swell", "CrossingCuts");
            ChainStep last = default;
            for (int i = 0; i < 3; i++) last = other.Register(i, new[] { B, A, A }[i], true, "4/4");
            Assert.IsFalse(last.Completed, "Swell is a 6/8 sequence: nothing in 4/4");
        }

        [Test]
        public void RiptideIsNineEighthsOnly()
        {
            var chain = ChainOf("Riptide");
            var notes = new[] { B, A, A, B, A, A, B, A, A };
            ChainStep last = default;
            for (int i = 0; i < notes.Length; i++) last = chain.Register(i, notes[i], true, "9/8");
            Assert.IsTrue(last.Completed);
            Assert.AreEqual(3.0f, SequenceLibrary.Find("Riptide").Finisher);
        }

        // ---------------- Learning ----------------

        static readonly ClassSpec[] Classes =
        {
            new ClassSpec(ProgressionRules.Minstrel, "Minstrel", ClassBracket.Base),
            new ClassSpec(ProgressionRules.Fighter, "Fighter", ClassBracket.Low),
            new ClassSpec(ProgressionRules.Warden, "Warden", ClassBracket.High, PlayLean.Guard, "sword"),
            new ClassSpec(ProgressionRules.Crusader, "Crusader", ClassBracket.High, PlayLean.Dodge, "greatsword"),
        };

        [Test]
        public void ANewGameKnowsTheMinstrelsTree()
        {
            var s = SaveData.NewGame(ProgressionRules.Minstrel, null);
            foreach (var k in SequenceLibrary.TreeKeys(SequenceLibrary.Minstrel)) Assert.IsTrue(s.Knows(k), k);
            Assert.IsFalse(s.Knows("Tempest"));
        }

        [Test]
        public void TakingAClassLearnsItsTreeAndKeepsTheRest()
        {
            var s = SaveData.NewGame(ProgressionRules.Minstrel, null);
            ProgressionRules.SetClass(s, ProgressionRules.Warden, Classes);
            ProgressionRules.SetClass(s, ProgressionRules.Crusader, Classes);
            foreach (var k in new[] { "CrossingCuts", "Bulwark", "LanternStep", "Brandish", "Crusade" }) Assert.IsTrue(s.Knows(k), k);
            Assert.IsFalse(s.Knows("Drill"));
            var learned = ProgressionRules.Learned(s, ProgressionRules.Fighter);
            CollectionAssert.AreEqual(new[] { "Drill", "Hammerfall", "IronCount" }, learned);
            Assert.AreEqual(0, ProgressionRules.Learned(s, ProgressionRules.Fighter).Count, "already known");
        }

        [Test]
        public void TheFighterFallbackLearnsTheFightersTree()
        {
            var rules = ProgressionRules.Default;
            var s = SaveData.NewGame(ProgressionRules.Minstrel, null);
            s.level = 5;
            s.xp = rules.XpToNext(5) - 1;
            var log = new PlaystyleLog { MaxHearts = 5 };
            log.AddKill();
            var o = rules.Apply(s, "Cliffside", new StageResult(60, StageRank.B, StageRank.B, new List<string>(), 1.0, true), log, Classes);
            Assert.AreEqual(ProgressionRules.Fighter, s.classId);
            Assert.IsTrue(s.Knows("Hammerfall"));
            CollectionAssert.AreEqual(new[] { "Drill", "Hammerfall", "IronCount" }, o.SequencesLearned);
        }

        [Test]
        public void LoadingASaveLearnsItsClassesTree()
        {
            var s = SaveData.NewGame(ProgressionRules.Minstrel, null);
            s.classId = ProgressionRules.Warden;
            s.sequences.Clear();
            s.Normalize();
            Assert.IsTrue(s.Knows("Bulwark"));
            Assert.IsTrue(s.Knows("RisingVerse"), "the Minstrel's tree is always known");
        }
    }
}
