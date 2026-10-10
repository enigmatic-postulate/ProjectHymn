namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Whether a menu or dialogue currently owns the controls. Menus call <see cref="Push"/> when they open and
    /// <see cref="Pop"/> when they close; the input reader then hands nothing to movement or combat.
    /// </summary>
    public static class UiState
    {
        static int _open;

        public static bool Blocking => _open > 0;
        /// <summary>When the last menu closed (unscaled time): the press that closed it must not open something else.</summary>
        public static float LastClosed { get; private set; } = -10f;

        /// <summary>
        /// A menu closed in the last 0.2 s. A close stamped in the future can't be "just": with domain reload off, a stamp
        /// left over from an earlier play session (whose clock ran longer) used to read as just-closed for minutes, and
        /// E and Esc did nothing after Continue.
        /// </summary>
        public static bool JustClosed
        {
            get
            {
                float since = UnityEngine.Time.unscaledTime - LastClosed;
                return since >= 0f && since < 0.2f;
            }
        }

        public static void Push() => _open++;
        public static void Pop() { if (_open > 0) { _open--; if (_open == 0) LastClosed = UnityEngine.Time.unscaledTime; } }
        /// <summary>Scene change: nothing from the old scene stays open.</summary>
        public static void Reset() { _open = 0; LastClosed = -10f; }

        /// <summary>Entering play mode without a domain reload keeps statics: start clean every time.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Reset();
    }
}
