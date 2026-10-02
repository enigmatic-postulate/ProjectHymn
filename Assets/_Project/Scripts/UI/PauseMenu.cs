using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Town = "Town";
    }

    /// <summary>
    /// Esc / Start pauses: song time freezes and audio re-syncs on resume, so the beat never drifts.
    /// Resume, Settings, Return to Town (arenas only), Main Menu, Quit.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] bool isTown;

        Canvas _canvas;
        bool _paused;

        public static bool IsOpen { get; private set; }

        void Awake() { if (input == null) input = FindAnyObjectByType<PlayerInputReader>(); }

        void Update()
        {
            if (TownController.PanelOpen) return;
            if (input != null && input.PausePressed)
            {
                if (_paused) Resume(); else Pause();
            }
        }

        public void Pause()
        {
            if (_paused || ResultsScreen.IsOpen) return;
            _paused = true;
            IsOpen = true;
            Time.timeScale = 0f;
            var conductor = FindAnyObjectByType<Conductor>();
            if (conductor != null) conductor.Pause();
            ShowMain();
        }

        public void Resume()
        {
            if (!_paused) return;
            _paused = false;
            IsOpen = false;
            if (_canvas != null) Destroy(_canvas.gameObject);
            Time.timeScale = 1f;
            var conductor = FindAnyObjectByType<Conductor>();
            if (conductor != null) conductor.Resume();
        }

        void ShowMain()
        {
            Rebuild(out var panel);
            UIFactory.Label(panel, "PAUSED", 56, color: UIFactory.Gold);
            var resume = UIFactory.Button(panel, "Resume", Resume);
            UIFactory.Button(panel, "Settings", ShowSettings);
            if (!isTown) UIFactory.Button(panel, "Return to Town", () => Load(SceneNames.Town));
            UIFactory.Button(panel, "Main Menu", () => Load(SceneNames.MainMenu));
            UIFactory.Button(panel, "Quit Game", Quit);
            UIFactory.Focus(resume);
        }

        void ShowSettings()
        {
            Rebuild(out var panel, 820);
            SettingsMenu.Build(panel, ShowMain);
        }

        void Rebuild(out RectTransform panel, float height = 560)
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = UIFactory.Overlay("PauseMenu");
            UIFactory.Backdrop(_canvas.transform);
            panel = UIFactory.Panel(_canvas.transform, new Vector2(720, height));
        }

        static void Load(string scene)
        {
            Time.timeScale = 1f;
            IsOpen = false;
            SaveSystem.Save();
            SceneManager.LoadScene(scene);
        }

        public static void Quit()
        {
            SaveSystem.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
