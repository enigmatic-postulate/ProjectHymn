using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// An animation clip baked to plain per-bone samples (art pass). The editor bakes each imported KayKit clip into
    /// one of these, keyed by bone name, so the rhythm animator can sample any clip at any normalized time on the
    /// music's clock (and blend, warp and layer them) without an Animator. Bones are matched by name, so a clip
    /// fits every character on the same rig.
    /// </summary>
    public class BakedClip : ScriptableObject
    {
        public string clipName;
        public float length = 1f;
        public int frameCount = 2;
        public string[] bones = new string[0];
        [HideInInspector] public Quaternion[] rotations = new Quaternion[0];   // frame-major: [frame * bones + bone]
        [Tooltip("Index into positions per bone, or -1 when the clip doesn't move that bone.")]
        public int[] positionSlot = new int[0];
        [HideInInspector] public Vector3[] positions = new Vector3[0];         // [frame * positionBones + slot]
        public int positionBones;

        Dictionary<string, int> _lookup;

        public int IndexOf(string bone)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, int>();
                for (int i = 0; i < bones.Length; i++) _lookup[bones[i]] = i;
            }
            return _lookup.TryGetValue(bone, out int b) ? b : -1;
        }

        void Frame(float u, bool loop, out int f0, out int f1, out float k)
        {
            if (loop) u -= Mathf.Floor(u); else u = Mathf.Clamp01(u);
            float f = u * (frameCount - 1);
            f0 = Mathf.Clamp(Mathf.FloorToInt(f), 0, frameCount - 1);
            f1 = Mathf.Min(f0 + 1, frameCount - 1);
            k = f - f0;
        }

        /// <summary>The bone's local rotation at normalized time <paramref name="u"/> (0..1 over the clip).</summary>
        public Quaternion Rotation(int bone, float u, bool loop = false)
        {
            Frame(u, loop, out int f0, out int f1, out float k);
            int n = bones.Length;
            return Quaternion.Slerp(rotations[f0 * n + bone], rotations[f1 * n + bone], k);
        }

        /// <summary>The bone's local position, if the clip animates it.</summary>
        public bool TryPosition(int bone, float u, bool loop, out Vector3 position)
        {
            int slot = bone >= 0 && bone < positionSlot.Length ? positionSlot[bone] : -1;
            if (slot < 0 || positionBones == 0) { position = default; return false; }
            Frame(u, loop, out int f0, out int f1, out float k);
            position = Vector3.Lerp(positions[f0 * positionBones + slot], positions[f1 * positionBones + slot], k);
            return true;
        }

        void OnValidate() => _lookup = null;
    }

    /// <summary>
    /// Samples <see cref="BakedClip"/>s onto a set of bone transforms by name. Keeps a pose buffer
    /// (bind pose + layers blended on top) and writes it in one go. Used by the KayKit enemies.
    /// </summary>
    [Serializable]
    public sealed class ClipPose
    {
        readonly Transform[] _bones;
        readonly string[] _names;
        readonly Quaternion[] _bindRot;
        readonly Vector3[] _bindPos;
        readonly bool[] _lockPosition;
        public readonly Quaternion[] Rot;
        public readonly Vector3[] Pos;
        readonly Dictionary<BakedClip, int[]> _maps = new Dictionary<BakedClip, int[]>();

        public ClipPose(Transform[] bones, string lockedBone = "root")
        {
            _bones = bones;
            int n = bones.Length;
            _names = new string[n];
            _bindRot = new Quaternion[n];
            _bindPos = new Vector3[n];
            _lockPosition = new bool[n];
            Rot = new Quaternion[n];
            Pos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                _names[i] = bones[i] != null ? bones[i].name : "";
                _bindRot[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
                _bindPos[i] = bones[i] != null ? bones[i].localPosition : Vector3.zero;
                _lockPosition[i] = _names[i] == lockedBone;
            }
            Reset();
        }

        public int Count => _bones.Length;
        public string Name(int i) => _names[i];
        public Quaternion BindRotation(int i) => _bindRot[i];
        public Vector3 BindPosition(int i) => _bindPos[i];

        int[] Map(BakedClip clip)
        {
            if (_maps.TryGetValue(clip, out var m)) return m;
            m = new int[_names.Length];
            for (int i = 0; i < m.Length; i++) m[i] = clip.IndexOf(_names[i]);
            _maps[clip] = m;
            return m;
        }

        /// <summary>Back to the bind pose.</summary>
        public void Reset()
        {
            Array.Copy(_bindRot, Rot, Rot.Length);
            Array.Copy(_bindPos, Pos, Pos.Length);
        }

        /// <summary>Blend the clip at normalized time <paramref name="u"/> over the current buffer by <paramref name="weight"/>.</summary>
        public void Layer(BakedClip clip, float u, float weight = 1f, bool loop = false)
        {
            if (clip == null || weight <= 0f) return;
            var map = Map(clip);
            for (int i = 0; i < Rot.Length; i++)
            {
                int c = map[i];
                if (c < 0) continue;
                var q = clip.Rotation(c, u, loop);
                Rot[i] = weight >= 1f ? q : Quaternion.Slerp(Rot[i], q, weight);
                if (!_lockPosition[i] && clip.TryPosition(c, u, loop, out var p))
                    Pos[i] = weight >= 1f ? p : Vector3.Lerp(Pos[i], p, weight);
            }
        }

        /// <summary>Write the buffer to the bones.</summary>
        public void Apply()
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;
                _bones[i].localRotation = Rot[i];
                _bones[i].localPosition = Pos[i];
            }
        }
    }
}
