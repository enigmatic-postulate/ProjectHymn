using NUnit.Framework;
using ProjectHymn.Combat;

namespace ProjectHymn.Tests
{
    public class RhythmChainTests
    {
        const NoteButton A = NoteButton.One, B = NoteButton.Two;
        RhythmChain _chain;

        [SetUp]
        public void SetUp()
        {
            _chain = new RhythmChain();
            _chain.SetSequences(new[]
            {
                new SequencePattern("Crossing Cuts", new[] { A, B, A }),
                new SequencePattern("Powerslice", new[] { B, B }),
                new SequencePattern("Tempest", new[] { A, A, A, B, A, A, A }),
            });
        }

        ChainStep On(long beat, NoteButton b) => _chain.Register(beat, b, true);

        [Test]
        public void SingleNotesDealNoDamage()
        {
            var s = On(0, A);
            Assert.AreEqual(1, s.ChainLength);
            Assert.IsFalse(s.DealsDamage);
            Assert.IsTrue(On(1, A).DealsDamage);
        }

        [Test]
        public void GapStartsANewChain()
        {
            On(0, A); On(1, A);
            var s = On(3, A); // beat 2 skipped
            Assert.AreEqual(1, s.ChainLength);
            Assert.IsFalse(s.DealsDamage);
        }

        [Test]
        public void OffBeatAndDoublePressBreak()
        {
            On(0, A); On(1, B);
            var off = _chain.Register(2, A, false);
            Assert.IsTrue(off.Broke);
            Assert.IsFalse(off.DealsDamage);
            On(3, A);
            var dbl = On(3, B);
            Assert.IsTrue(dbl.Broke);
            Assert.AreEqual(0, _chain.ChainLength);
        }

        [Test]
        public void CrossingCutsCompletes()
        {
            var s0 = On(0, A);
            Assert.AreEqual(0, s0.Sequence); // shortest candidate starting with 1
            Assert.AreEqual(0, s0.Step);
            var s1 = On(1, B);
            Assert.AreEqual(0, s1.Sequence);
            Assert.AreEqual(1, s1.Step);
            var s2 = On(2, A);
            Assert.IsTrue(s2.Completed);
            Assert.AreEqual(2, s2.Step);
            Assert.AreEqual(3, s2.ChainLength);
        }

        [Test]
        public void PowersliceIsTwoHeavies()
        {
            var first = On(0, B);
            Assert.AreEqual(1, first.Sequence);
            Assert.IsFalse(first.DealsDamage);
            var second = On(1, B);
            Assert.IsTrue(second.Completed);
            Assert.IsTrue(second.DealsDamage);
        }

        [Test]
        public void TempestFollowsTheLongPattern()
        {
            var notes = new[] { A, A, A, B, A, A, A };
            ChainStep last = default;
            for (int i = 0; i < notes.Length; i++)
            {
                last = On(i, notes[i]);
                Assert.AreEqual(i, last.Step, $"step {i}");
                if (i >= 1) Assert.AreEqual(2, last.Sequence, $"note {i} follows Tempest");
            }
            Assert.IsTrue(last.Completed);
            Assert.AreEqual(7, last.ChainLength);
        }

        [Test]
        public void ChainKeepsGoingAfterACompletion()
        {
            On(0, B); On(1, B);              // Powerslice done
            var s = On(2, A);                 // a fresh match, still on the chain
            Assert.AreEqual(3, s.ChainLength);
            Assert.IsTrue(s.DealsDamage);
            Assert.AreEqual(0, s.Step);
        }

        [Test]
        public void WrongTurnFallsBackToTheLongestMatchingTail()
        {
            On(0, A); On(1, A);               // Tempest so far
            var s = On(2, B);                 // 1-1-2 matches nothing; tail 1-2 is Crossing Cuts
            Assert.AreEqual(0, s.Sequence);
            Assert.AreEqual(1, s.Step);
        }

        [Test]
        public void PredictionNamesTheNextNote()
        {
            On(0, A);
            Assert.IsTrue(_chain.Predict(out int seq, out int step, out var button));
            Assert.AreEqual(0, seq);          // Crossing Cuts is the shortest candidate
            Assert.AreEqual(B, button);
            Assert.AreEqual(1, step);
            On(1, A);
            Assert.IsTrue(_chain.Predict(out seq, out step, out button));
            Assert.AreEqual(2, seq);          // only Tempest starts 1-1
            Assert.AreEqual(A, button);
            On(2, A);
            Assert.IsTrue(_chain.Predict(out seq, out step, out button));
            Assert.AreEqual(B, button);       // the punch
        }

        [Test]
        public void ProgressForTheHud()
        {
            On(0, A); On(1, A);
            Assert.AreEqual(0, _chain.Progress(0));
            Assert.AreEqual(0, _chain.Progress(1));
            Assert.AreEqual(2, _chain.Progress(2));
        }
    }
}
