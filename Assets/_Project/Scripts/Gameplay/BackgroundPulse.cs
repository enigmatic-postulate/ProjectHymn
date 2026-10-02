using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Environment pulse (GDD: timing comes from the music, idle bob and environment, not a beat ring).
    /// Brightens the camera background on each beat, more on downbeats.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BackgroundPulse : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] Color pulseColor = new Color(0.22f, 0.14f, 0.32f);
        [SerializeField, Range(0f, 1f)] float beatStrength = 0.35f;
        [SerializeField, Range(0f, 1f)] float downbeatStrength = 0.8f;
        [SerializeField, Range(1f, 30f)] float sharpness = 7f;

        Camera _cam;
        Color _base;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _base = _cam.backgroundColor;
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
        }

        void Update()
        {
            if (conductor == null || !conductor.IsPlaying) return;
            var b = conductor.CurrentBeat;
            float strength = b.IsDownbeat ? downbeatStrength : beatStrength;
            float k = strength * Mathf.Exp(-conductor.BeatPhase * sharpness);
            _cam.backgroundColor = Color.Lerp(_base, pulseColor, k);
        }

        void OnDisable() { if (_cam != null) _cam.backgroundColor = _base; }
    }
}
