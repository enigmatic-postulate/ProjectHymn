using System.Linq;
using NUnit.Framework;
using ProjectHymn.Movement;

namespace ProjectHymn.Tests
{
    /// <summary>Starlit Spring, the brawler arena (Slice 11): its platforms, waves and how enemies hop between floors.</summary>
    public class SpringTests
    {
        static readonly JumpArc Arc = SliceTuning.Arc;
        static float Reach => Arc.MaxHeight(SliceTuning.JumpSpeed, SliceTuning.AirJumpSpeed);
        static StageLayout Stage => StageLayout.Spring;

        [Test]
        public void EveryPlatformCanBeReachedByThePlayer()
        {
            var floors = Stage.Route.Concat(Stage.Platforms).ToList();
            foreach (var p in Stage.Platforms)
            {
                bool reachable = floors.Any(f => f.Name != p.Name && f.Top < p.Top &&
                                                 p.Top - f.Top + 0.3f <= Reach &&
                                                 System.Math.Max(0f, System.Math.Max(p.XMin - f.XMax, f.XMin - p.XMax)) <= 3.5f);
                Assert.IsTrue(reachable, $"{p.Name} is out of jumping reach");
            }
            Assert.Greater(Stage.Platforms.Max(p => p.Top), Reach, "the crown needs a dais: it can't be jumped to from the ground");
        }

        [Test]
        public void ThreeWavesOnTheGroundAndOnPlatforms()
        {
            int waves = Stage.Markers.Max(m => ArenaLayout.WaveOf(m.Name));
            Assert.AreEqual(3, waves);
            var platformTops = Stage.Platforms.Select(p => p.Top).ToList();
            for (int w = 1; w <= waves; w++)
            {
                var enemies = Stage.Markers.Where(m => ArenaLayout.WaveOf(m.Name) == w).ToList();
                Assert.IsTrue(enemies.Any(m => m.Y == 0f), $"wave {w} has someone on the ground");
                Assert.IsTrue(enemies.Any(m => platformTops.Contains(m.Y)), $"wave {w} has someone on a platform");
            }
            Assert.IsTrue(Stage.OpenEdges);
            Assert.AreEqual(8, Stage.EnemyCount);
        }

        [Test]
        public void FloorsUnderPoints()
        {
            var map = Stage.Floors();
            Assert.AreEqual("Island", map.FloorAt(0f, 0f).Name);
            Assert.AreEqual("Crown", map.FloorAt(0f, SpringLayout.CrownTop + 0.05f).Name);
            Assert.AreEqual("Island", map.FloorAt(0f, 3f).Name, "between the floors: the ground under you");
            Assert.AreEqual("West Dais", map.FloorAt(-8f, SpringLayout.DaisTop).Name);
            Assert.IsFalse(map.FloorAt(20f, 0f).IsValid, "off the edge");
        }

        [Test]
        public void HopsGoUpOneStepAtATime()
        {
            var map = Stage.Floors();
            var ground = map.Floors.First(f => f.Name == "Island");
            var west = map.Floors.First(f => f.Name == "West Dais");
            var crown = map.Floors.First(f => f.Name == "Crown");
            Assert.IsTrue(map.CanHop(ground, west));
            Assert.IsFalse(map.CanHop(ground, crown), "5 m is too high for one hop");
            Assert.IsTrue(map.CanHop(crown, ground), "dropping down is always fine");
            StringAssert.Contains("Dais", map.NextStep(ground, crown).Name, "the way up to the crown is a dais");
            Assert.AreEqual("Crown", map.NextStep(west, crown).Name);
        }

        [Test]
        public void LandingsStayOnTheFloorAndOffThePlayer()
        {
            var map = Stage.Floors();
            var west = map.Floors.First(f => f.Name == "West Dais");
            float x = map.LandingX(west, -2f, -8f, playerOnIt: true);
            Assert.AreEqual(-8f + 1.4f, x, 1e-4, "short of the player, on our side");
            x = map.LandingX(west, -12f, -9.3f, playerOnIt: true);
            Assert.IsTrue(x >= west.XMin + map.EdgeMargin && x <= west.XMax - map.EdgeMargin);
            Assert.AreEqual(-10f + 1.4f, map.LandingX(west, -12f, -10f, playerOnIt: true), 1e-4, "no room on our side: the far side");
            Assert.AreEqual(west.XMin + map.EdgeMargin, map.LandingX(west, -12.5f, 0f, playerOnIt: false), 1e-4);
            var (lo, hi) = map.Bounds(west);
            Assert.AreEqual(west.XMin + 0.8f, lo, 1e-4);
            Assert.AreEqual(west.XMax - 0.8f, hi, 1e-4);
        }

        [Test]
        public void TheHopArcMeetsBothFloors()
        {
            Assert.AreEqual(0f, FloorMap.HopHeight(0f, 2.6f, 0f), 1e-4);
            Assert.AreEqual(2.6f, FloorMap.HopHeight(0f, 2.6f, 1f), 1e-4);
            float peak = 0f;
            for (int i = 0; i <= 100; i++) peak = System.Math.Max(peak, FloorMap.HopHeight(0f, 2.6f, i / 100f));
            Assert.AreEqual(2.6f + 1.1f, peak, 0.02f);
            Assert.AreEqual(0f, FloorMap.HopHeight(5f, 0f, 1f), 1e-4, "a drop lands on the ground");
        }
    }
}
