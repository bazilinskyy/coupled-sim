using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public enum Weather
{
    Clear,
    PartlyCloudy,
    Overcast,
    LightRain,
    HeavyRain,
    Fog,
    LightSnow,
    HeavySnow,
}

/// <summary>
/// Time of day and weather of an experiment. Time of day sets the sun from the solar position
/// (latitude, day of year), the sky, ambient light and street lights; weather sets clouds (sky
/// tint, sun strength), fog, rain and wet roads.
///
/// Settable per trial as experiment parameters (it is an IExperimentModifier):
/// time ("17:30" or 17.5), weather (Clear, PartlyCloudy, Overcast, LightRain, HeavyRain, Fog, LightSnow, HeavySnow),
/// snow (cover on the ground 0..1), wind (m/s) and winddirection (compass degrees it comes from),
/// fog (density, e.g. 0.02), rain (0..1), timeSpeed (multiplier of real time; 0 = fixed),
/// realWeather (a date and local time, e.g. 2025-06-01T14:00: the weather that really was there then, see RealWeather).
/// The host sends the state to all clients, so every participant sees the same conditions.
/// </summary>
public class EnvironmentController : MonoBehaviour, IExperimentModifier
{
    [Header("Time of day")]
    [Tooltip("Clock time (hours) in the time zone given by UtcOffset.")]
    [Range(0f, 24f)] public float TimeOfDay = 13f;
    [Tooltip("Latitude and longitude in degrees (Eindhoven 51.44, 5.48; Delft 52.01, 4.36).")]
    public float Latitude = 51.44f;
    public float Longitude = 5.48f;
    [Tooltip("Hours ahead of UTC of the clock time (CET 1, CEST 2).")]
    public float UtcOffset = 2f;
    /// <summary>The city map's place, whose clock (summer time or not) follows <see cref="DayOfYear"/>.</summary>
    MapInfo _clockZone; bool _hasClockZone;
    [Range(1, 365)] public int DayOfYear = 172;
    [Tooltip("How fast simulated time runs relative to real time; 0 keeps the time fixed.")]
    public float TimeSpeed = 0f;
    [Tooltip("Compass direction (degrees) of the scene's +z axis; 0 = north.")]
    public float NorthOffset = 0f;

    [Header("Weather")]
    public Weather Weather = Weather.PartlyCloudy;
    [Tooltip("Extra fog density on top of the weather's own (exponential squared).")]
    public float ExtraFog = 0f;
    [Tooltip("Rain intensity override (0..1); negative: from the weather.")]
    public float RainOverride = -1f;
    [Tooltip("Snow on the ground (0..1: grass, pavements and roofs white, slush on the roads, less grip); negative: from the weather (falling snow covers it).")]
    public float SnowCover = -1f;
    [Tooltip("Wind speed (m/s, at 10 m): trees sway, rain and snow slant.")]
    public float WindSpeed = 2f;
    [Tooltip("Where the wind comes from (compass degrees: 270 = from the west).")]
    public float WindDirection = 240f;

    [Header("Lighting")]
    [Tooltip("Keep the scene's baked lightmaps and sun as they are (as in earlier experiments): only fog, rain, wet roads and street lights change. Off: the sun follows the time of day with realtime lighting.")]
    public bool UseBakedLighting = false;

    [Header("Scene objects (found automatically when empty)")]
    public Light Sun;
    public List<Light> StreetLights = new List<Light>();
    [Tooltip("Add a light to every street lamp (objects whose name starts with this) that switches on at dusk; empty: off.")]
    public string LampNamePrefix = "StreetLamp";
    public float LampIntensity = 6f;
    public float LampRange = 18f;
    [Tooltip("Only the street lights nearest to the viewer are switched on (performance); 0 = all.")]
    public int MaxActiveStreetLights = 48;
    [Tooltip("Material name parts of surfaces that get wet in the rain.")]
    public string[] WetSurfaceKeywords = { "road", "asphalt", "lane", "street", "sidewalk", "pavement", "crossing", "tile" };

    [Header("Reflections")]
    [Tooltip("A realtime reflection probe above the viewer, renewed when the time of day or weather changes or the viewer has moved: reflections in car paint, windows and wet roads show the actual sky and surroundings (dark at night, with the street lights). The scene's own reflection probes still apply inside their areas.")]
    public bool LiveReflections = true;
    [Tooltip("Size of each face of the live reflection cube (pixels): 64 to 256.")]
    public int LiveReflectionResolution = 128;
    [Tooltip("The live reflections are renewed when the viewer has moved this far (metres).")]
    public float LiveReflectionDistance = 30f;

    [Header("State (read only)")]
    public float SunElevation;
    public float Rain;
    public float Snowfall;
    public float Snow;
    public bool StreetLightsOn;

    /// <summary>The wind in the scene (m/s), towards where it blows (horizontal).</summary>
    public Vector3 Wind
    {
        get
        {
            float yaw = (WindDirection + 180f - NorthOffset) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * Mathf.Max(0f, WindSpeed);
        }
    }

    Material _sky;
    ParticleSystem _rain, _snow;
    Transform _rainFollow;
    readonly List<Renderer> _wetRenderers = new List<Renderer>();
    readonly List<Renderer> _snowRenderers = new List<Renderer>();
    [Tooltip("Material name parts of surfaces that snow covers (roads get slush).")]
    public string[] SnowSurfaceKeywords = { "grass", "green", "roof", "pavement", "paving", "sidewalk", "ground", "kerb", "gravel", "cycle", "tile" };
    MaterialPropertyBlock _mpb;
    readonly List<ReflectionProbe> _probes = new List<ReflectionProbe>();
    bool _initialised;
    float _appliedTime = -1;
    Weather _appliedWeather;
    float _appliedFog = -1, _appliedRain = -2, _appliedSnow = -2, _appliedSnowfall = -2, _appliedWind = -1;

    /// <summary>The least fog density (exponential squared): the haze of clear air.</summary>
    public const float MinHaze = 0.00028f;

    public static EnvironmentController Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    // ---- overcast sky: fog does not apply to the skybox, so under heavy clouds, rain and fog the
    // procedural sky would stay blue above the horizon. Cameras then clear to the fog colour.

    /// <summary>True when the sky is drawn as a uniform overcast grey (the fog colour).</summary>
    public bool OvercastSky { get; private set; }
    readonly HashSet<Camera> _overcastCameras = new HashSet<Camera>();

    void OnEnable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += BeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += EndCamera;
    }

    void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= EndCamera;
    }

    void BeginCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
    {
        if (!OvercastSky || cam.clearFlags != CameraClearFlags.Skybox || cam.cameraType == CameraType.SceneView) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;
        _overcastCameras.Add(cam);
    }

    void EndCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
    {
        if (!_overcastCameras.Remove(cam) || cam == null) return;
        cam.clearFlags = CameraClearFlags.Skybox;
    }

    void Start()
    {
        Init();
        Apply(true);
    }

    void Init()
    {
        if (_initialised) return;
        _initialised = true;
        // in a city map: that city's location and time zone
        var map = OsmMap.Current;
        if (map != null && map.Generator != null)
        {
            Latitude = (float)map.Info.Latitude;
            Longitude = (float)map.Info.Longitude;
            UtcOffset = map.Info.UtcOffsetJune;
            _clockZone = map.Info; _hasClockZone = true;
        }
        if (Sun == null)
        {
            Sun = RenderSettings.sun;
            if (Sun == null)
            {
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { Sun = l; break; }
            }
        }
        if (StreetLights.Count == 0)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == Sun || l.type == LightType.Directional) continue;
                var n = (l.name + " " + (l.transform.parent != null ? l.transform.parent.name : "")).ToLower();
                if (n.Contains("street") || n.Contains("lamp") || n.Contains("light")) StreetLights.Add(l);
            }
        }
        if (!UseBakedLighting) MakeLightingDynamic();
        if (!string.IsNullOrEmpty(LampNamePrefix)) CreateLampLights();
        _sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Environment sky" };
        if (!UseBakedLighting) RenderSettings.skybox = _sky;
        if (Sun != null) RenderSettings.sun = Sun;
        _mpb = new MaterialPropertyBlock();
        foreach (var r in FindObjectsByType<MeshRenderer>())
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                var n = m.name.ToLower();
                bool wet = false;
                foreach (var k in WetSurfaceKeywords) if (n.Contains(k)) { wet = true; break; }
                // city-map road surfaces (OsmMapGenerator)
                if (n.Contains("paving") || n.Contains("cobble") || n.Contains("manhole") || n.Contains("patch") || n.Contains("kerb")) wet = true;
                if (wet && m.HasProperty("_Smoothness")) { _wetRenderers.Add(r); break; }
            }
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !m.HasProperty("_BaseMap")) continue;
                var n = m.name.ToLower();
                bool snow = false;
                foreach (var k in SnowSurfaceKeywords) if (n.Contains(k)) { snow = true; break; }
                if (snow && !n.Contains("facade") && !n.Contains("ground floor")) { _snowRenderers.Add(r); break; }
            }
        }
        CreateRain();
        CreateSnow();
        // -liveReflections 0: off (comparisons, slow PCs)
        var args = CommandLine.Args;
        int li = System.Array.IndexOf(args, "-liveReflections");
        if (li >= 0 && li + 1 < args.Length) LiveReflections = args[li + 1] != "0";
        if (LiveReflections) CreateLiveReflections();
        // the viewer's own vehicle out of the live reflections, the people in it out of the cabin's (both with or
        // without live reflections: the cabin probe is made either way)
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += HideOwnVehicle;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += ShowOwnVehicle;
    }

    ReflectionProbe _live;
    int _liveRender = -1;
    Vector3 _liveAt = new Vector3(1e9f, 0f, 0f);
    bool _liveDirty = true;
    float _nextLiveCheck;

    void CreateLiveReflections()
    {
        var go = new GameObject("Live reflections");
        go.transform.SetParent(transform, false);
        _live = go.AddComponent<ReflectionProbe>();
        _live.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        _live.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
        _live.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
        _live.resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(LiveReflectionResolution, 64, 256));
        _live.hdr = true;
        _live.size = new Vector3(4000f, 1000f, 4000f);   // everywhere; the scene's probes (more important) win inside their boxes
        _live.boxProjection = false;
        _live.importance = 0;
        _live.nearClipPlane = 0.5f;
        _live.farClipPlane = 800f;
        _live.shadowDistance = 60f;
        _live.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
    }

    // the viewer's own vehicle (or body) is left out of the live reflections: seen from just above, its roof would
    // fill the probe's lower half and show up as a smear of its own colour in its windscreen
    Transform _ownRoot;
    readonly List<Renderer> _own = new List<Renderer>();
    readonly List<Renderer> _hidden = new List<Renderer>();

    // the viewer's cabin has its own small probe, rendered with the car in it: from the driver's seat the
    // windscreen then reflects the dashboard, as a real one does, instead of the road far below the outdoor probe
    ReflectionProbe _cabin;
    int _cabinRender = -1;
    bool _cabinDirty = true;
    bool CabinRendering => _cabin != null && _cabinRender >= 0 && !_cabin.IsFinishedRendering(_cabinRender);
    bool LiveRendering => _live != null && _liveRender >= 0 && !_live.IsFinishedRendering(_liveRender);
    Vector3 _cabinAt = new Vector3(1e9f, 0f, 0f);

    static readonly Collider[] _around = new Collider[8];

    /// <summary>The vehicle (or body) the viewer sits in: the camera's own root, or (replays, external cameras)
    /// the vehicle whose collider holds the camera. Null when the viewer is in none.</summary>
    static Transform ViewerVehicle(Camera main)
    {
        if (main == null) return null;
        var root = main.transform.root;
        // a passenger seated in another participant's car: that car (its own avatar only rides along)
        var seated = root.GetComponent<SeatedPassenger>();
        if (seated != null && seated.Car != null) return seated.Car;
        if (root != main.transform && root.GetComponent<Rigidbody>() != null) return root;
        int n = Physics.OverlapSphereNonAlloc(main.transform.position, 0.2f, _around, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (_around[i].attachedRigidbody != null) return _around[i].attachedRigidbody.transform.root;
        // replays: the vehicles are moved without physics; the nearest participant vehicle around the camera
        Transform best = null; float bestD = 9f;
        foreach (var a in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
        {
            float d = (a.transform.position - main.transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = a.transform; }
        }
        if (best != null) return best;
        return root != main.transform ? root : null;
    }

    void UpdateCabinReflections()
    {
        var main = ViewerOverride != null ? ViewerOverride.GetComponent<Camera>() : ViewerCamera();
        var root = ViewerVehicle(main);
        bool inVehicle = root != null && (root.GetComponent<Rigidbody>() != null || root.GetComponent<PlayerAvatar>() != null);
        if (!inVehicle) { if (_cabin != null) _cabin.gameObject.SetActive(false); return; }
        if (_cabin == null)
        {
            var go = new GameObject("Cabin reflections");
            _cabin = go.AddComponent<ReflectionProbe>();
            _cabin.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            _cabin.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            _cabin.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            _cabin.resolution = 64;
            _cabin.hdr = true;
            _cabin.size = new Vector3(2.6f, 1.8f, 3.2f);   // about a car's cabin around the eyes
            // not box-projected: the box of a probe is always aligned with the world's axes, not with the car, and fitted
            // to no cabin; projected onto it, the bright displays in the dashboard (the smart's yellow trip computer) came
            // out as a large yellow streak high on the windscreen. Unprojected, the cabin reflects as dark and soft as seen
            // from the eyes.
            _cabin.boxProjection = false;
            _cabin.importance = 10;                        // wins over the outdoor probe inside the cabin
            _cabin.blendDistance = 0.1f;
            _cabin.nearClipPlane = 0.05f;
            _cabin.farClipPlane = 300f;
            _cabin.shadowDistance = 20f;
            _cabin.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            _cabinDirty = true;
        }
        _cabin.gameObject.SetActive(true);
        if (_cabin.transform.parent != root) { _cabin.transform.SetParent(root, false); _cabinDirty = true; }
        _cabin.transform.position = main.transform.position;
        _cabin.transform.rotation = root.rotation;
        // The in-car displays (world-space canvases: the instrument cluster, a centre screen) are left out of the cabin's
        // reflections: unlit and bright against a dark cabin, they came out of the small, blurry probe as large coloured
        // blobs on the windscreen above the dashboard and in the side window by the mirror. The windscreen still reflects
        // the dark dashboard. (They are drawn on the UI layer, which every camera shows and the cabin probe leaves out.)
        int ui = LayerMask.NameToLayer("UI");
        if (ui >= 0)
        {
            _cabin.cullingMask = ~(1 << ui);
            foreach (var c in root.GetComponentsInChildren<Canvas>())
                if (c.renderMode == RenderMode.WorldSpace)
                    foreach (var t in c.GetComponentsInChildren<Transform>(true))
                        if (t.gameObject.layer != ui) { t.gameObject.layer = ui; _cabinDirty = true; }
        }
        if (_debugProbes) LogProbes(main, root);
        if (_cabinDirty && !LiveRendering && !CabinRendering)
        {
            _cabinDirty = false;
            _cabinRender = _cabin.RenderProbe();
        }
    }

    // -debugProbes: which reflection probes the road ahead and the viewer's own glass use (every 2 s)
    static readonly bool _debugProbes = Array.IndexOf(CommandLine.Args, "-debugProbes") >= 0;
    float _nextProbeLog;
    readonly List<UnityEngine.Rendering.ReflectionProbeBlendInfo> _blend = new List<UnityEngine.Rendering.ReflectionProbeBlendInfo>();
    void LogProbes(Camera main, Transform root)
    {
        if (Time.unscaledTime < _nextProbeLog) return;
        _nextProbeLog = Time.unscaledTime + 2f;
        string Info(Renderer r)
        {
            if (r == null) return "-";
            r.GetClosestReflectionProbes(_blend);
            return r.name + ": " + string.Join(", ", _blend.Select(b => $"{(b.probe != null ? b.probe.name : "?")} {b.weight:F2}"));
        }
        var parts = new List<string>();
        foreach (var d in new[] { 6f, 15f })
        {
            var from = main.transform.position + Vector3.ProjectOnPlane(main.transform.forward, Vector3.up).normalized * d + Vector3.up * 3f;
            if (Physics.Raycast(from, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                parts.Add($"ground {d} m ahead {Info(hit.collider.GetComponent<Renderer>())}");
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var n = r.name.ToLowerInvariant();
            if (n.Contains("glass") || n.Contains("window") || n.Contains("wind") || n.Contains("dash")) parts.Add("own " + Info(r));
        }
        Debug.Log($"ProbeDebug cabin importance {_cabin.importance}: " + string.Join(" | ", parts));
    }

    void HideOwnVehicle(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
    {
        if (cam.cameraType != CameraType.Reflection) return;
        if (CabinRendering)
        {
            // The cabin probe sits at the viewer's eyes, inside the head of the figure in that seat: the figures' bodies
            // (shirt and skin) a few centimetres away filled its small, blurry cube and showed as a see-through
            // pink-and-blue smear high on the windscreen on the passenger's side. The people are left out of it; the
            // windscreen still reflects the dashboard. (A real windscreen shows the people in it only faintly.)
            var car = _cabin.transform.parent;
            if (car == null) return;
            _hidden.Clear();
            foreach (var an in car.GetComponentsInChildren<Animator>())
                if (an != null && an.isHuman)
                    foreach (var r in an.GetComponentsInChildren<Renderer>())
                        if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; _hidden.Add(r); }
            return;
        }
        if (_live == null) return;
        var root = _cabin != null && _cabin.gameObject.activeSelf && _cabin.transform.parent != null ? _cabin.transform.parent : ViewerVehicle(ViewerCamera());
        if (root == null) return;
        if (root != _ownRoot) { _ownRoot = root; _own.Clear(); root.GetComponentsInChildren(true, _own); }
        _hidden.Clear();
        foreach (var r in _own)
            if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; _hidden.Add(r); }
    }

    void ShowOwnVehicle(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != null && cam.cameraType == CameraType.Reflection) RestoreOwnVehicle();
    }

    void RestoreOwnVehicle()
    {
        foreach (var r in _hidden) if (r != null) r.forceRenderingOff = false;
        _hidden.Clear();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= HideOwnVehicle;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= ShowOwnVehicle;
        RestoreOwnVehicle();
        if (_sky != null) Destroy(_sky);
    }

    /// <summary>True when the live and cabin reflection probes show the viewer's present place. Pictures taken right
    /// after the camera jumped (the replay's shots) wait for it: the probes are rendered a face per frame, and the
    /// windscreen otherwise reflected what the probe had seen from the previous camera position (a green sheet over a
    /// shop front in the replay's own view).</summary>
    public bool ReflectionsCurrent
    {
        get
        {
            if (CabinRendering || LiveRendering) return false;
            if (_cabin != null && _cabin.gameObject.activeSelf && _cabinDirty) return false;
            if (_live == null) return true;
            var v = ViewerPosition();
            return !_liveDirty && (v - _liveAt).sqrMagnitude < LiveReflectionDistance * LiveReflectionDistance;
        }
    }

    // above the viewer (out of their own car), when the light changed or the viewer moved on
    void UpdateLiveReflections()
    {
        if (Time.unscaledTime < _nextLiveCheck) return;
        _nextLiveCheck = Time.unscaledTime + 0.5f;
        if (_live == null)
        {
            // -liveReflections 0: the cabin still gets its probe. Without it the glass of the viewer's own car reflected
            // the scene's probes, baked outdoors: a see-through grey-brown sheet over the windscreen (with a hard edge
            // where its box ends) and the side windows, and over the wing mirror behind them.
            var at = ViewerPosition();
            if ((at - _cabinAt).sqrMagnitude > LiveReflectionDistance * LiveReflectionDistance) { _cabinAt = at; _cabinDirty = true; }
            UpdateCabinReflections();
            return;
        }
        if (_liveRender >= 0 && !_live.IsFinishedRendering(_liveRender)) return;
        UpdateCabinReflections();
        if (CabinRendering) return;
        var viewer = ViewerPosition();
        if (!_liveDirty && (viewer - _liveAt).sqrMagnitude < LiveReflectionDistance * LiveReflectionDistance) return;
        _live.transform.position = viewer + Vector3.up * 3.5f;
        _liveAt = viewer;
        _liveDirty = false;
        _cabinDirty = true;    // the light outside changed or the viewer moved on: the cabin's view out too
        _liveRender = _live.RenderProbe();
    }

    void Update()
    {
        if (TimeSpeed > 0f)
        {
            TimeOfDay = Mathf.Repeat(TimeOfDay + Time.deltaTime * TimeSpeed / 3600f, 24f);
        }
        Apply(false);
        FollowCamera();
        UpdateStreetLights(false);
        UpdateLiveReflections();
    }

    float _nextLampUpdate;
    readonly List<(float d, Light l)> _lampOrder = new List<(float, Light)>();

    // Switches on the street lights nearest to the viewer (a city's worth of lights at once is too much for most GPUs).
    void UpdateStreetLights(bool force)
    {
        if (!force && Time.time < _nextLampUpdate) return;
        _nextLampUpdate = Time.time + 0.5f;
        foreach (var g in _lampGlows) if (g != null && g.activeSelf != StreetLightsOn) g.SetActive(StreetLightsOn);
        if (!StreetLightsOn || MaxActiveStreetLights <= 0)
        {
            foreach (var l in StreetLights) if (l != null) l.enabled = StreetLightsOn;
            return;
        }
        var viewer = ViewerPosition();
        _lampOrder.Clear();
        foreach (var l in StreetLights)
            if (l != null) _lampOrder.Add(((l.transform.position - viewer).sqrMagnitude, l));
        _lampOrder.Sort((a, b) => a.d.CompareTo(b.d));
        for (int i = 0; i < _lampOrder.Count; i++) _lampOrder[i].l.enabled = i < MaxActiveStreetLights;
        if (!_lampLogged && _lampOrder.Count > 0)
        {
            _lampLogged = true;
            var near = _lampOrder.Take(4).Select(e => $"{e.l.name} under {(e.l.transform.parent != null ? e.l.transform.parent.name : "-")} {Mathf.Sqrt(e.d):F0} m");
            Debug.Log($"EnvironmentController: street lights on near {viewer}: {string.Join("; ", near)}");
        }
    }
    bool _lampLogged;

    /// <summary>A camera that renders the scene by itself (a video's camera while it records): the street lights and
    /// the like follow it instead of the screen's view.</summary>
    public static Transform ViewerOverride;

    /// <summary>Where the viewer is (see <see cref="ViewerPosition"/>), for anything that keeps lights near the view.</summary>
    public static Vector3 Viewer => Instance != null ? Instance.ViewerPosition() : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

    Vector3 ViewerPosition()
    {
        if (ViewerOverride != null) return ViewerOverride.position;
        var cam = ViewerCamera();
        return cam != null ? cam.transform.position : transform.position;
    }

    /// <summary>
    /// The camera that draws the world on the screen: the highest depth with the default layer, not an overlay that draws
    /// only the interface. (Not always Camera.main: a passenger seated in another participant's car sees through its
    /// avatar's own camera.)
    /// </summary>
    static Camera ViewerCamera()
    {
        Camera cam = null;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null && (c.cullingMask & 1) != 0 && (cam == null || c.depth > cam.depth)) cam = c;
        return cam != null ? cam : Camera.main;
    }

    // ------------------------------------------------------------------ parameters and network

    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters)
    {
        foreach (var p in experimentParameters)
        {
            var name = p.name.ToLower();
            if (name == "time" || name == "timeofday") TimeOfDay = ParseTime(p.value);
            else if (name == "weather")
            {
                var v = p.value.Trim().ToLower();
                if (v == "rain" || v == "rainy") v = "LightRain";
                else if (v == "snow" || v == "snowy") v = "LightSnow";
                else if (v == "cloudy") v = "Overcast";
                else if (v == "foggy") v = "Fog";
                if (Enum.TryParse(v, true, out Weather w)) Weather = w;
                else Debug.LogWarning($"Weather '{p.value}' unknown: use {string.Join(", ", Enum.GetNames(typeof(Weather)))}");
            }
            else if (name == "fog") ExtraFog = ParseFloat(p.value);
            else if (name == "rain") RainOverride = ParseFloat(p.value);
            else if (name == "snow" || name == "snowcover") SnowCover = ParseFloat(p.value);
            else if (name == "wind" || name == "windspeed") WindSpeed = ParseFloat(p.value);
            else if (name == "winddirection") WindDirection = ParseFloat(p.value);
            else if (name == "timespeed") TimeSpeed = ParseFloat(p.value);
            else if (name == "dayofyear") DayOfYear = (int)ParseFloat(p.value);
            // (date=12-15 or 2026-12-15: that day's sun, and the city's clock on it)
            else if (name == "date")
            {
                var parts = p.value.Trim().Split('-');
                int y = parts.Length == 3 && int.TryParse(parts[0], out var yy) ? yy : 2025;
                if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 2], out var mo) && int.TryParse(parts[parts.Length - 1], out var dd)
                    && mo >= 1 && mo <= 12 && dd >= 1 && dd <= DateTime.DaysInMonth(y, mo))
                    DayOfYear = new DateTime(y, mo, dd).DayOfYear;
                else Debug.LogWarning($"Date '{p.value}' unknown: use MM-DD or YYYY-MM-DD");
            }
            else if (name == "latitude") Latitude = ParseFloat(p.value);
            else if (name == "realweather")
            {
                // the weather that really was then and there (the city map's place, if it is built already)
                var map = OsmMap.Current;
                if (map != null && map.Generator != null) { Latitude = (float)map.Info.Latitude; Longitude = (float)map.Info.Longitude; UtcOffset = map.Info.UtcOffsetJune; _clockZone = map.Info; _hasClockZone = true; }
                RealWeather.Apply(this, p.value);
            }
        }
    }

    static float ParseFloat(string s, float fallback = 0f)
    {
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        Debug.LogWarning($"EnvironmentController: invalid number '{s}'");
        return fallback;
    }

    static float ParseTime(string s)
    {
        if (!string.IsNullOrEmpty(s) && s.Contains(":"))
        {
            var parts = s.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hh) && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mm))
                return hh + mm / 60f;
            Debug.LogWarning($"EnvironmentController: invalid time '{s}'");
            return 13f;
        }
        return ParseFloat(s, 13f);
    }

    public struct EnvironmentStateMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_Environment;
        public float TimeOfDay, TimeSpeed, ExtraFog, RainOverride, Latitude, SnowCover, WindSpeed, WindDirection;
        public int Weather, DayOfYear;

        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref TimeOfDay);
            synchronizer.Sync(ref TimeSpeed);
            synchronizer.Sync(ref ExtraFog);
            synchronizer.Sync(ref RainOverride);
            synchronizer.Sync(ref Latitude);
            synchronizer.Sync(ref Weather);
            synchronizer.Sync(ref DayOfYear);
            synchronizer.Sync(ref SnowCover);
            synchronizer.Sync(ref WindSpeed);
            synchronizer.Sync(ref WindDirection);
        }
    }

    public EnvironmentStateMsg GetState() => new EnvironmentStateMsg
    {
        TimeOfDay = TimeOfDay, TimeSpeed = TimeSpeed, ExtraFog = ExtraFog, RainOverride = RainOverride,
        Latitude = Latitude, Weather = (int)Weather, DayOfYear = DayOfYear, SnowCover = SnowCover, WindSpeed = WindSpeed, WindDirection = WindDirection,
    };

    public void SetState(EnvironmentStateMsg s)
    {
        TimeOfDay = s.TimeOfDay; TimeSpeed = s.TimeSpeed; ExtraFog = s.ExtraFog; RainOverride = s.RainOverride;
        Latitude = s.Latitude; Weather = (Weather)s.Weather; DayOfYear = s.DayOfYear;
        SnowCover = s.SnowCover; WindSpeed = s.WindSpeed; WindDirection = s.WindDirection;
        Apply(true);
    }

    /// <summary>Client side: applies the state the host sends.</summary>
    public static void ClientHandleState(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<EnvironmentStateMsg>(sync);
        var env = Instance != null ? Instance : FindAnyObjectByType<EnvironmentController>();
        if (env != null) env.SetState(msg);
    }

    // ------------------------------------------------------------------ applying

    /// <summary>Sun elevation and azimuth (degrees) for the time, latitude and day (simple solar model).</summary>
    public static void SolarPosition(float hours, float latitude, int day, out float elevation, out float azimuth)
    {
        float decl = -23.44f * Mathf.Cos(2f * Mathf.PI / 365f * (day + 10)) * Mathf.Deg2Rad;
        float lat = latitude * Mathf.Deg2Rad;
        float hourAngle = (hours - 12f) * 15f * Mathf.Deg2Rad;
        float sinEl = Mathf.Sin(lat) * Mathf.Sin(decl) + Mathf.Cos(lat) * Mathf.Cos(decl) * Mathf.Cos(hourAngle);
        float el = Mathf.Asin(Mathf.Clamp(sinEl, -1f, 1f));
        float cosAz = (Mathf.Sin(decl) - Mathf.Sin(el) * Mathf.Sin(lat)) / Mathf.Max(1e-4f, Mathf.Cos(el) * Mathf.Cos(lat));
        float az = Mathf.Acos(Mathf.Clamp(cosAz, -1f, 1f));
        if (hourAngle > 0) az = 2f * Mathf.PI - az; // afternoon: west
        elevation = el * Mathf.Rad2Deg;
        azimuth = az * Mathf.Rad2Deg;
    }

    float WeatherCloudiness() => Weather switch
    {
        Weather.Clear => 0f,
        Weather.PartlyCloudy => 0.3f,
        Weather.Overcast => 0.85f,
        Weather.LightRain => 0.9f,
        Weather.HeavyRain => 1f,
        Weather.Fog => 0.8f,
        Weather.LightSnow => 0.9f,
        Weather.HeavySnow => 1f,
        _ => 0f,
    };

    float WeatherSnowfall() => Weather switch
    {
        Weather.LightSnow => 0.35f,
        Weather.HeavySnow => 1f,
        _ => 0f,
    };

    float WeatherSnowCover() => SnowCover >= 0 ? Mathf.Clamp01(SnowCover) : Weather switch
    {
        Weather.LightSnow => 0.6f,
        Weather.HeavySnow => 1f,
        _ => 0f,
    };

    float WeatherRain() => RainOverride >= 0 ? Mathf.Clamp01(RainOverride) : Weather switch
    {
        Weather.LightRain => 0.35f,
        Weather.HeavyRain => 1f,
        _ => 0f,
    };

    float WeatherFog() => ExtraFog + Weather switch
    {
        Weather.Overcast => 0.002f,
        Weather.LightRain => 0.006f,
        Weather.HeavyRain => 0.012f,
        Weather.Fog => 0.03f,
        Weather.LightSnow => 0.005f,
        Weather.HeavySnow => 0.014f,
        _ => 0.0005f,
    };

    void Apply(bool force)
    {
        if (!_initialised) Init();
        float rain = WeatherRain(), fog = WeatherFog(), snowfall = WeatherSnowfall(), snow = WeatherSnowCover();
        if (!force && Mathf.Abs(_appliedTime - TimeOfDay) < 1f / 120f && _appliedWeather == Weather &&
            Mathf.Approximately(_appliedFog, fog) && Mathf.Approximately(_appliedRain, rain) && Mathf.Approximately(_appliedSnow, snow) &&
            Mathf.Approximately(_appliedSnowfall, snowfall) && Mathf.Approximately(_appliedWind, WindSpeed + WindDirection * 1000f)) return;
        _appliedTime = TimeOfDay; _appliedWeather = Weather; _appliedFog = fog; _appliedRain = rain;
        _appliedSnow = snow; _appliedSnowfall = snowfall; _appliedWind = WindSpeed + WindDirection * 1000f;
        Snowfall = snowfall; Snow = snow;
        SetSnowfall(snowfall);
        SetBareTrees(snow >= 0.3f);

        // (the city's clock on that day: summer time or not, as the date falls)
        if (_hasClockZone) UtcOffset = _clockZone.UtcOffsetOn(DayOfYear);
        float solarTime = TimeOfDay - UtcOffset + Longitude / 15f; // local solar time
        SolarPosition(solarTime, Latitude, DayOfYear, out var elevation, out var azimuth);
        SunElevation = elevation;
        float clouds = WeatherCloudiness();
        float day = Mathf.Clamp01((elevation + 4f) / 9f);         // 0 at night, 1 from 5° up (the sky stays bright until sunset)
        float low = Mathf.Clamp01(1f - elevation / 25f);          // warmer light when low

        if (Sun != null && !UseBakedLighting)
        {
            Sun.transform.rotation = Quaternion.Euler(Mathf.Max(elevation, -10f), azimuth + NorthOffset, 0f);
            Sun.useColorTemperature = true;
            Sun.colorTemperature = Mathf.Lerp(6200f, 2400f, low * low);
            Sun.color = Color.white;
            Sun.intensity = Mathf.Lerp(0f, 1.3f, day) * Mathf.Lerp(1f, 0.15f, clouds);
            Sun.shadowStrength = Mathf.Lerp(0.9f, 0.25f, clouds);
            Sun.enabled = elevation > -6f;
        }

        if (UseBakedLighting)
        {
            // only weather: keep sky, sun and ambient of the scene
            // always some haze (aerial perspective): under 1% at 300 m, about half at 3 km, so the
            // distant city and the surroundings photo fade into the sky instead of ending at a line
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Max(fog, MinHaze);
            Rain = rain;
            SetRain(rain);
            SetSurfaces(rain > 0 ? Mathf.Clamp01(0.4f + rain) : 0f, snow);
            return;
        }

        _sky.SetFloat("_SunSize", 0.04f);
        _sky.SetFloat("_SunSizeConvergence", 6f);
        // under clouds the sky is a neutral grey: less scattering, grey tint
        _sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1f, 0.55f, clouds));
        // a dark blue night sky instead of the procedural sky's brownish twilight
        var dayTint = Color.Lerp(new Color(0.5f, 0.55f, 0.6f), new Color(0.75f, 0.76f, 0.78f), clouds);
        _sky.SetColor("_SkyTint", Color.Lerp(new Color(0.18f, 0.24f, 0.42f), dayTint, Mathf.Clamp01((elevation + 8f) / 10f)));
        // below the horizon (seen past the edge of the map from high up): the haze, not brown earth
        _sky.SetColor("_GroundColor", Color.Lerp(new Color(0.05f, 0.06f, 0.08f), Color.Lerp(new Color(0.52f, 0.56f, 0.62f), new Color(0.5f, 0.51f, 0.53f), clouds), day));
        _sky.SetFloat("_Exposure", Mathf.Lerp(0.15f, 1.25f, day) * Mathf.Lerp(1f, 0.8f, clouds));

        // ambient light: brighter and bluer by day, grey under clouds, dim blue at night
        // night: some sky glow of the city
        // (the shade of a street lit by the sky alone: a little brighter than before, as photos of streets in shade show)
        var skyCol = Color.Lerp(new Color(0.1f, 0.11f, 0.16f), Color.Lerp(new Color(0.66f, 0.76f, 0.96f), new Color(0.64f, 0.66f, 0.7f), clouds), day);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyCol;
        RenderSettings.ambientEquatorColor = skyCol * 0.8f;
        RenderSettings.ambientGroundColor = skyCol * 0.45f;

        // fog
        // always some haze (aerial perspective): under 1% at 300 m, about half at 3 km, so the
        // distant city and the surroundings photo fade into the sky instead of ending at a line
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Max(fog, MinHaze);
        RenderSettings.fogColor = Color.Lerp(new Color(0.05f, 0.06f, 0.08f), Color.Lerp(new Color(0.7f, 0.76f, 0.85f), new Color(0.62f, 0.64f, 0.66f), clouds), day);
        OvercastSky = clouds >= 0.7f || fog > 0.004f;

        // reflection probes were captured in daylight
        foreach (var p in _probes) if (p != null) p.intensity = Mathf.Lerp(0.05f, 1f, day) * Mathf.Lerp(1f, 0.7f, clouds);

        StreetLightsOn = elevation < 4f || (clouds > 0.8f && elevation < 12f);
        UpdateStreetLights(true);

        Rain = rain;
        SetRain(rain);
        SetSurfaces(rain > 0 ? Mathf.Clamp01(0.4f + rain) : 0f, snow);
        DynamicGI.UpdateEnvironment();
        _liveDirty = true;
        _cabinDirty = true;
    }

    // A spot light under the head of each street lamp model (the highest part, at the end of the arm).
    readonly List<GameObject> _lampGlows = new List<GameObject>();
    static Material _lampGlow;

    /// <summary>One warm emissive material for the street lamps' lenses (a copy of a lamp's own, so it is a shader every
    /// build has).</summary>
    static Material LampGlowMaterial(Material from)
    {
        if (_lampGlow != null) return _lampGlow;
        _lampGlow = from != null ? new Material(from) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _lampGlow.name = "Street lamp glow";
        var warm = new Color(1f, 0.82f, 0.55f);
        if (_lampGlow.HasProperty("_BaseColor")) _lampGlow.SetColor("_BaseColor", warm);
        if (_lampGlow.HasProperty("_BaseMap")) _lampGlow.SetTexture("_BaseMap", null);
        _lampGlow.SetColor("_EmissionColor", warm * 6f);
        _lampGlow.EnableKeyword("_EMISSION");
        _lampGlow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return _lampGlow;
    }

    void CreateLampLights()
    {
        foreach (var t in FindObjectsByType<Transform>())
        {
            if (!t.name.StartsWith(LampNamePrefix) || (t.parent != null && t.parent.name.StartsWith(LampNamePrefix))) continue;
            // (a lamp model's own light, if any, is off: this one replaces it)
            foreach (var own in t.GetComponentsInChildren<Light>(true)) { own.enabled = false; StreetLights.Remove(own); }
            var renderers = t.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) continue;
            Bounds head = renderers[0].bounds;
            foreach (var r in renderers) if (r.bounds.max.y > head.max.y + 0.01f || (Mathf.Abs(r.bounds.max.y - head.max.y) < 0.05f && r.bounds.size.y < head.size.y)) head = r.bounds;
            var go = new GameObject("Lamp light");
            go.transform.SetParent(t, true);
            go.transform.position = new Vector3(head.center.x, head.min.y - 0.1f, head.center.z);
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 120f;
            l.innerSpotAngle = 60f;
            // as bright on the ground under a tall pole as under a low one (the light falls off with the square of the
            // height: 4.5 m is the default's height)
            float mount = Mathf.Max(2.5f, go.transform.position.y - t.position.y);
            l.range = Mathf.Max(LampRange, mount * 3f);
            l.intensity = LampIntensity * Mathf.Clamp(mount * mount / (4.5f * 4.5f), 1f, 4f);
            l.useColorTemperature = true;
            l.colorTemperature = 3200f;
            l.color = Color.white;
            l.shadows = LightShadows.None;
            l.enabled = false;
            StreetLights.Add(l);
            // the lamp's glowing lens under its head, lit with the street lights (without it the heads looked switched off at
            // night, a lit street under dark lamps): a thin disc of the head's size in a warm emissive copy of its material
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.name = "Lamp glow";
            Destroy(glow.GetComponent<Collider>());
            glow.transform.SetParent(t, true);
            glow.transform.position = new Vector3(head.center.x, head.min.y + 0.01f, head.center.z);
            float lens = Mathf.Clamp(Mathf.Min(head.size.x, head.size.z) * 0.7f, 0.15f, 0.6f);
            glow.transform.localScale = Vector3.Scale(new Vector3(lens, 0.01f, lens), new Vector3(1f / Mathf.Max(1e-3f, t.lossyScale.x), 1f / Mathf.Max(1e-3f, t.lossyScale.y), 1f / Mathf.Max(1e-3f, t.lossyScale.z)));
            var gr = glow.GetComponent<MeshRenderer>();
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gr.sharedMaterial = LampGlowMaterial(renderers[0].sharedMaterial);
            glow.SetActive(false);
            _lampGlows.Add(glow);
        }
        Debug.Log($"EnvironmentController: {StreetLights.Count} street lights");
    }

    // Baked lightmaps hold the sun's shadows for one sun position: switch the scene to realtime
    // lighting so the sun can move (static objects lose their lightmaps and use ambient light).
    void MakeLightingDynamic()
    {
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            if (r.lightmapIndex >= 0 && r.lightmapIndex < 0xFFFE) r.lightmapIndex = -1;
        }
        foreach (var r in FindObjectsByType<Terrain>(FindObjectsInactive.Include)) r.lightmapIndex = -1;
        LightmapSettings.lightProbes = null;
        if (Sun != null)
        {
            Sun.bakingOutput = new LightBakingOutput
            {
                isBaked = false,
                lightmapBakeType = LightmapBakeType.Realtime,
                mixedLightingMode = MixedLightingMode.IndirectOnly,
            };
            Sun.shadows = LightShadows.Soft;
        }
        _probes.AddRange(FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include));
    }

    static Texture2D _snowTex;

    /// <summary>Fresh snow: white with a faint blue-grey grain and a few glints.</summary>
    static Texture2D SnowTexture()
    {
        if (_snowTex != null) return _snowTex;
        const int n = 256;
        var rng = new System.Random(7);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = 0.9f + 0.08f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f) + ((float)rng.NextDouble() - 0.5f) * 0.05f;
                if (rng.NextDouble() < 0.002) v = 1.05f;
                px[y * n + x] = new Color(v * 0.97f, v * 0.98f, Mathf.Min(1f, v * 1.02f));
            }
        _snowTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "snow", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        _snowTex.SetPixels(px);
        _snowTex.Apply(true, true);
        return _snowTex;
    }

    // wet: darker and glossier; snow: grass, pavements, roofs and the ground white (from a quarter cover), roads slushy and lighter
    void SetSurfaces(float wet, float snow)
    {
        var all = new HashSet<Renderer>(_wetRenderers);
        foreach (var r in _snowRenderers) all.Add(r);
        var snowSet = new HashSet<Renderer>(_snowRenderers);
        foreach (var r in all)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.Clear();
            var mat = r.sharedMaterial;
            var baseCol = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            bool covered = snowSet.Contains(r) && snow >= 0.25f;
            if (covered)
            {
                _mpb.SetTexture("_BaseMap", SnowTexture());
                _mpb.SetColor("_BaseColor", Color.Lerp(new Color(0.72f, 0.74f, 0.78f), new Color(0.97f, 0.98f, 1f), Mathf.InverseLerp(0.25f, 1f, snow)));
                _mpb.SetFloat("_Smoothness", wet > 0f ? 0.45f : 0.2f);
            }
            else if (wet > 0f || snow > 0f)
            {
                var col = baseCol * Mathf.Lerp(1f, 0.65f, wet);                       // wet surfaces are darker
                if (snow > 0f) col = Color.Lerp(col, col * 2.2f, snow * 0.6f);        // slush: lighter grey
                col.a = baseCol.a;
                _mpb.SetColor("_BaseColor", col);
                _mpb.SetFloat("_Smoothness", Mathf.Max(wet > 0f ? Mathf.Lerp(0.2f, 0.85f, wet) : 0.2f, snow > 0f ? 0.65f : 0f));
            }
            r.SetPropertyBlock(_mpb);
        }
    }

    void CreateRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        _rain = go.AddComponent<ParticleSystem>();
        _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _rain.main;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0f;
        main.startSize = 0.02f;
        main.maxParticles = 12000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.75f, 0.8f, 0.85f, 0.45f);
        var shape = _rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 1f, 40f);
        shape.position = new Vector3(0f, 12f, 0f);
        var vel = _rain.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-11f, -9f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.06f;
        renderer.lengthScale = 1f;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            var mat = new Material(shader) { name = "Rain" };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetColor("_BaseColor", new Color(0.8f, 0.85f, 0.9f, 0.35f));
            Transparent(mat);
            renderer.sharedMaterial = mat;
        }
    }

    void SetRain(float intensity)
    {
        if (_rain == null) return;
        var emission = _rain.emission;
        emission.rateOverTime = 9000f * intensity;
        Blow(_rain, 10f, 12f, RainBox);
        if (intensity > 0.01f && !_rain.isPlaying) _rain.Play();
        else if (intensity <= 0.01f && _rain.isPlaying) _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // carried by the wind: drops and flakes drift with it, emitted upwind so they still fall around the viewer.
    // Snow (slow: 13 s from 14 m) is emitted through the whole height, so it is around the viewer from the start.
    void Blow(ParticleSystem ps, float fallSpeed, float height, float size, bool wholeHeight = false)
    {
        var w = Wind * 0.85f;
        var vel = ps.velocityOverLifetime;
        vel.x = new ParticleSystem.MinMaxCurve(w.x - 0.5f, w.x + 0.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-fallSpeed * 1.1f, -fallSpeed * 0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(w.z - 0.5f, w.z + 0.5f);
        var shape = ps.shape;
        float drift = height / fallSpeed;
        shape.position = new Vector3(-w.x * drift * 0.5f, wholeHeight ? height * 0.5f - 1f : height, -w.z * drift * 0.5f);
        shape.scale = new Vector3(size + Mathf.Abs(w.x) * drift, wholeHeight ? height + 2f : 1f, size + Mathf.Abs(w.z) * drift);
    }

    void CreateSnow()
    {
        var go = new GameObject("Snow");
        go.transform.SetParent(transform, false);
        _snow = go.AddComponent<ParticleSystem>();
        _snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _snow.main;
        main.loop = true;
        main.startLifetime = 14f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.maxParticles = 20000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(1f, 1f, 1f, 0.9f);
        var shape = _snow.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        var vel = _snow.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        // flakes flutter
        var noise = _snow.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.3f;
        var collision = _snow.collision;   // and stop on the ground instead of falling through it
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.lifetimeLoss = 1f;
        collision.quality = ParticleSystemCollisionQuality.Low;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            var mat = new Material(shader) { name = "Snow" };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            // a soft round flake
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "snowflake", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d * d) );
            }
            tex.SetPixels(px); tex.Apply();
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.95f));
            Transparent(mat);
            // flakes right in front of the camera fade out (as a lens never sees them sharp), and none fills the view
            mat.SetFloat("_CameraFadingEnabled", 1f); mat.EnableKeyword("_FADING_ON");
            mat.SetVector("_CameraFadeParams", new Vector4(0.6f, 1f / 2.4f, 0f, 0f));   // near 0.6 m, fully there from 3 m
            mat.SetFloat("_CameraNearFadeDistance", 0.6f); mat.SetFloat("_CameraFarFadeDistance", 3f);
            renderer.maxParticleSize = 0.012f;
            renderer.sharedMaterial = mat;
        }
    }

    // URP particle material set to alpha blending from code (the inspector sets these keywords and states itself)
    static void Transparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
    }

    // deciduous trees under snow: their leaves' materials swapped for a fully clipped copy (bare branches); needle trees
    // and palms keep theirs
    readonly Dictionary<Renderer, Material[]> _leafy = new Dictionary<Renderer, Material[]>();
    readonly Dictionary<Material, Material> _bareOf = new Dictionary<Material, Material>();
    bool _bare;

    void SetBareTrees(bool bare)
    {
        if (bare == _bare) return;
        _bare = bare;
        if (bare && _leafy.Count == 0)
        {
            var trees = OsmMap.Current != null && OsmMap.Current.Generator != null ? OsmMap.Current.Generator.TreeTransforms : null;
            if (trees == null) return;
            foreach (var t in trees)
                if (t != null)
                    foreach (var r in t.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.sharedMaterials;
                        if (mats.Any(m => m != null && IsLeaves(m.name))) _leafy[r] = mats;
                    }
        }
        foreach (var kv in _leafy)
        {
            if (kv.Key == null) continue;
            kv.Key.sharedMaterials = bare ? kv.Value.Select(m => m != null && IsLeaves(m.name) ? Bare(m) : m).ToArray() : kv.Value;
        }
    }

    static bool IsLeaves(string name)
    {
        var n = name.ToLowerInvariant();
        return (n.Contains("leaf") || n.Contains("leaves") || n.Contains("foliage")) && !n.Contains("needle") && !n.Contains("conifer") && !n.Contains("palm") && !n.Contains("pine");
    }

    Material Bare(Material leaves)
    {
        if (_bareOf.TryGetValue(leaves, out var b)) return b;
        b = new Material(leaves) { name = leaves.name + " (bare)" };
        b.SetFloat("_AlphaClip", 1f); b.EnableKeyword("_ALPHATEST_ON"); b.SetFloat("_Cutoff", 1f);
        var c = b.HasProperty("_BaseColor") ? b.GetColor("_BaseColor") : Color.white; c.a = 0f;
        if (b.HasProperty("_BaseColor")) b.SetColor("_BaseColor", c);
        b.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        return _bareOf[leaves] = b;
    }

    void SetSnowfall(float intensity)
    {
        if (_snow == null) return;
        var emission = _snow.emission;
        emission.rateOverTime = 4000f * intensity;
        Blow(_snow, 1.1f, 14f, SnowBox, true);
        if (intensity > 0.01f && !_snow.isPlaying) _snow.Play();
        else if (intensity <= 0.01f && _snow.isPlaying) _snow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    /// <summary>Where rain and snow fall around when not the main camera (e.g. a video's camera); null: the main camera.</summary>
    public static Transform PrecipitationAnchor;

    // rain and snow fall around whoever is looking, wherever they go: the emitters follow the camera (found again
    // every frame: the camera may change), and drops and flakes the camera leaves behind (the box is 40-50 m wide;
    // flakes take 13 s to fall) are moved to the box's other side, ahead of it, so it never stops snowing around a
    // moving viewer
    void FollowCamera()
    {
        if (_rain == null || (Rain <= 0.01f && Snowfall <= 0.01f)) return;
        _rainFollow = PrecipitationAnchor;
        if (_rainFollow == null || !_rainFollow.gameObject.activeInHierarchy)
        {
            var cam = Camera.main;
            if (cam == null || !cam.isActiveAndEnabled)
            {
                cam = null;
                foreach (var c in FindObjectsByType<Camera>())
                    if (c.isActiveAndEnabled && c.targetTexture == null) { cam = c; break; }
            }
            _rainFollow = cam != null ? cam.transform : null;
        }
        if (_rainFollow == null) return;
        var at = _rainFollow.position;
        _rain.transform.position = at;
        if (_snow != null) _snow.transform.position = at;
        if (Rain > 0.01f) Wrap(_rain, at, RainBox * 0.5f);
        if (_snow != null && Snowfall > 0.01f) Wrap(_snow, at, SnowBox * 0.5f);
    }

    const float RainBox = 40f, SnowBox = 50f;
    ParticleSystem.Particle[] _wrapBuffer;

    // particles more than half a box from the viewer (horizontally) go to the other side of the box
    void Wrap(ParticleSystem ps, Vector3 at, float half)
    {
        int max = ps.main.maxParticles;
        if (_wrapBuffer == null || _wrapBuffer.Length < max) _wrapBuffer = new ParticleSystem.Particle[max];
        int n = ps.GetParticles(_wrapBuffer);
        bool moved = false;
        for (int i = 0; i < n; i++)
        {
            var p = _wrapBuffer[i].position;
            float dx = p.x - at.x, dz = p.z - at.z;
            if (dx > half || dx < -half || dz > half || dz < -half)
            {
                p.x = at.x + Mathf.Repeat(dx + half, 2f * half) - half;
                p.z = at.z + Mathf.Repeat(dz + half, 2f * half) - half;
                _wrapBuffer[i].position = p;
                moved = true;
            }
        }
        if (moved) ps.SetParticles(_wrapBuffer, n);
    }
}
