using System;
using System.Collections.Generic;

namespace CoupledSim.Pedestrians
{
    /// <summary>What an approaching vehicle's eHMI tells the pedestrian.</summary>
    public enum EhmiSignal
    {
        None,
        Walk,
        DontWalk,
    }

    public enum CrossingPhase
    {
        /// <summary>Walking towards the kerb.</summary>
        Approaching,
        /// <summary>At the kerb, looking for a gap.</summary>
        Waiting,
        /// <summary>A gap was accepted; stepping off after the reaction time.</summary>
        Starting,
        /// <summary>On the road.</summary>
        Crossing,
        /// <summary>Reached the far kerb.</summary>
        Done,
    }

    /// <summary>A vehicle approaching the crossing, as the pedestrian perceives it.</summary>
    public struct ApproachingVehicle
    {
        /// <summary>Stable ID, used to remember per-vehicle impressions (e.g. trust in its eHMI).</summary>
        public int Id;
        /// <summary>Distance (m) from the vehicle's front to the pedestrian's path, along the vehicle's path. &lt;= 0 means passed.</summary>
        public float DistanceToConflict;
        /// <summary>Speed (m/s).</summary>
        public float Speed;
        /// <summary>Longitudinal acceleration (m/s²); negative is braking.</summary>
        public float Acceleration;
        /// <summary>Distance (m) the pedestrian must walk from the kerb to clear this vehicle's lane.</summary>
        public float ClearDistance;
        public EhmiSignal Ehmi;
    }

    /// <summary>Result of evaluating one vehicle.</summary>
    public struct VehicleAssessment
    {
        public int Id;
        public float TimeToArrival;
        public float RequiredGap;
        public bool Yielding;
        public bool Acceptable;
    }

    /// <summary>
    /// Gap-acceptance crossing decision for one pedestrian at one crossing.
    /// The pedestrian crosses when every approaching vehicle either arrives later than the
    /// required gap or is seen to yield (braking enough to stop before the crossing, stopped,
    /// or - with a probability given by the pedestrian's trust - signalling "walk" on its eHMI).
    /// The required gap is the larger of the (impatience-reduced) critical gap and the time needed
    /// to clear the vehicle's lane plus a safety margin. After an acceptable situation is seen,
    /// the pedestrian steps off after the reaction time, unless the situation changes in between.
    /// </summary>
    public sealed class GapAcceptanceModel
    {
        public PedestrianProfile Profile;
        public CrossingPhase Phase { get; private set; } = CrossingPhase.Approaching;
        public float WaitingTime { get; private set; }
        public float StartTimer { get; private set; }
        /// <summary>Short text describing the last decision, for logging and debugging.</summary>
        public string Reason { get; private set; } = "";
        public readonly List<VehicleAssessment> LastAssessment = new List<VehicleAssessment>();

        /// <summary>Seconds a pedestrian looks before the first decision (scanning left and right).</summary>
        public float MinimumLookTime = 0.8f;
        /// <summary>A vehicle slower than this (m/s) within <see cref="StoppedDistance"/> counts as stopped for the pedestrian.</summary>
        public float StoppedSpeed = 0.5f;
        public float StoppedDistance = 20f;

        readonly Random _rng;
        readonly Dictionary<int, bool> _trustsEhmi = new Dictionary<int, bool>();

        public GapAcceptanceModel(PedestrianProfile profile, Random rng)
        {
            Profile = profile;
            _rng = rng ?? new Random(0);
        }

        /// <summary>The pedestrian reached the kerb and starts to wait.</summary>
        public void ArriveAtKerb()
        {
            Phase = CrossingPhase.Waiting;
            WaitingTime = 0;
            StartTimer = 0;
            Reason = "arrived at kerb";
        }

        public void FinishCrossing()
        {
            Phase = CrossingPhase.Done;
            Reason = "crossed";
        }

        public void Reset()
        {
            Phase = CrossingPhase.Approaching;
            WaitingTime = 0;
            StartTimer = 0;
            _trustsEhmi.Clear();
        }

        /// <summary>Accepted gap after waiting `waited` seconds (people get less choosy while waiting).</summary>
        public float AcceptedGap(float waited)
        {
            float f = (float)Math.Exp(-waited / Math.Max(1e-3f, Profile.PatienceTime));
            float frac = Profile.MinGapFraction + (1f - Profile.MinGapFraction) * f;
            return Profile.CriticalGap * frac;
        }

        /// <summary>
        /// Time (s) until the vehicle reaches the conflict point with constant acceleration;
        /// +inf if it stops before it.
        /// </summary>
        public static float TimeToArrival(float distance, float speed, float accel)
        {
            if (distance <= 0) return 0;
            if (Math.Abs(accel) < 1e-3f)
                return speed > 1e-3f ? distance / speed : float.PositiveInfinity;
            // distance = v t + a t² / 2
            float disc = speed * speed + 2f * accel * distance;
            if (disc < 0) return float.PositiveInfinity; // stops before reaching it
            float t = (-speed + (float)Math.Sqrt(disc)) / accel;
            return t >= 0 ? t : float.PositiveInfinity;
        }

        public VehicleAssessment Assess(in ApproachingVehicle v, float waited)
        {
            var a = new VehicleAssessment { Id = v.Id };
            a.TimeToArrival = TimeToArrival(v.DistanceToConflict, v.Speed, v.Acceleration);

            bool stopped = v.Speed < StoppedSpeed && v.DistanceToConflict < StoppedDistance;
            bool braking = -v.Acceleration >= Profile.YieldDetectionDeceleration;
            bool stopsBefore = v.Acceleration < 0 &&
                               v.Speed * v.Speed / (2f * -v.Acceleration) < v.DistanceToConflict;
            bool trustsEhmi = false;
            if (v.Ehmi == EhmiSignal.Walk)
            {
                if (!_trustsEhmi.TryGetValue(v.Id, out trustsEhmi))
                {
                    trustsEhmi = _rng.NextDouble() < Profile.EhmiTrust;
                    _trustsEhmi[v.Id] = trustsEhmi;
                }
            }
            a.Yielding = stopped || (braking && stopsBefore) || trustsEhmi;

            float clearTime = v.ClearDistance / Math.Max(0.1f, Profile.CrossingSpeed);
            a.RequiredGap = Math.Max(AcceptedGap(waited), clearTime + Profile.SafetyMargin);
            if (v.Ehmi == EhmiSignal.DontWalk && !stopped)
                a.RequiredGap *= 1.25f; // an explicit "don't walk" makes people more careful
            a.Acceptable = v.DistanceToConflict <= 0 || a.Yielding || a.TimeToArrival >= a.RequiredGap;
            return a;
        }

        /// <summary>
        /// Advances the decision by dt seconds with the current traffic picture.
        /// Returns true while the pedestrian should be (or start) walking on the road.
        /// </summary>
        public bool Update(float dt, IReadOnlyList<ApproachingVehicle> vehicles)
        {
            switch (Phase)
            {
                case CrossingPhase.Approaching:
                    return false;
                case CrossingPhase.Crossing:
                    return true;
                case CrossingPhase.Done:
                    return false;
            }

            WaitingTime += dt;
            LastAssessment.Clear();
            bool allOk = true;
            string blocker = null;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var a = Assess(vehicles[i], WaitingTime);
                LastAssessment.Add(a);
                if (!a.Acceptable)
                {
                    allOk = false;
                    blocker = $"vehicle {a.Id}: TTA {a.TimeToArrival:F1}s < {a.RequiredGap:F1}s";
                }
            }

            if (WaitingTime < MinimumLookTime)
            {
                Reason = "looking";
                return false;
            }

            if (!allOk)
            {
                Phase = CrossingPhase.Waiting;
                StartTimer = 0;
                Reason = blocker;
                return false;
            }

            if (Phase == CrossingPhase.Waiting)
            {
                Phase = CrossingPhase.Starting;
                StartTimer = 0;
                Reason = vehicles.Count == 0 ? "road clear" : "gap accepted";
            }
            StartTimer += dt;
            if (StartTimer >= Profile.ReactionTime)
            {
                Phase = CrossingPhase.Crossing;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Speed factor while on the road: pedestrians hurry when a non-yielding vehicle gets close.
        /// </summary>
        public float CrossingSpeedFactor(IReadOnlyList<ApproachingVehicle> vehicles)
        {
            float factor = 1f;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (v.DistanceToConflict <= 0) continue;
                var a = Assess(v, WaitingTime);
                if (!a.Yielding && a.TimeToArrival < 3f)
                    factor = Math.Max(factor, 1.5f); // hurry / jog
            }
            return factor;
        }
    }
}
