using System.Collections;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>A target for testing timing and combos. Flashes and punches on hits, refills when emptied.</summary>
    public class TrainingDummy : MonoBehaviour, IBeatDamageable
    {
        [SerializeField] float maxHp = 500f;
        [SerializeField] Color hitColor = Color.white;
        [SerializeField] Color finisherColor = new Color(1f, 0.85f, 0.2f);

        float _hp;
        Vector3 _baseScale;
        ToonTint _tint;

        public float Hp => _hp;
        public float MaxHp => maxHp;
        public float LastDamage { get; private set; }
        public string LastEffect { get; private set; }

        void Awake()
        {
            _hp = maxHp;
            _baseScale = transform.localScale;
            _tint = GetComponent<ToonTint>();
            if (_tint == null) _tint = gameObject.AddComponent<ToonTint>();
        }

        public void TakeHit(HitData hit)
        {
            _hp -= hit.Damage;
            LastDamage = hit.Damage;
            LastEffect = hit.EffectId;
            if (_hp <= 0f) _hp = maxHp;
            _tint.Flash(hit.IsFinisher ? finisherColor : hitColor, hit.IsFinisher ? 0.3f : 0.12f);
            StopAllCoroutines();
            StartCoroutine(Punch(hit.IsFinisher ? 1.35f : 1.12f));
        }

        IEnumerator Punch(float punch)
        {
            transform.localScale = _baseScale * punch;
            yield return new WaitForSecondsRealtime(0.08f);
            transform.localScale = _baseScale;
        }
    }
}
