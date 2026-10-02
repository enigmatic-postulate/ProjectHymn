using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Stem layering by Groove tier (GDD). With stems (drums, bass, harmony, lead): tier 0 plays drums and
    /// bass, each tier above adds the next stem, and Groove x3 brings in the lead. With a single mixed track:
    /// the music opens up from muffled to full brightness as Groove rises, so playing well still sounds bigger.
    /// </summary>
    public class MusicMixer : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] BeatCombatController combat;
        [Tooltip("Seconds to fade a stem in or out.")]
        [SerializeField] float fadeSeconds = 0.6f;
        [Tooltip("Single-track mode: low-pass cutoff (Hz) per Groove tier 0..3.")]
        [SerializeField] float[] cutoffByTier = { 1800f, 4500f, 11000f, 22000f };
        [Tooltip("Stems that are always on at tier 0 (drums, bass).")]
        [SerializeField, Min(1)] int baseStems = 2;

        float[] _vol = new float[0];
        float _cutoff = 22000f;

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            if (combat == null) combat = FindAnyObjectByType<BeatCombatController>();
        }

        void Update()
        {
            if (conductor == null || !conductor.IsPlaying) return;
            int tier = combat != null ? combat.Groove.Tier : 3; // no combat (town): full mix
            int layers = conductor.LayerCount;
            if (_vol.Length != layers) _vol = new float[layers];
            float step = fadeSeconds <= 0 ? 1f : Time.unscaledDeltaTime / fadeSeconds;

            if (conductor.HasStems && layers > 1)
            {
                int audible = Mathf.Min(layers, baseStems + tier);
                for (int i = 0; i < layers; i++)
                {
                    _vol[i] = Mathf.MoveTowards(_vol[i], i < audible ? 1f : 0f, step);
                    conductor.SetLayerVolume(i, _vol[i]);
                }
                conductor.SetLowPass(22000f);
            }
            else
            {
                float target = cutoffByTier[Mathf.Clamp(tier, 0, cutoffByTier.Length - 1)];
                // Move in log space so the sweep sounds even.
                float lc = Mathf.Log(_cutoff), lt = Mathf.Log(target);
                _cutoff = Mathf.Exp(Mathf.MoveTowards(lc, lt, step * 2f));
                conductor.SetLowPass(_cutoff);
                for (int i = 0; i < layers; i++) conductor.SetLayerVolume(i, 1f);
            }
        }
    }
}
