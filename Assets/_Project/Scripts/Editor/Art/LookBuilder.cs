using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectHymn.EditorTools
{
    /// <summary>
    /// The shared M5 look: warm angled key light with soft shadows, flat violet ambient, a saturated post-process
    /// profile (colour boost, bloom on glowing parts, light vignette), and the angled camera rig.
    /// Also a tiny "kit" for building environments out of the generated block meshes.
    /// </summary>
    public static class LookBuilder
    {
        const string ProfilePath = ModelGenerator.ArtDir + "/HymnLook.asset";

        public static Light KeyLight(Color color, float intensity = 1.15f)
        {
            var go = new GameObject("Key Light", typeof(Light));
            go.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var l = go.GetComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.6f;
            RenderSettings.sun = l;
            return l;
        }

        public static void Ambient(Color flat)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = flat;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
        }

        public static VolumeProfile Profile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory(ModelGenerator.ArtDir);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(32f);
            color.contrast.Override(14f);
            color.postExposure.Override(0.15f);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static Volume GlobalVolume()
        {
            var go = new GameObject("Post Processing", typeof(Volume));
            var v = go.GetComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = Profile();
            return v;
        }

        /// <summary>Angled perspective camera rig. Fixed (frames an arena) when target is null.</summary>
        public static CameraRig CameraRig(Transform target, Vector2 pivot, float distance, float pitch, Rect bounds, Color background)
        {
            var rigGo = new GameObject("Camera Rig", typeof(CameraRig));
            rigGo.transform.position = Ground.ToWorld(pivot);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rigGo.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;

            var rig = rigGo.GetComponent<CameraRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("pitch").floatValue = pitch;
            so.FindProperty("distance").floatValue = distance;
            so.FindProperty("fieldOfView").floatValue = 30f;
            so.FindProperty("bounds").rectValue = bounds;
            so.ApplyModifiedPropertiesWithoutUndo();
            // Place the camera now so the scene view and first frame are right.
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            camGo.transform.localPosition = rot * new Vector3(0f, 0f, -distance);
            camGo.transform.localRotation = rot;
            cam.fieldOfView = 30f;
            return rig;
        }

        // ---------------- environment kit ----------------

        /// <summary>A block (box, cylinder, cone, roof, gem) with a toon colour, placed by its bottom-centre.</summary>
        public static GameObject Block(Transform parent, string mesh, uint color, Vector3 bottomCenter, Vector3 size,
                                       float yaw = 0f, bool collider = false, bool glow = false, string name = null)
        {
            var go = new GameObject(name ?? mesh, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var m = ModelGenerator.LoadMesh(mesh);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            go.GetComponent<MeshRenderer>().sharedMaterial = ModelGenerator.Toon(color, glow);
            // Box, cylinder and gem are centred; cone and roof start at y = 0.
            bool centred = mesh == "Block_Box" || mesh == "Block_Cylinder" || mesh == "Block_Gem";
            go.transform.localPosition = bottomCenter + (centred ? new Vector3(0f, size.y * 0.5f, 0f) : Vector3.zero);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            if (collider)
            {
                var bc = go.AddComponent<BoxCollider>();
                if (!centred) bc.center = new Vector3(0f, 0.5f, 0f);
            }
            go.isStatic = true;
            return go;
        }

        /// <summary>Invisible wall (keeps the player inside an area).</summary>
        public static void Wall(Transform parent, Vector3 center, Vector3 size, string name = "Wall")
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.GetComponent<BoxCollider>().size = size;
        }

        /// <summary>Invisible walls around a ground-plane rectangle.</summary>
        public static void Bounds(Transform parent, Rect r, float height = 3f, float thickness = 1f)
        {
            var c = Ground.ToWorld(r.center, height / 2f);
            Wall(parent, new Vector3(c.x, c.y, r.yMax + thickness / 2f), new Vector3(r.width + 2 * thickness, height, thickness), "Bound N");
            Wall(parent, new Vector3(c.x, c.y, r.yMin - thickness / 2f), new Vector3(r.width + 2 * thickness, height, thickness), "Bound S");
            Wall(parent, new Vector3(r.xMax + thickness / 2f, c.y, c.z), new Vector3(thickness, height, r.height), "Bound E");
            Wall(parent, new Vector3(r.xMin - thickness / 2f, c.y, c.z), new Vector3(thickness, height, r.height), "Bound W");
        }

        /// <summary>Place a model prefab instance.</summary>
        public static GameObject Model(Transform parent, string prefab, Vector3 position, float yaw = 0f, float scale = 1f)
        {
            var p = ModelGenerator.LoadPrefab(prefab);
            if (p == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }
    }
}
