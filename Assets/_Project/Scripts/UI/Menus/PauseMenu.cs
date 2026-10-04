using System.Text;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Esc / Start: pauses the music on its clock and the world, with Resume, Songbook (the sequences you know and the
    /// lore you found), Items (pick the quick-slot item), Settings, Return to the village and Quit to the title.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] bool allowReturnToVillage = true;

        PlayerInputReader _input;
        Conductor _conductor;
        GameObject _root;
        RectTransform _body;
        bool _open;
        float _openedAt;

        void Start()
        {
            _input = FindAnyObjectByType<PlayerInputReader>();
            _conductor = FindAnyObjectByType<Conductor>();
        }

        void Update()
        {
            if (_input == null) _input = FindAnyObjectByType<PlayerInputReader>();
            bool toggle = _input != null && _input.PausePressed;
            if (!_open)
            {
                if (toggle && !UiState.Blocking && !UiState.JustClosed && !GameSession.Instance.Travelling) Open();
                return;
            }
            if (Time.unscaledTime - _openedAt > 0.1f && UiInput.Cancel) Main();
        }

        void Open()
        {
            _open = true;
            _onMain = false;
            _openedAt = Time.unscaledTime;
            UiState.Push();
            Time.timeScale = 0f;
            if (_conductor != null) _conductor.Pause();
            var c = UiKit.Canvas("Pause Canvas", 300, transform);
            _root = c.gameObject;
            var panel = UiKit.Panel("Pause", c.transform, new Vector2(1000f, 820f));
            _body = panel;
            Main();
        }

        void Close()
        {
            if (!_open) return;
            _open = false;
            Destroy(_root);
            Time.timeScale = 1f;
            if (_conductor != null) _conductor.Resume();
            UiState.Pop();
        }

        RectTransform Page()
        {
            UiKit.Clear(_body);
            return UiKit.Column(_body, 10f, 34);
        }

        bool _onMain;

        void Main()
        {
            if (!_open) return;
            if (_onMain && Time.unscaledTime - _openedAt > 0.1f) { Close(); return; }     // Cancel on the main page resumes
            _onMain = true;
            var col = Page();
            UiKit.Line(col, "PAUSED", 46, UiKit.Gold, 70f);
            var resume = UiKit.Button(col, "Resume", Close);
            UiKit.Button(col, "Songbook", Songbook);
            UiKit.Button(col, "Items", Items);
            UiKit.Button(col, "Settings", Settings);
            var s = GameSession.Instance;
            bool inHub = s.Db != null && s.SceneName == s.Db.hubScene;
            if (allowReturnToVillage && !inHub && s.Db != null)
                UiKit.Button(col, "Return to the village", () => { Close(); s.Travel(s.Db.hubScene, "FromCliffside", saveOnArrival: true); });
            UiKit.Button(col, "Quit to title", () => { Close(); s.ToTitle(); });
            UiKit.Line(col, s.FreePlay ? "Free play (started from the editor): nothing is saved." : "Rest at the inn to save. Returning to the village saves too.", 22, UiKit.Dim, 40f);
            UiKit.Select(resume);
        }

        void Songbook()
        {
            _onMain = false;
            var col = Page();
            UiKit.Line(col, "SONGBOOK", 40, UiKit.Gold, 60f);
            var s = GameSession.Instance;
            var cls = s.Class;
            var sb = new StringBuilder();
            if (cls != null && cls.weapon != null)
            {
                sb.Append($"<color=#8a84a0>{cls.displayName} — play notes on consecutive beats; 1 = light (J / X), 2 = heavy (K / Y)</color>\n\n");
                foreach (var q in cls.weapon.sequences)
                {
                    if (q == null) continue;
                    if (s.FreePlay || s.Data.Knows(q.key))
                    {
                        var moves = new StringBuilder();
                        foreach (var m in q.moves) { if (moves.Length > 0) moves.Append(", "); moves.Append(m != null ? m.displayName : "?"); }
                        sb.Append($"<color=#ffd56a>{q.displayName}</color>   <b>{q.NotesText}</b>   finisher ×{q.finisherMultiplier:0.#}\n<color=#b8b2c8>{moves}</color>\n\n");
                    }
                    else sb.Append($"<color=#6a6478>???   {new string('·', q.notes.Count)}   (find or buy its scroll)</color>\n\n");
                }
            }
            UiKit.Line(col, sb.ToString(), 26, UiKit.Text, 360f, TextAnchor.UpperLeft);
            UiKit.Line(col, $"LORE  ({s.Data.lore.Count} found)", 30, UiKit.Gold, 46f);
            Button first = null;
            if (s.Db != null)
                foreach (var id in s.Data.lore)
                {
                    var item = s.Db.Item(id);
                    if (item == null) continue;
                    var b = UiKit.Button(col, item.displayName, () => DialogueBox.Show(item.displayName, new[] { item.loreText }, null), 26, 48f);
                    if (first == null) first = b;
                }
            var back = UiKit.Button(col, "Back", Main);
            UiKit.Select(first != null ? first : back);
        }

        void Items()
        {
            _onMain = false;
            var col = Page();
            UiKit.Line(col, "ITEMS", 40, UiKit.Gold, 60f);
            var s = GameSession.Instance;
            UiKit.Line(col, $"♪ {s.Data.notes} Notes      choose the item for the quick slot (E / B uses it)", 24, UiKit.Dim, 40f);
            Button first = null;
            foreach (var it in s.Data.items)
            {
                var def = s.Db != null ? s.Db.Item(it.id) : null;
                string name = def != null ? def.displayName : it.id;
                string mark = it.id == s.Data.quickItem ? "   <color=#ffd56a>in quick slot</color>" : "";
                string id = it.id;
                var b = UiKit.Button(col, $"{name} ×{it.count}{mark}", () => { s.Data.quickItem = id; s.NotifyChanged(); Items(); });
                if (def != null) b.gameObject.AddComponent<SelectHint>();
                if (first == null) first = b;
            }
            if (s.Data.items.Count == 0) UiKit.Line(col, "Nothing yet. The village shop sells potions and tonics.", 26, UiKit.Dim);
            var back = UiKit.Button(col, "Back", Main);
            UiKit.Select(first != null ? first : back);
        }

        void Settings()
        {
            _onMain = false;
            var col = Page();
            var first = SettingsPanel.Build(col, Main, () =>
            {
                Close();
                var cal = FindAnyObjectByType<CalibrationPanel>();
                if (cal != null) cal.Begin();
                else GameHud.Toast("Calibrate in a stage (it needs the music)");
            });
            UiKit.Select(first);
        }

        void OnDestroy() { if (_open) { Time.timeScale = 1f; UiState.Pop(); } }
    }
}
