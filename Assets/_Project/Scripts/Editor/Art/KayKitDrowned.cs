using System.Collections.Generic;
using System.IO;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;
using static ProjectHymn.EditorTools.ProjectHymnSetup;

namespace ProjectHymn.EditorTools.Art
{
    /// <summary>
    /// VS-6's skeletons for the Drowned Nave: the KayKit warrior and mage in sea-worn, wet bone (a green-grey tint and a
    /// sheen), fighting in 6/8. Their wind-ups and swings are timed by the dotted-quarter pulse (three eighths), and their
    /// red aura flares a dotted quarter before each blow.
    /// </summary>
    public static partial class KayKitBuilder
    {
        static Material DrownedBone => ArtLibrary.Textured("KayKit_Skeleton_Drowned", $"{ArtLibrary.KayKit}/Characters/skeleton_texture.png",
                                                           null, new Color(0.66f, 0.86f, 0.8f), 0.55f);

        /// <summary>The Barnacled Knight: the skeleton knight in drowned bone with the 6/8 cut and lilt.</summary>
        public static ShieldKnight BuildBarnacledKnight(Transform parent, Vector3 position, Vector2 bounds, Conductor conductor)
        {
            var k = BuildSkeletonKnight(parent, position, bounds, conductor);
            k.gameObject.name = "Barnacled Knight";
            ArtLibrary.Paint(k.gameObject, DrownedBone);
            DrownedEnemies.Compound(k, "Barnacled Knight", DrownedPatterns.Knight(), 200f);
            return k;
        }

        /// <summary>The Drowned Chorister: the skeleton mage, empty-handed, singing bolts (the throw) and a held note (the taunt).</summary>
        public static DrownedChorister BuildSkeletonChorister(Transform parent, Vector3 position, Vector2 bounds, Conductor conductor, Material additive)
        {
            var root = new GameObject("Drowned Chorister", typeof(CapsuleCollider), typeof(DrownedChorister));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var cap = root.GetComponent<CapsuleCollider>();
            cap.radius = 0.42f; cap.height = 2.0f; cap.center = new Vector3(0f, 1.0f, 0f);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            string fbx = File.Exists(ArtLibrary.SkeletonMageFbx) ? ArtLibrary.SkeletonMageFbx : ArtLibrary.SkeletonFbx;
            var (fit, rigRoot, model) = Place(fbx, body, 2.0f, DrownedBone);
            var head = ArtLibrary.FindDeep(rigRoot, "head");
            var mouth = new GameObject("Mouth").transform;
            mouth.SetParent(head != null ? head : body, false);
            mouth.position = (head != null ? head.position : root.transform.position + Vector3.up * 1.6f) + Vector3.up * 0.15f;
            var lightGo = new GameObject("Song Glow", typeof(Light));
            lightGo.transform.SetParent(mouth, false);
            var l = lightGo.GetComponent<Light>();
            l.type = LightType.Point; l.range = 3f; l.intensity = 1.2f; l.color = new Color(0.45f, 0.95f, 1f);

            var c = root.GetComponent<DrownedChorister>();
            SetRef(c, "conductor", conductor);
            SetRef(c, "body", body);
            SetRef(c, "rigRoot", rigRoot);
            SetRef(c, "mouth", mouth);
            SetRef(c, "boltMaterial", additive);
            SetArray(c, "flashRenderers", root.GetComponentsInChildren<Renderer>(true));
            SetRef(c, "idleClip", ArtLibrary.Clip("Skeletons_Idle"));
            SetRef(c, "walkClip", ArtLibrary.Clip("Skeletons_Walking"));
            SetRef(c, "castClip", ArtLibrary.Clip("Throw"));
            SetRef(c, "sustainClip", ArtLibrary.Clip("Skeletons_Taunt"));
            SetRef(c, "staggerClip", ArtLibrary.Clip("Hit_B"));
            SetRef(c, "deathClip", ArtLibrary.Clip("Skeletons_Death"));
            var so = new SerializedObject(c);
            so.FindProperty("bounds").vector2Value = bounds;
            so.FindProperty("aggroRange").floatValue = 12f;
            so.FindProperty("aggroHeight").floatValue = 3.5f;
            so.FindProperty("leashRange").floatValue = 22f;
            so.FindProperty("barHeight").floatValue = 2.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
            DrownedEnemies.Compound(c, "Drowned Chorister", DrownedPatterns.Chorister(), 110f);
            body.rotation = Quaternion.Euler(0f, -90f + 14f, 0f);
            return c;
        }
        /// <summary>
        /// VS-6's boss, the Drowned Precentor: the skeleton mage in drowned bone, 3.6 m, with a staff; the one-handed
        /// clips for his strokes, the jump chop for the Great Bell, the taunt for conducting.
        /// </summary>
        public static DrownedPrecentor BuildDrownedPrecentor(Transform parent, string name, Vector3 position, Vector2 bounds, Conductor conductor, float maxHp)
        {
            const string Diag = "Melee_1H_Attack_Slice_Diagonal", Horiz = "Melee_1H_Attack_Slice_Horizontal",
                         Chop = "Melee_1H_Attack_Chop", Stab = "Melee_1H_Attack_Stab", Jump = "Melee_1H_Attack_Jump_Chop";
            return SkeletonCantor<DrownedPrecentor>(parent, name, position, bounds, conductor, maxHp, DrownedBone, 3.6f,
                new (string, string[])[]
                {
                    ("Toll", new[] { Chop }),
                    ("Rocking", new[] { Diag, Horiz }),
                    ("Hemiola", new[] { Diag, Horiz, Chop }),
                    ("Long Rock", new[] { Diag, Horiz, Stab }),
                    ("Barcarolle", new[] { Diag, Horiz, Diag, Chop }),
                    ("Suspension", new[] { Chop, Diag }),
                    ("Great Bell", new[] { Jump }),
                    ("Riptide", new[] { Diag, Horiz, Diag, Horiz, Diag, Stab }),
                });
        }
    }
}
