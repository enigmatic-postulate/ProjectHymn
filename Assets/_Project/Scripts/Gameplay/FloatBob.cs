using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>A slow float: bobs up and down and turns a little (distant islets, the fountain star).</summary>
    public class FloatBob : MonoBehaviour
    {
        public float amplitude = 0.4f;
        public float period = 6f;
        public float phase;
        public Vector3 spin = Vector3.zero;

        Vector3 _rest;

        void Awake() => _rest = transform.localPosition;

        void Update()
        {
            float t = Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, period) + phase;
            transform.localPosition = _rest + Vector3.up * Mathf.Sin(t) * amplitude;
            if (spin != Vector3.zero) transform.Rotate(spin * Time.deltaTime, Space.Self);
        }
    }
}
