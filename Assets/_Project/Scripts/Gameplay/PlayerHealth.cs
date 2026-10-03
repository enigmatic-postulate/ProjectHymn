using System;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>Player HP, a short invulnerability after each hit, and a down/refill cycle for the arena.</summary>
    [RequireComponent(typeof(ToonTint))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] float maxHp = 100f;
        [SerializeField] float hurtInvulnerability = 0.5f;
        [SerializeField] Color hurtColor = new Color(1f, 0.25f, 0.25f);

        BeatCombatController _combat;
        PlayerMotor _motor;
        ToonTint _tint;
        float _invulnUntil;

        public float Hp { get; private set; }
        public float MaxHp => maxHp;
        public float BaseMaxHp => 100f;
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
            _tint = GetComponent<ToonTint>();
        }

        /// <summary>Armor bonus: change max HP and refill.</summary>
        public void SetMaxHp(float value)
        {
            maxHp = Mathf.Max(1f, value);
            Hp = maxHp;
        }

        public void TakeHit(float damage)
        {
            if (IsDown) return;
            Hp = Mathf.Max(0f, Hp - damage);
            _invulnUntil = Time.time + hurtInvulnerability;
            if (_combat != null) _combat.RegisterHurt();
            Damaged?.Invoke(damage);
            if (_tint != null) _tint.Flash(hurtColor, 0.35f);
            if (Hp <= 0f)
            {
                IsDown = true;
                if (_tint != null) _tint.SetTint(new Color(0.55f, 0.55f, 0.6f));
                Downed?.Invoke();
            }
        }

        public void Revive()
        {
            Hp = maxHp;
            IsDown = false;
            _invulnUntil = Time.time + 1f;
            if (_tint != null)
            {
                _tint.SetTint(Color.white);
                _tint.Flash(Color.white, 0.4f);
            }
            Revived?.Invoke();
        }
    }
}
