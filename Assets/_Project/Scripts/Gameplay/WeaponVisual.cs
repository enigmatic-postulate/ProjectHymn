using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// The weapon model in the player's right hand. Lives under the turning visual root, so it follows the
    /// facing; each attack plays a quick horizontal slash (Perfect hits swing wider).
    /// </summary>
    public class WeaponVisual : MonoBehaviour
    {
        [SerializeField] BeatCombatController combat;
        [Tooltip("Resting pose: held angled forward.")]
        [SerializeField] Vector3 restEuler = new Vector3(55f, 20f, 0f);
        [SerializeField] float swingArc = 150f;

        GameObject _model;
        float _swing;
        float _arcScale = 1f;

        public void SetModel(GameObject prefab)
        {
            if (_model != null) Destroy(_model);
            if (prefab == null) return;
            _model = Instantiate(prefab, transform, false);
            _model.name = "WeaponModel";
            foreach (var c in _model.GetComponentsInChildren<Collider>()) Destroy(c);
            var tint = GetComponentInParent<ToonTint>();
            if (tint != null) tint.Refresh();
        }

        void Awake()
        {
            if (combat == null) combat = GetComponentInParent<BeatCombatController>();
            if (_model == null && transform.childCount > 0) _model = transform.GetChild(0).gameObject;
        }

        void OnEnable() { if (combat != null) combat.ActionJudged += OnAction; }
        void OnDisable() { if (combat != null) combat.ActionJudged -= OnAction; }

        void OnAction(BeatActionResult r)
        {
            if (!r.IsAttack) return;
            _swing = 1f;
            _arcScale = r.Effective == Judgement.Perfect ? 1.25f : r.Effective == Judgement.OffBeat ? 0.75f : 1f;
        }

        void LateUpdate()
        {
            _swing = Mathf.MoveTowards(_swing, 0f, Time.unscaledDeltaTime * 5f);
            // Ease out: fast at the start of the slash, settling back to rest.
            float t = 1f - _swing;
            float sweep = _swing > 0f ? Mathf.Lerp(swingArc * 0.5f, -swingArc * 0.5f, 1f - (1f - t) * (1f - t)) * _arcScale : 0f;
            var rest = Quaternion.Euler(restEuler);
            var slash = Quaternion.Euler(_swing > 0f ? 80f : 0f, sweep, 0f);
            transform.localRotation = _swing > 0f ? slash : rest;
        }
    }
}
