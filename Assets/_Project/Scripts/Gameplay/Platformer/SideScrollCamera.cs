using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Side-scrolling camera for vertical slice 1, in the spirit of Dragon Valor's side views but with a slow, PS2-era
    /// glide: looks along +Z at the lane from <see cref="distance"/> metres, leads the player in the direction they face,
    /// and only follows vertically when they land on a new height or leave a dead zone (so jumps don't bob the view).
    /// <see cref="CameraZone"/>s re-frame stretches of the level. Snaps after a respawn.
    /// </summary>
    public class SideScrollCamera : CameraRig
    {
        [SerializeField] Transform target;
        [Header("Default framing")]
        [SerializeField] float distance = 11f;
        [SerializeField] float height = 2.3f;
        [SerializeField] float lookHeight = 1.5f;
        [SerializeField] float lookAhead = 2.0f;
        [Header("Follow")]
        [SerializeField] float followX = 5f;
        [SerializeField] float followY = 3f;
        [SerializeField] float aheadSmoothing = 1.6f;
        [SerializeField] float zoneSmoothing = 1.2f;
        [Tooltip("While airborne the view only moves up/down once the player is this far above/below it.")]
        [SerializeField] Vector2 deadZone = new Vector2(1.6f, 0.8f);   // up, down

        PlatformerController _player;
        float _focusY, _ahead, _distance, _height, _lookHeight, _lookAhead, _bias;
        Vector3 _focus;

        void Awake()
        {
            FindTarget();
            _distance = distance; _height = height; _lookHeight = lookHeight; _lookAhead = lookAhead;
        }

        void OnEnable() { if (_player != null) _player.Respawned += Snap; }
        void OnDisable() { if (_player != null) _player.Respawned -= Snap; }

        void Start() => Snap();

        /// <summary>
        /// Follow the assigned target, or (when it is missing or switched off: each scene holds one player per class)
        /// whichever player is active.
        /// </summary>
        void FindTarget()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                var pc = FindAnyObjectByType<PlatformerController>();
                target = pc != null ? pc.transform : null;
            }
            _player = target != null ? target.GetComponent<PlatformerController>() : null;
        }

        public void Snap()
        {
            if (target == null) return;
            _focusY = target.position.y;
            _ahead = _player != null ? _player.Facing.x * _lookAhead : 0f;
            _focus = new Vector3(target.position.x + _ahead, _focusY, target.position.z);
            foreach (var z in CameraZone.Active)
                if (z.fixedFrame && z.Contains(target.position))
                {
                    var f = z.fixedFocus;
                    _focus = new Vector3(f.x + (target.position.x - f.x) * z.fixedFollow, f.y + (target.position.y - f.y) * z.fixedFollow + z.verticalBias, target.position.z);
                    _distance = z.distance; _height = z.height; _lookHeight = z.lookHeight; _lookAhead = z.lookAhead; _bias = z.verticalBias;
                    break;
                }
            Place();
        }

        void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (_player != null) _player.Respawned -= Snap;
                FindTarget();
                if (_player != null) { _player.Respawned += Snap; Snap(); }
            }
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            var p = target.position;

            // Zone framing
            CameraZone zone = null;
            foreach (var z in CameraZone.Active)
                if (z.Contains(p) && (zone == null || z.priority > zone.priority)) zone = z;
            float kz = 1f - Mathf.Exp(-zoneSmoothing * dt);
            _distance = Mathf.Lerp(_distance, zone != null ? zone.distance : distance, kz);
            _height = Mathf.Lerp(_height, zone != null ? zone.height : height, kz);
            _lookHeight = Mathf.Lerp(_lookHeight, zone != null ? zone.lookHeight : lookHeight, kz);
            _lookAhead = Mathf.Lerp(_lookAhead, zone != null ? zone.lookAhead : lookAhead, kz);
            _bias = Mathf.Lerp(_bias, zone != null ? zone.verticalBias : 0f, kz);

            // Vertical: follow the ground the player stands on; in the air only outside the dead zone.
            bool settled = _player == null || _player.Grounded || _player.State == LocoState.Hang || _player.State == LocoState.Climb;
            if (settled) _focusY = p.y;
            else if (p.y > _focusY + deadZone.x) _focusY = p.y - deadZone.x;
            else if (p.y < _focusY - deadZone.y) _focusY = p.y + deadZone.y;

            // Horizontal lead in the facing direction
            float face = _player != null ? _player.Facing.x : 0f;
            _ahead = Mathf.Lerp(_ahead, face * _lookAhead, 1f - Mathf.Exp(-aheadSmoothing * dt));

            if (zone != null && zone.fixedFrame)
            {
                // A brawler's fixed view: held on the zone's point, drifting only a little toward the player.
                var f = zone.fixedFocus;
                float k = 1f - Mathf.Exp(-2.5f * dt);
                _focus.x = Mathf.Lerp(_focus.x, f.x + (p.x - f.x) * zone.fixedFollow, k);
                _focus.y = Mathf.Lerp(_focus.y, f.y + (p.y - f.y) * zone.fixedFollow + _bias, k);
            }
            else
            {
                _focus.x = Mathf.Lerp(_focus.x, p.x + _ahead, 1f - Mathf.Exp(-followX * dt));
                _focus.y = Mathf.Lerp(_focus.y, _focusY + _bias, 1f - Mathf.Exp(-followY * dt));
            }
            _focus.z = p.z;
            Place();
            ApplyShake(dt);
        }

        void Place()
        {
            var eye = new Vector3(_focus.x, _focus.y + _height, _focus.z - _distance);
            var look = new Vector3(_focus.x, _focus.y + _lookHeight, _focus.z);
            transform.position = eye;
            transform.rotation = Quaternion.LookRotation(look - eye, Vector3.up);
        }
    }
}
