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
/// Sound of the scene with levels in dB SPL (after calibration), made from models rather than recordings:
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
/// -cabinMusicDb 60, -audioReferenceDb 94, -birds 0, -signalTicks 0, -footsteps 0), which every agent reads.
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

    static AudioClip _tone;
    readonly Dictionary<Transform, VehicleVoice> _voices = new Dictionary<Transform, VehicleVoice>();
    readonly List<Transform> _vehicles = new List<Transform>();
    AudioSource _ambience, _rain, _roofRain, _wind, _music, _calibration;
    readonly List<AudioSource> _birds = new List<AudioSource>();
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
        var args = System.Environment.GetCommandLineArgs();
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
            }
        }
        if (Array.IndexOf(args, "-audioCalibration") >= 0) ToggleCalibration();
    }

    /// <summary>Linear gain of a 2D source playing a full-scale tone to be heard at <paramref name="db"/> dB SPL.</summary>
    public float Gain(float db) => Mathf.Clamp01(Mathf.Pow(10f, (db - ReferenceDb) / 20f));

    /// <summary>Gain of one of the <see cref="SoundClips"/> (RMS <see cref="SoundClips.Rms"/>) to be heard at
    /// <paramref name="db"/> dB SPL (2D), or at 1 m (3D sources, which fall off by 6 dB per doubling of distance).</summary>
    public float ClipGain(float db) => db < -50f ? 0f : Mathf.Clamp01(Mathf.Pow(10f, (db - ReferenceDb) / 20f) * 0.7071f / SoundClips.Rms);

    IEnumerator Start()
    {
        if (VehicleSounds)
        {
            _ambience = Source2D("Distant traffic", SoundClips.DistantTraffic());
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3)) ToggleCalibration();
        if (!VehicleSounds) return;
        var listener = FindListener();
        if (listener == null) return;
        var me = NetworkingManager.Instance != null && NetworkingManager.Instance.PlayerSystem != null ? NetworkingManager.Instance.PlayerSystem.LocalPlayer : null;
        OwnVehicle = me != null && (me.Type == AvatarType.Driver) ? me.transform : null;
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
        if (_ambience != null) _ambience.volume = AmbienceDb == 0f ? 0f : ClipGain(amb + (InsideCar ? -12f : 0f));
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
            while (_birds.Count < want) { var s = Source3D(transform, "Bird", SoundClips.Birds(_birds.Count), true); s.time = UnityEngine.Random.value * 8f; s.pitch = 0.9f + 0.2f * UnityEngine.Random.value; s.Play(); _birds.Add(s); }
            for (int i = 0; i < _birds.Count; i++)
            {
                if (i < want && near.Count > 0) { var t = near[(i * 3 + UnityEngine.Random.Range(0, 3)) % near.Count]; _birds[i].transform.position = t.position + Vector3.up * (3f + 4f * UnityEngine.Random.value); }
                else _birds[i].transform.position = at + Vector3.up * 500f;   // (out of earshot)
            }
        }
        foreach (var b in _birds) b.volume = act <= 0f ? 0f : ClipGain(62f + 10f * Mathf.Log10(act + 1e-3f) + (InsideCar ? -20f : 0f));
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
        Vector3 _last;
        float _speed, _accel, _surfaceDb, _nextSurface, _nextOcclusion, _occluded, _clickTimer;
        bool _first = true, _occludedNow;

        public float Kmh => _speed;
        public float RollingVolume => _rollLo != null ? _rollLo.volume + (_rollHi != null ? _rollHi.volume : 0f) : 0f;
        public float PropulsionVolume => (_prop != null ? _prop.volume : 0f) + (_engine != null ? _engine.volume : 0f);

        public VehicleVoice(Transform car, Soundscape sc)
        {
            _car = car;
            var tw = car.GetComponent<TwoWheelerController>();
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
            var srcs = new List<AudioSource>(4);
            AudioSource Add(string name, AudioClip clip)
            {
                var s = Source3D(car, name, clip, true, new Vector3(0f, 0.4f, 0f));
                s.time = UnityEngine.Random.value * clip.length * 0.9f;
                lps.Add(s.gameObject.AddComponent<AudioLowPassFilter>());
                srcs.Add(s);
                s.Play();
                return s;
            }
            if (_bicycle)
            {
                _rollLo = Add("Tyres", SoundClips.Rolling(VehicleCategory.Light, 30f));
                _clicks = Source3D(car, "Freewheel", SoundClips.Freewheel(), false, new Vector3(0f, 0.5f, 0f));
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
                _prop = Add("Propulsion", SoundClips.PropulsionNoise(_cat, _electric));
                _engine = Add(_electric ? "Motor" : "Engine", _electric ? SoundClips.Whine() : SoundClips.Engine(_cat == VehicleCategory.Moped ? "twostroke" : _cat == VehicleCategory.Light ? "petrol" : "diesel"));
            }
            _sources = srcs.ToArray();
            _lp = lps.ToArray();
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
            if (Time.time >= _nextSurface) { _nextSurface = Time.time + 0.25f; _surfaceDb = CoupledSim.Vehicles.RoadSurfaces.Under(_car).NoiseDb; }
            // load: cruising, more when accelerating, little when coasting or braking
            float throttle = Mathf.Clamp01(0.25f + _accel / 2f);
            _engineModel.Update(kmh, throttle);

            // sound power (CNOSSOS-EU), then the level 1 m from a source on reflecting ground (L_W - 8 dB)
            float lr = kmh < 2f || _rollLo == null ? -100f : Cnossos.TotalRolling(_cat == VehicleCategory.Moped ? VehicleCategory.Light : _cat, kmh, _surfaceDb, false);
            float lp = _prop == null && !_scooter ? -100f : Cnossos.TotalPropulsion(_cat, kmh, _electric, false);
            lp += _electric ? 0f : 6f * (throttle - 0.25f) - (kmh < 5f ? 6f : 0f);   // accelerating louder, idling quieter (an assumption)
            float r1 = Cnossos.PressureAt(lr, 1f), p1 = Cnossos.PressureAt(lp, 1f);
            if (_bicycle) r1 -= 24f;          // narrow tyres
            if (_scooter) { r1 -= 14f; p1 = Cnossos.PressureAt(Cnossos.TotalPropulsion(VehicleCategory.Light, Mathf.Max(kmh, 5f), true, false), 1f) - 22f + (kmh < 1f ? -60f : 0f); }

            // the path to the listener: distance (air absorption), buildings in the way
            var listener = sc.Listener;
            float extra = 0f, cutoff = 22000f;
            if (ownInside)
            {
                // inside the participant's own car: tyres and engine through the floor and the firewall
                for (int i = 0; i < _sources.Length; i++) _sources[i].spatialBlend = 0f;
                r1 -= 21f; p1 -= 16f; cutoff = 900f;
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

            float t = Mathf.Clamp01((kmh - 30f) / 60f);
            if (_rollLo != null) _rollLo.volume = sc.ClipGain(r1 + extra + 10f * Mathf.Log10(Mathf.Max(1f - t, 1e-4f)) * (_rollHi != null ? 1f : 0f));
            if (_rollHi != null) _rollHi.volume = sc.ClipGain(r1 + extra + 10f * Mathf.Log10(Mathf.Max(t, 1e-4f)));
            if (_rollLo != null) _rollLo.pitch = 0.85f + 0.3f * Mathf.Clamp01(kmh / 120f);
            if (_prop != null) { _prop.volume = sc.ClipGain(p1 + extra - 4f); _prop.pitch = 0.8f + 0.5f * _engineModel.Rpm / _engineModel.MaxRpm; }
            if (_engine != null)
            {
                if (_electric || _scooter) { _engine.volume = sc.ClipGain(p1 + extra - 6f + (kmh < 1f ? -40f : 0f)); _engine.pitch = Mathf.Clamp(0.35f + kmh / 45f, 0.3f, 3f); }
                else { _engine.volume = sc.ClipGain(p1 + extra - 2.2f); _engine.pitch = Mathf.Clamp(_engineModel.FiringHz / SoundClips.EngineBaseHz, 0.2f, 3f); }
            }
            // a bicycle's freewheel clicks while it rolls without pedalling
            if (_clicks != null)
            {
                bool coasting = kmh > 6f && _accel < 0.05f;
                float rate = kmh / 3.6f / (2f * Mathf.PI * 0.34f) * 24f;   // wheel turns per second x 24 pawl clicks
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

        public void Dispose()
        {
            for (int i = 0; i < _sources.Length; i++) if (_sources[i] != null) Destroy(_sources[i].gameObject);
            if (_clicks != null) Destroy(_clicks.gameObject);
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
                foreach (var l in near) if (!_on.ContainsKey(l)) _on[l] = (Source3D(l.transform, "Signal ticker", SoundClips.Tick(), false, Vector3.up * 1.1f), Time.time + UnityEngine.Random.value);
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
        sealed class Walker { public AudioSource Src; public Vector3 Last; public float Dist; public bool First = true; }
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
                foreach (var t in want) if (!_walkers.ContainsKey(t)) _walkers[t] = new Walker { Src = Source3D(t, "Footsteps", null, false, Vector3.up * 0.05f) };
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
                w.Src.PlayOneShot(SoundClips.Footstep(surface, UnityEngine.Random.Range(0, 4)), sc.ClipGain(db));
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
