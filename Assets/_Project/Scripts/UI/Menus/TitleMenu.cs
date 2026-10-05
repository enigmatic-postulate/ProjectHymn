using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>The title screen: New Game (choose a class), Continue, Settings, Quit.</summary>
    public class TitleMenu : MonoBehaviour
    {
        RectTransform _body;
        GameSession _session;
        Text _status;

        void Start()
        {
            UiState.Reset();
            Time.timeScale = 1f;
            _session = GameSession.ForTitle();
            var c = UiKit.Canvas("Title Canvas", 100, transform);
            var title = UiKit.Label(c.transform, "PROJECT HYMN", 110, TextAnchor.UpperCenter, UiKit.Gold);
            ((RectTransform)title.transform).offsetMax = new Vector2(0f, -90f);
            title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            var sub = UiKit.Label(c.transform, "every blow on the beat", 34, TextAnchor.UpperCenter, UiKit.Text);
            ((RectTransform)sub.transform).offsetMax = new Vector2(0f, -220f);
            sub.gameObject.AddComponent<Outline>();
            _status = UiKit.Label(c.transform, "", 26, TextAnchor.LowerCenter, UiKit.Dim);
            ((RectTransform)_status.transform).offsetMin = new Vector2(0f, 40f);
            _body = UiKit.Panel("Menu", c.transform, new Vector2(560f, 420f), new Vector2(-560f, -160f));
            Main();
        }

        void Main()
        {
            UiKit.Clear(_body);
            var col = UiKit.Column(_body, 12f, 30);
            var first = UiKit.Button(col, "New Game", NewGame, 34, 70f);
            var cont = UiKit.Button(col, "Continue", () => { if (!_session.Continue()) _status.text = "The save file could not be read."; }, 34, 70f);
            cont.interactable = GameSession.HasSave;
            UiKit.Button(col, "Settings", Settings, 34, 70f);
            UiKit.Button(col, "Quit", Quit, 34, 70f);
            UiKit.Select(GameSession.HasSave ? cont : first);
        }

        void NewGame() =>
            _session.NewGame();

        void Settings()
        {
            UiKit.Clear(_body);
            ((RectTransform)_body.parent).sizeDelta = new Vector2(900f, 720f);
            var col = UiKit.Column(_body, 10f, 30);
            UiKit.Select(SettingsPanel.Build(col, () => { ((RectTransform)_body.parent).sizeDelta = new Vector2(566f, 426f); Main(); }, null));
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
