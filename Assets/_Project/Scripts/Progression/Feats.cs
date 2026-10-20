using System;
using System.Collections.Generic;

namespace ProjectHymn.Progression
{
    /// <summary>A finished run, as a feat judges it: who played, how it ended, and how they fought.</summary>
    public readonly struct FeatRun
    {
        public readonly string Stage, ClassId;
        public readonly bool Cleared;
        /// <summary>Finished with a playtest key (F12 / Shift+F12): earns nothing.</summary>
        public readonly bool Debug;
        public readonly StageRank Rank;
        public readonly double Seconds, ParSeconds;
        public readonly PlaystyleLog Log;

        public FeatRun(string stage, string classId, bool cleared, StageRank rank, double seconds, double parSeconds,
                       PlaystyleLog log, bool debug = false)
        {
            Stage = stage ?? ""; ClassId = classId ?? ""; Cleared = cleared; Rank = rank;
            Seconds = seconds; ParSeconds = parSeconds; Log = log; Debug = debug;
        }
    }

    /// <summary>Where a feat can be earned: anywhere, in the Drowned Nave, or against a boss.</summary>
    public enum FeatWhere { Anywhere, Deep, Boss }

    /// <summary>
    /// One unlock in a class's research tree (VS-7): a thing you have to do in a run, and the ability it earns. The GDD
    /// asks for unlocks that take a feat ("beating the act 2 boss using only parries and shield blocks"), so every feat
    /// is about how you fought, not how fast: they are the reason to go back to a stage you have already ranked.
    /// </summary>
    public sealed class Feat
    {
        public readonly string Key, Name, Tree, How, Ability;
        public readonly FeatWhere Where;
        readonly Func<FeatRun, bool> _test;

        public Feat(string key, string name, string tree, FeatWhere where, string how, string ability, Func<FeatRun, bool> test)
        {
            Key = key; Name = name; Tree = tree; Where = where; How = how; Ability = ability; _test = test;
        }

        /// <summary>The stage suits this feat (anywhere, a drowned stage, or a boss stage).</summary>
        public bool Fits(string stage)
        {
            switch (Where)
            {
                case FeatWhere.Deep: return Feats.IsDeep(stage);
                case FeatWhere.Boss: return Feats.IsBoss(stage);
                default: return true;
            }
        }

        /// <summary>
        /// This run earned the feat: cleared (not with a playtest key), playing the class whose tree it belongs to, on a
        /// stage the feat fits, and the feat's own test passes.
        /// </summary>
        public bool EarnedBy(FeatRun r) =>
            r.Cleared && !r.Debug && r.Log != null && r.ClassId == Tree && Fits(r.Stage) && _test(r);
    }

    /// <summary>
    /// The research trees (VS-7): three feats per class, each earning an ability. They are deliberately about the things
    /// the first playtests showed nobody doing — sequences in compound meter, dodging at all — so the trees pull play
    /// toward the rhythm instead of toward the clock, which is all the rank rewards today.
    /// </summary>
    public static class Feats
    {
        /// <summary>The Drowned Nave's stages (region 1 of the road).</summary>
        public static bool IsDeep(string stage)
        {
            var stop = StageRoute.Find(stage);
            return stop != null && stop.Region == 1;
        }

        /// <summary>A stage a boss holds.</summary>
        public static bool IsBoss(string stage) => stage == "Summit" || stage == "Apse";

        public static readonly IReadOnlyList<Feat> All = new[]
        {
            // Minstrel: the singer. Chains and sequences.
            new Feat("carry_the_tune", "Carry the Tune", ProgressionRules.Minstrel, FeatWhere.Anywhere,
                     "Clear a stage completing 8 sequences.", "Sustain",
                     r => r.Log.SequencesCompleted >= 8),
            new Feat("deep_measure", "Deep Measure", ProgressionRules.Minstrel, FeatWhere.Deep,
                     "Clear a stage in the Drowned Nave completing 4 sequences.", "Swell of the Tide",
                     r => r.Log.SequencesCompleted >= 4),
            new Feat("perfect_cadence", "Perfect Cadence", ProgressionRules.Minstrel, FeatWhere.Anywhere,
                     "Clear a stage with half your notes Perfect or better.", "True Voice",
                     r => r.Log.Attacks >= 20 && Perfects(r.Log) * 2 >= r.Log.Attacks),

            // Fighter: relentless. Pressure and precision.
            new Feat("ironclad", "Ironclad", ProgressionRules.Fighter, FeatWhere.Anywhere,
                     "Clear a stage without taking a hit.", "Ironclad",
                     r => r.Log.HitsTaken == 0 && r.Log.EnemyAttacks > 0),
            new Feat("on_the_one", "On the One", ProgressionRules.Fighter, FeatWhere.Anywhere,
                     "Clear a stage with nine notes in ten on the beat (40 notes or more).", "Downbeat",
                     r => r.Log.Attacks >= 40 && r.Log.OnBeatShare >= 0.9),
            new Feat("no_quarter", "No Quarter", ProgressionRules.Fighter, FeatWhere.Anywhere,
                     "Clear a stage defeating every enemy without losing half your hearts.", "Second Wind",
                     r => r.Log.EnemiesDefeated > 0 && !r.Log.HalfHeartsLost && r.Rank <= StageRank.B),

            // Warden: the guard. The GDD's own example: beat a boss on parries and blocks alone.
            new Feat("shield_wall", "Shield Wall", ProgressionRules.Warden, FeatWhere.Anywhere,
                     "Clear a stage answering every enemy attack with a parry or a block (6 attacks or more).", "Shield Wall",
                     r => r.Log.EnemyAttacks >= 6 && r.Log.HitsTaken == 0 && r.Log.Dodges == 0),
            new Feat("counterweight", "Counterweight", ProgressionRules.Warden, FeatWhere.Anywhere,
                     "Parry 10 attacks in one run.", "Counterweight",
                     r => r.Log.Parries >= 10),
            new Feat("the_cantors_count", "The Cantor's Count", ProgressionRules.Warden, FeatWhere.Boss,
                     "Beat a boss with 8 parries, and more parries than hits taken.", "Conductor's Guard",
                     r => r.Log.Parries >= 8 && r.Log.Parries > r.Log.HitsTaken),

            // Crusader: the dodge. Nobody dodged in the first playtests; these are the reason to.
            new Feat("riptide_runner", "Riptide Runner", ProgressionRules.Crusader, FeatWhere.Anywhere,
                     "Dodge 5 attacks in one run.", "Riptide Step",
                     r => r.Log.Dodges >= 5),
            new Feat("eye_of_the_storm", "Eye of the Storm", ProgressionRules.Crusader, FeatWhere.Boss,
                     "Beat a boss without taking a hit.", "Eye of the Storm",
                     r => r.Log.HitsTaken == 0 && r.Log.EnemyAttacks > 0),
            new Feat("undertow", "Undertow", ProgressionRules.Crusader, FeatWhere.Deep,
                     "Clear a stage in the Drowned Nave dodging 3 attacks.", "Undertow",
                     r => r.Log.Dodges >= 3),
        };

        static int Perfects(PlaystyleLog log) => log.Hits(Core.Judgement.Perfect) + log.Hits(Core.Judgement.TruePerfect);

        public static Feat Find(string key)
        {
            foreach (var f in All) if (f.Key == key) return f;
            return null;
        }

        /// <summary>A class's tree, in order.</summary>
        public static List<Feat> Tree(string classId)
        {
            var list = new List<Feat>();
            foreach (var f in All) if (f.Tree == classId) list.Add(f);
            return list;
        }

        /// <summary>The feats this run earns that the save doesn't already hold (in tree order).</summary>
        public static List<Feat> NewlyEarned(FeatRun run, SaveData save)
        {
            var won = new List<Feat>();
            foreach (var f in All)
                if (f.EarnedBy(run) && (save == null || !save.HasFeat(f.Key))) won.Add(f);
            return won;
        }

        /// <summary>How many of a class's feats are done (for the Songbook).</summary>
        public static int Done(SaveData save, string classId)
        {
            int n = 0;
            foreach (var f in All) if (f.Tree == classId && save != null && save.HasFeat(f.Key)) n++;
            return n;
        }
    }
}
