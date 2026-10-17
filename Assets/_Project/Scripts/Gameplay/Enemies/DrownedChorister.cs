using System;
using System.Collections;
using System.Collections.Generic;
using ProjectHymn.Combat;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// VS-6's ranged enemy for the Drowned Nave's 6/8: a drowned choir singer that keeps its distance and sings at you.
    /// The Antiphon is three bolts of song on eighths one, two and three and a heavier note on four: each bolt is sung
    /// one eighth before it lands, so it arrives exactly on its beat, and each can be guarded on its own (a parry staggers
    /// the singer and silences the rest). The Sustain is a held note that rolls out along the floor as a ring: no guard
    /// stops it, so jump over it or dodge through it. It backs away when you close in and walks up when you're far, and it
    /// has no armour: every cut lands in full.
    /// </summary>
    public class DrownedChorister : BeatEnemy
    {
        [Header("Body")]
        [SerializeField] Transform body;
        [SerializeField] float cameraYaw = 14f;
        [SerializeField] Transform mouth;

        [Header("Clip rig (art pass)")]
        [SerializeField] Transform rigRoot;
        [SerializeField] BakedClip idleClip, walkClip, castClip, sustainClip, staggerClip, deathClip;
        [SerializeField] float castHit = 0.45f;

        [Header("Song")]
        [SerializeField] Material boltMaterial;
        [SerializeField] Color boltColor = new Color(0.45f, 0.95f, 1f);
        [SerializeField] Color sustainColor = new Color(0.72f, 0.45f, 1f);
        [Tooltip("Reach of a bolt (metres along the lane) and of the floor ring.")]
        [SerializeField] float reach = 11f;
        [SerializeField] float hitHeight = 2.2f;

        [Header("Movement")]
        [SerializeField] float keepDistance = 6f;
        [SerializeField] float walkSpeed = 2.4f;
        [SerializeField] Vector2 bounds = new Vector2(-1000f, 1000f);

        int _face = -1;
        ClipPose _pose;
        float _staggerT = 9f, _lastX;
        readonly List<Bolt> _bolts = new List<Bolt>();
        Transform _ring;
        SpriteRenderer _ringSprite;

        sealed class Bolt
        {
            public GameObject Go;
            public int Index;
            public double HitBeat;
            public Vector3 From;
            public DefenseResult? Result;
            public float After;
        }

        public static List<EnemyAttackPattern> DefaultPatterns() => DrownedPatterns.Chorister();

        bool Sustain => Plan.IsValid && Plan.Pattern.Unblockable;

        protected override void Awake()
        {
            if (patterns.Count == 0) patterns.AddRange(DefaultPatterns());
            base.Awake();
            if (rigRoot != null) _pose = new ClipPose(rigRoot.GetComponentsInChildren<Transform>(true), "root");
            _lastX = transform.position.x;
            BuildRing();
            ApplyFacing(true);
        }

        protected override void Tick(bool live)
        {
            float dt = Time.deltaTime;
            var phase = Phase;
            bool singing = phase == AttackPhase.Telegraph || phase == AttackPhase.Active;
            var pos = transform.position;
            if (live && Player != null && Aggro && !IsStaggered)
            {
                float dx = Player.position.x - pos.x;
                if (!singing) _face = dx >= 0f ? 1 : -1;
                float dist = Mathf.Abs(dx);
                float step = 0f;
                if (!singing)
                {
                    if (dist < keepDistance - 1.5f) step = -_face;          // back away
                    else if (dist > keepDistance + 3f) step = _face;        // close in
                }
                pos.x = Mathf.Clamp(pos.x + step * walkSpeed * dt, bounds.x, bounds.y);
                transform.position = pos;
            }
            ApplyFacing(false);
            UpdateBolts(live);
            UpdateRing();
            Pose(dt, phase);
        }

        void ApplyFacing(bool snap)
        {
            if (body == null) return;
            var want = Quaternion.AngleAxis(_face * cameraYaw, Vector3.up) * Quaternion.LookRotation(new Vector3(_face, 0f, 0f));
            body.rotation = snap ? want : Quaternion.RotateTowards(body.rotation, want, 540f * Time.deltaTime);
        }

        Vector3 Mouth => mouth != null ? mouth.position : transform.position + new Vector3(_face * 0.3f, 1.6f, 0f);
        Vector3 Chest => Player != null ? Player.position + Vector3.up * 1.1f : transform.position + Vector3.right * _face * 3f;

        // ---------------- Bolts ----------------

        protected override void OnPlanned(PlannedAttack a)
        {
            ClearBolts();
            if (a.Pattern.Unblockable) return;
            for (int i = 0; i < a.HitCount; i++) _bolts.Add(new Bolt { Index = i, HitBeat = a.HitBeat(i) });
        }

        void UpdateBolts(bool live)
        {
            if (_bolts.Count == 0) return;
            double now = BeatNow;
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var b = _bolts[i];
                double left = b.HitBeat - now;
                if (b.Go == null)
                {
                    if (left > 1.0 || !live) continue;              // sung one eighth before it lands
                    b.From = Mouth;
                    b.Go = MakeBolt(b.Index == 3 ? 0.42f : 0.28f);
                }
                if (b.Result == null)
                {
                    float k = 1f - Mathf.Clamp01((float)left);
                    var target = Chest;
                    var arc = Vector3.up * (0.5f * Mathf.Sin(Mathf.PI * k));
                    b.Go.transform.position = Vector3.Lerp(b.From, target, k) + arc;
                    b.Go.transform.localScale = Vector3.one * (b.Index == 3 ? 0.42f : 0.28f) * (1f + 0.25f * Mathf.Sin(Time.time * 30f));
                    if (left < -1.5) { Destroy(b.Go); _bolts.RemoveAt(i); }
                    continue;
                }
                // After it resolves: a parry throws it back at the singer, a block bursts it, a hit bursts on the player,
                // out of range lets it sail on and fade.
                b.After += Time.deltaTime;
                var go = b.Go;
                switch (b.Result.Value)
                {
                    case DefenseResult.Parried:
                        go.transform.position = Vector3.Lerp(go.transform.position, Mouth, 1f - Mathf.Exp(-14f * Time.deltaTime));
                        break;
                    case DefenseResult.OutOfRange:
                        go.transform.position += (go.transform.position - b.From).normalized * 9f * Time.deltaTime;
                        break;
                    default:
                        go.transform.localScale *= 1f + 6f * Time.deltaTime;
                        break;
                }
                if (b.After > 0.35f) { Destroy(go); _bolts.RemoveAt(i); }
            }
        }

        GameObject MakeBolt(float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "Song Bolt";
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<MeshRenderer>();
            if (boltMaterial != null) r.sharedMaterial = boltMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", boltColor);
            mpb.SetColor("_Color", boltColor);
            r.SetPropertyBlock(mpb);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point; light.range = 2.5f; light.intensity = 1.2f; light.color = boltColor;
            go.transform.position = Mouth;
            return go;
        }

        void ClearBolts()
        {
            foreach (var b in _bolts) if (b.Go != null) Destroy(b.Go);
            _bolts.Clear();
        }

        protected override void OnHitResolved(int index, DefenseResult result)
        {
            foreach (var b in _bolts) if (b.Index == index && b.Go != null && b.Result == null) { b.Result = result; b.After = 0f; }
        }

        // ---------------- The sustained note: a ring along the floor ----------------

        void BuildRing()
        {
            var go = new GameObject("Sustain Ring", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            _ring = go.transform;
            _ringSprite = go.GetComponent<SpriteRenderer>();
            _ringSprite.sprite = UI.ProceduralSprites.Ring;
            _ringSprite.sortingOrder = 15;
            _ringSprite.enabled = false;
        }

        void UpdateRing()
        {
            if (_ring == null) return;
            bool on = Sustain && Phase != AttackPhase.Waiting;
            float s = on ? SinceHit(0) : -9f;
            _ringSprite.enabled = on && s > -0.6f && s < 1.6f;
            if (!_ringSprite.enabled) return;
            // Swells at the singer's feet as the note builds, then rolls out across the floor on the beat.
            float radius = s < 0f ? 0.6f + 0.6f * (s + 0.6f) / 0.6f : 1.2f + reach * Mathf.Clamp01(s / 1.2f);
            _ring.position = new Vector3(transform.position.x, transform.position.y + 0.08f, transform.position.z);
            _ring.rotation = Quaternion.Euler(90f, 0f, 0f);
            _ring.localScale = Vector3.one * radius * 2.35f;
            var c = sustainColor;
            c.a = s < 0f ? 0.5f + 0.5f * (s + 0.6f) / 0.6f : 1f - Mathf.Clamp01(s / 1.6f);
            _ringSprite.color = c;
        }

        // ---------------- Pose ----------------

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float Loop(BakedClip c) => c == null ? 0f : (float)(Time.timeAsDouble / Mathf.Max(0.1f, c.length));

        void Pose(float dt, AttackPhase phase)
        {
            _staggerT += dt;
            float x = transform.position.x;
            bool walking = Mathf.Abs(x - _lastX) > 0.5f * dt;
            _lastX = x;
            if (_pose == null) { PrimitivePose(phase); return; }
            _pose.Reset();
            var stance = walking && walkClip != null ? walkClip : idleClip;
            _pose.Layer(stance, Loop(stance), 1f, true);
            if (Plan.IsValid && !IsStaggered)
            {
                if (Sustain && sustainClip != null)
                {
                    float w = phase == AttackPhase.Telegraph ? Smooth(Windup * 1.5f) : 1f - Smooth(PoseSince(0) - 0.5f);
                    _pose.Layer(sustainClip, Mathf.Clamp01(0.2f + 0.6f * Windup), w);
                }
                else if (castClip != null)
                {
                    // One throw per bolt, its release on the beat the bolt is sung (one eighth before it lands).
                    for (int i = 0; i < Plan.HitCount; i++)
                    {
                        float tau = (SinceHit(i) + 1f) / Mathf.Max(0.01f, poseBeatScale);
                        if (tau < -0.6f || tau > 0.6f) continue;
                        float u = ClipTiming.Warp(tau, -0.5f, 0.5f, 0f, castHit, 1f);
                        _pose.Layer(castClip, u, 1f - Smooth((Mathf.Abs(tau) - 0.35f) / 0.25f));
                    }
                }
            }
            if (staggerClip != null && IsStaggered) _pose.Layer(staggerClip, Mathf.Clamp01(_staggerT / 0.7f), 1f);
            _pose.Apply();
        }

        void PrimitivePose(AttackPhase phase)
        {
            if (body == null || body.childCount == 0) return;
            var t = body.GetChild(0);
            float lean = phase == AttackPhase.Telegraph ? -10f * Smooth(Windup) : 0f;
            float sway = Mathf.Sin((float)(BeatNow / 3.0) * Mathf.PI) * 4f;
            t.localRotation = Quaternion.Euler(lean, 0f, sway);
        }

        // ---------------- Hits ----------------

        protected override bool InHitRange()
        {
            if (Player == null) return false;
            float dx = Mathf.Abs(Player.position.x - transform.position.x);
            float dy = Player.position.y - transform.position.y;
            if (Sustain) return dx < reach && dy < 0.6f && dy > -1.5f;          // jump over the ring
            return dx < reach && Mathf.Abs(dy) < hitHeight;
        }

        protected override Vector3 HitOrigin => Sustain ? transform.position : Mouth;

        protected override void Shove(float dx)
        {
            var p = transform.position;
            p.x = Mathf.Clamp(p.x + dx, bounds.x, bounds.y);
            transform.position = p;
        }

        protected override void OnFloorChanged(float minX, float maxX) => bounds = new Vector2(minX, maxX);

        protected override void OnStaggered()
        {
            _staggerT = 0f;
            foreach (var b in _bolts) if (b.Result == null && b.Go != null) { b.Result = DefenseResult.Blocked; b.After = 0f; }
        }

        protected override void OnReset()
        {
            _face = -1;
            ClearBolts();
            ApplyFacing(true);
        }

        protected override void OnDied()
        {
            ClearBolts();
            if (_ringSprite != null) _ringSprite.enabled = false;
            StartCoroutine(Fall());
        }

        IEnumerator Fall()
        {
            float t = 0f;
            if (_pose != null && deathClip != null)
            {
                while (t < 1f)
                {
                    t += Time.deltaTime / Mathf.Max(0.3f, deathClip.length);
                    _pose.Reset();
                    _pose.Layer(deathClip, t);
                    _pose.Apply();
                    yield return null;
                }
                yield return new WaitForSeconds(1f);
            }
            else
            {
                var start = body != null ? body.localRotation : Quaternion.identity;
                while (t < 1f)
                {
                    t += Time.deltaTime / 0.5f;
                    if (body != null) body.localRotation = start * Quaternion.Euler(0f, 0f, -85f * Smooth(t) * _face);
                    yield return null;
                }
                yield return new WaitForSeconds(0.8f);
            }
            var p0 = transform.position;
            for (t = 0f; t < 1f; t += Time.deltaTime / 0.8f) { transform.position = p0 + Vector3.down * 1.2f * t; yield return null; }
            gameObject.SetActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearBolts();
        }
    }
}
