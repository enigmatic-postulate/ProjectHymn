using System;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>One camera framing: angle, distance, lens, and how the pivot follows the player.</summary>
    [Serializable]
    public struct CameraShot
    {
        [Range(5f, 85f)] public float pitch;
        public float distance;
        [Range(10f, 70f)] public float fieldOfView;
        [Tooltip("Follow the target along X (lane) and/or Z. Axes not followed use Fixed Pivot.")]
        public bool followX, followZ;
        [Tooltip("Ground-plane pivot (x = world X, y = world Z) for the axes that don't follow.")]
        public Vector2 fixedPivot;
        [Tooltip("Pivot stays inside this ground-plane area. Zero size = no limit.")]
        public Rect bounds;

        /// <summary>GDD M6 side lane: low, close, follows along X only.</summary>
        public static CameraShot Lane(Rect room, float laneZ) => new CameraShot
        {
            pitch = 22f, distance = 13f, fieldOfView = 30f, followX = true, followZ = false,
            fixedPivot = new Vector2(room.center.x, laneZ),
            bounds = new Rect(room.xMin + 5.5f, laneZ, Mathf.Max(0f, room.width - 11f), 0f),
        };

        /// <summary>The M5 angled view, fixed over an arena (boss rooms).</summary>
        public static CameraShot Arena(Rect room) => new CameraShot
        {
            pitch = 55f, distance = 17.5f, fieldOfView = 30f, followX = false, followZ = false,
            fixedPivot = new Vector2(room.center.x, room.center.y - 0.6f),
        };
    }

    /// <summary>
    /// Perspective camera rig (GDD M5/M6): a pivot on the ground plane with the camera child pitched down and
    /// pulled back. M6 adds framings (<see cref="CameraShot"/>): the low side-lane camera for stage rooms and the
    /// angled view for arenas, with blends that can be timed to start on a beat. Camera shake is added on top.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Camera cam;
        [Range(5f, 85f)] [SerializeField] float pitch = 52f;
        [SerializeField] float yaw = 0f;
        [SerializeField] float distance = 16f;
        [Range(10f, 70f)] [SerializeField] float fieldOfView = 30f;
        [Tooltip("How quickly the pivot catches up with the target.")]
        [SerializeField] float smoothing = 7f;
        [Tooltip("Pivot stays inside this area of the ground plane (x = world X, y = world Z). Zero size = no limit.")]
        [SerializeField] Rect bounds;
        [Tooltip("Which axes follow the target. Axes not followed stay at Fixed Pivot.")]
        [SerializeField] bool followX = true, followZ = true;
        [SerializeField] Vector2 fixedPivot;

        Vector3 _shake;
        float _shakeTime, _shakeDuration, _shakeAmount;

        // Blend between framings
        bool _blending;
        float _blendDelay, _blendTime, _blendDuration;
        CameraShot _from, _to;
        Vector3 _pivotFrom;

        public Transform Target { get => target; set => target = value; }
        public Camera Camera => cam;

        void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (target != null) transform.position = Goal();
            Place();
        }

        void OnValidate()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            Place();
        }

        /// <summary>The current framing as a shot.</summary>
        public CameraShot Current => new CameraShot
        {
            pitch = pitch, distance = distance, fieldOfView = fieldOfView, followX = followX, followZ = followZ,
            fixedPivot = fixedPivot, bounds = bounds,
        };

        /// <summary>Jump to a framing now.</summary>
        public void Cut(CameraShot shot)
        {
            _blending = false;
            Apply(shot);
            transform.position = Goal();
            Place();
        }

        /// <summary>Move to a framing over <paramref name="seconds"/>, starting after <paramref name="delay"/> (e.g. the next downbeat).</summary>
        public void Blend(CameraShot shot, float seconds, float delay = 0f)
        {
            _from = Current;
            _to = shot;
            _blendDelay = Mathf.Max(0f, delay);
            _blendTime = 0f;
            _blendDuration = Mathf.Max(0.01f, seconds);
            _blending = true;
            _pivotFrom = transform.position;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (_blending)
            {
                if (_blendDelay > 0f) _blendDelay -= dt;
                else
                {
                    _blendTime += dt;
                    float k = Mathf.Clamp01(_blendTime / _blendDuration);
                    float e = k * k * (3f - 2f * k);
                    pitch = Mathf.Lerp(_from.pitch, _to.pitch, e);
                    distance = Mathf.Lerp(_from.distance, _to.distance, e);
                    fieldOfView = Mathf.Lerp(_from.fieldOfView, _to.fieldOfView, e);
                    // Follow rules and bounds switch at once; the pivot eases from where it was.
                    followX = _to.followX; followZ = _to.followZ; fixedPivot = _to.fixedPivot; bounds = _to.bounds;
                    transform.position = Vector3.Lerp(_pivotFrom, Goal(), e);
                    if (k >= 1f) _blending = false;
                }
            }

            if (!_blending || _blendDelay > 0f)
            {
                var goal = Goal();
                transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smoothing * dt));
            }

            _shake = Vector3.zero;
            if (_shakeTime > 0f)
            {
                _shakeTime -= dt;
                float k = Mathf.Clamp01(_shakeTime / Mathf.Max(0.0001f, _shakeDuration));
                var r = UnityEngine.Random.insideUnitCircle * _shakeAmount * k;
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

        void Apply(CameraShot s)
        {
            pitch = s.pitch; distance = s.distance; fieldOfView = s.fieldOfView;
            followX = s.followX; followZ = s.followZ; fixedPivot = s.fixedPivot; bounds = s.bounds;
        }

        Vector3 Goal()
        {
            var t = target != null ? Ground.ToPlane(target.position) : fixedPivot;
            var p = new Vector2(target != null && followX ? t.x : fixedPivot.x, target != null && followZ ? t.y : fixedPivot.y);
            if (target == null && fixedPivot == Vector2.zero) p = Ground.ToPlane(transform.position); // legacy fixed rigs
            return Clamp(Ground.ToWorld(p));
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
            // A zero-height bounds still clamps X (a lane); zero size on both = no limit.
            if (bounds.width <= 0f && bounds.height <= 0f && bounds.x == 0f && bounds.y == 0f) return p;
            p.x = Mathf.Clamp(p.x, bounds.xMin, bounds.xMax);
            p.z = Mathf.Clamp(p.z, bounds.yMin, bounds.yMax);
            return p;
        }
    }
}
