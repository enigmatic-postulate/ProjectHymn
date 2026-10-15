using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Progression;

namespace ProjectHymn.Tests
{
    /// <summary>Playtest logs and their summary (VS-5 Part C).</summary>
    public class PlaytestTests
    {
        static RunLog Clear(string stage, float seconds, StageRank rank, float par = 75f, int feel = 0)
        {
            var r = new RunLog { stage = stage, started = "2026-10-15 19:00:00", feel = feel };
            var caps = new List<string>();
            if (rank != StageRank.S && seconds <= par) caps.Add("a hit taken: S needs a clean run here");
            var time = StageScoring.TimeRank(seconds, par);
            r.Grade(new StageResult(seconds, time, rank, caps, 0.8), par);
            r.attacks = 20; r.offBeat = 4; r.perfect = 6; r.truePerfect = 1;
            return r;
        }

        [Test]
        public void GradingRecordsWhyARunMissedS()
        {
            var r = new RunLog();
            r.Grade(new StageResult(90.0, StageRank.B, StageRank.B, new List<string> { "enemies 5/7: S and A need every enemy" }, 0.7), 75f);
            Assert.AreEqual("clear", r.outcome);
            Assert.AreEqual("B", r.rank);
            Assert.AreEqual(2, r.missedS.Count);
            StringAssert.Contains("against par 75s (+20%)", r.missedS[0]);
            Assert.AreEqual("S and A need every enemy", PlaytestSummary.Reason(r.missedS[1]));
            Assert.AreEqual("over par", PlaytestSummary.Reason(r.missedS[0]));

            var s = new RunLog();
            s.Grade(new StageResult(60.0, StageRank.S, StageRank.S, new List<string>(), 1.0), 75f);
            Assert.AreEqual(0, s.missedS.Count);

            var c = new RunLog();
            c.Grade(new StageResult(200.0, StageRank.C, StageRank.C, new List<string>(), 0.4), 75f);
            Assert.AreEqual("failed", c.outcome);
        }

        [Test]
        public void TheTallyIsCopied()
        {
            var log = new PlaystyleLog { MaxHearts = 5 };
            log.AddNote(Judgement.Perfect); log.AddNote(Judgement.OffBeat); log.AddNote(Judgement.Good);
            log.AddDefense(DefenseResult.Parried); log.AddHurt(2); log.AddKill(); log.AddSequence();
            var r = new RunLog();
            r.Fill(log);
            Assert.AreEqual(3, r.attacks);
            Assert.AreEqual(1, r.offBeat);
            Assert.AreEqual(1, r.perfect);
            Assert.AreEqual(1, r.parries);
            Assert.AreEqual(2, r.heartsLost);
            Assert.AreEqual(1, r.enemiesDefeated);
            Assert.AreEqual(1, r.sequences);
            Assert.AreEqual(5, r.maxHearts);
            Assert.AreEqual(2f / 3f, r.OnBeat, 1e-5);
            Assert.AreEqual(-1f, new RunLog().OnBeat);
        }

        [Test]
        public void FileNamesSortByTime()
        {
            Assert.AreEqual("20261015-185400_Cliffside.json", RunLog.FileName(new DateTime(2026, 10, 15, 18, 54, 0), "Cliffside"));
            Assert.AreEqual("20261015-185400_Stage.json", RunLog.FileName(new DateTime(2026, 10, 15, 18, 54, 0), ""));
        }

        [Test]
        public void SuggestedParIsTheFastestThird()
        {
            Assert.AreEqual(70.0, PlaytestSummary.SuggestedPar(new List<double> { 59.2, 70, 80, 90, 100, 110 }), "two of six make it");
            Assert.AreEqual(71.0, PlaytestSummary.SuggestedPar(new List<double> { 70.1, 90 }));
            Assert.AreEqual(0.0, PlaytestSummary.SuggestedPar(new List<double>()));
            Assert.AreEqual(2.5, PlaytestSummary.Median(new List<double> { 1, 2, 3, 4 }));
            Assert.AreEqual("1:05.5", PlaytestSummary.Clock(65.5));
        }

        [Test]
        public void StretchesSplitTheStage()
        {
            Assert.AreEqual(0, PlaytestSummary.Stretch(0f, 0f, 60f));
            Assert.AreEqual(5, PlaytestSummary.Stretch(60f, 0f, 60f));
            Assert.AreEqual(2, PlaytestSummary.Stretch(25f, 0f, 60f));
            Assert.AreEqual(0, PlaytestSummary.Stretch(-5f, 0f, 60f));
            Assert.AreEqual(0, PlaytestSummary.Stretch(5f, 3f, 3f));
        }

        [Test]
        public void TheReportCoversEachStageInRouteOrder()
        {
            var runs = new List<RunLog>
            {
                Clear("Belfry", 80f, StageRank.A, 85f, feel: 4),
                Clear("Cliffside", 70f, StageRank.A, 75f, feel: 3),
                Clear("Cliffside", 90f, StageRank.B, 75f),
                new RunLog { stage = "Cliffside", outcome = "died", started = "2026-10-15 18:50:00" },
            };
            runs[3].Sample(0f, 0f, 0f); runs[3].Sample(0.5f, 60f, 0f);
            runs[3].Event(10f, "died", 40f, 2f, "Shield Knight");
            runs.Add(new RunLog { stage = "Cliffside", outcome = "clear", seconds = 5f, rank = "S", debug = true });
            string md = PlaytestSummary.Report(runs);
            StringAssert.Contains("(1 more left out: finished or ranked with a playtest key)", md);
            StringAssert.Contains("4 runs, 2026-10-15 18:50:00 to 2026-10-15 19:00:00", md);
            Assert.IsTrue(md.IndexOf("## Cliffside", StringComparison.Ordinal) < md.IndexOf("## Belfry Ridge", StringComparison.Ordinal));
            StringAssert.Contains("**Runs:** 3 (2 clear, 1 died)", md);
            StringAssert.Contains("fastest 1:10.0, median 1:20.0, slowest 1:30.0; par 1:15.0 (made by 1 of 2)", md);
            StringAssert.Contains("over par ×1", md);
            StringAssert.Contains("| 4: 30 to 40 |", md);
            StringAssert.Contains("**Felt:** 3.0 / 5 from 1 answer", md);
            StringAssert.Contains("No runs yet", PlaytestSummary.Report(new List<RunLog>()));
        }
    }
}
