using System.Collections.Generic;
using ProjectHymn.Combat;
using ProjectHymn.Core;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// Visual rhythm feedback for the sword prototype (GDD M2):
    /// - a ring on the floor at every on-beat note (gold Perfect, white Great, blue Good), a grey puff off the beat;
    /// - sparks where the blade connects, many and bright when the hit dealt damage, a few dull ones when it didn't;
    /// - a big burst and two rings when a rhythm sequence is completed, in the sequence's colour;
    /// - a light on the blade that flashes on every on-beat hit.
    /// Sword tracers are TrailRenderers on the blade tips, driven by the RhythmAnimator.
    /// </summary>
    public class SwordFx : MonoBehaviour
    {
        [SerializeField] SwordCombatController combat;
        [SerializeField] Material additive;
        [SerializeField] Transform player;
        [SerializeField] FollowCamera cameraRig;

        ParticleSystem _sparks, _burst, _dust;
        Light _bladeLight;
        float _lightTime;
        readonly List<(LineRenderer line, float age, float life, float r0, float r1, Color color)> _rings = new List<(LineRenderer, float, float, float, float, Color)>();
        readonly List<LineRenderer> _ringPool = new List<LineRenderer>();
        const int RingPoints = 48;

        void Awake()
        {
            if (combat == null) combat = FindAnyObjectByType<SwordCombatController>();
            if (player == null && combat != null) player = combat.transform;
            if (cameraRig == null) cameraRig = FindAnyObjectByType<FollowCamera>();
            _sparks = MakeSystem("Sparks", 0.35f, new Vector2(3f, 8f), new Vector2(0.03f, 0.08f), 0.8f, stretch: true);
            _burst = MakeSystem("Burst", 0.7f, new Vector2(2f, 6f), new Vector2(0.08f, 0.18f), -0.1f, stretch: false);
            _dust = MakeSystem("Dust", 0.5f, new Vector2(0.3f, 1.2f), new Vector2(0.1f, 0.25f), -0.05f, stretch: false);
            var lightGo = new GameObject("Blade Light", typeof(Light));
            lightGo.transform.SetParent(transform, false);
            _bladeLight = lightGo.GetComponent<Light>();
            _bladeLight.type = LightType.Point;
            _bladeLight.range = 3f;
            _bladeLight.intensity = 0f;
        }

        void OnEnable()
        {
            if (combat == null) return;
            combat.NoteJudged += OnNote;
            combat.Impacted += OnImpact;
            combat.SequenceCompleted += OnSequence;
        }

        void OnDisable()
        {
            if (combat == null) return;
            combat.NoteJudged -= OnNote;
            combat.Impacted -= OnImpact;
            combat.SequenceCompleted -= OnSequence;
        }

        ParticleSystem MakeSystem(string name, float life, Vector2 speed, Vector2 size, float gravity, bool stretch)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(transform, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (additive != null) r.sharedMaterial = additive;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = stretch ? 0.04f : 0f;
            r.lengthScale = stretch ? 1.2f : 1f;
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 at, Color color, int count)
        {
            var p = new ParticleSystem.EmitParams { position = at, startColor = color, applyShapeToPosition = true };
            ps.Emit(p, count);
        }

        static Color JudgementColor(Judgement j) => j switch
        {
            Judgement.Perfect => new Color(1f, 0.82f, 0.25f),
            Judgement.Great => new Color(0.95f, 0.95f, 1f),
            Judgement.Good => new Color(0.5f, 0.8f, 1f),
            _ => new Color(0.55f, 0.55f, 0.6f),
        };

        void OnNote(NoteResult n)
        {
            if (player == null) return;
            var feet = player.position + Vector3.up * 0.03f;
            if (n.OnBeat) Ring(feet, 0.25f, 1.1f, 0.35f, JudgementColor(n.Effective));
            else Emit(_dust, feet + Vector3.up * 0.1f, new Color(0.5f, 0.5f, 0.55f, 0.6f), 10);
        }

        void OnImpact(ImpactInfo i)
        {
            var c = i.Note.OnBeat ? i.Note.Move.trailColor : new Color(0.6f, 0.6f, 0.65f);
            if (i.TargetsHit > 0) Emit(_sparks, i.Point, i.DealtDamage ? c : new Color(0.7f, 0.7f, 0.75f), i.DealtDamage ? (i.CompletedSequence ? 60 : 26) : 6);
            if (i.Note.OnBeat)
            {
                var tip = combat.Animator != null ? combat.Animator.Tip : null;
                if (tip != null) _bladeLight.transform.position = tip.position;
                _bladeLight.color = c;
                _lightTime = i.DealtDamage ? 0.18f : 0.08f;
            }
        }

        void OnSequence(SwordSequence s)
        {
            if (player == null) return;
            var at = player.position + Vector3.up * 1.0f;
            Emit(_burst, at, s.color, 70);
            Ring(player.position + Vector3.up * 0.03f, 0.4f, 2.6f, 0.55f, s.color);
            Ring(player.position + Vector3.up * 0.03f, 0.2f, 1.8f, 0.45f, Color.white);
            if (cameraRig != null) cameraRig.Shake(0.12f, 0.2f);
        }

        void Ring(Vector3 center, float r0, float r1, float life, Color color)
        {
            LineRenderer line = null;
            foreach (var l in _ringPool) if (!l.gameObject.activeSelf) { line = l; break; }
            if (line == null)
            {
                var go = new GameObject("Beat Ring", typeof(LineRenderer));
                go.transform.SetParent(transform, false);
                line = go.GetComponent<LineRenderer>();
                line.loop = true;
                line.positionCount = RingPoints;
                line.useWorldSpace = true;
                line.widthMultiplier = 0.06f;
                if (additive != null) line.sharedMaterial = additive;
                _ringPool.Add(line);
            }
            line.gameObject.SetActive(true);
            line.transform.position = center;
            _rings.Add((line, 0f, life, r0, r1, color));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var r = _rings[i];
                r.age += dt;
                float k = Mathf.Clamp01(r.age / r.life);
                float radius = Mathf.Lerp(r.r0, r.r1, 1f - (1f - k) * (1f - k));
                var c = r.color; c.a = 1f - k;
                r.line.startColor = r.line.endColor = c;
                var center = r.line.transform.position;
                for (int p = 0; p < RingPoints; p++)
                {
                    float a = p / (float)RingPoints * Mathf.PI * 2f;
                    r.line.SetPosition(p, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                }
                if (k >= 1f) { r.line.gameObject.SetActive(false); _rings.RemoveAt(i); }
                else _rings[i] = r;
            }

            if (_lightTime > 0f)
            {
                _lightTime -= dt;
                _bladeLight.intensity = Mathf.Max(0f, _lightTime) * 30f;
                var tip = combat != null && combat.Animator != null ? combat.Animator.Tip : null;
                if (tip != null) _bladeLight.transform.position = tip.position;
            }
            else _bladeLight.intensity = 0f;
        }
    }
}
