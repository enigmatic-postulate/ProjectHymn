using System;
using System.Collections.Generic;
using System.Text;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// A list to choose from with details for the highlighted entry: the class offer at a level-up, the mentor's
    /// questions and weapon rack, and the stage gate. Without a cancel action there is no Back: the choice must be made.
    /// </summary>
    public class ClassMenu : MonoBehaviour
    {
        public struct Option
        {
            public string id, label, info;
            public Option(string id, string label, string info) { this.id = id; this.label = label; this.info = info; }
        }

        static ClassMenu _open;
        Action<string> _chosen;
        Action _cancelled;
        Text _info;

        public static bool IsOpen => _open != null;

        public static void Open(string title, string subtitle, List<Option> options, string selectId, Action<string> chosen, Action cancelled = null)
        {
            if (_open != null) return;
            _open = new GameObject("Choice Menu").AddComponent<ClassMenu>();
            _open._chosen = chosen;
            _open._cancelled = cancelled;
            _open.Build(title, subtitle, options, selectId);
        }

        void Build(string title, string subtitle, List<Option> options, string selectId)
        {
            UiState.Push();
            var c = UiKit.Canvas("Choice Canvas", 270, transform);
            var panel = UiKit.Panel("Choices", c.transform, new Vector2(1100f, 760f));
            var col = UiKit.Column(panel, 10f, 30);
            UiKit.Line(col, title.ToUpperInvariant(), 40, UiKit.Gold, 60f);
            if (!string.IsNullOrEmpty(subtitle)) UiKit.Line(col, subtitle, 24, UiKit.Dim, 40f);
            Button select = null;
            foreach (var o in options)
            {
                var k = o;
                var b = UiKit.Button(col, k.label, () => Pick(k.id), 34, 66f);
                b.gameObject.AddComponent<SelectHint>().OnSelected = () => _info.text = k.info;
                if (select == null || k.id == selectId) select = b;
            }
            _info = UiKit.Line(col, "", 25, UiKit.Text, 300f, TextAnchor.UpperLeft);
            if (_cancelled != null) UiKit.Button(col, "Back", Cancel, 28, 54f);
            UiKit.Select(select);
        }

        // ---------------- Options ----------------

        /// <summary>The class offer: each class with its perks; the current one marked "keep", the fitting one marked.</summary>
        public static List<Option> ClassOptions(IEnumerable<string> ids, string current, string recommended)
        {
            var db = GameSession.Instance.Db;
            var list = new List<Option>();
            foreach (var id in ids)
            {
                var c = db != null ? db.Class(id) : null;
                if (c == null || c.id != id) continue;
                string mark = id == current ? "   <color=#8a84a0>(keep)</color>" : id == recommended ? "   <color=#ffd56a>(fits how you fought)</color>" : "";
                list.Add(new Option(id, c.displayName + mark, Describe(c)));
            }
            return list;
        }

        public static string Describe(ClassDefinition c)
        {
            var sb = new StringBuilder();
            sb.Append(c.description).Append("\n\n");
            sb.Append($"<color=#ff5a4a>{c.hearts} hearts</color>   damage x{c.damage:0.0#}\n");
            sb.Append($"Perks: {c.PerksText()}\n");
            var names = new List<string>();
            foreach (var w in c.weapons) if (w != null) names.Add(w.displayName);
            sb.Append($"Equips: {(names.Count > 0 ? string.Join(", ", names) : "—")}\n");
            var tree = ProjectHymn.Combat.SequenceLibrary.Tree(c.id);
            if (tree.Count > 0)
            {
                var parts = new List<string>();
                foreach (var e in tree)
                    parts.Add($"{e.Name} {e.NotesText}{(e.Meter != ProjectHymn.Combat.SequenceLibrary.Common ? $" ({e.Meter})" : "")}" +
                              (Sequences.Knows(e.Key) ? "" : " <color=#ffd56a>new</color>"));
                sb.Append($"Teaches: {string.Join(", ", parts)}");
            }
            return sb.ToString();
        }

        /// <summary>The weapon rack: every weapon, locked ones and ones the class can't hold greyed in the details.</summary>
        public static List<Option> WeaponOptions(out string selectId)
        {
            var s = GameSession.Instance;
            var cls = s.Class;
            var list = new List<Option>();
            selectId = s.Data.weaponId;
            if (s.Db == null) return list;
            foreach (var w in s.Db.weapons)
            {
                if (w == null) continue;
                bool unlocked = s.Data.HasWeapon(w.id), allowed = cls == null || cls.CanEquip(w.id);
                string mark = w.id == s.Data.weaponId ? "   <color=#8a84a0>(equipped)</color>"
                            : !unlocked ? "   <color=#6a6478>(locked)</color>"
                            : !allowed ? "   <color=#6a6478>(not for your class)</color>" : "";
                var sb = new StringBuilder();
                sb.Append(w.twoHanded || string.IsNullOrEmpty(w.offHandObject) ? "Two hands on the hilt." : "Blade and shield.");
                sb.Append($"   {(w.weight == WeaponWeight.Heavy ? "Heavy" : "Light")}   damage x{w.damageScale:0.0#}\n");
                if (!unlocked) sb.Append(s.Data.weaponsForSale.Contains(w.id) ? $"<color=#ffd56a>For sale in the shop ({w.price} Notes).</color>\n"
                                       : w.id == Progression.ProgressionRules.Greatsword ? "<color=#ffd56a>Clear a stage with a B or better and the shop will stock it.</color>\n" : "<color=#6a6478>Locked.</color>\n");
                else if (!allowed) sb.Append($"<color=#6a6478>The {cls.displayName} can't equip it.</color>\n");
                sb.Append("\nSequences:");
                foreach (var q in w.sequences)
                    if (q != null) sb.Append($"\n<color=#ffd56a>{q.displayName}</color>  {q.NotesText}");
                list.Add(new Option(w.id, w.displayName + mark, sb.ToString()));
            }
            return list;
        }

        void Pick(string id) { var cb = _chosen; Close(); cb?.Invoke(id); }
        void Cancel() { if (_cancelled == null) return; var cb = _cancelled; Close(); cb?.Invoke(); }
        void Update() { if (UiInput.Cancel) Cancel(); }

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
