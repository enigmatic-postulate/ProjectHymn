using UnityEngine;
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectHymn.Gameplay
{
    /// <summary>Menu keys read directly (they must work while gameplay input is blocked).</summary>
    public static class UiInput
    {
        /// <summary>Back out of a menu: Esc, Backspace, or B / Start on a pad.</summary>
        public static bool Cancel
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
                var kb = Keyboard.current; var gp = Gamepad.current;
                return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
                    || (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.startButton.wasPressedThisFrame));
#else
                return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1);
#endif
            }
        }

        /// <summary>Advance dialogue: Enter, Space, J, W / up, or A / X on a pad.</summary>
        public static bool Advance
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
                var kb = Keyboard.current; var gp = Gamepad.current;
                return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                    || (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame));
#else
                return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)
                    || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.JoystickButton0);
#endif
            }
        }
    }
}
