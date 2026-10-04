using NUnit.Framework;
using ProjectHymn.Movement;

namespace ProjectHymn.Tests
{
    public class JumpRulesTests
    {
        const float Dt = 1f / 60f;

        [Test]
        public void GroundJumpOnPress()
        {
            var j = new JumpRules();
            Assert.AreEqual(JumpKind.None, j.Tick(Dt, true, false));
            Assert.AreEqual(JumpKind.Ground, j.Tick(Dt, true, true));
        }

        [Test]
        public void CoyoteTimeAllowsLateGroundJump()
        {
            var j = new JumpRules { CoyoteTime = 0.1f };
            j.Tick(Dt, true, false);
            for (int i = 0; i < 4; i++) j.Tick(Dt, false, false);     // ~0.067 s after leaving the ledge
            Assert.AreEqual(JumpKind.Ground, j.Tick(Dt, false, true));
        }

        [Test]
        public void AfterCoyoteThePressIsTheAirJump()
        {
            var j = new JumpRules { CoyoteTime = 0.1f };
            j.Tick(Dt, true, false);
            for (int i = 0; i < 12; i++) j.Tick(Dt, false, false);    // 0.2 s
            Assert.AreEqual(JumpKind.Air, j.Tick(Dt, false, true));
            Assert.AreEqual(JumpKind.None, j.Tick(Dt, false, true), "only one air jump");
        }

        [Test]
        public void DoubleJumpThenNoMore()
        {
            var j = new JumpRules();
            j.Tick(Dt, true, false);
            Assert.AreEqual(JumpKind.Ground, j.Tick(Dt, true, true));
            j.Tick(Dt, true, false);                                   // still touching the floor: locked out
            Assert.AreEqual(JumpKind.Air, j.Tick(Dt, false, true));
            Assert.AreEqual(0, j.AirJumpsLeft);
            Assert.AreEqual(JumpKind.None, j.Tick(Dt, false, true));
        }

        [Test]
        public void GroundContactRightAfterJumpingDoesNotRefill()
        {
            var j = new JumpRules { GroundLockout = 0.08f };
            j.Tick(Dt, true, false);
            j.Tick(Dt, true, true);
            Assert.AreEqual(JumpKind.Air, j.Tick(Dt, true, true), "the floor contact during the lockout is ignored");
        }

        [Test]
        public void BufferedPressJumpsOnLanding()
        {
            var j = new JumpRules { BufferTime = 0.12f };
            j.Tick(Dt, true, false);
            j.Tick(Dt, true, true);                                    // jump
            for (int i = 0; i < 10; i++) j.Tick(Dt, false, false);
            Assert.AreEqual(JumpKind.Air, j.Tick(Dt, false, true));    // double jump
            for (int i = 0; i < 10; i++) j.Tick(Dt, false, false);
            Assert.AreEqual(JumpKind.None, j.Tick(Dt, false, true), "no air jump left: the press is buffered");
            j.Tick(Dt, false, false);
            Assert.AreEqual(JumpKind.Ground, j.Tick(Dt, true, false), "lands within the buffer and jumps");
        }

        [Test]
        public void StaleBufferDoesNothing()
        {
            var j = new JumpRules { BufferTime = 0.12f, AirJumps = 0 };
            j.Tick(Dt, false, false);
            j.Tick(Dt, false, true);
            for (int i = 0; i < 12; i++) j.Tick(Dt, false, false);    // 0.2 s
            Assert.AreEqual(JumpKind.None, j.Tick(Dt, true, false));
        }

        [Test]
        public void ReleaseCutsRiseOnce()
        {
            var j = new JumpRules { ReleaseCut = 0.5f };
            j.Tick(Dt, true, false);
            j.Tick(Dt, true, true);
            Assert.AreEqual(10f, j.ApplyRelease(10f, true), 1e-4);
            Assert.AreEqual(4f, j.ApplyRelease(8f, false), 1e-4);
            Assert.AreEqual(3f, j.ApplyRelease(3f, false), 1e-4, "only once per jump");
        }

        [Test]
        public void LedgeGrabRefillsAirJump()
        {
            var j = new JumpRules();
            j.Tick(Dt, true, false);
            j.Tick(Dt, true, true);
            j.Tick(Dt, false, true);
            Assert.AreEqual(0, j.AirJumpsLeft);
            j.RefillAirJumps();
            Assert.AreEqual(JumpKind.Air, j.Tick(Dt, false, true));
        }
    }

    public class StrideClockTests
    {
        [Test]
        public void StepsPerBeatTracksTempo()
        {
            Assert.AreEqual(1.0, StrideClock.StepsPerBeat(60.0 / 130.0), 1e-9);
            Assert.AreEqual(2.0, StrideClock.StepsPerBeat(60.0 / 90.0), 1e-9);
            Assert.AreEqual(1.0, StrideClock.StepsPerBeat(60.0 / 170.0), 1e-9);
            Assert.AreEqual(0.5, StrideClock.StepsPerBeat(60.0 / 400.0), 1e-9);
        }

        [Test]
        public void FootfallsLandOnBeats()
        {
            // One step per beat: right foot on even beats (phase 0), left on odd beats (phase 0.5).
            Assert.AreEqual(0f, StrideClock.Phase(4.0, 1.0), 1e-6);
            Assert.AreEqual(0.5f, StrideClock.Phase(5.0, 1.0), 1e-6);
            Assert.AreEqual(0.25f, StrideClock.Phase(4.5, 1.0), 1e-6);
            // Two steps per beat: a full cycle every beat.
            Assert.AreEqual(0.5f, StrideClock.Phase(7.5, 2.0), 1e-6);
            Assert.AreEqual(0f, StrideClock.Phase(8.0, 2.0), 1e-6);
        }
    }

    public class CliffsideReachTests
    {
        static readonly JumpArc Arc = SliceTuning.Arc;
        static float V1 => SliceTuning.JumpSpeed;
        static float V2 => SliceTuning.AirJumpSpeed;

        [Test]
        public void TuningHeights()
        {
            Assert.AreEqual(SliceTuning.JumpHeight, Arc.MaxHeight(V1), 1e-3);
            Assert.AreEqual(SliceTuning.JumpHeight + SliceTuning.AirJumpHeight, Arc.MaxHeight(V1, V2), 1e-3);
        }

        [Test]
        public void SingleJumpClearsLessThanTheFirstTierWall()
        {
            // The first cliff teaches the double jump or the ledge grab: one plain jump only just reaches its lip.
            float wall = CliffsideLayout.Route[1].Top - CliffsideLayout.Route[0].Top;
            Assert.LessOrEqual(Arc.MaxHeight(V1), wall + 0.01f);
        }

        [Test]
        public void EveryStepOfTheRouteIsReachable()
        {
            var r = CliffsideLayout.Route;
            for (int i = 1; i < r.Length; i++)
            {
                var a = r[i - 1];
                var b = r[i];
                float rise = b.Top - a.Top;
                float gap = b.XMin - a.XMax;
                // Reach: land on top with a margin, or grab the lip (its top within grab range at the apex).
                float best = Arc.MaxHeight(V1, V2);
                bool canLand = best >= rise + 0.25f;
                bool canGrab = best + SliceTuning.GrabMax >= rise + 0.1f;
                Assert.IsTrue(canLand || canGrab, $"{a.Name} → {b.Name}: rise {rise} m is out of reach");
                if (gap > 0f)
                {
                    // Across a gap: must still be above the target's lip (or its grab point) after covering the gap.
                    float needAt = canLand ? rise : rise - SliceTuning.GrabMax + 0.2f;
                    float dist = Arc.Distance(SliceTuning.RunSpeed, V1, V2, needAt);
                    Assert.Greater(dist, gap + 0.6f, $"{a.Name} → {b.Name}: {gap} m gap is too wide");
                }
            }
        }

        [Test]
        public void RouteIsContiguousAndAscending()
        {
            var r = CliffsideLayout.Route;
            for (int i = 1; i < r.Length; i++)
            {
                Assert.GreaterOrEqual(r[i].XMin, r[i - 1].XMax - 0.001f, $"{r[i].Name} overlaps {r[i - 1].Name}");
                Assert.GreaterOrEqual(r[i].Top, r[i - 1].Top, $"{r[i].Name} is lower than {r[i - 1].Name}");
            }
        }

        [Test]
        public void MarkersStandOnTheRoute()
        {
            foreach (var m in CliffsideLayout.Markers)
            {
                if (m.Kind == CliffsideLayout.MarkerKind.CameraZone || m.Kind == CliffsideLayout.MarkerKind.FloatingTarget) continue;
                bool on = false;
                foreach (var b in CliffsideLayout.Route)
                    if (m.X > b.XMin + 0.5f && m.X < b.XMax - 0.5f && System.Math.Abs(m.Y - b.Top) < 0.01f) on = true;
                Assert.IsTrue(on, $"{m.Name} is not standing on a cliff top");
            }
        }

        [Test]
        public void FloatingTargetIsInJumpingRange()
        {
            foreach (var m in CliffsideLayout.MarkersOf(CliffsideLayout.MarkerKind.FloatingTarget))
            {
                // Over a gap, and its straw body (1.2 m above its base) within a plain jump's swing from the ledge before it.
                CliffsideLayout.Block before = default;
                foreach (var b in CliffsideLayout.Route) if (b.XMax <= m.X) before = b;
                float bodyAboveLedge = m.Y + 1.2f - before.Top;
                float swingHeight = 1.05f;   // the sword's hit centre above the feet
                Assert.LessOrEqual(bodyAboveLedge, Arc.MaxHeight(V1) + swingHeight + 0.5f);
                Assert.GreaterOrEqual(bodyAboveLedge, swingHeight);
            }
        }
    }
}
