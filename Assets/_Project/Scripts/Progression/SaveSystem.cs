using System;
using System.IO;
using UnityEngine;

namespace ProjectHymn.Progression
{
    /// <summary>
    /// Loads and saves <see cref="SaveData"/> as JSON in <c>{persistentDataPath}/save.json</c>.
    /// One save slot. <see cref="Current"/> is loaded lazily and shared by every scene.
    /// </summary>
    public static class SaveSystem
    {
        static SaveData _current;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");
        public static bool HasSave => File.Exists(FilePath);

        public static SaveData Current
        {
            get
            {
                if (_current == null) _current = LoadOrNew();
                return _current;
            }
        }

        public static ProgressionRules Rules => new ProgressionRules(Current);

        public static event Action Changed;

        public static SaveData LoadOrNew()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    if (data != null) return data;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not read save ({e.Message}); starting fresh.");
            }
            return SaveData.NewGame();
        }

        public static void NewGame()
        {
            var keepSettings = _current?.settings;
            _current = SaveData.NewGame();
            if (keepSettings != null) _current.settings = keepSettings;
            Save();
        }

        public static void Save()
        {
            if (_current == null) return;
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not write save: {e.Message}");
            }
            Changed?.Invoke();
        }
    }
}
