using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Feeds the music's beat to the Hymn shaders: <c>_HymnBeat</c> is 1 on each beat and falls away before the next,
    /// so glowing leaves, crystals and spire bands breathe with the song (Starlit Spring). In the village, with no Conductor,
    /// it follows the waltz (<see cref="LayeredMusic"/>).
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
            else if (LayeredMusic.Active != null && LayeredMusic.Active.Beat >= 0.0) v = BeatClock.Pulse(sharpness);   // the village waltz
            Shader.SetGlobalFloat(BeatId, v);
        }

        void OnDisable() => Shader.SetGlobalFloat(BeatId, 0f);
    }
}
