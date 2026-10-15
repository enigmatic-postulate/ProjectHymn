using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>The title screen: Continue (the newest slot), New Game (pick a slot, the intro, the village), Load Game, Settings, Quit.</summary>
    public class TitleMenu : MonoBehaviour
    {
        RectTransform _body;
        GameSession _session;
        Text _status;
        RectTransform _logo;
        Canvas _canvas;
        static readonly int BeatId = Shader.PropertyToID("_HymnBeat");

        void Start()
        {
            UiState.Reset();
            Time.timeScale = 1f;
            _session = GameSession.ForTitle();
            var c = UiKit.Canvas("Title Canvas", 100, transform);
            _canvas = c;
            // VS-4: the painted logo (Tools/paint_logo.py), breathing with the music; plain text if it's missing.
            var tex = Resources.Load<Texture2D>("UI/HymnLogo");
            if (tex != null)
            {
                _logo = UiKit.Rect("Logo", c.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                _logo.pivot = new Vector2(0.5f, 1f);
                _logo.sizeDelta = new Vector2(1300f, 1300f * tex.height / tex.width);
                _logo.anchoredPosition = new Vector2(0f, -30f);
                var img = _logo.gameObject.AddComponent<Image>();
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                img.raycastTarget = false;
            }
            else
            {
                var title = UiKit.Label(c.transform, "PROJECT HYMN", 110, TextAnchor.UpperCenter, UiKit.Gold);
                ((RectTransform)title.transform).offsetMax = new Vector2(0f, -90f);
                title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            }
            var sub = UiKit.Label(c.transform, "every blow on the beat", 34, TextAnchor.UpperCenter, UiKit.Text);
            ((RectTransform)sub.transform).offsetMax = new Vector2(0f, tex != null ? -400f : -220f);
            sub.gameObject.AddComponent<Outline>();
            _status = UiKit.Label(c.transform, "", 26, TextAnchor.LowerCenter, UiKit.Dim);
            ((RectTransform)_status.transform).offsetMin = new Vector2(0f, 40f);
            _body = UiKit.Panel("Menu", c.transform, new Vector2(560f, 500f), new Vector2(-560f, -160f));
            Main();
        }

        void Update()
        {
            if (_logo != null) _logo.localScale = Vector3.one * (1f + 0.012f * Shader.GetGlobalFloat(BeatId));
        }

        static readonly Vector2 MainSize = new Vector2(566f, 506f), SlotsSize = new Vector2(1060f, 560f);

        /// <summary>Resize the menu panel: the main menu sits left of the heroes, the wide slot list in the middle.</summary>
        void Size(Vector2 size)
        {
            var frame = (RectTransform)_body.parent;
            frame.sizeDelta = size;
            frame.anchoredPosition = new Vector2(size.x > 700f ? 0f : -560f, -160f);
        }

        void Main()
        {
            UiKit.Clear(_body);
            Size(MainSize);
            var col = UiKit.Column(_body, 12f, 30);
            bool any = GameSession.HasSave;
            int latest = GameSession.LatestSlot();
            var cont = UiKit.Button(col, latest > 0 ? $"Continue   <color=#8a84a0>slot {latest}</color>" : "Continue", () =>
            {
                if (!_session.Continue()) _status.text = "The save file could not be read.";
            }, 34, 70f);
            cont.interactable = any;
            var first = UiKit.Button(col, "New Game", () => Slots(newGame: true), 34, 70f);
            var load = UiKit.Button(col, "Load Game", () => Slots(newGame: false), 34, 70f);
            load.interactable = any;
            UiKit.Button(col, "Settings", Settings, 34, 70f);
            UiKit.Button(col, "Quit", Quit, 34, 70f);
            UiKit.Select(any ? cont : first);
        }

        /// <summary>
        /// The three save slots (VS-5 Part B): for a new game pick any slot (a used one asks before it's replaced); to load,
        /// pick a used one. Each shows its level, class, verses home, play time and when it was saved.
        /// </summary>
        void Slots(bool newGame)
        {
            UiKit.Clear(_body);
            Size(SlotsSize);
            var col = UiKit.Column(_body, 12f, 30);
            UiKit.Line(col, newGame ? "NEW GAME: CHOOSE A SLOT" : "LOAD GAME", 34, UiKit.Gold, 56f);
            Button select = null;
            int latest = GameSession.LatestSlot();
            for (int i = 1; i <= SaveSlots.Count; i++)
            {
                int slot = i;
                var data = GameSession.Peek(slot);
                var at = GameSession.SavedAt(slot);
                string label = data == null
                    ? (GameSession.SlotUsed(slot) ? $"Slot {slot}   <color=#ff7a6a>unreadable</color>" : $"Slot {slot}   <color=#6a6478>empty</color>")
                    : $"Slot {slot}   {SaveSlots.Summary(data, _session.ClassName(data.classId))}   <color=#8a84a0>{at:MMM d, HH:mm}</color>";
                var b = UiKit.Button(col, label, () =>
                {
                    if (newGame)
                    {
                        if (GameSession.SlotUsed(slot)) ConfirmOverwrite(slot);
                        else StartStory(slot);
                    }
                    else if (!_session.LoadSlot(slot)) _status.text = $"Slot {slot} could not be read.";
                }, 28, 84f);
                if (!newGame && data == null) b.interactable = false;
                if (select == null && b.interactable && (newGame ? !GameSession.SlotUsed(slot) : slot == latest)) select = b;
                if (select == null && newGame && slot == SaveSlots.Count) select = b;
            }
            var back = UiKit.Button(col, "Back", Main, 30, 60f);
            UiKit.Select(select != null ? select : back);
        }

        void ConfirmOverwrite(int slot)
        {
            UiKit.Clear(_body);
            Size(MainSize);
            var col = UiKit.Column(_body, 12f, 30);
            UiKit.Line(col, $"Start over in slot {slot}? Its saved game will be replaced.", 28, UiKit.Text, 110f, TextAnchor.MiddleCenter);
            UiKit.Button(col, "Start a new game", () => StartStory(slot), 32, 66f);
            var back = UiKit.Button(col, "Back", () => Slots(newGame: true), 32, 66f);
            UiKit.Select(back);
        }

        /// <summary>The intro cards (VS-5), then a new game in the slot.</summary>
        void StartStory(int slot)
        {
            _canvas.gameObject.SetActive(false);
            StoryCards.Play(Progression.Story.Intro, () => _session.NewGame(slot));
        }

        void Settings()
        {
            UiKit.Clear(_body);
            Size(new Vector2(900f, 720f));
            var col = UiKit.Column(_body, 10f, 30);
            UiKit.Select(SettingsPanel.Build(col, Main, null));
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
