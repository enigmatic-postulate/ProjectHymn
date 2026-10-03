using System.Collections.Generic;
using System.IO;
using ProjectHymn.Combat;
using ProjectHymn.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectHymn.EditorTools.Sword
{
    /// <summary>
    /// Turns MoveSpecs into editable assets: SwordMove (keyframes in beats), SwordSequence (1-2-1, 2-2,
    /// 1-1-1-2-1-1-1) and WeaponSet (Saber, Broadsword) under Assets/_Project/Data/Sword. Existing assets are kept
    /// (so your keyframe tuning survives scene rebuilds); "Reset Sword Moves to Defaults" rewrites them.
    /// </summary>
    public static class SwordContent
    {
        const string DataDir = "Assets/_Project/Data/Sword";

        [MenuItem("Project Hymn/Reset Sword Moves to Defaults", priority = 20)]
        public static void ResetToDefaults()
        {
            if (!EditorUtility.DisplayDialog("Reset sword moves", "Overwrite every SwordMove, SwordSequence and WeaponSet with the generated defaults? Keyframe tuning will be lost.", "Reset", "Cancel")) return;
            Ensure(overwrite: true);
            Debug.Log("[Project Hymn] Sword moves reset to defaults.");
        }

        /// <summary>Create the weapon sets (and their moves and sequences). Returns Saber, Broadsword.</summary>
        public static List<WeaponSet> Ensure(bool overwrite = false)
        {
            var list = new List<WeaponSet>();
            foreach (var spec in new[] { MoveSpecs.Saber(), MoveSpecs.Broadsword() })
                list.Add(BuildWeapon(spec, overwrite));
            AssetDatabase.SaveAssets();
            return list;
        }

        static WeaponSet BuildWeapon(WeaponSpec spec, bool overwrite)
        {
            foreach (var d in new[] { DataDir, DataDir + "/Moves", DataDir + "/Sequences" }) Directory.CreateDirectory(d);
            string wpath = $"{DataDir}/{spec.Id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponSet>(wpath);
            if (existing != null && !overwrite) return existing;

            var moves = new Dictionary<string, SwordMove>();
            foreach (var m in spec.Moves) moves[m.Id] = BuildMove(m);

            var w = existing != null ? existing : ScriptableObject.CreateInstance<WeaponSet>();
            w.displayName = spec.Name;
            w.swordObject = spec.Id;
            w.twoHanded = spec.TwoHanded;
            w.stance = ToKey(spec.Stance);
            w.lightA = moves[spec.LightFallbackA];
            w.lightB = moves[spec.LightFallbackB];
            w.heavy = moves[spec.HeavyFallback];
            w.damageScale = 1f;
            w.sequences = new List<SwordSequence>();
            foreach (var s in spec.Sequences)
            {
                string path = $"{DataDir}/Sequences/{spec.Id}_{s.Id}.asset";
                var seq = AssetDatabase.LoadAssetAtPath<SwordSequence>(path);
                bool fresh = seq == null;
                if (fresh) seq = ScriptableObject.CreateInstance<SwordSequence>();
                seq.displayName = s.Name;
                seq.notes = new List<NoteButton>();
                foreach (var n in s.Notes) seq.notes.Add((NoteButton)n);
                seq.moves = new List<SwordMove>();
                foreach (var id in s.Moves) seq.moves.Add(moves[id]);
                seq.finisherMultiplier = s.FinisherMultiplier;
                seq.color = ToColor(s.Color);
                if (fresh) AssetDatabase.CreateAsset(seq, path); else EditorUtility.SetDirty(seq);
                w.sequences.Add(seq);
            }
            if (existing == null) AssetDatabase.CreateAsset(w, wpath); else EditorUtility.SetDirty(w);
            return w;
        }

        static SwordMove BuildMove(MoveSpec m)
        {
            string path = $"{DataDir}/Moves/{m.Id}.asset";
            var a = AssetDatabase.LoadAssetAtPath<SwordMove>(path);
            bool fresh = a == null;
            if (fresh) a = ScriptableObject.CreateInstance<SwordMove>();
            a.displayName = m.Name;
            a.keys = new List<MoveKey>();
            foreach (var k in m.Keys) a.keys.Add(ToKey(k));
            a.SortKeys();
            a.activeStart = m.ActiveStart;
            a.activeEnd = m.ActiveEnd;
            a.damage = m.Damage;
            a.reach = m.Reach;
            a.radius = m.Radius;
            a.effect = m.Effect;
            a.trailColor = ToColor(m.TrailColor);
            a.offHandFree = m.OffHandFree;
            if (fresh) AssetDatabase.CreateAsset(a, path); else EditorUtility.SetDirty(a);
            return a;
        }

        static MoveKey ToKey(KeySpec k)
        {
            var key = new MoveKey { beat = k.Beat, rootOffset = new Vector3(k.Root.x, k.Root.y, k.Root.z) };
            foreach (var b in k.Bones) key.bones.Add(new BoneRotation(b.Key, new Vector3(b.Value.x, b.Value.y, b.Value.z)));
            return key;
        }

        static Color ToColor(uint rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
