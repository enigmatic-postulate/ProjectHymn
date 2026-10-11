using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// The village weapon shop (GDD update 18): it sells the weapons play has put on sale (the greatsword after a B or
    /// better), lists the ones you own, and shows your Notes. Potions, tonics and scrolls are gone.
    /// </summary>
    public class ShopMenu : MonoBehaviour
    {
        static ShopMenu _open;
        RectTransform _list;
        Text _notes, _info;
        string _title;

        public static void Open(string title)
        {
            if (_open != null) return;
            _open = new GameObject("Shop").AddComponent<ShopMenu>();
            _open._title = title;
            _open.Build();
        }

        void Build()
        {
            UiState.Push();
            var c = UiKit.Canvas("Shop Canvas", 210, transform);
            var panel = UiKit.Panel("Shop", c.transform, new Vector2(1000f, 720f));
            var col = UiKit.Column(panel, 8f, 30);
            UiKit.Line(col, _title.ToUpperInvariant(), 40, UiKit.Gold, 60f);
            _notes = UiKit.Line(col, "", 30, UiKit.Gold);
            _list = UiKit.Column(col, 8f, 0);
            _list.gameObject.AddComponent<LayoutElement>().preferredHeight = 420f;
            _info = UiKit.Line(col, "", 24, UiKit.Dim, 90f, TextAnchor.UpperLeft);
            Refresh(0);
        }

        void Refresh(int select)
        {
            UiKit.Clear(_list);
            var s = GameSession.Instance;
            _notes.text = $"{s.Data.notes} Notes";
            Button first = null, chosen = null;
            int i = 0;
            if (s.Db != null)
                foreach (var w in s.Db.weapons)
                {
                    if (w == null) continue;
                    bool owned = s.Data.HasWeapon(w.id), forSale = s.Data.weaponsForSale.Contains(w.id);
                    if (!owned && !forSale) continue;
                    string kind = w.weight == WeaponWeight.Heavy ? "heavy" : "light";
                    string label = owned ? $"{w.displayName}   <color=#8a84a0>owned</color>"
                                         : $"{w.displayName}   <color=#ffd56a>{w.price} Notes</color>";
                    var cls = s.Class;
                    string info = $"A {kind} weapon{(w.HasShield ? " with a shield" : "")}, damage x{w.damageScale:0.0#}. " +
                                  (w.weight == WeaponWeight.Heavy ? "The Crusader's kind (no shield)." : "The Warden's kind.") +
                                  (cls != null && !cls.CanEquip(w.id) ? $"\n<color=#6a6478>Your {cls.displayName} can't hold it yet.</color>" : "");
                    var weapon = w;
                    int index = i;
                    var b = UiKit.Button(_list, label, () => Buy(weapon, index));
                    b.gameObject.AddComponent<SelectHint>().OnSelected = () => _info.text = info;
                    if (first == null) first = b;
                    if (i == select) chosen = b;
                    i++;
                }
            if (s.Data.weaponsForSale.Count == 0)
                UiKit.Line(_list, "Nothing new on the rack. Clear a stage with a B or better and I'll have something heavier.", 24, UiKit.Dim, 70f);
            var close = UiKit.Button(_list, "Leave", Close);
            UiKit.Select(chosen != null ? chosen : first != null ? first : close);
        }

        void Buy(WeaponSet w, int index)
        {
            var s = GameSession.Instance;
            if (s.Data.HasWeapon(w.id)) { GameHud.Toast($"You own the {w.displayName}. Equip it from the pause menu."); return; }
            switch (s.BuyWeapon(w))
            {
                case PurchaseResult.Bought: GameHud.Toast($"Bought the {w.displayName}. Equip it from the pause menu (Weapon)."); break;
                case PurchaseResult.TooPoor: GameHud.Toast("Not enough Notes"); break;
                default: GameHud.Toast("Not for sale"); break;
            }
            Refresh(index);
        }

        void Update() { if (UiInput.Cancel) Close(); }

        void Close()
        {
            if (_open != this) return;
            _open = null;
            UiState.Pop();
            Destroy(gameObject);
        }

        void OnDestroy() { if (_open == this) { _open = null; UiState.Pop(); } }
    }
}
