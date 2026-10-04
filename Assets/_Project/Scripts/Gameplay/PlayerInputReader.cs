using UnityEngine;
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Reads the M2 sword prototype controls from gamepad or keyboard (Input System package or legacy Input Manager):
    /// move, note 1 (light: J / X), note 2 (heavy: K / Y), jump (Space / A), guard (L / LB), dodge (Shift / RT), weapon swap (Tab / Select),
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
        /// <summary>Guard: parry an enemy hit by pressing on its beat (L / LB).</summary>
        public bool GuardPressed { get; private set; }
        /// <summary>Dodge: a roll with invulnerable frames (Left Shift / RT). Replaces the dash.</summary>
        public bool DodgePressed { get; private set; }

        // When each timed press actually happened, on the Time.realtimeSinceStartupAsDouble clock (Input System
        // event time). Judging by these instead of the frame removes up to a frame of error. NaN = not pressed.
        public double StrikeRealtime { get; private set; } = double.NaN;
        public double SmashRealtime { get; private set; } = double.NaN;
        public double GuardRealtime { get; private set; } = double.NaN;
        public double DodgeRealtime { get; private set; } = double.NaN;
        /// <summary>Talk / use a door: up (W, Up arrow, stick up).</summary>
        public bool InteractPressed { get; private set; }
        /// <summary>Use the quick-slot item (E / B).</summary>
        public bool UseItemPressed { get; private set; }
        /// <summary>Next item in the quick slot (Q / D-pad down).</summary>
        public bool CycleItemPressed { get; private set; }
        /// <summary>Open or close the pause menu (Esc / Start). Reported even while a menu blocks play.</summary>
        public bool PausePressed { get; private set; }

        float _lastMoveY;
        public bool SwapWeaponPressed { get; private set; }
        /// <summary>-1 / +1 when the animation offset keys are pressed this frame.</summary>
        public int OffsetNudge { get; private set; }
        /// <summary>0 = none, 1..4 = F1..F4 (song, 90, 130, 170 BPM).</summary>
        public int TempoKey { get; private set; }

#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
        // Timed buttons go through actions so each press keeps its own event time.
        InputAction _note1, _note2, _guard, _dodge;
        double _note1At, _note2At, _guardAt, _dodgeAt;

        static InputAction Button(string a, string b)
        {
            var act = new InputAction(type: InputActionType.Button);
            act.AddBinding(a);
            act.AddBinding(b);
            return act;
        }

        void OnEnable()
        {
            _note1 = Button("<Keyboard>/j", "<Gamepad>/buttonWest");
            _note2 = Button("<Keyboard>/k", "<Gamepad>/buttonNorth");
            _guard = Button("<Keyboard>/l", "<Gamepad>/leftShoulder");
            _dodge = Button("<Keyboard>/leftShift", "<Gamepad>/rightTrigger");
            _note1.performed += c => _note1At = c.time;
            _note2.performed += c => _note2At = c.time;
            _guard.performed += c => _guardAt = c.time;
            _dodge.performed += c => _dodgeAt = c.time;
            _note1.Enable(); _note2.Enable(); _guard.Enable(); _dodge.Enable();
        }

        void OnDisable()
        {
            foreach (var a in new[] { _note1, _note2, _guard, _dodge }) if (a != null) { a.Disable(); a.Dispose(); }
            _note1 = _note2 = _guard = _dodge = null;
        }

        /// <summary>An input event time as a realtimeSinceStartup moment (events are stamped on their own clock).</summary>
        static double ToRealtime(double eventTime)
        {
            double age = InputState.currentTime - eventTime;
            if (age < 0.0 || age > 0.25) age = 0.0;
            return Time.realtimeSinceStartupAsDouble - age;
        }

        static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();
#endif

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

            StrikePressed = Pressed(_note1);
            SmashPressed = Pressed(_note2);
            StrikeRealtime = StrikePressed ? ToRealtime(_note1At) : double.NaN;
            SmashRealtime = SmashPressed ? ToRealtime(_note2At) : double.NaN;
            StepPressed = (kb?.leftShiftKey.wasPressedThisFrame ?? false) || (gp?.rightShoulder.wasPressedThisFrame ?? false);
            JumpPressed = (kb?.spaceKey.wasPressedThisFrame ?? false) || (gp?.buttonSouth.wasPressedThisFrame ?? false);
            JumpHeld = (kb?.spaceKey.isPressed ?? false) || (gp?.buttonSouth.isPressed ?? false);
            GuardPressed = Pressed(_guard);
            GuardRealtime = GuardPressed ? ToRealtime(_guardAt) : double.NaN;
            DodgePressed = Pressed(_dodge);
            DodgeRealtime = DodgePressed ? ToRealtime(_dodgeAt) : double.NaN;
            SwapWeaponPressed = (kb?.tabKey.wasPressedThisFrame ?? false) || (gp?.selectButton.wasPressedThisFrame ?? false);
            OffsetNudge = ((kb?.rightBracketKey.wasPressedThisFrame ?? false) || (gp?.dpad.right.wasPressedThisFrame ?? false) ? 1 : 0)
                        - ((kb?.leftBracketKey.wasPressedThisFrame ?? false) || (gp?.dpad.left.wasPressedThisFrame ?? false) ? 1 : 0);
            TempoKey = kb == null ? 0 : kb.f1Key.wasPressedThisFrame ? 1 : kb.f2Key.wasPressedThisFrame ? 2
                     : kb.f3Key.wasPressedThisFrame ? 3 : kb.f4Key.wasPressedThisFrame ? 4 : 0;
            bool upKey = (kb?.wKey.wasPressedThisFrame ?? false) || (kb?.upArrowKey.wasPressedThisFrame ?? false);
            UseItemPressed = (kb?.eKey.wasPressedThisFrame ?? false) || (gp?.buttonEast.wasPressedThisFrame ?? false);
            CycleItemPressed = (kb?.qKey.wasPressedThisFrame ?? false) || (gp?.dpad.down.wasPressedThisFrame ?? false);
            PausePressed = (kb?.escapeKey.wasPressedThisFrame ?? false) || (gp?.startButton.wasPressedThisFrame ?? false);
#else
            Move = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            StrikePressed = Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.JoystickButton2);
            SmashPressed = Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton3);
            StepPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.JoystickButton5);
            JumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
            JumpHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
            GuardPressed = Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton4);
            DodgePressed = Input.GetKeyDown(KeyCode.LeftShift);
            double nowRt = Time.realtimeSinceStartupAsDouble;     // the legacy manager has no event times
            StrikeRealtime = StrikePressed ? nowRt : double.NaN;
            SmashRealtime = SmashPressed ? nowRt : double.NaN;
            GuardRealtime = GuardPressed ? nowRt : double.NaN;
            DodgeRealtime = DodgePressed ? nowRt : double.NaN;
            SwapWeaponPressed = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton6);
            OffsetNudge = (Input.GetKeyDown(KeyCode.RightBracket) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftBracket) ? 1 : 0);
            TempoKey = Input.GetKeyDown(KeyCode.F1) ? 1 : Input.GetKeyDown(KeyCode.F2) ? 2 : Input.GetKeyDown(KeyCode.F3) ? 3 : Input.GetKeyDown(KeyCode.F4) ? 4 : 0;
            bool upKey = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            UseItemPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton1);
            CycleItemPressed = Input.GetKeyDown(KeyCode.Q);
            PausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
            // Up on the stick counts once as it crosses the threshold.
            InteractPressed = upKey || (Move.y > 0.6f && _lastMoveY <= 0.6f && !upKey && Mathf.Abs(Move.x) < 0.5f);
            _lastMoveY = Move.y;

            if (UiState.Blocking)
            {
                // A menu or dialogue is open: the player stands still and nothing reaches combat.
                Move = Vector2.zero;
                StrikePressed = SmashPressed = StepPressed = JumpPressed = JumpHeld = GuardPressed = DodgePressed = false;
                StrikeRealtime = SmashRealtime = GuardRealtime = DodgeRealtime = double.NaN;
                SwapWeaponPressed = InteractPressed = UseItemPressed = CycleItemPressed = false;
                OffsetNudge = 0;
                TempoKey = 0;
            }
        }
    }
}
