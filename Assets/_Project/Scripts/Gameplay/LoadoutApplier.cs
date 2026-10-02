using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Applies the saved loadout to the player when a scene starts: equipped Phrases, weapon behaviour and
    /// sprite, armor stats and look. Works in town (no combat) and in arenas.
    /// </summary>
    public class LoadoutApplier : MonoBehaviour
    {
        [SerializeField] GameCatalog catalog;
        [SerializeField] SpriteRenderer body;
        [SerializeField] WeaponVisual weaponVisual;

        void Start() => Apply();

        public void Apply()
        {
            if (catalog == null) return;
            var save = SaveSystem.Current;

            var combat = GetComponent<BeatCombatController>();
            if (combat != null)
            {
                var phrases = new List<PhraseData>();
                foreach (var id in save.equippedPhrases)
                {
                    var p = catalog.FindPhrase(id);
                    if (p != null) phrases.Add(p);
                }
                if (phrases.Count > 0) combat.SetPhrases(phrases);
            }

            var weapon = catalog.FindItem(save.weaponId);
            if (weapon != null && weapon.kind == ItemKind.Weapon)
            {
                if (combat != null) combat.SetWeapon(weapon.weaponType);
                if (weaponVisual != null) weaponVisual.SetSprite(weapon.sprite);
                var tracker = FindAnyObjectByType<PlaystyleTracker>();
                if (tracker != null) tracker.WeaponName = weapon.displayName;
            }

            var armor = catalog.FindItem(save.armorId);
            if (armor != null && armor.kind == ItemKind.Armor)
            {
                var health = GetComponent<PlayerHealth>();
                if (health != null) health.SetMaxHp(health.BaseMaxHp + armor.bonusHp);
                if (combat != null)
                {
                    combat.Stamina.Max = 100f + armor.bonusStamina;
                    combat.Stamina.RegenPerSecond = 35f * Mathf.Max(0.1f, armor.staminaRegenMultiplier);
                    combat.Stamina.Refill();
                }
                var motor = GetComponent<PlayerMotor>();
                if (motor != null) motor.SpeedMultiplier = armor.moveSpeedMultiplier;
                if (body != null && armor.sprite != null) body.sprite = armor.sprite;
            }
        }
    }
}
