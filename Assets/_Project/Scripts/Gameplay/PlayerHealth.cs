using System;
using System.Collections;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Player HP, a short invulnerability after each hit, and a down/refill cycle for the arena.</summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] float maxHp = 100f;
        [SerializeField] float hurtInvulnerability = 0.5f;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Color hurtColor = new Color(1f, 0.3f, 0.3f);

        BeatCombatController _combat;
        PlayerMotor _motor;
        float _invulnUntil;
        Color _baseColor;

        public float Hp { get; private set; }
        public float MaxHp => maxHp;
        public bool IsDown { get; private set; }
        /// <summary>On-beat Step i-frames or the post-hit grace period.</summary>
        public bool Invulnerable => IsDown || Time.time < _invulnUntil || (_motor != null && _motor.Invulnerable);

        public event Action<float> Damaged; // amount
        public event Action Downed;
        public event Action Revived;

        void Awake()
        {
            Hp = maxHp;
            _combat = GetComponent<BeatCombatController>();
            _motor = GetComponent<PlayerMotor>();
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null) _baseColor = sprite.color;
        }

        public void TakeHit(float damage)
        {
            if (IsDown) return;
            Hp = Mathf.Max(0f, Hp - damage);
            _invulnUntil = Time.time + hurtInvulnerability;
            if (_combat != null) _combat.RegisterHurt();
            Damaged?.Invoke(damage);
            StopAllCoroutines();
            StartCoroutine(Flash());
            if (Hp <= 0f)
            {
                IsDown = true;
                Downed?.Invoke();
            }
        }

        public void Revive()
        {
            Hp = maxHp;
            IsDown = false;
            _invulnUntil = Time.time + 1f;
            if (sprite != null) sprite.color = _baseColor;
            Revived?.Invoke();
        }

        IEnumerator Flash()
        {
            if (sprite == null) yield break;
            for (int i = 0; i < 3; i++)
            {
                sprite.color = hurtColor;
                yield return new WaitForSecondsRealtime(0.06f);
                sprite.color = _baseColor;
                yield return new WaitForSecondsRealtime(0.06f);
            }
        }
    }
}
