using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Feeds the music's beat to the Hymn shaders: <c>_HymnBeat</c> is 1 on each beat and falls away before the next,
    /// so glowing leaves, crystals and spire bands breathe with the song (Starlit Spring).
    /// </summary>
    public class AmbientBeat : MonoBehaviour
    {
        static readonly int BeatId = Shader.PropertyToID("_HymnBeat");
        [SerializeField] Conductor conductor;
        [Tooltip("How fast the glow falls after each beat.")]
        [SerializeField] float sharpness = 3f;

        void Awake() { if (conductor == null) conductor = FindAnyObjectByType<Conductor>(); }

        void Update()
        {
            float v = 0f;
            if (conductor != null && conductor.IsPlaying) v = Mathf.Pow(1f - Mathf.Clamp01(conductor.BeatPhase), sharpness);
            Shader.SetGlobalFloat(BeatId, v);
        }

        void OnDisable() => Shader.SetGlobalFloat(BeatId, 0f);
    }

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

    /// <summary>A steady turn about a local axis (the spires' orbiting rings).</summary>
    public class Spin : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 30f, 0f);

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
