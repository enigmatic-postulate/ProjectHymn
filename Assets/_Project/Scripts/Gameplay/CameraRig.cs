using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Base for cameras that combat effects can shake.</summary>
    public abstract class CameraRig : MonoBehaviour
    {
        Vector3 _shake;
        float _shakeTime, _shakeDuration, _shakeAmount;

        public void Shake(float amount, float duration)
        {
            _shakeAmount = Mathf.Max(_shakeTime > 0f ? _shakeAmount : 0f, amount);
            _shakeDuration = _shakeTime = duration;
        }

        /// <summary>Remove last frame's shake before moving the camera.</summary>
        protected Vector3 Unshaken => transform.position - _shake;

        /// <summary>Add this frame's shake after moving the camera.</summary>
        protected void ApplyShake(float dt)
        {
            _shake = Vector3.zero;
            if (_shakeTime <= 0f) return;
            _shakeTime -= dt;
            _shake = Random.insideUnitSphere * _shakeAmount * Mathf.Clamp01(_shakeTime / Mathf.Max(0.001f, _shakeDuration));
            transform.position += _shake;
        }
    }
}
