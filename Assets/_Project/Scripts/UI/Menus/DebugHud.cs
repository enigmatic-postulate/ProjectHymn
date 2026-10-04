using ProjectHymn.Gameplay;
using UnityEngine;

namespace ProjectHymn.UI
{
    /// <summary>Shows or hides the prototype rhythm HUD (Settings → Rhythm debug HUD, or F9).</summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] GameObject[] parts = new GameObject[0];
        bool _shown = true;

        void Start() => Apply(GameSettings.DebugHud);

        void Update()
        {
            if (F9()) GameSettings.DebugHud = !GameSettings.DebugHud;
            if (GameSettings.DebugHud != _shown) Apply(GameSettings.DebugHud);
        }

        void Apply(bool on)
        {
            _shown = on;
            foreach (var p in parts) if (p != null) p.SetActive(on);
        }

        static bool F9()
        {
#if ENABLE_INPUT_SYSTEM && HYMN_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.f9Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F9);
#endif
        }
    }
}
