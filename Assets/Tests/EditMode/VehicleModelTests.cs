using System;
using CoupledSim.Vehicles;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    public class VehicleModelTests
    {
        // straight-line acceleration of a point mass with the powertrain (no wheel slip)
        static (float t100, float vMax, int maxGear, float maxRpm) Accelerate(CarSpec spec, float seconds = 90f)
        {
            var p = new Powertrain(spec);
            float v = 0, t = 0, t100 = float.NaN, maxRpm = 0;
            int maxGear = 1;
            const float dt = 0.01f;
            for (; t < seconds; t += dt)
            {
                float force = p.WheelTorque(v, 1f, dt) / spec.WheelRadius - p.Resistance(v);
                // grip limit of the driven axle (about half the weight, mu 1)
                force = Math.Min(force, 0.5f * spec.Mass * 9.81f);
                v = Math.Max(0, v + force / spec.Mass * dt);
                if (float.IsNaN(t100) && v >= 100f / 3.6f) t100 = t;
                maxGear = Math.Max(maxGear, p.Gear);
                maxRpm = Math.Max(maxRpm, p.Rpm);
            }
            return (t100, v * 3.6f, maxGear, maxRpm);
        }

        [Test]
        public void SmartAcceleratesLikeTheRealCar()
        {
            var r = Accelerate(CarSpec.SmartFortwo);
            Assert.That(r.t100, Is.InRange(11f, 16f), "0-100 km/h (spec 13.3 s)");
            Assert.That(r.vMax, Is.InRange(135f, 160f), "top speed (spec 145 km/h)");
        }

        [Test]
        public void HatchbackAcceleratesLikeTheRealCar()
        {
            var r = Accelerate(CarSpec.Hatchback);
            Assert.That(r.t100, Is.InRange(9f, 13f), "0-100 km/h (spec 10.9 s)");
            Assert.That(r.vMax, Is.InRange(180f, 210f), "top speed (spec 196 km/h)");
        }

        // 0 to a speed (km/h), with the published figures
        static float TimeTo(CarSpec spec, float kmh)
        {
            var p = new Powertrain(spec);
            float v = 0, t = 0; const float dt = 0.01f;
            for (; t < 120f && v < kmh / 3.6f; t += dt)
            {
                float force = Math.Min(p.WheelTorque(v, 1f, dt) / spec.WheelRadius - p.Resistance(v), 0.5f * spec.Mass * 9.81f);
                v = Math.Max(0, v + force / spec.Mass * dt);
            }
            return t;
        }

        [Test]
        public void SuvAcceleratesLikeTheRealCar()
        {
            Assert.That(TimeTo(CarSpec.Suv, 100f), Is.InRange(6.5f, 9.5f), "0-100 km/h (spec 7.7 s)");
            Assert.That(Accelerate(CarSpec.Suv).vMax, Is.InRange(190f, 225f), "top speed (spec 208 km/h)");
        }

        [Test]
        public void EstateAcceleratesLikeTheRealCar()
        {
            Assert.That(TimeTo(CarSpec.Estate, 100f), Is.InRange(7.5f, 10.5f), "0-100 km/h (spec 8.9 s)");
            Assert.That(Accelerate(CarSpec.Estate).vMax, Is.InRange(200f, 230f), "top speed (spec 216 km/h)");
        }

        [Test]
        public void VanAcceleratesLikeTheRealVan()
        {
            Assert.That(TimeTo(CarSpec.Van, 100f), Is.InRange(12f, 18f), "0-100 km/h (about 15 s)");
            Assert.That(Accelerate(CarSpec.Van).vMax, Is.InRange(140f, 165f), "top speed (about 150 km/h)");
        }

        [Test]
        public void BusIsSlowAndLimitedTo80()
        {
            Assert.That(TimeTo(CarSpec.CityBus, 50f), Is.InRange(10f, 22f), "0-50 km/h (city buses about 12-18 s)");
            Assert.That(Accelerate(CarSpec.CityBus).vMax, Is.InRange(76f, 81f), "speed limiter 80 km/h");
        }

        [Test]
        public void TruckIsLimitedTo90()
        {
            Assert.That(TimeTo(CarSpec.Truck, 80f), Is.InRange(25f, 50f), "0-80 km/h (half-loaded 18 t truck)");
            Assert.That(Accelerate(CarSpec.Truck, 150f).vMax, Is.InRange(86f, 91f), "speed limiter 90 km/h");
        }

        [Test]
        public void HeavierVehiclesBrakeTheirWheelsHarder()
        {
            Assert.Greater(CarSpec.CityBus.MaxBrakeTorque, CarSpec.Van.MaxBrakeTorque);
            Assert.Greater(CarSpec.Van.MaxBrakeTorque, CarSpec.Hatchback.MaxBrakeTorque);
            foreach (var n in new[] { "smart", "hatchback", "suv", "estate", "van", "bus", "truck" }) Assert.IsNotNull(CarSpec.ByName(n), n);
        }

        [Test]
        public void ShiftsThroughGearsWithinTheRevRange()
        {
            var spec = CarSpec.Hatchback;
            var r = Accelerate(spec);
            Assert.GreaterOrEqual(r.maxGear, 5);
            Assert.LessOrEqual(r.maxRpm, spec.RedlineRpm + 1f);
        }

        [Test]
        public void TorqueCurvePeaksThenFallsWithPowerLimit()
        {
            var p = new Powertrain(CarSpec.Hatchback);
            float peak = p.EngineTorque(4000);
            Assert.AreEqual(159f, peak, 1f);
            Assert.Less(p.EngineTorque(1000), peak);
            // at max power rpm, torque = power / omega
            float expected = 92000f / (6300f * 2f * (float)Math.PI / 60f);
            Assert.AreEqual(expected, p.EngineTorque(6300), 1f);
            Assert.AreEqual(0f, p.EngineTorque(7000));
        }

        [Test]
        public void EngineBrakesOffThrottle()
        {
            var p = new Powertrain(CarSpec.Hatchback);
            for (int i = 0; i < 20; i++) p.WheelTorque(20f, 0f, 0.5f); // settle the gear (shifts interrupt torque)
            float t = p.WheelTorque(20f, 0f, 0.5f);
            Assert.Less(t, 0f);
        }

        [Test]
        public void GentleDrivingShiftsUpEarly()
        {
            var gentle = new Powertrain(CarSpec.Hatchback);
            var hard = new Powertrain(CarSpec.Hatchback);
            for (int i = 0; i < 50; i++)
            {
                gentle.WheelTorque(12f, 0.25f, 0.05f);
                hard.WheelTorque(12f, 1f, 0.05f);
            }
            Assert.Greater(gentle.Gear, hard.Gear);
        }

        [Test]
        public void ReverseDrivesBackwards()
        {
            var p = new Powertrain(CarSpec.SmartFortwo) { Reverse = true };
            Assert.Less(p.WheelTorque(0f, 0.5f, 0.02f), 0f);
        }

        [Test]
        public void ReverseSpeedIsLimited()
        {
            var spec = CarSpec.SmartFortwo;
            var p = new Powertrain(spec) { Reverse = true };
            float v = 0f, dt = 0.02f;
            for (int i = 0; i < 1500; i++)
            {
                float force = p.WheelTorque(v, 1f, dt) / spec.WheelRadius;
                v += (force - 400f * Math.Sign(v)) / spec.Mass * dt;
            }
            Assert.Less(v, -5f, "drives backwards");
            Assert.Greater(v, -p.MaxReverseSpeed - 0.5f, "no faster than the reverse limit");
        }

        [Test]
        public void AbsReleasesALockingWheelAndReappliesWhenRolling()
        {
            var abs = new AntiLockBrakes();
            float locked = abs.Modulate(1000f, -0.6f, 0.05f);
            Assert.Less(locked, 1000f);
            float after = locked;
            for (int i = 0; i < 20; i++) after = abs.Modulate(1000f, -0.02f, 0.05f);
            Assert.AreEqual(1000f, after, 1e-3);
        }
    }
}
