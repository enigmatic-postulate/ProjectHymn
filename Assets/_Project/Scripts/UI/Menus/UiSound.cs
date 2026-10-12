using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectHymn.UI
{
    /// <summary>A soft tick when the highlight moves onto a button (VS-4), not for the first selection a menu makes.</summary>
    public class UiSound : MonoBehaviour, ISelectHandler
    {
        static float _menuOpened;
        void OnEnable() => _menuOpened = Time.unscaledTime;
        public void OnSelect(BaseEventData e)
        {
            if (Time.unscaledTime - _menuOpened < 0.15f) return;
            ProjectHymn.Gameplay.Sfx.Play(ProjectHymn.Gameplay.Sfx.MenuMove, 0.5f);
        }
    }
}
