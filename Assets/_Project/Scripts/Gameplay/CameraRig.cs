using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Angled top-down camera (GDD M5): a pivot that follows the target on the ground plane, with the camera
    /// child pitched down and pulled back. A narrow lens keeps the stylized, nearly-isometric look.
    /// Arenas use a fixed pivot (no target) that frames the whole floor. Camera shake is added on top.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Camera cam;
        [Range(20f, 85f)] [SerializeField] float pitch = 52f;
        [SerializeField] float yaw = 0f;
        [SerializeField] float distance = 16f;
        [Range(10f, 70f)] [SerializeField] float fieldOfView = 30f;
        [Tooltip("How quickly the pivot catches up with the target.")]
        [SerializeField] float smoothing = 7f;
        [Tooltip("Pivot stays inside this area of the ground plane (x = world X, y = world Z). Zero size = no limit.")]
        [SerializeField] Rect bounds;

        Vector3 _shake;
        float _shakeTime, _shakeDuration, _shakeAmount;

        public Transform Target { get => target; set => target = value; }
        public Camera Camera => cam;

        void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (target != null) transform.position = Clamp(Ground.ToWorld(Ground.ToPlane(target.position)));
            Place();
        }

        void OnValidate()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            Place();
        }

        void LateUpdate()
        {
            if (target != null)
            {
                var goal = Clamp(Ground.ToWorld(Ground.ToPlane(target.position)));
                transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
            }

            _shake = Vector3.zero;
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_shakeTime / Mathf.Max(0.0001f, _shakeDuration));
                var r = Random.insideUnitCircle * _shakeAmount * k;
                _shake = new Vector3(r.x, r.y, 0f);
            }
            Place();
        }

        /// <summary>Screen shake in world units, fading over <paramref name="duration"/> seconds.</summary>
        public void Shake(float amount, float duration)
        {
            _shakeAmount = Mathf.Max(_shakeTime > 0f ? _shakeAmount : 0f, amount);
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        void Place()
        {
            if (cam == null) return;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.localPosition = rot * new Vector3(0f, 0f, -distance) + rot * _shake;
            cam.transform.localRotation = rot;
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
        }

        Vector3 Clamp(Vector3 p)
        {
            if (bounds.width <= 0f || bounds.height <= 0f) return p;
            p.x = Mathf.Clamp(p.x, bounds.xMin, bounds.xMax);
            p.z = Mathf.Clamp(p.z, bounds.yMin, bounds.yMax);
            return p;
        }
    }
}
