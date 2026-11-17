using System;

namespace CoupledSim.Sound
{
    /// <summary>Vehicle categories of CNOSSOS-EU.</summary>
    public enum VehicleCategory
    {
        /// <summary>1: passenger cars and vans up to 3.5 t.</summary>
        Light = 1,
        /// <summary>2: medium heavy vehicles (delivery trucks, two axles).</summary>
        MediumHeavy = 2,
        /// <summary>3: heavy vehicles (tractor units, buses, three or more axles).</summary>
        Heavy = 3,
        /// <summary>4a: mopeds and scooters up to 50 cc.</summary>
        Moped = 4,
        /// <summary>4b: motorcycles over 50 cc.</summary>
        Motorcycle = 5,
    }

    /// <summary>
    /// Sound power of a road vehicle by the European road traffic noise model CNOSSOS-EU (Commission Directive (EU)
    /// 2015/996, Annex, Appendix F, Table F-1): per octave band 63 Hz - 8 kHz, rolling noise
    /// L_WR = A_R + B_R log10(v / 70 km/h) and propulsion noise L_WP = A_P + B_P (v - 70) / 70, added on an energy
    /// basis. Valid for 20-130 km/h (below 20 km/h the propulsion keeps its 20 km/h level and the rolling noise
    /// follows its law down to 5 km/h). Electric light vehicles: propulsion lowered by <see cref="ElectricPropulsionDb"/>
    /// (an assumption, in line with measurements of electric cars being a few dB quieter than combustion cars below
    /// 50 km/h, where propulsion matters; Pallas et al. 2016, Applied Acoustics 113).
    /// </summary>
    public static class Cnossos
    {
        public static readonly float[] Bands = { 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f };
        /// <summary>A-weighting of the bands (IEC 61672-1).</summary>
        public static readonly float[] AWeighting = { -26.2f, -16.1f, -8.6f, -3.2f, 0f, 1.2f, 1.0f, -1.1f };
        public const float ElectricPropulsionDb = -12f;
        const float VRef = 70f;

        // Table F-1: A_R, B_R, A_P, B_P for categories 1, 2, 3, 4a, 4b
        static readonly float[][] AR =
        {
            new[] { 79.7f, 85.7f, 84.5f, 90.2f, 97.3f, 93.9f, 84.1f, 74.3f },
            new[] { 84.0f, 88.7f, 91.5f, 96.7f, 97.4f, 90.9f, 83.8f, 80.5f },
            new[] { 87.0f, 91.7f, 94.1f, 100.7f, 100.8f, 94.3f, 87.1f, 82.5f },
            null, null,
        };
        static readonly float[][] BR =
        {
            new[] { 30.0f, 41.5f, 38.9f, 25.7f, 32.5f, 37.2f, 39.0f, 40.0f },
            new[] { 30.0f, 35.8f, 32.6f, 23.8f, 30.1f, 36.2f, 38.3f, 40.1f },
            new[] { 30.0f, 33.5f, 31.3f, 25.4f, 31.8f, 37.1f, 38.6f, 40.6f },
            null, null,
        };
        static readonly float[][] AP =
        {
            new[] { 94.5f, 89.2f, 88.0f, 85.9f, 84.2f, 86.9f, 83.3f, 76.1f },
            new[] { 101.0f, 96.5f, 98.8f, 96.8f, 98.6f, 95.2f, 88.8f, 82.7f },
            new[] { 104.4f, 100.6f, 101.7f, 101.0f, 100.1f, 95.9f, 91.3f, 85.3f },
            new[] { 88.0f, 87.5f, 89.5f, 93.7f, 96.6f, 98.8f, 93.9f, 88.7f },
            new[] { 95.0f, 97.2f, 92.7f, 92.9f, 94.7f, 93.2f, 90.1f, 86.5f },
        };
        static readonly float[][] BP =
        {
            new[] { -1.3f, 7.2f, 7.7f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f },
            new[] { -1.9f, 4.7f, 6.4f, 6.5f, 6.5f, 6.5f, 6.5f, 6.5f },
            new[] { 0.0f, 3.0f, 4.6f, 5.0f, 5.0f, 5.0f, 5.0f, 5.0f },
            new[] { 4.2f, 7.4f, 9.8f, 11.6f, 15.7f, 18.9f, 20.3f, 20.6f },
            new[] { 3.2f, 5.9f, 11.9f, 11.6f, 11.5f, 12.6f, 11.1f, 12.0f },
        };

        static int Index(VehicleCategory c) => (int)c - 1;

        /// <summary>Rolling noise sound power in band i (dB re 1 pW); -inf for two-wheelers (their rolling noise is in the propulsion term).</summary>
        public static float Rolling(VehicleCategory c, float kmh, int i, float surfaceDb = 0f)
        {
            var a = AR[Index(c)];
            if (a == null) return float.NegativeInfinity;
            float v = Math.Max(kmh, 5f);
            return a[i] + BR[Index(c)][i] * (float)Math.Log10(v / VRef) + surfaceDb;
        }

        /// <summary>Propulsion noise sound power in band i (dB re 1 pW).</summary>
        public static float Propulsion(VehicleCategory c, float kmh, int i, bool electric = false)
        {
            float v = Math.Max(kmh, 20f);
            float l = AP[Index(c)][i] + BP[Index(c)][i] * (v - VRef) / VRef;
            return electric && c == VehicleCategory.Light ? l + ElectricPropulsionDb : l;
        }

        public static float Sum(float a, float b) => 10f * (float)Math.Log10(Math.Pow(10, a / 10) + Math.Pow(10, b / 10));

        /// <summary>Total sound power over the bands, A-weighted or not (dB).</summary>
        public static float Total(Func<int, float> band, bool aWeighted)
        {
            double e = 0;
            for (int i = 0; i < Bands.Length; i++) e += Math.Pow(10, (band(i) + (aWeighted ? AWeighting[i] : 0f)) / 10);
            return 10f * (float)Math.Log10(Math.Max(e, 1e-30));
        }

        /// <summary>Total rolling sound power over all octave bands without delegate allocation.</summary>
        public static float TotalRolling(VehicleCategory c, float kmh, float surfaceDb = 0f, bool aWeighted = false)
        {
            if (AR[Index(c)] == null) return -300f;
            double e = 0;
            for (int i = 0; i < Bands.Length; i++) e += Math.Pow(10, (Rolling(c, kmh, i, surfaceDb) + (aWeighted ? AWeighting[i] : 0f)) / 10);
            return 10f * (float)Math.Log10(Math.Max(e, 1e-30));
        }

        /// <summary>Total propulsion sound power over all octave bands without delegate allocation.</summary>
        public static float TotalPropulsion(VehicleCategory c, float kmh, bool electric = false, bool aWeighted = false)
        {
            double e = 0;
            for (int i = 0; i < Bands.Length; i++) e += Math.Pow(10, (Propulsion(c, kmh, i, electric) + (aWeighted ? AWeighting[i] : 0f)) / 10);
            return 10f * (float)Math.Log10(Math.Max(e, 1e-30));
        }

        /// <summary>Sound pressure level at <paramref name="metres"/> from a point source of sound power
        /// <paramref name="lw"/> on reflecting ground (hemispherical spreading): L_p = L_W - 10 log10(2 pi r^2).</summary>
        public static float PressureAt(float lw, float metres)
        {
            float r = Math.Max(metres, 0.01f);
            return lw - 10f * (float)Math.Log10(2 * Math.PI * r * r);
        }
    }

    /// <summary>
    /// Engine speed of a vehicle from its speed through its gears (automatic shifting between rev limits that rise
    /// with the throttle), and the firing frequency of the engine (a four-stroke engine fires each cylinder every
    /// second revolution, a two-stroke every revolution).
    /// </summary>
    public sealed class EngineModel
    {
        public float IdleRpm, ShiftUpRpm, ShiftDownRpm, MaxRpm, WheelRadius, FinalDrive;
        public float[] Gears;
        public int Cylinders;
        public bool TwoStroke;
        public int Gear { get; private set; } = 1;
        public float Rpm { get; private set; }

        public static EngineModel For(VehicleCategory c) => c switch
        {
            // a petrol four-cylinder with a five-speed gearbox
            VehicleCategory.Light => new EngineModel { IdleRpm = 800, ShiftUpRpm = 2600, ShiftDownRpm = 1300, MaxRpm = 6000, WheelRadius = 0.31f, FinalDrive = 3.9f, Gears = new[] { 3.6f, 2.1f, 1.4f, 1.0f, 0.8f }, Cylinders = 4 },
            // a four-cylinder diesel with six gears
            VehicleCategory.MediumHeavy => new EngineModel { IdleRpm = 700, ShiftUpRpm = 2100, ShiftDownRpm = 1100, MaxRpm = 3000, WheelRadius = 0.4f, FinalDrive = 4.6f, Gears = new[] { 6.0f, 3.5f, 2.2f, 1.5f, 1.0f, 0.8f }, Cylinders = 4 },
            // a six-cylinder diesel with an automatic gearbox (bus) or twelve gears (tractor unit): six steps here
            VehicleCategory.Heavy => new EngineModel { IdleRpm = 600, ShiftUpRpm = 1700, ShiftDownRpm = 1000, MaxRpm = 2200, WheelRadius = 0.5f, FinalDrive = 4.9f, Gears = new[] { 5.0f, 3.0f, 2.0f, 1.4f, 1.0f, 0.75f }, Cylinders = 6 },
            // a single-cylinder two-stroke with a variator (scooter): the engine runs up and stays high
            VehicleCategory.Moped => new EngineModel { IdleRpm = 1800, ShiftUpRpm = 7500, ShiftDownRpm = 5500, MaxRpm = 8500, WheelRadius = 0.23f, FinalDrive = 11f, Gears = new[] { 1.4f }, Cylinders = 1, TwoStroke = true },
            _ => new EngineModel { IdleRpm = 1200, ShiftUpRpm = 6000, ShiftDownRpm = 3500, MaxRpm = 9000, WheelRadius = 0.3f, FinalDrive = 3f, Gears = new[] { 2.8f, 2.0f, 1.6f, 1.3f, 1.1f, 0.95f }, Cylinders = 2 },
        };

        /// <summary>Updates the gear and engine speed; <paramref name="throttle"/> 0..1 (accelerating raises the shift points).</summary>
        public float Update(float kmh, float throttle)
        {
            float wheelRpm = Math.Max(kmh, 0f) / 3.6f / (2f * (float)Math.PI * WheelRadius) * 60f;
            float up = ShiftUpRpm + throttle * (MaxRpm - ShiftUpRpm) * 0.35f, down = ShiftDownRpm + throttle * 300f;
            if (Gears.Length == 1)
            {
                // variator: the engine speed rises with the throttle, then the ratio follows the speed
                float target = IdleRpm + throttle * (ShiftUpRpm - IdleRpm);
                Rpm = Math.Max(Math.Max(IdleRpm, wheelRpm * Gears[0] * FinalDrive), kmh > 3f ? target : IdleRpm);
                return Rpm;
            }
            int g = Math.Max(1, Math.Min(Gear, Gears.Length));
            float rpm = wheelRpm * Gears[g - 1] * FinalDrive;
            while (rpm > up && g < Gears.Length) { g++; rpm = wheelRpm * Gears[g - 1] * FinalDrive; }
            while (rpm < down && g > 1) { g--; rpm = wheelRpm * Gears[g - 1] * FinalDrive; }
            Gear = g;
            // pulling away: the clutch slips (or the torque converter) and keeps the engine above idle
            Rpm = Math.Min(MaxRpm, Math.Max(rpm, IdleRpm + throttle * 900f * Math.Max(0f, 1f - kmh / 15f)));
            return Rpm;
        }

        /// <summary>Firing frequency (Hz) at the current engine speed.</summary>
        public float FiringHz => Rpm / 60f * Cylinders / (TwoStroke ? 1f : 2f);
    }

    /// <summary>
    /// Atmospheric absorption of sound (ISO 9613-1, 20 °C, 70 % relative humidity): the attenuation coefficient per
    /// octave band, and the frequency above which a path of a given length loses more than 3 dB (the cut-off of a
    /// low-pass filter that stands for it).
    /// </summary>
    public static class AirAbsorption
    {
        /// <summary>dB per km in the bands 63 Hz - 8 kHz.</summary>
        public static readonly float[] DbPerKm = { 0.1f, 0.4f, 1.0f, 1.9f, 3.7f, 9.7f, 32.8f, 117f };

        public static float Cutoff(float metres, float lossDb = 3f)
        {
            if (metres <= 0f) return 22000f;
            float need = lossDb / (metres / 1000f);   // dB/km at the cut-off
            var b = Cnossos.Bands;
            // above 8 kHz the absorption grows about with the square of the frequency
            if (need >= DbPerKm[7]) return Math.Min(22000f, b[7] * (float)Math.Sqrt(need / DbPerKm[7]));
            for (int i = 1; i < b.Length; i++)
                if (need < DbPerKm[i])
                {
                    // log-log interpolation between the bands
                    double t = Math.Log(need / DbPerKm[i - 1]) / Math.Log(DbPerKm[i] / DbPerKm[i - 1]);
                    return (float)Math.Exp(Math.Log(b[i - 1]) + t * Math.Log(b[i] / b[i - 1]));
                }
            return b[0];
        }
    }
}
