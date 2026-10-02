using System.Collections.Generic;
using System.Text;
using ProjectHymn.Combat;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    public enum TownStation { Shop, Loadout, ArenaGate, Lore }

    /// <summary>A spot in town the player can use: a shop counter, the wardrobe, an arena gate, a hidden lore scroll.</summary>
    public class TownInteractable : MonoBehaviour
    {
        public TownStation station;
        public string title = "Station";
        [Min(0.2f)] public float radius = 1.3f;

        [Header("Arena gate")]
        public string sceneName;
        public string arenaId;
        [Tooltip("Arena ids that must be cleared before this gate opens.")]
        public string[] requiresCleared = new string[0];

        [Header("Lore")]
        public string loreId;
        [TextArea] public string loreText;
        public int loreNotes = 100;

        public bool IsLocked(out string reason)
        {
            reason = null;
            if (station != TownStation.ArenaGate) return false;
            var rules = SaveSystem.Rules;
            foreach (var id in requiresCleared)
            {
                if (rules.IsCleared(id)) continue;
                reason = "Clear the other arenas first.";
                return true;
            }
            return false;
        }

        public bool Hidden => station == TownStation.Lore && SaveSystem.Current.loreFound.Contains(loreId);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
