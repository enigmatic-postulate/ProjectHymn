using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>The weapon sprite in the player's hand: follows facing and swings on each attack.</summary>
    public class WeaponVisual : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] PlayerMotor motor;
        [SerializeField] BeatCombatController combat;
        [SerializeField] float handOffset = 0.45f;

        float _swing;

        public void SetSprite(Sprite s) { if (sprite != null) sprite.sprite = s; }

        void Awake()
        {
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            if (combat == null) combat = GetComponentInParent<BeatCombatController>();
        }

        void OnEnable() { if (combat != null) combat.ActionJudged += OnAction; }
        void OnDisable() { if (combat != null) combat.ActionJudged -= OnAction; }

        void OnAction(BeatActionResult r) { if (r.IsAttack) _swing = r.Effective == ProjectHymn.Core.Judgement.Perfect ? 1.3f : 1f; }

        void LateUpdate()
        {
            var facing = motor != null ? motor.Facing : Vector2.right;
            bool left = facing.x < -0.1f;
            transform.localPosition = new Vector3(left ? -handOffset : handOffset, -0.1f, 0f);
            if (sprite != null) sprite.flipX = left;
            _swing = Mathf.MoveTowards(_swing, 0f, Time.unscaledDeltaTime * 6f);
            float angle = (left ? 1f : -1f) * 80f * _swing;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
