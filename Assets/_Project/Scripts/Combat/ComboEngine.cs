using System;
using System.Collections.Generic;
using ProjectHymn.Core;

namespace ProjectHymn.Combat
{
    /// <summary>Plain data describing a phrase, so the engine can run without Unity (tests, tools).</summary>
    public readonly struct PhrasePattern
    {
        public readonly string Name;
        public readonly BeatAction[] Pattern;
        public readonly int RequiredBeatsPerMeasure;
        public readonly object Source; // the PhraseData asset, when there is one

        public PhrasePattern(string name, BeatAction[] pattern, int requiredBeatsPerMeasure = 0, object source = null)
        {
            Name = name; Pattern = pattern; RequiredBeatsPerMeasure = requiredBeatsPerMeasure; Source = source;
        }

        public static PhrasePattern From(PhraseData d) =>
            new PhrasePattern(d.displayName, d.pattern.ToArray(), d.requiredBeatsPerMeasure, d);
    }

    public enum RegisterOutcome { Accepted, DoubleInput, Missed }

    /// <summary>
    /// Records one action per beat and matches the longest equipped phrase that ends on the latest beat.
    /// Beats with no input count as Rests. A miss (or a second input on the same beat) breaks the chain.
    /// Also tracks full measures for the Bar Finisher. Pure C#.
    /// </summary>
    public sealed class ComboEngine
    {
        enum Slot : byte { Rest, Strike, Smash, Step, Guard, Broken }

        readonly List<PhrasePattern> _phrases = new List<PhrasePattern>();
        readonly Dictionary<long, Slot> _history = new Dictionary<long, Slot>();
        long _lastBeat = long.MinValue;
        long _consumedThrough = long.MinValue; // beats at or before this can't start a new phrase
        int _maxLen = 1;

        long _measure = long.MinValue;
        int _filledInMeasure;
        bool _measureBroken;

        public event Action<PhrasePattern, BeatInfo> PhraseCompleted;
        /// <summary>Every beat of a measure was played without a miss. Fire the finisher on the next downbeat.</summary>
        public event Action<BeatInfo> BarCompleted;

        public IReadOnlyList<PhrasePattern> Phrases => _phrases;

        public void SetPhrases(IEnumerable<PhrasePattern> phrases)
        {
            _phrases.Clear();
            _maxLen = 1;
            foreach (var p in phrases)
            {
                if (p.Pattern == null || p.Pattern.Length == 0 || p.Pattern[p.Pattern.Length - 1] == BeatAction.Rest) continue;
                _phrases.Add(p);
                _maxLen = Math.Max(_maxLen, p.Pattern.Length);
            }
            // longest first so the first match is the best match; stable for equal lengths
            var ordered = new List<PhrasePattern>(_phrases);
            ordered.Sort((a, b) => b.Pattern.Length.CompareTo(a.Pattern.Length));
            _phrases.Clear();
            _phrases.AddRange(ordered);
        }

        public void Reset()
        {
            _history.Clear();
            _lastBeat = long.MinValue;
            _consumedThrough = long.MinValue;
            _measure = long.MinValue;
            _filledInMeasure = 0;
            _measureBroken = false;
        }

        /// <summary>Has an input already been registered on this beat?</summary>
        public bool HasInputOn(long beatIndex) =>
            _history.TryGetValue(beatIndex, out var s) && s != Slot.Rest;

        /// <summary>Register an on-beat action. Returns the completed phrase, if any.</summary>
        public RegisterOutcome Register(BeatInfo beat, BeatAction action, out PhrasePattern? completed)
        {
            completed = null;
            if (action == BeatAction.Rest) throw new ArgumentException("Rests are implied by beats with no input.");

            if (HasInputOn(beat.Index))
            {
                Break(beat);
                return RegisterOutcome.DoubleInput;
            }

            Write(beat.Index, (Slot)action);
            TrackMeasure(beat, broken: false);

            foreach (var p in _phrases)
            {
                if (p.RequiredBeatsPerMeasure != 0 && p.RequiredBeatsPerMeasure != beat.BeatsPerMeasure) continue;
                if (!Matches(p.Pattern, beat.Index)) continue;
                completed = p;
                _consumedThrough = beat.Index;
                PhraseCompleted?.Invoke(p, beat);
                break;
            }
            return RegisterOutcome.Accepted;
        }

        /// <summary>Register a mistimed input. Breaks any phrase and the current bar.</summary>
        public void RegisterMiss(BeatInfo beat) => Break(beat);

        void Break(BeatInfo beat)
        {
            Write(beat.Index, Slot.Broken);
            TrackMeasure(beat, broken: true);
        }

        void Write(long index, Slot slot)
        {
            // fill skipped beats with rests
            if (_lastBeat != long.MinValue)
                for (long b = Math.Max(_lastBeat + 1, index - _maxLen); b < index; b++)
                    if (!_history.ContainsKey(b)) _history[b] = Slot.Rest;

            _history[index] = slot;
            if (index > _lastBeat) _lastBeat = index;

            // prune
            if (_history.Count > _maxLen * 4)
            {
                var stale = new List<long>();
                foreach (var k in _history.Keys) if (k < _lastBeat - _maxLen * 2) stale.Add(k);
                foreach (var k in stale) _history.Remove(k);
            }
        }

        bool Matches(BeatAction[] pattern, long endBeat)
        {
            long start = endBeat - pattern.Length + 1;
            if (start <= _consumedThrough) return false;
            for (int i = 0; i < pattern.Length; i++)
            {
                long b = start + i;
                var slot = _history.TryGetValue(b, out var s) ? s : Slot.Rest; // unknown history counts as rest
                if (slot == Slot.Broken) return false;
                if ((int)slot != (int)pattern[i]) return false;
            }
            return true;
        }

        void TrackMeasure(BeatInfo beat, bool broken)
        {
            if (beat.Measure != _measure)
            {
                _measure = beat.Measure;
                _filledInMeasure = 0;
                // a bar only counts if it is played from its downbeat
                _measureBroken = beat.BeatInMeasure != 0;
            }

            if (broken) { _measureBroken = true; return; }
            _filledInMeasure++;

            if (!_measureBroken && beat.IsLastBeatOfMeasure && _filledInMeasure == beat.BeatsPerMeasure)
            {
                _measureBroken = true; // fire once
                BarCompleted?.Invoke(beat);
            }
        }
    }
}
