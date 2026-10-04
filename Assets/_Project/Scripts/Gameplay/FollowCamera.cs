using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Side-on camera that follows the player and looks at the space between the player and the target.</summary>
    public class FollowCamera : CameraRig
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(4.3f, 2.0f, 0.3f);
        [SerializeField] Vector3 lookOffset = new Vector3(0f, 1.05f, 0.8f);
        [SerializeField] float smoothing = 6f;

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            var goal = target.position + offset;
            transform.position = Vector3.Lerp(Unshaken, goal, 1f - Mathf.Exp(-smoothing * dt));
            transform.rotation = Quaternion.LookRotation(target.position + lookOffset - transform.position);
            ApplyShake(dt);
        }
    }
}
