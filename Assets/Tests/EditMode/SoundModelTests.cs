using System.Collections.Generic;
using CoupledSim.Sound;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    /// <summary>The road traffic noise model (CNOSSOS-EU), the engine's gears, air absorption, the ambience's levels
    /// (wind at the ears, distant traffic) and the drones' levels.</summary>
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

        [Test]
        public void WindAtTheEarsIsSoftForWalkersAndARumbleForCyclists()
        {
            Assert.AreEqual(0.54f, Ambience.StreetWindFactor, 0.01f, "EN 1991-1-4, terrain category IV below z_min");
            // a pedestrian walking (1.4 m/s) in a light breeze (2 m/s at 10 m) at the top of a gust (+35 %)
            float walker = Ambience.WindNoiseDb(Ambience.AirSpeedAtEars(2f, 1.35f, 1.4f));
            Assert.Less(walker, 50f, "the wind's rumble (about 13 dB less A-weighted) stays under the distant traffic");
            Assert.Less(walker - 13f, Ambience.DistantTrafficDbA(7.75f) - 8f);
            // a cyclist at 20 km/h in still air
            Assert.AreEqual(68.5f, Ambience.WindNoiseDb(Ambience.AirSpeedAtEars(0f, 1f, 20f / 3.6f)), 1f);
            // 45 dB per tenfold speed (Seidman et al. 2017), and no step: it fades out smoothly
            Assert.AreEqual(45f, Ambience.WindNoiseDb(20f) - Ambience.WindNoiseDb(2f), 1e-3f);
            for (float v = 1f; v < 3f; v += 0.05f) Assert.Less(Ambience.WindNoiseDb(v + 0.05f) - Ambience.WindNoiseDb(v), 2.2f);
            // gusts change the wind, not the rider's own speed
            Assert.AreEqual(Ambience.AirSpeedAtEars(0f, 1.35f, 5f), 5f, 1e-4f);
        }

        [Test]
        public void DistantTrafficFollowsTheHourAtUrbanBackgroundLevels()
        {
            // L_A90 of a residential area (King et al. 2012): 45.4 dB(A) in the morning, 43.9 in the afternoon, 41.7 at night
            Assert.That(Ambience.DistantTrafficDbA(9f), Is.InRange(44f, 48f));
            Assert.That(Ambience.DistantTrafficDbA(14f), Is.InRange(43f, 47f));
            Assert.That(Ambience.DistantTrafficDbA(3f), Is.InRange(35f, 42f));
            Assert.Less(Ambience.DistantTrafficDbA(3f), Ambience.DistantTrafficDbA(8f) - 6f, "quieter at night");
            Assert.AreEqual(Ambience.DistantTrafficDbA(1f), Ambience.DistantTrafficDbA(25f), 1e-4f);
            Assert.AreEqual(Ambience.DistantTrafficDbA(0f), Ambience.DistantTrafficDbA(24f), 1e-4f);
        }

        // DroneSound is in the game's own assembly (Assembly-CSharp), which test assemblies cannot reference: called by reflection
        static float Drone(string method, params object[] args) =>
            (float)System.Type.GetType("DroneSound, Assembly-CSharp", true).GetMethod(method).Invoke(null, args);

        [Test]
        public void DronesHaveTheSoundPowerOfTheirClass()
        {
            // Regulation (EU) 2019/945: 81 dB(A) below 900 g, 18.5 dB more per tenfold mass
            Assert.AreEqual(81f, Drone("SoundPowerDbA", 0.9f), 0.01f);
            Assert.AreEqual(81f, Drone("SoundPowerDbA", 0.25f), 0.01f);
            Assert.AreEqual(81f + 18.5f * 0.6478f, Drone("SoundPowerDbA", 4f), 0.05f);
            // a consumer drone hovering 40 m away is faint (under a city's daytime background), 1 m away loud
            float far = Drone("LevelDbA", 0.9f, 1f, 40f), near = Drone("LevelDbA", 0.9f, 1f, 1f);
            Assert.That(far, Is.InRange(35f, 40f));
            Assert.Less(far, Ambience.DistantTrafficDbA(10.5f));
            Assert.That(near, Is.InRange(68f, 72f));
            Assert.AreEqual(6f, Drone("LevelDbA", 0.9f, 1f, 1f) - Drone("LevelDbA", 0.9f, 1f, 2f), 0.05f, "-6 dB per doubling of distance");
            Assert.Greater(Drone("LevelDbA", 0.9f, 1.5f, 10f), Drone("LevelDbA", 0.9f, 1f, 10f), "louder climbing");
        }
    }
}
