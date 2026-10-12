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
}
