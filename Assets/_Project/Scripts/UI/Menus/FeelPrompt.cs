using System;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// "How did that feel?" after a stage (VS-5 Part C, playtests): five answers from rough to great, or skip, or stop
    /// asking. Number keys 1 to 5 answer straight away. The answer goes into the run's log.
    /// </summary>
    public class FeelPrompt : MonoBehaviour
    {
        static FeelPrompt _open;
        Action<int> _done;
        float _openedAt;

        static readonly string[] Labels = { "1   Rough", "2   Meh", "3   Okay", "4   Good", "5   Great" };

        public static void Ask(Action<int> done)
        {
            if (_open != null || !PlaytestLogs.AskFeel) { done?.Invoke(0); return; }
            _open = new GameObject("Feel Prompt").AddComponent<FeelPrompt>();
            _open.Build(done);
        }

        void Build(Action<int> done)
        {
            UiState.Push();
            _done = done;
            _openedAt = Time.unscaledTime;
            var c = UiKit.Canvas("Feel Canvas", 280, transform);
            var panel = UiKit.Panel("Feel", c.transform, new Vector2(620f, 640f));
            var col = UiKit.Column(panel, 8f, 28);
            UiKit.Line(col, "HOW DID THAT FEEL?", 36, UiKit.Gold, 56f);
            UiKit.Line(col, "For the playtest log. Number keys answer.", 22, UiKit.Dim, 34f);
            Button first = null;
            for (int i = 0; i < Labels.Length; i++)
            {
                int v = i + 1;
                var b = UiKit.Button(col, Labels[i], () => Answer(v), 30, 56f);
                if (v == 3) first = b;
            }
            UiKit.Button(col, "Skip", () => Answer(0), 26, 48f);
            UiKit.Button(col, "Stop asking (Shift+F9 turns it back on)", () => { PlaytestLogs.AskFeel = false; Answer(0); }, 22, 44f);
            UiKit.Select(first);
        }

        void Update()
        {
            if (Time.unscaledTime - _openedAt < 0.2f) return;
            if (UiInput.Cancel) { Answer(0); return; }
            for (int v = 1; v <= 5; v++) if (Digit(v)) { Sfx.Play(Sfx.MenuConfirm, 0.7f); Answer(v); return; }
        }

        static bool Digit(int v)
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            switch (v)
            {
                case 1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
                case 2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
                case 3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
                case 4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
                default: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
            }
#else
            return Input.GetKeyDown(KeyCode.Alpha0 + v) || Input.GetKeyDown(KeyCode.Keypad0 + v);
#endif
        }

        void Answer(int v)
        {
            if (_open != this) return;
            _open = null;
            UiState.Pop();
            var done = _done;
            _done = null;
            Destroy(gameObject);
            done?.Invoke(v);
        }

        void OnDestroy() { if (_open == this) { _open = null; UiState.Pop(); } }
    }
}
