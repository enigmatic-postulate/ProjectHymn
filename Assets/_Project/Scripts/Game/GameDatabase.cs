using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Everything the menus and the save need to look up by id. Lives in Resources so any scene finds it.</summary>
    public class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "HymnDatabase";

        public List<ClassDefinition> classes = new List<ClassDefinition>();
        public List<ItemDefinition> items = new List<ItemDefinition>();
        [Tooltip("What the village shop sells, in order.")]
        public List<ItemDefinition> shopStock = new List<ItemDefinition>();
        public List<ItemCount> startingItems = new List<ItemCount>();
        public string hubScene = "Village";
        public string titleScene = "Title";

        [System.Serializable]
        public class ItemCount { public string id; public int count = 1; }

        public ClassDefinition Class(string id)
        {
            foreach (var c in classes) if (c != null && c.id == id) return c;
            return classes.Count > 0 ? classes[0] : null;
        }

        public ItemDefinition Item(string id)
        {
            foreach (var i in items) if (i != null && i.id == id) return i;
            return null;
        }

        static GameDatabase _cached;
        public static GameDatabase Load()
        {
            if (_cached == null) _cached = Resources.Load<GameDatabase>(ResourcePath);
            return _cached;
        }
    }
}
