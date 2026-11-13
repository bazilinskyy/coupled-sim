using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum Weather
{
    Clear,
    PartlyCloudy,
    Overcast,
    LightRain,
    HeavyRain,
    Fog,
}

/// <summary>
/// Time of day and weather of an experiment. Time of day sets the sun from the solar position
/// (latitude, day of year), the sky, ambient light and street lights; weather sets clouds (sky
/// tint, sun strength), fog, rain and wet roads.
///
/// Settable per trial as experiment parameters (it is an IExperimentModifier):
/// time ("17:30" or 17.5), weather (Clear, PartlyCloudy, Overcast, LightRain, HeavyRain, Fog),
/// fog (density, e.g. 0.02), rain (0..1), timeSpeed (multiplier of real time; 0 = fixed).
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

    [Header("Lighting")]
    [Tooltip("Keep the scene's baked lightmaps and sun as they are (as in earlier experiments): only fog, rain, wet roads and street lights change. Off: the sun follows the time of day with realtime lighting.")]
    public bool UseBakedLighting = false;

    [Header("Scene objects (found automatically when empty)")]
    public Light Sun;
    public List<Light> StreetLights = new List<Light>();
    [Tooltip("Add a light to every street lamp (objects whose name starts with this) that switches on at dusk; empty: off.")]
    public string LampNamePrefix = "StreetLamp";
    public float LampIntensity = 4f;
    public float LampRange = 18f;
    [Tooltip("Only the street lights nearest to the viewer are switched on (performance); 0 = all.")]
    public int MaxActiveStreetLights = 24;
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
    public bool StreetLightsOn;

    Material _sky;
    ParticleSystem _rain;
    Transform _rainFollow;
    readonly List<Renderer> _wetRenderers = new List<Renderer>();
    MaterialPropertyBlock _mpb;
    readonly List<ReflectionProbe> _probes = new List<ReflectionProbe>();
    bool _initialised;
    float _appliedTime = -1;
    Weather _appliedWeather;
    float _appliedFog = -1, _appliedRain = -2;

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
                if (wet && m.HasProperty("_Smoothness")) { _wetRenderers.Add(r); break; }
            }
        }
        CreateRain();
        if (LiveReflections) CreateLiveReflections();
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

    // above the viewer (out of their own car), when the light changed or the viewer moved on
    void UpdateLiveReflections()
    {
        if (_live == null || Time.unscaledTime < _nextLiveCheck) return;
        _nextLiveCheck = Time.unscaledTime + 0.5f;
        if (_liveRender >= 0 && !_live.IsFinishedRendering(_liveRender)) return;
        var viewer = ViewerPosition();
        if (!_liveDirty && (viewer - _liveAt).sqrMagnitude < LiveReflectionDistance * LiveReflectionDistance) return;
        _live.transform.position = viewer + Vector3.up * 3.5f;
        _liveAt = viewer;
        _liveDirty = false;
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
    }

    Vector3 ViewerPosition()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            foreach (var c in FindObjectsByType<Camera>())
                if (c.isActiveAndEnabled && c.targetTexture == null) { cam = c; break; }
        }
        return cam != null ? cam.transform.position : transform.position;
    }

    // ------------------------------------------------------------------ parameters and network

    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters)
    {
        foreach (var p in experimentParameters)
        {
            var name = p.name.ToLower();
            if (name == "time" || name == "timeofday") TimeOfDay = ParseTime(p.value);
            else if (name == "weather" && Enum.TryParse(p.value, true, out Weather w)) Weather = w;
            else if (name == "fog") ExtraFog = ParseFloat(p.value);
            else if (name == "rain") RainOverride = ParseFloat(p.value);
            else if (name == "timespeed") TimeSpeed = ParseFloat(p.value);
            else if (name == "dayofyear") DayOfYear = (int)ParseFloat(p.value);
            else if (name == "latitude") Latitude = ParseFloat(p.value);
        }
    }

    static float ParseFloat(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    static float ParseTime(string s)
    {
        if (s.Contains(":"))
        {
            var parts = s.Split(':');
            return int.Parse(parts[0], CultureInfo.InvariantCulture) + int.Parse(parts[1], CultureInfo.InvariantCulture) / 60f;
        }
        return ParseFloat(s);
    }

    public struct EnvironmentStateMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_Environment;
        public float TimeOfDay, TimeSpeed, ExtraFog, RainOverride, Latitude;
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
        }
    }

    public EnvironmentStateMsg GetState() => new EnvironmentStateMsg
    {
        TimeOfDay = TimeOfDay, TimeSpeed = TimeSpeed, ExtraFog = ExtraFog, RainOverride = RainOverride,
        Latitude = Latitude, Weather = (int)Weather, DayOfYear = DayOfYear,
    };

    public void SetState(EnvironmentStateMsg s)
    {
        TimeOfDay = s.TimeOfDay; TimeSpeed = s.TimeSpeed; ExtraFog = s.ExtraFog; RainOverride = s.RainOverride;
        Latitude = s.Latitude; Weather = (Weather)s.Weather; DayOfYear = s.DayOfYear;
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
        _ => 0.0005f,
    };

    void Apply(bool force)
    {
        if (!_initialised) Init();
        float rain = WeatherRain(), fog = WeatherFog();
        if (!force && Mathf.Abs(_appliedTime - TimeOfDay) < 1f / 120f && _appliedWeather == Weather &&
            Mathf.Approximately(_appliedFog, fog) && Mathf.Approximately(_appliedRain, rain)) return;
        _appliedTime = TimeOfDay; _appliedWeather = Weather; _appliedFog = fog; _appliedRain = rain;

        float solarTime = TimeOfDay - UtcOffset + Longitude / 15f; // local solar time
        SolarPosition(solarTime, Latitude, DayOfYear, out var elevation, out var azimuth);
        SunElevation = elevation;
        float clouds = WeatherCloudiness();
        float day = Mathf.Clamp01((elevation + 4f) / 14f);        // 0 at night, 1 from 10° up
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
            RenderSettings.fog = fog > 0.0006f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fog;
            Rain = rain;
            SetRain(rain);
            SetWetness(rain > 0 ? Mathf.Clamp01(0.4f + rain) : 0f);
            return;
        }

        _sky.SetFloat("_SunSize", 0.04f);
        _sky.SetFloat("_SunSizeConvergence", 6f);
        // under clouds the sky is a neutral grey: less scattering, grey tint
        _sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1f, 0.55f, clouds));
        // a dark blue night sky instead of the procedural sky's brownish twilight
        var dayTint = Color.Lerp(new Color(0.5f, 0.55f, 0.6f), new Color(0.75f, 0.76f, 0.78f), clouds);
        _sky.SetColor("_SkyTint", Color.Lerp(new Color(0.18f, 0.24f, 0.42f), dayTint, Mathf.Clamp01((elevation + 8f) / 10f)));
        _sky.SetColor("_GroundColor", Color.Lerp(new Color(0.05f, 0.06f, 0.08f), Color.Lerp(new Color(0.37f, 0.35f, 0.33f), new Color(0.45f, 0.45f, 0.47f), clouds), day));
        _sky.SetFloat("_Exposure", Mathf.Lerp(0.15f, 1.25f, day) * Mathf.Lerp(1f, 0.8f, clouds));

        // ambient light: brighter and bluer by day, grey under clouds, dim blue at night
        // night: some sky glow of the city
        var skyCol = Color.Lerp(new Color(0.1f, 0.11f, 0.16f), Color.Lerp(new Color(0.55f, 0.65f, 0.85f), new Color(0.55f, 0.57f, 0.6f), clouds), day);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyCol;
        RenderSettings.ambientEquatorColor = skyCol * 0.8f;
        RenderSettings.ambientGroundColor = skyCol * 0.45f;

        // fog
        RenderSettings.fog = fog > 0.0006f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fog;
        RenderSettings.fogColor = Color.Lerp(new Color(0.05f, 0.06f, 0.08f), Color.Lerp(new Color(0.7f, 0.76f, 0.85f), new Color(0.62f, 0.64f, 0.66f), clouds), day);
        OvercastSky = clouds >= 0.7f || fog > 0.004f;

        // reflection probes were captured in daylight
        foreach (var p in _probes) if (p != null) p.intensity = Mathf.Lerp(0.05f, 1f, day) * Mathf.Lerp(1f, 0.7f, clouds);

        StreetLightsOn = elevation < 4f || (clouds > 0.8f && elevation < 12f);
        UpdateStreetLights(true);

        Rain = rain;
        SetRain(rain);
        SetWetness(rain > 0 ? Mathf.Clamp01(0.4f + rain) : 0f);
        DynamicGI.UpdateEnvironment();
        _liveDirty = true;
    }

    // A spot light under the head of each street lamp model (the highest part, at the end of the arm).
    void CreateLampLights()
    {
        foreach (var t in FindObjectsByType<Transform>())
        {
            if (!t.name.StartsWith(LampNamePrefix) || (t.parent != null && t.parent.name.StartsWith(LampNamePrefix))) continue;
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
            l.range = LampRange;
            l.intensity = LampIntensity;
            l.useColorTemperature = true;
            l.colorTemperature = 3200f;
            l.color = Color.white;
            l.shadows = LightShadows.None;
            l.enabled = false;
            StreetLights.Add(l);
        }
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

    void SetWetness(float wet)
    {
        foreach (var r in _wetRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            if (wet <= 0f)
            {
                _mpb.Clear();
            }
            else
            {
                var baseCol = r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor") ? r.sharedMaterial.GetColor("_BaseColor") : Color.white;
                _mpb.SetFloat("_Smoothness", Mathf.Lerp(0.2f, 0.85f, wet));
                _mpb.SetColor("_BaseColor", baseCol * Mathf.Lerp(1f, 0.65f, wet)); // wet surfaces are darker
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
            mat.renderQueue = 3000;
            renderer.sharedMaterial = mat;
        }
    }

    void SetRain(float intensity)
    {
        if (_rain == null) return;
        var emission = _rain.emission;
        emission.rateOverTime = 9000f * intensity;
        if (intensity > 0.01f && !_rain.isPlaying) _rain.Play();
        else if (intensity <= 0.01f && _rain.isPlaying) _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // rain falls around whoever is looking
    void FollowCamera()
    {
        if (_rain == null || Rain <= 0.01f) return;
        if (_rainFollow == null || !_rainFollow.gameObject.activeInHierarchy)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                foreach (var c in FindObjectsByType<Camera>())
                    if (c.isActiveAndEnabled && c.targetTexture == null) { cam = c; break; }
            }
            _rainFollow = cam != null ? cam.transform : null;
        }
        if (_rainFollow != null) _rain.transform.position = _rainFollow.position;
    }
}
