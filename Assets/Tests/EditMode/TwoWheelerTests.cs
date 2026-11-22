using System;
using CoupledSim.TwoWheelers;
using NUnit.Framework;
using UnityEngine;

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

        // ---------------------------------------------------------------- race bike

        [Test]
        public void RaceBikeCruisesAt30To35AndTopsOutNear45()
        {
            float half = Kmh(Ride(TwoWheelerType.RaceBike, 120, new TwoWheelerInput { Drive = 0.45f }).Speed);
            float full = Kmh(Ride(TwoWheelerType.RaceBike, 120, new TwoWheelerInput { Drive = 1f }).Speed);
            Assert.That(half, Is.InRange(30f, 36f), $"cruising {half:F1} km/h");
            Assert.That(full, Is.InRange(40f, 47f), $"flat out {full:F1} km/h");
        }

        [Test]
        public void RaceBikeAcceleratesMuchQuickerThanACityBike()
        {
            float race = Ride(TwoWheelerType.RaceBike, 6, new TwoWheelerInput { Drive = 1 }).Speed;
            float city = Ride(TwoWheelerType.Bicycle, 6, new TwoWheelerInput { Drive = 1 }).Speed;
            Assert.Greater(race, city * 1.4f, $"after 6 s: race {Kmh(race):F1} km/h, city {Kmh(city):F1} km/h");
            // 0 to 30 km/h in about 6-10 s at full effort
            var s = new TwoWheelerState(); var p = TwoWheelerParameters.Preset(TwoWheelerType.RaceBike); float t = 0f;
            while (s.Speed < 30f / 3.6f && t < 30f) { s = TwoWheelerDynamics.Step(s, p, new TwoWheelerInput { Drive = 1 }, 0.02f); t += 0.02f; }
            Assert.That(t, Is.InRange(5f, 11f));
        }

        [Test]
        public void RaceBikeIsPedalledWithoutAMotorCutOff()
        {
            var p = TwoWheelerParameters.Preset(TwoWheelerType.RaceBike);
            Assert.IsTrue(p.PedalOnly);
            // no cut-off: the rider still pushes at 40 km/h
            Assert.Greater(TwoWheelerDynamics.DriveForce(p, 40f / 3.6f, 1f), 20f);
        }

        // ---------------------------------------------------------------- pedalling

        static PedallingState Pedal(TwoWheelerType type, PedallingState s, float speed, float effort, float seconds, float brake = 0f, float lean = 0f)
        {
            var p = PedallingParameters.Preset(type);
            for (float t = 0; t < seconds; t += 0.02f) s = Pedalling.Step(s, p, speed, effort, brake, lean, 0.02f);
            return s;
        }

        [TestCase(10f)]
        [TestCase(14f)]
        [TestCase(18f)]
        [TestCase(22f)]
        [TestCase(25f)]
        public void CityCadenceStaysBetween60And90(float kmh)
        {
            // from a standstill up to the speed: the gears follow
            var s = new PedallingState();
            for (float v = 0.5f; v < kmh; v += 1f) s = Pedal(TwoWheelerType.Bicycle, s, v / 3.6f, 0.6f, 0.5f);
            s = Pedal(TwoWheelerType.Bicycle, s, kmh / 3.6f, 0.5f, 5f);
            Assert.IsTrue(s.Pedalling);
            Assert.That(s.Cadence, Is.InRange(58f, 90f), $"{kmh} km/h: {s.Cadence:F0} rpm in gear {s.Gear + 1}");
        }

        [TestCase(25f)]
        [TestCase(30f)]
        [TestCase(35f)]
        [TestCase(40f)]
        [TestCase(45f)]
        public void RaceCadenceStaysBetween78And100(float kmh)
        {
            var s = new PedallingState();
            for (float v = 0.5f; v < kmh; v += 1f) s = Pedal(TwoWheelerType.RaceBike, s, v / 3.6f, 0.6f, 0.4f);
            s = Pedal(TwoWheelerType.RaceBike, s, kmh / 3.6f, 0.5f, 5f);
            Assert.That(s.Cadence, Is.InRange(78f, 101f), $"{kmh} km/h: {s.Cadence:F0} rpm in gear {s.Gear + 1}");
        }

        [Test]
        public void CranksTurnAtTheCadence()
        {
            var p = PedallingParameters.Preset(TwoWheelerType.Bicycle);
            var s = new PedallingState { Gear = Pedalling.GearFor(p, 5f) };
            s = Pedal(TwoWheelerType.Bicycle, s, 5f, 0.5f, 1f);
            float a0 = s.CrankAngle, turned = 0f;
            for (int i = 0; i < 50; i++) { s = Pedalling.Step(s, p, 5f, 0.5f, 0f, 0f, 0.02f); turned += Mathf.Repeat(s.CrankAngle - a0 + 180f, 360f) - 180f; a0 = s.CrankAngle; }
            // one second: cadence/60 revolutions
            Assert.AreEqual(s.Cadence / 60f * 360f, turned, 2f);
        }

        [Test]
        public void FreewheelingStopsTheCranksLevel()
        {
            var s = Pedal(TwoWheelerType.Bicycle, new PedallingState(), 5f, 0.5f, 3.3f);
            s = Pedal(TwoWheelerType.Bicycle, s, 5f, 0f, 2f);   // coasting
            Assert.IsFalse(s.Pedalling);
            Assert.AreEqual(0f, s.Cadence);
            float a = s.CrankAngle;
            Assert.IsTrue(Mathf.Abs(Mathf.DeltaAngle(a, 90f)) < 3f || Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 3f, $"cranks at {a:F0}°");
            var b = Pedal(TwoWheelerType.Bicycle, s, 4.5f, 0f, 1f, brake: 0.6f);   // braking: still level, not turning
            Assert.AreEqual(a, b.CrankAngle, 1f);
        }

        [Test]
        public void LeaningFarTheInsidePedalComesUp()
        {
            var s = Pedal(TwoWheelerType.Bicycle, new PedallingState(), 6f, 0.5f, 2f);
            var right = Pedal(TwoWheelerType.Bicycle, s, 6f, 0.5f, 2f, lean: 0.5f);
            Assert.IsFalse(right.Pedalling);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, right.CrankAngle), 3f, "right pedal up leaning right");
            var left = Pedal(TwoWheelerType.Bicycle, s, 6f, 0.5f, 2f, lean: -0.5f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(180f, left.CrankAngle), 3f, "left pedal up leaning left");
        }

        [Test]
        public void StoppedTheRiderPutsAFootDownAndLiftsItToGo()
        {
            var p = PedallingParameters.Preset(TwoWheelerType.Bicycle);
            var s = Pedal(TwoWheelerType.Bicycle, new PedallingState(), 4f, 0.5f, 2f);
            Assert.AreEqual(0f, s.FootDown);
            s = Pedal(TwoWheelerType.Bicycle, s, 0f, 0f, 1.5f, brake: 0.5f);
            Assert.AreEqual(1f, s.FootDown, 1e-3f, "foot on the ground after stopping");
            Assert.AreEqual(0f, Mathf.DeltaAngle(p.ReadyAngle, s.CrankAngle), 3f, "the right pedal forward and up, to push off from");
            Assert.AreEqual(0, s.Gear, "in the lowest gear to set off");
            s = Pedal(TwoWheelerType.Bicycle, s, 0f, 1f, 0.5f);
            Assert.AreEqual(0f, s.FootDown, 1e-3f, "foot back on the pedal");
        }

        [Test]
        public void SettingOffHardTheRiderStandsThenSits()
        {
            var s = Pedal(TwoWheelerType.Bicycle, new PedallingState { FootDown = 1f, StoppedFor = 2f }, 0f, 1f, 0.5f);
            s = Pedal(TwoWheelerType.Bicycle, s, 1.5f, 1f, 1f);
            Assert.Greater(s.Standing, 0.8f, "standing on the pedals at a hard effort, slowly");
            s = Pedal(TwoWheelerType.Bicycle, s, 5f, 1f, 1.5f);
            Assert.AreEqual(0f, s.Standing, 1e-3f, "seated at speed");
            var easy = Pedal(TwoWheelerType.Bicycle, new PedallingState(), 1.5f, 0.35f, 2f);
            Assert.AreEqual(0f, easy.Standing, 1e-3f, "seated at an easy effort");
        }

        [Test]
        public void ZeroTimeStepProducesFiniteAcceleration()
        {
            var p = TwoWheelerParameters.Preset(TwoWheelerType.Bicycle);
            var s = TwoWheelerDynamics.Step(new TwoWheelerState { Speed = 4f }, p, new TwoWheelerInput { Drive = 1f }, 0f);
            Assert.IsFalse(float.IsNaN(s.Acceleration) || float.IsInfinity(s.Acceleration));
            Assert.AreEqual(4f, s.Speed, 1e-6f);
        }
    }
}
