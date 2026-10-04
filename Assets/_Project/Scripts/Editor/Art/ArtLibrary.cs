using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// Imported art for the art pass: where the CC0 packs live in the project, their import settings, baking their
    /// animation clips into <see cref="BakedClip"/> assets, and textured materials. Everything is optional: when a
    /// pack is missing the builders fall back to the generated placeholder art.
    /// </summary>
    public static class ArtLibrary
    {
        public const string Root = "Assets/_Project/Art/Imported";
        public const string KayKit = Root + "/KayKit";
        public const string Village = Root + "/Village";
        public const string Boulders = Root + "/Boulders";
        public const string ClipDir = "Assets/_Project/Data/Clips";
        public const string MatDir = "Assets/_Project/Art/Materials/Imported";
        public const string MeshDir = "Assets/_Project/Art/Generated";

        public static string KnightFbx => $"{KayKit}/Characters/Knight.fbx";
        public static string SkeletonFbx => $"{KayKit}/Characters/Skeleton_Warrior.fbx";
        public static readonly string[] AnimationFbx =
        {
            $"{KayKit}/Animations/Rig_Medium_CombatMelee.fbx",
            $"{KayKit}/Animations/Rig_Medium_General.fbx",
            $"{KayKit}/Animations/Rig_Medium_MovementBasic.fbx",
            $"{KayKit}/Animations/Rig_Medium_Special.fbx",
        };

        public static bool HasKayKit => File.Exists(KnightFbx) && AnimationFbx.All(File.Exists);
        public static bool HasSkeleton => File.Exists(SkeletonFbx);
        public static bool HasVillage => Directory.Exists(Village + "/Models") && File.Exists(Village + "/Textures/T_UnevenBrick_BaseColor.png");
        public static bool HasBoulders => File.Exists(Boulders + "/namaqualand_boulder_03.fbx");

        // ---------------- Import settings ----------------

        /// <summary>Make every imported pack import the way the builders expect (safe to call repeatedly).</summary>
        public static void EnsureImportSettings()
        {
            if (Directory.Exists(KayKit))
            {
                foreach (var path in Directory.GetFiles(KayKit, "*.fbx", SearchOption.AllDirectories).Select(Norm))
                    ConfigureModel(path, rigged: !path.Contains("/Weapons/"), animations: path.Contains("/Animations/"));
                foreach (var path in Directory.GetFiles(KayKit, "*.png", SearchOption.AllDirectories).Select(Norm))
                    ConfigureTexture(path, normal: false, linear: false, maxSize: 512, point: false);
            }
            if (Directory.Exists(Village))
            {
                foreach (var path in Directory.GetFiles(Village, "*.fbx", SearchOption.AllDirectories).Select(Norm))
                    ConfigureModel(path, rigged: false, animations: false);
                foreach (var path in Directory.GetFiles(Village, "*.png", SearchOption.AllDirectories).Select(Norm))
                {
                    string n = Path.GetFileNameWithoutExtension(path);
                    ConfigureTexture(path, normal: n.EndsWith("_Normal"), linear: n.EndsWith("_ORM") || n.EndsWith("_Roughness") || n.Contains("Noise"), maxSize: 1024, point: false);
                }
            }
            if (Directory.Exists(Boulders))
            {
                foreach (var path in Directory.GetFiles(Boulders, "*.fbx").Select(Norm)) ConfigureModel(path, rigged: false, animations: false);
                foreach (var path in Directory.GetFiles(Boulders).Where(p => p.EndsWith(".png") || p.EndsWith(".jpg")).Select(Norm))
                    ConfigureTexture(path, normal: path.Contains("_nor"), linear: false, maxSize: 1024, point: false);
            }
        }

        static string Norm(string p) => p.Replace('\\', '/');

        static void ConfigureModel(string path, bool rigged, bool animations)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            bool dirty = false;
            var type = rigged ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (mi.animationType != type) { mi.animationType = type; dirty = true; }
            if (rigged && mi.avatarSetup != ModelImporterAvatarSetup.NoAvatar) { mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar; dirty = true; }
            if (mi.importAnimation != animations) { mi.importAnimation = animations; dirty = true; }
            if (animations && mi.animationCompression != ModelImporterAnimationCompression.Off) { mi.animationCompression = ModelImporterAnimationCompression.Off; dirty = true; }
            if (mi.importCameras) { mi.importCameras = false; dirty = true; }
            if (mi.importLights) { mi.importLights = false; dirty = true; }
            if (mi.addCollider) { mi.addCollider = false; dirty = true; }
            if (dirty) mi.SaveAndReimport();
        }

        static void ConfigureTexture(string path, bool normal, bool linear, int maxSize, bool point)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            bool dirty = false;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != type) { ti.textureType = type; dirty = true; }
            bool srgb = !normal && !linear;
            if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; dirty = true; }
            if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }

        // ---------------- Clips ----------------

        /// <summary>Bake one clip from the KayKit animation files (by take name), or return the existing bake.</summary>
        public static BakedClip Clip(string name, bool rebake = false)
        {
            Directory.CreateDirectory(ClipDir);
            string path = $"{ClipDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BakedClip>(path);
            if (existing != null && !rebake && existing.frameCount > 1) return existing;
            AnimationClip clip = null;
            foreach (var fbx in AnimationFbx)
            {
                clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__") && (c.name == name || c.name.EndsWith("|" + name)));
                if (clip != null) break;
            }
            if (clip == null) { Debug.LogWarning($"[Project Hymn] Animation clip '{name}' not found in the KayKit animation files."); return existing; }
            var baked = Bake(clip, name);
            if (existing != null) { EditorUtility.CopySerialized(baked, existing); Object.DestroyImmediate(baked); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(baked, path);
            return baked;
        }

        /// <summary>Sample an imported clip's transform curves at 30 fps into a <see cref="BakedClip"/>, keyed by bone name.</summary>
        public static BakedClip Bake(AnimationClip clip, string name, float fps = 30f)
        {
            var rot = new Dictionary<string, AnimationCurve[]>();
            var eul = new Dictionary<string, AnimationCurve[]>();
            var pos = new Dictionary<string, AnimationCurve[]>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.type != typeof(Transform) || string.IsNullOrEmpty(b.path)) continue;
                string bone = b.path.Substring(b.path.LastIndexOf('/') + 1);
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                string p = b.propertyName;
                int axis = p.EndsWith(".x") ? 0 : p.EndsWith(".y") ? 1 : p.EndsWith(".z") ? 2 : p.EndsWith(".w") ? 3 : -1;
                if (axis < 0) continue;
                Dictionary<string, AnimationCurve[]> set = p.StartsWith("m_LocalRotation") ? rot
                    : p.StartsWith("localEulerAngles") ? eul : p.StartsWith("m_LocalPosition") ? pos : null;
                if (set == null) continue;
                if (!set.TryGetValue(bone, out var arr)) set[bone] = arr = new AnimationCurve[4];
                arr[axis] = curve;
            }
            var bones = rot.Where(kv => kv.Value[0] != null && kv.Value[1] != null && kv.Value[2] != null && kv.Value[3] != null).Select(kv => kv.Key)
                .Concat(eul.Where(kv => kv.Value[0] != null && kv.Value[1] != null && kv.Value[2] != null).Select(kv => kv.Key))
                .Distinct().ToArray();
            int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps) + 1);
            var posBones = bones.Where(b => pos.TryGetValue(b, out var a) && a[0] != null && a[1] != null && a[2] != null).ToArray();

            var baked = ScriptableObject.CreateInstance<BakedClip>();
            baked.clipName = name;
            baked.length = Mathf.Max(clip.length, 1f / fps);
            baked.frameCount = frames;
            baked.bones = bones;
            baked.rotations = new Quaternion[frames * bones.Length];
            baked.positionBones = posBones.Length;
            baked.positions = new Vector3[frames * posBones.Length];
            baked.positionSlot = bones.Select(b => System.Array.IndexOf(posBones, b)).ToArray();
            for (int b = 0; b < bones.Length; b++)
            {
                Quaternion prev = Quaternion.identity;
                for (int f = 0; f < frames; f++)
                {
                    float t = Mathf.Min(clip.length, f / fps);
                    Quaternion q;
                    if (rot.TryGetValue(bones[b], out var r) && r[0] != null && r[3] != null)
                        q = new Quaternion(r[0].Evaluate(t), r[1].Evaluate(t), r[2].Evaluate(t), r[3].Evaluate(t));
                    else
                    {
                        var e = eul[bones[b]];
                        q = Quaternion.Euler(e[0].Evaluate(t), e[1].Evaluate(t), e[2].Evaluate(t));
                    }
                    q = Normalize(q);
                    if (f > 0 && Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // keep one hemisphere
                    baked.rotations[f * bones.Length + b] = q;
                    prev = q;
                    int slot = baked.positionSlot[b];
                    if (slot >= 0)
                    {
                        var pc = pos[bones[b]];
                        baked.positions[f * posBones.Length + slot] = new Vector3(pc[0].Evaluate(t), pc[1].Evaluate(t), pc[2].Evaluate(t));
                    }
                }
            }
            return baked;
        }

        static Quaternion Normalize(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m < 1e-6f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        // ---------------- Materials ----------------

        static Shader LitShader() => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        /// <summary>A URP Lit material with a base texture (and optional normal map), kept as an asset and updated in place.</summary>
        public static Material Textured(string name, string baseMap, string normalMap = null, Color? tint = null, float smoothness = 0.15f,
                                        float tile = 1f, bool cutout = false, bool doubleSided = false)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(LitShader()); AssetDatabase.CreateAsset(m, path); }
            var tex = baseMap != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap) : null;
            var color = tint ?? Color.white;
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetTextureScale("_BaseMap", Vector2.one * tile);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            var nrm = normalMap != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalMap) : null;
            m.SetTexture("_BumpMap", nrm);
            if (nrm != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.4f);
                m.EnableKeyword("_ALPHATEST_ON");
            }
            if (doubleSided) m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------- Instancing helpers ----------------

        public static GameObject Instance(string fbxPath, Transform parent, string name = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (name != null) go.name = name;
            return go;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>Give every renderer under <paramref name="root"/> one material (KayKit characters share one atlas).</summary>
        public static void Paint(GameObject root, Material m)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>World bounds of every renderer under a transform.</summary>
        public static Bounds RendererBounds(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(root.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
