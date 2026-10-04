using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    public enum ItemKind { Potion, Tonic, Scroll, Lore }

    /// <summary>An item the player can carry, buy or find (Hub &amp; Loop).</summary>
    [CreateAssetMenu(menuName = "Project Hymn/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string id = "potion";
        public string displayName = "Potion";
        [TextArea] public string description = "";
        public ItemKind kind;
        public int price = 60;
        [Tooltip("Potions: hearts restored.")]
        public int heal = 2;
        [Tooltip("Tonics: damage multiplier and how many beats it lasts.")]
        public float damageBoost = 1.25f;
        public int boostBeats = 16;
        [Tooltip("Scrolls: the sequence key they teach (e.g. Tempest).")]
        public string teaches = "";
        [Tooltip("Lore: the text read when found.")]
        [TextArea(3, 8)] public string loreText = "";
        public Color color = Color.white;
    }
}
