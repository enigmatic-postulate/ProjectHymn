using NUnit.Framework;
using ProjectHymn.Movement;

namespace ProjectHymn.Tests
{
    /// <summary>The same reach checks as Cliffside's, for every stage (Belfry Ridge since VS-2).</summary>
    public class StageLayoutTests
    {
        static readonly JumpArc Arc = SliceTuning.Arc;
        static float V1 => SliceTuning.JumpSpeed;
        static float V2 => SliceTuning.AirJumpSpeed;

        [Test]
        public void EveryStageRouteIsReachable()
        {
            foreach (var stage in StageLayout.All)
            {
                var r = stage.Route;
                for (int i = 1; i < r.Length; i++)
                {
                    var a = r[i - 1];
                    var b = r[i];
                    float rise = b.Top - a.Top;
                    float gap = b.XMin - a.XMax;
                    float best = Arc.MaxHeight(V1, V2);
                    bool canLand = best >= rise + 0.25f;
                    bool canGrab = best + SliceTuning.GrabMax >= rise + 0.1f;
                    Assert.IsTrue(canLand || canGrab, $"{stage.Name}: {a.Name} → {b.Name}: rise {rise} m is out of reach");
                    if (gap > 0f)
                    {
                        float needAt = canLand ? rise : rise - SliceTuning.GrabMax + 0.2f;
                        float dist = Arc.Distance(SliceTuning.RunSpeed, V1, V2, needAt);
                        Assert.Greater(dist, gap + 0.6f, $"{stage.Name}: {a.Name} → {b.Name}: {gap} m gap is too wide");
                    }
                    Assert.GreaterOrEqual(b.XMin, a.XMax - 0.001f, $"{stage.Name}: {b.Name} overlaps {a.Name}");
                    Assert.GreaterOrEqual(b.Top, a.Top, $"{stage.Name}: {b.Name} is lower than {a.Name}");
                }
            }
        }

        [Test]
        public void EveryStageMarkerStandsOnItsRoute()
        {
            foreach (var stage in StageLayout.All)
                foreach (var m in stage.Markers)
                {
                    if (m.Kind == CliffsideLayout.MarkerKind.CameraZone || m.Kind == CliffsideLayout.MarkerKind.FloatingTarget) continue;
                    bool on = false;
                    foreach (var b in stage.Route)
                        if (m.X > b.XMin + 0.5f && m.X < b.XMax - 0.5f && System.Math.Abs(m.Y - b.Top) < 0.01f) on = true;
                    foreach (var b in stage.Secrets)
                        if (m.X > b.XMin + 0.3f && m.X < b.XMax - 0.3f && System.Math.Abs(m.Y - b.Top) < 0.01f) on = true;
                    Assert.IsTrue(on, $"{stage.Name}: {m.Name} is not standing on a top");
                }
        }

        [Test]
        public void FloatingTargetsAreInJumpingRange()
        {
            foreach (var stage in StageLayout.All)
                foreach (var m in stage.MarkersOf(CliffsideLayout.MarkerKind.FloatingTarget))
                {
                    CliffsideLayout.Block before = default;
                    foreach (var b in stage.Route) if (b.XMax <= m.X) before = b;
                    float bodyAboveLedge = m.Y + 1.2f - before.Top;
                    Assert.LessOrEqual(bodyAboveLedge, Arc.MaxHeight(V1) + 1.05f + 0.5f, $"{stage.Name}: {m.Name}");
                    Assert.GreaterOrEqual(bodyAboveLedge, 1.05f, $"{stage.Name}: {m.Name}");
                }
        }

        [Test]
        public void StagesHaveOneSpawnOneGoalAndEnemies()
        {
            foreach (var stage in StageLayout.All)
            {
                int spawns = 0, goals = 0;
                foreach (var m in stage.MarkersOf(CliffsideLayout.MarkerKind.Spawn)) spawns++;
                foreach (var m in stage.MarkersOf(CliffsideLayout.MarkerKind.Goal)) goals++;
                Assert.AreEqual(1, spawns, stage.Name);
                Assert.AreEqual(1, goals, stage.Name);
                Assert.Greater(stage.EnemyCount, 0, stage.Name);
                Assert.Greater(stage.ParSeconds, 0f, stage.Name);
                foreach (var m in stage.MarkersOf(CliffsideLayout.MarkerKind.Pickup))
                    Assert.IsTrue(m.Name.StartsWith("Notes ") || m.Name.StartsWith("Scroll ") || m.Name.StartsWith("Lore ") || m.Name.StartsWith("Item "), m.Name);
            }
        }

        [Test]
        public void BelfryIsAWaltzWithDancers()
        {
            var b = StageLayout.Belfry;
            Assert.AreEqual(3, b.Meter);
            int dancers = 0;
            foreach (var m in b.MarkersOf(CliffsideLayout.MarkerKind.Dancer)) dancers++;
            Assert.GreaterOrEqual(dancers, 2);
            Assert.AreEqual(4, b.EnemyCount);
            Assert.AreEqual(4, StageLayout.Cliffside.Meter);
            Assert.AreEqual(2, StageLayout.Cliffside.EnemyCount);
        }
    }
}
