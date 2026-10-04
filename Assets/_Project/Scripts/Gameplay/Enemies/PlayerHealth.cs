using System;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The player's hit points (vertical slice 2): 5 by default, a short grace after each hit, knockback, a red
    /// flash on the model. At zero the player respawns at the last checkpoint with full HP and every enemy still
    /// standing resets.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] int maxHp = 5;
        [SerializeField] float graceSeconds = 0.4f;
        [SerializeField] float respawnDelay = 0.7f;
        [SerializeField] Renderer[] flashRenderers = new Renderer[0];

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Vitality _vit;
        PlatformerController _platformer;
        MaterialPropertyBlock _mpb;
        float _flash, _respawnAt = -1f;
        Color[][] _base;
        bool _tinted;

        public int Hp => _vit != null ? _vit.Hp : maxHp;
        public int MaxHp => maxHp;
        public bool IsDead => _vit != null && _vit.IsDead;

        public event Action<int> Hurt;        // damage taken
        public event Action Died;
        public event Action Revived;

        void Awake()
        {
            _vit = new Vitality(maxHp) { GraceSeconds = graceSeconds };
            _platformer = GetComponent<PlatformerController>();
            _mpb = new MaterialPropertyBlock();
            if (flashRenderers.Length == 0) flashRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            _base = new Color[flashRenderers.Length][];
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                var mats = flashRenderers[i] != null ? flashRenderers[i].sharedMaterials : new Material[0];
                _base[i] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                    _base[i][m] = mats[m] != null && mats[m].HasProperty(BaseColorId) ? mats[m].GetColor(BaseColorId) : Color.white;
            }
        }

        /// <summary>Take an enemy hit coming from <paramref name="from"/>. Ignored during grace or death.</summary>
        public bool Damage(int amount, Vector3 from)
        {
            if (!_vit.Damage(amount, Time.timeAsDouble)) return false;
            _flash = 1f;
            float dir = transform.position.x >= from.x ? 1f : -1f;
            if (_platformer != null) _platformer.Knockback(dir);
            Hurt?.Invoke(amount);
            if (_vit.IsDead)
            {
                _respawnAt = Time.time + respawnDelay;
                Died?.Invoke();
            }
            return true;
        }

        void Update()
        {
            if (_respawnAt > 0f && Time.time >= _respawnAt)
            {
                _respawnAt = -1f;
                _vit.Refill();
                if (_platformer != null) _platformer.Respawn();
                Revived?.Invoke();
            }
            bool grace = _vit.InGrace(Time.timeAsDouble);
            if (_flash <= 0f && !grace && !_tinted) return;
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 4f);
            // Red flash on the hit, then a flicker through the grace period.
            bool flicker = grace && Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
            float k = Mathf.Max(_flash, flicker ? 0.35f : 0f);
            _tinted = k > 0f;
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                var r = flashRenderers[i];
                if (r == null) continue;
                for (int m = 0; m < _base[i].Length; m++)
                {
                    _mpb.Clear();
                    if (_tinted) _mpb.SetColor(BaseColorId, Color.Lerp(_base[i][m], new Color(1f, 0.2f, 0.15f), k));
                    r.SetPropertyBlock(_mpb, m);
                }
            }
        }
    }
}
