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
        /// <summary>Speed limiter (km/h; 0 = none), e.g. 80 for city buses, 90 for EU trucks.</summary>
        public float MaxSpeedKmh;
        /// <summary>Acceleration limit for standing passengers (m/s²; 0 = none): city buses about 1.2-1.5.</summary>
        public float MaxAcceleration;
        /// <summary>Mass of each wheel with its tyre (kg) for the wheel colliders.</summary>
        public float WheelMass;

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

        /// <summary>SUV: VW Tiguan 2.0 TSI 4Motion (2016, 132 kW, 7-speed DSG): 0-100 km/h 7.7 s, 208 km/h, 1670 kg.</summary>
        public static CarSpec Suv => new CarSpec
        {
            Name = "SUV (Tiguan 2.0 TSI)", Mass = 1750, Wheelbase = 2.68f, TrackWidth = 1.58f, CentreOfMassHeight = 0.66f, WheelRadius = 0.35f,
            DragCoefficient = 0.32f, FrontalArea = 2.6f, RollingResistance = 0.012f,
            MaxPowerKw = 132, MaxPowerRpm = 5000, MaxTorqueNm = 320, MaxTorqueRpm = 2500, IdleRpm = 750, RedlineRpm = 6500,
            Gears = new[] { 3.40f, 2.29f, 1.55f, 1.10f, 0.88f, 0.73f, 0.62f }, ReverseGear = 3.8f, FinalDrive = 4.47f,
            DrivetrainEfficiency = 0.88f, ShiftTime = 0.15f, RearWheelDrive = false, FrontBrakeBias = 0.68f, MaxBrakeTorque = 3000, WheelMass = 24,
        };

        /// <summary>Estate: VW Passat Variant 2.0 TDI (B8, 110 kW diesel, 6-speed DSG): 0-100 km/h 8.9 s, 216 km/h, 1550 kg.</summary>
        public static CarSpec Estate => new CarSpec
        {
            Name = "Estate (Passat Variant 2.0 TDI)", Mass = 1625, Wheelbase = 2.79f, TrackWidth = 1.58f, CentreOfMassHeight = 0.55f, WheelRadius = 0.33f,
            DragCoefficient = 0.29f, FrontalArea = 2.3f, RollingResistance = 0.011f,
            MaxPowerKw = 110, MaxPowerRpm = 3750, MaxTorqueNm = 340, MaxTorqueRpm = 2200, IdleRpm = 800, RedlineRpm = 4800,
            Gears = new[] { 3.46f, 2.05f, 1.30f, 0.95f, 0.76f, 0.62f }, ReverseGear = 3.9f, FinalDrive = 3.6f,
            DrivetrainEfficiency = 0.9f, ShiftTime = 0.15f, RearWheelDrive = false, FrontBrakeBias = 0.7f, MaxBrakeTorque = 2800, WheelMass = 22,
        };

        /// <summary>Van: Ford Transit 2.0 EcoBlue (2019, 96 kW diesel, 6-speed, rear-wheel drive), about 2.2 t empty: 0-100 km/h about 15 s, about 150 km/h.</summary>
        public static CarSpec Van => new CarSpec
        {
            Name = "Van (Transit 2.0 EcoBlue)", Mass = 2300, Wheelbase = 3.75f, TrackWidth = 1.74f, CentreOfMassHeight = 0.85f, WheelRadius = 0.36f,
            DragCoefficient = 0.37f, FrontalArea = 4.6f, RollingResistance = 0.012f,
            MaxPowerKw = 96, MaxPowerRpm = 3500, MaxTorqueNm = 385, MaxTorqueRpm = 1750, IdleRpm = 750, RedlineRpm = 4500,
            Gears = new[] { 4.71f, 2.53f, 1.56f, 1.10f, 0.84f, 0.70f }, ReverseGear = 4.2f, FinalDrive = 3.73f,
            DrivetrainEfficiency = 0.88f, ShiftTime = 0.5f, RearWheelDrive = true, FrontBrakeBias = 0.68f, MaxBrakeTorque = 3600, WheelMass = 30,
        };

        /// <summary>City bus: Mercedes-Benz Citaro (12 m, OM 936 h, 260 kW, 1400 Nm, ZF 6-speed automatic), about 14 t with passengers, limited to 80 km/h.</summary>
        public static CarSpec CityBus => new CarSpec
        {
            Name = "City bus (Citaro 12 m)", Mass = 14000, Wheelbase = 5.85f, TrackWidth = 2.1f, CentreOfMassHeight = 1.1f, WheelRadius = 0.48f,
            DragCoefficient = 0.65f, FrontalArea = 7.5f, RollingResistance = 0.008f,
            MaxPowerKw = 260, MaxPowerRpm = 2200, MaxTorqueNm = 1400, MaxTorqueRpm = 1300, IdleRpm = 600, RedlineRpm = 2500,
            Gears = new[] { 3.36f, 1.91f, 1.42f, 1.0f, 0.72f, 0.62f }, ReverseGear = 4.2f, FinalDrive = 6.2f,
            DrivetrainEfficiency = 0.86f, ShiftTime = 0.4f, RearWheelDrive = true, FrontBrakeBias = 0.45f, MaxBrakeTorque = 14000, MaxSpeedKmh = 80, MaxAcceleration = 1.4f, WheelMass = 110,
        };

        /// <summary>Box truck: DAF LF 260 (18 t rigid, PX-7 6.7 l, 191 kW, 1000 Nm, 12-speed automated gearbox), about 12 t half loaded, limited to 90 km/h.</summary>
        public static CarSpec Truck => new CarSpec
        {
            Name = "Box truck (DAF LF 18 t)", Mass = 12000, Wheelbase = 5.0f, TrackWidth = 2.05f, CentreOfMassHeight = 1.4f, WheelRadius = 0.47f,
            DragCoefficient = 0.7f, FrontalArea = 8.5f, RollingResistance = 0.007f,
            MaxPowerKw = 191, MaxPowerRpm = 2300, MaxTorqueNm = 1000, MaxTorqueRpm = 1400, IdleRpm = 600, RedlineRpm = 2600,
            Gears = new[] { 12.0f, 8.4f, 6.2f, 4.6f, 3.4f, 2.5f, 1.85f, 1.36f, 1.0f, 0.78f }, ReverseGear = 11f, FinalDrive = 3.9f,
            DrivetrainEfficiency = 0.86f, ShiftTime = 0.6f, RearWheelDrive = true, FrontBrakeBias = 0.45f, MaxBrakeTorque = 12000, MaxSpeedKmh = 90, WheelMass = 100,
        };

        /// <summary>The preset for a name (smart, hatchback, suv, estate, van, bus, truck), or null.</summary>
        public static CarSpec? ByName(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "smart": case "smartfortwo": return SmartFortwo;
                case "hatchback": return Hatchback;
                case "suv": return Suv;
                case "estate": case "sedan": return Estate;
                case "van": return Van;
                case "bus": case "citybus": return CityBus;
                case "truck": return Truck;
                default: return null;
            }
        }
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
        /// <summary>Reverse speed limit (m/s), as automatic cars limit it electronically (about 25 km/h).</summary>
        public float MaxReverseSpeed = 7f;
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
            // reverse: the throttle fades out towards the reverse speed limit
            if (Reverse) throttle *= Math.Clamp((MaxReverseSpeed - Math.Abs(speed)) / 1.5f, 0f, 1f);
            // speed limiter (buses, trucks)
            else if (Spec.MaxSpeedKmh > 0f) throttle *= Math.Clamp((Spec.MaxSpeedKmh / 3.6f - speed) / 1.5f, 0f, 1f);
            float engine = throttle > 0.01f
                ? throttle * EngineTorque(Rpm)
                : -EngineBraking(Rpm) * (wheelRpm > Spec.IdleRpm ? 1f : 0f);
            float t = engine * Ratio * Spec.DrivetrainEfficiency;
            // passenger-comfort limit of buses: at most MaxAcceleration from the drive
            if (Spec.MaxAcceleration > 0f && t > 0f) t = Math.Min(t, Spec.MaxAcceleration * Spec.Mass * Spec.WheelRadius);
            return Reverse ? -t : t;
        }

        /// <summary>Engine braking torque (Nm, positive) off throttle: friction and pumping losses.</summary>
        public float EngineBraking(float rpm) => (0.0045f * Math.Max(0f, rpm - Spec.IdleRpm) + 4f) * Math.Max(1f, Spec.MaxTorqueNm / 160f);

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
