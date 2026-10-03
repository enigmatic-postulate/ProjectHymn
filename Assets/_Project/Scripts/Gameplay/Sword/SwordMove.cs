using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>One bone's rotation in a keyframe (Euler degrees, relative to the bind pose).</summary>
    [Serializable]
    public struct BoneRotation
    {
        public string bone;
        public Vector3 euler;
        public BoneRotation(string bone, Vector3 euler) { this.bone = bone; this.euler = euler; }
    }

    /// <summary>
    /// A keyframe. <see cref="beat"/> is measured in beats from the hit: 0 is the moment the blade connects, which is
    /// played exactly on the musical beat; -0.2 is a fifth of a beat before it. Bones not listed hold the stance.
    /// </summary>
    [Serializable]
    public class MoveKey
    {
        public float beat;
        [Tooltip("Body offset added to the stance (metres; z = forward). Lunges and crouches.")]
        public Vector3 rootOffset;
        public List<BoneRotation> bones = new List<BoneRotation>();

        public bool TryGet(string bone, out Vector3 euler)
        {
            foreach (var b in bones) if (b.bone == bone) { euler = b.euler; return true; }
            euler = default;
            return false;
        }
    }

    /// <summary>
    /// One sword attack as keyframes timed in beats (GDD M2 prototype). Because times are in beats, the same move plays
    /// faster on a faster song and its hit always lands on the beat. Edit the keys here to tune a cut; the
    /// <see cref="RhythmAnimator"/> also scales how far the keys swing with tempo.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Hymn/Sword Move", fileName = "NewSwordMove")]
    public class SwordMove : ScriptableObject
    {
        public string displayName = "Cut";
        public List<MoveKey> keys = new List<MoveKey>();

        [Header("Hit")]
        [Tooltip("Sword trail and hit window, in beats around the hit.")]
        public float activeStart = -0.08f;
        public float activeEnd = 0.14f;
        public float damage = 10f;
        [Tooltip("Where the hit lands in front of the player, and how wide (metres).")]
        public float reach = 1.25f;
        public float radius = 0.9f;
        public string effect = "cut";

        [Header("Look")]
        public Color trailColor = new Color(1f, 0.88f, 0.55f);
        [Tooltip("Two-handed weapons: release the left hand for this move (the punch).")]
        public bool offHandFree;

        public float FirstBeat => keys.Count > 0 ? keys[0].beat : 0f;
        public float LastBeat => keys.Count > 0 ? keys[keys.Count - 1].beat : 0f;

        /// <summary>Keep keys in time order (called by the editor tools and OnValidate).</summary>
        public void SortKeys() => keys.Sort((a, b) => a.beat.CompareTo(b.beat));

        void OnValidate() => SortKeys();
    }
}
