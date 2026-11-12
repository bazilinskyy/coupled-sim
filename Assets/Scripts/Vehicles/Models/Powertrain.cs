using System;

namespace CoupledSim.Vehicles
{
    /// <summary>Specification of a car's powertrain and body (from manufacturer data).</summary>
    [Serializable]
    public struct CarSpec
    {
        public string Name;
        /// <summary>Kerb mass plus driver (kg).</summary>
        public float Mass;
        public float Wheelbase;
        public float TrackWidth;
        public float CentreOfMassHeight;
        public float WheelRadius;
        public float DragCoefficient;
        public float FrontalArea;
        public float RollingResistance;

        public float MaxPowerKw;
        public float MaxPowerRpm;
        public float MaxTorqueNm;
        public float MaxTorqueRpm;
        public float IdleRpm;
        public float RedlineRpm;
        /// <summary>Forward gear ratios, first gear first.</summary>
        public float[] Gears;
        public float ReverseGear;
        public float FinalDrive;
        public float DrivetrainEfficiency;
        /// <summary>Seconds without drive torque while shifting.</summary>
        public float ShiftTime;
        public bool RearWheelDrive;
        /// <summary>Share of braking on the front axle (0..1).</summary>
        public float FrontBrakeBias;
        /// <summary>Maximum brake torque per wheel (Nm).</summary>
        public float MaxBrakeTorque;

        /// <summary>Smart Fortwo (451, 52 kW petrol, automated 5-speed): 0-100 km/h 13.3 s, 145 km/h (limited).</summary>
        public static CarSpec SmartFortwo => new CarSpec
        {
            Name = "Smart Fortwo", Mass = 900, Wheelbase = 1.87f, TrackWidth = 1.29f, CentreOfMassHeight = 0.55f, WheelRadius = 0.29f,
            DragCoefficient = 0.37f, FrontalArea = 1.95f, RollingResistance = 0.012f,
            MaxPowerKw = 52, MaxPowerRpm = 5800, MaxTorqueNm = 92, MaxTorqueRpm = 3000, IdleRpm = 850, RedlineRpm = 6200,
            Gears = new[] { 3.31f, 1.98f, 1.33f, 1.0f, 0.79f }, ReverseGear = 3.5f, FinalDrive = 4.2f,
            DrivetrainEfficiency = 0.9f, ShiftTime = 0.45f, RearWheelDrive = true, FrontBrakeBias = 0.65f, MaxBrakeTorque = 1600,
        };

        /// <summary>Ford Focus 2011 1.6 Ti-VCT (92 kW, 6-speed): 0-100 km/h 10.9 s, 196 km/h.</summary>
        public static CarSpec Hatchback => new CarSpec
        {
            Name = "Hatchback (Focus 1.6)", Mass = 1350, Wheelbase = 2.65f, TrackWidth = 1.55f, CentreOfMassHeight = 0.55f, WheelRadius = 0.31f,
            DragCoefficient = 0.31f, FrontalArea = 2.2f, RollingResistance = 0.011f,
            MaxPowerKw = 92, MaxPowerRpm = 6300, MaxTorqueNm = 159, MaxTorqueRpm = 4000, IdleRpm = 800, RedlineRpm = 6800,
            Gears = new[] { 3.58f, 1.93f, 1.28f, 0.95f, 0.76f, 0.62f }, ReverseGear = 3.6f, FinalDrive = 4.07f,
            DrivetrainEfficiency = 0.9f, ShiftTime = 0.3f, RearWheelDrive = false, FrontBrakeBias = 0.7f, MaxBrakeTorque = 2200,
        };
    }

    /// <summary>
    /// Engine and automatic gearbox: engine torque from rpm (rising to the torque peak, then limited
    /// by maximum power), gear selection with throttle-dependent shift points and kick-down, a
    /// torque interruption while shifting, and engine braking off throttle.
    /// </summary>
    public sealed class Powertrain
    {
        public readonly CarSpec Spec;
        public int Gear { get; private set; } = 1;
        public float Rpm { get; private set; }
        public bool Reverse { get; set; }
        float _shiftTimer;

        public Powertrain(CarSpec spec)
        {
            Spec = spec;
            Rpm = spec.IdleRpm;
        }

        /// <summary>Full-throttle engine torque (Nm) at rpm.</summary>
        public float EngineTorque(float rpm)
        {
            rpm = Math.Max(rpm, Spec.IdleRpm);
            if (rpm > Spec.RedlineRpm) return 0f; // rev limiter
            float rising = Spec.MaxTorqueNm * (0.65f + 0.35f * Math.Min(1f, (rpm - Spec.IdleRpm) / Math.Max(1f, Spec.MaxTorqueRpm - Spec.IdleRpm)));
            float powerLimited = Spec.MaxPowerKw * 1000f / (rpm * 2f * (float)Math.PI / 60f);
            return Math.Min(rising, Math.Max(powerLimited, 0f));
        }

        public float Ratio => (Reverse ? Spec.ReverseGear : Spec.Gears[Gear - 1]) * Spec.FinalDrive;

        float RpmAt(float speed, int gear) =>
            Math.Abs(speed) / Spec.WheelRadius * 60f / (2f * (float)Math.PI) * (Reverse ? Spec.ReverseGear : Spec.Gears[gear - 1]) * Spec.FinalDrive;

        /// <summary>
        /// Torque at the driven wheels in total (Nm, signed in the driving direction) for vehicle speed
        /// (m/s, forward positive) and throttle 0..1; updates gear and rpm.
        /// </summary>
        public float WheelTorque(float speed, float throttle, float dt)
        {
            throttle = Math.Clamp(throttle, 0f, 1f);
            if (!Reverse) AutoShift(speed, throttle);
            else Gear = 1;
            // clutch slips below idle: the engine stays at least at idle (or higher when pulling away)
            float wheelRpm = RpmAt(speed, Gear);
            float launch = Spec.IdleRpm + throttle * (Spec.MaxTorqueRpm - Spec.IdleRpm) * 0.6f;
            Rpm = Math.Max(wheelRpm, Math.Min(launch, Spec.RedlineRpm));
            if (_shiftTimer > 0f)
            {
                _shiftTimer -= dt;
                return 0f;
            }
            float engine = throttle > 0.01f
                ? throttle * EngineTorque(Rpm)
                : -EngineBraking(Rpm) * (wheelRpm > Spec.IdleRpm ? 1f : 0f);
            float t = engine * Ratio * Spec.DrivetrainEfficiency;
            return Reverse ? -t : t;
        }

        /// <summary>Engine braking torque (Nm, positive) off throttle: friction and pumping losses.</summary>
        public float EngineBraking(float rpm) => 0.0045f * Math.Max(0f, rpm - Spec.IdleRpm) + 4f;

        void AutoShift(float speed, float throttle)
        {
            float rpm = RpmAt(speed, Gear);
            // shift up early at light throttle, near the power peak at full throttle
            float upShift = Spec.IdleRpm + (Spec.MaxPowerRpm * 1.02f - Spec.IdleRpm) * (0.3f + 0.7f * throttle);
            float downShift = Math.Max(Spec.IdleRpm * 1.4f, upShift * 0.45f);
            if (Gear < Spec.Gears.Length && rpm > upShift)
            {
                Gear++;
                _shiftTimer = Spec.ShiftTime;
            }
            else if (Gear > 1 && (rpm < downShift || (throttle > 0.9f && RpmAt(speed, Gear - 1) < Spec.MaxPowerRpm * 0.95f)))
            {
                Gear--;
                _shiftTimer = Spec.ShiftTime * 0.6f;
            }
        }

        /// <summary>Aerodynamic drag plus rolling resistance (N) at speed.</summary>
        public float Resistance(float speed)
        {
            float v = Math.Abs(speed);
            float f = 0.5f * 1.2f * Spec.DragCoefficient * Spec.FrontalArea * v * v + (v > 0.05f ? Spec.RollingResistance * Spec.Mass * 9.81f : 0f);
            return speed >= 0 ? f : -f;
        }
    }

    /// <summary>
    /// Anti-lock brakes per wheel: when a wheel locks (longitudinal slip beyond the threshold), its
    /// brake pressure is released quickly and built up again, keeping the tyre near its peak grip.
    /// </summary>
    public sealed class AntiLockBrakes
    {
        public float SlipThreshold = 0.15f;
        public float ReleaseRate = 12f;   // pressure fraction per second
        public float ApplyRate = 6f;
        float _pressure = 1f;

        /// <summary>Brake torque to apply (Nm) for the requested torque and the wheel's slip (negative = locking).</summary>
        public float Modulate(float requested, float slip, float dt)
        {
            if (requested <= 0f) { _pressure = 1f; return 0f; }
            if (slip < -SlipThreshold) _pressure = Math.Max(0.2f, _pressure - ReleaseRate * dt);
            else _pressure = Math.Min(1f, _pressure + ApplyRate * dt);
            return requested * _pressure;
        }
    }
}
