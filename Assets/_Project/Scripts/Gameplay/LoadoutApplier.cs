using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Applies the saved loadout to the player when a scene starts: equipped Phrases, weapon behaviour and
    /// model in hand, armor stats and the armor's body model. Works in town (no combat) and in arenas.
    /// </summary>
    public class LoadoutApplier : MonoBehaviour
    {
        [SerializeField] GameCatalog catalog;
        [Tooltip("Parent for the body model; turned to face the move direction by PlayerMotor.")]
        [SerializeField] Transform visualRoot;
        [SerializeField] WeaponVisual weaponVisual;

        GameObject _body;

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
                if (armor.model != null) SetBody(armor.model);
            }

            var weapon = catalog.FindItem(save.weaponId);
            if (weapon != null && weapon.kind == ItemKind.Weapon)
            {
                if (combat != null) combat.SetWeapon(weapon.weaponType);
                if (weaponVisual != null) weaponVisual.SetModel(weapon.model);
                var tracker = FindAnyObjectByType<PlaystyleTracker>();
                if (tracker != null) tracker.WeaponName = weapon.displayName;
            }

            var tint = GetComponent<ToonTint>();
            if (tint != null) tint.Refresh();
        }

        void SetBody(GameObject prefab)
        {
            if (visualRoot == null) return;
            // Replace the current body model (keep the weapon, which is a separate child).
            if (_body == null)
            {
                var existing = visualRoot.Find("Body");
                if (existing != null) _body = existing.gameObject;
            }
            if (_body != null) Destroy(_body);
            _body = Instantiate(prefab, visualRoot, false);
            _body.name = "Body";
            foreach (var c in _body.GetComponentsInChildren<Collider>()) Destroy(c);
        }
    }
}
