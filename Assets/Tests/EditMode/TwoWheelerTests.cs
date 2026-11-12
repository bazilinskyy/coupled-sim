using System;
using CoupledSim.TwoWheelers;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    public class TwoWheelerTests
    {
        static TwoWheelerState Ride(TwoWheelerType type, float seconds, TwoWheelerInput input, float slope = 0f, float startSpeed = 0f)
        {
            var p = TwoWheelerParameters.Preset(type);
            var s = new TwoWheelerState { Speed = startSpeed };
            for (float t = 0; t < seconds; t += 0.02f) s = TwoWheelerDynamics.Step(s, p, input, 0.02f, slope);
            return s;
        }

        static float Kmh(float v) => v * 3.6f;

        [Test]
        public void CityCyclistCruisesAtTypicalSpeed()
        {
            // full effort on a city bike gives about 18-25 km/h on the flat (Dutch cyclists: ~18 km/h at moderate effort)
            float v = Kmh(Ride(TwoWheelerType.Bicycle, 120, new TwoWheelerInput { Drive = 1 }).Speed);
            Assert.That(v, Is.InRange(18f, 27f));
        }

        [Test]
        public void ModerateEffortIsSlower()
        {
            float full = Ride(TwoWheelerType.Bicycle, 120, new TwoWheelerInput { Drive = 1 }).Speed;
            float half = Ride(TwoWheelerType.Bicycle, 120, new TwoWheelerInput { Drive = 0.5f }).Speed;
            Assert.Less(half, full);
            Assert.That(Kmh(half), Is.InRange(13f, 22f));
        }

        [TestCase(TwoWheelerType.EBike, 25f)]
        [TestCase(TwoWheelerType.EScooter, 25f)]
        [TestCase(TwoWheelerType.Moped25, 25f)]
        [TestCase(TwoWheelerType.Moped45, 45f)]
        public void MotorStopsAssistingAtTheLegalLimit(TwoWheelerType type, float limitKmh)
        {
            float v = Kmh(Ride(type, 120, new TwoWheelerInput { Drive = 1 }).Speed);
            Assert.That(v, Is.InRange(limitKmh - 3f, limitKmh + 2f), $"{type} top speed {v:F1} km/h");
        }

        [Test]
        public void MopedAcceleratesFasterThanBicycle()
        {
            float bike = Ride(TwoWheelerType.Bicycle, 4, new TwoWheelerInput { Drive = 1 }).Speed;
            float moped = Ride(TwoWheelerType.Moped45, 4, new TwoWheelerInput { Drive = 1 }).Speed;
            Assert.Greater(moped, bike);
        }

        [Test]
        public void UphillIsSlower()
        {
            float flat = Ride(TwoWheelerType.Bicycle, 120, new TwoWheelerInput { Drive = 1 }).Speed;
            float hill = Ride(TwoWheelerType.Bicycle, 120, new TwoWheelerInput { Drive = 1 }, slope: 0.04f).Speed;
            Assert.Less(hill, flat * 0.75f);
        }

        [Test]
        public void BrakingStopsWithinRealisticDistance()
        {
            var p = TwoWheelerParameters.Preset(TwoWheelerType.Bicycle);
            var s = new TwoWheelerState { Speed = 20f / 3.6f };
            float start = s.Z;
            while (s.Speed > 0) s = TwoWheelerDynamics.Step(s, p, new TwoWheelerInput { Brake = 1 }, 0.02f);
            float distance = s.Z - start;
            // v²/(2a) at 5 m/s² from 20 km/h: 3.1 m
            Assert.That(distance, Is.InRange(2.5f, 3.6f));
        }

        [Test]
        public void LeansIntoTurnsAndTurnsMoreSharplyWhenSlow()
        {
            var right = Ride(TwoWheelerType.Bicycle, 3, new TwoWheelerInput { Drive = 0.3f, Steer = 1 }, startSpeed: 5f);
            Assert.Greater(right.YawRate, 0f, "turning right");
            Assert.Greater(right.Lean, 0.05f, "leaning right into the turn");
            var p = TwoWheelerParameters.Preset(TwoWheelerType.Bicycle);
            Assert.Greater(TwoWheelerDynamics.MaxSteer(p, 2f), TwoWheelerDynamics.MaxSteer(p, 8f));
        }

        [Test]
        public void LeanMatchesEquilibrium()
        {
            var s = Ride(TwoWheelerType.Bicycle, 10, new TwoWheelerInput { Drive = 0.4f, Steer = 0.5f }, startSpeed: 5f);
            float expected = (float)Math.Atan(s.Speed * s.YawRate / TwoWheelerDynamics.Gravity);
            Assert.AreEqual(expected, s.Lean, 0.03f);
        }

        [Test]
        public void NoReversing()
        {
            var s = Ride(TwoWheelerType.Bicycle, 5, new TwoWheelerInput { Brake = 1 });
            Assert.AreEqual(0f, s.Speed);
        }
    }
}
