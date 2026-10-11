using System;
using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;

namespace ProjectHymn.Progression
{
    public enum LessonKind { Calibrate, Beat, TwoNotes, Sequences, Guard, Parry, Dodge, Launch, Bout }

    /// <summary>One station of the Practice Yard: what the mentor says, what to do, and what passes it.</summary>
    public sealed class Lesson
    {
        public LessonKind Kind;
        public string Title;
        /// <summary>The mentor's lines before the lesson starts (the game pauses while they're shown).</summary>
        public string[] Intro = Array.Empty<string>();
        /// <summary>The task, shown while the lesson runs.</summary>
        public string Task = "";
        /// <summary>Notes to prompt on the metronome strip from each bar's downbeat ("1-2-1-." ; "." = rest; empty = none).</summary>
        public string Prompt = "";
        /// <summary>How many of the counted thing pass the lesson.</summary>
        public int Need = 1;
        /// <summary>A second count (the heavy notes of Two Notes, Powerslice in Sequences).</summary>
        public int Need2;
        /// <summary>Whether the metronome strip shows note prompts (they fade out once the basics are in).</summary>
        public bool ShowPrompts => !string.IsNullOrEmpty(Prompt);
    }

    /// <summary>
    /// The Practice Yard's lessons and their pass checks (Slice 10). Pure C#: the director feeds it what the player did
    /// (notes, sequences, guards, dodges, launches, the final bout) and it moves to the next lesson when one is passed.
    /// Counts are placeholders for playtest.
    /// </summary>
    public sealed class TutorialPlan
    {
        public readonly List<Lesson> Lessons;
        public int Index { get; private set; }
        public Lesson Current => Index < Lessons.Count ? Lessons[Index] : null;
        public bool Done => Index >= Lessons.Count;
        /// <summary>The lesson changed (passed, or skipped to).</summary>
        public event Action<Lesson> Started;

        /// <summary>The counts toward the current lesson.</summary>
        public int Count { get; private set; }
        public int Count2 { get; private set; }

        public TutorialPlan(List<Lesson> lessons = null) { Lessons = lessons ?? Standard(); }

        public static List<Lesson> Standard() => new List<Lesson>
        {
            new Lesson
            {
                Kind = LessonKind.Calibrate, Title = "Tune your ears",
                Intro = new[]
                {
                    "Welcome to the yard, Cantor. Out there, every blow lands on the beat. In here, you learn to hear it.",
                    "First, let's tune you to this machine. Tap on every click; I'll measure how late your ears and screen run.",
                },
                Task = "Calibrate: tap J on each of 16 clicks (C to skip)", Need = 1,
            },
            new Lesson
            {
                Kind = LessonKind.Beat, Title = "The beat",
                Intro = new[]
                {
                    "See the strip along the bottom? Each mark is a beat. They slide toward the line; press as one crosses it.",
                    "The closer you land, the harder you hit: Good, Great, Perfect, and the true Perfect, dead on the beat. Off the beat still hits, for a quarter.",
                },
                Task = "Press J on the beat, 8 times in a row (Good or better)", Prompt = "1-1-1-1", Need = 8,
            },
            new Lesson
            {
                Kind = LessonKind.TwoNotes, Title = "Two notes",
                Intro = new[]
                {
                    "Two notes: 1 is a light cut (J), 2 is a heavy blow (K). Strike the dummy. Watch the numbers when you're off the beat.",
                },
                Task = "Hit on the beat: 4 light (J) and 4 heavy (K)", Prompt = "1-.-2-.", Need = 4, Need2 = 4,
            },
            new Lesson
            {
                Kind = LessonKind.Sequences, Title = "Chains and sequences",
                Intro = new[]
                {
                    "Notes on beats in a row make a chain. Play a known pattern and it becomes a sequence; its last blow is a finisher that hits far harder.",
                    "Crossing Cuts is 1-2-1. Powerslice is 2-2. Follow the strip.",
                },
                Task = "Finish Crossing Cuts (1-2-1) twice and Powerslice (2-2) once", Prompt = "1-2-1-.-2-2-.-.", Need = 2, Need2 = 1,
            },
            new Lesson
            {
                Kind = LessonKind.Guard, Title = "The red glow",
                Intro = new[]
                {
                    "Now walk over to that knight. His blade is blunted; it won't cost you hearts. Watch him: a beat before each blow, he glows red.",
                    "Red means it lands on the next beat. Press L on that beat to guard.",
                },
                Task = "Guard 3 blows (L on the beat they land)", Need = 3,
            },
            new Lesson
            {
                Kind = LessonKind.Parry, Title = "Parry",
                Intro = new[]
                {
                    "A guard right on the beat is a parry. It throws him off balance, and for two beats he takes half again the damage. Make him pay.",
                },
                Task = "Parry 2 blows (L dead on the beat), then strike", Need = 2,
            },
            new Lesson
            {
                Kind = LessonKind.Dodge, Title = "Dodge",
                Intro = new[]
                {
                    "Some blows can't be guarded. They glow violet. Don't stand there: roll through them (Shift).",
                },
                Task = "Dodge 2 violet blows (Shift as they land)", Need = 2,
            },
            new Lesson
            {
                Kind = LessonKind.Launch, Title = "Rising Verse",
                Intro = new[]
                {
                    "One more song every Minstrel knows: Rising Verse, 1-1-2. The last blow throws them up; they can't fight back until they land.",
                },
                Task = "Launch the dummy with Rising Verse (1-1-2)", Prompt = "1-1-2-.", Need = 1,
            },
            new Lesson
            {
                Kind = LessonKind.Bout, Title = "Final bout",
                Intro = new[]
                {
                    "Enough wood and blunt steel. That one's a real bone knight, and his blade is sharp. Use what you've learned.",
                },
                Task = "Defeat the bone knight", Need = 1,
            },
        };

        public void Begin()
        {
            Index = 0;
            Count = Count2 = 0;
            Started?.Invoke(Current);
        }

        /// <summary>Jump to a lesson (Skip to the final bout).</summary>
        public void SkipTo(LessonKind kind)
        {
            for (int i = 0; i < Lessons.Count; i++)
                if (Lessons[i].Kind == kind) { Index = i; Count = Count2 = 0; Started?.Invoke(Current); return; }
        }

        bool Is(LessonKind k) => !Done && Current.Kind == k;

        bool Check()
        {
            var l = Current;
            if (Count < l.Need || Count2 < l.Need2) return false;
            Index++;
            Count = Count2 = 0;
            Started?.Invoke(Current);
            return true;
        }

        // ---------------- What the player did ----------------

        public bool Calibrated() => Is(LessonKind.Calibrate) && Bump();

        /// <summary>A note: <paramref name="onBeat"/> is false for off-beat notes (and double presses).</summary>
        public bool Note(NoteButton button, bool onBeat, Judgement judgement)
        {
            bool good = onBeat && judgement >= Judgement.Good;
            if (Is(LessonKind.Beat))
            {
                if (button != NoteButton.One) return false;
                Count = good ? Count + 1 : 0;
                return Check();
            }
            if (Is(LessonKind.TwoNotes) && good)
            {
                if (button == NoteButton.One) Count = Math.Min(Current.Need, Count + 1);
                else Count2 = Math.Min(Current.Need2, Count2 + 1);
                return Check();
            }
            return false;
        }

        /// <summary>A sequence finished; <paramref name="landed"/> when its finisher hit something.</summary>
        public bool Sequence(string key, bool landed)
        {
            if (Is(LessonKind.Sequences))
            {
                if (key == "CrossingCuts") Count = Math.Min(Current.Need, Count + 1);
                else if (key == "Powerslice") Count2 = Math.Min(Current.Need2, Count2 + 1);
                else return false;
                return Check();
            }
            if (Is(LessonKind.Launch) && key == "RisingVerse" && landed) return Bump();
            return false;
        }

        /// <summary>An enemy blow was answered (or not).</summary>
        public bool Defense(DefenseResult r)
        {
            if (Is(LessonKind.Guard) && (r == DefenseResult.Blocked || r == DefenseResult.Parried)) return Bump();
            if (Is(LessonKind.Parry) && r == DefenseResult.Parried) return Bump();
            if (Is(LessonKind.Dodge) && r == DefenseResult.Dodged) return Bump();
            return false;
        }

        public bool BoutWon() => Is(LessonKind.Bout) && Bump();

        bool Bump() { Count++; return Check(); }

        /// <summary>"5 / 8", "Light 2/4  Heavy 4/4", for the task line.</summary>
        public string ProgressText
        {
            get
            {
                var l = Current;
                if (l == null) return "";
                switch (l.Kind)
                {
                    case LessonKind.TwoNotes: return $"light {Count}/{l.Need}   heavy {Count2}/{l.Need2}";
                    case LessonKind.Sequences: return $"Crossing Cuts {Count}/{l.Need}   Powerslice {Count2}/{l.Need2}";
                    case LessonKind.Beat: return $"in a row {Count}/{l.Need}";
                    case LessonKind.Calibrate: case LessonKind.Bout: return "";
                    default: return $"{Count}/{l.Need}";
                }
            }
        }

        /// <summary>The prompted note for a beat (null = none): the lesson's pattern repeats from each bar's downbeat.</summary>
        public static NoteButton? PromptAt(string prompt, int beatInBar, long measure, int beatsPerMeasure)
        {
            if (string.IsNullOrEmpty(prompt)) return null;
            var steps = prompt.Split('-');
            long beat = measure * beatsPerMeasure + beatInBar;
            long i = ((beat % steps.Length) + steps.Length) % steps.Length;
            switch (steps[i])
            {
                case "1": return NoteButton.One;
                case "2": return NoteButton.Two;
                default: return null;
            }
        }
    }
}
