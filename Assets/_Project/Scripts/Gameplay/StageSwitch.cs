using System;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A bell plate on the floor (GDD M6 "one simple switch puzzle"): stepping on it rings it and keeps it down.
    /// A stage room can require it before its door opens.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StageSwitch : MonoBehaviour
    {
        [SerializeField] Transform plate;
        [SerializeField] float pressDepth = 0.12f;
        [SerializeField] CombatFeedback feedback;

        ToonTint _tint;

        public bool IsPressed { get; private set; }
        public event Action<StageSwitch> Pressed;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            _tint = GetComponent<ToonTint>();
            if (feedback == null) feedback = FindAnyObjectByType<CombatFeedback>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsPressed || other.GetComponentInParent<PlayerMotor>() == null) return;
            IsPressed = true;
            if (plate != null) plate.localPosition += Vector3.down * pressDepth;
            if (_tint != null) _tint.SetGlow(new Color(1f, 0.8f, 0.3f) * 2f);
            if (feedback != null) feedback.PlayParry();
            Pressed?.Invoke(this);
        }
    }
}
