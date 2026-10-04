using System;
using System.Collections.Generic;
using ProjectHymn.Core;
using ProjectHymn.Movement;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Plays the swordsman's keyframed moves on the music's clock (GDD M2 prototype).
    ///
    /// - Time comes from the Conductor's song time, not frame time: a move's keys are in beats around its hit, the hit
    ///   is placed on the judged beat, so at 90 BPM a cut is slow and at 170 BPM it snaps, always landing on the beat.
    /// - Tempo also shapes the keys: on fast songs the wind-up and follow-through swing less far (they are pulled
    ///   toward the hit pose), on slow songs a little further, so swings stay readable at any tempo.
    /// - Anticipation: when the combat code knows the next note of a rhythm, the next move's wind-up starts half a beat
    ///   early and holds just before the hit; if the player presses on the beat it continues without a seam.
    /// - A late press catches up: the move starts from its cocked pose and eases into musical time over a quarter beat.
    /// - Off-beat presses play the move unsynced and a little sluggish.
    /// - Calibration: <see cref="AnimationOffsetMs"/> shifts all animation against the audio (saved per machine).
    /// - Two-handed weapons keep the left hand on the hilt with two-bone IK.
    /// - Locomotion (vertical slice 1): with an <see cref="ILocomotionSource"/> and a <see cref="LocomotionSet"/>, the
    ///   body runs, jumps, flips, hangs and climbs; the run cycle's footfalls follow the beat. A sword move then owns
    ///   the upper body (spine up and both arms) while the legs and hips keep moving; standing still it owns everything.
    /// Raises <see cref="Impact"/> when a move reaches its hit key; combat applies damage then.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class RhythmAnimator : MonoBehaviour
    {
        public const string OffsetPrefKey = "ProjectHymn.AnimOffsetMs";

        [SerializeField] Conductor conductor;
        [Tooltip("Moved for lunges and crouches (the model under the player).")]
        [SerializeField] Transform modelRoot;
        [SerializeField] Transform[] bones = new Transform[0];
        [SerializeField] Transform grip;
        [SerializeField] WeaponSet weapon;

        [Header("Timing")]
        [Tooltip("Shift animation against the audio. Positive = animation earlier.")]
        [SerializeField] float animationOffsetMs;
        [SerializeField] float blendSeconds = 0.07f;
        [Tooltip("A late press starts from this point of the move (beats) and catches up with the music.")]
        [SerializeField] float lateStartBeat = -0.12f;
        [SerializeField] float catchUpBeats = 0.25f;
        [Tooltip("Off-beat moves play at this share of the beat's speed.")]
        [SerializeField, Range(0.3f, 1f)] float offBeatSpeed = 0.75f;
        [Tooltip("Anticipated wind-ups hold here until the press arrives.")]
        [SerializeField] float anticipationHoldBeat = -0.06f;

        [Header("Tempo shaping")]
        [SerializeField] float referenceBpm = 130f;
        [SerializeField] Vector2 amplitudeRange = new Vector2(0.75f, 1.15f);

        [Header("Idle")]
        [SerializeField] float idleBob = 0.025f;

        [Header("Locomotion")]
        [SerializeField] LocomotionSet locomotion;
        [SerializeField] float locoBlendSeconds = 0.12f;
        [Tooltip("How fast the legs switch between the sword move and the locomotion pose.")]
        [SerializeField] float lowerBodyBlend = 0.12f;

        /// <summary>Bones a sword move keeps while the player is moving; the rest follow the locomotion pose.</summary>
        static readonly string[] UpperBody = { "Spine", "Chest", "Neck", "Head", "UpperArm.R", "LowerArm.R", "Hand.R", "UpperArm.L", "LowerArm.L", "Hand.L" };
        static readonly object[] LocoKeys = BuildLocoKeys();
        static object[] BuildLocoKeys()
        {
            var keys = new object[Enum.GetValues(typeof(LocoState)).Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new object();
            return keys;
        }

        class Track
        {
            public SwordMove Move;
            public double BeatTime, Spb, Start;
            public float StartT, CatchUp;
            public bool OnBeat, ImpactDone;
            public int Id;
            public float Amplitude;
        }

        readonly Dictionary<string, int> _index = new Dictionary<string, int>();
        Quaternion[] _display, _target, _from;
        Vector3 _rootDisplay, _rootTarget, _rootFrom, _modelBase;
        float _blend = 1f;
        object _source;          // what the pose currently comes from (track, or null = idle)
        Track _current, _anticipate;
        int _nextId = 1;
        Transform _offHand, _tip;
        TrailRenderer _trail;
        bool _trailOn;
        ILocomotionSource _loco;
        Quaternion[] _locoPose;
        Vector3 _locoRoot;
        bool[] _upper;
        string[] _names;
        float _lowerWeight, _blendDuration;
        int _hips = -1, _spine = -1;

        /// <summary>A move reached its hit key. Arg: the id returned by <see cref="Play"/>.</summary>
        public event Action<int> Impact;

        public WeaponSet Weapon => weapon;
        public Transform Tip => _tip;
        public bool IsAttacking => _current != null;
        public float AnimationOffsetMs
        {
            get => animationOffsetMs;
            set { animationOffsetMs = Mathf.Clamp(value, -250f, 250f); PlayerPrefs.SetFloat(OffsetPrefKey, animationOffsetMs); }
        }
        /// <summary>The current move's time in beats around its hit (NaN when idle). For the HUD and debugging.</summary>
        public float CurrentBeatPosition { get; private set; } = float.NaN;
        public LocomotionSet Locomotion { get => locomotion; set => locomotion = value; }

        void Awake()
        {
            if (conductor == null) conductor = FindAnyObjectByType<Conductor>();
            animationOffsetMs = PlayerPrefs.GetFloat(OffsetPrefKey, animationOffsetMs);
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) _index[bones[i].name] = i;
            _display = new Quaternion[bones.Length];
            _target = new Quaternion[bones.Length];
            _from = new Quaternion[bones.Length];
            _locoPose = new Quaternion[bones.Length];
            _upper = new bool[bones.Length];
            _names = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                _names[i] = bones[i] != null ? bones[i].name : "";
                _upper[i] = Array.IndexOf(UpperBody, _names[i]) >= 0;
            }
            if (!_index.TryGetValue("Hips", out _hips)) _hips = -1;
            if (!_index.TryGetValue("Spine", out _spine)) _spine = -1;
            _loco = GetComponentInParent<ILocomotionSource>();
            if (modelRoot != null) _modelBase = modelRoot.localPosition;
            SetWeapon(weapon);
            Evaluate(null, 0f, 1f);
            Array.Copy(_target, _display, _target.Length);
            _rootDisplay = _rootTarget;
        }

        void OnEnable() { if (conductor != null) conductor.SongStarted += OnSongStarted; }
        void OnDisable() { if (conductor != null) conductor.SongStarted -= OnSongStarted; }

        void OnSongStarted(SongData s) => Stop();

        /// <summary>Switch stance and sword model.</summary>
        public void SetWeapon(WeaponSet w)
        {
            weapon = w;
            Stop();
            if (grip == null || w == null) return;
            _tip = null; _offHand = null; _trail = null;
            foreach (Transform sword in grip)
            {
                bool on = sword.name == w.swordObject;
                sword.gameObject.SetActive(on);
                if (!on) continue;
                _tip = sword.Find("Tip");
                _offHand = sword.Find("OffHandGrip");
                _trail = _tip != null ? _tip.GetComponent<TrailRenderer>() : null;
                if (_trail != null) { _trail.emitting = false; _trail.Clear(); }
            }
        }

        /// <summary>Drop every move and go back to the stance (song change, weapon swap).</summary>
        public void Stop()
        {
            _current = null;
            _anticipate = null;
            StartBlend(null);
        }

        // ---------------- API for the combat code ----------------

        /// <summary>
        /// Play a move whose hit belongs on <paramref name="beatTime"/> (song seconds) for an on-beat note, or right away
        /// for an off-beat one. Returns an id that <see cref="Impact"/> reports back.
        /// </summary>
        public int Play(SwordMove move, double beatTime, double secondsPerBeat, bool onBeat)
        {
            if (move == null || move.keys.Count == 0) return 0;
            double now = Now();
            double spb = Math.Max(0.05, secondsPerBeat);

            // The anticipated move was right: keep playing it, no seam.
            if (onBeat && _anticipate != null && _anticipate.Move == move && Math.Abs(_anticipate.BeatTime - beatTime) < 0.02)
            {
                var a = _anticipate;
                _anticipate = null;
                float t = (float)((now - a.BeatTime) / a.Spb);
                float shown = Mathf.Min(t, anticipationHoldBeat);
                a.StartT = t;
                a.CatchUp = Mathf.Min(0f, shown - t);   // late press: resume from the held pose and catch up
                a.Id = _nextId++;
                _current = a;
                _source = a;
                return a.Id;
            }

            var tr = new Track
            {
                Move = move, Spb = spb, OnBeat = onBeat, Id = _nextId++, Amplitude = AmplitudeFor(spb),
                BeatTime = onBeat ? beatTime : now, Start = now,
            };
            if (onBeat)
            {
                float t = (float)((now - beatTime) / spb);
                tr.StartT = t;
                tr.CatchUp = t > lateStartBeat ? lateStartBeat - t : 0f; // early presses just follow the music
            }
            _anticipate = null;
            _current = tr;
            StartBlend(tr);
            return tr.Id;
        }

        /// <summary>
        /// Play a move from its first key right now at the music's tempo (the guard: it reacts to the press rather than
        /// landing on a beat). Cancels any move in progress. Returns 0: its key 0 raises no impact anyone waits for.
        /// </summary>
        public int PlayNow(SwordMove move, double secondsPerBeat)
        {
            if (move == null || move.keys.Count == 0) return 0;
            double spb = Math.Max(0.05, secondsPerBeat);
            double now = Now();
            var tr = new Track
            {
                Move = move, Spb = spb, OnBeat = true, Id = _nextId++, Amplitude = 1f,
                BeatTime = now - move.FirstBeat * spb, Start = now, StartT = move.FirstBeat,
            };
            _anticipate = null;
            _current = tr;
            StartBlend(tr);
            return 0;
        }

        /// <summary>The next note will probably be this move on this beat: start its wind-up in time.</summary>
        public void Anticipate(SwordMove move, double beatTime, double secondsPerBeat)
        {
            if (move == null || move.keys.Count == 0) { _anticipate = null; return; }
            _anticipate = new Track
            {
                Move = move, BeatTime = beatTime, Spb = Math.Max(0.05, secondsPerBeat), OnBeat = true,
                Amplitude = AmplitudeFor(secondsPerBeat),
            };
        }

        public void ClearAnticipation() => _anticipate = null;

        // ---------------- Per frame ----------------

        double Now() => conductor != null ? conductor.SongTime + animationOffsetMs / 1000.0 : Time.timeAsDouble;

        float AmplitudeFor(double spb)
        {
            float bpm = (float)(60.0 / Math.Max(0.05, spb));
            return Mathf.Clamp(referenceBpm / Mathf.Max(1f, bpm), amplitudeRange.x, amplitudeRange.y);
        }

        float Tau(Track t, double now)
        {
            if (!t.OnBeat) return lateStartBeat + (float)((now - t.Start) / t.Spb) * offBeatSpeed;
            float x = (float)((now - t.BeatTime) / t.Spb);
            if (t.CatchUp < 0f)
            {
                float k = Mathf.Clamp01((x - t.StartT) / catchUpBeats);
                x += t.CatchUp * (1f - k * k * (3f - 2f * k));
            }
            return x;
        }

        void LateUpdate()
        {
            if (bones.Length == 0 || weapon == null) return;
            double now = Now();
            object source = null;
            float tau = float.NaN;
            bool active = false;
            bool moving = EvaluateLocomotion(now, out var locoState);
            _lowerWeight = Mathf.MoveTowards(_lowerWeight, moving ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, lowerBodyBlend));

            if (_current != null)
            {
                tau = Tau(_current, now);
                if (!_current.ImpactDone && tau >= 0f)
                {
                    _current.ImpactDone = true;
                    Impact?.Invoke(_current.Id);
                }
                if (tau > _current.Move.LastBeat) _current = null;
                else
                {
                    source = _current;
                    active = tau >= _current.Move.activeStart && tau <= _current.Move.activeEnd;
                    Evaluate(_current.Move, tau, _current.Amplitude);
                    MixLowerBody();
                }
            }

            if (_current == null && _anticipate != null)
            {
                float t = (float)((now - _anticipate.BeatTime) / _anticipate.Spb);
                if (t > 0.3f) _anticipate = null;                    // the note never came
                else if (t >= Mathf.Max(-0.5f, _anticipate.Move.FirstBeat))
                {
                    source = _anticipate;
                    tau = Mathf.Min(t, anticipationHoldBeat);
                    Evaluate(_anticipate.Move, tau, _anticipate.Amplitude);
                    MixLowerBody();
                }
            }

            if (source == null)
            {
                if (moving)
                {
                    Array.Copy(_locoPose, _target, _target.Length);
                    _rootTarget = _locoRoot;
                    source = LocoKeys[(int)locoState];
                }
                else Evaluate(null, 0f, 1f);
                tau = float.NaN;
            }
            if (!ReferenceEquals(source, _source)) StartBlend(source);

            CurrentBeatPosition = tau;
            ApplyPose();
            SolveOffHand();
            UpdateTrail(active, _current != null ? _current.Move : null, _current != null && _current.OnBeat);
        }

        void StartBlend(object source)
        {
            _source = source;
            _blendDuration = source == null || source is Track ? blendSeconds : locoBlendSeconds;
            if (_display == null) return;
            Array.Copy(_display, _from, _display.Length);
            _rootFrom = _rootDisplay;
            _blend = 0f;
        }

        void ApplyPose()
        {
            float dt = Time.unscaledDeltaTime;
            _blend = Mathf.Min(1f, _blend + (_blendDuration > 0f ? dt / _blendDuration : 1f));
            float w = _blend * _blend * (3f - 2f * _blend);
            for (int i = 0; i < bones.Length; i++)
            {
                _display[i] = w >= 1f ? _target[i] : Quaternion.Slerp(_from[i], _target[i], w);
                if (bones[i] != null) bones[i].localRotation = _display[i];
            }
            _rootDisplay = Vector3.Lerp(_rootFrom, _rootTarget, w);
            if (modelRoot != null) modelRoot.localPosition = _modelBase + _rootDisplay;
        }

        // ---------------- Pose evaluation ----------------

        Quaternion StanceRot(string bone) =>
            weapon != null && weapon.stance.TryGet(bone, out var e) ? Quaternion.Euler(e) : Quaternion.identity;

        Quaternion KeyRot(MoveKey k, string bone) => k.TryGet(bone, out var e) ? Quaternion.Euler(e) : StanceRot(bone);

        /// <summary>Fill the target pose: the stance (move == null, with a beat bob) or a move at <paramref name="tau"/>.</summary>
        void Evaluate(SwordMove move, float tau, float amplitude)
        {
            if (move == null || move.keys.Count == 0)
            {
                for (int i = 0; i < bones.Length; i++) _target[i] = bones[i] != null ? StanceRot(bones[i].name) : Quaternion.identity;
                _rootTarget = weapon != null ? weapon.stance.rootOffset : Vector3.zero;
                if (conductor != null && conductor.IsPlaying)
                {
                    // Idle groove: dip on every beat, more on the downbeat. Shows the tempo before any attack.
                    float k = Mathf.Exp(-conductor.BeatPhase * 6f) * (conductor.CurrentBeat.IsDownbeat ? 1.6f : 1f);
                    _rootTarget += Vector3.down * idleBob * k;
                    if (_index.TryGetValue("Chest", out int c)) _target[c] *= Quaternion.Euler(6f * k, 0f, 0f);
                }
                return;
            }

            var keys = move.keys;
            int hit = 0;
            for (int i = 0; i < keys.Count; i++) if (Mathf.Abs(keys[i].beat) < Mathf.Abs(keys[hit].beat)) hit = i;
            int a = 0;
            while (a < keys.Count - 2 && keys[a + 1].beat <= tau) a++;
            int b = Mathf.Min(a + 1, keys.Count - 1);
            float span = keys[b].beat - keys[a].beat;
            float u = span > 1e-4f ? Mathf.Clamp01((tau - keys[a].beat) / span) : 1f;
            u = u * u * (3f - 2f * u);

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                string n = bones[i].name;
                var qa = Shape(keys, a, hit, n, amplitude);
                var qb = Shape(keys, b, hit, n, amplitude);
                _target[i] = Quaternion.Slerp(qa, qb, u);
            }
            var stanceRoot = weapon != null ? weapon.stance.rootOffset : Vector3.zero;
            _rootTarget = stanceRoot + Vector3.Lerp(keys[a].rootOffset * Amp(keys, a, hit, amplitude), keys[b].rootOffset * Amp(keys, b, hit, amplitude), u);
        }

        static float Amp(List<MoveKey> keys, int k, int hit, float amplitude) => k == hit ? 1f : amplitude;

        /// <summary>Tempo shaping: keys other than the hit swing toward (fast songs) or past (slow songs) the hit pose.</summary>
        Quaternion Shape(List<MoveKey> keys, int k, int hit, string bone, float amplitude)
        {
            var q = KeyRot(keys[k], bone);
            if (k == hit || Mathf.Approximately(amplitude, 1f)) return q;
            return Quaternion.SlerpUnclamped(KeyRot(keys[hit], bone), q, amplitude);
        }

        // ---------------- Locomotion layer ----------------

        /// <summary>
        /// Fill <see cref="_locoPose"/> from the movement state. Returns false when the body is standing still on the
        /// ground (the stance and the sword moves own the whole body then).
        /// </summary>
        bool EvaluateLocomotion(double now, out LocoState state)
        {
            state = LocoState.Idle;
            if (_loco == null || locomotion == null) return false;
            state = _loco.State;
            var L = locomotion;
            switch (state)
            {
                case LocoState.Run:
                {
                    float speed = _loco.Speed01;
                    if (speed < 0.05f || L.run.Count == 0) { state = LocoState.Idle; return false; }
                    double spb, beatPos;
                    if (conductor != null && conductor.IsPlaying && conductor.Map != null)
                    {
                        spb = conductor.CurrentBeat.SecondsPerBeat;
                        beatPos = conductor.Map.TimeToBeat(now);
                    }
                    else
                    {
                        spb = 60.0 / referenceBpm;
                        beatPos = Time.timeAsDouble / spb;
                    }
                    float phase = StrideClock.Phase(beatPos, StrideClock.StepsPerBeat(spb, L.cadence));
                    SampleCycle(L.run, phase, Mathf.Lerp(L.minStride, 1f, speed));
                    return true;
                }
                case LocoState.Jump: PoseLerp(L.jump, L.jump, 0f); return true;
                case LocoState.Fall: PoseLerp(L.fall, L.fall, 0f); return true;
                case LocoState.Land: PoseLerp(L.land, L.land, 0f); return true;
                case LocoState.Hang: PoseLerp(L.hang, L.hang, 0f); return true;
                case LocoState.AirJump:
                {
                    PoseLerp(L.airJump, L.airJump, 0f);
                    float k = _loco.StateTime / Mathf.Max(0.05f, L.flipSeconds);
                    if (k < 1f && _current == null && _hips >= 0)
                    {
                        // One forward flip around the hips; eased so it snaps through the middle.
                        float e = k * k * (3f - 2f * k);
                        _locoPose[_hips] = Quaternion.AngleAxis(360f * e, Vector3.right) * _locoPose[_hips];
                    }
                    else if (k < 1f) PoseLerp(L.fall, L.fall, 0f);
                    return true;
                }
                case LocoState.Climb:
                {
                    float k = _loco.ClimbProgress;
                    if (k < 0.5f) PoseLerp(L.hang, L.climbPull, Smooth(k / 0.5f));
                    else PoseLerp(L.climbPull, L.climbOver, Smooth((k - 0.5f) / 0.5f));
                    return true;
                }
                default:
                    return false;
            }
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>A locomotion key's rotation for a bone: replaces the stance, adds to it, or (unlisted) is the stance.</summary>
        Quaternion LocoRot(MoveKey k, int i, out bool listed)
        {
            string n = _names[i];
            listed = false;
            if (k == null || !k.TryGet(n, out var e)) return StanceRot(n);
            listed = true;
            return locomotion.IsAdditive(n) ? StanceRot(n) * Quaternion.Euler(e) : Quaternion.Euler(e);
        }

        Vector3 StanceRoot => weapon != null ? weapon.stance.rootOffset : Vector3.zero;

        void PoseLerp(MoveKey a, MoveKey b, float u)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                var qa = LocoRot(a, i, out _);
                _locoPose[i] = u <= 0f ? qa : Quaternion.Slerp(qa, LocoRot(b, i, out _), u);
            }
            var ra = a != null ? a.rootOffset : Vector3.zero;
            var rb = b != null ? b.rootOffset : Vector3.zero;
            _locoRoot = StanceRoot + Vector3.Lerp(ra, rb, u);
        }

        /// <summary>Sample a looping cycle at <paramref name="phase"/>; <paramref name="amount"/> scales the swing.</summary>
        void SampleCycle(List<MoveKey> keys, float phase, float amount)
        {
            int a = keys.Count - 1;
            for (int i = 0; i < keys.Count; i++) if (keys[i].beat <= phase) a = i;
            int b = (a + 1) % keys.Count;
            float ta = keys[a].beat, tb = keys[b].beat;
            if (ta > phase) ta -= 1f;              // phase is before the first key: wrap from the last
            if (tb <= ta) tb += 1f;
            float u = Mathf.Clamp01((phase - ta) / Mathf.Max(1e-4f, tb - ta));
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                var qa = LocoRot(keys[a], i, out bool la);
                var qb = LocoRot(keys[b], i, out bool lb);
                var q = Quaternion.Slerp(qa, qb, u);
                if (amount < 0.999f)
                {
                    // Shrink the swing toward a neutral pose: straight limbs for listed bones, the stance otherwise.
                    var neutral = (la || lb) && !locomotion.IsAdditive(_names[i]) ? Quaternion.identity : StanceRot(_names[i]);
                    q = Quaternion.SlerpUnclamped(neutral, q, amount);
                }
                _locoPose[i] = q;
            }
            _locoRoot = StanceRoot + Vector3.Lerp(keys[a].rootOffset, keys[b].rootOffset, u) * amount;
        }

        /// <summary>
        /// While moving, a sword move keeps the upper body and the legs follow the locomotion pose. The move's hip turn
        /// is carried into the spine so the swing still faces where it was authored.
        /// </summary>
        void MixLowerBody()
        {
            float w = _lowerWeight;
            if (w <= 0f) return;
            if (_hips >= 0 && _spine >= 0)
            {
                var carried = Quaternion.Inverse(StanceRot("Hips")) * _target[_hips] * _target[_spine];
                _target[_spine] = Quaternion.Slerp(_target[_spine], carried, w);
            }
            for (int i = 0; i < bones.Length; i++)
                if (!_upper[i] && bones[i] != null) _target[i] = Quaternion.Slerp(_target[i], _locoPose[i], w);
            _rootTarget = Vector3.Lerp(_rootTarget, _locoRoot, w);
        }

        // ---------------- Two-handed grip ----------------

        void SolveOffHand()
        {
            if (weapon == null || !weapon.twoHanded || _offHand == null) return;
            if (_current != null && _current.Move.offHandFree) return;
            if (_loco != null && (_loco.State == LocoState.Hang || _loco.State == LocoState.Climb)) return;   // hands on the ledge
            if (!_index.TryGetValue("UpperArm.L", out int ua) || !_index.TryGetValue("LowerArm.L", out int la) || !_index.TryGetValue("Hand.L", out int ha)) return;
            TwoBoneIk(bones[ua], bones[la], bones[ha], _offHand.position);
            bones[ha].rotation = _offHand.rotation * Quaternion.Euler(-90f, 0f, 0f);
        }

        /// <summary>Analytic two-bone IK (law of cosines), bending in the arm's current plane.</summary>
        static void TwoBoneIk(Transform upper, Transform lower, Transform end, Vector3 target)
        {
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float lab = (b - a).magnitude, lcb = (b - c).magnitude;
            float lat = Mathf.Clamp((target - a).magnitude, 0.01f, lab + lcb - 0.01f);
            float acab0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (b - a).normalized), -1f, 1f));
            float babc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
            float acat0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (target - a).normalized), -1f, 1f));
            float acab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float babc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));
            Vector3 axis0 = Vector3.Cross(c - a, b - a);
            if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(c - a, upper.forward);
            axis0.Normalize();
            Vector3 axis1 = Vector3.Cross(c - a, target - a);
            if (axis1.sqrMagnitude < 1e-8f) axis1 = axis0;
            axis1.Normalize();
            var r0 = Quaternion.AngleAxis((acab1 - acab0) * Mathf.Rad2Deg, Quaternion.Inverse(upper.rotation) * axis0);
            var r1 = Quaternion.AngleAxis((babc1 - babc0) * Mathf.Rad2Deg, Quaternion.Inverse(lower.rotation) * axis0);
            var r2 = Quaternion.AngleAxis(acat0 * Mathf.Rad2Deg, Quaternion.Inverse(upper.rotation) * axis1);
            upper.localRotation = upper.localRotation * (r0 * r2);
            lower.localRotation = lower.localRotation * r1;
        }

        // ---------------- Trail ----------------

        void UpdateTrail(bool active, SwordMove move, bool onBeat)
        {
            if (_trail == null) return;
            if (active && !_trailOn && move != null)
            {
                var c = onBeat ? move.trailColor : new Color(0.6f, 0.6f, 0.65f);
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                          new[] { new GradientAlphaKey(onBeat ? 0.9f : 0.4f, 0f), new GradientAlphaKey(0f, 1f) });
                _trail.colorGradient = g;
            }
            _trailOn = active;
            _trail.emitting = active;
        }
    }
}
