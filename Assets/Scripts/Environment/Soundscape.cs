using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CoupledSim.Sound;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sound of the scene with levels in dB SPL (after calibration), with the levels from models and the sounds from
/// recordings where there are any (<see cref="SoundClips"/>; each recording is set to the level, A-weighted where the
/// model gives a spectrum, that the model gives):
/// - <b>vehicles</b>: sound power by the European road traffic noise model CNOSSOS-EU (<see cref="Cnossos"/>: rolling
///   and propulsion noise per octave band for cars and vans, medium and heavy trucks and buses, mopeds), louder on
///   bricks and cobblestones (RoadSurfaces), with the engine's firing frequency through its gears (<see cref="EngineModel"/>)
///   and louder when accelerating; electric cars (a share of the fleet, and the city buses) whine instead and are
///   quieter at low speed; bicycles roll and freewheel, e-scooters whine;
/// - <b>propagation</b>: spherical spreading (-6 dB per doubling of distance), air absorption (ISO 9613-1: a low-pass
///   that falls with distance), shielding by buildings (about 15 dB and the highs lost behind them), the Doppler shift,
///   and the participant's own car: its body keeps out about 20 dB and the highs, its own engine and tyres come
///   through the floor;
/// - <b>ambience</b>: distant traffic following the time of day, rain (on the roof inside a car), wind at the ears
///   (with gusts; for riders also their own speed), birds in the trees (most at dawn, none at night or in the rain),
///   and echoes between buildings (more reverberation in narrow streets);
/// - <b>people</b>: footsteps of the participant and of nearby pedestrians on hard, soft or loose ground, and the
///   ticking of pedestrian signals (slow at red, fast at green, as on Dutch "rateltikkers");
/// - calibration: a 1 kHz tone at full scale (F3, or -audioCalibration) to measure with a sound level meter at the
///   participant's ears; enter the reading as ReferenceDb (-audioReferenceDb).
/// On city maps it is on by default (-vehicleSounds 0 turns it off). Settings: this component on an experiment
/// prefab (trial parameters vehicleSounds, electric (share 0..1, or 0/1), ambienceDb, cabinMusic, cabinMusicDb on the
/// host), or the command line (-vehicleSounds 1, -electricCars 0.3, -ambienceDb 50, -cabinMusic file.wav,
/// -cabinMusicDb 60, -audioReferenceDb 94, -birds 0, -signalTicks 0, -footsteps 0, -soundDebug 1 to log the levels
/// of the participant's own vehicle), which every agent reads.
/// </summary>
public class Soundscape : MonoBehaviour, IExperimentModifier
{
    [Tooltip("Vehicles, ambience, weather, birds, footsteps and signals.")]
    public bool VehicleSounds;
    [Tooltip("Share of electric cars (0..1); city buses are electric.")]
    [Range(0f, 1f)] public float ElectricShare = 0.15f;
    [Tooltip("Distant traffic at the listener, dB; negative: by the time of day (about 55 dB by day, 44 dB at night); 0 = none.")]
    public float AmbienceDb = -1f;
    public bool Birds = true, Weather = true, Footsteps = true, SignalTicks = true, Reverb = true;
    [Tooltip("WAV or OGG file in StreamingAssets/Audio played in the participant's car; empty = none.")]
    public string CabinMusic = "";
    public float CabinMusicDb = 60f;
    [Tooltip("SPL at the participant's ears of a full-scale 1 kHz tone (a 3D source 1 m away, or a 2D source), measured with the calibration tone.")]
    public float ReferenceDb = 94f;
    [Tooltip("Vehicles with sound at a time (the nearest).")]
    public int MaxVehicles = 20;
    public float MaxDistance = 250f;

    /// <summary>Old setting: all cars electric (true) or none (false).</summary>
    public bool ElectricCars { get => ElectricShare >= 0.999f; set => ElectricShare = value ? 1f : 0f; }

    public static Soundscape Current { get; private set; }

    /// <summary>Logs the levels of the participant's own vehicle every 2 s (-soundDebug 1).</summary>
    public static bool SoundDebug;

    static AudioClip _tone;
    readonly Dictionary<Transform, VehicleVoice> _voices = new Dictionary<Transform, VehicleVoice>();
    readonly List<Transform> _vehicles = new List<Transform>();
    AudioSource _ambience, _rain, _roofRain, _wind, _music, _calibration;
    SoundClips.Recording _ambienceRec;
    readonly List<AudioSource> _birds = new List<AudioSource>();
    readonly List<SoundClips.Recording> _birdRecs = new List<SoundClips.Recording>();
    AudioReverbFilter _reverb;
    float _nextScan, _nextEnv, _canyon, _nextBirds;
    Transform _listener;

    /// <summary>The scene's soundscape: the experiment's, else one made from the command line (on city maps on by default).</summary>
    public static Soundscape Ensure()
    {
        if (Current != null) return Current;
        var found = FindAnyObjectByType<Soundscape>();
        if (found != null) { found.ApplyCommandLine(); return found; }
        var go = new GameObject("Soundscape");
        var s = go.AddComponent<Soundscape>();
        s.VehicleSounds = OsmMap.Current != null;
        s.ApplyCommandLine();
        return s;
    }

    void Awake() => Current = this;
    void OnDestroy()
    {
        if (Current == this) Current = null;
        foreach (var v in _voices.Values) v?.Dispose();
        _voices.Clear();
        _ticks.Dispose();
        _steps.Dispose();
    }

    static bool Truthy(string v) => v == "1" || v.ToLowerInvariant() == "true";

    public void SetParameter(NetworkingManager.ExperimentParameter[] ps)
    {
        var ci = CultureInfo.InvariantCulture;
        foreach (var p in ps)
        {
            var n = p.name.ToLowerInvariant();
            if (n == "vehiclesounds") VehicleSounds = Truthy(p.value);
            else if (n == "electric" && float.TryParse(p.value.ToLowerInvariant() == "true" ? "1" : p.value.ToLowerInvariant() == "false" ? "0" : p.value, NumberStyles.Float, ci, out var e)) ElectricShare = Mathf.Clamp01(e);
            else if (n == "ambiencedb" && float.TryParse(p.value, NumberStyles.Float, ci, out var a)) AmbienceDb = a;
            else if (n == "cabinmusic") CabinMusic = p.value;
            else if (n == "cabinmusicdb" && float.TryParse(p.value, NumberStyles.Float, ci, out var m)) CabinMusicDb = m;
        }
    }

    void ApplyCommandLine()
    {
        var args = CommandLine.Args;
        var ci = CultureInfo.InvariantCulture;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            var v = args[i + 1];
            switch (args[i])
            {
                case "-vehicleSounds": VehicleSounds = v != "0"; break;
                case "-electricCars": if (float.TryParse(v, NumberStyles.Float, ci, out var e)) ElectricShare = Mathf.Clamp01(e); break;
                case "-ambienceDb": float.TryParse(v, NumberStyles.Float, ci, out AmbienceDb); break;
                case "-cabinMusic": CabinMusic = v; break;
                case "-cabinMusicDb": float.TryParse(v, NumberStyles.Float, ci, out CabinMusicDb); break;
                case "-audioReferenceDb": float.TryParse(v, NumberStyles.Float, ci, out ReferenceDb); break;
                case "-birds": Birds = v != "0"; break;
                case "-signalTicks": SignalTicks = v != "0"; break;
                case "-footsteps": Footsteps = v != "0"; break;
                case "-soundDebug": SoundDebug = v != "0"; break;
            }
        }
        if (Array.IndexOf(args, "-audioCalibration") >= 0) ToggleCalibration();
    }

    /// <summary>Linear gain of a 2D source playing a full-scale tone to be heard at <paramref name="db"/> dB SPL.</summary>
    public float Gain(float db) => Mathf.Clamp01(Mathf.Pow(10f, (db - ReferenceDb) / 20f));

    /// <summary>Gain of one of the <see cref="SoundClips"/> (RMS <see cref="SoundClips.Rms"/>) to be heard at
    /// <paramref name="db"/> dB SPL (2D), or at 1 m (3D sources, which fall off by 6 dB per doubling of distance).</summary>
    public float ClipGain(float db) => db < -50f ? 0f : Mathf.Clamp01(Mathf.Pow(10f, (db - ReferenceDb) / 20f) * 0.7071f / SoundClips.Rms);

    /// <summary>Gain of a recording (or, when <paramref name="r"/> is null, of a synthesised clip) to be heard at
    /// <paramref name="db"/> dB SPL; with <paramref name="aShift"/> (a model's A-weighted minus unweighted level,
    /// <see cref="AShift"/>) its A-weighted level matches the model's, whatever the recording's spectrum.</summary>
    public float RecGain(SoundClips.Recording r, float db, float? aShift = null)
    {
        if (db < -50f) return 0f;
        if (r == null) return ClipGain(db);
        // (at most 12 dB louder unweighted than the model: rumbly recordings, such as on cobblestones, have most of their
        // energy at low frequencies that the A-weighting hardly counts)
        if (aShift.HasValue) db += Mathf.Min(aShift.Value - r.AWeightDb, 12f);
        return Mathf.Clamp01(Mathf.Pow(10f, (db - ReferenceDb) / 20f) * 0.7071f / SoundClips.Rms * r.Factor);
    }

    /// <summary>A-weighted minus unweighted total of an octave-band spectrum (dB), optionally after a two-pole low-pass
    /// at <paramref name="lowPassHz"/> (the A-weighted part only: the low-passed level a recording made there has).</summary>
    public static float AShift(Func<int, float> band, float lowPassHz = 0f)
    {
        float z = Cnossos.Total(band, false);
        float a = Cnossos.Total(i => band(i) + (lowPassHz > 0f ? -10f * Mathf.Log10(1f + Mathf.Pow(Cnossos.Bands[i] / lowPassHz, 4f)) : 0f), true);
        return a - z;
    }

    IEnumerator Start()
    {
        if (VehicleSounds)
        {
            _ambienceRec = SoundClips.Rec("city_traffic");
            _ambience = Source2D("Distant traffic", _ambienceRec != null ? _ambienceRec.Clip : SoundClips.DistantTraffic());
            _rain = Source2D("Rain", SoundClips.Rain(false));
            _roofRain = Source2D("Rain on the roof", SoundClips.Rain(true));
            _wind = Source2D("Wind", SoundClips.Wind());
            foreach (var s in new[] { _ambience, _rain, _roofRain, _wind }) { s.volume = 0f; s.time = UnityEngine.Random.value * s.clip.length * 0.9f; s.Play(); }
        }
        if (!string.IsNullOrEmpty(CabinMusic)) yield return LoadMusic();
    }

    IEnumerator LoadMusic()
    {
        var path = Path.IsPathRooted(CabinMusic) ? CabinMusic : Path.Combine(Application.streamingAssetsPath, "Audio", CabinMusic);
        var type = path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS : path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? AudioType.MPEG : AudioType.WAV;
        var uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        using var req = UnityWebRequestMultimedia.GetAudioClip(uri, type);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning($"Soundscape: cannot load cabin music {path}: {req.error}"); yield break; }
        _music = Source2D("Cabin music", DownloadHandlerAudioClip.GetContent(req));
        _music.volume = Gain(CabinMusicDb);
        _music.Play();
        Debug.Log($"Soundscape: cabin music {Path.GetFileName(path)} at {CabinMusicDb} dB");
    }

    AudioSource Source2D(string name, AudioClip clip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.clip = clip; s.loop = true; s.spatialBlend = 0f; s.playOnAwake = false;
        return s;
    }

    public static AudioSource Source3D(Transform parent, string name, AudioClip clip, bool loop, Vector3 local = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip; s.loop = loop; s.playOnAwake = false;
        s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 1f; s.maxDistance = 1000f; s.dopplerLevel = 1f;
        s.volume = 0f;
        return s;
    }

    /// <summary>A source for PlayOneShot, whose level is in its volume scale: the source's own volume at 1 (Source3D
    /// starts silent, which kept footsteps, signal ticks and freewheel clicks silent before).</summary>
    static AudioSource ForOneShots(AudioSource s) { s.volume = 1f; return s; }

    public void ToggleCalibration()
    {
        if (_calibration == null)
        {
            _tone ??= AudioClipTone();
            _calibration = Source2D("Calibration tone", _tone);
            _calibration.volume = 1f;
        }
        if (_calibration.isPlaying) _calibration.Stop(); else { _calibration.Play(); Debug.Log($"Soundscape: calibration tone (1 kHz, full scale) - measure the SPL and set ReferenceDb (now {ReferenceDb} dB)"); }
    }

    static AudioClip AudioClipTone()
    {
        int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        var d = new float[rate];
        for (int i = 0; i < rate; i++) d[i] = Mathf.Sin(2f * Mathf.PI * 1000f * i / rate);
        var c = AudioClip.Create("1 kHz", rate, 1, rate, false); c.SetData(d, 0);
        return c;
    }

    // ------------------------------------------------------------------ every frame

    /// <summary>The participant's own vehicle (null for pedestrians and drones).</summary>
    public Transform OwnVehicle { get; private set; }
    /// <summary>The listener is inside the participant's car.</summary>
    public bool InsideCar { get; private set; }
    public Transform Listener => _listener;
    /// <summary>The local participant's avatar (null before there is one).</summary>
    public Transform Participant { get; private set; }

    /// <summary>Whose place the listener is at, when not the local participant's (a video of a replay: the first quarter
    /// of the grid); null: the local participant.</summary>
    public static PlayerAvatar ListenerAvatar;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3)) ToggleCalibration();
        if (!VehicleSounds) return;
        var listener = FindListener();
        if (listener == null) return;
        var me = ListenerAvatar != null ? ListenerAvatar
            : NetworkingManager.Instance != null && NetworkingManager.Instance.PlayerSystem != null ? NetworkingManager.Instance.PlayerSystem.LocalPlayer : null;
        OwnVehicle = me != null && (me.Type == AvatarType.Driver) ? me.transform : null;
        Participant = me != null ? me.transform : null;
        InsideCar = OwnVehicle != null && listener.IsChildOf(OwnVehicle);
        if (Time.unscaledTime >= _nextScan) { _nextScan = Time.unscaledTime + 0.5f; Scan(listener.position); }
        foreach (var kv in _voices) if (kv.Key != null) kv.Value.Tick(this, kv.Key == OwnVehicle && InsideCar);
        UpdateEnvironment(listener, me);
        if (SignalTicks) _ticks.Tick(this, listener.position);
        if (Footsteps) _steps.Tick(this, listener, me);
    }

    AudioListener _listenerComp;
    Transform FindListener()
    {
        if (_listenerComp != null && _listenerComp.isActiveAndEnabled) return _listener;
        _listener = null;
        foreach (var l in FindObjectsByType<AudioListener>()) if (l.isActiveAndEnabled) { _listenerComp = l; _listener = l.transform; break; }
        if (_listener != null && Reverb) { if (!_listener.TryGetComponent(out _reverb)) _reverb = _listener.gameObject.AddComponent<AudioReverbFilter>(); _reverb.reverbPreset = AudioReverbPreset.User; }
        return _listener;
    }

    // the nearest vehicles get a voice; the others lose theirs
    void Scan(Vector3 at)
    {
        _vehicles.Clear();
        foreach (var c in FindObjectsByType<AICar>()) if (c.gameObject.activeInHierarchy) _vehicles.Add(c.transform);
        foreach (var c in FindObjectsByType<TrafficCar>()) if (!_vehicles.Contains(c.transform)) _vehicles.Add(c.transform);
        foreach (var p in FindObjectsByType<PlayerAvatar>()) if ((p.Type == AvatarType.Driver || p.GetComponent<TwoWheelerController>() != null) && !_vehicles.Contains(p.transform)) _vehicles.Add(p.transform);
        _vehicles.RemoveAll(t => t == null || (t.position - at).sqrMagnitude > MaxDistance * MaxDistance);
        _vehicles.Sort((a, b) => (a.position - at).sqrMagnitude.CompareTo((b.position - at).sqrMagnitude));
        var keep = new HashSet<Transform>(_vehicles.GetRange(0, Mathf.Min(MaxVehicles, _vehicles.Count)));
        if (OwnVehicle != null) keep.Add(OwnVehicle);
        var drop = _voices.Keys.Where(t => t == null || !keep.Contains(t)).ToList();
        foreach (var t in drop) { _voices[t]?.Dispose(); _voices.Remove(t); }
        foreach (var t in keep) if (!_voices.ContainsKey(t)) _voices[t] = new VehicleVoice(t, this);
        if (!_reported && _voices.Count > 0)
        {
            _reported = true;
            Debug.Log($"Soundscape: {_voices.Count} of {_vehicles.Count} vehicles within {MaxDistance} m have sound (CNOSSOS-EU; {ElectricShare * 100:F0}% of cars electric), reference {ReferenceDb} dB");
        }
    }
    bool _reported;

    /// <summary>For tests: (vehicle, speed km/h, rolling volume, propulsion volume) of the vehicles with sound.</summary>
    public IEnumerable<(string car, float kmh, float tyre, float engine)> Voices()
    {
        foreach (var kv in _voices) if (kv.Key != null) yield return (kv.Key.name, kv.Value.Kmh, kv.Value.RollingVolume, kv.Value.PropulsionVolume);
    }

    /// <summary>Distant traffic by the hour (a typical urban day: quiet at night, a peak in the morning).</summary>
    public static float DiurnalDb(float hour)
    {
        float[] h = { 0, 3, 5, 7, 8, 12, 17, 19, 22, 24 };
        float[] db = { 46, 43, 44, 53, 56, 54, 56, 52, 48, 46 };
        hour = Mathf.Repeat(hour, 24f);
        for (int i = 1; i < h.Length; i++) if (hour <= h[i]) return Mathf.Lerp(db[i - 1], db[i], (hour - h[i - 1]) / (h[i] - h[i - 1]));
        return db[0];
    }

    void UpdateEnvironment(Transform listener, PlayerAvatar me)
    {
        var env = EnvironmentController.Instance;
        float hour = env != null ? env.TimeOfDay : 13f;
        float inside = InsideCar ? -20f : 0f;   // the car's body
        // distant traffic
        float amb = AmbienceDb < 0f ? DiurnalDb(hour) : AmbienceDb;
        if (_ambience != null) _ambience.volume = AmbienceDb == 0f ? 0f : RecGain(_ambienceRec, amb + (InsideCar ? -12f : 0f));
        // rain: on the ground around, or drumming on the roof
        float rain = Weather && env != null ? env.Rain : 0f;
        if (_rain != null) _rain.volume = rain > 0.01f ? ClipGain(46f + 18f * rain + inside) : 0f;
        if (_roofRain != null) _roofRain.volume = rain > 0.01f && InsideCar ? ClipGain(52f + 16f * rain) : 0f;
        // wind at the ears (outside a car): the wind plus a rider's own speed, with gusts
        if (_wind != null)
        {
            float w = Weather && env != null ? env.WindSpeed : 0f;
            var tw = me != null ? me.GetComponent<TwoWheelerController>() : null;
            if (tw != null) w = Mathf.Sqrt(w * w + Mathf.Pow(tw.SpeedKmh / 3.6f, 2));
            float gust = 1f + 0.35f * (Mathf.PerlinNoise(Time.time * 0.25f, 3.1f) - 0.5f) * 2f;
            float v = w * gust;
            _wind.volume = InsideCar || v < 1.5f ? 0f : ClipGain(35f + 45f * Mathf.Log10(v));
        }
        if (Time.unscaledTime < _nextEnv) return;
        _nextEnv = Time.unscaledTime + 0.5f;
        // echoes between buildings: how much of the horizon is a wall, and how close
        if (_reverb != null)
        {
            int hits = 0; float dist = 0f;
            for (int k = 0; k < 12; k++)
            {
                var dir = Quaternion.Euler(0, k * 30f, 0) * Vector3.forward;
                if (Physics.Raycast(listener.position, dir, out var hit, 45f, ~0, QueryTriggerInteraction.Ignore) && IsBuilding(hit.collider)) { hits++; dist += hit.distance; }
            }
            float frac = hits / 12f, near = hits > 0 ? Mathf.Clamp01(1f - dist / hits / 45f) : 0f;
            _canyon = Mathf.Lerp(_canyon, frac * (0.5f + 0.5f * near), 0.5f);
            float c = InsideCar ? 0f : _canyon;
            _reverb.dryLevel = 0f;
            _reverb.room = Mathf.Lerp(-10000f, -900f, Mathf.Sqrt(c));
            _reverb.roomHF = -800f;
            _reverb.decayTime = Mathf.Lerp(0.5f, 1.7f, c);
            _reverb.decayHFRatio = 0.6f;
            _reverb.reflectionsLevel = Mathf.Lerp(-10000f, -600f, Mathf.Sqrt(c));
            _reverb.reflectionsDelay = Mathf.Lerp(0.03f, 0.008f, near);
            _reverb.reverbLevel = Mathf.Lerp(-10000f, -500f, Mathf.Sqrt(c));
            _reverb.reverbDelay = 0.02f;
            _reverb.diffusion = 80f; _reverb.density = 100f;
        }
        if (Birds) PlaceBirds(listener.position, hour, rain);
    }

    public static bool IsBuilding(Collider c) => c != null && (c.name.StartsWith("Buildings") || c.name.StartsWith("Roofs") || c.name.StartsWith("Real buildings") || c.name == "Cornices");

    // birds sing in the nearest trees: most at dawn, little at midday, none at night or in the rain
    void PlaceBirds(Vector3 at, float hour, float rain)
    {
        float act = hour < 4.5f || hour > 21.5f ? 0f : hour < 5.5f ? (hour - 4.5f) : hour < 8.5f ? 1f : hour < 11f ? Mathf.Lerp(1f, 0.45f, (hour - 8.5f) / 2.5f) : hour < 18f ? 0.45f : hour < 20.5f ? 0.6f : (21.5f - hour) * 0.6f;
        act *= 1f - Mathf.Clamp01(rain * 1.5f);
        if (Time.unscaledTime >= _nextBirds)
        {
            _nextBirds = Time.unscaledTime + 15f;
            var trees = OsmMap.Current != null && OsmMap.Current.Generator != null ? OsmMap.Current.Generator.TreeTransforms : null;
            var near = trees == null ? new List<Transform>() : trees.Where(t => t != null && (t.position - at).sqrMagnitude < 60f * 60f).OrderBy(t => (t.position - at).sqrMagnitude).Take(12).ToList();
            int want = Mathf.Min(4, near.Count / 3 + (near.Count > 0 ? 1 : 0));
            while (_birds.Count < want)
            {
                var rec = SoundClips.BirdRecording(_birds.Count);
                var s = Source3D(transform, "Bird", rec != null ? rec.Clip : SoundClips.Birds(_birds.Count), true);
                s.time = UnityEngine.Random.value * s.clip.length * 0.9f; s.pitch = 0.92f + 0.16f * UnityEngine.Random.value; s.Play();
                _birds.Add(s); _birdRecs.Add(rec);
            }
            for (int i = 0; i < _birds.Count; i++)
            {
                if (i < want && near.Count > 0) { var t = near[(i * 3 + UnityEngine.Random.Range(0, 3)) % near.Count]; _birds[i].transform.position = t.position + Vector3.up * (3f + 4f * UnityEngine.Random.value); }
                else _birds[i].transform.position = at + Vector3.up * 500f;   // (out of earshot)
            }
        }
        for (int i = 0; i < _birds.Count; i++) _birds[i].volume = act <= 0f ? 0f : RecGain(_birdRecs[i], 62f + 10f * Mathf.Log10(act + 1e-3f) + (InsideCar ? -20f : 0f));
    }

    readonly Tickers _ticks = new Tickers();
    readonly Steps _steps = new Steps();

    // ------------------------------------------------------------------ one vehicle

    sealed class VehicleVoice
    {
        readonly Transform _car;
        readonly AudioSource _rollLo, _rollHi, _prop, _engine, _clicks;
        readonly AudioSource[] _sources;
        readonly AudioLowPassFilter[] _lp;
        readonly VehicleCategory _cat;
        readonly EngineModel _engineModel;
        readonly bool _electric, _bicycle, _scooter, _twoWheeler;
        readonly TwoWheelerController _tw;
        // recordings: engine layers (crossfaded by the firing frequency), a bicycle's tyres on asphalt and on
        // cobblestones and its freewheel, and the inside of the participant's own car (made when first needed)
        readonly AudioSource[] _layers;
        readonly (SoundClips.Recording rec, float firingHz)[] _layerRecs;
        readonly AudioSource _bikeSmooth, _bikeRough, _freewheel;
        readonly SoundClips.Recording _bikeSmoothRec, _bikeRoughRec, _freewheelRec;
        AudioSource _cabinSmooth, _cabinRough;
        SoundClips.Recording _cabinSmoothRec, _cabinRoughRec;
        bool _cabinTried;
        Vector3 _last;
        float _speed, _accel, _surfaceDb, _roughness, _nextSurface, _nextOcclusion, _occluded, _clickTimer, _coast, _nextDebug, _rough, _surfaceTarget;
        bool _first = true, _occludedNow;

        public float Kmh => _speed;
        public float RollingVolume => (_rollLo != null ? _rollLo.volume : 0f) + (_rollHi != null ? _rollHi.volume : 0f)
            + (_bikeSmooth != null ? _bikeSmooth.volume + _bikeRough.volume : 0f) + (_cabinSmooth != null ? _cabinSmooth.volume + _cabinRough.volume : 0f);
        public float PropulsionVolume => (_prop != null ? _prop.volume : 0f) + (_engine != null ? _engine.volume : 0f) + (_layers != null ? _layers.Sum(l => l.volume) : 0f);

        public VehicleVoice(Transform car, Soundscape sc)
        {
            _car = car;
            var tw = _tw = car.GetComponent<TwoWheelerController>();
            string n = car.name.ToLowerInvariant();
            var dyn = car.GetComponentInChildren<RealisticVehicleDynamics>(true);
            var preset = dyn != null ? dyn.Car : RealisticVehicleDynamics.CarPreset.Hatchback;
            var type = tw != null ? tw.Type : (CoupledSim.TwoWheelers.TwoWheelerType?)null;
            _bicycle = n.Contains("bicycle") || type == CoupledSim.TwoWheelers.TwoWheelerType.Bicycle || type == CoupledSim.TwoWheelers.TwoWheelerType.EBike;
            _scooter = n.Contains("scooter") || type == CoupledSim.TwoWheelers.TwoWheelerType.EScooter;
            bool moped = n.Contains("moped") || type == CoupledSim.TwoWheelers.TwoWheelerType.Moped25 || type == CoupledSim.TwoWheelers.TwoWheelerType.Moped45;
            _twoWheeler = _bicycle || _scooter || moped;
            _cat = moped ? VehicleCategory.Moped
                : preset == RealisticVehicleDynamics.CarPreset.CityBus || n.Contains("daf") || n.Contains("bus") ? VehicleCategory.Heavy
                : preset == RealisticVehicleDynamics.CarPreset.Truck ? VehicleCategory.MediumHeavy : VehicleCategory.Light;
            // electric: city buses (as in most Dutch cities), e-scooters, e-bikes and a share of the cars, the same on every agent
            uint h = (uint)car.name.GetHashCode();
            _electric = _scooter || preset == RealisticVehicleDynamics.CarPreset.CityBus || n.Contains("articulatedbus") || (_cat == VehicleCategory.Light && !_twoWheeler && (h % 1000) / 1000f < sc.ElectricShare);
            _engineModel = EngineModel.For(_cat);
            var lps = new List<AudioLowPassFilter>(4);
            AudioSource Add(string name, AudioClip clip, float y = 0.4f)
            {
                var s = Source3D(car, name, clip, true, new Vector3(0f, y, 0f));
                s.time = UnityEngine.Random.value * clip.length * 0.9f;
                lps.Add(s.gameObject.AddComponent<AudioLowPassFilter>());
                s.Play();
                return s;
            }
            if (_bicycle)
            {
                _bikeSmoothRec = SoundClips.Rec("bike_asphalt"); _bikeRoughRec = SoundClips.Rec("bike_cobbles");
                if (_bikeSmoothRec != null && _bikeRoughRec != null)
                {
                    _bikeSmooth = Add("Tyres", _bikeSmoothRec.Clip);
                    _bikeRough = Add("Tyres on cobblestones", _bikeRoughRec.Clip);
                }
                else _rollLo = Add("Tyres", SoundClips.Rolling(VehicleCategory.Light, 30f));
                _freewheelRec = SoundClips.Rec("bike_freewheel");
                if (_freewheelRec != null) _freewheel = Add("Freewheel", _freewheelRec.Clip, 0.5f);
                else _clicks = ForOneShots(Source3D(car, "Freewheel", SoundClips.Freewheel(), false, new Vector3(0f, 0.5f, 0f)));
            }
            else if (_scooter)
            {
                _rollLo = Add("Wheels", SoundClips.Rolling(VehicleCategory.Light, 30f));
                _engine = Add("Motor", SoundClips.Whine());
            }
            else
            {
                if (_cat != VehicleCategory.Moped)
                {
                    _rollLo = Add("Tyres (low speed)", SoundClips.Rolling(_cat, 30f));
                    _rollHi = Add("Tyres (high speed)", SoundClips.Rolling(_cat, 90f));
                }
                _layerRecs = _electric || _cat == VehicleCategory.Moped ? null : SoundClips.EngineRecordings(_cat == VehicleCategory.Light ? "petrol" : "diesel");
                if (_layerRecs != null) _layers = _layerRecs.Select(l => Add("Engine (" + l.rec.Name + ")", l.rec.Clip)).ToArray();
                else
                {
                    _prop = Add("Propulsion", SoundClips.PropulsionNoise(_cat, _electric));
                    _engine = Add(_electric ? "Motor" : "Engine", _electric ? SoundClips.Whine() : SoundClips.Engine(_cat == VehicleCategory.Moped ? "twostroke" : _cat == VehicleCategory.Light ? "petrol" : "diesel"));
                }
            }
            // the tyre and engine sources, once (no iterator every frame); the bicycle's sounds are handled on their own
            var srcs = new List<AudioSource>(8);
            foreach (var x in new[] { _rollLo, _rollHi, _prop, _engine }) if (x != null) srcs.Add(x);
            if (_layers != null) srcs.AddRange(_layers);
            _sources = srcs.ToArray();
            _lp = lps.ToArray();
        }

        // the inside of the participant's own car: recorded road noise (made the first time the listener is inside)
        void MakeCabin(Soundscape sc)
        {
            _cabinTried = true;
            _cabinSmoothRec = SoundClips.Rec("car_interior_road"); _cabinRoughRec = SoundClips.Rec("car_interior_cobbles");
            if (_cabinSmoothRec == null || _cabinRoughRec == null || _rollLo == null) return;
            AudioSource Make(string name, AudioClip clip)
            {
                var s = Source3D(_car, name, clip, true);
                s.spatialBlend = 0f; s.dopplerLevel = 0f;
                s.time = UnityEngine.Random.value * clip.length * 0.9f;
                s.Play();
                return s;
            }
            _cabinSmooth = Make("Cabin: road noise", _cabinSmoothRec.Clip);
            _cabinRough = Make("Cabin: road noise on cobblestones", _cabinRoughRec.Clip);
        }

        public void Tick(Soundscape sc, bool ownInside)
        {
            if (_car == null) return;
            var p = _car.position;
            if (_first) { _last = p; _first = false; }
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float v = Vector3.Distance(p, _last) / dt * 3.6f;
            _last = p;
            if (v > 250f) v = _speed;   // teleports
            float prev = _speed;
            _speed = Mathf.Lerp(_speed, v, 1f - Mathf.Exp(-dt * 4f));
            _accel = Mathf.Lerp(_accel, (_speed - prev) / 3.6f / dt, 1f - Mathf.Exp(-dt * 2f));
            float kmh = Mathf.Max(_speed, 0f);
            if (Time.time >= _nextSurface) { _nextSurface = Time.time + 0.25f; var surf = CoupledSim.Vehicles.RoadSurfaces.Under(_car); _surfaceTarget = surf.NoiseDb; _roughness = surf.Roughness; }
            _surfaceDb = Mathf.MoveTowards(_surfaceDb, _surfaceTarget, dt * 24f);   // (a new surface fades in over a third of a second)
            // load: cruising, more when accelerating, little when coasting or braking
            float throttle = Mathf.Clamp01(0.25f + _accel / 2f);
            _engineModel.Update(kmh, throttle);
            if (ownInside && !_cabinTried) MakeCabin(sc);

            // sound power (CNOSSOS-EU), then the level 1 m from a source on reflecting ground (L_W - 8 dB)
            var rollCat = _cat == VehicleCategory.Moped ? VehicleCategory.Light : _cat;
            bool rolls = _rollLo != null || _bikeSmooth != null;
            float lr = kmh < 2f || !rolls ? -100f : Cnossos.TotalRolling(rollCat, kmh, _surfaceDb, false);
            float lp = _prop == null && _layers == null && !_scooter ? -100f : Cnossos.TotalPropulsion(_cat, kmh, _electric, false);
            lp += _electric ? 0f : 6f * (throttle - 0.25f) - (kmh < 5f ? 6f : 0f);   // accelerating louder, idling quieter (an assumption)
            float r1 = Cnossos.PressureAt(lr, 1f), p1 = Cnossos.PressureAt(lp, 1f);
            if (_bicycle) r1 -= 24f;          // narrow tyres
            if (_scooter) { r1 -= 14f; p1 = Cnossos.PressureAt(Cnossos.TotalPropulsion(VehicleCategory.Light, Mathf.Max(kmh, 5f), true, false), 1f) - 22f + (kmh < 1f ? -60f : 0f); }

            // the path to the listener: distance (air absorption), buildings in the way
            var listener = sc.Listener;
            float extra = 0f, cutoff = 22000f, rollInside = 0f;
            if (ownInside)
            {
                // inside the participant's own car: tyres and engine through the floor and the firewall. A recorded
                // engine, set to the model's A-weighted level, loses 24 dB (bonnet, firewall and engine mounts: at a
                // steady speed the engine is then about as loud as the road, louder when accelerating); the
                // synthesised one, whose energy is mostly in its lowest harmonics, 16 dB
                for (int i = 0; i < _sources.Length; i++) _sources[i].spatialBlend = 0f;
                rollInside = -21f;
                p1 -= _layers != null ? 24f : 16f; cutoff = 900f;
            }
            else if (listener != null)
            {
                for (int i = 0; i < _sources.Length; i++) _sources[i].spatialBlend = 1f;
                float d = Vector3.Distance(listener.position, p);
                cutoff = AirAbsorption.Cutoff(d);
                if (Time.time >= _nextOcclusion)
                {
                    _nextOcclusion = Time.time + 0.3f;
                    var from = listener.position; var to = p + Vector3.up * 0.8f;
                    _occludedNow = Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore) && IsBuilding(hit.collider);
                }
                _occluded = Mathf.MoveTowards(_occluded, _occludedNow ? 1f : 0f, dt * 2f);
                extra -= 15f * _occluded;
                cutoff = Mathf.Min(cutoff, Mathf.Lerp(22000f, 700f, _occluded));
                if (sc.InsideCar) { extra -= 20f; cutoff = Mathf.Min(cutoff, 1000f); }   // heard from inside the participant's car
            }
            foreach (var f in _lp) f.cutoffFrequency = cutoff;

            // rough surfaces (bricks, cobblestones, gravel) mix in the recordings made on cobblestones (equal power)
            _rough = Mathf.MoveTowards(_rough, Mathf.Clamp01((_roughness - 0.3f) / 2.5f), dt * 3f);   // (over a third of a second)
            float rough = _rough, wSmooth = Mathf.Cos(rough * Mathf.PI / 2f), wRough = Mathf.Sin(rough * Mathf.PI / 2f);
            bool cabin = ownInside && _cabinSmooth != null;
            float t = Mathf.Clamp01((kmh - 30f) / 60f);
            if (_rollLo != null) _rollLo.volume = cabin ? 0f : sc.ClipGain(r1 + rollInside + extra + 10f * Mathf.Log10(Mathf.Max(1f - t, 1e-4f)) * (_rollHi != null ? 1f : 0f));
            if (_rollHi != null) _rollHi.volume = cabin ? 0f : sc.ClipGain(r1 + rollInside + extra + 10f * Mathf.Log10(Mathf.Max(t, 1e-4f)));
            if (_rollLo != null) _rollLo.pitch = 0.85f + 0.3f * Mathf.Clamp01(kmh / 120f);
            if (_cabinSmooth != null)
            {
                // the recordings were made inside cars: their A-weighted level is the model's through the floor (-21 dB) and the cabin's low-pass
                float a = cabin ? AShift(i => Cnossos.Rolling(rollCat, kmh, i, _surfaceDb), 900f) : 0f;
                _cabinSmooth.volume = cabin ? sc.RecGain(_cabinSmoothRec, r1 - 21f, a) * wSmooth : 0f;
                _cabinRough.volume = cabin ? sc.RecGain(_cabinRoughRec, r1 - 21f, a) * wRough : 0f;
                _cabinSmooth.pitch = _cabinRough.pitch = 0.8f + 0.4f * Mathf.Clamp01(kmh / 100f);
            }
            if (_bikeSmooth != null)
            {
                float a = AShift(i => Cnossos.Rolling(VehicleCategory.Light, kmh, i, _surfaceDb));
                _bikeSmooth.volume = sc.RecGain(_bikeSmoothRec, r1 + extra, a) * wSmooth;
                _bikeRough.volume = sc.RecGain(_bikeRoughRec, r1 + extra, a) * wRough;
                _bikeSmooth.pitch = _bikeRough.pitch = 0.8f + 0.4f * Mathf.Clamp01(kmh / 30f);
            }
            if (_prop != null) { _prop.volume = sc.ClipGain(p1 + extra - 4f); _prop.pitch = 0.8f + 0.5f * _engineModel.Rpm / _engineModel.MaxRpm; }
            if (_engine != null)
            {
                if (_electric || _scooter) { _engine.volume = sc.ClipGain(p1 + extra - 6f + (kmh < 1f ? -40f : 0f)); _engine.pitch = Mathf.Clamp(0.35f + kmh / 45f, 0.3f, 3f); }
                else { _engine.volume = sc.ClipGain(p1 + extra - 2.2f); _engine.pitch = Mathf.Clamp(_engineModel.FiringHz / SoundClips.EngineBaseHz, 0.2f, 3f); }
            }
            if (_layers != null)
            {
                // the recorded engine carries all the propulsion noise (tonal and broadband), at the model's A-weighted level;
                // the two recordings nearest the firing frequency are crossfaded (equal power, in log frequency) and pitched to it
                float a = AShift(i => Cnossos.Propulsion(_cat, kmh, i, false));
                float f = Mathf.Max(_engineModel.FiringHz, 1f);
                int k = 0;
                while (k < _layerRecs.Length - 1 && f >= _layerRecs[k + 1].firingHz) k++;
                float u = k >= _layerRecs.Length - 1 || f <= _layerRecs[k].firingHz ? 0f
                    : Mathf.Log(f / _layerRecs[k].firingHz) / Mathf.Log(_layerRecs[k + 1].firingHz / _layerRecs[k].firingHz);
                for (int i = 0; i < _layers.Length; i++)
                {
                    float w = i == k ? Mathf.Cos(u * Mathf.PI / 2f) : i == k + 1 ? Mathf.Sin(u * Mathf.PI / 2f) : 0f;
                    _layers[i].volume = w <= 1e-3f ? 0f : sc.RecGain(_layerRecs[i].rec, p1 + extra, a) * w;
                    _layers[i].pitch = Mathf.Clamp(f / _layerRecs[i].firingHz, 0.4f, 2.5f);
                }
            }
            if (SoundDebug && Time.time >= _nextDebug && (ownInside || _car == sc.Participant))
            {
                _nextDebug = Time.time + 2f;
                string L(AudioSource src, SoundClips.Recording r) => src == null || src.volume <= 0f ? "-" : $"{sc.ReferenceDb + 20f * Mathf.Log10(src.volume * SoundClips.Rms / (r != null ? r.Factor : 1f) / 0.7071f):F1}";
                string eng = _layers != null ? string.Join("/", _layers.Select((l, i) => L(l, _layerRecs[i].rec))) : L(_engine, null);
                Debug.Log($"Soundscape levels (dB, unweighted, at 1 m or 2D): {_car.name} {kmh:F0} km/h, {_engineModel.Rpm:F0} rpm, rough {_roughness:F1}; engine {eng}; " +
                    $"tyres {L(_rollLo, null)}/{L(_rollHi, null)}; cabin {L(_cabinSmooth, _cabinSmoothRec)}/{L(_cabinRough, _cabinRoughRec)}; bicycle {L(_bikeSmooth, _bikeSmoothRec)}/{L(_bikeRough, _bikeRoughRec)}, freewheel {L(_freewheel, _freewheelRec)}");
            }
            // a bicycle's freewheel ticks while it rolls without pedalling (recorded: a loop whose ticking follows the wheel)
            bool coasting = kmh > 6f && _accel < 0.05f && (_tw == null || _tw.Input.Drive <= 0.05f);   // (not while pedalling)
            float wheelHz = kmh / 3.6f / (2f * Mathf.PI * 0.34f);
            if (_freewheel != null)
            {
                _coast = Mathf.MoveTowards(_coast, coasting ? 1f : 0f, dt * 3f);
                _freewheel.volume = sc.RecGain(_freewheelRec, 40f + extra) * _coast;
                _freewheel.pitch = Mathf.Clamp(wheelHz * FreewheelClicksPerTurn / FreewheelRecordedClicks, 0.5f, 1.8f);
            }
            if (_clicks != null)
            {
                float rate = wheelHz * 24f;   // wheel turns per second x 24 pawl clicks
                _clickTimer -= dt;
                if (coasting && _clickTimer <= 0f)
                {
                    _clicks.spatialBlend = ownInside ? 0f : 1f;
                    _clicks.pitch = 0.95f + 0.1f * UnityEngine.Random.value;
                    _clicks.PlayOneShot(_clicks.clip, sc.ClipGain(46f + extra));
                    _clickTimer = 1f / Mathf.Max(rate, 1f);
                }
            }
        }

        /// <summary>Clicks per wheel turn of a city bike's freewheel (18 engagement points), and per second in the recording.</summary>
        const float FreewheelClicksPerTurn = 18f, FreewheelRecordedClicks = 26.3f;

        public void Dispose()
        {
            for (int i = 0; i < _sources.Length; i++) if (_sources[i] != null) Destroy(_sources[i].gameObject);
            foreach (var s in new[] { _clicks, _bikeSmooth, _bikeRough, _freewheel, _cabinSmooth, _cabinRough }) if (s != null) Destroy(s.gameObject);
        }
    }

    // ------------------------------------------------------------------ pedestrian signals

    /// <summary>Accessible pedestrian signals: a tick once a second at red (to find the pole), fast ticking at green.</summary>
    sealed class Tickers
    {
        readonly Dictionary<PedestrianTrafficLight, (AudioSource src, float next)> _on = new Dictionary<PedestrianTrafficLight, (AudioSource, float)>();
        readonly List<PedestrianTrafficLight> _keys = new List<PedestrianTrafficLight>();
        PedestrianTrafficLight[] _all = new PedestrianTrafficLight[0];
        float _nextScan;

        public void Tick(Soundscape sc, Vector3 at)
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                _all = FindObjectsByType<PedestrianTrafficLight>();
                var near = _all.Where(l => l != null && (l.transform.position - at).sqrMagnitude < 30f * 30f).OrderBy(l => (l.transform.position - at).sqrMagnitude).Take(6).ToList();
                foreach (var l in _on.Keys.Where(l => l == null || !near.Contains(l)).ToList()) { if (_on[l].src != null) Destroy(_on[l].src.gameObject); _on.Remove(l); }
                foreach (var l in near) if (!_on.ContainsKey(l)) _on[l] = (ForOneShots(Source3D(l.transform, "Signal ticker", SoundClips.Tick(), false, Vector3.up * 1.1f)), Time.time + UnityEngine.Random.value);
            }
            var env = EnvironmentController.Instance;
            float hour = env != null ? env.TimeOfDay : 13f;
            float level = 60f - (hour < 7f || hour > 22f ? 6f : 0f) + (sc.InsideCar ? -20f : 0f);   // quieter at night
            _keys.Clear();
            foreach (var k in _on.Keys) _keys.Add(k);
            for (int i = 0; i < _keys.Count; i++)
            {
                var l = _keys[i];
                if (l == null) continue;
                var (src, next) = _on[l];
                if (src == null || Time.time < next) continue;
                bool walk = l.State == LightState.GREEN || l.State == LightState.BLINK_GREEN;
                src.PlayOneShot(src.clip, sc.ClipGain(level));
                _on[l] = (src, Time.time + (walk ? 0.12f : 1f));
            }
        }

        public void Dispose()
        {
            foreach (var kv in _on.Values) if (kv.src != null) Destroy(kv.src.gameObject);
            _on.Clear();
        }
    }

    // ------------------------------------------------------------------ footsteps

    /// <summary>Footsteps of the participant (when walking) and of the pedestrians nearby, on the ground under them.</summary>
    sealed class Steps
    {
        sealed class Walker { public AudioSource Src; public Vector3 Last; public float Dist; public bool First = true; public int LastVariant = -1; }
        bool _reportedOwn;
        readonly Dictionary<Transform, Walker> _walkers = new Dictionary<Transform, Walker>();
        float _nextScan;

        public void Tick(Soundscape sc, Transform listener, PlayerAvatar me)
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                var want = new List<Transform>();
                if (me != null && me.Type == AvatarType.Pedestrian && me.GetComponent<SeatedPassenger>() == null) want.Add(me.transform);
                want.AddRange(FindObjectsByType<AIPedestrian>().Select(a => a.transform).Where(t => (t.position - listener.position).sqrMagnitude < 14f * 14f)
                    .OrderBy(t => (t.position - listener.position).sqrMagnitude).Take(6));
                foreach (var t in _walkers.Keys.Where(t => t == null || !want.Contains(t)).ToList()) { if (_walkers[t].Src != null) Destroy(_walkers[t].Src.gameObject); _walkers.Remove(t); }
                foreach (var t in want) if (!_walkers.ContainsKey(t)) _walkers[t] = new Walker { Src = ForOneShots(Source3D(t, "Footsteps", null, false, Vector3.up * 0.05f)) };
            }
            foreach (var kv in _walkers)
            {
                var t = kv.Key; var w = kv.Value;
                if (t == null || w.Src == null) continue;
                var p = t.position; p.y = 0f;
                if (w.First) { w.Last = p; w.First = false; continue; }
                float step = Vector3.Distance(p, w.Last), dt = Mathf.Max(Time.deltaTime, 1e-4f);
                w.Last = p;
                if (step / dt > 9f) continue;   // teleports
                w.Dist += step;
                float stride = step / dt > 2.5f ? 1.2f : 0.72f;
                if (w.Dist < stride) continue;
                w.Dist = 0f;
                string surface = Surface(t.position);
                bool own = me != null && t == me.transform;
                w.Src.spatialBlend = own ? 0.3f : 1f;
                w.Src.pitch = 0.92f + 0.16f * UnityEngine.Random.value;
                float db = (surface == "hard" ? 56f : surface == "soft" ? 48f : 54f) + (own ? -4f : 0f) + (sc.InsideCar ? -20f : 0f);
                int variant = UnityEngine.Random.Range(0, 4);
                if (variant == w.LastVariant) variant = (variant + 1) & 3;   // (never the same step twice in a row)
                w.LastVariant = variant;
                var rec = SoundClips.FootstepRecording(surface, variant);
                float vol = sc.RecGain(rec, db) * (0.85f + 0.3f * UnityEngine.Random.value);
                w.Src.PlayOneShot(rec != null ? rec.Clip : SoundClips.Footstep(surface, variant), vol);
                if (own && !_reportedOwn) { _reportedOwn = true; Debug.Log($"Soundscape: footsteps of the participant on {surface} ground ({(rec != null ? "recorded" : "synthesised")}), {db:F0} dB"); }
            }
        }

        public void Dispose()
        {
            foreach (var w in _walkers.Values) if (w.Src != null) Destroy(w.Src.gameObject);
            _walkers.Clear();
        }

        static string Surface(Vector3 at)
        {
            if (!Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)) return "hard";
            var n = hit.collider.name.ToLowerInvariant();
            var env = EnvironmentController.Instance;
            if (env != null && env.Snow > 0.3f) return "loose";
            if (n.Contains("green") || n.Contains("grass") || n.Contains("park")) return "soft";
            if (n.Contains("gravel") || n.Contains("unpaved")) return "loose";
            return "hard";
        }
    }
}
