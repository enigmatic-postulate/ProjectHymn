using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    /// <summary>The class research trees (VS-7): what each feat asks for, and that a run only earns its own class's.</summary>
    public class FeatsTests
    {
        static PlaystyleLog Log(int attacks = 0, int offBeat = 0, int perfect = 0, int sequences = 0, int parries = 0,
                                int blocks = 0, int dodges = 0, int hits = 0, int hearts = 0, int kills = 0, int maxHearts = 5)
        {
            var log = new PlaystyleLog { MaxHearts = maxHearts };
            for (int i = 0; i < perfect; i++) log.AddNote(Judgement.Perfect);
            for (int i = 0; i < offBeat; i++) log.AddNote(Judgement.OffBeat);
            for (int i = perfect + offBeat; i < attacks; i++) log.AddNote(Judgement.Great);
            for (int i = 0; i < sequences; i++) log.AddSequence();
            for (int i = 0; i < kills; i++) log.AddKill();
            for (int i = 0; i < parries; i++) log.AddDefense(DefenseResult.Parried);
            for (int i = 0; i < blocks; i++) log.AddDefense(DefenseResult.Blocked);
            for (int i = 0; i < dodges; i++) log.AddDefense(DefenseResult.Dodged);
            for (int i = 0; i < hits; i++) log.AddHurt(i < hearts ? 1 : 0);
            return log;
        }

        static FeatRun Run(string stage, string cls, PlaystyleLog log, StageRank rank = StageRank.A, bool cleared = true, bool debug = false) =>
            new FeatRun(stage, cls, cleared, rank, 40.0, 50.0, log, debug);

        static bool Earns(string key, FeatRun run) => Feats.Find(key).EarnedBy(run);

        [Test]
        public void EveryFeatBelongsToAClassAndNamesItsAbility()
        {
            Assert.AreEqual(Feats.All.Count, Feats.All.Select(f => f.Key).Distinct().Count(), "keys are unique");
            foreach (var f in Feats.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(f.Name), f.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(f.How), f.Key + " needs to say what to do");
                Assert.IsFalse(string.IsNullOrWhiteSpace(f.Ability), f.Key + " needs to earn something");
                Assert.IsNotNull(SequenceLibrary.Tree(f.Tree), f.Key);
            }
            foreach (var tree in new[] { ProgressionRules.Minstrel, ProgressionRules.Fighter, ProgressionRules.Warden, ProgressionRules.Crusader })
                Assert.AreEqual(3, Feats.Tree(tree).Count, tree + " has three feats");
        }

        [Test]
        public void AFeatIsOnlyEarnedByItsOwnClassOnAClearedRun()
        {
            var log = Log(attacks: 30, sequences: 8);
            Assert.IsTrue(Earns("carry_the_tune", Run("Cliffside", ProgressionRules.Minstrel, log)));
            Assert.IsFalse(Earns("carry_the_tune", Run("Cliffside", ProgressionRules.Fighter, log)), "a Fighter doesn't fill the Minstrel's tree");
            Assert.IsFalse(Earns("carry_the_tune", Run("Cliffside", ProgressionRules.Minstrel, log, cleared: false)), "the stage has to be cleared");
            Assert.IsFalse(Earns("carry_the_tune", Run("Cliffside", ProgressionRules.Minstrel, log, debug: true)), "playtest keys earn nothing");
        }

        [Test]
        public void WhereAFeatCanBeEarned()
        {
            var deep = Log(attacks: 20, sequences: 4);
            Assert.IsTrue(Earns("deep_measure", Run("Nave", ProgressionRules.Minstrel, deep)));
            Assert.IsTrue(Earns("deep_measure", Run("Apse", ProgressionRules.Minstrel, deep)));
            Assert.IsFalse(Earns("deep_measure", Run("Cliffside", ProgressionRules.Minstrel, deep)), "the Drowned Nave only");

            var boss = Log(attacks: 20, parries: 9, hits: 1);
            Assert.IsTrue(Earns("the_cantors_count", Run("Summit", ProgressionRules.Warden, boss)));
            Assert.IsFalse(Earns("the_cantors_count", Run("Belfry", ProgressionRules.Warden, boss)), "bosses only");
            Assert.IsTrue(Feats.IsBoss("Apse") && Feats.IsDeep("Loft") && !Feats.IsDeep("Summit"));
        }

        [Test]
        public void TheMinstrelsTreeWantsSequencesAndPerfectNotes()
        {
            Assert.IsFalse(Earns("carry_the_tune", Run("Cliffside", ProgressionRules.Minstrel, Log(attacks: 30, sequences: 7))));
            Assert.IsTrue(Earns("perfect_cadence", Run("Cliffside", ProgressionRules.Minstrel, Log(attacks: 20, perfect: 10))));
            Assert.IsFalse(Earns("perfect_cadence", Run("Cliffside", ProgressionRules.Minstrel, Log(attacks: 20, perfect: 9))), "half the notes");
            Assert.IsFalse(Earns("perfect_cadence", Run("Cliffside", ProgressionRules.Minstrel, Log(attacks: 10, perfect: 10))), "ten notes is too few to prove it");
        }

        [Test]
        public void TheWardensTreeWantsTheGuard()
        {
            // The GDD's own example: beat it on parries and blocks alone.
            Assert.IsTrue(Earns("shield_wall", Run("Belfry", ProgressionRules.Warden, Log(attacks: 20, parries: 4, blocks: 3))));
            Assert.IsFalse(Earns("shield_wall", Run("Belfry", ProgressionRules.Warden, Log(attacks: 20, parries: 4, blocks: 3, hits: 1))), "a hit landed");
            Assert.IsFalse(Earns("shield_wall", Run("Belfry", ProgressionRules.Warden, Log(attacks: 20, parries: 4, blocks: 2, dodges: 1))), "dodging isn't guarding");
            Assert.IsTrue(Earns("counterweight", Run("Nave", ProgressionRules.Warden, Log(attacks: 20, parries: 10))));
            Assert.IsFalse(Earns("the_cantors_count", Run("Summit", ProgressionRules.Warden, Log(attacks: 20, parries: 8, hits: 9))), "more parries than hits taken");
        }

        [Test]
        public void TheCrusadersTreeIsTheReasonToDodge()
        {
            // One dodge in 18 runs across the playtests: the dodge needs something to be for.
            Assert.IsTrue(Earns("riptide_runner", Run("Arena", ProgressionRules.Crusader, Log(attacks: 20, dodges: 5))));
            Assert.IsFalse(Earns("riptide_runner", Run("Arena", ProgressionRules.Crusader, Log(attacks: 20, dodges: 4))));
            Assert.IsTrue(Earns("undertow", Run("Loft", ProgressionRules.Crusader, Log(attacks: 20, dodges: 3))));
            Assert.IsFalse(Earns("undertow", Run("Summit", ProgressionRules.Crusader, Log(attacks: 20, dodges: 3))), "the Drowned Nave only");
            Assert.IsTrue(Earns("eye_of_the_storm", Run("Apse", ProgressionRules.Crusader, Log(attacks: 20, parries: 2, dodges: 2))));
            Assert.IsFalse(Earns("eye_of_the_storm", Run("Apse", ProgressionRules.Crusader, Log(attacks: 20, dodges: 2, hits: 1))));
        }

        [Test]
        public void TheFightersTreeWantsPressureHeldOnTheBeat()
        {
            Assert.IsTrue(Earns("ironclad", Run("Cliffside", ProgressionRules.Fighter, Log(attacks: 20, parries: 1))));
            Assert.IsFalse(Earns("ironclad", Run("Cliffside", ProgressionRules.Fighter, Log(attacks: 20))), "nothing was thrown at you");
            Assert.IsTrue(Earns("on_the_one", Run("Cliffside", ProgressionRules.Fighter, Log(attacks: 40, offBeat: 4))));
            Assert.IsFalse(Earns("on_the_one", Run("Cliffside", ProgressionRules.Fighter, Log(attacks: 40, offBeat: 5))));
            Assert.IsFalse(Earns("on_the_one", Run("Cliffside", ProgressionRules.Fighter, Log(attacks: 30))), "40 notes or more");
            Assert.IsTrue(Earns("no_quarter", Run("Cliffside", ProgressionRules.Fighter, Log(attacks: 20, kills: 3, hits: 2, hearts: 2))));
            Assert.IsFalse(Earns("no_quarter", Run("Cliffside", ProgressionRules.Fighter, Log(attacks: 20, kills: 3, hits: 3, hearts: 3))), "half the hearts gone");
            Assert.IsFalse(Earns("no_quarter", Run("Cliffside", ProgressionRules.Fighter, Log(attacks: 20, kills: 3), StageRank.C)), "a C is a fail");
        }

        [Test]
        public void FeatsAreEarnedOnceAndKept()
        {
            var save = SaveData.NewGame(ProgressionRules.Minstrel, null);
            var run = Run("Cliffside", ProgressionRules.Minstrel, Log(attacks: 30, perfect: 20, sequences: 8));
            var won = Feats.NewlyEarned(run, save);
            CollectionAssert.AreEquivalent(new[] { "carry_the_tune", "perfect_cadence" }, won.Select(f => f.Key).ToList());
            foreach (var f in won) Assert.IsTrue(save.MarkFeat(f.Key));
            Assert.AreEqual(0, Feats.NewlyEarned(run, save).Count, "a feat is earned once");
            Assert.AreEqual(2, Feats.Done(save, ProgressionRules.Minstrel));
            Assert.IsFalse(save.MarkFeat("carry_the_tune"));
            save.feats = null;
            save.Normalize();
            Assert.IsNotNull(save.feats);
        }
    }
}
