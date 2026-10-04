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
        public static bool JustClosed => UnityEngine.Time.unscaledTime - LastClosed < 0.2f;
        public static void Push() => _open++;
        public static void Pop() { if (_open > 0) { _open--; if (_open == 0) LastClosed = UnityEngine.Time.unscaledTime; } }
        /// <summary>Scene change: nothing from the old scene stays open.</summary>
        public static void Reset() => _open = 0;
    }
}
