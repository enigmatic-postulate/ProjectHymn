using ProjectHymn.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Tap-along latency calibration: tap on the click for 16 beats; the average offset becomes the
    /// player's calibration. Press C in the prototype scene to start.
    /// </summary>
    public class CalibrationPanel : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] Text label;
        [SerializeField] int tapsNeeded = 16;
        #pragma warning disable 0414
        [SerializeField] KeyCode legacyToggleKey = KeyCode.C;
#pragma warning restore 0414

        bool _active;
        int _taps;
        double _sum;

        void Awake() { if (conductor == null) conductor = FindAnyObjectByType<Conductor>(); }

        void Update()
        {
            if (TogglePressed()) { if (_active) Finish(false); else Begin(); }
            if (!_active || conductor == null || !conductor.IsPlaying) return;

            if (TapPressed())
            {
                // Raw song time (no calibration applied) so we measure the full offset.
                double t = conductor.SongTime;
                long nearest = (long)System.Math.Round(conductor.Map.TimeToBeat(t));
                double offset = t - conductor.Map.GetBeat(nearest).Time;
                _sum += offset;
                _taps++;
                if (_taps >= tapsNeeded) Finish(true);
            }

            if (label != null)
                label.text = $"CALIBRATING: tap J / X on every click  {_taps}/{tapsNeeded}   (C to cancel)";
        }

        void Begin()
        {
            _active = true; _taps = 0; _sum = 0;
            if (conductor != null) conductor.MetronomeEnabled = true;
        }

        void Finish(bool save)
        {
            _active = false;
            if (save && _taps > 0 && conductor != null)
            {
                float ms = (float)(_sum / _taps * 1000.0);
                conductor.SaveCalibration(ms);
                ProjectHymn.Progression.SaveSystem.Current.settings.calibrationMs = ms;
                ProjectHymn.Progression.SaveSystem.Save();
                if (label != null) label.text = $"Calibration saved: {ms:+0;-0;0} ms   (C to redo)";
            }
            else if (label != null) label.text = "Press C to calibrate audio latency";
        }

        bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.cKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(legacyToggleKey);
#endif
        }

        bool TapPressed()
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            return (kb != null && kb.jKey.wasPressedThisFrame) || (gp != null && gp.buttonWest.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.J);
#endif
        }
    }
}
