using System;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Swings a bell like a pendulum that takes one bar to cross, so it reaches the top of its swing (and rings) on
    /// every downbeat: in 3/4 it counts the waltz for you. Belfry Ridge (VS-2).
    /// </summary>
    public class BellSwing : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] float amplitude = 28f;
        [Tooltip("Swing axis in local space.")]
        [SerializeField] Vector3 axis = Vector3.forward;

        Quaternion _base;

        void Awake()
        {
            _base = transform.localRotation;
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
        }

        void Update()
        {
            if (conductor == null || !conductor.IsPlaying || conductor.Map == null) return;
            double beat = conductor.Map.TimeToBeat(conductor.SongTime);
            var info = conductor.Map.GetBeat((long)Math.Floor(beat));
            int perBar = Mathf.Max(1, info.BeatsPerMeasure);
            double bars = info.Measure + (info.BeatInMeasure + (beat - Math.Floor(beat))) / perBar;
            float angle = amplitude * Mathf.Cos((float)(Math.PI * bars));
            transform.localRotation = _base * Quaternion.AngleAxis(angle, axis);
        }
    }
}
