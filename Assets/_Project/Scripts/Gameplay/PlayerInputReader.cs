using UnityEngine;
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Reads the M2 sword prototype controls from gamepad or keyboard (Input System package or legacy Input Manager):
    /// move, note 1 (light: J / X), note 2 (heavy: K / Y), jump (Space / A), Step dash (Shift / RB), weapon swap (Tab / Select),
    /// animation offset ([ and ] / D-pad left and right) and test tempos (F1 to F4).
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        /// <summary>Note 1, the light button.</summary>
        public bool StrikePressed { get; private set; }
        /// <summary>Note 2, the heavy button.</summary>
        public bool SmashPressed { get; private set; }
        public bool StepPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool SwapWeaponPressed { get; private set; }
        /// <summary>-1 / +1 when the animation offset keys are pressed this frame.</summary>
        public int OffsetNudge { get; private set; }
        /// <summary>0 = none, 1..4 = F1..F4 (song, 90, 130, 170 BPM).</summary>
        public int TempoKey { get; private set; }

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
            StepPressed = (kb?.leftShiftKey.wasPressedThisFrame ?? false) || (gp?.rightShoulder.wasPressedThisFrame ?? false);
            JumpPressed = (kb?.spaceKey.wasPressedThisFrame ?? false) || (gp?.buttonSouth.wasPressedThisFrame ?? false);
            JumpHeld = (kb?.spaceKey.isPressed ?? false) || (gp?.buttonSouth.isPressed ?? false);
            SwapWeaponPressed = (kb?.tabKey.wasPressedThisFrame ?? false) || (gp?.selectButton.wasPressedThisFrame ?? false);
            OffsetNudge = ((kb?.rightBracketKey.wasPressedThisFrame ?? false) || (gp?.dpad.right.wasPressedThisFrame ?? false) ? 1 : 0)
                        - ((kb?.leftBracketKey.wasPressedThisFrame ?? false) || (gp?.dpad.left.wasPressedThisFrame ?? false) ? 1 : 0);
            TempoKey = kb == null ? 0 : kb.f1Key.wasPressedThisFrame ? 1 : kb.f2Key.wasPressedThisFrame ? 2
                     : kb.f3Key.wasPressedThisFrame ? 3 : kb.f4Key.wasPressedThisFrame ? 4 : 0;
#else
            Move = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            StrikePressed = Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.JoystickButton2);
            SmashPressed = Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton3);
            StepPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.JoystickButton5);
            JumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
            JumpHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
            SwapWeaponPressed = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton6);
            OffsetNudge = (Input.GetKeyDown(KeyCode.RightBracket) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftBracket) ? 1 : 0);
            TempoKey = Input.GetKeyDown(KeyCode.F1) ? 1 : Input.GetKeyDown(KeyCode.F2) ? 2 : Input.GetKeyDown(KeyCode.F3) ? 3 : Input.GetKeyDown(KeyCode.F4) ? 4 : 0;
#endif
        }
    }
}
