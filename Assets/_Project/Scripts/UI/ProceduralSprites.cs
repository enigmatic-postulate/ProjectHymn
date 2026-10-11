using UnityEngine;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Sprites drawn in code: a heart, a ring and a plain white square. Sprites render with Unity's built-in sprite and
    /// UI shaders, which every build includes (WebGL too), so nothing here depends on a shader being found by name at
    /// run time; and nothing depends on a font having a glyph like ♥.
    /// </summary>
    public static class ProceduralSprites
    {
        static Sprite _heart, _ring, _white, _glow;

        /// <summary>A soft round glow (the enemies' galvanize aura).</summary>
        public static Sprite Glow
        {
            get
            {
                if (_glow != null) return _glow;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Glow", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    px[j * size + i] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                tex.SetPixels32(px);
                tex.Apply();
                _glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                _glow.name = "Glow";
                return _glow;
            }
        }

        public static Sprite Heart => _heart != null ? _heart : (_heart = Make(64, (x, y) =>
        {
            // The classic implicit heart: (x² + y² − 1)³ − x² y³ ≤ 0, scaled into the square.
            float u = x * 1.25f, v = y * 1.25f + 0.15f;
            float a = u * u + v * v - 1f;
            return a * a * a - u * u * v * v * v;
        }, "Heart"));

        public static Sprite Ring => _ring != null ? _ring : (_ring = Make(128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Abs(r - 0.85f) - 0.1f;
        }, "Ring"));

        /// <summary>A 1×1-unit white square (pivot in the middle) for bars.</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "White", filterMode = FilterMode.Point };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                _white.name = "White";
                return _white;
            }
        }

        /// <summary>A white shape with soft edges from a signed field over [-1, 1]² (negative = inside).</summary>
        static Sprite Make(int size, System.Func<float, float, float> field, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float step = 2f / size;
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                // 4×4 supersampling for a clean edge.
                int inside = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float x = -1f + (i + (sx + 0.5f) / 4f) * step;
                    float y = -1f + (j + (sy + 0.5f) / 4f) * step;
                    if (field(x, y) <= 0f) inside++;
                }
                px[j * size + i] = new Color32(255, 255, 255, (byte)(inside * 255 / 16));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            s.name = name;
            return s;
        }
    }
}
