using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Scales a transform on every beat (bigger on downbeats). Drop on anything that should groove.</summary>
    public class BeatPulse : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] float beatPunch = 0.08f;
        [SerializeField] float downbeatPunch = 0.18f;
        [SerializeField, Range(1f, 30f)] float sharpness = 8f;

        Vector3 _base;
        float _amount;

        void Awake()
        {
            _base = transform.localScale;
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
        }

        void Update()
        {
            if (conductor == null || !conductor.IsPlaying) return;
            var b = conductor.CurrentBeat;
            float punch = b.IsDownbeat ? downbeatPunch : beatPunch;
            _amount = punch * Mathf.Exp(-conductor.BeatPhase * sharpness);
            transform.localScale = _base * (1f + _amount);
        }
    }
}
