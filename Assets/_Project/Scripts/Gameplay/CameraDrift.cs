using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>A slow, looping sway of a camera around where it stands (the title screen).</summary>
    public class CameraDrift : MonoBehaviour
    {
        public Vector3 amplitude = new Vector3(0.3f, 0.1f, 0.2f);
        public float period = 12f;

        Vector3 _rest;
        Quaternion _look;

        void Awake() { _rest = transform.position; _look = transform.rotation; }

        void Update()
        {
            float t = Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(1f, period);
            transform.position = _rest + new Vector3(Mathf.Sin(t) * amplitude.x, Mathf.Sin(t * 0.7f + 1f) * amplitude.y, Mathf.Sin(t * 0.5f + 2f) * amplitude.z);
            transform.rotation = _look * Quaternion.Euler(Mathf.Sin(t * 0.6f) * 0.6f, Mathf.Sin(t * 0.8f + 0.5f) * 1.2f, 0f);
        }
    }
}
