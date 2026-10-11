using System;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>The end of the vertical slice route. Raises <see cref="Reached"/> the first time the player touches it.</summary>
    public class SliceGoal : MonoBehaviour
    {
        [SerializeField] Transform spin;
        [SerializeField] float spinSpeed = 60f;
        [Tooltip("The goal stays sealed until this enemy falls (a boss stage). Empty = always open.")]
        [SerializeField] BeatEnemy requires;
        [Tooltip("The goal stays sealed until these waves are cleared (the Arena). Empty = no waves.")]
        [SerializeField] ArenaWaves waves;

        public bool Done { get; private set; }
        public bool Open => (requires == null || requires.IsDead) && (waves == null || waves.Done);
        public event Action Reached;
        float _nagAt = -9f;
        Vector3 _spinScale;

        void Awake() { if (spin != null) _spinScale = spin.localScale; }

        public void Reach(PlatformerController player)
        {
            if (Done) return;
            if (!Open)
            {
                if (Time.time - _nagAt > 2.5f)
                {
                    _nagAt = Time.time;
                    ProjectHymn.UI.GameHud.Toast(requires != null && !requires.IsDead ? $"Sealed until the {requires.DisplayName} falls." : "Sealed until every wave falls.");
                }
                return;
            }
            Done = true;
            Reached?.Invoke();
        }

        void Update()
        {
            if (spin == null) return;
            spin.localScale = Vector3.Lerp(spin.localScale, Open ? _spinScale : _spinScale * 0.15f, 1f - Mathf.Exp(-4f * Time.deltaTime));
            spin.Rotate(0f, spinSpeed * (Done ? 4f : 1f) * Time.deltaTime, 0f, Space.World);
        }
    }
}
