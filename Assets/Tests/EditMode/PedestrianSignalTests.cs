using System;
using System.Linq;
using CoupledSim.Pedestrians;
using NUnit.Framework;

namespace CoupledSim.Tests
{
    /// <summary>Pedestrians at pedestrian lights: compliant ones wait for green, opportunists cross on red after their tolerance.</summary>
    public class PedestrianSignalTests
    {
        [Test]
        public void CompliantPedestriansWaitForGreen()
        {
            var p = PedestrianProfile.Default; p.RedLightOpportunist = false; p.RedLightTolerance = 10f;
            Assert.IsTrue(p.MayStartAtSignal(true, false, 0f));
            Assert.IsFalse(p.MayStartAtSignal(false, false, 600f), "never on red");
            Assert.IsFalse(p.MayStartAtSignal(false, true, 0f), "not when green blinks");
        }

        [Test]
        public void OpportunistsCrossOnRedAfterTheirTolerance()
        {
            var p = PedestrianProfile.Default; p.RedLightOpportunist = true; p.RedLightTolerance = 30f;
            Assert.IsFalse(p.MayStartAtSignal(false, false, 10f));
            Assert.IsTrue(p.MayStartAtSignal(false, false, 31f));
            Assert.IsTrue(p.MayStartAtSignal(false, true, 0f), "a blinking green: still go");
        }

        [Test]
        public void ComplianceIsDrawnFromThePopulation()
        {
            var rng = new Random(3);
            int n = 4000, opp = 0; double tol = 0;
            for (int i = 0; i < n; i++)
            {
                var p = PedestrianProfile.Default; p.SampleSignalCompliance(rng);
                if (p.RedLightOpportunist) opp++;
                tol += p.RedLightTolerance;
            }
            Assert.AreEqual(0.25, opp / (double)n, 0.03, "share of opportunists among adults");
            // mean of Weibull(1.6, 40) = 40 * Gamma(1 + 1/1.6) = 35.9 s
            Assert.AreEqual(35.9, tol / n, 1.5);
        }
    }
}
