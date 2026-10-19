using System;
using System.Collections.Generic;

namespace ProjectHymn.Combat
{
    /// <summary>The two attack buttons of the M2 sword prototype: 1 = light (J / X), 2 = heavy (K / Y).</summary>
    public enum NoteButton { One = 1, Two = 2 }

    /// <summary>A named button rhythm such as 1-2-1. Each step plays its own move.</summary>
    public readonly struct SequencePattern
    {
        public readonly string Name;
        public readonly NoteButton[] Notes;
        public readonly object Source; // the SwordSequence asset, when there is one
        /// <summary>Home meter ("4/4", "3/4", "7/8"): the sequence only plays in it. Empty = any meter.</summary>
        public readonly string Meter;

        public SequencePattern(string name, NoteButton[] notes, object source = null, string meter = "")
        {
            Name = name; Notes = notes; Source = source; Meter = meter ?? "";
        }

        public bool PlaysIn(string meter) => string.IsNullOrEmpty(Meter) || string.IsNullOrEmpty(meter) || Meter == meter;

        public int Length => Notes?.Length ?? 0;
    }

    /// <summary>What one note did to the chain.</summary>
    public readonly struct ChainStep
    {
        /// <summary>Consecutive on-beat notes so far, including this one (0 after an off-beat note).</summary>
        public readonly int ChainLength;
        /// <summary>GDD update 3: every note deals damage (off-beat notes at 25%). Always true; kept for callers.</summary>
        public readonly bool DealsDamage;
        /// <summary>Index of the sequence this note is following (-1 = none: a plain cut).</summary>
        public readonly int Sequence;
        /// <summary>Which step of that sequence this note is (0-based).</summary>
        public readonly int Step;
        /// <summary>This note finished the sequence.</summary>
        public readonly bool Completed;
        /// <summary>The note was off the beat (or a second press on one beat) and broke the chain.</summary>
        public readonly bool Broke;

        public ChainStep(int chainLength, bool dealsDamage, int sequence, int step, bool completed, bool broke)
        {
            ChainLength = chainLength; DealsDamage = dealsDamage; Sequence = sequence; Step = step;
            Completed = completed; Broke = broke;
        }
    }

    /// <summary>
    /// The M2 rhythm rules, pure C#:
    /// - A chain is a run of on-beat notes a felt pulse apart (<see cref="Meters"/>): consecutive beats in 4/4 and 3/4,
    ///   and either the dotted quarter or straight eighths in 6/8 and 9/8, so the compound meters can be played on their
    ///   big beats. A longer gap starts a new chain; an off-beat note (or a second press on the same beat) breaks it.
    /// - Every note deals damage (GDD update 3); rhythm decides the combo, not whether a note hits.
    /// - The chain's most recent notes are matched against the sequences (1-2-1, 2-2, 1-1-1-2-1-1-1...): the longest
    ///   tail of the chain that is the start of a sequence decides which move plays. Finishing a sequence completes it;
    ///   the chain keeps going and the next note starts matching afresh, unless a longer sequence carries on from the
    ///   one just finished (Brandish 2-1 into Iron Count 2-1-2-1): then the longer one keeps going, so the longest
    ///   match wins without a choice (each finisher pays as it lands).
    /// - Home meters (GDD update 18): a sequence only plays in its meter. A meter change starts the matching afresh.
    /// - <see cref="Predict"/> tells the animator which move is most likely next, so it can wind up in time.
    /// </summary>
    public sealed class RhythmChain
    {
        readonly List<SequencePattern> _sequences = new List<SequencePattern>();
        readonly List<NoteButton> _tail = new List<NoteButton>(); // notes since the last completed sequence (or chain start)
        long _lastBeat = long.MinValue;
        string _meter = "";

        public IReadOnlyList<SequencePattern> Sequences => _sequences;
        /// <summary>The meter of the last note ("" until one is given).</summary>
        public string Meter => _meter;
        public int ChainLength { get; private set; }
        public long LastBeat => _lastBeat;
        /// <summary>Notes the current sequence match is built from.</summary>
        public IReadOnlyList<NoteButton> Tail => _tail;
        /// <summary>The sequence and step matched by the last note (-1 = none).</summary>
        public int CurrentSequence { get; private set; } = -1;
        public int CurrentStep { get; private set; } = -1;

        public event Action<SequencePattern> SequenceCompleted;

        public void SetSequences(IEnumerable<SequencePattern> sequences)
        {
            _sequences.Clear();
            foreach (var s in sequences) if (s.Length > 0) _sequences.Add(s);
            Reset();
        }

        public void Reset()
        {
            _tail.Clear();
            ChainLength = 0;
            _lastBeat = long.MinValue;
            _meter = "";
            CurrentSequence = CurrentStep = -1;
        }

        /// <summary>Register a note judged on <paramref name="beatIndex"/>, in <paramref name="meter"/> ("4/4"; null = any).</summary>
        public ChainStep Register(long beatIndex, NoteButton button, bool onBeat, string meter = null)
        {
            meter = meter ?? "";
            if (meter != _meter) { _tail.Clear(); _meter = meter; }
            if (!onBeat || beatIndex == _lastBeat)
            {
                Reset();
                return new ChainStep(0, true, -1, -1, false, broke: true);
            }

            if (_lastBeat != long.MinValue && Meters.IsPulseGap(_meter, beatIndex - _lastBeat)) ChainLength++;
            else { ChainLength = 1; _tail.Clear(); }
            _lastBeat = beatIndex;
            _tail.Add(button);

            // Longest tail that starts some sequence; drop older notes until one matches.
            int seq = -1;
            while (_tail.Count > 0)
            {
                seq = BestMatch(_tail);
                if (seq >= 0) break;
                _tail.RemoveAt(0);
            }

            int step = seq >= 0 ? _tail.Count - 1 : -1;
            bool completed = seq >= 0 && _tail.Count == _sequences[seq].Length;
            CurrentSequence = seq;
            CurrentStep = step;
            var result = new ChainStep(ChainLength, true, seq, step, completed, broke: false);
            if (completed)
            {
                SequenceCompleted?.Invoke(_sequences[seq]);
                if (!ExtendsFurther(_tail)) _tail.Clear();
            }
            return result;
        }

        /// <summary>
        /// Most likely next note: the sequence being followed (shortest candidate when several share this start)
        /// and its next step. False when no sequence is in progress.
        /// </summary>
        public bool Predict(out int sequence, out int step, out NoteButton button)
        {
            sequence = step = -1;
            button = NoteButton.One;
            if (_tail.Count == 0) return false;
            int best = -1;
            for (int i = 0; i < _sequences.Count; i++)
            {
                var s = _sequences[i];
                if (!s.PlaysIn(_meter) || s.Length <= _tail.Count || !StartsWith(s, _tail)) continue;
                if (best < 0 || s.Length < _sequences[best].Length) best = i;
            }
            if (best < 0) return false;
            sequence = best;
            step = _tail.Count;
            button = _sequences[best].Notes[step];
            return true;
        }

        /// <summary>Progress (notes matched) of every sequence, for the HUD.</summary>
        public int Progress(int sequence)
        {
            var s = _sequences[sequence];
            return s.PlaysIn(_meter) && _tail.Count <= s.Length && StartsWith(s, _tail) ? _tail.Count : 0;
        }

        /// <summary>A sequence starting with exactly these notes: prefer one that this note completes, then the shortest.</summary>
        int BestMatch(List<NoteButton> notes)
        {
            int best = -1;
            for (int i = 0; i < _sequences.Count; i++)
            {
                var s = _sequences[i];
                if (!s.PlaysIn(_meter) || s.Length < notes.Count || !StartsWith(s, notes)) continue;
                if (best < 0) { best = i; continue; }
                bool completes = s.Length == notes.Count, bestCompletes = _sequences[best].Length == notes.Count;
                if (completes && !bestCompletes) best = i;
                else if (completes == bestCompletes && s.Length < _sequences[best].Length) best = i;
            }
            return best;
        }

        /// <summary>A longer sequence in this meter starts with these notes.</summary>
        bool ExtendsFurther(List<NoteButton> notes)
        {
            foreach (var s in _sequences)
                if (s.PlaysIn(_meter) && s.Length > notes.Count && StartsWith(s, notes)) return true;
            return false;
        }

        static bool StartsWith(SequencePattern s, List<NoteButton> notes)
        {
            for (int i = 0; i < notes.Count; i++) if (s.Notes[i] != notes[i]) return false;
            return true;
        }
    }
}
