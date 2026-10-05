using System.Collections.Generic;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Everything the menus and the save need to look up by id. Lives in Resources so any scene finds it.</summary>
    public class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "HymnDatabase";

        public List<ClassDefinition> classes = new List<ClassDefinition>();
        [Tooltip("Every weapon, sword first (each has its own moveset; classes list the ones they equip).")]
        public List<WeaponSet> weapons = new List<WeaponSet>();
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

        public WeaponSet Weapon(string id)
        {
            foreach (var w in weapons) if (w != null && w.id == id) return w;
            return weapons.Count > 0 ? weapons[0] : null;
        }

        /// <summary>The classes as the progression rules see them.</summary>
        public List<ClassSpec> ClassSpecs()
        {
            var list = new List<ClassSpec>();
            foreach (var c in classes) if (c != null) list.Add(c.ToSpec());
            return list;
        }

        ProgressionRules _rules;
        /// <summary>Rank &amp; Class rules (placeholder numbers, see ProgressionRules).</summary>
        public ProgressionRules Rules => _rules ??= ProgressionRules.Default;

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
