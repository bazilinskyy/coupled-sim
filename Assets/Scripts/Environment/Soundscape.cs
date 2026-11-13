using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sound of the scene with levels in dB SPL (after calibration):
/// - vehicle sound: tyre-road noise of every car that rises with speed (about 30 log10 v, as in
///   road traffic noise models), plus an engine for combustion cars (pitch and level with speed);
///   3D sound with the inverse-distance law (-6 dB per doubling), for the nearest cars;
/// - the participant's own car: the same sounds as heard inside (quieter, muffled), and optional
///   cabin music (a WAV/OGG file in StreamingAssets/Audio);
/// - ambience: a diffuse distant-traffic hum at a set level;
/// - calibration: a 1 kHz tone at full scale (F3, or -audioCalibration) to measure with a sound
///   level meter at the participant's ears; enter the reading as ReferenceDb (-audioReferenceDb).
/// Levels: vehicle sounds are given as dB at 7.5 m (pass-by), the others at the listener.
/// Settings: this component on an experiment prefab (also trial parameters vehicleSounds=0/1,
/// electric=0/1, ambienceDb, cabinMusic, cabinMusicDb on the host), or the command line
/// (-vehicleSounds 1, -electricCars 0, -ambienceDb 50, -cabinMusic file.wav, -cabinMusicDb 60,
/// -audioReferenceDb 94), which every agent reads.
/// </summary>
public class Soundscape : MonoBehaviour, IExperimentModifier
{
    [Tooltip("Speed-dependent tyre and engine sound for all cars.")]
    public bool VehicleSounds;
    [Tooltip("Cars are electric (no engine sound, AVAS from auditory eHMIs).")]
    public bool ElectricCars = true;
    [Tooltip("Tyre-road noise of a car at 50 km/h, dB at 7.5 m.")]
    public float TyreDbAt50 = 68f;
    [Tooltip("Engine sound of a combustion car at low speed, dB at 7.5 m.")]
    public float EngineDb = 64f;
    [Tooltip("Heard inside the participant's own car: this much quieter than outside at 7.5 m (plus muffling).")]
    public float InteriorReductionDb = 6f;
    [Tooltip("Diffuse distant-traffic hum at the listener, dB; 0 = none.")]
    public float AmbienceDb;
    [Tooltip("WAV or OGG file in StreamingAssets/Audio played in the participant's car; empty = none.")]
    public string CabinMusic = "";
    public float CabinMusicDb = 60f;
    [Tooltip("SPL at the participant's ears of a full-scale source 1 m away (or a 2D source at full scale), measured with the calibration tone.")]
    public float ReferenceDb = 94f;
    [Tooltip("Cars with sound at a time (the nearest).")]
    public int MaxVehicles = 16;
    public float MaxDistance = 150f;

    public static Soundscape Current { get; private set; }

    static AudioClip _tyre, _engine, _hum, _tone;
    readonly Dictionary<Transform, VehicleVoice> _voices = new Dictionary<Transform, VehicleVoice>();
    readonly List<Transform> _cars = new List<Transform>();
    AudioSource _ambience, _music, _calibration;
    float _nextScan;

    /// <summary>The scene's soundscape: the experiment's, else one made from the command line (null when nothing is asked for).</summary>
    public static Soundscape Ensure()
    {
        if (Current != null) return Current;
        var found = FindAnyObjectByType<Soundscape>();
        if (found != null) { found.ApplyCommandLine(); return found; }
        var go = new GameObject("Soundscape");
        var s = go.AddComponent<Soundscape>();
        s.ApplyCommandLine();
        return s;
    }

    void Awake() => Current = this;
    void OnDestroy() { if (Current == this) Current = null; }

    public void SetParameter(NetworkingManager.ExperimentParameter[] ps)
    {
        var ci = CultureInfo.InvariantCulture;
        foreach (var p in ps)
        {
            var n = p.name.ToLowerInvariant();
            if (n == "vehiclesounds") VehicleSounds = p.value == "1" || p.value.ToLowerInvariant() == "true";
            else if (n == "electric") ElectricCars = p.value == "1" || p.value.ToLowerInvariant() == "true";
            else if (n == "ambiencedb" && float.TryParse(p.value, NumberStyles.Float, ci, out var a)) AmbienceDb = a;
            else if (n == "cabinmusic") CabinMusic = p.value;
            else if (n == "cabinmusicdb" && float.TryParse(p.value, NumberStyles.Float, ci, out var m)) CabinMusicDb = m;
        }
    }

    void ApplyCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        var ci = CultureInfo.InvariantCulture;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            var v = args[i + 1];
            switch (args[i])
            {
                case "-vehicleSounds": VehicleSounds = v != "0"; break;
                case "-electricCars": ElectricCars = v != "0"; break;
                case "-ambienceDb": float.TryParse(v, NumberStyles.Float, ci, out AmbienceDb); break;
                case "-cabinMusic": CabinMusic = v; break;
                case "-cabinMusicDb": float.TryParse(v, NumberStyles.Float, ci, out CabinMusicDb); break;
                case "-audioReferenceDb": float.TryParse(v, NumberStyles.Float, ci, out ReferenceDb); break;
            }
        }
        if (Array.IndexOf(args, "-audioCalibration") >= 0) ToggleCalibration();
    }

    /// <summary>Linear gain of a source whose level should be <paramref name="db"/> dB SPL at 1 m (3D) or at the listener (2D).</summary>
    public float Gain(float db) => Mathf.Clamp01(Mathf.Pow(10f, (db - ReferenceDb) / 20f));

    IEnumerator Start()
    {
        _tyre ??= Noise("tyre noise", 3f, pink: true, 0.5f);
        _engine ??= Engine();
        _hum ??= Noise("distant traffic", 4f, pink: false, 0.6f);
        _tone ??= MakeClip("1 kHz", 1f, t => Mathf.Sin(2f * Mathf.PI * 1000f * t));
        if (AmbienceDb > 0f)
        {
            _ambience = Source2D("Ambience", _hum, Gain(AmbienceDb));
            _ambience.Play();
        }
        if (!string.IsNullOrEmpty(CabinMusic)) yield return LoadMusic();
    }

    IEnumerator LoadMusic()
    {
        var path = Path.IsPathRooted(CabinMusic) ? CabinMusic : Path.Combine(Application.streamingAssetsPath, "Audio", CabinMusic);
        var type = path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS : path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? AudioType.MPEG : AudioType.WAV;
        using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning($"Soundscape: cannot load cabin music {path}: {req.error}"); yield break; }
        _music = Source2D("Cabin music", DownloadHandlerAudioClip.GetContent(req), Gain(CabinMusicDb));
        _music.Play();
        Debug.Log($"Soundscape: cabin music {Path.GetFileName(path)} at {CabinMusicDb} dB");
    }

    AudioSource Source2D(string name, AudioClip clip, float volume)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.clip = clip; s.loop = true; s.spatialBlend = 0f; s.volume = volume; s.playOnAwake = false;
        return s;
    }

    public void ToggleCalibration()
    {
        if (_calibration == null) { _tone ??= MakeClip("1 kHz", 1f, t => Mathf.Sin(2f * Mathf.PI * 1000f * t)); _calibration = Source2D("Calibration tone", _tone, 1f); }
        if (_calibration.isPlaying) _calibration.Stop(); else { _calibration.Play(); Debug.Log($"Soundscape: calibration tone (1 kHz, full scale) - measure the SPL and set ReferenceDb (now {ReferenceDb} dB)"); }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3)) ToggleCalibration();
        if (!VehicleSounds) return;
        var listener = FindListener();
        if (listener == null) return;
        if (Time.unscaledTime >= _nextScan) { _nextScan = Time.unscaledTime + 1f; Scan(listener.position); }
        var own = NetworkingManager.Instance != null && NetworkingManager.Instance.PlayerSystem != null && NetworkingManager.Instance.PlayerSystem.LocalPlayer != null
            ? NetworkingManager.Instance.PlayerSystem.LocalPlayer.transform : null;
        foreach (var kv in _voices) if (kv.Key != null) kv.Value.Tick(this, kv.Key == own);
    }

    Transform _listener;
    Transform FindListener()
    {
        if (_listener != null && _listener.gameObject.activeInHierarchy) return _listener;
        foreach (var l in FindObjectsByType<AudioListener>()) if (l.isActiveAndEnabled) { _listener = l.transform; break; }
        return _listener;
    }

    // the nearest cars get a voice; the others lose theirs
    void Scan(Vector3 at)
    {
        _cars.Clear();
        foreach (var c in FindObjectsByType<AICar>()) _cars.Add(c.transform);
        foreach (var c in FindObjectsByType<TrafficCar>()) _cars.Add(c.transform);
        foreach (var p in FindObjectsByType<PlayerAvatar>()) if (p.Type == AvatarType.Driver && !_cars.Contains(p.transform)) _cars.Add(p.transform);
        _cars.RemoveAll(t => t == null || (t.position - at).sqrMagnitude > MaxDistance * MaxDistance);
        _cars.Sort((a, b) => (a.position - at).sqrMagnitude.CompareTo((b.position - at).sqrMagnitude));
        var keep = new HashSet<Transform>(_cars.GetRange(0, Mathf.Min(MaxVehicles, _cars.Count)));
        var drop = new List<Transform>();
        foreach (var kv in _voices) if (kv.Key == null || !keep.Contains(kv.Key)) drop.Add(kv.Key);
        foreach (var t in drop) { if (_voices[t] != null) _voices[t].Dispose(); _voices.Remove(t); }
        foreach (var t in keep) if (!_voices.ContainsKey(t)) _voices[t] = new VehicleVoice(t, _tyre, _engine);
        if (!_reported && _voices.Count > 0)
        {
            _reported = true;
            Debug.Log($"Soundscape: {_voices.Count} of {_cars.Count} cars within {MaxDistance} m have sound ({(ElectricCars ? "electric" : "combustion")}), reference {ReferenceDb} dB, ambience {AmbienceDb} dB");
        }
    }
    bool _reported;

    /// <summary>For tests: (car, speed km/h, tyre volume, engine volume) of the cars with sound.</summary>
    public IEnumerable<(string car, float kmh, float tyre, float engine)> Voices()
    {
        foreach (var kv in _voices) if (kv.Key != null) yield return (kv.Key.name, kv.Value.Kmh, kv.Value.TyreVolume, kv.Value.EngineVolume);
    }

    /// <summary>Tyre and engine sources of one car.</summary>
    sealed class VehicleVoice
    {
        readonly Transform _car;
        readonly AudioSource _tyre, _engine;
        readonly AudioLowPassFilter _tyreLp, _engineLp;
        Vector3 _last;
        float _speed;   // km/h, smoothed
        bool _first = true;

        public VehicleVoice(Transform car, AudioClip tyre, AudioClip engine)
        {
            _car = car;
            _tyre = Make(car, "Tyre noise", tyre, out _tyreLp);
            _engine = Make(car, "Engine", engine, out _engineLp);
        }

        static AudioSource Make(Transform car, string name, AudioClip clip, out AudioLowPassFilter lp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(car, false);
            go.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.playOnAwake = false;
            s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 1f; s.maxDistance = 500f; s.dopplerLevel = 0.5f;
            s.time = UnityEngine.Random.value * clip.length * 0.9f;
            lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000f;
            s.volume = 0f;
            s.Play();
            return s;
        }

        public void Tick(Soundscape sc, bool own)
        {
            if (_car == null) return;
            var p = _car.position;
            if (_first) { _last = p; _first = false; }
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float v = Vector3.Distance(p, _last) / dt * 3.6f;
            _last = p;
            if (v > 250f) v = _speed;   // teleports
            _speed = Mathf.Lerp(_speed, v, 1f - Mathf.Exp(-dt * 4f));
            float kmh = Mathf.Max(_speed, 0f);
            // dB at 7.5 m -> at 1 m (+17.5 dB), the reference distance of the 3D sources
            float tyreDb = kmh < 3f ? -100f : sc.TyreDbAt50 + 30f * Mathf.Log10(Mathf.Max(kmh, 3f) / 50f) + 17.5f;
            float engineDb = sc.ElectricCars ? -100f : sc.EngineDb + 6f * Mathf.Clamp01(kmh / 60f) + 17.5f;
            bool inside = own;
            float reduce = inside ? sc.InteriorReductionDb + 17.5f : 0f;   // inside: at the reference distance, quieter
            _tyre.spatialBlend = _engine.spatialBlend = inside ? 0f : 1f;
            _tyreLp.cutoffFrequency = inside ? 700f : 8000f;
            _engineLp.cutoffFrequency = inside ? 1200f : 22000f;
            _tyre.volume = sc.Gain(tyreDb - reduce);
            _tyre.pitch = 0.6f + 0.8f * Mathf.Clamp01(kmh / 100f);
            _engine.volume = sc.Gain(engineDb - reduce);
            // engine: "gears" every 25 km/h so the pitch rises and drops like a gearbox
            float inGear = Mathf.Repeat(kmh, 25f) / 25f;
            _engine.pitch = kmh < 2f ? 0.8f : 0.9f + 0.7f * inGear;
        }

        public float Kmh => _speed;
        public float TyreVolume => _tyre != null ? _tyre.volume : 0f;
        public float EngineVolume => _engine != null ? _engine.volume : 0f;

        public void Dispose()
        {
            if (_tyre != null) Destroy(_tyre.gameObject);
            if (_engine != null) Destroy(_engine.gameObject);
        }
    }

    // ------------------------------------------------------------------ procedural clips

    static AudioClip MakeClip(string name, float seconds, Func<float, float> f)
    {
        int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        int n = Mathf.RoundToInt(seconds * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = f(i / (float)rate);
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // pink (tyre roar) or brown (distant hum) noise, looped without a click (cross-faded ends)
    static AudioClip Noise(string name, float seconds, bool pink, float amplitude)
    {
        var rnd = new System.Random(pink ? 7 : 11);
        int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        int n = Mathf.RoundToInt(seconds * rate);
        var data = new float[n];
        double b0 = 0, b1 = 0, b2 = 0, brown = 0;
        float max = 1e-6f;
        for (int i = 0; i < n; i++)
        {
            double w = rnd.NextDouble() * 2 - 1;
            double v;
            if (pink) { b0 = 0.99765 * b0 + w * 0.0990460; b1 = 0.96300 * b1 + w * 0.2965164; b2 = 0.57000 * b2 + w * 1.0526913; v = b0 + b1 + b2 + w * 0.1848; }
            else { brown = (brown + 0.02 * w) / 1.02; v = brown * 3.5; }
            data[i] = (float)v; max = Mathf.Max(max, Mathf.Abs(data[i]));
        }
        int fade = rate / 10;
        for (int i = 0; i < fade; i++) { float a = i / (float)fade; data[n - fade + i] = data[n - fade + i] * (1 - a) + data[i] * a; }
        for (int i = 0; i < n; i++) data[i] = data[i] / max * amplitude;
        var clip = AudioClip.Create(name, n - fade, 1, rate, false);
        var trimmed = new float[n - fade];
        Array.Copy(data, fade, trimmed, 0, n - fade - fade);
        Array.Copy(data, n - fade, trimmed, n - fade - fade, fade);
        clip.SetData(trimmed, 0);
        return clip;
    }

    // a four-cylinder idle: 25 Hz firing with harmonics, a little roughness
    static AudioClip Engine() => MakeClip("engine", 2f, t =>
    {
        float f = 50f;
        float v = 0.35f * Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 2f * f * t + 0.3f)
                + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * f * t + 1.1f) + 0.08f * Mathf.Sin(2f * Mathf.PI * 4f * f * t);
        return v * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 12.5f * t));
    });
}
