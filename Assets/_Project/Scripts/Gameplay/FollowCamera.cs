using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Side-on camera that follows the player and looks at the space between the player and the target.</summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(4.3f, 2.0f, 0.3f);
        [SerializeField] Vector3 lookOffset = new Vector3(0f, 1.05f, 0.8f);
        [SerializeField] float smoothing = 6f;

        Vector3 _shake;
        float _shakeTime, _shakeDuration, _shakeAmount;

        public void Shake(float amount, float duration)
        {
            _shakeAmount = Mathf.Max(_shakeTime > 0f ? _shakeAmount : 0f, amount);
            _shakeDuration = _shakeTime = duration;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            var goal = target.position + offset;
            transform.position = Vector3.Lerp(transform.position - _shake, goal, 1f - Mathf.Exp(-smoothing * dt));
            transform.rotation = Quaternion.LookRotation(target.position + lookOffset - transform.position);
            _shake = Vector3.zero;
            if (_shakeTime > 0f)
            {
                _shakeTime -= dt;
                _shake = Random.insideUnitSphere * _shakeAmount * Mathf.Clamp01(_shakeTime / Mathf.Max(0.001f, _shakeDuration));
                transform.position += _shake;
            }
        }
    }
}
