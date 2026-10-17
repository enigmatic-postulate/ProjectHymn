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

        /// <summary>Step a setting down (-1) or up (+1) this frame: left / right arrows, A / D, or the d-pad; 0 when neither.</summary>
        public static int Horizontal
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
                var kb = Keyboard.current; var gp = Gamepad.current;
                bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (gp != null && gp.dpad.left.wasPressedThisFrame);
                bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (gp != null && gp.dpad.right.wasPressedThisFrame);
#else
                bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
                bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
#endif
                return left == right ? 0 : left ? -1 : 1;
            }
        }

        /// <summary>The quick volume keys, any time: minus (or numpad minus) is -1, equals / plus (or numpad plus) is +1; 0 when neither.</summary>
        public static int VolumeKey
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb == null) return 0;
                bool down = kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame;
                bool up = kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame;
#else
                bool down = Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus);
                bool up = Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus);
#endif
                return down == up ? 0 : down ? -1 : 1;
            }
        }

        /// <summary>Advance dialogue: Enter, Space, J, E, W / up, or A / X on a pad.</summary>
        public static bool Advance
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
                var kb = Keyboard.current; var gp = Gamepad.current;
                return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                    || (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame));
#else
                return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.E)
                    || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.JoystickButton0);
#endif
            }
        }
    }
}
