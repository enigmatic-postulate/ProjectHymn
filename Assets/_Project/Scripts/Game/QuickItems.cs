using ProjectHymn.Progression;
using UnityEngine;

using ProjectHymn.UI;

namespace ProjectHymn.Gameplay
{
    /// <summary>On the player: the quick-slot item (E / B to use, Q / D-pad down to switch).</summary>
    public class QuickItems : MonoBehaviour
    {
        PlayerInputReader _input;
        PlayerHealth _health;
        SwordCombatController _combat;
        static AudioClip _gulp;

        void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _health = GetComponent<PlayerHealth>();
            _combat = GetComponent<SwordCombatController>();
            _interactor = GetComponent<PlayerInteractor>();
        }

        PlayerInteractor _interactor;

        void Update()
        {
            if (_input == null) return;
            var s = GameSession.Instance;
            if (_input.CycleItemPressed) s.CycleQuickItem();
            if (!_input.UseItemPressed) return;
            if (_interactor != null && _interactor.HasTarget) return;     // E talks to whoever is near
            if (UiState.JustClosed) return;
            var id = s.Data.quickItem;
            var item = s.Db != null ? s.Db.Item(id) : null;
            if (item == null) { GameHud.Toast("No item in the quick slot"); return; }
            switch (item.kind)
            {
                case ItemKind.Potion:
                    if (_health == null || _health.Hp >= _health.MaxHp) { GameHud.Toast("Hearts already full"); return; }
                    _health.Heal(item.heal);
                    break;
                case ItemKind.Tonic:
                    if (_combat == null || !_combat.CanBoost) { GameHud.Toast("Save it for a fight (no music here)"); return; }
                    _combat.ApplyBoost(item.damageBoost, item.boostBeats);
                    break;
                default:
                    return;
            }
            s.UseItem(id);
            GameHud.Toast($"Used {item.displayName}");
            if (_gulp == null) _gulp = CombatFeedback.Synth.Chord("Gulp", new[] { 67, 74 }, 0.2f, 18f, bright: false);
            AudioSource.PlayClipAtPoint(_gulp, transform.position, 0.7f);
        }
    }
}
