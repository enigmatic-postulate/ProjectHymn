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

        public SequencePattern(string name, NoteButton[] notes, object source = null)
        {
            Name = name; Notes = notes; Source = source;
        }

        public int Length => Notes?.Length ?? 0;
    }

    /// <summary>What one note did to the chain.</summary>
    public readonly struct ChainStep
    {
        /// <summary>Consecutive on-beat notes so far, including this one (0 after an off-beat note).</summary>
        public readonly int ChainLength;
        /// <summary>GDD M2: single notes do nothing; from the second note of a chain on, hits deal damage.</summary>
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
    /// - A chain is a run of on-beat notes on consecutive beats. A gap of a beat or more starts a new chain;
    ///   an off-beat note (or a second press on the same beat) breaks it.
    /// - Damage needs rhythm: the first note of a chain never deals damage, every later note does.
    /// - The chain's most recent notes are matched against the sequences (1-2-1, 2-2, 1-1-1-2-1-1-1...): the longest
    ///   tail of the chain that is the start of a sequence decides which move plays. Finishing a sequence completes it;
    ///   the chain keeps going and the next note starts matching afresh.
    /// - <see cref="Predict"/> tells the animator which move is most likely next, so it can wind up in time.
    /// </summary>
    public sealed class RhythmChain
    {
        readonly List<SequencePattern> _sequences = new List<SequencePattern>();
        readonly List<NoteButton> _tail = new List<NoteButton>(); // notes since the last completed sequence (or chain start)
        long _lastBeat = long.MinValue;

        public IReadOnlyList<SequencePattern> Sequences => _sequences;
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
            CurrentSequence = CurrentStep = -1;
        }

        /// <summary>Register a note judged on <paramref name="beatIndex"/>.</summary>
        public ChainStep Register(long beatIndex, NoteButton button, bool onBeat)
        {
            if (!onBeat || beatIndex == _lastBeat)
            {
                Reset();
                return new ChainStep(0, false, -1, -1, false, broke: true);
            }

            if (_lastBeat != long.MinValue && beatIndex == _lastBeat + 1) ChainLength++;
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
            var result = new ChainStep(ChainLength, ChainLength >= 2, seq, step, completed, broke: false);
            if (completed)
            {
                SequenceCompleted?.Invoke(_sequences[seq]);
                _tail.Clear();
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
                if (s.Length <= _tail.Count || !StartsWith(s, _tail)) continue;
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
            return _tail.Count <= s.Length && StartsWith(s, _tail) ? _tail.Count : 0;
        }

        /// <summary>A sequence starting with exactly these notes: prefer one that this note completes, then the shortest.</summary>
        int BestMatch(List<NoteButton> notes)
        {
            int best = -1;
            for (int i = 0; i < _sequences.Count; i++)
            {
                var s = _sequences[i];
                if (s.Length < notes.Count || !StartsWith(s, notes)) continue;
                if (best < 0) { best = i; continue; }
                bool completes = s.Length == notes.Count, bestCompletes = _sequences[best].Length == notes.Count;
                if (completes && !bestCompletes) best = i;
                else if (completes == bestCompletes && s.Length < _sequences[best].Length) best = i;
            }
            return best;
        }

        static bool StartsWith(SequencePattern s, List<NoteButton> notes)
        {
            for (int i = 0; i < notes.Count; i++) if (s.Notes[i] != notes[i]) return false;
            return true;
        }
    }
}
