using System;
using System.Collections.Generic;

namespace ProjectHymn.Progression
{
    [Serializable]
    public class ItemCount
    {
        public string id;
        public int count;
        public ItemCount() { }
        public ItemCount(string id, int count) { this.id = id; this.count = count; }
    }

    public enum PurchaseResult { Bought, TooPoor, AlreadyKnown, Invalid }

    /// <summary>
    /// Play counted over clean clears (Slice 8): what unlocks the Warden and the Crusader. Shares are of the enemy attacks
    /// that reached the player (parried, blocked, dodged or taken) and of the player's hits.
    /// </summary>
    [Serializable]
    public class PlayTotals
    {
        public int parries, blocks, dodges, hitsTaken, hits, offBeat, cleanClears;

        public int EnemyAttacks => parries + blocks + dodges + hitsTaken;
        public double GuardShare => EnemyAttacks == 0 ? 0.0 : (parries + blocks) / (double)EnemyAttacks;
        public double DodgeShare => EnemyAttacks == 0 ? 0.0 : dodges / (double)EnemyAttacks;
        public double OnBeatShare => hits == 0 ? 0.0 : (hits - offBeat) / (double)hits;

        public void Add(PlaystyleLog log)
        {
            parries += log.Parries; blocks += log.Blocks; dodges += log.Dodges; hitsTaken += log.HitsTaken;
            hits += log.Attacks; offBeat += log.OffBeat; cleanClears++;
        }
    }

    /// <summary>One finished run (rank history).</summary>
    [Serializable]
    public class RunRecord
    {
        public string stage;
        public string rank;
        public double seconds;
    }

    /// <summary>A stage's best clear (rank letter and time) and how often it was cleared.</summary>
    [Serializable]
    public class StageRecord
    {
        public string stage;
        public string bestRank = "";
        public double bestSeconds;
        public int clears;
    }

    /// <summary>
    /// Everything a save file holds, and the rules for changing it: Notes, items and the quick slot, learned
    /// sequences, lore found, pickups collected and stages cleared (Hub &amp; Loop); level, experience, class, weapon,
    /// unlocked weapons and each stage's best rank (Rank &amp; Class, version 2). Plain C# with public fields so
    /// Unity's JsonUtility writes it as is; unit-tested.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 3;
        public const int HistoryLength = 30;
        public const int MaxStack = 9;

        public int version = CurrentVersion;
        public string classId = "";
        public int notes;
        public List<ItemCount> items = new List<ItemCount>();
        public string quickItem = "";
        public List<string> sequences = new List<string>();
        public List<string> lore = new List<string>();
        public List<string> collected = new List<string>();
        public List<string> stagesCleared = new List<string>();
        public string scene = "";
        public string spawn = "";
        public double playSeconds;
        public int level = 1;
        /// <summary>Experience into the current level (a level-down keeps it).</summary>
        public int xp;
        public string weaponId = ProgressionRules.Sword;
        public List<string> weapons = new List<string> { ProgressionRules.Sword };
        public List<StageRecord> stageRecords = new List<StageRecord>();
        /// <summary>Classes play has unlocked (the Minstrel always; the Fighter once reached).</summary>
        public List<string> unlockedClasses = new List<string> { ProgressionRules.Minstrel };
        public PlayTotals totals = new PlayTotals();
        /// <summary>Weapons play has put in the shop (bought ones move to <see cref="weapons"/>).</summary>
        public List<string> weaponsForSale = new List<string>();
        public List<RunRecord> history = new List<RunRecord>();
        /// <summary>The Practice Yard was finished once (its experience is paid only the first time).</summary>
        public bool tutorialDone;
        /// <summary>Story moments already shown (VS-5): "verse1".."verse3" (the village celebrated it), "ending".</summary>
        public List<string> story = new List<string>();

        public static SaveData NewGame(string classId, IEnumerable<string> startingSequences, IEnumerable<ItemCount> startingItems = null)
        {
            var s = new SaveData { classId = classId };
            s.level = 1;
            s.LearnTree(ProgressionRules.Minstrel);
            s.LearnTree(classId);
            if (startingSequences != null) foreach (var q in startingSequences) s.Learn(q);
            if (startingItems != null) foreach (var it in startingItems) s.AddItem(it.id, it.count);
            return s;
        }

        // ---------------- Notes ----------------

        public void AddNotes(int amount) { if (amount > 0) notes += amount; }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || notes < amount) return false;
            notes -= amount;
            return true;
        }

        // ---------------- Items ----------------

        public int Count(string id)
        {
            foreach (var it in items) if (it.id == id) return it.count;
            return 0;
        }

        /// <summary>Add up to the stack limit; returns how many were added. Sets the quick slot if it is empty.</summary>
        public int AddItem(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return 0;
            ItemCount slot = null;
            foreach (var it in items) if (it.id == id) slot = it;
            if (slot == null) { slot = new ItemCount(id, 0); items.Add(slot); }
            int added = Math.Min(count, MaxStack - slot.count);
            slot.count += Math.Max(0, added);
            if (string.IsNullOrEmpty(quickItem)) quickItem = id;
            return Math.Max(0, added);
        }

        /// <summary>Use one of an item. Returns false if there is none. An emptied quick slot moves to the next item held.</summary>
        public bool UseItem(string id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].id != id || items[i].count <= 0) continue;
                items[i].count--;
                if (items[i].count == 0)
                {
                    items.RemoveAt(i);
                    if (quickItem == id) quickItem = items.Count > 0 ? items[0].id : "";
                }
                return true;
            }
            return false;
        }

        /// <summary>The next held item after the quick slot's (wraps); empty when nothing is held.</summary>
        public string CycleQuickItem()
        {
            if (items.Count == 0) { quickItem = ""; return quickItem; }
            int at = items.FindIndex(it => it.id == quickItem);
            quickItem = items[(at + 1) % items.Count].id;
            return quickItem;
        }

        // ---------------- Sequences, lore, pickups, stages ----------------

        public bool Knows(string sequence) => sequences.Contains(sequence);

        /// <summary>Learn a class's whole tree (the sequence library); returns the keys that were new.</summary>
        public List<string> LearnTree(string classId)
        {
            var learned = new List<string>();
            if (string.IsNullOrEmpty(classId)) return learned;
            foreach (var k in ProjectHymn.Combat.SequenceLibrary.TreeKeys(classId)) if (Learn(k)) learned.Add(k);
            return learned;
        }

        public bool Learn(string sequence)
        {
            if (string.IsNullOrEmpty(sequence) || sequences.Contains(sequence)) return false;
            sequences.Add(sequence);
            return true;
        }

        public bool AddLore(string id)
        {
            if (string.IsNullOrEmpty(id) || lore.Contains(id)) return false;
            lore.Add(id);
            return true;
        }

        public bool IsCollected(string pickupId) => collected.Contains(pickupId);

        public bool MarkCollected(string pickupId)
        {
            if (string.IsNullOrEmpty(pickupId) || collected.Contains(pickupId)) return false;
            collected.Add(pickupId);
            return true;
        }

        public bool HasStory(string flag) => story != null && story.Contains(flag);

        public bool MarkStory(string flag)
        {
            if (string.IsNullOrEmpty(flag) || story.Contains(flag)) return false;
            story.Add(flag);
            return true;
        }

        public bool HasCleared(string stage) => stagesCleared.Contains(stage);

        public void ClearStage(string stage) { if (!string.IsNullOrEmpty(stage) && !stagesCleared.Contains(stage)) stagesCleared.Add(stage); }

        // ---------------- Weapons and stage records ----------------

        public bool HasWeapon(string id) => !string.IsNullOrEmpty(id) && weapons.Contains(id);

        public bool HasUnlockedClass(string id) => !string.IsNullOrEmpty(id) && unlockedClasses.Contains(id);

        public bool UnlockClass(string id)
        {
            if (string.IsNullOrEmpty(id) || unlockedClasses.Contains(id)) return false;
            unlockedClasses.Add(id);
            return true;
        }

        /// <summary>Put a weapon in the shop (play unlocks, the shop sells). False if it's already owned or on sale.</summary>
        public bool OfferWeapon(string id)
        {
            if (string.IsNullOrEmpty(id) || weapons.Contains(id) || weaponsForSale.Contains(id)) return false;
            weaponsForSale.Add(id);
            return true;
        }

        /// <summary>Buy a weapon the shop offers.</summary>
        public PurchaseResult BuyWeapon(string id, int price)
        {
            if (string.IsNullOrEmpty(id) || price < 0) return PurchaseResult.Invalid;
            if (weapons.Contains(id)) return PurchaseResult.AlreadyKnown;
            if (!weaponsForSale.Contains(id)) return PurchaseResult.Invalid;
            if (!TrySpend(price)) return PurchaseResult.TooPoor;
            weaponsForSale.Remove(id);
            weapons.Add(id);
            return PurchaseResult.Bought;
        }

        public void AddHistory(string stage, StageRank rank, double seconds)
        {
            if (string.IsNullOrEmpty(stage)) return;
            history.Add(new RunRecord { stage = stage, rank = rank.ToString(), seconds = seconds });
            if (history.Count > HistoryLength) history.RemoveRange(0, history.Count - HistoryLength);
        }

        /// <summary>Own a weapon outright (the start, a class that brings its weapon, or free play).</summary>
        public bool UnlockWeapon(string id)
        {
            if (string.IsNullOrEmpty(id) || weapons.Contains(id)) return false;
            weapons.Add(id);
            weaponsForSale.Remove(id);
            return true;
        }

        public StageRecord Record(string stage)
        {
            foreach (var r in stageRecords) if (r.stage == stage) return r;
            return null;
        }

        public bool TryBestRank(string stage, out StageRank rank)
        {
            rank = StageRank.C;
            var r = Record(stage);
            return r != null && Enum.TryParse(r.bestRank, out rank);
        }

        /// <summary>Count a clear; returns true when it beats the stage's best rank (or time at the same rank).</summary>
        public bool RecordStage(string stage, StageRank rank, double seconds)
        {
            if (string.IsNullOrEmpty(stage)) return false;
            ClearStage(stage);
            var r = Record(stage);
            if (r == null) { r = new StageRecord { stage = stage }; stageRecords.Add(r); }
            r.clears++;
            bool better = !Enum.TryParse(r.bestRank, out StageRank best) || rank < best || (rank == best && seconds < r.bestSeconds);
            if (better) { r.bestRank = rank.ToString(); r.bestSeconds = seconds; }
            return better;
        }

        // ---------------- Shop ----------------

        /// <summary>
        /// Buy one item for <paramref name="price"/> Notes. A scroll (<paramref name="teaches"/> set) is learned at once
        /// instead of stored, and can't be bought twice.
        /// </summary>
        public PurchaseResult Buy(string itemId, int price, string teaches = null)
        {
            if (string.IsNullOrEmpty(itemId) || price < 0) return PurchaseResult.Invalid;
            if (!string.IsNullOrEmpty(teaches) && Knows(teaches)) return PurchaseResult.AlreadyKnown;
            if (string.IsNullOrEmpty(teaches) && Count(itemId) >= MaxStack) return PurchaseResult.Invalid;
            if (!TrySpend(price)) return PurchaseResult.TooPoor;
            if (!string.IsNullOrEmpty(teaches)) Learn(teaches); else AddItem(itemId);
            return PurchaseResult.Bought;
        }

        /// <summary>Fix up a file from an older or hand-edited save.</summary>
        public void Normalize()
        {
            if (items == null) items = new List<ItemCount>();
            if (sequences == null) sequences = new List<string>();
            if (lore == null) lore = new List<string>();
            if (collected == null) collected = new List<string>();
            if (stagesCleared == null) stagesCleared = new List<string>();
            if (weapons == null) weapons = new List<string>();
            if (stageRecords == null) stageRecords = new List<StageRecord>();
            stageRecords.RemoveAll(r => r == null || string.IsNullOrEmpty(r.stage));
            if (unlockedClasses == null) unlockedClasses = new List<string>();
            if (totals == null) totals = new PlayTotals();
            if (weaponsForSale == null) weaponsForSale = new List<string>();
            if (history == null) history = new List<RunRecord>();
            if (story == null) story = new List<string>();
            if (version < 3)
            {
                // Saves from before Update 18 load as a level 1 Minstrel (Notes, lore and stage records are kept).
                classId = ProgressionRules.Minstrel;
                level = 1;
                xp = 0;
                unlockedClasses.Clear();
                totals = new PlayTotals();
                weapons = new List<string> { ProgressionRules.Sword };
                weaponsForSale.Clear();
                weaponId = ProgressionRules.Sword;
                items.Clear();
                quickItem = "";
            }
            if (string.IsNullOrEmpty(classId)) classId = ProgressionRules.Minstrel;
            UnlockClass(ProgressionRules.Minstrel);
            UnlockClass(classId);
            LearnTree(ProgressionRules.Minstrel);
            LearnTree(classId);
            UnlockWeapon(ProgressionRules.Sword);
            if (!HasWeapon(weaponId)) weaponId = ProgressionRules.Sword;
            if (level < 1) level = 1;
            if (xp < 0) xp = 0;
            items.RemoveAll(it => it == null || string.IsNullOrEmpty(it.id) || it.count <= 0);
            foreach (var it in items) it.count = Math.Min(it.count, MaxStack);
            if (notes < 0) notes = 0;
            if (!string.IsNullOrEmpty(quickItem) && Count(quickItem) == 0) quickItem = items.Count > 0 ? items[0].id : "";
            version = CurrentVersion;
        }
    }

    /// <summary>Notes awarded by enemies (a roll between a minimum and maximum). Pure C#.</summary>
    public static class Rewards
    {
        public static int RollNotes(int min, int max, Random rng)
        {
            if (max < min) { int t = min; min = max; max = t; }
            return rng.Next(min, max + 1);
        }
    }
}
