using System.Collections;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Per-object colour effects for toon-shaded models without creating material copies:
    /// a multiply tint (stagger grey, guard darkening), a glow (enemy telegraphs, picked up by bloom) and
    /// short flashes (hits). Uses MaterialPropertyBlocks on every renderer under this object.
    /// </summary>
    public class ToonTint : MonoBehaviour
    {
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int GlowId = Shader.PropertyToID("_GlowColor");

        Renderer[] _renderers = new Renderer[0];
        MaterialPropertyBlock _block;
        Color _tint = Color.white;
        Color _glow = Color.black;
        Color _flashColor = Color.white;
        float _flash;
        Coroutine _flashRoutine;

        void Awake() => Refresh();

        /// <summary>Re-collect renderers (call after swapping models, e.g. armor).</summary>
        public void Refresh()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            Apply();
        }

        public void SetTint(Color c) { if (c == _tint) return; _tint = c; Apply(); }

        /// <summary>Additive glow (HDR allowed, so bloom picks it up). Black = off.</summary>
        public void SetGlow(Color c) { if (c == _glow) return; _glow = c; Apply(); }

        /// <summary>Blend toward a colour, fading out over <paramref name="seconds"/> (unscaled, survives hit-stop).</summary>
        public void Flash(Color c, float seconds)
        {
            _flashColor = c;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            if (isActiveAndEnabled) _flashRoutine = StartCoroutine(FlashRoutine(seconds));
        }

        /// <summary>Hold a flash amount directly (0..1).</summary>
        public void SetFlash(Color c, float amount) { _flashColor = c; _flash = Mathf.Clamp01(amount); Apply(); }

        IEnumerator FlashRoutine(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                _flash = 1f - t / Mathf.Max(0.0001f, seconds);
                Apply();
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            _flash = 0f;
            Apply();
            _flashRoutine = null;
        }

        void Apply()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(TintId, _tint);
                _block.SetColor(GlowId, _glow);
                _block.SetColor(FlashColorId, _flashColor);
                _block.SetFloat(FlashAmountId, _flash);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
