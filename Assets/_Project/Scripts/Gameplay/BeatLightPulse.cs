using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Stage lighting on the beat (GDD: timing comes from the music and the environment, not a beat ring).
    /// Pulses the key light's intensity and the glow of stage props (staff lines, lamps) on every beat,
    /// harder on downbeats.
    /// </summary>
    public class BeatLightPulse : MonoBehaviour
    {
        static readonly int GlowId = Shader.PropertyToID("_GlowColor");

        [SerializeField] Conductor conductor;
        [SerializeField] Light keyLight;
        [SerializeField] float lightBoost = 0.25f;
        [Tooltip("Props whose glow pulses (any material using the toon shader).")]
        [SerializeField] Renderer[] glowRenderers = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] Color glowColor = new Color(1f, 0.8f, 0.3f) * 1.5f;
        [SerializeField, Range(0f, 1f)] float restingGlow = 0.25f;
        [SerializeField, Range(1f, 30f)] float sharpness = 6f;

        float _baseIntensity;
        MaterialPropertyBlock _block;

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (keyLight != null) _baseIntensity = keyLight.intensity;
            _block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (conductor == null || !conductor.IsPlaying || conductor.IsPaused) return;
            var b = conductor.CurrentBeat;
            float k = (b.IsDownbeat ? 1f : 0.5f) * Mathf.Exp(-conductor.BeatPhase * sharpness);

            if (keyLight != null) keyLight.intensity = _baseIntensity * (1f + lightBoost * k);

            var c = glowColor * Mathf.Lerp(restingGlow, 1f, k);
            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(GlowId, c);
                r.SetPropertyBlock(_block);
            }
        }

        void OnDisable() { if (keyLight != null && _baseIntensity > 0f) keyLight.intensity = _baseIntensity; }
    }
}
