using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The running game (Hub &amp; Loop): the save data, the class, and travel between scenes with a fade.
    /// Lives across scene loads. Created by the title screen (New Game / Continue) or, when a stage is played
    /// straight from the editor, on first use as a free-play session (first class, every sequence known).
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        static GameSession _instance;

        public static GameSession Instance
        {
            get
            {
                if (_instance == null) Create(freePlay: true);
                return _instance;
            }
        }
        public static bool Exists => _instance != null;

        public GameDatabase Db { get; private set; }
        public SaveData Data { get; private set; }
        /// <summary>Started from the editor rather than the title: everything is unlocked and nothing gates play.</summary>
        public bool FreePlay { get; private set; }
        /// <summary>Spawn point id the next scene should place the player at.</summary>
        public string PendingSpawn { get; set; }
        public bool Travelling { get; private set; }

        /// <summary>Notes, items, sequences or lore changed.</summary>
        public event Action Changed;

        CanvasGroup _fade;
        bool _saveOnArrival;

        /// <summary>The save slot in use (VS-5: three slots, 1 to 3).</summary>
        public int Slot { get; private set; } = 1;

        public static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, SaveSlots.FileName(slot));
        public string SavePath => SlotPath(Slot);
        public static bool SlotUsed(int slot) => File.Exists(SlotPath(slot));
        /// <summary>Any slot holds a save.</summary>
        public static bool HasSave
        {
            get
            {
                MigrateLegacySave();
                for (int i = 1; i <= SaveSlots.Count; i++) if (SlotUsed(i)) return true;
                return false;
            }
        }

        /// <summary>When a slot was last written, or null if it's empty.</summary>
        public static DateTime? SavedAt(int slot) => SlotUsed(slot) ? File.GetLastWriteTime(SlotPath(slot)) : (DateTime?)null;

        /// <summary>The slot saved most recently (Continue), or 0.</summary>
        public static int LatestSlot()
        {
            MigrateLegacySave();
            var at = new DateTime?[SaveSlots.Count];
            for (int i = 0; i < at.Length; i++) at[i] = SavedAt(i + 1);
            return SaveSlots.Latest(at);
        }

        /// <summary>Read a slot for the title's list without loading it; null if empty or unreadable.</summary>
        public static SaveData Peek(int slot)
        {
            try
            {
                if (!SlotUsed(slot)) return null;
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SlotPath(slot)));
                d?.Normalize();
                return d;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Builds before VS-5 kept one save file: move it into slot 1 (once).</summary>
        static void MigrateLegacySave()
        {
            try
            {
                string legacy = Path.Combine(Application.persistentDataPath, SaveSlots.LegacyFile);
                if (File.Exists(legacy) && !File.Exists(SlotPath(1))) File.Move(legacy, SlotPath(1));
            }
            catch (Exception e) { Debug.LogWarning($"[Project Hymn] Could not move the old save into slot 1: {e.Message}"); }
        }
        public ClassDefinition Class => Db != null ? Db.Class(Data.classId) : null;
        public WeaponSet Weapon => Db != null ? Db.Weapon(Data.weaponId) : null;
        public string SceneName => SceneManager.GetActiveScene().name;

        static void Create(bool freePlay)
        {
            var go = new GameObject("Game Session");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameSession>();
            Sfx.Ensure();
            _instance.Db = GameDatabase.Load();
            _instance.FreePlay = freePlay;
            _instance.Data = freePlay ? _instance.FreePlayData() : new SaveData();
            _instance.BuildFade();
            GameSettings.Apply();
        }

        /// <summary>The session the title screen drives (made if needed, not in free play).</summary>
        public static GameSession ForTitle()
        {
            if (_instance == null) Create(freePlay: false);
            _instance.FreePlay = false;
            return _instance;
        }

        SaveData FreePlayData()
        {
            var s = SaveData.NewGame(ProgressionRules.Minstrel, null);
            if (Db != null)
                foreach (var w in Db.weapons)
                {
                    if (w == null) continue;
                    s.UnlockWeapon(w.id);
                    foreach (var q in w.sequences) if (q != null) s.Learn(q.key);
                }
            if (Db != null) foreach (var c in Db.classes) if (c != null) s.UnlockClass(c.id);
            return s;
        }

        void Update()
        {
            if (Data != null) Data.playSeconds += Time.unscaledDeltaTime;
            DebugKeys();
        }

        // ---------------- Debug keys (Slice 8: test classes and ranks in minutes) ----------------

        /// <summary>A rank the next stage clear is forced to (debug F12), or null.</summary>
        public StageRank? DebugRank { get; set; }

        void DebugKeys()
        {
            if (Data == null || UiState.Blocking) return;
            bool shift;
            bool f8, f10, f11, f12;
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            f8 = kb.f8Key.wasPressedThisFrame;
            f10 = kb.f10Key.wasPressedThisFrame; f11 = kb.f11Key.wasPressedThisFrame; f12 = kb.f12Key.wasPressedThisFrame;
#else
            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            f8 = Input.GetKeyDown(KeyCode.F8);
            f10 = Input.GetKeyDown(KeyCode.F10); f11 = Input.GetKeyDown(KeyCode.F11); f12 = Input.GetKeyDown(KeyCode.F12);
#endif
            if (f8) { DebugCycleClass(shift ? -1 : 1); return; }
            var rules = Db != null ? Db.Rules : ProgressionRules.Default;
            if (f10)
            {
                // +experience: enough for the next level (levels up through the normal rules on the next clear).
                Data.xp = Mathf.Max(Data.xp, rules.XpToNext(Data.level) - 1);
                ProjectHymn.UI.GameHud.Toast($"Debug: {Data.xp}/{rules.XpToNext(Data.level)} experience (the next clear levels up)");
                Changed?.Invoke();
            }
            if (f11)
            {
                if (shift) { var d = rules.LoseLevel(Data, "debug", Db != null ? Db.ClassSpecs() : null); ProjectHymn.UI.GameHud.Toast($"Debug: level {d.LevelAfter} ({ClassName(Data.classId)})"); }
                else { Data.level = Mathf.Min(rules.LevelCap, Data.level + 1); ProjectHymn.UI.GameHud.Toast($"Debug: level {Data.level}"); }
                Changed?.Invoke();
            }
            if (f12 && !shift)
            {
                DebugRank = DebugRank == null ? StageRank.S : DebugRank == StageRank.C ? (StageRank?)null : DebugRank + 1;
                ProjectHymn.UI.GameHud.Toast(DebugRank == null ? "Debug: ranks from real play" : $"Debug: the next clear is {DebugRank} (its clear time set to match)");
            }
        }

        // ---------------- Debug: switch class ----------------

        /// <summary>F8 / Shift+F8: the next (previous) class in the database, applied at once.</summary>
        public void DebugCycleClass(int step)
        {
            if (Db == null || Db.classes.Count == 0) return;
            int i = Db.classes.FindIndex(c => c != null && c.id == Data.classId);
            for (int n = 0; n < Db.classes.Count; n++)
            {
                i = ((i + step) % Db.classes.Count + Db.classes.Count) % Db.classes.Count;
                if (Db.classes[i] != null) { DebugSetClass(Db.classes[i].id); return; }
            }
        }

        /// <summary>
        /// Playtest only: become a class without earning it. Unlocks it (for good, in a real save), learns its tree, grants a
        /// weapon it can hold, saves, and restarts the scene so its hearts, perks and weapon take effect.
        /// </summary>
        public void DebugSetClass(string classId)
        {
            if (Db == null || Db.Class(classId) == null) return;
            var learned = new List<string>();
            string granted = SetClass(classId, learned);
            SaveProgress();
            var w = Db.Weapon(Data.weaponId);
            string msg = $"Debug: now a {ClassName(classId)} with the {(w != null ? w.displayName : Data.weaponId)}";
            if (granted != null) msg += " (granted)";
            if (learned.Count > 0) msg += $"\nLearned: {Sequences.Names(learned)}";
            ProjectHymn.UI.GameHud.Toast(msg + "\nRestarting the scene...", 2.5f);
            Travel(SceneName, "Start");
        }

        public string ClassName(string id)
        {
            var c = Db != null ? Db.Class(id) : null;
            return c != null && c.id == id ? c.displayName : id;
        }

        // ---------------- Game flow ----------------

        /// <summary>A new game in <paramref name="slot"/>: everyone starts as the Minstrel with the sword, at level 1.</summary>
        public void NewGame(int slot = 1)
        {
            Slot = SaveSlots.Clamp(slot);
            var cls = Db != null ? Db.Class(ProgressionRules.Minstrel) : null;
            Data = SaveData.NewGame(ProgressionRules.Minstrel, cls != null ? cls.startingSequences : null);
            Data.weaponId = ProgressionRules.Sword;
            FreePlay = false;
            Travel(Db != null ? Db.hubScene : "Village", "Start", saveOnArrival: true);
        }

        /// <summary>Carry on from the slot saved most recently.</summary>
        public bool Continue()
        {
            int latest = LatestSlot();
            return latest > 0 && LoadSlot(latest);
        }

        /// <summary>Load a slot and go to where it was saved.</summary>
        public bool LoadSlot(int slot)
        {
            Slot = SaveSlots.Clamp(slot);
            if (!Load()) return false;
            FreePlay = false;
            string scene = string.IsNullOrEmpty(Data.scene) ? (Db != null ? Db.hubScene : "Village") : Data.scene;
            Travel(scene, string.IsNullOrEmpty(Data.spawn) ? "Start" : Data.spawn);
            return true;
        }

        public void ToTitle() => Travel(Db != null ? Db.titleScene : "Title", "");

        /// <summary>
        /// Change class (a level-up's choice): learns its sequence tree (into <paramref name="learned"/>), keeps a weapon the
        /// class can hold, and returns a weapon the class brought.
        /// </summary>
        public string SetClass(string classId, List<string> learned = null)
        {
            string granted = ProgressionRules.SetClass(Data, classId, Db != null ? Db.ClassSpecs() : null, learned);
            var cls = Class;
            if (cls != null) foreach (var q in cls.startingSequences) Data.Learn(q);
            Changed?.Invoke();
            return granted;
        }

        /// <summary>Equip an owned weapon the class can hold. Takes effect when the scene next loads.</summary>
        public bool SetWeapon(string weaponId)
        {
            var cls = Class;
            if (!Data.HasWeapon(weaponId) || (cls != null && !cls.CanEquip(weaponId))) return false;
            Data.weaponId = weaponId;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Buy a weapon play has put in the shop.</summary>
        public PurchaseResult BuyWeapon(WeaponSet w)
        {
            if (w == null) return PurchaseResult.Invalid;
            var r = Data.BuyWeapon(w.id, w.price);
            if (r == PurchaseResult.Bought) { SaveProgress(); Changed?.Invoke(); }
            return r;
        }

        /// <summary>Lose one level now (dying, or half your hearts). Saved at once, so quitting doesn't undo it.</summary>
        public LevelDown LoseLevel(string reason)
        {
            var rules = Db != null ? Db.Rules : ProgressionRules.Default;
            var d = rules.LoseLevel(Data, reason, Db != null ? Db.ClassSpecs() : null);
            SaveProgress();
            Changed?.Invoke();
            return d;
        }

        /// <summary>A run reached the goal: rank history, best rank, unlocks, experience and level-ups (a C fails).</summary>
        public RunOutcome RecordClear(string stage, StageResult result, PlaystyleLog log, IEnumerable<LevelDown> levelDowns)
        {
            var rules = Db != null ? Db.Rules : ProgressionRules.Default;
            var outcome = rules.Apply(Data, stage, result, log, Db != null ? Db.ClassSpecs() : null, levelDowns);
            var cls = Class;
            if (cls != null) foreach (var q in cls.startingSequences) Data.Learn(q);
            SaveProgress();
            Changed?.Invoke();
            return outcome;
        }

        // ---------------- Changes that the HUD shows ----------------

        public void AddNotes(int n) { Data.AddNotes(n); Changed?.Invoke(); }
        public int AddItem(string id, int n = 1) { int a = Data.AddItem(id, n); Changed?.Invoke(); return a; }
        public bool UseItem(string id) { bool ok = Data.UseItem(id); if (ok) Changed?.Invoke(); return ok; }
        public void CycleQuickItem() { Data.CycleQuickItem(); Changed?.Invoke(); }
        public bool Learn(string seq) { bool ok = Data.Learn(seq); if (ok) Changed?.Invoke(); return ok; }
        public bool AddLore(string id) { bool ok = Data.AddLore(id); if (ok) Changed?.Invoke(); return ok; }
        public PurchaseResult Buy(ItemDefinition item)
        {
            var r = Data.Buy(item.id, item.price, item.kind == ItemKind.Scroll ? item.teaches : null);
            if (r == PurchaseResult.Bought) Changed?.Invoke();
            return r;
        }
        public void NotifyChanged() => Changed?.Invoke();

        // ---------------- Save ----------------

        public bool Save(string spawn = null)
        {
            if (FreePlay) return false;
            Data.scene = SceneName;
            if (spawn != null) Data.spawn = spawn;
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"[Project Hymn] Could not save: {e.Message}"); return false; }
        }

        /// <summary>Write progress without moving the saved place (level-downs and clears in a stage).</summary>
        public bool SaveProgress()
        {
            if (FreePlay || !SlotUsed(Slot) && string.IsNullOrEmpty(Data.scene)) return false;
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"[Project Hymn] Could not save: {e.Message}"); return false; }
        }

        public bool Load()
        {
            try
            {
                MigrateLegacySave();
                if (!SlotUsed(Slot)) return false;
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (d == null) return false;
                d.Normalize();
                Data = d;
                Changed?.Invoke();
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"[Project Hymn] Could not load the save: {e.Message}"); return false; }
        }

        // ---------------- Travel ----------------

        public void Travel(string scene, string spawn, bool saveOnArrival = false)
        {
            if (Travelling) return;
            StartCoroutine(TravelRoutine(scene, spawn, saveOnArrival));
        }

        IEnumerator TravelRoutine(string scene, string spawn, bool saveOnArrival)
        {
            Travelling = true;
            PendingSpawn = spawn;
            _saveOnArrival = saveOnArrival;
            float startVolume = AudioListener.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                _fade.alpha = t;
                AudioListener.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }
            _fade.alpha = 1f;
            UiState.Reset();
            Time.timeScale = 1f;
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            if (_saveOnArrival) { Save(spawn); _saveOnArrival = false; }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                _fade.alpha = 1f - t;
                AudioListener.volume = Mathf.Lerp(0f, GameSettings.Volume, t);
                yield return null;
            }
            _fade.alpha = 0f;
            GameSettings.Apply();
            Travelling = false;
        }

        void BuildFade()
        {
            var canvasGo = new GameObject("Fade", typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            _fade = canvasGo.GetComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
            var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.GetComponent<Image>().color = Color.black;
            img.GetComponent<Image>().raycastTarget = false;
        }
    }
}
