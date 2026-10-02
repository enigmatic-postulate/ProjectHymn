using System;
using System.Collections.Generic;

namespace ProjectHymn.Progression
{
    public enum Rank { D = 0, C = 1, B = 2, A = 3, S = 4 }

    [Serializable]
    public class ArenaRecord
    {
        public string id;
        public bool cleared;
        public int bestRank = -1;      // Rank as int, -1 = never ranked
        public long bestScore;
        public bool remixUnlocked;     // GDD: an S rank unlocks a remix of the arena's theme
    }

    [Serializable]
    public class SettingsData
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.8f;
        public bool metronome = true;
        public bool showBeatCue = false;   // accessibility, off by default (GDD)
        public bool easyTiming = false;    // widens timing windows by 50%
        public float calibrationMs = 0f;
    }

    /// <summary>Everything that persists between sessions. Plain data, saved as JSON.</summary>
    [Serializable]
    public class SaveData
    {
        public const int Version = 1;
        public const int MaxEquippedPhrases = 6;

        public int version = Version;
        public int notes;
        public List<string> unlockedPhrases = new List<string>();
        public List<string> equippedPhrases = new List<string>();
        public List<string> ownedItems = new List<string>();
        public string weaponId = "Blade";
        public string armorId = "CantorRobe";
        public List<ArenaRecord> arenas = new List<ArenaRecord>();
        public List<string> loreFound = new List<string>();
        public SettingsData settings = new SettingsData();

        /// <summary>A fresh game: three starter Phrases equipped, the Blade and the Cantor Robe.</summary>
        public static SaveData NewGame()
        {
            var s = new SaveData();
            foreach (var p in new[] { "TripletCut", "Syncopate", "OffStep" })
            {
                s.unlockedPhrases.Add(p);
                s.equippedPhrases.Add(p);
            }
            s.ownedItems.Add("Blade");
            s.ownedItems.Add("CantorRobe");
            return s;
        }
    }

    /// <summary>What an arena run earned, for the results screen.</summary>
    public readonly struct RunReward
    {
        public readonly Rank Rank;
        public readonly int NotesEarned;
        public readonly bool FirstClear;
        public readonly string PhraseUnlocked;   // null if none
        public readonly bool NewBestRank;
        public readonly bool RemixUnlocked;      // newly unlocked this run

        public RunReward(Rank rank, int notes, bool firstClear, string phrase, bool newBest, bool remix)
        {
            Rank = rank; NotesEarned = notes; FirstClear = firstClear; PhraseUnlocked = phrase;
            NewBestRank = newBest; RemixUnlocked = remix;
        }
    }

    /// <summary>Rules that change a <see cref="SaveData"/>. Pure C#: no Unity, fully unit-tested.</summary>
    public sealed class ProgressionRules
    {
        public int ScorePerNote = 200;
        public int[] RankBonusNotes = { 0, 50, 100, 200, 300 }; // D..S

        public SaveData Data { get; }

        public ProgressionRules(SaveData data) { Data = data ?? SaveData.NewGame(); }

        // ---- Notes and shop ----

        public void AddNotes(int amount) => Data.notes = Math.Max(0, Data.notes + amount);

        public bool CanAfford(int price) => Data.notes >= price;

        public bool OwnsItem(string id) => Data.ownedItems.Contains(id);

        /// <summary>Buy a weapon or armor. Fails if already owned or too expensive.</summary>
        public bool BuyItem(string id, int price)
        {
            if (string.IsNullOrEmpty(id) || OwnsItem(id) || !CanAfford(price)) return false;
            Data.notes -= price;
            Data.ownedItems.Add(id);
            return true;
        }

        /// <summary>Buy a Phrase. Fails if already known or too expensive. Bought Phrases auto-equip if there's room.</summary>
        public bool BuyPhrase(string id, int price)
        {
            if (string.IsNullOrEmpty(id) || HasPhrase(id) || !CanAfford(price)) return false;
            Data.notes -= price;
            UnlockPhrase(id);
            return true;
        }

        // ---- Phrases ----

        public bool HasPhrase(string id) => Data.unlockedPhrases.Contains(id);
        public bool IsEquipped(string id) => Data.equippedPhrases.Contains(id);

        /// <summary>Learn a Phrase; equip it straight away when a slot is free. Returns false if already known.</summary>
        public bool UnlockPhrase(string id)
        {
            if (string.IsNullOrEmpty(id) || HasPhrase(id)) return false;
            Data.unlockedPhrases.Add(id);
            if (Data.equippedPhrases.Count < SaveData.MaxEquippedPhrases) Data.equippedPhrases.Add(id);
            return true;
        }

        /// <summary>Equip or unequip a known Phrase (max 6 equipped, at least 1).</summary>
        public bool TogglePhrase(string id)
        {
            if (!HasPhrase(id)) return false;
            if (IsEquipped(id))
            {
                if (Data.equippedPhrases.Count <= 1) return false;
                Data.equippedPhrases.Remove(id);
                return true;
            }
            if (Data.equippedPhrases.Count >= SaveData.MaxEquippedPhrases) return false;
            Data.equippedPhrases.Add(id);
            return true;
        }

        // ---- Gear ----

        public bool EquipWeapon(string id)
        {
            if (!OwnsItem(id)) return false;
            Data.weaponId = id;
            return true;
        }

        public bool EquipArmor(string id)
        {
            if (!OwnsItem(id)) return false;
            Data.armorId = id;
            return true;
        }

        // ---- Arenas ----

        public ArenaRecord Arena(string id)
        {
            var r = Data.arenas.Find(a => a.id == id);
            if (r == null) { r = new ArenaRecord { id = id }; Data.arenas.Add(r); }
            return r;
        }

        public bool IsCleared(string id) => Data.arenas.Exists(a => a.id == id && a.cleared);

        /// <summary>Record a finished arena: Notes from score and rank, first-clear Phrase reward, best rank and remix unlock.</summary>
        public RunReward RecordArena(string id, Rank rank, long score, string firstClearPhrase)
        {
            var r = Arena(id);
            bool first = !r.cleared;
            r.cleared = true;

            bool newBest = (int)rank > r.bestRank;
            if (newBest) r.bestRank = (int)rank;
            if (score > r.bestScore) r.bestScore = score;

            bool remix = false;
            if (rank == Rank.S && !r.remixUnlocked) { r.remixUnlocked = true; remix = true; }

            int notes = (int)Math.Min(int.MaxValue, Math.Max(0, score) / Math.Max(1, ScorePerNote)) + RankBonusNotes[(int)rank];
            AddNotes(notes);

            string phrase = null;
            if (first && !string.IsNullOrEmpty(firstClearPhrase) && UnlockPhrase(firstClearPhrase)) phrase = firstClearPhrase;

            return new RunReward(rank, notes, first, phrase, newBest, remix);
        }

        // ---- Lore ----

        /// <summary>Pick up a hidden lore piece. Returns false if already found.</summary>
        public bool FindLore(string id, int notesReward)
        {
            if (string.IsNullOrEmpty(id) || Data.loreFound.Contains(id)) return false;
            Data.loreFound.Add(id);
            AddNotes(notesReward);
            return true;
        }
    }

    /// <summary>
    /// Arena rank (GDD: S to D from accuracy and Groove peak). Each time the player went down costs one
    /// rank step. Pure C#.
    /// </summary>
    public static class RankCalculator
    {
        public static Rank Calculate(double onBeatAccuracy, float peakGroove, int timesDowned)
        {
            Rank r;
            if (onBeatAccuracy >= 0.90 && peakGroove >= 80f) r = Rank.S;
            else if (onBeatAccuracy >= 0.80 && peakGroove >= 50f) r = Rank.A;
            else if (onBeatAccuracy >= 0.65) r = Rank.B;
            else if (onBeatAccuracy >= 0.50) r = Rank.C;
            else r = Rank.D;
            return (Rank)Math.Max(0, (int)r - Math.Max(0, timesDowned));
        }
    }
}
