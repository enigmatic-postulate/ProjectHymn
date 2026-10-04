using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>The village shop: the stock with prices, what you hold, and your Notes. Buying a scroll learns it.</summary>
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
            _info = UiKit.Line(col, "", 24, UiKit.Dim, 70f, TextAnchor.UpperLeft);
            Refresh(0);
        }

        void Refresh(int select)
        {
            UiKit.Clear(_list);
            var s = GameSession.Instance;
            _notes.text = $"♪ {s.Data.notes} Notes";
            Button first = null, chosen = null;
            int i = 0;
            if (s.Db != null)
                foreach (var item in s.Db.shopStock)
                {
                    if (item == null) continue;
                    string label;
                    if (item.kind == ItemKind.Scroll)
                    {
                        var seq = SequenceInfo.Find(item.teaches);
                        string notes = seq != null ? "  " + seq.NotesText : "";
                        label = s.Data.Knows(item.teaches) ? $"{item.displayName}{notes}   <color=#8a84a0>known</color>"
                                                           : $"{item.displayName}{notes}   <color=#ffd56a>{item.price} ♪</color>";
                    }
                    else label = $"{item.displayName}   <color=#ffd56a>{item.price} ♪</color>   <color=#8a84a0>have {s.Data.Count(item.id)}</color>";
                    var it = item;
                    int index = i;
                    var b = UiKit.Button(_list, label, () => Buy(it, index));
                    var trig = b.gameObject.AddComponent<SelectHint>();
                    trig.OnSelected = () => _info.text = it.description;
                    if (first == null) first = b;
                    if (i == select) chosen = b;
                    i++;
                }
            var close = UiKit.Button(_list, "Leave", Close);
            UiKit.Select(chosen != null ? chosen : first != null ? first : close);
        }

        void Buy(ItemDefinition item, int index)
        {
            var r = GameSession.Instance.Buy(item);
            switch (r)
            {
                case PurchaseResult.Bought:
                    GameHud.Toast(item.kind == ItemKind.Scroll ? $"Learned {item.displayName.Replace("Scroll: ", "")}" : $"Bought {item.displayName}");
                    break;
                case PurchaseResult.TooPoor: GameHud.Toast("Not enough Notes"); break;
                case PurchaseResult.AlreadyKnown: GameHud.Toast("You already know that song"); break;
                default: GameHud.Toast("You can't carry more"); break;
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
