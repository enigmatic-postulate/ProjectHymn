using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>A playable class: the weapon is the class (Knight: sword and shield; Barbarian: greatsword).</summary>
    [CreateAssetMenu(menuName = "Project Hymn/Class", fileName = "Class")]
    public class ClassDefinition : ScriptableObject
    {
        public string id = "knight";
        public string displayName = "Knight";
        [TextArea] public string description = "";
        public int hearts = 5;
        public float damage = 1f;
        public WeaponSet weapon;
        [Tooltip("Sequence keys known from the start.")]
        public List<string> startingSequences = new List<string> { "CrossingCuts" };
    }
}
