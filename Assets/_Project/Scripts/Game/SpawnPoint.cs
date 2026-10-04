using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>A named place a scene can be entered at ("Start", "FromCliffside", "Inn", ...).</summary>
    public class SpawnPoint : MonoBehaviour
    {
        public string id = "Start";

        public static SpawnPoint Find(string id)
        {
            SpawnPoint first = null;
            foreach (var s in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            {
                if (first == null || s.id == "Start") first = first == null || s.id == "Start" ? s : first;
                if (s.id == id) return s;
            }
            return first;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
        }
    }
}
