using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Switches the whole game between the two looks (GDD M6): the Dragon Valor look (default: painted textures,
    /// per-vertex lighting, no outlines, no colour boost, distance fog) and the M5 cel look (hard light bands,
    /// ink outlines, saturated post-processing). One global shader keyword flips every material; the scene's
    /// <see cref="LookProfiles"/> swaps the post-processing profile and fog. Assets are never modified.
    /// </summary>
    public static class HymnLook
    {
        public const string CelKeyword = "HYMN_LOOK_CEL";

        public static bool IsCel { get; private set; }

        public static void Apply(bool cel)
        {
            IsCel = cel;
            if (cel) Shader.EnableKeyword(CelKeyword);
            else Shader.DisableKeyword(CelKeyword);
            var profiles = Object.FindAnyObjectByType<LookProfiles>();
            if (profiles != null) profiles.Apply(cel);
        }
    }
}
