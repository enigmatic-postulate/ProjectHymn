using System;
using System.Collections.Generic;

namespace ProjectHymn.Progression
{
    /// <summary>One illustrated card of the intro or the ending: a painted picture and a line or two of text.</summary>
    public readonly struct StoryCard
    {
        /// <summary>Texture under Resources/UI/Story (painted by Tools/paint_story.py).</summary>
        public readonly string Image;
        public readonly string Text;
        public StoryCard(string image, string text) { Image = image; Text = text; }
    }

    /// <summary>
    /// The story around the run (VS-5 Part A), as plain data so it can be tested and rewritten in one place. All text is
    /// placeholder, built on the GDD's setting: the Hymn, sung at the world's making, has cracked; its verses are scattered
    /// and the dead march to what is left. The player is the cantor of Saltmere, bringing the verses home one stage at a
    /// time. Each story stage restores a verse; the village shows and plays what has come back.
    /// </summary>
    public static class Story
    {
        // Save flags (SaveData.story)
        public const string EndingSeen = "ending";
        /// <summary>VS-6: the second chapter's ending (after the Sunken Apse) and its prologue (first arrival at the Air Bell).</summary>
        public const string DeepEndingSeen = "ending2", DeepIntroSeen = "chapter2";
        public static string VerseShownFlag(int verse) => "verse" + verse;

        /// <summary>
        /// The stages that hold a verse, in story order (the Yard, the Arena and Starlit Spring hold none). Verses 1 to 3 are
        /// Saltmere's (chapter one); 4 to 6 lie in the Drowned Nave (VS-6, chapter two) and are celebrated at the Air Bell.
        /// </summary>
        public static readonly string[] VerseStages = { "Cliffside", "Belfry", "Summit", "Nave", "Loft", "Apse" };
        public static readonly string[] VerseNames =
        {
            "the Verse of the Tide", "the Verse of the Bell", "the Verse of the Summit",
            "the Verse of the Swell", "the Verse of the Choir", "the Verse of the Deep",
        };
        public const int Verses = 6;
        /// <summary>Verses per region: Saltmere holds 1 to 3, the Drowned Nave 4 to 6.</summary>
        public const int VersesPerRegion = 3;

        /// <summary>The region a verse belongs to (0 = Saltmere, 1 = the Drowned Nave).</summary>
        public static int RegionOf(int verse) => verse <= 0 ? 0 : (verse - 1) / VersesPerRegion;

        /// <summary>How many verses are home: the story stages cleared, in order (0 to 3). The story's chapter.</summary>
        public static int Chapter(SaveData s)
        {
            if (s == null) return 0;
            int n = 0;
            while (n < VerseStages.Length && s.HasCleared(VerseStages[n])) n++;
            return n;
        }

        /// <summary>The verse a stage holds (1-based), or 0.</summary>
        public static int VerseOf(string stage) => Array.IndexOf(VerseStages, stage) + 1;

        /// <summary>A name for verse <paramref name="verse"/> (1-based), capitalised for a banner.</summary>
        public static string VerseTitle(int verse) =>
            verse >= 1 && verse <= Verses ? char.ToUpperInvariant(VerseNames[verse - 1][0]) + VerseNames[verse - 1].Substring(1) : "";

        /// <summary>
        /// The newest verse home if the village hasn't celebrated it yet (1-based), or 0. Only the newest is celebrated: a
        /// save from before the story (several stages already cleared) gets one moment, not three in a row.
        /// </summary>
        public static int VerseToReveal(SaveData s)
        {
            int ch = Chapter(s);
            return ch > 0 && !s.HasStory(VerseShownFlag(ch)) ? ch : 0;
        }

        /// <summary>Mark verses 1 to <paramref name="verse"/> as celebrated.</summary>
        public static void MarkRevealed(SaveData s, int verse)
        {
            for (int v = 1; v <= verse && v <= Verses; v++) s.MarkStory(VerseShownFlag(v));
        }

        /// <summary>The save flag of the ending a stage closes (the Summit ends chapter one, the Sunken Apse chapter two), or empty.</summary>
        public static string EndingFlag(string stage) => stage == "Summit" ? EndingSeen : stage == "Apse" ? DeepEndingSeen : "";

        /// <summary>A chapter's ending plays once, after the first clear of its last stage.</summary>
        public static bool EndingDue(SaveData s, string stageCleared)
        {
            string flag = EndingFlag(stageCleared);
            return s != null && flag.Length > 0 && s.HasCleared(stageCleared) && !s.HasStory(flag);
        }

        /// <summary>The ending's cards for the stage that closed the chapter.</summary>
        public static StoryCard[] EndingCards(string stage) => stage == "Apse" ? DeepEnding : Ending;

        /// <summary>Where an ending sends you afterwards: the hub of the chapter it closed.</summary>
        public static string EndingHub(string stage) => stage == "Apse" ? "AirBell" : "Village";

        // ---------------- The intro and the ending ----------------

        public static readonly StoryCard[] Intro =
        {
            new StoryCard("intro_song", "In the beginning there was a song. The Hymn set the tides rolling and the stars turning, every one of them in time."),
            new StoryCard("intro_saltmere", "Saltmere kept a verse of it. Fishers sang it at the oars, and cantors stood watch on the cliffs, striking only when the song struck."),
            new StoryCard("intro_crack", "Then the Last Cantor climbed the summit and would not stop counting. The Hymn cracked, its verses scattered into the hills, and the dead rose to march to what was left."),
            new StoryCard("intro_cantor", "Now Saltmere is quiet, and its lanterns won't light. You are the last cantor it has. Find the verses, and bring the song home."),
        };

        public static readonly StoryCard[] Ending =
        {
            new StoryCard("end_summit", "The Last Cantor lowers his staff. For the first time in a hundred years the counting stops, and the summit is silent."),
            new StoryCard("end_verses", "Then, faintly, the third verse rises from the stones to meet the other two. Down the mountain, Saltmere hears it."),
            new StoryCard("end_home", "The Hymn isn't whole. Other towns kept other verses, and other conductors still count in the dark. But tonight, Saltmere sings."),
        };

        /// <summary>Chapter two's prologue, the first time you reach the Air Bell.</summary>
        public static readonly StoryCard[] DeepIntro =
        {
            new StoryCard("deep_descent", "Below Saltmere's harbour the old cathedral lies under the sea. Its bells still ring down there, slow, in sixes."),
            new StoryCard("deep_airbell", "Divers live in its great bell, in the air trapped under the bronze. They heard the summit fall silent, and they have been waiting for you."),
        };

        /// <summary>Chapter two's ending, after the Drowned Precentor falls.</summary>
        public static readonly StoryCard[] DeepEnding =
        {
            new StoryCard("end2_precentor", "The Precentor lowers his hand. The sixes stop rocking, and the water in the nave goes still."),
            new StoryCard("end2_bell", "The great bell hums on its own, low and long, and three more verses rise through the water toward the light."),
            new StoryCard("end2_road", "Six verses home. Up on the cliffs, Saltmere hears the deep answer. The rest of the Hymn is still out there, counting."),
        };

        /// <summary>The credits, one line each ("# " starts a heading, an empty string is a gap).</summary>
        public static readonly string[] Credits =
        {
            "# PROJECT HYMN", "",
            "# Design", "Tyler", "",
            "# Development", "Marcel", "",
            "# Code assistance", "Claude (Anthropic)", "",
            "# Characters and animation", "KayKit by Kay Lousberg (CC0)", "",
            "# Village and environment", "Quaternius Medieval Village MegaKit (CC0)", "Poly Haven (CC0)", "",
            "# Music", "Battle Theme 1: placeholder reference track",
            "Evening in the Village, Waltz of the Belfry, Hymn of the Summit,", "Practice in the Yard, Barcarolle of the Drowned Nave,",
            "Canticle of the Precentor, Hush of the Air Bell: placeholders written for Project Hymn", "",
            "# Story", "Placeholder, to be rewritten", "", "",
            "Thanks for playing.",
        };

        // ---------------- The villagers ----------------

        /// <summary>The villagers who follow the story (HubBuilder places them; the Mentor, Innkeeper and Merchant have their own menus).</summary>
        public static readonly string[] Villagers = { "Elder", "Fisher", "Lookout", "Lamplighter" };

        /// <summary>The divers at the Air Bell (VS-6). Their lines start at chapter 3 (the summit cleared), not 0.</summary>
        public static readonly string[] Divers = { "Diver", "Bellwarden", "Novice" };

        /// <summary>The chapter a speaker's first lines belong to (3 for the divers, 0 for Saltmere).</summary>
        public static int FirstChapterOf(string speaker) => Array.IndexOf(Divers, speaker) >= 0 ? 3 : 0;

        /// <summary>What each villager says, by chapter (0 = no verse home yet, 3 = all three); a diver's lines count from chapter 3.</summary>
        static readonly Dictionary<string, string[][]> Lines = new Dictionary<string, string[][]>
        {
            ["Elder"] = new[]
            {
                new[]
                {
                    "So you're the new cantor. Good. The cliffs have been restless.",
                    "Strike on the beat and the blow lands true. Strike off it and you're only waving a sword.",
                    "The first verse went west, to the cliffs. Bring it home. Here, let me tell you the old song...",
                },
                new[]
                {
                    "You brought the tide verse back. Listen... the boats are singing again.",
                    "The bell on the ridge kept the second verse. The dead up there dance in threes. Count three, not four.",
                },
                new[]
                {
                    "Two verses home. I'd forgotten how the old song turns in the middle.",
                    "Only the summit is left. The Last Cantor counts in four, then three, then seven, and he raises his staff a bar before he changes.",
                },
                new[]
                {
                    "It's whole again, or as whole as we ever had it. Thank you, cantor.",
                    "Other towns kept other verses, you know. Somewhere the rest of the Hymn is waiting.",
                },
            },
            ["Fisher"] = new[]
            {
                new[]
                {
                    "Bone knights on the tiers again. They lift that blade a whole bar before it comes down.",
                    "Count it. Guard on the beat it lands and they reel. That's when you cut.",
                },
                new[]
                {
                    "Took the boat out at dusk and the oars fell in time on their own. Haven't felt that since I was a boy.",
                    "Knights are still on the tiers, mind. Guard on the beat and they reel.",
                },
                new[]
                {
                    "The bell's ringing on the ridge again. My wife cried. Don't tell her I told you.",
                },
                new[]
                {
                    "Whole village is singing at the oars tomorrow. You're coming. No, you don't get a say.",
                },
            },
            ["Lookout"] = new[]
            {
                new[]
                {
                    "The cliffs are through the gate, east end of the street. New to the beat? Try the practice yard first.",
                    "Clear the cliffs and the arena road opens too, if you want a fight without the climb. Three waves, and they don't wait.",
                },
                new[]
                {
                    "Cliffs are quiet. The ridge road to the belfry is open now, past the gate. Mind your count: they waltz up there.",
                    "And the arena's open, if you want a fight without the climb.",
                },
                new[]
                {
                    "The summit path is open. Nobody's come back down it. Well, nobody but you, so far.",
                },
                new[]
                {
                    "Nothing moving on the summit tonight. First time in my watch.",
                    "The arena's still open if you miss the fighting. And there's a spring up in the stars, they say, if you can find the way.",
                    "And the divers sent word up the harbour chain: there's a way down to the drowned cathedral now. It's on the map at the gate.",
                },
            },
            ["Lamplighter"] = new[]
            {
                new[]
                {
                    "No point lighting them. The flame won't take while the song is gone. I've tried every night.",
                    "Bring a verse back and I'll light every lantern on this street. That's a promise.",
                },
                new[]
                {
                    "See? They took! Every one. It was the tide verse they were waiting for.",
                },
                new[]
                {
                    "Strung the banners too. The bell verse is a dancing verse; it seemed only right.",
                },
                new[]
                {
                    "Look up. That star over the gate wasn't there yesterday. I didn't put it there. I think the song did.",
                },
            },
            ["Diver"] = new[]
            {
                new[]
                {
                    "You came down the chain? Then the summit really is quiet. Welcome to the Air Bell.",
                    "Everything below rocks in sixes. Count ONE-two-three FOUR-five-six, and lean on the one and the four.",
                },
                new[]
                {
                    "The swell verse is back. Listen: the water outside the bronze has a pulse again.",
                    "The choir loft is open above the transept. The singers still sing. Guard their answers and they fly home.",
                },
                new[]
                {
                    "Two verses down here. Only the apse is left, and the Precentor keeps it.",
                    "He bends the bar into threes and then stretches it to nine. Watch his hand: he lifts it a bar before he turns.",
                },
                new[]
                {
                    "Six verses. I never thought I'd hear the old song with a bell under it.",
                    "There are other cathedrals, they say. Other towns. Get some sleep first.",
                },
            },
            ["Bellwarden"] = new[]
            {
                new[]
                {
                    "Mind the bronze, it rings if you so much as breathe on it. This bell is all the air we have.",
                    "Rest by my lamp if you need it. The deck's safe. The water isn't.",
                },
                new[]
                {
                    "The lamps took the flame on their own last night. Sea-glass, burning. Never seen the like.",
                },
                new[]
                {
                    "The ribbons from the old choir moved in the still air. All of them, the same way, in time.",
                },
                new[]
                {
                    "Hear that hum? That's the bell itself. Three hundred years I've kept it and it's never sung before.",
                },
            },
            ["Novice"] = new[]
            {
                new[]
                {
                    "The jellies hang high and sting on the big beats. When they sink after, that's your moment. Cut.",
                    "The knights down here swing heavy on the one. Guard it. Don't try to outrun it.",
                },
                new[]
                {
                    "I followed you as far as the transept. Then a chorister held one long note and the floor rang. I jumped. Barely.",
                },
                new[]
                {
                    "If the Precentor tolls the great bell, nothing stops it. Not your guard, not anything. Get off the floor.",
                },
                new[]
                {
                    "Can I come up to Saltmere? Just to see it. I've only ever seen the sea from underneath.",
                },
            },
        };

        /// <summary>A villager's lines for a chapter (the nearest earlier chapter if none are written); empty if unknown.</summary>
        public static string[] LinesFor(string villager, int chapter)
        {
            if (string.IsNullOrEmpty(villager) || !Lines.TryGetValue(villager, out var byChapter)) return new string[0];
            chapter = Math.Max(0, chapter - FirstChapterOf(villager));
            for (int c = Math.Min(chapter, byChapter.Length - 1); c >= 0; c--)
                if (byChapter[c] != null && byChapter[c].Length > 0) return byChapter[c];
            return new string[0];
        }

        /// <summary>The lore piece a villager hands over on the first talk (the Elder's old song), or empty.</summary>
        public static string LoreFrom(string villager) => villager == "Elder" ? "lore_village" : "";

        /// <summary>The banner when a verse comes home, and the line under it.</summary>
        public static string RevealLine(int verse)
        {
            switch (verse)
            {
                case 1: return "The lanterns of Saltmere take the flame.";
                case 2: return "Banners go up along the street, and the bell's tune joins the song.";
                case 3: return "A star rises over the gate. The Hymn of Saltmere is whole.";
                case 4: return "Sea-glass lamps light up along the divers' deck.";
                case 5: return "The old choir's ribbons stir under the bronze, and its chant joins the hush.";
                case 6: return "The crown of the great bell shines. The deep verses are home.";
                default: return "";
            }
        }
    }
}
