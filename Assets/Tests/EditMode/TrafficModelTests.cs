using System;
using System.Collections.Generic;
using CoupledSim.Traffic;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    public class TrafficModelTests
    {
        static IEnumerable<CarFollowingModelType> AllModels() => (CarFollowingModelType[])Enum.GetValues(typeof(CarFollowingModelType));

        [TestCaseSource(nameof(AllModels))]
        public void ReachesDesiredSpeedOnFreeRoad(CarFollowingModelType type)
        {
            var m = CarFollowingModels.Create(type, 13.9f, new Random(1));
            if (m is KraussModel k) k.Sigma = 0; // without dawdling it reaches the exact speed
            float v = 0;
            for (int i = 0; i < 60 * 20; i++) v = Math.Max(0, v + m.Acceleration(v, Leader.None, 0.05f) * 0.05f);
            Assert.AreEqual(13.9f, v, 0.5f);
        }

        [TestCaseSource(nameof(AllModels))]
        public void StopsBeforeStationaryObstacle(CarFollowingModelType type)
        {
            var m = CarFollowingModels.Create(type, 13.9f, new Random(2));
            float x = 0, v = 13.9f, obstacle = 80f;
            for (int i = 0; i < 60 * 20; i++)
            {
                float a = m.Acceleration(v, Leader.Stationary(obstacle - x), 0.05f);
                v = Math.Max(0, v + a * 0.05f);
                x += v * 0.05f;
            }
            Assert.Less(x, obstacle, $"{type} drove into the obstacle");
            Assert.Greater(x, obstacle - 8f, $"{type} stopped far too early");
            Assert.Less(v, 0.3f);
        }

        [TestCaseSource(nameof(AllModels))]
        public void PlatoonBehindBrakingLeaderHasNoCollisions(CarFollowingModelType type)
        {
            const int n = 10;
            const float dt = 0.05f, length = 4.5f;
            var rng = new Random(3);
            var models = new ICarFollowingModel[n];
            var x = new float[n];
            var v = new float[n];
            for (int i = 0; i < n; i++)
            {
                models[i] = CarFollowingModels.Create(type, 13.9f + (float)rng.NextDouble(), rng);
                x[i] = -i * 30f;
                v[i] = 12f;
            }
            float minGap = float.MaxValue;
            for (int step = 0; step < 90 / dt; step++)
            {
                float t = step * dt;
                // leader: cruise, emergency-brake to standstill at t = 20 s, drive off at t = 40 s
                float aLead = t >= 20 && t < 40 ? -6f : 1f;
                var acc = new float[n];
                acc[0] = Math.Min(aLead, 13.9f - v[0]);
                for (int i = 1; i < n; i++)
                    acc[i] = models[i].Acceleration(v[i], Leader.Vehicle(x[i - 1] - x[i] - length, v[i - 1]), dt);
                for (int i = 0; i < n; i++)
                {
                    v[i] = Math.Max(0, v[i] + acc[i] * dt);
                    x[i] += v[i] * dt;
                }
                for (int i = 1; i < n; i++) minGap = Math.Min(minGap, x[i - 1] - x[i] - length);
            }
            Assert.Greater(minGap, 0.5f, $"{type}: vehicles collided (min gap {minGap:F2} m)");
            Assert.Greater(v[n - 1], 5f, $"{type}: the platoon did not drive off again");
        }

        [Test]
        public void IdmKeepsAboutTheDesiredHeadway()
        {
            var m = new IntelligentDriverModel { DesiredSpeed = 20f };
            // follow a leader at a constant 10 m/s until equilibrium
            float gap = 50, v = 10;
            for (int i = 0; i < 4000; i++)
            {
                float a = m.Acceleration(v, Leader.Vehicle(gap, 10f), 0.05f);
                v = Math.Max(0, v + a * 0.05f);
                gap += (10f - v) * 0.05f;
            }
            float expected = m.MinimumGap + 10f * m.TimeHeadway; // s* at dv = 0, slightly more at v < v0
            Assert.AreEqual(10f, v, 0.1f);
            Assert.That(gap, Is.GreaterThan(expected).And.LessThan(expected * 1.3f));
        }

        [Test]
        public void MobilChangesToFasterLaneWhenSafe()
        {
            Assert.IsTrue(Mobil.ShouldChange(accelCurrent: -1.5f, accelTarget: 0.8f,
                newFollowerBefore: 0.2f, newFollowerAfter: -0.5f, oldFollowerBefore: -0.5f, oldFollowerAfter: 0.3f));
        }

        [Test]
        public void MobilRefusesUnsafeChange()
        {
            Assert.IsFalse(Mobil.ShouldChange(accelCurrent: -1.5f, accelTarget: 1.0f,
                newFollowerBefore: 0.5f, newFollowerAfter: -6f, oldFollowerBefore: 0f, oldFollowerAfter: 0f));
        }

        [Test]
        public void MobilIgnoresTinyAdvantage()
        {
            Assert.IsFalse(Mobil.ShouldChange(accelCurrent: 0.5f, accelTarget: 0.6f,
                newFollowerBefore: 0f, newFollowerAfter: 0f, oldFollowerBefore: 0f, oldFollowerAfter: 0f));
        }
    }
}
