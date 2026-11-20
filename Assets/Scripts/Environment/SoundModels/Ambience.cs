using System;

namespace CoupledSim.Sound
{
    /// <summary>
    /// Levels of the ambience: the wind at the listener's ears and the distant traffic by the hour.
    /// </summary>
    public static class Ambience
    {
        /// <summary>
        /// Mean wind at head height in a built-up area as a share of the weather's wind (at 10 m over open country):
        /// the roughness factor c_r of EN 1991-1-4:2005 (4.3.2) for terrain category IV (towns, z0 = 1 m), which takes
        /// the wind below z_min = 10 m as at z_min: c_r = 0.19 (z0 / 0.05 m)^0.07 ln(z_min / z0) = 0.54. (In a street
        /// between buildings it is often lower still.)
        /// </summary>
        public static readonly float StreetWindFactor = (float)(0.19 * Math.Pow(1.0 / 0.05, 0.07) * Math.Log(10.0 / 1.0));

        /// <summary>
        /// Speed of the air past the ears (m/s): the wind in the street (<paramref name="wind10m"/>, the weather's wind
        /// at 10 m, times <paramref name="gust"/> and <see cref="StreetWindFactor"/>) and the listener's own speed
        /// (walking or riding; 0 in a car), added as at right angles (the wind's direction to the listener's is not known
        /// to the sound).
        /// </summary>
        public static float AirSpeedAtEars(float wind10m, float gust, float ownSpeed)
        {
            float w = Math.Max(wind10m, 0f) * StreetWindFactor * Math.Max(gust, 0f);
            float o = Math.Max(ownSpeed, 0f);
            return (float)Math.Sqrt(w * w + o * o);
        }

        /// <summary>
        /// Unweighted level (dB SPL, 2D) of the wind noise at the ears at an air speed of <paramref name="metresPerSecond"/>:
        /// 35 + 45 log10(v). The slope is that of wind noise measured at a cyclist's ears in a wind tunnel (84.9 dB at
        /// 10 mph to 120.3 dB at 60 mph: 45 dB per decade; Seidman et al. 2017, Otolaryngology-Head and Neck Surgery
        /// 157(3), 448-452). The level is an estimate, 20 dB below theirs: a microphone in the flow records its own
        /// wind noise, far louder than what a listener hears (hearing-aid microphones: up to 85 dB at 3 m/s, Zakis 2011,
        /// JASA 129(6), 3897-3907). So wind is a soft rumble for a cyclist at 20 km/h (about 69 dB, nine tenths of it
        /// below 250 Hz: about 55 dB(A)) and nearly inaudible for a pedestrian in a light breeze (below 50 dB, about
        /// 35 dB(A), under the distant traffic). No step at a threshold: it fades out smoothly at low speeds.
        /// </summary>
        public static float WindNoiseDb(float metresPerSecond) => 35f + 45f * (float)Math.Log10(Math.Max(metresPerSecond, 0.01f));

        /// <summary>
        /// Distant traffic (the background, A-weighted, dB(A)) by the hour: the residual sound of a city where no
        /// vehicle is near, i.e. the L_A90. Measured in a residential area (King et al. 2012, Noise levels associated
        /// with urban land use, Journal of Urban Health 89(6), 1017-1030, table 2): L_A90 45.4 dB(A) in the morning,
        /// 43.9 in the afternoon, 41.6 in the evening and 41.7 at night; city centres a dB or two louder by day, and the
        /// small hours quieter (traffic at 3-4 am is about a tenth of the daytime's, 10 dB less). The vehicles near by
        /// are modelled one by one on top of it.
        /// </summary>
        public static float DistantTrafficDbA(float hour)
        {
            hour = hour - 24f * (float)Math.Floor(hour / 24f);
            for (int i = 1; i < Hours.Length; i++)
                if (hour <= Hours[i]) return Levels[i - 1] + (Levels[i] - Levels[i - 1]) * (hour - Hours[i - 1]) / (Hours[i] - Hours[i - 1]);
            return Levels[0];
        }

        static readonly float[] Hours = { 0, 3, 5, 7, 8, 12, 17, 19, 22, 24 };
        static readonly float[] Levels = { 40, 37, 38, 44, 47, 45, 47, 44, 42, 40 };
    }
}
