using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// A straw training dummy on a post (M2 prototype). Hits that deal damage knock it back on a spring, flash it and
    /// pop a number; hits that deal none (single notes, off-beat notes) just wobble it a little and pop "–".
    /// HP refills when emptied.
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IBeatDamageable
    {
        [SerializeField] float maxHp = 800f;
        [Tooltip("Rotates around the base when hit.")]
        [SerializeField] Transform wobbleRoot;
        [SerializeField] Renderer[] flashRenderers = new Renderer[0];
        [SerializeField] float stiffness = 90f;
        [SerializeField] float damping = 9f;
        [SerializeField] DamagePopups popups;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        float _hp;
        Vector2 _tilt, _tiltVel;     // degrees around X (forward/back) and Z (sideways)
        float _flash;
        Color _flashColor = Color.white;
        MaterialPropertyBlock _mpb;
        Color[] _baseColors;

        public float Hp => _hp;
        public float MaxHp => maxHp;

        void Awake()
        {
            _hp = maxHp;
            if (wobbleRoot == null) wobbleRoot = transform;
            if (popups == null) popups = FindAnyObjectByType<DamagePopups>();
            _mpb = new MaterialPropertyBlock();
            _baseColors = new Color[flashRenderers.Length];
            for (int i = 0; i < flashRenderers.Length; i++)
                _baseColors[i] = flashRenderers[i] != null && flashRenderers[i].sharedMaterial != null && flashRenderers[i].sharedMaterial.HasProperty(BaseColorId)
                    ? flashRenderers[i].sharedMaterial.GetColor(BaseColorId) : Color.white;
        }

        public void TakeHit(HitData hit)
        {
            bool real = hit.Damage > 0f;
            float push = real ? (hit.IsFinisher ? 520f : 240f) : 60f;
            // Tilt away from the hit: forward hits rotate around the axis perpendicular to the hit direction.
            var local = transform.InverseTransformDirection(new Vector3(hit.Direction.x, 0f, hit.Direction.y));
            _tiltVel += new Vector2(local.z, -local.x) * push;
            if (real)
            {
                _hp -= hit.Damage;
                if (_hp <= 0f) _hp = maxHp;
                _flash = 1f;
                _flashColor = hit.IsFinisher ? new Color(1f, 0.85f, 0.3f) : Color.white;
            }
            if (popups != null)
            {
                var at = hit.Point == default ? transform.position + Vector3.up * 1.6f : hit.Point + Vector3.up * 0.3f;
                popups.Show(at, real ? hit.Damage.ToString("0") : "–", real ? (hit.IsFinisher ? new Color(1f, 0.82f, 0.25f) : Color.white) : new Color(0.6f, 0.6f, 0.68f), hit.IsFinisher ? 1.6f : real ? 1f : 0.7f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _tiltVel += (-stiffness * _tilt - damping * _tiltVel) * dt;
            _tilt += _tiltVel * dt;
            _tilt = Vector2.ClampMagnitude(_tilt, 35f);
            wobbleRoot.localRotation = Quaternion.Euler(_tilt.x, 0f, _tilt.y);

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt * 6f);
                for (int i = 0; i < flashRenderers.Length; i++)
                {
                    var r = flashRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(BaseColorId, Color.Lerp(_baseColors[i], _flashColor, _flash));
                    r.SetPropertyBlock(_mpb);
                }
            }
        }
    }
}
