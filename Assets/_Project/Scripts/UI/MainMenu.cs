using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>Title screen: Continue, New Game, Settings, Quit.</summary>
    public class MainMenu : MonoBehaviour
    {
        Canvas _canvas;

        void Start()
        {
            Time.timeScale = 1f;
            GameSettings.Apply();
            ShowMain();
        }

        void ShowMain()
        {
            Rebuild(out var panel, 640);
            UIFactory.Label(panel, "PROJECT HYMN", 76, color: UIFactory.Gold, height: 110);
            UIFactory.Label(panel, "an action RPG played on the beat", 26, color: UIFactory.DimText);
            Button first = null;
            if (SaveSystem.HasSave)
            {
                var notes = SaveSystem.Current.notes;
                first = UIFactory.Button(panel, $"Continue  ({notes} Notes)", () => SceneManager.LoadScene(SceneNames.Town));
            }
            var newGame = UIFactory.Button(panel, "New Game", () =>
            {
                SaveSystem.NewGame();
                SceneManager.LoadScene(SceneNames.Town);
            });
            UIFactory.Button(panel, "Settings", ShowSettings);
            UIFactory.Button(panel, "Quit", PauseMenu.Quit);
            UIFactory.Focus(first != null ? first : newGame);
        }

        void ShowSettings()
        {
            Rebuild(out var panel, 820);
            SettingsMenu.Build(panel, ShowMain);
        }

        void Rebuild(out RectTransform panel, float height)
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = UIFactory.Overlay("MainMenu");
            panel = UIFactory.Panel(_canvas.transform, new Vector2(760, height));
        }
    }
}
