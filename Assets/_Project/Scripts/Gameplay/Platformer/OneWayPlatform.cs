using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A platform you jump up through and land on, and drop down through with Down + Jump (the brawl stage, Starlit
    /// Spring). Its collider is solid only while the player's feet are at or above its top and they aren't dropping
    /// through; below it, or rising into it, it isn't there. Enemies don't use physics, so this is about the player only.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DefaultExecutionOrder(-20)]        // before the player moves (PlatformerController runs at -10)
    public class OneWayPlatform : MonoBehaviour
    {
        [Tooltip("World height of the walking surface (filled from the collider when 0).")]
        [SerializeField] float top;
        [Tooltip("Feet may sink this far below the top and still count as on it.")]
        [SerializeField] float tolerance = 0.03f;

        Collider _col;
        PlatformerController _player;

        public float Top => top;

        void Awake()
        {
            _col = GetComponent<Collider>();
            if (Mathf.Approximately(top, 0f)) top = _col.bounds.max.y;
        }

        void Update()
        {
            if (_player == null || !_player.isActiveAndEnabled) _player = FindAnyObjectByType<PlatformerController>();
            bool solid = _player != null && !_player.DroppingThrough && _player.transform.position.y >= top - tolerance;
            if (_col.enabled != solid) _col.enabled = solid;
        }
    }
}
