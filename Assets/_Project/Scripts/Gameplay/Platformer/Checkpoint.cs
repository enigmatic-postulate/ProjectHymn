using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A respawn point (vertical slice 1). Touching its trigger makes it the place a fall returns the player to, and
    /// lights its crystal.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] Renderer[] glow = new Renderer[0];
        [SerializeField] Light lamp;
        [SerializeField] Color lit = new Color(0.45f, 0.95f, 1f);
        [SerializeField] bool startsLit;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock _mpb;
        float _pulse;

        public bool Reached { get; private set; }
        public Vector3 SpawnPoint => transform.position + Vector3.up * 0.05f;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (lamp != null) lamp.enabled = false;
            if (startsLit) Activate();
        }

        public void Activate()
        {
            if (Reached) return;
            Reached = true;
            _pulse = 1f;
            if (lamp != null) lamp.enabled = true;
            Paint(1f);
        }

        void Update()
        {
            if (_pulse <= 0f) return;
            _pulse = Mathf.Max(0f, _pulse - Time.deltaTime * 1.5f);
            Paint(1f + _pulse * 2f);
            if (lamp != null) lamp.intensity = 1.2f + _pulse * 4f;
        }

        void Paint(float strength)
        {
            foreach (var r in glow)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, lit);
                _mpb.SetColor(EmissionId, lit * strength);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
