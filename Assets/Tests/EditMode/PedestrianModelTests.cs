using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CoupledSim.Pedestrians;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    public class PedestrianProfileTests
    {
        [Test]
        public void SameSeedGivesSamePedestrian()
        {
            var a = PedestrianProfile.Sample(new System.Random(42));
            var b = PedestrianProfile.Sample(new System.Random(42));
            Assert.AreEqual(a.WalkingSpeed, b.WalkingSpeed);
            Assert.AreEqual(a.CriticalGap, b.CriticalGap);
        }

        [Test]
        public void SampledWalkingSpeedsMatchLiterature()
        {
            var rng = new System.Random(1);
            var speeds = Enumerable.Range(0, 5000).Select(_ => PedestrianProfile.Sample(rng).WalkingSpeed).ToList();
            Assert.AreEqual(1.34, speeds.Average(), 0.03);
            Assert.That(speeds.Min(), Is.GreaterThanOrEqualTo(0.6f));
            Assert.That(speeds.Max(), Is.LessThanOrEqualTo(2.0f));
        }

        [Test]
        public void OlderPedestriansAreSlowerAndMoreCautious()
        {
            var rng = new System.Random(2);
            var adults = Enumerable.Range(0, 2000).Select(_ => PedestrianProfile.Sample(rng, AgeGroup.Adult)).ToList();
            var older = Enumerable.Range(0, 2000).Select(_ => PedestrianProfile.Sample(rng, AgeGroup.Older)).ToList();
            Assert.Less(older.Average(p => p.WalkingSpeed), adults.Average(p => p.WalkingSpeed));
            Assert.Greater(older.Average(p => p.CriticalGap), adults.Average(p => p.CriticalGap));
        }
    }

    public class GapAcceptanceTests
    {
        static GapAcceptanceModel Model(float trust = 0.7f)
        {
            var p = PedestrianProfile.Default;
            p.EhmiTrust = trust;
            var m = new GapAcceptanceModel(p, new System.Random(0));
            m.ArriveAtKerb();
            return m;
        }

        static bool RunFor(GapAcceptanceModel m, float seconds, Func<float, List<ApproachingVehicle>> traffic)
        {
            const float dt = 0.02f;
            for (float t = 0; t < seconds; t += dt)
            {
                if (m.Update(dt, traffic(t))) return true;
            }
            return false;
        }

        static ApproachingVehicle Car(float distance, float speed, float accel = 0, EhmiSignal ehmi = EhmiSignal.None) =>
            new ApproachingVehicle { Id = 1, DistanceToConflict = distance, Speed = speed, Acceleration = accel, ClearDistance = 3.5f, Ehmi = ehmi };

        [Test]
        public void TimeToArrivalConstantSpeed()
        {
            Assert.AreEqual(5f, GapAcceptanceModel.TimeToArrival(50, 10, 0), 1e-4);
        }

        [Test]
        public void TimeToArrivalIsInfiniteWhenCarStopsBefore()
        {
            // 10 m/s braking at 4 m/s² stops in 12.5 m
            Assert.IsTrue(float.IsPositiveInfinity(GapAcceptanceModel.TimeToArrival(20, 10, -4)));
        }

        [Test]
        public void CrossesEmptyRoadAfterLookingAndReacting()
        {
            var m = Model();
            float crossedAt = -1;
            const float dt = 0.02f;
            for (float t = 0; t < 5; t += dt)
            {
                if (m.Update(dt, new List<ApproachingVehicle>())) { crossedAt = t; break; }
            }
            Assert.That(crossedAt, Is.GreaterThan(m.Profile.ReactionTime).And.LessThan(m.MinimumLookTime + m.Profile.ReactionTime + 0.1f));
            Assert.AreEqual(CrossingPhase.Crossing, m.Phase);
        }

        [Test]
        public void WaitsForCloseFastCar()
        {
            var m = Model();
            // car 30 m away at 50 km/h: 2.2 s gap
            Assert.IsFalse(RunFor(m, 2.0f, t => new List<ApproachingVehicle> { Car(30 - 13.9f * t, 13.9f) }));
            StringAssert.Contains("TTA", m.Reason);
        }

        [Test]
        public void AcceptsLargeGap()
        {
            var m = Model();
            // car 150 m away at 50 km/h: 10.8 s gap
            Assert.IsTrue(RunFor(m, 3f, t => new List<ApproachingVehicle> { Car(150 - 13.9f * t, 13.9f) }));
        }

        [Test]
        public void CrossesInFrontOfYieldingCar()
        {
            var m = Model(trust: 0);
            // 40 m away at 13.9 m/s braking at 3 m/s²: stops after 32 m
            Assert.IsTrue(RunFor(m, 3f, t =>
            {
                float v = Math.Max(0, 13.9f - 3f * t);
                float d = 40 - (13.9f * t - 1.5f * t * t);
                return new List<ApproachingVehicle> { Car(d, v, v > 0 ? -3f : 0) };
            }));
        }

        [Test]
        public void TrustingPedestrianCrossesOnEhmiBeforeCarSlows()
        {
            var trusting = Model(trust: 1);
            var sceptic = Model(trust: 0);
            // 70 m away at 13.9 m/s (5 s gap, just below most required gaps) with eHMI "walk", no braking yet
            Func<float, List<ApproachingVehicle>> traffic = t => new List<ApproachingVehicle> { Car(60 - 13.9f * t, 13.9f, 0, EhmiSignal.Walk) };
            Assert.IsTrue(RunFor(trusting, 2.5f, traffic));
            Assert.IsFalse(RunFor(sceptic, 2.5f, traffic));
        }

        [Test]
        public void ImpatienceShrinksAcceptedGap()
        {
            var m = Model();
            Assert.Less(m.AcceptedGap(120), m.AcceptedGap(0));
            Assert.GreaterOrEqual(m.AcceptedGap(1000), m.Profile.CriticalGap * m.Profile.MinGapFraction - 1e-4);
        }

        [Test]
        public void ChangingSituationResetsStart()
        {
            var m = Model(trust: 0);
            // road clear for 1.2 s (look time passes, start timer runs), then a fast car appears close by
            bool crossed = RunFor(m, 1.2f, t => new List<ApproachingVehicle>());
            Assert.IsFalse(crossed);
            Assert.AreEqual(CrossingPhase.Starting, m.Phase);
            m.Update(0.02f, new List<ApproachingVehicle> { Car(20, 13.9f) });
            Assert.AreEqual(CrossingPhase.Waiting, m.Phase);
        }

        [Test]
        public void HurriesWhenCarApproachesDuringCrossing()
        {
            var m = Model();
            Assert.AreEqual(1f, m.CrossingSpeedFactor(new List<ApproachingVehicle> { Car(200, 13.9f) }));
            Assert.Greater(m.CrossingSpeedFactor(new List<ApproachingVehicle> { Car(20, 13.9f) }), 1f);
        }
    }

    public class SocialForceTests
    {
        [Test]
        public void AcceleratesTowardsDesiredVelocity()
        {
            var a = new SocialAgent { Position = Vector2.Zero, Velocity = Vector2.Zero, Radius = 0.25f };
            var acc = SocialForceModel.Acceleration(a, -1, Vector2.UnitX, 1.3f, null, null, SocialForceParameters.Default);
            Assert.Greater(acc.X, 0);
            Assert.AreEqual(0, acc.Y, 1e-5);
        }

        [Test]
        public void TwoWalkersHeadOnDoNotCollide()
        {
            var p = SocialForceParameters.Default;
            var agents = new List<SocialAgent>
            {
                new SocialAgent { Position = new Vector2(-5, 0.05f), Velocity = new Vector2(1.3f, 0), Radius = 0.25f },
                new SocialAgent { Position = new Vector2(5, -0.05f), Velocity = new Vector2(-1.3f, 0), Radius = 0.25f },
            };
            var goals = new[] { new Vector2(10, 0), new Vector2(-10, 0) };
            float minDist = float.MaxValue;
            const float dt = 0.05f;
            for (int step = 0; step < 300; step++)
            {
                var next = new List<SocialAgent>(agents);
                for (int i = 0; i < agents.Count; i++)
                {
                    var a = agents[i];
                    var acc = SocialForceModel.Acceleration(a, i, goals[i] - a.Position, 1.3f, agents, null, p);
                    a.Velocity = SocialForceModel.Integrate(a.Velocity, acc, 1.3f, dt, p);
                    a.Position += a.Velocity * dt;
                    next[i] = a;
                }
                agents = next;
                minDist = Math.Min(minDist, Vector2.Distance(agents[0].Position, agents[1].Position));
            }
            Assert.Greater(minDist, 0.5f, "bodies (2 x 0.25 m) must not overlap");
            Assert.Greater(agents[0].Position.X, 5f, "both still reach their goal side");
            Assert.Less(agents[1].Position.X, -5f);
        }

        [Test]
        public void WallPushesAway()
        {
            var a = new SocialAgent { Position = new Vector2(0, 0.3f), Velocity = Vector2.Zero, Radius = 0.25f };
            var wall = new List<WallSegment> { new WallSegment(new Vector2(-5, 0), new Vector2(5, 0)) };
            var acc = SocialForceModel.Acceleration(a, -1, Vector2.Zero, 0, null, wall, SocialForceParameters.Default);
            Assert.Greater(acc.Y, 0);
        }
    }
}
