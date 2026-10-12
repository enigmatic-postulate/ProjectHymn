using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A light that swells on every beat (braziers, the belfry bell, village lanterns): reads the <c>_HymnBeat</c> pulse
    /// the AmbientBeat component sets, so it needs one in the scene.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class BeatLight : MonoBehaviour
    {
        static readonly int BeatId = Shader.PropertyToID("_HymnBeat");
        [Tooltip("Extra intensity on the beat, as a share of the light's own.")]
        public float pulse = 0.45f;
        [Tooltip("A slow flicker on top (fire), as a share.")]
        public float flicker = 0.08f;

        Light _light;
        float _base, _seed;

        void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _seed = Random.value * 10f;
        }

        void Update()
        {
            float beat = Shader.GetGlobalFloat(BeatId);
            float f = 1f + (Mathf.PerlinNoise(Time.time * 3f, _seed) - 0.5f) * 2f * flicker;
            _light.intensity = _base * (1f + beat * pulse) * f;
        }
    }
}
