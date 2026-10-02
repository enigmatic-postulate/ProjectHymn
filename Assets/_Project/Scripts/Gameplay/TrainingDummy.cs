using System.Collections;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>A target for testing timing and combos. Shows damage, flashes, refills when emptied.</summary>
    public class TrainingDummy : MonoBehaviour, IBeatDamageable
    {
        [SerializeField] float maxHp = 500f;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Color hitColor = Color.white;
        [SerializeField] Color finisherColor = new Color(1f, 0.85f, 0.2f);

        float _hp;
        Color _baseColor;
        Vector3 _baseScale;

        public float Hp => _hp;
        public float MaxHp => maxHp;
        public float LastDamage { get; private set; }
        public string LastEffect { get; private set; }

        void Awake()
        {
            _hp = maxHp;
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null) _baseColor = sprite.color;
            _baseScale = transform.localScale;
        }

        public void TakeHit(HitData hit)
        {
            _hp -= hit.Damage;
            LastDamage = hit.Damage;
            LastEffect = hit.EffectId;
            if (_hp <= 0f) _hp = maxHp;
            StopAllCoroutines();
            StartCoroutine(Flash(hit.IsFinisher ? finisherColor : hitColor, hit.IsFinisher ? 1.35f : 1.12f));
        }

        IEnumerator Flash(Color c, float punch)
        {
            if (sprite != null) sprite.color = c;
            transform.localScale = _baseScale * punch;
            yield return new WaitForSeconds(0.08f);
            if (sprite != null) sprite.color = _baseColor;
            transform.localScale = _baseScale;
        }
    }
}
