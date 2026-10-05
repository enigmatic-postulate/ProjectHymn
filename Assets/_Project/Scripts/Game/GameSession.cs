using System;
using System.Collections;
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
        const string SaveFile = "hymn_save.json";
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

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFile);
        public static bool HasSave => File.Exists(SavePath);
        public ClassDefinition Class => Db != null ? Db.Class(Data.classId) : null;
        public WeaponSet Weapon => Db != null ? Db.Weapon(Data.weaponId) : null;
        public string SceneName => SceneManager.GetActiveScene().name;

        static void Create(bool freePlay)
        {
            var go = new GameObject("Game Session");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameSession>();
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
            return s;
        }

        void Update()
        {
            if (Data != null) Data.playSeconds += Time.unscaledDeltaTime;
        }

        // ---------------- Game flow ----------------

        /// <summary>A new game: everyone starts as the Minstrel with the sword, at level 1.</summary>
        public void NewGame()
        {
            var cls = Db != null ? Db.Class(ProgressionRules.Minstrel) : null;
            Data = SaveData.NewGame(ProgressionRules.Minstrel, cls != null ? cls.startingSequences : null);
            Data.weaponId = ProgressionRules.Sword;
            if (Db != null) foreach (var it in Db.startingItems) Data.AddItem(it.id, it.count);
            FreePlay = false;
            Travel(Db != null ? Db.hubScene : "Village", "Start", saveOnArrival: true);
        }

        public bool Continue()
        {
            if (!Load()) return false;
            FreePlay = false;
            string scene = string.IsNullOrEmpty(Data.scene) ? (Db != null ? Db.hubScene : "Village") : Data.scene;
            Travel(scene, string.IsNullOrEmpty(Data.spawn) ? "Start" : Data.spawn);
            return true;
        }

        public void ToTitle() => Travel(Db != null ? Db.titleScene : "Title", "");

        /// <summary>Change class (a class check's choice); keeps a weapon the class can hold.</summary>
        public void SetClass(string classId)
        {
            ProgressionRules.SetClass(Data, classId, Db != null ? Db.ClassSpecs() : null);
            var cls = Class;
            if (cls != null) foreach (var q in cls.startingSequences) Data.Learn(q);
            Changed?.Invoke();
        }

        /// <summary>Equip an unlocked weapon the class can hold. Takes effect when the scene next loads.</summary>
        public bool SetWeapon(string weaponId)
        {
            var cls = Class;
            if (!Data.HasWeapon(weaponId) || (cls != null && !cls.CanEquip(weaponId))) return false;
            Data.weaponId = weaponId;
            Changed?.Invoke();
            return true;
        }

        /// <summary>A stage clear: best rank, the greatsword unlock, experience, level-downs and the class check.</summary>
        public RunOutcome RecordClear(string stage, StageResult result, PlaystyleLog log)
        {
            var rules = Db != null ? Db.Rules : ProgressionRules.Default;
            var outcome = rules.Apply(Data, stage, result, log, Db != null ? Db.ClassSpecs() : null);
            var cls = Class;
            if (cls != null) foreach (var q in cls.startingSequences) Data.Learn(q);
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

        public bool Load()
        {
            try
            {
                if (!HasSave) return false;
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
