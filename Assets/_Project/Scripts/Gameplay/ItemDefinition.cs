using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    public enum ItemKind { Weapon, Armor }

    /// <summary>A weapon (Instrument) or armor the player can buy and equip in town.</summary>
    [CreateAssetMenu(menuName = "Project Hymn/Item", fileName = "NewItem")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName = "Item";
        [TextArea] public string description;
        public ItemKind kind;
        [Min(0)] public int price;

        [Header("Weapon")]
        public WeaponType weaponType;

        [Header("Armor")]
        public float bonusHp;
        public float bonusStamina;
        [Tooltip("1 = normal stamina regeneration.")]
        public float staminaRegenMultiplier = 1f;
        [Tooltip("1 = normal move speed.")]
        public float moveSpeedMultiplier = 1f;

        [Header("Look")]
        [Tooltip("Weapon: the model held in hand (grip at origin, blade along +Y). Armor: the whole player model while wearing it (faces +Z, feet at y = 0).")]
        public GameObject model;
        [Tooltip("Shop icon colour.")]
        public Color color = Color.white;

        /// <summary>Save-file id: the asset's name.</summary>
        public string Id => name;
    }
}
