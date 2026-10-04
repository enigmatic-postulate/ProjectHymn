using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A weapon and its fighting style: the stance (idle pose), the rhythm sequences it knows, and the moves a lone
    /// note plays when it isn't part of a sequence. The saber fights one-handed from a side-on stance; the broadsword
    /// two-handed from a square guard (left hand kept on the hilt by IK).
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Weapon Set", fileName = "NewWeaponSet")]
    public class WeaponSet : ScriptableObject
    {
        public string displayName = "Saber";
        [Tooltip("Name of the sword model under the hand's Grip to show for this weapon.")]
        public string swordObject = "Saber";
        public bool twoHanded;
        public MoveKey stance = new MoveKey();
        public List<SwordSequence> sequences = new List<SwordSequence>();
        [Tooltip("Light notes outside a sequence alternate between these two cuts.")]
        public SwordMove lightA, lightB;
        [Tooltip("A heavy note outside a sequence.")]
        public SwordMove heavy;
        [Tooltip("Played when the player guards (keys in beats; 0 = the guard is up).")]
        public SwordMove guard;
        [Header("Clip rig (art pass)")]
        [Tooltip("Looping idle stance clip (replaces the stance keyframe when set).")]
        public BakedClip idleClip;
        [Tooltip("Name of the object under the off-hand slot to show with this weapon (a shield), or empty.")]
        public string offHandObject = "";
        [Tooltip("Damage scale for this weapon.")]
        public float damageScale = 1f;
    }
}
