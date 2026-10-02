using System;
using System.Collections.Generic;

namespace ProjectHymn.Core
{
    /// <summary>Everything gameplay needs to know about one beat.</summary>
    public readonly struct BeatInfo
    {
        public readonly long Index;          // absolute beat index since song start (keeps counting across loops)
        public readonly long Measure;        // absolute measure index
        public readonly int BeatInMeasure;   // 0-based; 0 is the downbeat
        public readonly int BeatsPerMeasure;
        public readonly int BeatUnit;
        public readonly double Bpm;
        public readonly double Time;         // song time (seconds from beat 0) at which this beat lands
        public readonly int SectionIndex;

        public BeatInfo(long index, long measure, int beatInMeasure, int beatsPerMeasure, int beatUnit,
                        double bpm, double time, int sectionIndex)
        {
            Index = index; Measure = measure; BeatInMeasure = beatInMeasure; BeatsPerMeasure = beatsPerMeasure;
            BeatUnit = beatUnit; Bpm = bpm; Time = time; SectionIndex = sectionIndex;
        }

        public bool IsDownbeat => BeatInMeasure == 0;
        public bool IsLastBeatOfMeasure => BeatInMeasure == BeatsPerMeasure - 1;
        public string Signature => $"{BeatsPerMeasure}/{BeatUnit}";
        public double SecondsPerBeat => 60.0 / Bpm;
    }

    /// <summary>
    /// Converts between song time (seconds since beat 0) and beats, across tempo and meter changes.
    /// Pure C#: no Unity calls, so it is unit-testable and deterministic.
    /// </summary>
    public sealed class TempoMap
    {
        struct Segment
        {
            public double StartBeat, StartTime, SecPerBeat, Bpm;
            public long StartMeasure;
            public int Measures, BeatsPerMeasure, BeatUnit;
            public double Beats => (double)Measures * BeatsPerMeasure;
            public double EndTime => StartTime + Beats * SecPerBeat;
            public double EndBeat => StartBeat + Beats;
        }

        readonly Segment[] _segs;

        public bool Loops { get; }
        public double LengthSeconds { get; }
        public long LengthBeats { get; }
        public long LengthMeasures { get; }
        public int SectionCount => _segs.Length;

        public TempoMap(IReadOnlyList<TempoSection> sections, bool loop)
        {
            if (sections == null || sections.Count == 0)
                sections = new[] { new TempoSection(4, 120f, 4) };

            _segs = new Segment[sections.Count];
            double beat = 0, time = 0;
            long measure = 0;
            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                var seg = new Segment
                {
                    StartBeat = beat,
                    StartTime = time,
                    Bpm = Math.Max(1.0, s.bpm),
                    SecPerBeat = 60.0 / Math.Max(1.0, s.bpm),
                    StartMeasure = measure,
                    Measures = Math.Max(1, s.measures),
                    BeatsPerMeasure = Math.Max(1, s.beatsPerMeasure),
                    BeatUnit = s.beatUnit <= 0 ? 4 : s.beatUnit,
                };
                _segs[i] = seg;
                beat = seg.EndBeat;
                time = seg.EndTime;
                measure += seg.Measures;
            }

            Loops = loop;
            LengthSeconds = time;
            LengthBeats = (long)Math.Round(beat);
            LengthMeasures = measure;
        }

        /// <summary>Continuous beat position for a song time. Negative times (count-in) extrapolate the first section.</summary>
        public double TimeToBeat(double time)
        {
            long loops = 0;
            if (time >= LengthSeconds && Loops)
            {
                loops = (long)Math.Floor(time / LengthSeconds);
                time -= loops * LengthSeconds;
            }

            int i = FindSegmentByTime(time);
            var s = _segs[i];
            return loops * (double)LengthBeats + s.StartBeat + (time - s.StartTime) / s.SecPerBeat;
        }

        /// <summary>Song time at which a (possibly fractional) beat position lands.</summary>
        public double BeatToTime(double beat)
        {
            long loops = 0;
            if (beat >= LengthBeats && Loops)
            {
                loops = (long)Math.Floor(beat / LengthBeats);
                beat -= loops * (double)LengthBeats;
            }

            int i = FindSegmentByBeat(beat);
            var s = _segs[i];
            return loops * LengthSeconds + s.StartTime + (beat - s.StartBeat) * s.SecPerBeat;
        }

        public BeatInfo GetBeat(long index)
        {
            long loops = 0;
            long local = index;
            if (Loops && index >= LengthBeats)
            {
                loops = index / LengthBeats;
                local = index - loops * LengthBeats;
            }

            int i = FindSegmentByBeat(local);
            var s = _segs[i];
            long beatInSeg = local - (long)Math.Round(s.StartBeat);
            long measureInSeg, beatInMeasure;
            if (beatInSeg >= 0)
            {
                measureInSeg = beatInSeg / s.BeatsPerMeasure;
                beatInMeasure = beatInSeg % s.BeatsPerMeasure;
            }
            else
            {
                // count-in before beat 0: floor division
                measureInSeg = (beatInSeg - (s.BeatsPerMeasure - 1)) / s.BeatsPerMeasure;
                beatInMeasure = beatInSeg - measureInSeg * s.BeatsPerMeasure;
            }

            double time = loops * LengthSeconds + s.StartTime + beatInSeg * s.SecPerBeat;
            long measure = loops * LengthMeasures + s.StartMeasure + measureInSeg;
            return new BeatInfo(index, measure, (int)beatInMeasure, s.BeatsPerMeasure, s.BeatUnit, s.Bpm, time, i);
        }

        /// <summary>Absolute beat index of a measure's downbeat (counts across loops; negative measures extrapolate).</summary>
        public long MeasureStartBeat(long measure)
        {
            long loops = 0, local = measure;
            if (Loops && measure >= LengthMeasures)
            {
                loops = measure / LengthMeasures;
                local = measure - loops * LengthMeasures;
            }

            int i = 0;
            while (i < _segs.Length - 1 && local >= _segs[i].StartMeasure + _segs[i].Measures) i++;
            var s = _segs[i];
            return loops * LengthBeats + (long)Math.Round(s.StartBeat) + (local - s.StartMeasure) * s.BeatsPerMeasure;
        }

        int FindSegmentByTime(double time)
        {
            for (int i = 0; i < _segs.Length - 1; i++)
                if (time < _segs[i].EndTime) return i;
            return _segs.Length - 1; // past the end of a non-looping map: last tempo continues
        }

        int FindSegmentByBeat(double beat)
        {
            for (int i = 0; i < _segs.Length - 1; i++)
                if (beat < _segs[i].EndBeat - 1e-9) return i;
            return _segs.Length - 1;
        }
    }
}
