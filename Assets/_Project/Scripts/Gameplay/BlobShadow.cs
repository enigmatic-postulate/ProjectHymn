using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A soft dark disc on the ground under a character (GDD M6 readability: shows where a jumper or a flyer
    /// will land). Stays on the floor whatever its parent's height and shrinks as <see cref="heightOf"/> rises.
    /// </summary>
    public class BlobShadow : MonoBehaviour
    {
        [Tooltip("Whose height above the floor shrinks the shadow (the jumping visual, a hovering model).")]
        [SerializeField] Transform heightOf;
        [SerializeField] float groundY = 0.02f;
        [SerializeField] float size = 1f;
        [Tooltip("Height at which the shadow is at its smallest.")]
        [SerializeField] float fadeHeight = 2.5f;

        public void Setup(Transform height, float diameter)
        {
            heightOf = height;
            size = diameter;
        }

        void LateUpdate()
        {
            var p = transform.parent != null ? transform.parent.position : transform.position;
            transform.position = new Vector3(p.x, groundY, p.z);
            transform.rotation = Quaternion.identity;
            float h = heightOf != null ? Mathf.Max(0f, heightOf.position.y - groundY) : 0f;
            float k = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(h / Mathf.Max(0.01f, fadeHeight)));
            var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float sx = size * k / Mathf.Max(0.001f, parentScale.x), sz = size * k / Mathf.Max(0.001f, parentScale.z);
            transform.localScale = new Vector3(sx, 0.01f / Mathf.Max(0.001f, parentScale.y), sz);
        }
    }
}
