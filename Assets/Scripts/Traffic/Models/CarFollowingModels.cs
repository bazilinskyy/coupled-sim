using System;

namespace CoupledSim.Traffic
{
    /// <summary>What a driver sees ahead: the next vehicle, a red light's stop line or a pedestrian on the road.</summary>
    public struct Leader
    {
        /// <summary>False on a free road.</summary>
        public bool Present;
        /// <summary>Bumper-to-bumper distance (m) to the leader (or to the stop line / pedestrian).</summary>
        public float Gap;
        /// <summary>Speed (m/s) of the leader; 0 for stop lines and pedestrians.</summary>
        public float Speed;

        public static Leader None => new Leader { Present = false, Gap = float.PositiveInfinity };
        public static Leader Stationary(float gap) => new Leader { Present = true, Gap = gap, Speed = 0 };
        public static Leader Vehicle(float gap, float speed) => new Leader { Present = true, Gap = gap, Speed = speed };
    }

    public enum CarFollowingModelType
    {
        /// <summary>Intelligent Driver Model (Treiber, Hennecke &amp; Helbing, 2000).</summary>
        IDM,
        /// <summary>Gipps (1981) safe-speed model.</summary>
        Gipps,
        /// <summary>Krauss (1998), SUMO's default car-following model, with random dawdling.</summary>
        Krauss,
    }

    /// <summary>A longitudinal driver model: acceleration from own speed and the leader.</summary>
    public interface ICarFollowingModel
    {
        string Name { get; }
        /// <summary>Desired (free-road) speed (m/s).</summary>
        float DesiredSpeed { get; set; }
        /// <summary>Acceleration (m/s²) for the next time step of length dt (s).</summary>
        float Acceleration(float speed, in Leader leader, float dt);
    }

    /// <summary>Intelligent Driver Model. Defaults are typical urban values (Treiber &amp; Kesting, 2013).</summary>
    [Serializable]
    public sealed class IntelligentDriverModel : ICarFollowingModel
    {
        public string Name => "IDM";
        public float DesiredSpeed { get; set; } = 13.9f;
        /// <summary>Desired time headway (s).</summary>
        public float TimeHeadway = 1.5f;
        /// <summary>Maximum acceleration (m/s²).</summary>
        public float MaxAcceleration = 1.0f;
        /// <summary>Comfortable deceleration (m/s², positive).</summary>
        public float ComfortableDeceleration = 2.0f;
        /// <summary>Minimum gap when stopped (m).</summary>
        public float MinimumGap = 2.0f;
        /// <summary>Acceleration exponent.</summary>
        public float Delta = 4f;
        /// <summary>Hardest braking the model may ask for (m/s², positive).</summary>
        public float MaxDeceleration = 9f;

        public float Acceleration(float speed, in Leader leader, float dt)
        {
            float v0 = Math.Max(0.1f, DesiredSpeed);
            float free = 1f - (float)Math.Pow(Math.Max(0f, speed) / v0, Delta);
            float interaction = 0f;
            if (leader.Present)
            {
                float dv = speed - leader.Speed;
                float sStar = MinimumGap + Math.Max(0f, speed * TimeHeadway +
                    speed * dv / (2f * (float)Math.Sqrt(MaxAcceleration * ComfortableDeceleration)));
                float s = Math.Max(0.1f, leader.Gap);
                interaction = (sStar / s) * (sStar / s);
            }
            float a = MaxAcceleration * (free - interaction);
            return Math.Max(-MaxDeceleration, a);
        }
    }

    /// <summary>Gipps (1981): the speed is the smaller of a free-acceleration and a safe-braking speed.</summary>
    [Serializable]
    public sealed class GippsModel : ICarFollowingModel
    {
        public string Name => "Gipps";
        public float DesiredSpeed { get; set; } = 13.9f;
        /// <summary>Maximum acceleration (m/s²).</summary>
        public float MaxAcceleration = 1.7f;
        /// <summary>Most severe braking the driver will use (m/s², positive).</summary>
        public float MaxDeceleration = 3.4f;
        /// <summary>The driver's estimate of the leader's most severe braking (m/s², positive).</summary>
        public float LeaderDecelerationEstimate = 3.2f;
        /// <summary>Reaction time (s); Gipps used 2/3 s.</summary>
        public float ReactionTime = 0.667f;
        /// <summary>Gap kept at standstill (m).</summary>
        public float MinimumGap = 2.0f;

        public float Acceleration(float speed, in Leader leader, float dt)
        {
            float tau = Math.Max(dt, ReactionTime);
            float V = Math.Max(0.1f, DesiredSpeed);
            float v = Math.Max(0f, speed);
            float vFree = v + 2.5f * MaxAcceleration * tau * (1f - v / V) * (float)Math.Sqrt(Math.Max(0f, 0.025f + v / V));
            float vNext = vFree;
            if (leader.Present)
            {
                float B = MaxDeceleration;
                float gap = leader.Gap - MinimumGap;
                float term = B * B * tau * tau + B * (2f * gap - v * tau + leader.Speed * leader.Speed / Math.Max(0.1f, LeaderDecelerationEstimate));
                float vSafe = term > 0 ? -B * tau + (float)Math.Sqrt(term) : 0f;
                vNext = Math.Min(vFree, Math.Max(0f, vSafe));
            }
            // the model gives the speed after the reaction time; spread the change over that time
            return Math.Max(-MaxDeceleration * 2.5f, (vNext - v) / tau);
        }
    }

    /// <summary>
    /// Krauss (1998) safe-speed model as used by SUMO: drive at the safe speed for the leader,
    /// limited by acceleration, and randomly dawdle (imperfection sigma).
    /// </summary>
    [Serializable]
    public sealed class KraussModel : ICarFollowingModel
    {
        public string Name => "Krauss";
        public float DesiredSpeed { get; set; } = 13.9f;
        public float MaxAcceleration = 2.6f;
        public float Deceleration = 4.5f;
        /// <summary>Driver imperfection 0..1 (SUMO default 0.5).</summary>
        public float Sigma = 0.5f;
        /// <summary>Reaction time / desired headway (s).</summary>
        public float Tau = 1.0f;
        public float MinimumGap = 2.5f;
        [NonSerialized] public Random Random = new Random(0);

        public float Acceleration(float speed, in Leader leader, float dt)
        {
            float v = Math.Max(0f, speed);
            float vSafe = float.PositiveInfinity;
            if (leader.Present)
            {
                float g = Math.Max(0f, leader.Gap - MinimumGap);
                float vMean = 0.5f * (v + leader.Speed);
                vSafe = leader.Speed + (g - leader.Speed * Tau) / (vMean / Deceleration + Tau);
                vSafe = Math.Max(0f, vSafe);
            }
            float vDes = Math.Min(Math.Min(DesiredSpeed, v + MaxAcceleration * dt), vSafe);
            float dawdle = Sigma * MaxAcceleration * dt * (float)(Random?.NextDouble() ?? 0.5);
            float vNext = Math.Max(0f, vDes - dawdle);
            // SUMO does not brake harder than needed; never brake faster than physically sensible
            return Math.Max(-9f, (vNext - v) / Math.Max(1e-3f, dt));
        }
    }

    public static class CarFollowingModels
    {
        public static ICarFollowingModel Create(CarFollowingModelType type, float desiredSpeed, Random rng)
        {
            switch (type)
            {
                case CarFollowingModelType.Gipps: return new GippsModel { DesiredSpeed = desiredSpeed };
                case CarFollowingModelType.Krauss: return new KraussModel { DesiredSpeed = desiredSpeed, Random = rng ?? new Random(0) };
                default: return new IntelligentDriverModel { DesiredSpeed = desiredSpeed };
            }
        }
    }

    /// <summary>
    /// MOBIL lane-change decision (Kesting, Treiber &amp; Helbing, 2007): change if the own advantage
    /// plus the politeness-weighted change for the followers exceeds a threshold, and the new
    /// follower does not have to brake harder than the safe limit.
    /// </summary>
    public static class Mobil
    {
        /// <param name="accelCurrent">own acceleration in the current lane</param>
        /// <param name="accelTarget">own acceleration after the change</param>
        /// <param name="newFollowerBefore">acceleration of the follower in the target lane now</param>
        /// <param name="newFollowerAfter">its acceleration if we change in front of it</param>
        /// <param name="oldFollowerBefore">acceleration of our current follower now</param>
        /// <param name="oldFollowerAfter">its acceleration after we leave</param>
        public static bool ShouldChange(float accelCurrent, float accelTarget,
            float newFollowerBefore, float newFollowerAfter,
            float oldFollowerBefore, float oldFollowerAfter,
            float politeness = 0.3f, float threshold = 0.2f, float safeDeceleration = 4f, float bias = 0f)
        {
            if (newFollowerAfter < -safeDeceleration) return false;
            float incentive = accelTarget - accelCurrent +
                              politeness * ((newFollowerAfter - newFollowerBefore) + (oldFollowerAfter - oldFollowerBefore));
            return incentive > threshold - bias;
        }
    }
}
