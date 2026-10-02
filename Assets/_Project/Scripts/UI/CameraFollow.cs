using System.Collections.Generic;
using System.Text;
using ProjectHymn.Combat;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>Smoothly follows the player and stays inside the town's bounds.</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Rect bounds = new Rect(-15f, -8.4f, 30f, 16.8f);
        [SerializeField] float smoothing = 8f;

        Camera _cam;

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (target == null || _cam == null) return;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            var p = (Vector2)target.position;
            p.x = Mathf.Clamp(p.x, bounds.xMin + halfW, Mathf.Max(bounds.xMin + halfW, bounds.xMax - halfW));
            p.y = Mathf.Clamp(p.y, bounds.yMin + halfH, Mathf.Max(bounds.yMin + halfH, bounds.yMax - halfH));
            var cur = (Vector2)transform.position;
            var next = Vector2.Lerp(cur, p, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }
    }
}
