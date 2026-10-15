using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A KayKit townsperson (or a posed hero on the title screen): loops an idle clip on the rig and, when the player
    /// comes near, turns to face them. Needs no Conductor, so it works in the village where no music clock runs.
    /// </summary>
    public class Villager : MonoBehaviour
    {
        [SerializeField] Transform rigRoot;
        [Tooltip("Turned to face the player (the model's yaw).")]
        [SerializeField] Transform body;
        [SerializeField] BakedClip idleClip;
        [Tooltip("Played instead of the idle while the player is close (optional).")]
        [SerializeField] BakedClip nearClip;
        [SerializeField] float lookRange = 4.5f;
        [Tooltip("Yaw when nobody is near: 180 faces the camera.")]
        [SerializeField] float restYaw = 180f;
        [SerializeField] float turnSpeed = 240f;

        /// <summary>Turn to face the camera (restYaw) whatever the player does: set while they're talking (VS-5 portraits).</summary>
        [System.NonSerialized] public bool facingViewer;

        ClipPose _pose;
        Transform _player;
        float _offset, _blend;

        void Awake()
        {
            if (rigRoot != null) _pose = new ClipPose(rigRoot.GetComponentsInChildren<Transform>(true), "root");
            _offset = Random.value * 10f;
            if (body != null) body.localRotation = Quaternion.Euler(0f, restYaw, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_player == null || !_player.gameObject.activeInHierarchy)
            {
                var pc = FindAnyObjectByType<PlatformerController>();
                _player = pc != null ? pc.transform : null;
            }

            bool near = false;
            float yaw = restYaw;
            if (_player != null)
            {
                var d = _player.position - transform.position;
                if (Mathf.Abs(d.x) < lookRange && Mathf.Abs(d.y) < 2.5f)
                {
                    near = true;
                    yaw = d.x >= 0f ? 115f : -115f;      // toward the player, a little toward the camera
                }
            }
            if (facingViewer) yaw = restYaw;
            if (body != null)
            {
                float cur = body.localEulerAngles.y;
                body.localRotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(cur, yaw, turnSpeed * dt), 0f);
            }

            if (idleClip == null || rigRoot == null) return;
            if (_pose == null || _pose.Rot == null) _pose = new ClipPose(rigRoot.GetComponentsInChildren<Transform>(true), "root");
            _blend = Mathf.MoveTowards(_blend, near && nearClip != null ? 1f : 0f, dt * 3f);
            float t = Time.time + _offset;
            _pose.Reset();
            _pose.Layer(idleClip, t / Mathf.Max(0.1f, idleClip.length), 1f, true);
            if (nearClip != null && _blend > 0f) _pose.Layer(nearClip, t / Mathf.Max(0.1f, nearClip.length), _blend, true);
            _pose.Apply();
        }
    }
}
