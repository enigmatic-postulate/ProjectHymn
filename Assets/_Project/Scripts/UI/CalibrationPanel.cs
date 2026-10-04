using ProjectHymn.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// Tap-along latency calibration: tap on the click for 16 beats; the median offset becomes the player's calibration,
    /// and the spread of the taps is shown as a quick check (tight enough for True Perfect, or worth redoing). Taps are
    /// timed by their input event when the player's input reader is there. Press C to start.
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
        readonly System.Collections.Generic.List<double> _offsets = new System.Collections.Generic.List<double>();
        ProjectHymn.Gameplay.PlayerInputReader _reader;

        void Awake() { if (conductor == null) conductor = FindAnyObjectByType<Conductor>(); }

        void Update()
        {
            if (TogglePressed()) { if (_active) Finish(false); else Begin(); }
            if (!_active || conductor == null || !conductor.IsPlaying) return;

            if (TapPressed(out double realtime))
            {
                // Raw song time (no calibration applied) so we measure the full offset.
                double t = conductor.InputSongTimeAt(realtime) + conductor.CalibrationSeconds;
                long nearest = (long)System.Math.Round(conductor.Map.TimeToBeat(t));
                _offsets.Add(t - conductor.Map.GetBeat(nearest).Time);
                if (_offsets.Count >= tapsNeeded) Finish(true);
            }

            if (label != null)
                label.text = $"CALIBRATING: tap J / X on every click  {_offsets.Count}/{tapsNeeded}   (C to cancel)";
        }

        /// <summary>Start the 16-tap test (also from the Settings menu).</summary>
        public void Begin()
        {
            _active = true; _offsets.Clear();
            if (conductor != null) conductor.MetronomeEnabled = true;
        }

        void Finish(bool save)
        {
            _active = false;
            if (save && _offsets.Count > 0 && conductor != null)
            {
                var r = CalibrationMath.Summarize(_offsets);
                conductor.SaveCalibration((float)r.OffsetMs);
                if (label != null)
                    label.text = r.Consistent
                        ? $"Calibration saved: {r.OffsetMs:+0;-0;0} ms (taps within \u00b1{r.SpreadMs:0} ms: good)   (C to redo)"
                        : $"Calibration saved: {r.OffsetMs:+0;-0;0} ms, but taps spread \u00b1{r.SpreadMs:0} ms: worth redoing (C)";
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

        bool TapPressed(out double realtime)
        {
            realtime = double.NaN;
            if (_reader == null || !_reader.isActiveAndEnabled) _reader = FindAnyObjectByType<ProjectHymn.Gameplay.PlayerInputReader>();
            if (_reader != null)
            {
                if (!_reader.StrikePressed) return false;
                realtime = _reader.StrikeRealtime;
                return true;
            }
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
