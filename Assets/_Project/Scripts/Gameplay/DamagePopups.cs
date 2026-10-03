using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Floating numbers that rise, face the camera and fade (pooled 3D text).</summary>
    public class DamagePopups : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] float lifetime = 0.8f;
        [SerializeField] float rise = 0.9f;

        class Popup { public TextMesh Text; public MeshRenderer Renderer; public Vector3 Start; public float Age; public Color Color; public float Scale; }
        readonly List<Popup> _pool = new List<Popup>();

        public void Show(Vector3 position, string text, Color color, float scale = 1f)
        {
            Popup p = null;
            foreach (var x in _pool) if (x.Age >= lifetime) { p = x; break; }
            if (p == null)
            {
                var go = new GameObject("Popup", typeof(TextMesh));
                go.transform.SetParent(transform, false);
                var tm = go.GetComponent<TextMesh>();
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = 64;
                tm.characterSize = 0.035f;
                tm.fontStyle = FontStyle.Bold;
                if (font != null) { tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
                p = new Popup { Text = tm, Renderer = go.GetComponent<MeshRenderer>() };
                _pool.Add(p);
            }
            p.Text.text = text;
            p.Start = position + Random.insideUnitSphere * 0.12f;
            p.Age = 0f;
            p.Color = color;
            p.Scale = scale;
            p.Text.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            foreach (var p in _pool)
            {
                if (p.Age >= lifetime) { if (p.Text.gameObject.activeSelf) p.Text.gameObject.SetActive(false); continue; }
                p.Age += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(p.Age / lifetime);
                var t = p.Text.transform;
                t.position = p.Start + Vector3.up * rise * (1f - (1f - k) * (1f - k));
                if (cam != null) t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);
                float pop = k < 0.15f ? Mathf.Lerp(1.6f, 1f, k / 0.15f) : 1f;
                t.localScale = Vector3.one * p.Scale * pop;
                var c = p.Color; c.a = 1f - k * k;
                p.Text.color = c;
            }
        }
    }
}
