using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Village dressing that appears once a verse is home (VS-5): verse 1 lights the lanterns, verse 2 strings the
    /// banners, verse 3 raises the star over the gate. Each direct child is one piece, popped in on its own when the verse
    /// first comes home (see <see cref="VerseRestoration"/>).
    /// </summary>
    public class VerseGroup : MonoBehaviour
    {
        [Range(1, 6)] public int verse = 1;
    }
}
