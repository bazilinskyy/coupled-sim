using System;
using System.Collections.Generic;
using CoupledSim.Sound;
using UnityEngine;

/// <summary>
/// The simulator's sounds. Where a recording is in <c>Assets/Resources/Sounds</c> (CC0 recordings from Freesound, see
/// CREDITS.md there) it is used (<see cref="Rec"/>): engines (idle and on load, crossfaded and pitched by the engine
/// speed), the inside of a car on asphalt and on cobblestones, a bicycle's tyres and freewheel, footsteps on hard,
/// loose and soft ground, birds and distant city traffic. The rest is made from code, and so is each of these when its
/// recording is missing (as in tests): noise shaped to octave-band spectra (vehicle rolling and propulsion noise by
/// CNOSSOS-EU, rain, wind, distant traffic), engine harmonics at a firing frequency, an electric motor's whine, and
/// short one-shots (footsteps, the tick of a pedestrian signal, a freewheel click), bird calls and drone rotors. Every sound is
/// normalised to a known RMS (<see cref="Rms"/>, recordings through <see cref="Recording.Factor"/>) so that its level
/// can be set in dB SPL (<see cref="Soundscape.ClipGain"/>).
/// </summary>
public static class SoundClips
{
    public const float Rms = 0.2f;
    static int Rate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
    static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

    static AudioClip Make(string name, float[] data, bool loop)
    {
        // normalise the RMS; loops are cross-faded so they repeat without a click (noise made by OctaveNoise
        // repeats seamlessly by itself and needs no cross-fade)
        int n = data.Length;
        if (!_seamless.Remove(data) && loop)
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

    static readonly HashSet<float[]> _seamless = new HashSet<float[]>();

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

    /// <summary>Noise with the given octave-band levels (dB, 63 Hz - 8 kHz; only the differences matter), shaped in
    /// the frequency domain: the level per octave is interpolated smoothly between the band centres (no peaks at the
    /// centres), and the noise repeats seamlessly (its length is a power of two of at least <paramref name="seconds"/>).</summary>
    public static float[] OctaveNoise(float seconds, float[] bandDb, int seed)
    {
        int rate = Rate, n = 1;
        while (n < seconds * rate) n <<= 1;
        var re = new double[n]; var im = new double[n];
        var rnd = new System.Random(seed);
        float max = float.NegativeInfinity; foreach (var d in bandDb) max = Mathf.Max(max, d);
        for (int k = 1; k < n / 2; k++)
        {
            float f = k * (float)rate / n;
            // energy per octave -> energy per Hz (an octave at f is 0.71 f wide); Gaussian noise: Rayleigh magnitude, random phase
            double psd = Math.Pow(10, (BandDbAt(bandDb, f) - max) / 10) / (0.71 * f);
            double mag = Math.Sqrt(psd) * Math.Sqrt(-2 * Math.Log(1 - rnd.NextDouble())), ph = rnd.NextDouble() * 2 * Math.PI;
            re[k] = mag * Math.Cos(ph); im[k] = mag * Math.Sin(ph);
            re[n - k] = re[k]; im[n - k] = -im[k];
        }
        Fft(re, im, true);
        var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = (float)re[i];
        _seamless.Add(o);
        return o;
    }

    /// <summary>Level at <paramref name="f"/> Hz of a spectrum given in octave bands: linear in log frequency between the
    /// band centres; below 63 Hz falling 6 dB per octave, above 8 kHz 12 dB per octave.</summary>
    static float BandDbAt(float[] bandDb, float f)
    {
        var c = Cnossos.Bands;
        int m = Mathf.Min(c.Length, bandDb.Length);
        float o = Mathf.Log(f / c[0], 2f);
        if (o <= 0f) return bandDb[0] + 6f * o;
        if (o >= m - 1) return bandDb[m - 1] - 12f * (o - (m - 1));
        int i = Mathf.FloorToInt(o);
        return Mathf.Lerp(bandDb[i], bandDb[i + 1], o - i);
    }

    /// <summary>In-place radix-2 FFT (length a power of two); <paramref name="inverse"/> without the 1/n scaling.</summary>
    static void Fft(double[] re, double[] im, bool inverse)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = 2 * Math.PI / len * (inverse ? 1 : -1), wr = Math.Cos(ang), wi = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = a + len / 2;
                    double xr = re[b] * cr - im[b] * ci, xi = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - xr; im[b] = im[a] - xi; re[a] += xr; im[a] += xi;
                    double t = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = t;
                }
            }
        }
    }

    // ---- recordings

    /// <summary>A recording in Resources/Sounds with what is needed to play it at a level in dB SPL.</summary>
    public sealed class Recording
    {
        public string Name;
        public AudioClip Clip;
        /// <summary>Gain that brings the recording to the RMS of the synthesised sounds (<see cref="Rms"/>): play it at <c>ClipGain(dB) * Factor</c>.</summary>
        public float Factor = 1f;
        /// <summary>Its A-weighted level minus its unweighted level (dB), to match it to a model's A-weighted level.</summary>
        public float AWeightDb;
    }

    static readonly Dictionary<string, Recording> _recs = new Dictionary<string, Recording>();

    /// <summary>The recording Resources/Sounds/<paramref name="name"/>, or null when it is not there (then the
    /// synthesised sound is used).</summary>
    public static Recording Rec(string name)
    {
        if (_recs.TryGetValue(name, out var r) && (r == null || r.Clip != null)) return r;
        var clip = Resources.Load<AudioClip>("Sounds/" + name);
        r = clip == null ? null : Measure(name, clip);
        _recs[name] = r;
        return r;
    }

    static Recording Measure(string name, AudioClip clip)
    {
        var r = new Recording { Name = name, Clip = clip };
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        int ch = Mathf.Max(1, clip.channels), n = clip.samples;
        var data = new float[n * ch];
        if (n == 0 || !clip.GetData(data, 0)) { Debug.LogWarning($"SoundClips: cannot read {name}; it plays unnormalised"); return r; }
        if (ch > 1) { var m = new float[n]; for (int i = 0; i < n; i++) { float v = 0; for (int c = 0; c < ch; c++) v += data[i * ch + c]; m[i] = v / ch; } data = m; }
        double e = 0; foreach (var v in data) e += v * v;
        e /= n;
        if (e < 1e-14) return r;
        r.Factor = Rms / (float)Math.Sqrt(e);
        // A-weighting from the octave bands (the first 8 s)
        int len = Mathf.Min(n, clip.frequency * 8);
        double ea = 0;
        for (int b = 0; b < Cnossos.Bands.Length; b++)
        {
            float f = Cnossos.Bands[b];
            if (f > clip.frequency * 0.45f) continue;
            var bp = Biquad.BandPass(f, 1.41f, clip.frequency); var bp2 = Biquad.BandPass(f, 1.41f, clip.frequency);
            double eb = 0;
            for (int i = 0; i < len; i++) { float y = bp2.Process(bp.Process(data[i])); eb += y * y; }
            ea += eb / len * Math.Pow(10, Cnossos.AWeighting[b] / 10);
        }
        r.AWeightDb = (float)(10 * Math.Log10(Math.Max(ea, 1e-20) / e));
        return r;
    }

    /// <summary>Engine recordings to crossfade by the engine's firing frequency: each with the firing frequency it was
    /// recorded at (from its spectrum); null when one is missing. Petrol: a Peugeot 308 at idle (750 rpm, four
    /// cylinders) and a 2.5-litre four-cylinder at 2000 and 2940 rpm; diesel: a coach idling (six cylinders, 600 rpm).</summary>
    public static (Recording rec, float firingHz)[] EngineRecordings(string kind)
    {
        var spec = kind == "petrol" ? new[] { ("engine_petrol_idle", 25f), ("engine_petrol_mid", 66.7f), ("engine_petrol_high", 98f) }
            : kind == "diesel" ? new[] { ("engine_diesel_idle", 30f) } : null;
        if (spec == null) return null;
        var o = new (Recording, float)[spec.Length];
        for (int i = 0; i < spec.Length; i++) { var r = Rec(spec[i].Item1); if (r == null) return null; o[i] = (r, spec[i].Item2); }
        return o;
    }

    /// <summary>A recorded step on "hard", "loose" or "soft" ground (4 variants), or null.</summary>
    public static Recording FootstepRecording(string surface, int variant) => Rec($"step_{surface}_{variant & 3}");

    /// <summary>Recorded bird song (a blackbird, house sparrows), or null.</summary>
    public static Recording BirdRecording(int variant) => Rec(variant % 2 == 0 ? "birds_blackbird" : "birds_sparrows");

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

    // ---- drones

    // a loop of 2^16 samples; every tone a whole number of periods in it (so it repeats seamlessly): four rotors about
    // 3 % apart around the blade-passing frequency DroneBaseHz (about 200 Hz at 48 kHz, a consumer drone in hover)
    const int DroneLength = 1 << 16, DroneBaseBin = 273;
    static readonly int[] DroneRotorBins = { 265, 269, 277, 281 };
    static Recording _droneRotors;
    static double[] _droneBands;   // its energy in sixth-octave bands from 16 Hz

    /// <summary>Blade-passing frequency (Hz) of <see cref="DroneRotors"/> at pitch 1.</summary>
    public static float DroneBaseHz => DroneBaseBin * (float)Rate / DroneLength;

    /// <summary>A multirotor's rotors (played with pitch = blade-passing frequency / <see cref="DroneBaseHz"/>): the
    /// blade-passing tone of each of four rotors with its harmonics (falling as 1/order), the rotors a few per cent apart
    /// in speed as those of a flying multirotor are (so the tones beat instead of one pure buzz), 6 dB above broadband
    /// rotor noise that peaks around 500 Hz-1 kHz. Its A-weighting at the pitch it is played at is
    /// <see cref="DroneAWeightDb"/>.</summary>
    public static Recording DroneRotors()
    {
        if (_droneRotors != null && _droneRotors.Clip != null) return _droneRotors;
        int n = DroneLength;
        var d = OctaveNoise((n - 0.5f) / Rate, new[] { -14f, -10f, -7f, -5f, -5f, -7f, -11f, -17f }, 77);
        double eb = 0; for (int i = 0; i < n; i++) eb += d[i] * d[i];
        var tone = new double[n];
        var rnd = new System.Random(11);
        foreach (int m in DroneRotorBins)
            for (int h = 1; h <= 10; h++)
            {
                double ph = rnd.NextDouble() * 2 * Math.PI, w = 2 * Math.PI * m * h / n;
                for (int i = 0; i < n; i++) tone[i] += Math.Sin(w * i + ph) / h;
            }
        double et = 0; for (int i = 0; i < n; i++) et += tone[i] * tone[i];
        double k = Math.Sqrt(eb / Math.Max(et, 1e-12) * Math.Pow(10, 0.6));
        for (int i = 0; i < n; i++) d[i] += (float)(k * tone[i]);
        _droneRotors = Measure("drone rotors", Make("drone rotors", d, true));
        // (Make normalised d in place) its spectrum in sixth-octave bands
        var re = new double[n]; var im = new double[n];
        for (int i = 0; i < n; i++) re[i] = d[i];
        Fft(re, im, false);
        _droneBands = new double[61];
        for (int b = 1; b < n / 2; b++)
        {
            int j = Mathf.RoundToInt(6f * Mathf.Log(b * (float)Rate / n / 16f, 2f));
            if (j >= 0 && j < _droneBands.Length) _droneBands[j] += re[b] * re[b] + im[b] * im[b];
        }
        return _droneRotors;
    }

    /// <summary>A-weighted minus unweighted level (dB) of <see cref="DroneRotors"/> played at <paramref name="pitch"/>
    /// (its tones are low, where the A-weighting changes fast with frequency).</summary>
    public static float DroneAWeightDb(float pitch)
    {
        DroneRotors();
        double e = 0, ea = 0;
        for (int j = 0; j < _droneBands.Length; j++) { e += _droneBands[j]; ea += _droneBands[j] * Math.Pow(10, AWeighting(16f * Mathf.Pow(2f, j / 6f) * pitch) / 10); }
        return e > 0 ? (float)(10 * Math.Log10(Math.Max(ea, 1e-30) / e)) : 0f;
    }

    /// <summary>The A-weighting (dB) at <paramref name="f"/> Hz (IEC 61672-1).</summary>
    public static float AWeighting(float f)
    {
        double f2 = (double)f * f;
        double ra = 12194.0 * 12194.0 * f2 * f2 / ((f2 + 20.6 * 20.6) * Math.Sqrt((f2 + 107.7 * 107.7) * (f2 + 737.9 * 737.9)) * (f2 + 12194.0 * 12194.0));
        return (float)(20 * Math.Log10(Math.Max(ra, 1e-20)) + 2.0);
    }

    // ---- environment

    /// <summary>Road traffic far away (behind buildings): the spectrum of a car at 50 km/h with the highs lost on the way.</summary>
    public static AudioClip DistantTraffic() => Cached("distant traffic", () => Make("distant traffic", OctaveNoise(6f, DistantTrafficSpectrum(), 5), true));

    static float[] DistantTrafficSpectrum()
    {
        var s = new float[8];
        float[] loss = { 0f, -2f, -5f, -9f, -14f, -20f, -28f, -38f };
        for (int i = 0; i < 8; i++) s[i] = Cnossos.Sum(Cnossos.Rolling(VehicleCategory.Light, 50f, i), Cnossos.Propulsion(VehicleCategory.Light, 50f, i)) + loss[i];
        return s;
    }

    /// <summary>A-weighted minus unweighted level of <see cref="DistantTraffic"/> (dB), to play it at an A-weighted level.</summary>
    public static float DistantTrafficAShift => _distantAShift ??= Soundscape.AShift(i => DistantTrafficSpectrum()[i]);
    static float? _distantAShift;

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
