using System.Collections.Generic;
using CoupledSim.Sound;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    /// <summary>The road traffic noise model (CNOSSOS-EU), the engine's gears and air absorption.</summary>
    public class SoundModelTests
    {
        static float PassBy(VehicleCategory c, float kmh, bool electric = false)
        {
            float lwa = Cnossos.Total(i => Cnossos.Sum(Cnossos.Rolling(c, kmh, i), Cnossos.Propulsion(c, kmh, i, electric)), true);
            return Cnossos.PressureAt(lwa, 7.5f);
        }

        [Test]
        public void CarPassByLevelsAreRealistic()
        {
            // a car at 50 km/h is about 70 dB(A) at 7.5 m, about 80 dB(A) at 100 km/h
            Assert.AreEqual(70.5f, PassBy(VehicleCategory.Light, 50f), 1.0f);
            Assert.AreEqual(80.0f, PassBy(VehicleCategory.Light, 100f), 1.0f);
            Assert.Greater(PassBy(VehicleCategory.Heavy, 50f), PassBy(VehicleCategory.MediumHeavy, 50f));
            Assert.Greater(PassBy(VehicleCategory.MediumHeavy, 50f), PassBy(VehicleCategory.Light, 50f) + 4f);
        }

        [Test]
        public void ElectricCarsAreQuieterOnlyAtLowSpeed()
        {
            float low = PassBy(VehicleCategory.Light, 20f) - PassBy(VehicleCategory.Light, 20f, true);
            float high = PassBy(VehicleCategory.Light, 100f) - PassBy(VehicleCategory.Light, 100f, true);
            Assert.Greater(low, 2f, "below 30 km/h propulsion matters");
            Assert.Less(high, 1f, "at 100 km/h tyre noise dominates");
        }

        [Test]
        public void TyreNoiseDominatesAboveAbout40KmhForCars()
        {
            float r = Cnossos.Total(i => Cnossos.Rolling(VehicleCategory.Light, 60f, i), true);
            float p = Cnossos.Total(i => Cnossos.Propulsion(VehicleCategory.Light, 60f, i), true);
            Assert.Greater(r, p);
        }

        [Test]
        public void EngineShiftsUpAndDown()
        {
            var e = EngineModel.For(VehicleCategory.Light);
            e.Update(0f, 0f);
            Assert.AreEqual(800f, e.Rpm, 1f, "idles");
            var gears = new List<int>();
            for (float v = 0; v <= 100f; v += 2f) { e.Update(v, 0.4f); gears.Add(e.Gear); Assert.LessOrEqual(e.Rpm, 6000f); }
            Assert.AreEqual(1, gears[2]);
            Assert.AreEqual(5, gears[gears.Count - 1], "top gear at 100 km/h");
            for (int i = 1; i < gears.Count; i++) Assert.GreaterOrEqual(gears[i], gears[i - 1], "only upshifts while accelerating");
            for (float v = 100; v >= 10f; v -= 2f) e.Update(v, 0f);
            Assert.LessOrEqual(e.Gear, 2, "down again when slowing");
            Assert.AreEqual(e.Rpm / 30f, e.FiringHz, 1e-3, "a four-stroke four-cylinder fires twice per revolution");
        }

        [Test]
        public void AirAbsorptionCutsTheHighsWithDistance()
        {
            float near = AirAbsorption.Cutoff(10f), mid = AirAbsorption.Cutoff(100f), far = AirAbsorption.Cutoff(500f);
            Assert.Greater(near, 8000f);
            Assert.That(mid, Is.InRange(3000f, 5000f));
            Assert.That(far, Is.InRange(1200f, 2500f));
        }

        [Test]
        public void NonAllocatingTotalsMatchBandTotalsAndZeroDistanceStaysFinite()
        {
            foreach (var cat in new[] { VehicleCategory.Light, VehicleCategory.MediumHeavy, VehicleCategory.Heavy, VehicleCategory.Moped })
            {
                foreach (bool aWeighted in new[] { false, true })
                {
                    Assert.AreEqual(
                        Cnossos.Total(i => Cnossos.Rolling(cat, 55f, i, 2.5f), aWeighted),
                        Cnossos.TotalRolling(cat, 55f, 2.5f, aWeighted),
                        1e-4f);
                    Assert.AreEqual(
                        Cnossos.Total(i => Cnossos.Propulsion(cat, 55f, i, true), aWeighted),
                        Cnossos.TotalPropulsion(cat, 55f, true, aWeighted),
                        1e-4f);
                }
            }
            float p0 = Cnossos.PressureAt(90f, 0f);
            Assert.IsFalse(float.IsInfinity(p0) || float.IsNaN(p0));
        }
    }
}
