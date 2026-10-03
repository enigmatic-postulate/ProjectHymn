using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A low obstacle the player can only cross in the air (GDD M6: jumping has a use outside combat).
    /// Its collider blocks walking; <see cref="PlayerMotor"/> ignores it while high enough in a jump.
    /// Enemies are clamped to room bounds and are not affected.
    /// </summary>
    public class Hurdle : MonoBehaviour
    {
        static readonly List<Hurdle> _all = new List<Hurdle>();
        public static IReadOnlyList<Hurdle> All => _all;

        [SerializeField] Collider blocker;

        public Collider Collider => blocker;

        void Awake() { if (blocker == null) blocker = GetComponent<Collider>(); }
        void OnEnable() => _all.Add(this);
        void OnDisable() => _all.Remove(this);
    }
}
