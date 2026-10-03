using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Keeps a world-space element (enemy HP bar, marker) facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        Camera _cam;

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            transform.rotation = _cam.transform.rotation;
        }
    }
}
