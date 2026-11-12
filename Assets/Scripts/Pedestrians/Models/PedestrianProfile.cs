using System;

namespace CoupledSim.Pedestrians
{
    public enum AgeGroup
    {
        Adult,
        Child,
        Older,
    }

    /// <summary>
    /// Individual parameters of one AI pedestrian. Draw them with <see cref="PedestrianProfile.Sample"/>
    /// from a seeded random generator so a trial can be repeated exactly.
    /// </summary>
    [Serializable]
    public struct PedestrianProfile
    {
        public AgeGroup AgeGroup;
        /// <summary>Free walking speed (m/s).</summary>
        public float WalkingSpeed;
        /// <summary>Walking speed on a crossing is usually a bit higher than on the pavement.</summary>
        public float CrossingSpeedFactor;
        /// <summary>Smallest time gap (s) to an approaching, non-yielding vehicle that this pedestrian accepts.</summary>
        public float CriticalGap;
        /// <summary>Extra time (s) the pedestrian wants between clearing a lane and the vehicle arriving.</summary>
        public float SafetyMargin;
        /// <summary>Time (s) between seeing an acceptable gap and stepping off the kerb.</summary>
        public float ReactionTime;
        /// <summary>Deceleration (m/s², positive) from which a vehicle is seen as yielding.</summary>
        public float YieldDetectionDeceleration;
        /// <summary>Probability (0..1) of trusting an eHMI that signals "walk" before the car visibly slows.</summary>
        public float EhmiTrust;
        /// <summary>Time constant (s) of impatience: the accepted gap shrinks the longer one waits.</summary>
        public float PatienceTime;
        /// <summary>Lower bound for the accepted gap as a fraction of the critical gap.</summary>
        public float MinGapFraction;
        /// <summary>Body radius for collision avoidance (m).</summary>
        public float Radius;

        public float CrossingSpeed => WalkingSpeed * CrossingSpeedFactor;

        public static PedestrianProfile Default => new PedestrianProfile
        {
            AgeGroup = AgeGroup.Adult,
            WalkingSpeed = 1.34f,
            CrossingSpeedFactor = 1.1f,
            CriticalGap = 4.5f,
            SafetyMargin = 1.0f,
            ReactionTime = 0.9f,
            YieldDetectionDeceleration = 1.0f,
            EhmiTrust = 0.7f,
            PatienceTime = 60f,
            MinGapFraction = 0.6f,
            Radius = 0.25f,
        };

        /// <summary>
        /// Draws a pedestrian. Sources for the defaults: free walking speed N(1.34, 0.26) m/s
        /// (Weidmann 1993); critical gaps of about 4-5 s with a log-normal spread (Brewer et al. 2006);
        /// perception-reaction around 1 s; older pedestrians walk slower and choose larger gaps
        /// (Oxley et al. 2005), children walk slower and accept smaller gaps.
        /// </summary>
        public static PedestrianProfile Sample(Random rng, AgeGroup group = AgeGroup.Adult)
        {
            var p = Default;
            p.AgeGroup = group;
            float speedMean = 1.34f, speedSd = 0.26f, gapMedian = 4.5f, reactionMedian = 0.9f;
            switch (group)
            {
                case AgeGroup.Child:
                    speedMean = 1.20f; speedSd = 0.25f; gapMedian = 3.8f; reactionMedian = 1.1f;
                    p.Radius = 0.2f;
                    break;
                case AgeGroup.Older:
                    speedMean = 1.05f; speedSd = 0.20f; gapMedian = 5.5f; reactionMedian = 1.2f;
                    break;
            }
            p.WalkingSpeed = Clamp(Normal(rng, speedMean, speedSd), 0.6f, 2.0f);
            p.CrossingSpeedFactor = Clamp(Normal(rng, 1.1f, 0.05f), 1.0f, 1.3f);
            p.CriticalGap = Clamp(LogNormal(rng, gapMedian, 0.25f), 2.0f, 10f);
            p.SafetyMargin = Clamp(Normal(rng, 1.0f, 0.3f), 0.3f, 2.0f);
            p.ReactionTime = Clamp(LogNormal(rng, reactionMedian, 0.3f), 0.3f, 2.5f);
            p.YieldDetectionDeceleration = Clamp(Normal(rng, 1.0f, 0.3f), 0.4f, 2.0f);
            p.EhmiTrust = Clamp(Normal(rng, 0.7f, 0.2f), 0f, 1f);
            p.PatienceTime = Clamp(Normal(rng, 60f, 20f), 20f, 120f);
            return p;
        }

        public static float Normal(Random rng, float mean, float sd)
        {
            // Box-Muller
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return (float)(mean + sd * z);
        }

        public static float LogNormal(Random rng, float median, float sigma) =>
            (float)(median * Math.Exp(sigma * Normal(rng, 0f, 1f)));

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
