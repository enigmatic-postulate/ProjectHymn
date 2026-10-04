using System;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>A speech box along the bottom of the screen; lines type out and advance on Enter, Space, J, up or A.</summary>
    public class DialogueBox : MonoBehaviour
    {
        static DialogueBox _open;

        Text _name, _body, _hint;
        string[] _lines;
        int _line;
        float _shown;
        Action _done;
        float _openedAt;

        public static bool IsOpen => _open != null;

        public static void Show(string speaker, string[] lines, Action onDone)
        {
            if (_open != null) return;
            var go = new GameObject("Dialogue");
            _open = go.AddComponent<DialogueBox>();
            _open.Build(speaker, lines ?? new string[0], onDone);
        }

        void Build(string speaker, string[] lines, Action onDone)
        {
            UiState.Push();
            _lines = lines.Length > 0 ? lines : new[] { "..." };
            _done = onDone;
            _openedAt = Time.unscaledTime;
            var c = UiKit.Canvas("Dialogue Canvas", 200, transform);
            var frame = UiKit.Rect("Frame", c.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            frame.pivot = new Vector2(0.5f, 0f);
            frame.sizeDelta = new Vector2(1500f, 250f);
            frame.anchoredPosition = new Vector2(0f, 40f);
            frame.gameObject.AddComponent<Image>().color = UiKit.Frame;
            var inner = UiKit.Rect("Inner", frame, Vector2.zero, Vector2.one, Vector2.one * 3f, -Vector2.one * 3f);
            inner.gameObject.AddComponent<Image>().color = UiKit.PanelColor;
            _name = UiKit.Label(inner, speaker, 32, TextAnchor.UpperLeft, UiKit.Gold);
            ((RectTransform)_name.transform).offsetMin = new Vector2(34f, 0f);
            ((RectTransform)_name.transform).offsetMax = new Vector2(-34f, -18f);
            _body = UiKit.Label(inner, "", 32, TextAnchor.UpperLeft);
            ((RectTransform)_body.transform).offsetMin = new Vector2(34f, 30f);
            ((RectTransform)_body.transform).offsetMax = new Vector2(-34f, -66f);
            _hint = UiKit.Label(inner, "", 22, TextAnchor.LowerRight, UiKit.Dim);
            ((RectTransform)_hint.transform).offsetMin = new Vector2(34f, 14f);
            ((RectTransform)_hint.transform).offsetMax = new Vector2(-30f, 0f);
        }

        void Update()
        {
            string line = _lines[_line];
            _shown = Mathf.Min(line.Length, _shown + Time.unscaledDeltaTime * 60f);
            _body.text = line.Substring(0, (int)_shown);
            bool full = (int)_shown >= line.Length;
            _hint.text = full ? (_line < _lines.Length - 1 ? "▼ next" : "▼ close") : "";
            if (Time.unscaledTime - _openedAt < 0.15f) return;     // the press that opened it doesn't skip it
            if (!UiInput.Advance && !UiInput.Cancel) return;
            if (!full) { _shown = line.Length; return; }
            if (++_line < _lines.Length) { _shown = 0f; return; }
            Close();
        }

        void Close()
        {
            _open = null;
            UiState.Pop();
            Destroy(gameObject);
            _done?.Invoke();
        }

        void OnDestroy() { if (_open == this) { _open = null; UiState.Pop(); } }
    }
}
