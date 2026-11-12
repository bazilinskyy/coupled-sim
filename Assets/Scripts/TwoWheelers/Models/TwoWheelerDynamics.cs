using System;

namespace CoupledSim.TwoWheelers
{
    public enum TwoWheelerType
    {
        Bicycle,
        EBike,
        EScooter,
        /// <summary>Dutch "snorfiets": moped limited to 25 km/h.</summary>
        Moped25,
        /// <summary>Dutch "bromfiets": moped limited to 45 km/h.</summary>
        Moped45,
    }

    /// <summary>Physical parameters of a two-wheeler with its rider.</summary>
    [Serializable]
    public struct TwoWheelerParameters
    {
        public TwoWheelerType Type;
        /// <summary>Vehicle plus rider (kg).</summary>
        public float Mass;
        /// <summary>Wheelbase (m).</summary>
        public float Wheelbase;
        /// <summary>Rolling resistance coefficient.</summary>
        public float RollingResistance;
        /// <summary>Drag area Cd·A (m²).</summary>
        public float DragArea;
        /// <summary>Sustained driving power (W): the rider's on a bicycle, the motor's otherwise.</summary>
        public float Power;
        /// <summary>Maximum driving force at low speed (N): gearing / motor torque limit.</summary>
        public float MaxDriveForce;
        /// <summary>Speed (m/s) above which the motor stops assisting (0 = none: pure pedalling).</summary>
        public float MotorCutoffSpeed;
        /// <summary>Extra pedal power on a pedelec (W), on top of the rider's.</summary>
        public float RiderPower;
        /// <summary>Maximum braking deceleration (m/s²), limited by tyre grip and stability.</summary>
        public float MaxBraking;
        /// <summary>Maximum steering angle (deg) at walking speed.</summary>
        public float MaxSteerAngle;
        /// <summary>Speed (m/s) at which the steering range has halved.</summary>
        public float SteerHalvingSpeed;
        /// <summary>Lean response time constant (s).</summary>
        public float LeanTimeConstant;
        /// <summary>Largest lean angle (deg).</summary>
        public float MaxLean;

        const float kmh = 1f / 3.6f;

        /// <summary>Typical values: city bike and rider (Wilson, Bicycling Science; Kooijman &amp; Schwab), e-steps and mopeds from manufacturer data and Dutch speed limits.</summary>
        public static TwoWheelerParameters Preset(TwoWheelerType type)
        {
            switch (type)
            {
                case TwoWheelerType.EBike:
                    return new TwoWheelerParameters
                    {
                        Type = type, Mass = 100, Wheelbase = 1.1f, RollingResistance = 0.006f, DragArea = 0.55f,
                        Power = 250, MaxDriveForce = 180, MotorCutoffSpeed = 25 * kmh, RiderPower = 120,
                        MaxBraking = 5.5f, MaxSteerAngle = 35, SteerHalvingSpeed = 4f, LeanTimeConstant = 0.35f, MaxLean = 40,
                    };
                case TwoWheelerType.EScooter:
                    return new TwoWheelerParameters
                    {
                        Type = type, Mass = 95, Wheelbase = 0.85f, RollingResistance = 0.012f, DragArea = 0.6f,
                        Power = 350, MaxDriveForce = 160, MotorCutoffSpeed = 25 * kmh, RiderPower = 0,
                        MaxBraking = 4f, MaxSteerAngle = 30, SteerHalvingSpeed = 3f, LeanTimeConstant = 0.25f, MaxLean = 30,
                    };
                case TwoWheelerType.Moped25:
                    return new TwoWheelerParameters
                    {
                        Type = type, Mass = 170, Wheelbase = 1.3f, RollingResistance = 0.012f, DragArea = 0.6f,
                        Power = 1500, MaxDriveForce = 450, MotorCutoffSpeed = 25 * kmh, RiderPower = 0,
                        MaxBraking = 6f, MaxSteerAngle = 30, SteerHalvingSpeed = 5f, LeanTimeConstant = 0.4f, MaxLean = 40,
                    };
                case TwoWheelerType.Moped45:
                    return new TwoWheelerParameters
                    {
                        Type = type, Mass = 175, Wheelbase = 1.3f, RollingResistance = 0.012f, DragArea = 0.6f,
                        Power = 3000, MaxDriveForce = 550, MotorCutoffSpeed = 45 * kmh, RiderPower = 0,
                        MaxBraking = 6.5f, MaxSteerAngle = 30, SteerHalvingSpeed = 5f, LeanTimeConstant = 0.4f, MaxLean = 42,
                    };
                default: // city bicycle
                    return new TwoWheelerParameters
                    {
                        Type = TwoWheelerType.Bicycle, Mass = 95, Wheelbase = 1.1f, RollingResistance = 0.006f, DragArea = 0.55f,
                        Power = 150, MaxDriveForce = 220, MotorCutoffSpeed = 0, RiderPower = 0,
                        MaxBraking = 5f, MaxSteerAngle = 35, SteerHalvingSpeed = 4f, LeanTimeConstant = 0.35f, MaxLean = 40,
                    };
            }
        }
    }

    /// <summary>Rider input, each 0..1 except Steer (-1 left .. 1 right).</summary>
    public struct TwoWheelerInput
    {
        public float Drive;
        public float Brake;
        public float Steer;
    }

    /// <summary>State on the ground plane: x east, z north (Unity), yaw clockwise from +z.</summary>
    public struct TwoWheelerState
    {
        public float X, Z;
        /// <summary>Heading (rad), clockwise from +z, like Unity's yaw.</summary>
        public float Yaw;
        /// <summary>Forward speed (m/s), never negative (no reversing).</summary>
        public float Speed;
        /// <summary>Front wheel steering angle (rad), positive to the right.</summary>
        public float Steer;
        /// <summary>Roll angle (rad), positive leaning right.</summary>
        public float Lean;
        public float Acceleration;
        public float YawRate;
    }

    /// <summary>
    /// Bicycle-model dynamics of a two-wheeler: longitudinal force balance (drive power and force
    /// limits, motor cut-off speed, rolling resistance, air drag, slope, braking) and a kinematic
    /// bicycle model for the yaw, with the steering range narrowing with speed and the rider leaning
    /// into turns (equilibrium lean tan φ = v·r/g, reached with a first-order lag). Counter-steering
    /// and capsize dynamics are not modelled.
    /// </summary>
    public static class TwoWheelerDynamics
    {
        public const float Gravity = 9.81f;
        const float AirDensity = 1.2f;

        /// <summary>Driving force (N) at speed v for drive input 0..1.</summary>
        public static float DriveForce(in TwoWheelerParameters p, float v, float drive)
        {
            drive = Math.Clamp(drive, 0f, 1f);
            if (drive <= 0f) return 0f;
            float vv = Math.Max(v, 0.5f);
            float motor = 0f, rider = 0f;
            if (p.Type == TwoWheelerType.Bicycle)
            {
                rider = Math.Min(p.MaxDriveForce, drive * p.Power / vv);
            }
            else
            {
                // assistance fades out over 1 km/h above the cut-off speed
                float fade = p.MotorCutoffSpeed > 0 ? Math.Clamp((p.MotorCutoffSpeed + 0.28f - v) / 0.28f, 0f, 1f) : 1f;
                motor = fade * Math.Min(p.MaxDriveForce, drive * p.Power / vv);
                if (p.RiderPower > 0) rider = Math.Min(200f, drive * p.RiderPower / vv);
            }
            return motor + rider;
        }

        public static float ResistanceForce(in TwoWheelerParameters p, float v, float slope)
        {
            float rolling = v > 0.01f ? p.RollingResistance * p.Mass * Gravity * (float)Math.Cos(slope) : 0f;
            float air = 0.5f * AirDensity * p.DragArea * v * v;
            float grade = p.Mass * Gravity * (float)Math.Sin(slope);
            return rolling + air + grade;
        }

        /// <summary>Largest steering angle (rad) at speed v: full range at walking pace, narrower when fast.</summary>
        public static float MaxSteer(in TwoWheelerParameters p, float v)
        {
            float max = p.MaxSteerAngle * (float)Math.PI / 180f;
            return max / (1f + Math.Max(0f, v) / Math.Max(0.1f, p.SteerHalvingSpeed));
        }

        /// <summary>Advances the state by dt seconds. slope: road gradient (rad), positive uphill.</summary>
        public static TwoWheelerState Step(TwoWheelerState s, in TwoWheelerParameters p, TwoWheelerInput input, float dt, float slope = 0f)
        {
            float v = s.Speed;
            float force = DriveForce(p, v, input.Drive) - ResistanceForce(p, v, slope);
            float a = force / p.Mass - Math.Clamp(input.Brake, 0f, 1f) * p.MaxBraking;
            float vNew = Math.Max(0f, v + a * dt);
            s.Acceleration = (vNew - v) / dt;

            // steering: the rider turns the handlebar at a finite rate
            float targetSteer = Math.Clamp(input.Steer, -1f, 1f) * MaxSteer(p, vNew);
            float maxRate = 2.5f * dt; // rad/s
            s.Steer += Math.Clamp(targetSteer - s.Steer, -maxRate, maxRate);

            float vMid = 0.5f * (v + vNew);
            s.YawRate = vMid * (float)Math.Tan(s.Steer) / p.Wheelbase;
            // at walking speed the rider keeps balance with the feet / small corrections: little lean
            float equilibriumLean = (float)Math.Atan(vMid * s.YawRate / Gravity);
            float maxLean = p.MaxLean * (float)Math.PI / 180f;
            equilibriumLean = Math.Clamp(equilibriumLean, -maxLean, maxLean);
            s.Lean += (equilibriumLean - s.Lean) * (1f - (float)Math.Exp(-dt / Math.Max(0.05f, p.LeanTimeConstant)));

            s.Yaw += s.YawRate * dt;
            s.X += vMid * (float)Math.Sin(s.Yaw) * dt;
            s.Z += vMid * (float)Math.Cos(s.Yaw) * dt;
            s.Speed = vNew;
            return s;
        }
    }
}
