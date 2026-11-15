using System;
using System.Collections.Generic;
using CoupledSim.Sound;
using UnityEngine;

/// <summary>
/// The simulator's sounds, made from code (no recordings), each normalised to a known RMS so that its level can be
/// set in dB SPL (<see cref="Soundscape.Gain"/>): noise shaped to octave-band spectra (vehicle rolling and propulsion
/// noise by CNOSSOS-EU, rain, distant traffic), engine harmonics at a firing frequency, an electric motor's whine,
/// and short one-shots (footsteps on hard, soft and loose ground, the tick of a pedestrian signal, a bicycle's
/// freewheel) and bird calls.
/// </summary>
public static class SoundClips
{
    public const float Rms = 0.2f;
    static int Rate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
    static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

    static AudioClip Make(string name, float[] data, bool loop)
    {
        // normalise the RMS; loops are cross-faded so they repeat without a click
        int n = data.Length;
        if (loop)
        {
            int fade = Mathf.Min(n / 4, Rate / 20);
            var o = new float[n - fade];
            Array.Copy(data, fade, o, 0, n - 2 * fade);
            for (int i = 0; i < fade; i++) { float a = i / (float)fade; o[n - 2 * fade + i] = data[n - fade + i] * (1 - a) + data[i] * a; }
            data = o; n = o.Length;
        }
        double e = 0; for (int i = 0; i < n; i++) e += data[i] * data[i];
        float rms = (float)Math.Sqrt(e / Math.Max(1, n));
        float k = rms > 1e-9f ? Rms / rms : 0f;
        float peak = 0f;
        for (int i = 0; i < n; i++) { data[i] *= k; peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
        if (peak > 0.98f) for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.98f;   // (very peaky one-shots)
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Cached(string key, Func<AudioClip> make)
    {
        if (!_cache.TryGetValue(key, out var c) || c == null) _cache[key] = c = make();
        return c;
    }

    // ---- filters

    sealed class Biquad
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;
        public static Biquad BandPass(float f, float q, int rate)
        {
            float w = 2f * Mathf.PI * f / rate, alpha = Mathf.Sin(w) / (2f * q), a0 = 1 + alpha;
            return new Biquad { b0 = alpha / a0, b1 = 0, b2 = -alpha / a0, a1 = -2 * Mathf.Cos(w) / a0, a2 = (1 - alpha) / a0 };
        }
        public static Biquad LowPass(float f, float q, int rate)
        {
            float w = 2f * Mathf.PI * Mathf.Min(f, rate * 0.45f) / rate, alpha = Mathf.Sin(w) / (2f * q), c = Mathf.Cos(w), a0 = 1 + alpha;
            return new Biquad { b0 = (1 - c) / 2 / a0, b1 = (1 - c) / a0, b2 = (1 - c) / 2 / a0, a1 = -2 * c / a0, a2 = (1 - alpha) / a0 };
        }
        public float Process(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    /// <summary>Noise with the given octave-band levels (dB, 63 Hz - 8 kHz; only the differences matter).</summary>
    public static float[] OctaveNoise(float seconds, float[] bandDb, int seed)
    {
        int rate = Rate, n = Mathf.RoundToInt(seconds * rate);
        var rnd = new System.Random(seed);
        var white = new float[n];
        for (int i = 0; i < n; i++) white[i] = (float)(rnd.NextDouble() * 2 - 1);
        var o = new float[n];
        float max = 0f; foreach (var d in bandDb) max = Mathf.Max(max, d);
        for (int b = 0; b < Cnossos.Bands.Length && b < bandDb.Length; b++)
        {
            float f = Cnossos.Bands[b];
            if (f > rate * 0.45f) continue;
            float g = Mathf.Pow(10f, (bandDb[b] - max) / 20f) * Mathf.Sqrt(f / 1000f);   // a band-pass of constant Q passes energy in proportion to its width
            var bp = Biquad.BandPass(f, 1.41f, rate); var bp2 = Biquad.BandPass(f, 1.41f, rate);
            for (int i = 0; i < n; i++) o[i] += g * bp2.Process(bp.Process(white[i]));
        }
        return o;
    }

    // ---- vehicles

    static float[] RollingSpectrum(VehicleCategory c, float kmh) { var s = new float[8]; for (int i = 0; i < 8; i++) s[i] = Cnossos.Rolling(c, kmh, i); return s; }
    static float[] PropulsionSpectrum(VehicleCategory c, float kmh, bool electric) { var s = new float[8]; for (int i = 0; i < 8; i++) s[i] = Cnossos.Propulsion(c, kmh, i, electric); return s; }

    /// <summary>Tyre/road noise with the spectrum of the category at <paramref name="kmh"/> (cars: 30 and 90 km/h are cross-faded).</summary>
    public static AudioClip Rolling(VehicleCategory c, float kmh) => Cached($"rolling {c} {kmh}", () => Make($"rolling {c} {kmh}", OctaveNoise(3f, RollingSpectrum(c, kmh), 17 + (int)c * 3 + (int)kmh), true));

    /// <summary>The broadband part of propulsion noise (intake, exhaust, mechanical).</summary>
    public static AudioClip PropulsionNoise(VehicleCategory c, bool electric) => Cached($"propulsion {c} {electric}", () => Make($"propulsion {c}", OctaveNoise(3f, PropulsionSpectrum(c, 50f, electric), 31 + (int)c), true));

    /// <summary>Engine harmonics at a firing frequency of <see cref="EngineBaseHz"/> (played with pitch = firing / base):
    /// petrol (smooth, few harmonics), diesel (rougher, with combustion knock), two-stroke (buzzy, many harmonics).</summary>
    public static AudioClip Engine(string kind) => Cached("engine " + kind, () =>
    {
        int rate = Rate; float f0 = EngineBaseHz;
        float seconds = 2f; int n = Mathf.RoundToInt(seconds * rate);
        var d = new float[n];
        var rnd = new System.Random(kind.GetHashCode());
        int harmonics = kind == "twostroke" ? 14 : kind == "diesel" ? 10 : 6;
        float tilt = kind == "twostroke" ? 0.85f : kind == "diesel" ? 0.75f : 0.6f;
        var phase = new float[harmonics * 2 + 2];
        for (int h = 0; h < phase.Length; h++) phase[h] = (float)rnd.NextDouble() * 6.28f;
        var lp = Biquad.LowPass(kind == "diesel" ? 2500f : 4000f, 0.7f, rate);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate, v = 0f;
            for (int h = 1; h <= harmonics; h++) v += Mathf.Pow(tilt, h - 1) * Mathf.Sin(2f * Mathf.PI * f0 * h * t + phase[h]);
            // half orders: cylinders that do not fire quite alike
            for (int h = 1; h <= 3; h++) v += 0.12f * Mathf.Pow(tilt, h) * Mathf.Sin(2f * Mathf.PI * f0 * (h - 0.5f) * t + phase[harmonics + h]);
            // combustion noise: bursts of noise at each firing (diesel knock is strong)
            float firingPhase = Mathf.Repeat(f0 * t, 1f);
            float burst = Mathf.Exp(-firingPhase * (kind == "diesel" ? 9f : 16f));
            v += (kind == "diesel" ? 0.9f : kind == "twostroke" ? 0.5f : 0.25f) * burst * (float)(rnd.NextDouble() * 2 - 1);
            d[i] = lp.Process(v);
        }
        return Make("engine " + kind, d, true);
    });
    public const float EngineBaseHz = 40f;

    /// <summary>An electric motor's and inverter's whine (played with pitch proportional to speed).</summary>
    public static AudioClip Whine() => Cached("whine", () => Make("whine", Tone(2f, t => Mathf.Sin(2f * Mathf.PI * 700f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 1400f * t + 0.6f) + 0.15f * Mathf.Sin(2f * Mathf.PI * 4200f * t)), true));

    static float[] Tone(float seconds, Func<float, float> f)
    {
        int rate = Rate, n = Mathf.RoundToInt(seconds * rate);
        var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = f(i / (float)rate);
        return d;
    }

    // ---- environment

    /// <summary>Road traffic far away (behind buildings): the spectrum of a car at 50 km/h with the highs lost on the way.</summary>
    public static AudioClip DistantTraffic() => Cached("distant traffic", () =>
    {
        var s = new float[8];
        float[] loss = { 0f, -2f, -5f, -9f, -14f, -20f, -28f, -38f };
        for (int i = 0; i < 8; i++) s[i] = Cnossos.Sum(Cnossos.Rolling(VehicleCategory.Light, 50f, i), Cnossos.Propulsion(VehicleCategory.Light, 50f, i)) + loss[i];
        return Make("distant traffic", OctaveNoise(6f, s, 5), true);
    });

    /// <summary>Rain: broadband hiss with the energy in the highs, and single drops.</summary>
    public static AudioClip Rain(bool onRoof) => Cached("rain " + onRoof, () =>
    {
        var spectrum = onRoof ? new[] { -6f, -3f, -1f, 0f, -2f, -6f, -12f, -20f } : new[] { -24f, -18f, -12f, -7f, -3f, 0f, 0f, -4f };
        var d = OctaveNoise(4f, spectrum, onRoof ? 41 : 43);
        var rnd = new System.Random(onRoof ? 7 : 9);
        int rate = Rate;
        for (int k = 0; k < (onRoof ? 900 : 500); k++)
        {
            int at = rnd.Next(d.Length - rate / 20); float f = onRoof ? 400f + (float)rnd.NextDouble() * 800f : 2500f + (float)rnd.NextDouble() * 5000f;
            float a = 0.5f + (float)rnd.NextDouble();
            for (int i = 0; i < rate / 40; i++) d[at + i] += a * 0.6f * Mathf.Exp(-i / (rate * 0.003f)) * Mathf.Sin(2f * Mathf.PI * f * i / rate);
        }
        return Make("rain", d, true);
    });

    /// <summary>Wind at the ears: low rumble with the gusts left to the volume.</summary>
    public static AudioClip Wind() => Cached("wind", () => Make("wind", OctaveNoise(5f, new[] { 0f, -2f, -6f, -12f, -18f, -26f, -34f, -40f }, 61), true));

    /// <summary>A few seconds of bird song: whistles, trills and chirps of different kinds with pauses between.</summary>
    public static AudioClip Birds(int variant) => Cached("birds " + variant, () =>
    {
        int rate = Rate; float seconds = 9f; int n = Mathf.RoundToInt(seconds * rate);
        var d = new float[n];
        var rnd = new System.Random(101 + variant * 13);
        float t0 = (float)rnd.NextDouble() * 0.5f;
        while (t0 < seconds - 1.2f)
        {
            int kind = rnd.Next(3);
            int notes = kind == 1 ? 6 + rnd.Next(10) : 1 + rnd.Next(4);
            float f1 = 2200f + (float)rnd.NextDouble() * 3500f, f2 = f1 * (0.7f + (float)rnd.NextDouble() * 0.7f);
            float len = kind == 1 ? 0.04f : 0.08f + (float)rnd.NextDouble() * 0.25f, gap = kind == 1 ? 0.025f : 0.06f;
            for (int k = 0; k < notes; k++)
            {
                int start = Mathf.RoundToInt((t0 + k * (len + gap)) * rate), m = Mathf.RoundToInt(len * rate);
                double ph = 0;
                for (int i = 0; i < m && start + i < n; i++)
                {
                    float u = i / (float)m;
                    float f = kind == 2 ? f1 + (f2 - f1) * u + 300f * Mathf.Sin(u * 30f) : f1 + (f2 - f1) * u;   // whistle, trill, warble
                    ph += 2 * Math.PI * f / rate;
                    d[start + i] += Mathf.Sin(u * Mathf.PI) * (float)Math.Sin(ph) * (kind == 1 ? 0.7f : 1f);
                }
            }
            t0 += notes * (len + gap) + 0.4f + (float)rnd.NextDouble() * 1.8f;
        }
        return Make("birds", d, true);
    });

    // ---- one-shots

    /// <summary>A step: heel and toe on hard ground (paving, asphalt), soft ground (grass) or loose ground (gravel, snow).</summary>
    public static AudioClip Footstep(string surface, int variant) => Cached($"step {surface} {variant}", () =>
    {
        int rate = Rate, n = rate / 4;
        var d = new float[n];
        var rnd = new System.Random(surface.GetHashCode() * 7 + variant);
        var lp = Biquad.LowPass(surface == "hard" ? 6000f : surface == "soft" ? 900f : 3500f, 0.8f, rate);
        float decay = surface == "hard" ? 0.012f : surface == "soft" ? 0.05f : 0.035f;
        for (int part = 0; part < 2; part++)   // heel, then toe
        {
            int at = part == 0 ? 0 : Mathf.RoundToInt((0.06f + (float)rnd.NextDouble() * 0.03f) * rate);
            float a = part == 0 ? 1f : 0.55f;
            for (int i = 0; at + i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t / decay);
                float grain = surface == "loose" && rnd.NextDouble() < 0.15 ? 2.5f : 1f;   // crunching stones
                d[at + i] += a * env * grain * (float)(rnd.NextDouble() * 2 - 1) + (surface == "hard" ? a * 0.6f * env * Mathf.Sin(2f * Mathf.PI * 120f * t) : 0f);
            }
        }
        for (int i = 0; i < n; i++) d[i] = lp.Process(d[i]);
        return Make("step", d, false);
    });

    /// <summary>The tick of an accessible pedestrian signal (as on Dutch "rateltikkers").</summary>
    public static AudioClip Tick() => Cached("tick", () =>
    {
        int rate = Rate, n = rate / 25;
        var d = new float[n];
        var rnd = new System.Random(3);
        for (int i = 0; i < n; i++) { float t = i / (float)rate; d[i] = Mathf.Exp(-t / 0.004f) * (0.8f * Mathf.Sin(2f * Mathf.PI * 1800f * t) + 0.5f * (float)(rnd.NextDouble() * 2 - 1)); }
        return Make("tick", d, false);
    });

    /// <summary>One click of a freewheeling bicycle hub.</summary>
    public static AudioClip Freewheel() => Cached("freewheel", () =>
    {
        int rate = Rate, n = rate / 100;
        var d = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)rate; d[i] = Mathf.Exp(-t / 0.0015f) * Mathf.Sin(2f * Mathf.PI * 5200f * t); }
        return Make("freewheel", d, false);
    });
}
