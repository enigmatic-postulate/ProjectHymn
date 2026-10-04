using System;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>The end of the vertical slice route. Raises <see cref="Reached"/> the first time the player touches it.</summary>
    public class SliceGoal : MonoBehaviour
    {
        [SerializeField] Transform spin;
        [SerializeField] float spinSpeed = 60f;

        public bool Done { get; private set; }
        public event Action Reached;

        public void Reach(PlatformerController player)
        {
            if (Done) return;
            Done = true;
            Reached?.Invoke();
        }

        void Update()
        {
            if (spin != null) spin.Rotate(0f, spinSpeed * (Done ? 4f : 1f) * Time.deltaTime, 0f, Space.World);
        }
    }
}
