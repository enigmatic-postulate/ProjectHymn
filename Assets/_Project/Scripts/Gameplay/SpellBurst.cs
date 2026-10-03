using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Visuals for a finisher spell (GDD M6): rings of glowing shapes burst out from the caster, circle and rise
    /// over one measure, then shrink away; a light flashes at the start. Bloom does the rest.
    /// </summary>
    public class SpellBurst : MonoBehaviour
    {
        const int Rings = 3, PerRing = 12;
        Transform[] _parts;
        float[] _angle, _radius, _speed;
        float _time, _duration;
        Light _light;

        public void Play(Mesh mesh, Material material, float seconds)
        {
            _duration = Mathf.Max(0.3f, seconds);
            int n = Rings * PerRing;
            _parts = new Transform[n];
            _angle = new float[n]; _radius = new float[n]; _speed = new float[n];
            for (int r = 0; r < Rings; r++)
            for (int i = 0; i < PerRing; i++)
            {
                int k = r * PerRing + i;
                GameObject go;
                if (mesh != null)
                {
                    go = new GameObject("Spark", typeof(MeshFilter), typeof(MeshRenderer));
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.GetComponent<MeshRenderer>();
                    if (material != null) mr.sharedMaterial = material;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(go.GetComponent<Collider>());
                    if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
                go.transform.SetParent(transform, false);
                _parts[k] = go.transform;
                _angle[k] = (i + r * 0.5f) / PerRing * Mathf.PI * 2f;
                _radius[k] = 2.5f + r * 3.2f;
                _speed[k] = (r % 2 == 0 ? 1f : -1f) * (1.6f - r * 0.3f);
            }
            var lightGo = new GameObject("Flash", typeof(Light));
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            _light = lightGo.GetComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 18f;
            _light.color = new Color(1f, 0.85f, 0.55f);
            _light.intensity = 6f;
            Update();
        }

        void Update()
        {
            if (_parts == null) return;
            _time += Time.deltaTime;
            float k = Mathf.Clamp01(_time / _duration);
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(k * 3f), 3f);   // burst out quickly
            float fade = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;          // shrink away at the end
            for (int i = 0; i < _parts.Length; i++)
            {
                float a = _angle[i] + _speed[i] * _time;
                float r = _radius[i] * grow;
                _parts[i].localPosition = new Vector3(Mathf.Cos(a) * r, 0.6f + k * 2.2f + Mathf.Sin(a * 3f + _time * 6f) * 0.25f, Mathf.Sin(a) * r);
                _parts[i].localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg * 2f, 45f);
                _parts[i].localScale = Vector3.one * 0.45f * fade;
            }
            if (_light != null) _light.intensity = 6f * (1f - Mathf.Clamp01(k * 4f));
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
