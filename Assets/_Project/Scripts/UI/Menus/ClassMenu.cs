using System;
using System.Text;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>Choose a class (title screen and the village trainer): hearts, damage, guard, starting sequences.</summary>
    public class ClassMenu : MonoBehaviour
    {
        static ClassMenu _open;
        Action<string> _chosen;
        Action _cancelled;
        Text _info;

        public static bool IsOpen => _open != null;

        public static void Open(string title, string current, Action<string> chosen, Action cancelled = null)
        {
            if (_open != null) return;
            _open = new GameObject("Class Menu").AddComponent<ClassMenu>();
            _open._chosen = chosen;
            _open._cancelled = cancelled;
            _open.Build(title, current);
        }

        void Build(string title, string current)
        {
            UiState.Push();
            var c = UiKit.Canvas("Class Canvas", 220, transform);
            var panel = UiKit.Panel("Classes", c.transform, new Vector2(1100f, 700f));
            var col = UiKit.Column(panel, 10f, 30);
            UiKit.Line(col, title.ToUpperInvariant(), 40, UiKit.Gold, 60f);
            var db = GameSession.Instance.Db;
            Button select = null;
            if (db != null)
                foreach (var cls in db.classes)
                {
                    if (cls == null) continue;
                    var k = cls;
                    string mark = cls.id == current ? "   <color=#8a84a0>(current)</color>" : "";
                    var b = UiKit.Button(col, $"{cls.displayName}{mark}", () => Pick(k.id), 34, 70f);
                    b.gameObject.AddComponent<SelectHint>().OnSelected = () => _info.text = Describe(k);
                    if (select == null || cls.id == current) select = b;
                }
            _info = UiKit.Line(col, "", 26, UiKit.Text, 330f, TextAnchor.UpperLeft);
            UiKit.Button(col, "Back", Cancel, 28, 54f);
            UiKit.Select(select);
        }

        static string Describe(ClassDefinition c)
        {
            var sb = new StringBuilder();
            sb.Append(c.description).Append("\n\n");
            sb.Append($"<color=#ff5a4a>{new string('♥', c.hearts)}</color>   damage ×{c.damage:0.0#}   ");
            sb.Append("guard: Perfect parries, Great or Good blocks").Append("\n");
            if (c.weapon != null)
                foreach (var s in c.weapon.sequences)
                    if (s != null)
                        sb.Append($"\n<color=#ffd56a>{s.displayName}</color>  {s.NotesText}{(c.startingSequences.Contains(s.key) ? "" : "   <color=#8a84a0>(learn from a scroll)</color>")}");
            return sb.ToString();
        }

        void Pick(string id) { var cb = _chosen; Close(); cb?.Invoke(id); }
        void Cancel() { var cb = _cancelled; Close(); cb?.Invoke(); }
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
