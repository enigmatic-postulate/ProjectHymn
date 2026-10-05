using System.Collections.Generic;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A class (Rank &amp; Class slice): earned from how you play, not picked. Its bracket and lean feed the class checks,
    /// its stats and perks change how a fight goes, and it lists the weapons it can equip (weapons are separate and
    /// unlocked on their own).
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Class", fileName = "Class")]
    public class ClassDefinition : ScriptableObject
    {
        public string id = ProgressionRules.Minstrel;
        public string displayName = "Minstrel";
        [TextArea] public string description = "";
        public ClassBracket bracket = ClassBracket.Base;
        [Tooltip("A level-down below this level costs the class.")]
        public int minLevel;
        [Tooltip("The play style that earns this class at a class check.")]
        public PlayLean lean = PlayLean.Balanced;
        public int hearts = 5;
        public float damage = 1f;
        [Tooltip("Weapons this class can equip, in order of preference.")]
        public List<WeaponSet> weapons = new List<WeaponSet>();
        [Tooltip("Sequence keys known from the start.")]
        public List<string> startingSequences = new List<string> { "CrossingCuts" };

        [Header("Perks")]
        [Tooltip("Extra block window either side, in seconds (the Fighter's wider guard). Parry timing never changes.")]
        public float blockBonus;
        [Tooltip("Damage dealt to the attacker by a block (Fighter).")]
        public float blockDamage;
        [Tooltip("Damage dealt to the attacker by a parry (Fighter, Warden).")]
        public float parryDamage;
        [Tooltip("A block makes the enemy skip its next note (Warden).")]
        public bool blockSkipsNote;
        [Tooltip("Seconds of invulnerability a dodge gives (Crusader: longer).")]
        public float dodgeInvulnerable = 0.25f;
        [Tooltip("Sequences in common time (4/4) only (Fighter). Every slice stage is 4/4 so far.")]
        public bool commonTimeOnly;

        public WeaponSet DefaultWeapon => weapons.Count > 0 ? weapons[0] : null;

        public bool CanEquip(string weaponId)
        {
            foreach (var w in weapons) if (w != null && w.id == weaponId) return true;
            return false;
        }

        public ClassSpec ToSpec()
        {
            var ids = new List<string>();
            foreach (var w in weapons) if (w != null) ids.Add(w.id);
            return new ClassSpec(id, displayName, bracket, minLevel, lean, ids.ToArray());
        }

        /// <summary>The perks in a line, for menus.</summary>
        public string PerksText()
        {
            var parts = new List<string>();
            if (blockBonus > 0f) parts.Add($"wider block window (+{blockBonus * 1000f:0} ms)");
            if (blockDamage > 0f) parts.Add($"blocks deal {blockDamage:0} damage");
            if (parryDamage > 0f) parts.Add($"parries deal {parryDamage:0} damage");
            if (blockSkipsNote) parts.Add("a block makes the enemy skip a note");
            if (dodgeInvulnerable > 0.26f) parts.Add($"longer dodge frames ({dodgeInvulnerable * 1000f:0} ms)");
            if (commonTimeOnly) parts.Add("common time (4/4) only");
            return parts.Count > 0 ? string.Join("; ", parts) : "no perks: the base class";
        }
    }
}
