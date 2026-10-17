using System;
using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectHymn.UI
{
    /// <summary>
    /// On a settings row: while the row is selected, left / right (A / D, the d-pad) steps its volume down / up by 10%,
    /// stopping at 0 and 100% (the row's click still steps up and wraps).
    /// </summary>
    public class VolumeStepper : MonoBehaviour
    {
        public Func<float> Get;
        public Action<float> Set;
        public Action Refresh;

        void Update()
        {
            if (Get == null || Set == null) return;
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject != gameObject) return;
            int step = UiInput.Horizontal;
            if (step == 0) return;
            float before = Get();
            float after = GameSettings.Stepped(before, step);
            if (Mathf.Approximately(before, after)) return;
            Set(after);
            Sfx.Play(Sfx.MenuMove, 0.6f);
            Refresh?.Invoke();
        }
    }
}
