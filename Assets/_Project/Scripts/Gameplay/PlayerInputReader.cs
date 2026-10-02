using UnityEngine;
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Reads movement and the four beat verbs from gamepad or keyboard. Works with either the
    /// Input System package (default) or the legacy Input Manager.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public bool StrikePressed { get; private set; }
        public bool SmashPressed { get; private set; }
        public bool StepPressed { get; private set; }
        public bool GuardPressed { get; private set; }
        public bool GuardHeld { get; private set; }
        /// <summary>Esc / Start: pause menu.</summary>
        public bool PausePressed { get; private set; }
        /// <summary>E or J / X: talk, open, enter (town).</summary>
        public bool InteractPressed { get; private set; }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                move.x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                move.y = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
            }
            if (gp != null && gp.leftStick.ReadValue().sqrMagnitude > 0.04f) move = gp.leftStick.ReadValue();
            Move = Vector2.ClampMagnitude(move, 1f);

            StrikePressed = (kb?.jKey.wasPressedThisFrame ?? false) || (gp?.buttonWest.wasPressedThisFrame ?? false);
            SmashPressed = (kb?.kKey.wasPressedThisFrame ?? false) || (gp?.buttonNorth.wasPressedThisFrame ?? false);
            StepPressed = (kb?.spaceKey.wasPressedThisFrame ?? false) || (gp?.buttonSouth.wasPressedThisFrame ?? false);
            GuardPressed = (kb?.lKey.wasPressedThisFrame ?? false) || (gp?.buttonEast.wasPressedThisFrame ?? false);
            GuardHeld = (kb?.lKey.isPressed ?? false) || (gp?.buttonEast.isPressed ?? false);
            PausePressed = (kb?.escapeKey.wasPressedThisFrame ?? false) || (gp?.startButton.wasPressedThisFrame ?? false);
            InteractPressed = (kb?.eKey.wasPressedThisFrame ?? false) || StrikePressed;
#else
            Move = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            StrikePressed = Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.JoystickButton2);
            SmashPressed = Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton3);
            StepPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
            GuardPressed = Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton1);
            GuardHeld = Input.GetKey(KeyCode.L) || Input.GetKey(KeyCode.JoystickButton1);
            PausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
            InteractPressed = Input.GetKeyDown(KeyCode.E) || StrikePressed;
#endif
        }
    }
}
