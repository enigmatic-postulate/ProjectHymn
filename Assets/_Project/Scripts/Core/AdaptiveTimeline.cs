using System;
using System.Collections.Generic;

namespace ProjectHymn.Core
{
    /// <summary>
    /// Adaptive song timeline. The song's sections are grouped into phases; the timeline plays the
    /// current phase's sections as a loop ("block") and keeps appending blocks ahead of playback.
    /// A phase change requested mid-block takes effect at the next block boundary, so every change
    /// lands on a bar line and only ever plays composed music. Pure C#.
    /// </summary>
    public sealed class AdaptiveTimeline
    {
        public readonly struct Block
        {
            public readonly int Phase;
            public readonly double StartTime, EndTime;     // song time (seconds from beat 0)
            public readonly double StartBeat, EndBeat;
            public readonly double SourceStart, SourceEnd; // seconds from beat 0 of the original, linear song

            public Block(int phase, double startTime, double endTime, double startBeat, double endBeat,
                         double sourceStart, double sourceEnd)
            {
                Phase = phase; StartTime = startTime; EndTime = endTime; StartBeat = startBeat; EndBeat = endBeat;
                SourceStart = sourceStart; SourceEnd = sourceEnd;
            }

            public double Duration => EndTime - StartTime;
        }

        readonly List<TempoSection> _source;
        readonly List<SongPhase> _phases;
        readonly double[] _sourceSectionStart; // linear song time where each source section begins
        readonly List<Block> _blocks = new List<Block>();
        readonly List<TempoSection> _played = new List<TempoSection>(); // merged sections actually scheduled

        public TempoMap Map { get; private set; }
        public IReadOnlyList<Block> Blocks => _blocks;
        public int RequestedPhase { get; private set; }
        public int PhaseCount => _phases.Count;
        public double PlannedEnd => _blocks.Count == 0 ? 0 : _blocks[_blocks.Count - 1].EndTime;

        public AdaptiveTimeline(IReadOnlyList<TempoSection> sections, IReadOnlyList<SongPhase> phases, int startPhase = 0)
        {
            if (sections == null || sections.Count == 0) throw new ArgumentException("Song has no sections.");
            if (phases == null || phases.Count == 0) throw new ArgumentException("Song has no phases.");
            _source = new List<TempoSection>(sections);
            _phases = new List<SongPhase>();
            foreach (var p in phases)
            {
                int first = Math.Clamp(p.firstSection, 0, _source.Count - 1);
                int count = Math.Clamp(p.sectionCount, 1, _source.Count - first);
                _phases.Add(new SongPhase(p.name, first, count));
            }

            _sourceSectionStart = new double[_source.Count + 1];
            for (int i = 0; i < _source.Count; i++)
            {
                var s = _source[i];
                _sourceSectionStart[i + 1] = _sourceSectionStart[i] + Math.Max(1, s.measures) * Math.Max(1, s.beatsPerMeasure) * 60.0 / Math.Max(1.0, s.bpm);
            }

            RequestedPhase = Math.Clamp(startPhase, 0, _phases.Count - 1);
            Append(RequestedPhase);
            Rebuild();
        }

        public string PhaseName(int phase) => phase >= 0 && phase < _phases.Count ? _phases[phase].name : null;

        /// <summary>Ask for a phase; it starts at the first block boundary not yet scheduled.</summary>
        public void RequestPhase(int phase) => RequestedPhase = Math.Clamp(phase, 0, _phases.Count - 1);

        /// <summary>Append blocks until the plan reaches <paramref name="horizonTime"/>. Returns true if anything was added.</summary>
        public bool Extend(double horizonTime)
        {
            bool changed = false;
            for (int guard = 0; guard < 1000 && PlannedEnd < horizonTime; guard++)
            {
                Append(RequestedPhase);
                changed = true;
            }
            if (changed) Rebuild();
            return changed;
        }

        public int BlockIndexAt(double time)
        {
            if (time < _blocks[0].StartTime) return 0;
            for (int i = 0; i < _blocks.Count; i++)
                if (time < _blocks[i].EndTime) return i;
            return _blocks.Count - 1;
        }

        public int PhaseAt(double time) => _blocks[BlockIndexAt(time)].Phase;

        /// <summary>The first scheduled block after <paramref name="time"/> whose phase differs from the one playing, if any.</summary>
        public Block? NextPhaseChangeAfter(double time)
        {
            int i = BlockIndexAt(time);
            int current = _blocks[i].Phase;
            for (int k = i + 1; k < _blocks.Count; k++)
                if (_blocks[k].Phase != current) return _blocks[k];
            return null;
        }

        void Append(int phase)
        {
            var p = _phases[phase];
            double startTime = PlannedEnd;
            double startBeat = _blocks.Count == 0 ? 0 : _blocks[_blocks.Count - 1].EndBeat;
            double beats = 0, seconds = 0;
            for (int i = p.firstSection; i < p.firstSection + p.sectionCount; i++)
            {
                var s = _source[i];
                AddPlayed(s);
                double b = Math.Max(1, s.measures) * Math.Max(1, s.beatsPerMeasure);
                beats += b;
                seconds += b * 60.0 / Math.Max(1.0, s.bpm);
            }
            _blocks.Add(new Block(phase, startTime, startTime + seconds, startBeat, startBeat + beats,
                                  _sourceSectionStart[p.firstSection], _sourceSectionStart[p.firstSection + p.sectionCount]));
        }

        void AddPlayed(TempoSection s)
        {
            if (_played.Count > 0)
            {
                var last = _played[_played.Count - 1];
                if (Math.Abs(last.bpm - s.bpm) < 1e-4 && last.beatsPerMeasure == s.beatsPerMeasure && last.beatUnit == s.beatUnit)
                {
                    last.measures += Math.Max(1, s.measures);
                    _played[_played.Count - 1] = last; // same tempo and meter: one segment, so SectionChanged only fires on real changes
                    return;
                }
            }
            _played.Add(s);
        }

        void Rebuild() => Map = new TempoMap(_played, false);
    }
}
