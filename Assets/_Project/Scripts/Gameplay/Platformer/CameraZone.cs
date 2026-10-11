using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A stretch of the lane where the side-scroll camera frames differently (pull back over a climb, look further
    /// ahead over a gap). The zone is a box in the lane plane centred on this transform; the highest priority zone
    /// containing the player wins and the camera eases into it.
    /// </summary>
    public class CameraZone : MonoBehaviour
    {
        public static readonly List<CameraZone> Active = new List<CameraZone>();

        [Tooltip("Width and height of the zone in the lane plane (metres).")]
        public Vector2 size = new Vector2(20f, 30f);
        public int priority;
        [Header("Framing")]
        public float distance = 13f;
        public float height = 2.6f;
        public float lookHeight = 1.8f;
        public float lookAhead = 2.5f;
        [Tooltip("Shift the framing up (positive) to show the cliffs above.")]
        public float verticalBias;
        [Header("Fixed framing (the brawl stage)")]
        [Tooltip("Hold the view on a point instead of following the player (a brawler's fixed camera).")]
        public bool fixedFrame;
        [Tooltip("The point the fixed view looks at (lane plane, world X and Y).")]
        public Vector2 fixedFocus;
        [Tooltip("How much the fixed view still drifts toward the player (0 = locked).")]
        [Range(0f, 0.5f)] public float fixedFollow = 0.12f;

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() => Active.Remove(this);

        public bool Contains(Vector3 p)
        {
            var c = transform.position;
            return Mathf.Abs(p.x - c.x) <= size.x * 0.5f && Mathf.Abs(p.y - c.y) <= size.y * 0.5f;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0.1f));
        }
    }
}
