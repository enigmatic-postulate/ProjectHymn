using System;
using ProjectHymn.Core;

namespace ProjectHymn.Combat
{
    /// <summary>
    /// Score is where accuracy shows most (GDD). Points per judgement, scaled by Groove, plus bonuses
    /// for phrases and bar finishers. Pure C#.
    /// </summary>
    public sealed class ScoreKeeper
    {
        public int PerfectPoints = 300, GreatPoints = 200, GoodPoints = 100, OffBeatPoints = 10;
        public int PhrasePointsPerBeat = 150;
        public int FinisherPointsPerBeat = 250;

        public long Total { get; private set; }

        public event Action<long, int> Scored; // new total, points just added

        public int PointsFor(Judgement j) => j switch
        {
            Judgement.TruePerfect => PerfectPoints * 2,
            Judgement.Perfect => PerfectPoints,
            Judgement.Great => GreatPoints,
            Judgement.Good => GoodPoints,
            _ => OffBeatPoints,
        };

        public int AddJudgement(Judgement j, float grooveMultiplier) => Add(PointsFor(j) * grooveMultiplier);

        public int AddPhrase(int phraseLength, float grooveMultiplier) => Add(PhrasePointsPerBeat * phraseLength * grooveMultiplier);

        public int AddFinisher(int beatsPerMeasure, float grooveMultiplier) => Add(FinisherPointsPerBeat * beatsPerMeasure * grooveMultiplier);

        public void Reset() => Total = 0;

        int Add(double points)
        {
            int p = (int)Math.Round(points);
            Total += p;
            Scored?.Invoke(Total, p);
            return p;
        }
    }
}
