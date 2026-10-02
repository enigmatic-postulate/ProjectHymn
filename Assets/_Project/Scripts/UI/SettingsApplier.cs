using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using ProjectHymn.Progression;
using UnityEngine;

namespace ProjectHymn.UI
{
    /// <summary>Drop in any scene: applies settings once everything has started.</summary>
    public class SettingsApplier : MonoBehaviour
    {
        void Start() => GameSettings.Apply();
    }
}
