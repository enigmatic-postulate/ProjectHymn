using System.IO;
using UnityEditor;
using UnityEngine;
using C = ProjectHymn.EditorTools.PixelCanvas.Rgba;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// Saves the generated placeholder art (HymnArt) as pixel-perfect sprites in Assets/_Project/Art/Generated:
    /// 16 pixels per unit, point filtering, no compression. Re-running overwrites them, so hand-made replacements
    /// should be saved under a different name and assigned in the assets instead.
    /// </summary>
    public static class ArtGenerator
    {
        public const string Dir = "Assets/_Project/Art/Generated";

        [MenuItem("Project Hymn/Regenerate Placeholder Art", priority = 20)]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            Save("player_robe", HymnArt.Player(C.Hex(0x3c5ac0), C.Hex(0x263880), C.Hex(0xf0c040)));
            Save("player_brass", HymnArt.Player(C.Hex(0xd8a440), C.Hex(0x9a6c20), C.Hex(0xfff0b0)));
            Save("player_vest", HymnArt.Player(C.Hex(0x4cb270), C.Hex(0x2e7448), C.Hex(0xe8e8f0)));
            Save("slime", HymnArt.Slime());
            Save("imp", HymnArt.Imp());
            Save("knight", HymnArt.Knight());
            Save("bat", HymnArt.Bat());
            Save("conductor", HymnArt.Conductor());
            Save("blade", HymnArt.Blade());
            Save("drum_hammer", HymnArt.DrumHammer());
            Save("lute_bow", HymnArt.LuteBow());
            Save("note_shot", HymnArt.NoteShot());
            Save("lore_scroll", HymnArt.LoreScroll());
            Save("marker", HymnArt.Marker());
            Save("bg_town", HymnArt.Town());
            Save("bg_arena_practice", HymnArt.Arena(HymnArt.ArenaStyle.Practice));
            Save("bg_arena_garden", HymnArt.Arena(HymnArt.ArenaStyle.Garden));
            Save("bg_arena_boss", HymnArt.Arena(HymnArt.ArenaStyle.Boss));
            AssetDatabase.SaveAssets();
            Debug.Log($"[Project Hymn] Placeholder art written to {Dir}.");
        }

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

        /// <summary>Generate the art only if it isn't there yet (keeps hand edits to the import settings).</summary>
        public static void EnsureGenerated()
        {
            if (Load("bg_town") == null || Load("player_robe") == null) GenerateAll();
        }

        static void Save(string name, PixelCanvas c)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(c.Width, c.Height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(c.ToBottomUp());
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
    }
}
