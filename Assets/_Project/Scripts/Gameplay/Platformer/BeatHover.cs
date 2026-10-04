using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Bobs an object up and down in time with the music (one swell every <see cref="beatsPerCycle"/> beats).</summary>
    public class BeatHover : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] float amplitude = 0.18f;
        [SerializeField] float beatsPerCycle = 4f;

        Vector3 _base;

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            _base = transform.position;
        }

        void Update()
        {
            double beat = conductor != null && conductor.IsPlaying && conductor.Map != null
                ? conductor.Map.TimeToBeat(conductor.SongTime) : Time.timeAsDouble * 130.0 / 60.0;
            float a = (float)(beat / Mathf.Max(0.5f, beatsPerCycle) * 2.0 * Mathf.PI);
            transform.position = _base + Vector3.up * Mathf.Sin(a) * amplitude;
        }
    }
}
