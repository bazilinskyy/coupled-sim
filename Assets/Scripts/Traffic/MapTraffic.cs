using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityStandardAssets.Utility;

/// <summary>
/// Background traffic on city maps without SUMO: lane routes across the map (<see cref="CoupledSim.Maps.MapRoutes"/>) driven by
/// <see cref="TrafficCar"/> (a car-following model; stops at red lights, for pedestrians and animals). A car that
/// reaches the end of its route, at the map's edge, starts again at its beginning (also at the edge) when that is
/// clear. Runs on the host when the map has no SUMO traffic (SUMO not installed), or always with -mapTraffic.
/// Also the participant's <b>autopilot</b> (-autopilot [km/h], trial parameter autopilot): the participant's car
/// drives itself along the route nearest to it, with the automated driving of the AV (<see cref="AutomatedDriving"/>,
/// TrafficModel mode), e.g. for demonstrations and videos.
/// Command line: -mapTraffic cars-per-km (0: none), -autopilot [km/h]; trial parameters mapTraffic, autopilot.
/// </summary>
public class MapTraffic : CarSpawnerBase, IExperimentModifier
{
    [Tooltip("Cars per km of route lane in an average city by day (times the city's CROWD factor and the time of day's; trial parameter mapTraffic sets it as it is).")]
    public float CarsPerKm = 20f;
    [Tooltip("At most this many vehicles, those nearest the participants.")]
    public int MaxCars = 150;
    [Tooltip("Cyclists per km of route in an average country (times its cycling factor: the Netherlands 3; trial parameter cyclistsPerKm).")]
    public float CyclistsPerKm = 8f;
    public int MaxCyclists = 100;
    [Tooltip("Pedestrians per km of pavement in an average city (times its CROWD factor; trial parameter pedestriansPerKm).")]
    public float PedestriansPerKm = 24f;
    public int MaxPedestrians = 100;
    [Tooltip("Share of the vehicles that are buses (on routes they can drive; trial parameter busShare).")]
    [Range(0f, 0.5f)] public float BusShare = 0.04f;
    [Tooltip("auto (by the time of day: rush hour 07:00-09:30 and 16:00-18:30, night 22:00-06:00), normal, quiet, rush, night (trial parameter traffic).")]
    public string TrafficPreset = "auto";
    [Tooltip("Scale cars and pedestrians by the city's figures in CROWD (trial parameter crowd=0 to switch off).")]
    public bool UseCrowd = true;
    /// <summary>The trial's time of day (time=HH:MM), for the auto preset; negative: not given.</summary>
    public float HourOfDay = -1f;
    bool _setCars, _setCyclists, _setPedestrians, _setBus, _setMax;
    [Tooltip("Share of the cyclists that are road racing cyclists (AIRaceBicycle): on the carriageway near the kerb, at 28-38 km/h.")]
    [Range(0f, 1f)] public float RaceCyclistShare = 0.1f;
    /// <summary>Road speed limit (km/h) up to which a race cyclist rides on the carriageway; beside faster roads it takes the
    /// cycle path (in the Netherlands one beside such a road is nearly always compulsory, the blue sign G11).</summary>
    public const float RaceOnRoadUpToKmh = 50f;
    public float DesiredSpeedKmh = 40f, DesiredSpeedSdKmh = 6f;
    [Tooltip("Car prefabs (each in NetworkingManager ➡️ AI Car System ➡️ Prefabs); empty: SUMO's car list or the AI car system's prefabs.")]
    public AICar[] CarPrefabs;
    public Color[] Colors = { Color.white, Color.black, new Color(0.6f, 0.6f, 0.62f), new Color(0.55f, 0.05f, 0.05f), new Color(0.1f, 0.2f, 0.5f), new Color(0.75f, 0.75f, 0.75f), new Color(0.2f, 0.3f, 0.25f) };
    public int Seed = 3;
    [Tooltip("Participant's autopilot speed (km/h); 0: off.")]
    public float AutopilotKmh;
    /// <summary>The autopilot's speed was given (-autopilot 50): it keeps it everywhere; else it follows the speed limits on fast roads.</summary>
    public bool AutopilotFixed;
    /// <summary>"lat,lon": the autopilot drives there by the quickest route (trial parameter destination, -destination).</summary>
    public string Destination;
    List<CoupledSim.Maps.MapRoutes.RoadLine> _lines, _rideLines;
    WaypointCircuit _autopilotRoute;
    float _autopilotFrom;
    /// <summary>The way the participant on the autopilot will go (car, rider or pedestrian): points every 20 m of its next
    /// 1.2 km, each with a handicap of 0.3 m for every metre to go (NearParticipants).</summary>
    readonly List<(Vector3 at, float handicap)> _wayAhead = new List<(Vector3, float)>();

    /// <summary>
    /// How near a place is to the participants (squared metres), for placing the traffic, cyclists and pedestrians nearest
    /// first: the distance to the nearest participant, or with a participant on the autopilot to a point of its way ahead
    /// plus 0.3 m for every metre it has to go to get there, so they are where it will be in the next minute too, not only
    /// where it starts: a rush hour stays a rush hour along the way (cyclists placed only round a car's start were all
    /// behind it after 20 s on De Zaale in Eindhoven).
    /// </summary>
    float NearParticipants(Vector3 q, List<Vector3> players)
    {
        float b = float.MaxValue;
        foreach (var c in players) b = Mathf.Min(b, (c - q).magnitude);
        foreach (var (c, h) in _wayAhead) if (h < b) b = Mathf.Min(b, (c - q).magnitude + h);
        return b * b;
    }

    void SetWayAhead(List<Vector3> path, int from)
    {
        _wayAhead.Clear();
        float s = 0f, next = 20f;
        for (int i = Mathf.Max(0, from) + 1; i < path.Count && s <= 1200f; i++)
        {
            s += Vector3.Distance(path[i - 1], path[i]);
            if (s >= next) { _wayAhead.Add((path[i], 0.3f * s)); next = s + 20f; }
        }
    }

    public static MapTraffic Current { get; private set; }
    public readonly List<WaypointCircuit> Routes = new List<WaypointCircuit>();
    public readonly List<TrafficCar> Cars = new List<TrafficCar>();
    bool _forced;

    protected override bool HasValidSpawnParams => true;

    /// <summary>Host: on city maps, the map traffic (made here unless the experiment has one).</summary>
    public static void Ensure(AICarSyncSystem sync)
    {
        if (OsmMap.Current == null || Current != null) return;
        var go = new GameObject("Map traffic");
        var t = go.AddComponent<MapTraffic>();
        t.ApplyCommandLine();
        t.Init(sync);
    }

    /// <summary>Clients: only the autopilot of their own participant (-autopilot; the host runs the traffic).</summary>
    public static void EnsureLocalAutopilot()
    {
        if (OsmMap.Current == null || Current != null || Array.IndexOf(CommandLine.Args, "-autopilot") < 0) return;
        var t = new GameObject("Map routes (autopilot)").AddComponent<MapTraffic>();
        t.ApplyCommandLine();
        t.CarsPerKm = 0f; t._forced = false;
        t.StartCoroutine(t.SpawnCoroutine());
    }

    void Awake() => Current = this;
    void OnDestroy() { if (Current == this) Current = null; }

    void ApplyCommandLine()
    {
        var args = CommandLine.Args;
        var ci = CultureInfo.InvariantCulture;
        int i = Array.IndexOf(args, "-mapTraffic");
        if (i >= 0) { _forced = true; if (i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, ci, out var d)) CarsPerKm = d; }
        i = Array.IndexOf(args, "-autopilot");
        // -autopilot: 35 km/h in town and the speed limit on faster roads (a motorway); -autopilot 50: always 50 km/h
        if (i >= 0)
        {
            float k = 0f;
            AutopilotFixed = i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, ci, out k);
            AutopilotKmh = AutopilotFixed ? k : 35f;
        }
        i = Array.IndexOf(args, "-destination");
        if (i >= 0 && i + 1 < args.Length) Destination = args[i + 1];
        // (-cyclistsPerKm 20, -pedestriansPerKm 40, -busShare 0.1, -raceCyclistShare 0.2, -maxCars 200, -maxCyclists,
        // -maxPedestrians, -traffic rush, -crowd 0: as the trial parameters of those names)
        for (int a = 0; a + 1 < args.Length; a++)
            if (args[a].StartsWith("-") && Set(args[a].Substring(1), args[a + 1])) a++;
        var nm = NetworkingManager.Instance;
        var trial = nm != null && nm.trials != null && nm.trials.Length > 0 ? nm.trials[Mathf.Clamp(nm.CurrentTrial, 0, nm.trials.Length - 1)] : null;
        if (trial?.experimentParameters != null) SetParameter(trial.experimentParameters);
    }

    public void SetParameter(NetworkingManager.ExperimentParameter[] ps)
    {
        var ci = CultureInfo.InvariantCulture;
        foreach (var p in ps)
        {
            var n = p.name.ToLowerInvariant();
            if (n == "maptraffic" && float.TryParse(p.value, NumberStyles.Float, ci, out var d)) { CarsPerKm = d; _forced = true; _setCars = true; }
            if (n == "autopilot" && float.TryParse(p.value, NumberStyles.Float, ci, out var k)) { AutopilotKmh = k; AutopilotFixed = true; }
            if (n == "destination") Destination = p.value;
            Set(p.name, p.value);
        }
    }

    /// <summary>The density parameters by name (any case; trial parameters and command line): cyclistsPerKm,
    /// pedestriansPerKm, busShare, raceCyclistShare, maxCars, maxCyclists, maxPedestrians, traffic, crowd, time.
    /// False for any other name.</summary>
    bool Set(string name, string value)
    {
        var ci = CultureInfo.InvariantCulture;
        bool F(out float v) => float.TryParse(value, NumberStyles.Float, ci, out v);
        switch (name.ToLowerInvariant())
        {
            case "cyclistsperkm": if (F(out var c)) { CyclistsPerKm = c; _setCyclists = true; } return true;
            case "pedestriansperkm": if (F(out var pd)) { PedestriansPerKm = pd; _setPedestrians = true; } return true;
            case "busshare": if (F(out var b)) { BusShare = Mathf.Clamp(b, 0f, 0.5f); _setBus = true; } return true;
            case "racecyclistshare": if (F(out var r)) RaceCyclistShare = Mathf.Clamp01(r); return true;
            case "maxcars": if (F(out var mc)) { MaxCars = Mathf.Max(0, (int)mc); _setMax = true; } return true;
            case "maxcyclists": if (F(out var my)) { MaxCyclists = Mathf.Max(0, (int)my); _setMax = true; } return true;
            case "maxpedestrians": if (F(out var mp)) { MaxPedestrians = Mathf.Max(0, (int)mp); _setMax = true; } return true;
            case "traffic": TrafficPreset = value.Trim().ToLowerInvariant(); return true;
            case "crowd": UseCrowd = !(value == "0" || value.Equals("off", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase)); return true;
            case "time":
                var hm = value.Split(':');
                if (hm.Length >= 2 && float.TryParse(hm[0], NumberStyles.Float, ci, out var h) && float.TryParse(hm[1], NumberStyles.Float, ci, out var m)) HourOfDay = h + m / 60f;
                return false;   // (also the environment's)
        }
        return false;
    }

    /// <summary>The preset in force: the one asked for, or by the time of day (auto).</summary>
    public string PresetNow()
    {
        if (TrafficPreset != "auto") return TrafficPreset;
        if (HourOfDay < 0f) return "normal";
        if ((HourOfDay >= 7f && HourOfDay < 9.5f) || (HourOfDay >= 16f && HourOfDay < 18.5f)) return "rush";
        if (HourOfDay >= 22f || HourOfDay < 6f) return "night";
        return "normal";
    }

    /// <summary>
    /// The densities for this map: the base figures (per km in an average city by day) times the city's factors
    /// (TrafficDensity: CROWD's cars and pedestrians, the country's cycling) and the preset's (rush hour: cars 1.6x,
    /// cyclists 2.5x and more of them near the participants, pedestrians 1.4x, buses 1.8x; quiet 0.5x; night: cars
    /// 0.4x, cyclists 0.3x, pedestrians by CROWD's night footage). A figure set by a parameter is kept as it is.
    /// </summary>
    void ApplyDensities()
    {
        bool known = OsmMap.Current != null;
        var info = known ? OsmMap.Current.Info : default;
        var f = UseCrowd && known ? TrafficDensity.For(info.Country, info.Latitude, info.Longitude)
            : new TrafficDensity.Factors { Cars = 1f, Pedestrians = 1f, Cyclists = 1f, PedestriansAtNight = 0.6f, From = "CROWD off" };
        string preset = PresetNow();
        float cars = 1f, cyc = 1f, peds = 1f, bus = 1f;
        switch (preset)
        {
            case "rush": cars = 1.6f; cyc = 2.5f; peds = 1.4f; bus = 1.8f; MaxCyclists = Mathf.RoundToInt(MaxCyclists * 1.5f); MaxCars = Mathf.RoundToInt(MaxCars * 1.2f); break;
            case "quiet": cars = cyc = peds = bus = 0.5f; break;
            case "night": cars = 0.4f; cyc = 0.3f; peds = f.PedestriansAtNight; bus = 0.5f; break;
        }
        // (a busier city than the average one: more of them placed too, up to 1.5 times the caps, so the density holds
        // further from the participants; in New York 150 vehicles filled only the streets within 270 m)
        if (!_setMax)
        {
            MaxCars = Mathf.RoundToInt(MaxCars * Mathf.Clamp(f.Cars, 1f, 1.5f));
            MaxPedestrians = Mathf.RoundToInt(MaxPedestrians * Mathf.Clamp(f.Pedestrians, 1f, 1.5f));
        }
        if (!_setCars) CarsPerKm *= f.Cars * cars;
        if (!_setCyclists) CyclistsPerKm *= f.Cyclists * cyc;
        if (!_setPedestrians) PedestriansPerKm *= f.Pedestrians * peds;
        if (!_setBus) BusShare = Mathf.Clamp(BusShare * bus, 0f, 0.5f);
        Debug.Log($"MapTraffic: densities {CarsPerKm:F1} cars, {CyclistsPerKm:F1} cyclists and {PedestriansPerKm:F1} pedestrians per km, {BusShare * 100f:F0} % buses, at most {MaxCars} vehicles, {MaxCyclists} cyclists, {MaxPedestrians} pedestrians ({preset}; city factors: cars {f.Cars:F2}, pedestrians {f.Pedestrians:F2}, cyclists {f.Cyclists:F2}, from {f.From})");
    }

    protected override IEnumerator SpawnCoroutine()
    {
        while (OsmMap.Current == null || OsmMap.Current.Generator == null || OsmMap.Current.Generator.Roads.Count == 0) yield return null;
        // SUMO gets a few seconds to connect; its traffic takes precedence unless asked otherwise
        var sumo = FindAnyObjectByType<SumoBridge>();
        float t0 = Time.time;
        while (sumo != null && sumo.isActiveAndEnabled && Time.time - t0 < 4f && !(sumo.Status ?? "").StartsWith("connected")) yield return null;
        bool sumoRuns = sumo != null && sumo.isActiveAndEnabled && (sumo.Status ?? "").StartsWith("connected");
        _signPosts = null; RideMoves = 0;   // (this map's)
        ApplyDensities();

        var gen = OsmMap.Current.Generator;
        CoupledSim.Maps.MapRoutes.RoadLine Line(OsmMapGenerator.Road r) => new CoupledSim.Maps.MapRoutes.RoadLine { Line = r.Line.Select(p => new System.Numerics.Vector3(p.x, p.y, p.z)).ToList(), Width = r.Width, OneWay = r.OneWay, ParkLeft = r.ParkLeft, ParkRight = r.ParkRight, SpeedKmh = r.SpeedLimitKmh, Lanes = r.Lanes };
        var lines = _lines = gen.Roads.Where(r => !r.NoThroughTraffic).Select(Line).ToList();
        // (riders to a destination also along the streets on paving and through buildings: on the TU/e campus, De Zaale
        // through Atlas)
        _rideLines = gen.Roads.Where(r => !r.NoThroughTraffic || r.PavedOver).Concat(gen.PassageStreets).Select(Line).ToList();
        // (a map with corridors along a highway: routes start and end at its area's edge; more of them, as it is longer)
        var area = gen.Area;
        System.Func<System.Numerics.Vector3, float> depth = area == null ? null : p => area.Depth(new System.Numerics.Vector2(p.X, p.Z));
        var lanes = CoupledSim.Maps.MapRoutes.Build(lines, gen.HalfSize, OsmMap.Current.Info.LeftHandTraffic, max: area == null ? 80 : 140, variants: 3, depth: depth)
            .Select(l => l.Select(p => new Vector3(p.X, p.Y, p.Z)).ToList()).ToList();
        if (area != null)
        {
            int outside = 0; float worst = 0f;
            foreach (var lane in lanes) { float m = lane.Min(q => area.Depth(new System.Numerics.Vector2(q.x, q.z))); if (m < -5f) { outside++; worst = Mathf.Min(worst, m); } }
            Debug.Log($"MapTraffic: {lanes.Count} routes in the map's area, {outside} leaving it (by up to {-worst:F0} m)");
        }
        // (and the streets near the participants that no route uses)
        if (!sumoRuns || _forced) lanes.AddRange(LocalRoutes(lanes, lines, OsmMap.Current.Info.LeftHandTraffic));
        Physics.SyncTransforms();
        int moved = 0; _moves.Clear(); ParkedMoves = 0; CornerMoves = 0;
        var someone = FindAnyObjectByType<PlayerAvatar>();
        DebugFocus = someone != null && System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0 ? someone.transform.position : (Vector3?)null;
        // (-debugLaneAt x,z or x,y,z: the lane points within 30 m of there instead; y, the ground's height there on a map
        // with terrain, for the ground seen around it)
        int dla = System.Array.IndexOf(CommandLine.Args, "-debugLaneAt");
        if (dla >= 0 && dla + 1 < CommandLine.Args.Length && CommandLine.Args[dla + 1].Split(',') is var xz && (xz.Length == 2 || xz.Length == 3)
            && float.TryParse(xz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var fx) && float.TryParse(xz[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var fz))
            DebugFocus = new Vector3(fx, xz.Length == 3 && float.TryParse(xz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var fy) ? fy : 0f, fz);
        if (dla >= 0 && DebugFocus != null)
            foreach (var r in gen.Roads)
                for (int i = 0; i < r.Line.Count; i++)
                    if (Flat(r.Line[i] - DebugFocus.Value).sqrMagnitude < 20f * 20f)
                        Debug.Log($"TrafficDebug: road {r.Type} width {r.Width:F1} one-way {r.OneWay} lanes {r.Lanes} point {i}/{r.Line.Count} at {r.Line[i]}");
        if (dla >= 0 && DebugFocus != null)
        {
            // the ground seen around there, 24 m square every 0.5 m, north up (R road, P pavement, p other paving or bare, C cycle, G green, - none)
            var f = DebugFocus.Value;
            for (int zi = 24; zi >= -24; zi--)
            {
                var row = new System.Text.StringBuilder();
                for (int xi = -24; xi <= 24; xi++)
                {
                    var g = SeenGround(new Vector3(f.x + xi * 0.5f, f.y + 2f, f.z + zi * 0.5f));
                    row.Append(xi == 0 && zi == 0 ? '+' : g == Ground.Road ? 'R' : g == Ground.Cycle ? 'C' : g == Ground.Pavement ? 'P' : g == Ground.Paving ? 'p' : g == Ground.Green ? 'G' : g == Ground.Bare ? 'b' : '-');
                }
                Debug.Log($"TrafficDebug: ground z={f.z + zi * 0.5f,7:F1} {row}");
            }
            // (and the surfaces under a line across there, west to east every 0.5 m: name and height of each)
            for (int xi = -12; xi <= 12; xi++)
            {
                var q = new Vector3(f.x + xi * 0.5f, f.y + 6f, f.z);
                var hs = Physics.RaycastAll(q, Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore).OrderByDescending(h => h.point.y);
                Debug.Log($"TrafficDebug: across x={q.x:F1}: " + string.Join(", ", hs.Select(h => $"{h.collider.name} {h.point.y:F2}{(h.collider.attachedRigidbody != null ? " (body)" : "")}")));
            }
            foreach (var h in Physics.RaycastAll(new Vector3(f.x, f.y + 6f, f.z), Vector3.down, 12f))
            {
                var mc = h.collider as MeshCollider; string tri = "";
                if (mc != null && mc.sharedMesh != null && h.triangleIndex >= 0)
                {
                    var m = mc.sharedMesh; var t = m.triangles; var v = m.vertices;
                    for (int q = 0; q < 3; q++) tri += " " + mc.transform.TransformPoint(v[t[h.triangleIndex * 3 + q]]).ToString("F1");
                }
                Debug.Log($"TrafficDebug: under the focus: {h.collider.name} at {h.point.y:F2}{tri}");
            }
        }
        BuildOncomingIndex(lanes);
        foreach (var lane in lanes) moved += KeepOnCarriageway(lane);
        _oncoming = null;
        // (the moves bend a lane within a few metres: smoothed again, lightly, so no bend is tighter than a car turns; a
        // car swung 1.8 m wide of such a kink into one waiting beside its lane)
        for (int i = 0; i < lanes.Count; i++) lanes[i] = SmoothMoved(lanes[i]);
        // (and the smoothed lanes checked once more: smoothing can pull a lane back over a corner's kerb)
        BuildOncomingIndex(lanes);
        foreach (var lane in lanes) moved += KeepOnCarriageway(lane);
        // (and the two lanes of a two-way street apart where it has room: where OpenStreetMap's centre line is off the
        // drawn carriageway, one lane ran along the kerb and the oncoming one down the middle, 2 m apart, and the
        // autopilot scraped an oncoming car)
        BuildOncomingIndex(lanes);
        int spread = 0; SpreadMax = 0f;
        foreach (var lane in lanes) spread += SpreadFromOncoming(lane, OsmMap.Current.Info.LeftHandTraffic);
        _oncoming = null;
        // (and clear of what stands beside the road: trees, lamp posts, building fronts, parked cars; a car's side
        // brushed the trees along a lane and a building's corner at a junction)
        BuildOncomingIndex(lanes);
        int cleared = 0, blockedLeft = 0; _unclearedLogged = 0;
        foreach (var lane in lanes) cleared += KeepLaneClear(lane, ref blockedLeft);
        _oncoming = null;
        if (cleared > 0 || blockedLeft > 0) Debug.Log($"MapTraffic: {cleared} lane points moved clear of trees, posts, walls and parked cars ({blockedLeft} could not be cleared)");
        if (spread > 0) Debug.Log($"MapTraffic: {spread} lane points moved away from the oncoming lane (to {OncomingApart:F1} m apart where the carriageway has room; by up to {SpreadMax:F2} m)");
        if (moved > 0) Debug.Log($"MapTraffic: {moved} route points moved off cycle paths, cycle lanes and pavements onto the carriageway ({ParkedMoves} of them off grass verges or clear of parked cars, {CornerMoves} of them only a little, out of a corner)");
        if (moved > 0 && System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0)
        {
            var geo = new CoupledSim.Maps.GeoProjection(OsmMap.Current.Info.Latitude, OsmMap.Current.Info.Longitude);
            Debug.Log("TrafficDebug: the largest moves onto the carriageway: " + string.Join("; ", _moves.OrderByDescending(m => Mathf.Abs(m.shift)).Take(8)
                .Select(m => { geo.ToGeo(m.at.x, m.at.z, out var la, out var lo); return $"{m.shift:F2} m at {m.at} ({la.ToString("F6", CultureInfo.InvariantCulture)},{lo.ToString("F6", CultureInfo.InvariantCulture)})"; })));
        }
        BuildJunctions(gen.Roads);
        _correctedLanes = lanes;
        foreach (var lane in lanes) { EaseBends(lane); Routes.Add(MakeCircuit(lane, Routes.Count)); }
        RoadSpeedLimits.Build(gen.Roads);
        foreach (var r in Routes) r.GetRoutePoint(0f);   // (the circuit works out its length on first use)
        Debug.Log($"MapTraffic: {Routes.Count} routes ({Routes.Sum(r => r.OpenLength) / 1000f:F1} km of lanes){(sumoRuns ? ", SUMO runs the traffic" : "")}");

        if (AutopilotKmh > 0f) Autopilot();
        if ((!sumoRuns || _forced) && CarsPerKm > 0f) yield return SpawnCars();
        if (area != null && System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0) StartCoroutine(WatchArea(area));
    }

    // -debugTraffic on a map with corridors: the first vehicles that leave its area, and how
    IEnumerator WatchArea(CoupledSim.Maps.MapArea area)
    {
        var told = new HashSet<TrafficCar>();
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            foreach (var c in Cars)
            {
                if (c == null || told.Contains(c)) continue;
                var p = c.transform.position;
                float dp = area.Depth(new System.Numerics.Vector2(p.x, p.z));
                if (dp > -5f && p.y > -20f) continue;
                told.Add(c);
                var tr = c.GetComponent<WaypointProgressTracker>();
                var rp = tr != null && tr.Circuit != null ? tr.Circuit.GetRoutePoint(tr.ProgressDistance).position : Vector3.zero;
                var rb = c.GetComponent<Rigidbody>();
                Debug.Log($"TrafficDebug: {c.name} left the area ({dp:F0} m) at {p}, speed {c.Speed:F1} m/s, body {(rb != null ? rb.linearVelocity.ToString() : "-")}, progress {(tr != null ? tr.ProgressDistance : -1f):F0} m of {(tr != null && tr.Circuit != null ? tr.Circuit.Length : 0f):F0} at {rp} ({Vector3.Distance(p, rp):F0} m away), {c.LeaderDescription}");
                if (told.Count >= 6) yield break;
            }
        }
    }

    /// <summary>Lane bends tighter than a car can drive (its centre's turning circle, about 4.7 m) eased, a little each pass:
    /// the lane is smoothed to bends of 5 m and more, but the corrections after it (onto the carriageway, apart from the
    /// oncoming lane, clear of what stands beside it) put kinks back, of 2.8 m radius at junctions in São Paulo, and the
    /// cars following them swung a metre out of their lanes. Only those points move, towards their neighbours' middle.</summary>
    public static int BendsEased;
    static void EaseBends(List<Vector3> lane, float minRadius = 5.2f)
    {
        if (lane == null || lane.Count < 5) return;
        var moved = new HashSet<int>();
        for (int pass = 0; pass < 25; pass++)
        {
            bool any = false;
            for (int i = 1; i + 1 < lane.Count; i++)
            {
                var a = Flat(lane[i - 1]); var b = Flat(lane[i]); var c = Flat(lane[i + 1]);
                float twice = Vector3.Cross(b - a, c - a).magnitude;
                if (twice < 1e-4f) continue;
                float r = (b - a).magnitude * (c - b).magnitude * (a - c).magnitude / (2f * twice);
                if (r >= minRadius) continue;
                var mid = (lane[i - 1] + lane[i + 1]) * 0.5f;
                lane[i] = Vector3.Lerp(lane[i], new Vector3(mid.x, lane[i].y, mid.z), 0.5f);
                moved.Add(i); any = true;
            }
            if (!any) break;
        }
        BendsEased += moved.Count;
    }

    WaypointCircuit MakeCircuit(List<Vector3> lane, int index)
    {
        var go = new GameObject($"Map route {index}");
        go.transform.SetParent(transform, false);
        go.SetActive(false);   // the circuit caches its points when it wakes up
        var c = go.AddComponent<WaypointCircuit>();
        var items = new Transform[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            var w = new GameObject("w" + i).transform;
            w.SetParent(go.transform, false);
            w.position = lane[i] + Vector3.up * 0.2f;   // (the roads' lines are on the terrain already: TerrainHeight.Apply)
            items[i] = w;
        }
        c.waypointList.circuit = c;
        c.waypointList.items = items;
        go.SetActive(true);
        return c;
    }

    IEnumerator SpawnCars()
    {
        var prefabs = CarPrefabs != null && CarPrefabs.Length > 0 ? CarPrefabs
            : FindAnyObjectByType<SumoBridge>()?.CarPrefabs is AICar[] sp && sp.Length > 0 ? sp
            : _syncSystem.Prefabs.Where(p => p != null && !p.name.Contains("Bicycle") && !p.name.Contains("Scooter") && !p.name.Contains("Moped") && (BusShare > 0f || !p.name.Contains("Bus"))).ToArray();
        if (prefabs.Length == 0) { Debug.LogWarning("MapTraffic: no car prefabs"); yield break; }
        var rng = new System.Random(Seed);
        var players = FindObjectsByType<PlayerAvatar>().Where(a => !a.name.StartsWith("Map ") && a.GetComponent<AIPedestrian>() == null).Select(a => a.transform.position).ToList();
        // a realistic mix: mostly cars, some vans, few trucks, an occasional bus
        float Base(AICar p) { var n = p.name.ToLowerInvariant(); return n.Contains("truck") || n.Contains("daf") ? 0.5f : n.Contains("van") || n.Contains("delivery") ? 1.2f : n.Contains("ambulance") || n.Contains("police") || n.Contains("taxi") ? 0.6f : 3f; }
        // (buses BusShare of all the vehicles, shared among the bus models)
        bool IsBus(AICar p) => p.name.ToLowerInvariant().Contains("bus");
        float others = prefabs.Where(p => !IsBus(p)).Sum(Base);
        int buses = prefabs.Count(IsBus);
        float busWeight = buses > 0 && BusShare > 0f && BusShare < 1f ? BusShare / (1f - BusShare) * others / buses : 0f;
        float Weight(AICar p) => IsBus(p) ? busWeight : Base(p);
        float total = prefabs.Sum(Weight);
        AICar Pick(System.Random from = null) { double x = (from ?? rng).NextDouble() * total; foreach (var p in prefabs) { x -= Weight(p); if (x <= 0) return p; } return prefabs[prefabs.Length - 1]; }
        // (the picks again for a truck or bus that cannot drive its route from a stream of their own, so the other
        // vehicles keep their models and colours)
        var swapRng = new System.Random(Seed + 7919);
        // trucks and buses only on routes whose bends they can drive (none tighter than their turning circle, by their
        // length as TrafficCar takes it, from the body's box): an articulated bus (8.4 m at its tightest) on a route
        // that turned 155 degrees round a 5 m junction corner swung 6 m off its lane, circled at full lock, its rear
        // section shoving the front, and drove across the junction into the cars and cyclists there
        var turnRadius = new Dictionary<AICar, float>();
        float TurnRadius(AICar prefab)
        {
            if (turnRadius.TryGetValue(prefab, out var r)) return r;
            float len = prefab.TryGetComponent<BoxCollider>(out var box) && !box.isTrigger ? box.size.z * Mathf.Abs(prefab.transform.localScale.z) : 4.5f;
            if (len < 2.5f || len > 25f) len = 4.5f;
            return turnRadius[prefab] = Mathf.Clamp(0.6f * len + 2f, 2.5f, 9f);   // (as AICar steers)
        }
        var tightest = new Dictionary<WaypointCircuit, float>();
        float Tightest(WaypointCircuit route)
        {
            if (tightest.TryGetValue(route, out var t)) return t;
            // the smallest circle through three points 3 m apart along it (not its closing piece back to the start)
            float best = float.MaxValue, len = route.OpenLength;
            for (float s = 3f; s + 3f < len; s += 1f)
            {
                var a = route.GetRoutePosition(s - 3f); var b = route.GetRoutePosition(s); var c = route.GetRoutePosition(s + 3f);
                a.y = b.y = c.y = 0f;
                float twiceArea = Vector3.Cross(b - a, c - a).magnitude;
                if (twiceArea > 1e-4f) best = Mathf.Min(best, (b - a).magnitude * (c - b).magnitude * (a - c).magnitude / (2f * twiceArea));
            }
            return tightest[route] = best;
        }
        // and only on routes with room for their width: the lanes keep 0.4 m beside a car of 1.8 m from what stands
        // fixed (KeepLaneClear); a lorry of 3.1 m with its mirrors, placed by a row of parked cars in London, stood
        // against them for good
        var halfWidth = new Dictionary<AICar, float>();
        float HalfWidthOf(AICar prefab)
        {
            if (halfWidth.TryGetValue(prefab, out var w)) return w;
            float wid = prefab.TryGetComponent<BoxCollider>(out var box) && !box.isTrigger ? box.size.x * Mathf.Abs(prefab.transform.localScale.x) : 1.9f;
            if (wid < 1.2f || wid > 3.2f) wid = 1.9f;
            return halfWidth[prefab] = wid / 2f;
        }
        var room = new Dictionary<WaypointCircuit, float>();
        float RoomBesideLane(WaypointCircuit route)
        {
            // the half width (m) a vehicle on it can have and keep 0.15 m from everything fixed along it: 0.9 m and the
            // clearance found clear at every point (2 m apart), of 0.9, 0.75, 0.55 and 0.4 m
            if (room.TryGetValue(route, out var r)) return r;
            float len = route.OpenLength;
            foreach (var c in new[] { 0.9f, 0.75f, 0.55f })
            {
                bool clear = true;
                for (float s = 0f; s < len && clear; s += 2f) { var rp = route.GetRoutePoint(s); clear = !FixedBeside(rp.position, Flat(rp.direction), c); }
                if (clear) return room[route] = 0.9f + c - 0.15f;
            }
            return room[route] = 0.9f + 0.4f - 0.15f;
        }
        int keptOff = 0, keptNarrow = 0;
        AICar PickFor(WaypointCircuit route)
        {
            var car = Pick();
            if (HalfWidthOf(car) > 1.1f && HalfWidthOf(car) > RoomBesideLane(route))
            {
                keptNarrow++;
                for (int k = 0; k < 20 && HalfWidthOf(car) > Mathf.Max(1.1f, RoomBesideLane(route)); k++) car = Pick(swapRng);
                if (HalfWidthOf(car) > Mathf.Max(1.1f, RoomBesideLane(route))) car = prefabs.Where(q => HalfWidthOf(q) <= 1.1f && TurnRadius(q) <= 5f).DefaultIfEmpty(car).First();
            }
            if (TurnRadius(car) <= 5f) return car;
            float bend = Tightest(route);
            if (TurnRadius(car) <= bend + 0.5f) return car;
            keptOff++;
            for (int k = 0; k < 20 && (TurnRadius(car) > bend + 0.5f || (HalfWidthOf(car) > 1.1f && HalfWidthOf(car) > RoomBesideLane(route))); k++) car = Pick(swapRng);
            return TurnRadius(car) <= bend + 0.5f ? car : prefabs.Where(q => TurnRadius(q) <= 5f && HalfWidthOf(q) <= 1.1f).DefaultIfEmpty(car).First();
        }
        var spawn = new GameObject("Map traffic spawn point").transform;
        int count = 0;
        var placed = new List<Vector3>();   // (routes share lanes: no car placed within 10 m of another)
        // the places along every route at its density; with more than MaxCars of them (a long map, a map with corridors,
        // or a high density: rush hour), those nearest the participants are taken, so the density holds where they are
        // and not only on average over all the routes
        var places = new List<(WaypointCircuit route, int i, float d, Vector3 at)>();
        foreach (var route in Routes)
        {
            float length = route.OpenLength > 0 ? route.OpenLength : 1f;   // (not the circuit's closing piece back to its start)
            int n = Mathf.RoundToInt(length / 1000f * CarsPerKm);
            for (int i = 0; i < n; i++)
            {
                float d = length * (i + 0.5f) / n + (float)rng.NextDouble() * 6f;
                if (d > length - 60f) continue;   // not in the turnaround at the end
                places.Add((route, i, d, route.GetRoutePoint(d).position));
            }
        }
        // (with the participant's car on the autopilot: also along the route it will drive, each point as though it were
        // 0.3 m further for every metre the car has to drive to get there, so the traffic is where the car will be in the
        // next minute too, not only where it starts: a rush hour stays a rush hour along the way)
        float Near(Vector3 q) => NearParticipants(q, players);
        if (places.Count > MaxCars) places = places.OrderBy(x => Near(x.at)).ToList();
        foreach (var (route, i, d, at) in places)
        {
            if (count >= MaxCars) break;
            var rp = route.GetRoutePoint(d);
            if (players.Any(p => (p - rp.position).sqrMagnitude < 15f * 15f)) continue;
            if (placed.Any(q => (q - rp.position).sqrMagnitude < 10f * 10f)) continue;
            // (not on an animal: a car was placed over a cat)
            if (AnimalAgent.All.Any(an => an != null && (an.transform.position - rp.position).sqrMagnitude < 5f * 5f)) continue;
            // (nor against a parked car, a wall or a post: one placed by a parked car stayed stuck to it)
            if (FixedBeside(rp.position, Flat(rp.direction), 0.3f)) continue;
            if (VehicleAt(rp.position, rp.direction, 3.5f, 1.4f)) continue;
            placed.Add(rp.position);
            var pos = rp.position;
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
            var dir = rp.direction; dir.y = 0;
            spawn.SetPositionAndRotation(pos + Vector3.up * 0.1f, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var p = spawnParams;
            p.SpawnPoint = spawn; p.Track = route; p.Car = PickFor(route);
            p.color = Colors[rng.Next(Colors.Length)]; p.SpawnDriver = true;
            var car = Spawn(p, false);
            car.GetComponent<WaypointProgressTracker>().SetProgress(d);
            car.name = $"Map traffic {route.name} {i}";
            NumberPlates.Fit(car.gameObject, p.Car.name, NumberPlates.For(OsmMap.Current.Info.Country), new System.Random(Seed * 7919 + Cars.Count), car.transform.rotation);   // (its own draws: the traffic stays as it was)
            var traffic = car.gameObject.AddComponent<TrafficCar>();
            traffic.FitToBody();
            traffic.CentrePivot();
            float v = (DesiredSpeedKmh + Normal(rng) * DesiredSpeedSdKmh) / 3.6f;
            traffic.Init(CoupledSim.Traffic.CarFollowingModelType.IDM, Mathf.Max(3f, v), new System.Random(rng.Next()));
            // (on fast roads: the limit times the driver's own factor, 0.85-1.12)
            traffic.SpeedLimitFactor = Mathf.Clamp(v * 3.6f / DesiredSpeedKmh, 0.85f, 1.12f);
            traffic.Speed = Mathf.Max(2f, v * 0.6f);
            car.gameObject.AddComponent<MapRouteRecycler>();
            Cars.Add(traffic);
            count++;
            if (count % 10 == 0) yield return null;
        }
        Destroy(spawn.gameObject);
        if (keptOff > 0) Debug.Log($"MapTraffic: {keptOff} trucks or buses swapped for vehicles that turn tighter (their routes bend tighter than they can)");
        if (keptNarrow > 0) Debug.Log($"MapTraffic: {keptNarrow} wide vehicles swapped for narrower ones (their routes pass parked cars, posts or walls too near for their width)");
        Debug.Log($"MapTraffic: {count} cars{(places.Count > MaxCars && placed.Count > 0 ? $" (of {places.Count} places at {CarsPerKm} per km: those within {Mathf.Sqrt(placed.Max(Near)):F0} m of the participants{(_wayAhead.Count > 0 ? " and of the autopilot's way ahead" : "")})" : "")}");
        yield return SpawnCyclists();
        yield return SpawnPedestrians();
    }

    /// <summary>The pedestrians walking the map's pavements.</summary>
    public readonly List<AIPedestrian> Pedestrians = new List<AIPedestrian>();

    /// <summary>
    /// Pedestrians on the pavements (the map's pavement lines; not its crossings): each walks a stretch of pavement to its
    /// end and back, the stretch cut where the line meets a carriageway or a cycle path (no one steps into the traffic)
    /// and kept 0.65 m clear of posts, trees, benches and walls (0.5 m: walkers brushed benches and posts) (moved aside by up to 1 m, else cut there), at
    /// PedestriansPerKm on the stretches nearest the participants (up to MaxPedestrians, 4 m apart, out of the
    /// participants' way). The pedestrian avatars in everyday clothes (women, men, children), each with its age group's walking speed, gap acceptance and looks
    /// (AIPedestrian's behaviour models); one adult in six walks as an older person.
    /// </summary>
    IEnumerator SpawnPedestrians()
    {
        var gen = OsmMap.Current.Generator;
        var assets = Resources.Load<PedestrianAvatars>("PedestrianAvatars");
        // (everyday clothes: not the athletic avatar in swimwear-like sportswear)
        var avatars = assets != null ? assets.Prefabs.Where(p => p != null && !p.name.Contains("Sports")).ToArray() : new GameObject[0];
        if (gen == null || avatars.Length == 0 || PedestriansPerKm <= 0f || MaxPedestrians <= 0) yield break;
        Physics.SyncTransforms();
        var rng = new System.Random(Seed + 29);
        // (the participants: not the map's own traffic, "Map traffic ...", "Map cyclist ...")
        var players = FindObjectsByType<PlayerAvatar>().Where(a => !a.name.StartsWith("Map ") && a.GetComponent<AIPedestrian>() == null).Select(a => a.transform.position).ToList();
        if (players.Count == 0) players.Add(Vector3.zero);
        float Near(Vector3 q) => NearParticipants(q, players);
        bool Walkable(Vector3 q) { var g = SeenGround(q); return g != Ground.Road && g != Ground.Cycle && g != Ground.None; }
        bool Clear(Vector3 q)
        {
            int n = Physics.OverlapCapsuleNonAlloc(q + Vector3.up * 0.6f, q + Vector3.up * 1.5f, 0.65f, _near, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _near[i];
                if (c.attachedRigidbody != null) continue;
                var nm = c.name;
                if (nm.StartsWith("Roads") || nm.StartsWith("Road ") || nm.StartsWith("BGT") || nm.StartsWith("Ground") || nm.StartsWith("Terrain") || nm.StartsWith("Kerbs") || nm.StartsWith("Pavements") || nm.StartsWith("Cycle") || nm.StartsWith("Green") || nm.StartsWith("Zebra")) continue;
                if (c.bounds.max.y < q.y + 0.4f) continue;
                return false;
            }
            return true;
        }
        // the stretches: every 2 m along each line, moved clear (up to 1 m aside) or cut
        var stretches = new List<List<Vector3>>();
        foreach (var line in gen.Pavements)
        {
            if (line == null || line.Count < 2 || gen.CrossingLines.Contains(line)) continue;
            var cur = new List<Vector3>();
            // (every 2 m along the whole line, not from each of its pieces' starts: a line with points every metre gave a
            // point every metre, and the pedestrians, placed at 2 m per point, were given a progress twice as far along
            // their way and walked to it straight over the roads)
            float next = 0f, run = 0f;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i]; var b = line[i + 1]; float len = Vector3.Distance(a, b);
                var side = Vector3.Cross(Vector3.up, Flat(b - a).normalized);
                float first = next - run;
                run += len;
                for (float d = first; d < len; d += 2f, next += 2f)
                {
                    var q = Vector3.Lerp(a, b, d / Mathf.Max(len, 1e-3f));
                    if (Physics.Raycast(q + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) q.y = hit.point.y;
                    Vector3? ok = null;
                    foreach (float off in new[] { 0f, 0.25f, -0.25f, 0.5f, -0.5f, 0.75f, -0.75f, 1f, -1f })
                    {
                        var t = q + side * off;
                        if (Walkable(t) && Clear(t)) { ok = t; break; }
                    }
                    // (and the way there from the last point clear too: a tree between two clear points)
                    if (ok.HasValue && cur.Count > 0 && !Clear((cur[cur.Count - 1] + ok.Value) / 2f)) ok = null;
                    if (ok.HasValue) cur.Add(ok.Value);
                    else { if (cur.Count >= 15) stretches.Add(cur); cur = new List<Vector3>(); }
                }
            }
            if (cur.Count >= 15) stretches.Add(cur);
        }
        // places: along each stretch at the density, the nearest to the participants first
        var places = new List<(List<Vector3> line, int i)>();
        // (the distance along each stretch to each of its points: about 2 m apart, more where one was moved aside)
        var alongOf = new Dictionary<List<Vector3>, float[]>();
        foreach (var st in stretches)
        {
            var al = new float[st.Count];
            for (int k = 1; k < st.Count; k++) al[k] = al[k - 1] + Vector3.Distance(st[k - 1], st[k]);
            alongOf[st] = al;
        }
        foreach (var st in stretches)
        {
            int n = Mathf.Max(0, Mathf.RoundToInt(alongOf[st][st.Count - 1] / 1000f * PedestriansPerKm + (float)rng.NextDouble() - 0.5f));
            for (int k = 0; k < n; k++) places.Add((st, (int)((k + 0.5f) / n * (st.Count - 1))));
        }
        places = places.OrderBy(x => Near(x.line[x.i])).ToList();
        var sync = FindAnyObjectByType<ExperimentDefinition>()?.AIPedestrians;
        var circuits = new Dictionary<List<Vector3>, WaypointCircuit>();
        var placed = new List<Vector3>();
        var participants = Participants();
        int count = 0, index = 0;
        foreach (var (line, i) in places)
        {
            if (count >= MaxPedestrians) break;
            var at = line[i];
            if (placed.Any(q => (q - at).sqrMagnitude < 4f * 4f)) continue;
            if (Cars.Any(c => c != null && (c.transform.position - at).sqrMagnitude < 3f * 3f)) continue;
            if (InParticipantsWay(at, participants)) continue;
            placed.Add(at);
            if (!circuits.TryGetValue(line, out var circuit))
            {
                // there and back along the stretch (the circuit closes where it began)
                var loop = new List<Vector3>(line);
                for (int k = line.Count - 2; k >= 1; k--) loop.Add(line[k]);
                circuit = MakeCircuit(loop.Select(q => q - Vector3.up * 0.2f).ToList(), Routes.Count + 5000 + index++);
                circuit.name = "Map pavement " + index;
                circuit.GetRoutePoint(0f);
                circuits[line] = circuit;
            }
            // (going one way or the other along it)
            bool back = rng.NextDouble() < 0.5;
            float along = alongOf[line][i];
            float progress = back ? circuit.OpenLength - along : along;
            var prefab = avatars[rng.Next(avatars.Length)];
            var rp = circuit.GetRoutePoint(progress);
            var dir = Flat(rp.direction); if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            var go = Instantiate(prefab, at, Quaternion.LookRotation(dir.normalized, Vector3.up));
            go.name = $"Map pedestrian {count} ({prefab.name.Replace("Pedestrian_", "")})";
            var ped = go.GetComponentInChildren<AIPedestrian>(true);
            if (ped == null) { Destroy(go); continue; }
            ped.Behaviour = AIPedestrian.BehaviourMode.BehaviourModels;
            ped.SampleWalkingSpeed = true;
            ped.Seed = Seed * 7919 + count;
            if (prefab.name.Contains("Child")) ped.AgeGroup = CoupledSim.Pedestrians.AgeGroup.Child;
            else if (rng.NextDouble() < 1.0 / 6.0) ped.AgeGroup = CoupledSim.Pedestrians.AgeGroup.Older;
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = true;
            // (posed whether a camera is known to see it or not: with the prefabs' culling, a video's camera did not count,
            // and pedestrians slid along in a standing pose in the README's rush-hour clip)
            if (anim != null) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ped.Init(circuit);
            var tracker = go.GetComponent<WaypointProgressTracker>();
            if (tracker != null) tracker.SetProgress(progress);
            var avatar = go.GetComponent<PlayerAvatar>();
            if (avatar != null) sync?.Register(avatar);
            Pedestrians.Add(ped);
            count++;
            if (count % 10 == 0) yield return null;
        }
        Debug.Log($"MapTraffic: {count} pedestrians on {circuits.Count} stretches of pavement ({stretches.Count} stretches, {places.Count} places at {PedestriansPerKm:F1} per km)");
    }

    /// <summary>Cyclists along the routes: on a cycle path or cycle track beside the road where there is one, else on the
    /// carriageway near its kerb (RideLine), not on roads over 60 km/h (motorways, trunk roads). The routes nearest the
    /// participants first, and on them the places nearest the participants (up to MaxCyclists), so the cyclists ride
    /// where they are seen, also on a map with long corridors.</summary>
    /// <summary>Whether a place is in a participant's way now: within 8 m of one, or in the 25 m in front of one (3 m to
    /// either side). Cyclists and pedestrians are placed after the participants have started (in New York a cyclist was
    /// placed in front of the autopilot's bonnet, and it drove into it).</summary>
    static bool InParticipantsWay(Vector3 q, List<Transform> participants)
    {
        foreach (var t in participants)
        {
            if (t == null) continue;
            var rel = q - t.position; rel.y = 0f;
            if (rel.sqrMagnitude < 8f * 8f) return true;
            var f = t.forward; f.y = 0f; f.Normalize();
            float along = Vector3.Dot(rel, f), across = Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, f)));
            if (along > 0f && along < 25f && across < 3f) return true;
        }
        return false;
    }

    static List<Transform> Participants() => FindObjectsByType<PlayerAvatar>().Where(a => !a.name.StartsWith("Map ") && a.GetComponent<AIPedestrian>() == null).Select(a => a.transform).ToList();

    IEnumerator SpawnCyclists()
    {
        var sumo = FindAnyObjectByType<SumoBridge>();
        var bikes = sumo != null && sumo.BicyclePrefabs != null && sumo.BicyclePrefabs.Length > 0 ? sumo.BicyclePrefabs : _syncSystem.Prefabs.Where(p => p != null && p.name.Contains("Bicycle") && !p.name.Contains("Race")).ToArray();
        var raceBike = _syncSystem.Prefabs.FirstOrDefault(p => p != null && p.name.Contains("RaceBicycle"));
        if (bikes.Length == 0 || CyclistsPerKm <= 0f || MaxCyclists <= 0) yield break;
        var country = OsmMap.Current.Info.Country ?? "";
        float perKm = CyclistsPerKm;   // (the country's cycling in it already: ApplyDensities)
        bool lht = OsmMap.Current.Info.LeftHandTraffic;
        var rng = new System.Random(Seed + 11);
        var players = FindObjectsByType<PlayerAvatar>().Where(a => !a.name.StartsWith("Map ") && a.GetComponent<AIPedestrian>() == null).Select(a => a.transform.position).ToList();
        if (players.Count == 0) players.Add(Vector3.zero);
        float Near(Vector3 q) => NearParticipants(q, players);
        // each route's stretch for cyclists: the one nearest the participants without a road over 60 km/h
        var stretches = new List<(WaypointCircuit route, float from, float to, float near)>();
        foreach (var route in Routes.ToList())
        {
            float from = -1f, near = float.MaxValue;
            (float from, float to, float near) best = (0f, 0f, float.MaxValue);
            for (float d = 0f; d <= route.OpenLength; d += 10f)
            {
                var q = route.GetRoutePoint(d).position;
                bool town = d < route.OpenLength - 10f && RoadSpeedLimits.At(q) <= 60f;
                if (town) { if (from < 0f) { from = d; near = float.MaxValue; } near = Mathf.Min(near, Near(q)); }
                else if (from >= 0f) { if (d - from >= 150f && near < best.near) best = (from, d, near); from = -1f; }
            }
            if (best.near < float.MaxValue) stretches.Add((route, best.from, best.to, best.near));
        }
        stretches.Sort((x, y) => x.near.CompareTo(y.near));
        var places = new List<(WaypointCircuit bike, int i, float d, Vector3 at)>();
        var stretchOf = new Dictionary<WaypointCircuit, (WaypointCircuit route, float from, float to, List<Vector3> line)>();
        int index = 0;
        foreach (var (route, from, to, near) in stretches)
        {
            // (enough places near the participants: the further routes would not get any)
            if (places.Count >= MaxCyclists * 2 && near > places.Select(x => Near(x.at)).OrderBy(x => x).ElementAt(MaxCyclists - 1)) break;
            var kerb = RideLine(route, from, to, 4f, lht, true);
            if (kerb.Count < 10) continue;
            KeepRideClear(kerb, lht);   // (posts, lamps, trees and walls: a cyclist rode into a street lamp)
            var bikeRoute = MakeCircuit(kerb, Routes.Count + 1000 + index++);
            bikeRoute.name = "Map cycle route " + index;
            bikeRoute.GetRoutePoint(0f);
            stretchOf[bikeRoute] = (route, from, to, kerb);
            float bikeLength = bikeRoute.OpenLength;
            int n = Mathf.RoundToInt(bikeLength / 1000f * perKm);
            for (int i = 0; i < n; i++)
            {
                float d = bikeLength * (i + 0.3f) / n;
                if (d <= bikeLength - 60f) places.Add((bikeRoute, i, d, bikeRoute.GetRoutePoint(d).position));
            }
        }
        // race cyclists: their own line on the carriageway (where the road is up to 50 km/h), made for a stretch when one
        // rides there; preferably a stretch whose cyclists are on a cycle path beside the road (where the race cyclist
        // overtakes no one on its line), on a map without cycle paths any
        var raceLines = new Dictionary<WaypointCircuit, WaypointCircuit>();
        bool cyclingCountry = country == "Netherlands" || country == "Belgium" || country == "Denmark";
        bool anyCyclePaths = OsmMap.Current.Generator != null && OsmMap.Current.Generator.CyclePaths.Count > 0;
        int raceCount = 0, raceIndex = 0;
        WaypointCircuit RaceRoute(WaypointCircuit bikeRoute)
        {
            if (raceLines.TryGetValue(bikeRoute, out var rr)) return rr;
            var (route, from, to, kerb) = stretchOf[bikeRoute];
            // (in the Netherlands, Belgium and Denmark a race cyclist takes the cycle path too wherever there is one: it is
            // compulsory there, and one on the road beside it is a sight one does not see)
            var line = RideLine(route, from, to, 4f, lht, cyclingCountry, RaceOnRoadUpToKmh, 1.0f);
            KeepRideClear(line, lht);
            rr = null;
            if (line.Count >= 10)
            {
                // (apart from the cyclists' line: more than 1.5 m on average, where both are)
                float apart = 0f; int m = Mathf.Min(line.Count, kerb.Count);
                for (int k = 0; k < m; k++) apart += Vector3.Distance(line[k], kerb[k]);
                if (!anyCyclePaths || cyclingCountry || apart / Mathf.Max(1, m) > 1.5f)
                {
                    rr = MakeCircuit(line, Routes.Count + 3000 + raceIndex++);
                    rr.name = "Map race route " + raceIndex;
                    rr.GetRoutePoint(0f);
                    if (Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0)
                    {
                        int onCycle = line.Count(q => SeenGround(q) == Ground.Cycle), fast = line.Count(q => RoadSpeedLimits.At(q) > RaceOnRoadUpToKmh);
                        Debug.Log($"TrafficDebug: {rr.name}: {line.Count} points from ({line[0].x:F0},{line[0].z:F0}), {onCycle} of them on a cycle path or lane, {fast} beside roads over {RaceOnRoadUpToKmh:F0} km/h, {apart / Mathf.Max(1, m):F1} m from the cyclists' line");
                    }
                }
            }
            return raceLines[bikeRoute] = rr;
        }
        var spawn = new GameObject("Map cyclists spawn point").transform;
        int count = 0;
        var placed = Cars.Select(c => c.transform.position).ToList();   // (6 m from every car and cyclist placed)
        var participants = Participants();
        var used = new HashSet<WaypointCircuit>();
        foreach (var (bikeRoute, i, d, at) in places.OrderBy(x => Near(x.at)))
        {
            if (count >= MaxCyclists) break;
            // (every so often a race cyclist, on its line on the carriageway beside this stretch)
            bool race = raceBike != null && RaceCyclistShare > 0f && raceCount < Mathf.CeilToInt(MaxCyclists * RaceCyclistShare)
                && (count + 1) * RaceCyclistShare >= raceCount + 1f;
            var ride = bikeRoute; float rd = d;
            if (race)
            {
                var rr = RaceRoute(bikeRoute);
                if (rr == null) race = false;
                else { ride = rr; rd = Mathf.Min(d, rr.OpenLength - 60f); if (rd < 0f) race = false; }
                if (!race) { ride = bikeRoute; rd = d; }
            }
            var rp = ride.GetRoutePoint(rd);
            if (placed.Any(q => (q - rp.position).sqrMagnitude < 6f * 6f)) continue;
            if (InParticipantsWay(rp.position, participants)) continue;
            if (VehicleAt(rp.position, rp.direction, 1.4f, 0.8f)) continue;
            if (AnimalAgent.All.Any(an => an != null && (an.transform.position - rp.position).sqrMagnitude < 3f * 3f)) continue;
            // (its bicycle clear of what stands beside its line: placed on a bend of a campus path, a cyclist stood 15 s with its
            // front wheel 15-30 cm into a hedge, in Eindhoven)
            if (!RiderClear(rp.position, rp.direction)) continue;
            placed.Add(rp.position);
            if (used.Add(ride)) CycleRoutes.Add(ride);
            var pos = rp.position;
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
            var dir = rp.direction; dir.y = 0;
            spawn.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var p = spawnParams;
            p.SpawnPoint = spawn; p.Track = ride; p.Car = race ? raceBike : bikes[rng.Next(bikes.Length)]; p.color = Colors[rng.Next(Colors.Length)]; p.SpawnDriver = true;
            var bike = Spawn(p, false);
            bike.GetComponent<WaypointProgressTracker>().SetProgress(rd);
            bike.name = race ? $"Map race cyclist {ride.name.Substring("Map race route ".Length)} {i}" : $"Map cyclist {bikeRoute.name.Substring("Map cycle route ".Length)} {i}";
            var t = bike.gameObject.AddComponent<TrafficCar>();
            t.VehicleLength = 1.8f; t.LaneHalfWidth = 0.8f;
            // (the rider pedals and looks round whether a camera is known to see it or not, as the pedestrians)
            foreach (var an in bike.GetComponentsInChildren<Animator>(true)) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (!race) t.InitRideLane(new System.Random(rng.Next()), OsmMap.Current.Info.LeftHandTraffic);
            // city cyclists about 16 km/h; race cyclists about 32 km/h (28-38), quicker off the mark
            float v = race ? (32f + Normal(rng) * 2.5f) / 3.6f : (16f + Normal(rng) * 3f) / 3.6f;
            t.Init(CoupledSim.Traffic.CarFollowingModelType.IDM, race ? Mathf.Clamp(v, 7.8f, 10.5f) : Mathf.Clamp(v, 2.5f, 7f), new System.Random(rng.Next()));
            if (race && t.Model is CoupledSim.Traffic.IntelligentDriverModel idm) { idm.MaxAcceleration = Mathf.Max(idm.MaxAcceleration, 1.6f); t.VehicleLength = 1.75f; raceCount++; }
            t.Speed = v * 0.7f;
            bike.gameObject.AddComponent<MapRouteRecycler>().KeepNear = true;
            Cars.Add(t);
            count++;
            if (count % 10 == 0) yield return null;
        }
        // (the cycle routes without a cyclist)
        int withCyclists = used.Count;
        foreach (var (bikeRoute, _, _, _) in places) if (used.Add(bikeRoute)) Destroy(bikeRoute.gameObject);
        foreach (var rr in raceLines.Values) if (rr != null && used.Add(rr)) Destroy(rr.gameObject);
        _laneCells = null;   // (built again with the cycle routes)
        Destroy(spawn.gameObject);
        Debug.Log($"MapTraffic: {count} cyclists on {withCyclists} cycle routes ({raceCount} of them race cyclists{(cyclingCountry ? ", on the cycle path where there is one" : " on the carriageway")})");
    }

    List<List<Vector3>> _correctedLanes;

    /// <summary>One more lane (the autopilot's route to a destination) given the traffic routes' corrections, against
    /// their lanes: KeepOnCarriageway, smoothed, KeepOnCarriageway again, SpreadFromOncoming, KeepLaneClear.</summary>
    List<Vector3> CorrectLane(List<Vector3> lane)
    {
        var all = new List<List<Vector3>>(_correctedLanes ?? new List<List<Vector3>>()) { lane };
        Physics.SyncTransforms();
        BuildOncomingIndex(all);
        KeepOnCarriageway(lane);
        lane = SmoothMoved(lane);
        all[all.Count - 1] = lane;
        BuildOncomingIndex(all);
        KeepOnCarriageway(lane);
        BuildOncomingIndex(all);
        SpreadFromOncoming(lane, OsmMap.Current.Info.LeftHandTraffic);
        BuildOncomingIndex(all);
        int left = 0;
        KeepLaneClear(lane, ref left);
        _oncoming = null;
        return lane;
    }

    /// <summary>
    /// Routes for the streets near the participants that the map's through routes (edge to edge) leave out: every road
    /// stretch within 300 m of a participant without a route lane on it (none within 6 m, the same way) gets a route from
    /// there, the quickest some 500 m on (MapRoutes.ShortestPath), at most 16 per participant, 40 m apart; they then
    /// get the routes' corrections and their share of the traffic like the others. On the TU/e campus, none of the 140
    /// routes used De Zaale, and the participant drove alone through the rush hour.
    /// </summary>
    List<List<Vector3>> LocalRoutes(List<List<Vector3>> lanes, List<CoupledSim.Maps.MapRoutes.RoadLine> lines, bool lht)
    {
        var result = new List<List<Vector3>>();
        var participants = FindObjectsByType<PlayerAvatar>().Where(a => !a.name.StartsWith("Map ") && a.GetComponent<AIPedestrian>() == null).Select(a => a.transform.position).ToList();
        if (participants.Count == 0 || lines == null) return result;
        // the lanes' points every 2 m with their headings, in 6 m cells
        var cells = new Dictionary<long, List<(Vector3 p, Vector3 d)>>();
        long Cell(Vector3 q) => ((long)Mathf.FloorToInt(q.x / 6f) << 32) ^ (uint)Mathf.FloorToInt(q.z / 6f);
        void AddLane(List<Vector3> l)
        {
            for (int i = 0; i + 1 < l.Count; i++)
            {
                var d = Flat(l[i + 1] - l[i]); if (d.sqrMagnitude < 1e-4f) continue;
                var k = Cell(l[i]); if (!cells.TryGetValue(k, out var c)) cells[k] = c = new List<(Vector3, Vector3)>();
                c.Add((l[i], d.normalized));
            }
        }
        foreach (var l in lanes) AddLane(l);
        bool Covered(Vector3 q, Vector3 dir)
        {
            int cx = Mathf.FloorToInt(q.x / 6f), cz = Mathf.FloorToInt(q.z / 6f);
            for (int x = cx - 1; x <= cx + 1; x++) for (int z = cz - 1; z <= cz + 1; z++)
                if (cells.TryGetValue(((long)x << 32) ^ (uint)z, out var c))
                    foreach (var (p, d) in c) if (Flat(p - q).sqrMagnitude < 36f && Vector3.Dot(d, dir) > 0.5f) return true;
            return false;
        }
        var done = new HashSet<long>();
        foreach (var who in participants)
        {
            int made = 0;
            var candidates = new List<(Vector3 at, Vector3 dir, float dist)>();
            foreach (var r in lines)
            {
                if (r.Line == null || r.Line.Count < 2 || r.SpeedKmh > 70f) continue;
                for (int i = 0; i + 1 < r.Line.Count; i++)
                {
                    var a = new Vector3(r.Line[i].X, r.Line[i].Y, r.Line[i].Z); var b = new Vector3(r.Line[i + 1].X, r.Line[i + 1].Y, r.Line[i + 1].Z);
                    float len = Vector3.Distance(a, b);
                    for (float t = 0f; t < len; t += 20f)
                    {
                        var q = Vector3.Lerp(a, b, t / Mathf.Max(len, 1e-3f));
                        float dist = Flat(q - who).magnitude;
                        if (dist > 300f) continue;
                        var dir = Flat(b - a).normalized;
                        candidates.Add((q, dir, dist));
                        if (!r.OneWay) candidates.Add((q, -dir, dist));
                    }
                }
            }
            foreach (var (at, dir, dist) in candidates.OrderBy(c => c.dist))
            {
                if (made >= 16) break;
                var key = ((long)Mathf.FloorToInt(at.x / 40f) << 32) ^ (uint)Mathf.FloorToInt(at.z / 40f);
                // (the lane is to the driver's side of the road's centre line: looked for there)
                var side = Vector3.Cross(Vector3.up, dir) * (lht ? -1f : 1f);
                if (Covered(at + side * 1.5f, dir) || Covered(at, dir) || !done.Add(key * 2 + (dir.x + dir.z > 0f ? 1 : 0))) continue;
                var to = at + dir * 500f;
                var path = CoupledSim.Maps.MapRoutes.ShortestPath(lines, new System.Numerics.Vector3(at.x, at.y, at.z), new System.Numerics.Vector3(dir.x, 0f, dir.z),
                    new System.Numerics.Vector3(to.x, 0f, to.z), lht);
                if (path.Count < 2) continue;
                var lane = path.Select(q => new Vector3(q.X, q.Y, q.Z)).ToList();
                float length = 0f; for (int i = 1; i < lane.Count; i++) length += Vector3.Distance(lane[i - 1], lane[i]);
                if (length < 150f) continue;
                result.Add(lane);
                AddLane(lane);
                made++;
            }
        }
        if (result.Count > 0) Debug.Log($"MapTraffic: {result.Count} local routes for the streets near the participants that the through routes leave out");
        return result;
    }

    /// <summary>The participant's car drives itself along the route nearest to it (where it points).</summary>
    void Autopilot()
    {
        var ps = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem : null;
        var me = ps != null ? ps.LocalPlayer : null;
        if (me == null) return;
        if (me.GetComponent<TwoWheelerController>() != null || me.GetComponent<KeyboardPlayerMovement>() != null) { WalkOrRide(me); return; }
        if (me.Type != AvatarType.Driver || Routes.Count == 0) { Debug.LogWarning("MapTraffic: no autopilot for this role"); return; }
        var tracker = me.GetComponent<WaypointProgressTracker>();
        var aiCar = me.GetComponent<AICar>();
        if (tracker == null || aiCar == null) { Debug.LogWarning("MapTraffic: this car has no automation"); return; }
        WaypointCircuit best = null; float bestD = 0f, bestScore = float.MaxValue;
        var at = me.transform.position; var fwd = me.transform.forward;
        // destination=lat,lon (-destination): the quickest route there through the road network
        best = RouteToDestination(at, fwd, out bestD);
        if (best != null) bestScore = 0f;
        // (a route whose lane runs over unpaved ground in the next 300 m, where OpenStreetMap's road crosses a verge or a
        // median the topography has as grass, counts as 20 m further away: of the routes along the participant's street, one
        // that keeps to the road is taken, then one that goes straight on)
        var unpaved = new Dictionary<WaypointCircuit, List<float>>();
        List<float> Unpaved(WaypointCircuit r)
        {
            if (unpaved.TryGetValue(r, out var u)) return u;
            u = new List<float>();
            for (float d = 0; d < r.OpenLength; d += 2f) if (SeenGround(r.GetRoutePoint(d).position) is var g && (g == Ground.Green || g == Ground.Bare)) u.Add(d);
            return unpaved[r] = u;
        }
        if (best == null)
        foreach (var r in Routes)
            for (float d = 0; d < r.OpenLength - 150f; d += 4f)
            {
                var rp = r.GetRoutePoint(d);
                float score = (rp.position - at).sqrMagnitude + (Vector3.Dot(rp.direction, fwd) < 0.5f ? 400f : 0f);
                if (score >= bestScore || (rp.position - at).sqrMagnitude > 900f) { if (score < bestScore) { bestScore = score; best = r; bestD = d; } continue; }
                if (Unpaved(r).Exists(x => x >= d && x <= d + 300f)) score += 400f;
                // (of routes running together along the street, the one that goes straight on rather than turning off at
                // the next junction: as 2 m further for every 90° it turns in the next 150 m)
                if ((rp.position - at).sqrMagnitude < 25f)
                {
                    float turn = 0f; var prevDir = rp.direction;
                    for (float k = 10f; k <= 150f && d + k < r.OpenLength; k += 10f) { var dir = r.GetRoutePoint(d + k).direction; turn += Vector3.Angle(Flat(prevDir), Flat(dir)); prevDir = dir; }
                    score += 4f * turn / 90f;
                }
                if (score < bestScore) { bestScore = score; best = r; bestD = d; }
            }
        if (System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0)
        {
            var near = Routes.Select(r => { float bd = float.MaxValue, dd = 0f; for (float d = 0; d < r.OpenLength; d += 4f) { var q = r.GetRoutePoint(d); float x = (q.position - at).sqrMagnitude; if (x < bd) { bd = x; dd = d; } } return (r, Mathf.Sqrt(bd), dd, Vector3.Dot(r.GetRoutePoint(dd).direction, fwd)); })
                .OrderBy(t => t.Item2).Take(6).Select(t => $"{t.r.name} {t.Item2:F0} m at {t.dd:F0} of {t.r.OpenLength:F0} m, heading {t.Item4:F2}");
            Debug.Log("TrafficDebug: routes near the participant: " + string.Join("; ", near));
        }
        if (best == null)
        {
            foreach (var r in Routes)
                if (r != null && (best == null || r.OpenLength > best.OpenLength)) { best = r; bestD = 0f; }
        }
        if (best == null) { Debug.LogWarning("MapTraffic: no suitable route for autopilot"); return; }
        var start = best.GetRoutePoint(bestD);
        if (System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0)
        {
            // the ground across the lane where the autopilot starts, as seen (left to right, every 0.5 m)
            var across = Vector3.Cross(Vector3.up, new Vector3(start.direction.x, 0f, start.direction.z).normalized);
            var sb = new System.Text.StringBuilder();
            for (int k = -10; k <= 10; k++) { var g = SeenGround(start.position + across * (k * 0.5f)); sb.Append(k == 0 ? "[" : "").Append(g == Ground.Road ? 'R' : g == Ground.Cycle ? 'C' : g == Ground.Pavement ? 'P' : g == Ground.Paving ? 'p' : g == Ground.Green ? 'G' : g == Ground.Bare ? 'b' : '-').Append(k == 0 ? "]" : ""); }
            Debug.Log($"TrafficDebug: the ground across the autopilot's lane at its start, every 0.5 m from 5 m left (R road, C cycle, P pavement, p other paving, - none): {sb}");
            var path = new System.Text.StringBuilder();
            for (float d = bestD; d < Mathf.Min(best.OpenLength, bestD + 400f); d += 2f) { var q = best.GetRoutePoint(d).position; path.Append($" {q.x:F1},{q.z:F1}"); }
            Debug.Log($"TrafficDebug: the autopilot's route every 2 m:{path}");
            // the ground across the lane every 10 m along it (K: a parked car)
            // (every 2 m over the first 120 m, where the videos turn; then every 10 m)
            for (float d = bestD; d < Mathf.Min(best.OpenLength, bestD + 400f); d += d - bestD < 120f ? 2f : 10f)
            {
                var rp = best.GetRoutePoint(d);
                var ac = Vector3.Cross(Vector3.up, new Vector3(rp.direction.x, 0f, rp.direction.z).normalized);
                var row = new System.Text.StringBuilder();
                for (int k = -10; k <= 10; k++)
                {
                    var q = rp.position + ac * (k * 0.5f);
                    bool parked = false;
                    int n = Physics.OverlapSphereNonAlloc(q + Vector3.up * 0.7f, 0.2f, _near, ~0, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < n && !parked; j++) for (var t = _near[j].transform; t != null; t = t.parent) if (t.name == "Parked car") { parked = true; break; }
                    var g = SeenGround(q);
                    row.Append(k == 0 ? "[" : "").Append(parked ? 'K' : g == Ground.Road ? 'R' : g == Ground.Cycle ? 'C' : g == Ground.Pavement ? 'P' : g == Ground.Paving ? 'p' : g == Ground.Green ? 'G' : g == Ground.Bare ? 'b' : '-').Append(k == 0 ? "]" : "");
                }
                var under = new System.Text.StringBuilder();
                int nh = Physics.RaycastNonAlloc(rp.position + Vector3.up * 4f, Vector3.down, _groundHits, 10f, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < nh; j++) under.Append($" {_groundHits[j].collider.name}@{_groundHits[j].point.y:F2}");
                Debug.Log($"TrafficDebug: across the autopilot's lane at {d - bestD:F0} m ({rp.position.x:F1},{rp.position.z:F1}): {row} (under it:{under})");
            }
        }
        // (facing along the route ahead: a closed circuit's direction at its start points back along its closing piece,
        // and the autopilot stood facing into the TU/e's Atlas, its route running the other way)
        var ahead = best.GetRoutePoint(Mathf.Min(bestD + 3f, best.OpenLength)).position - start.position; ahead.y = 0f;
        if (ahead.sqrMagnitude < 0.25f) { ahead = start.direction; ahead.y = 0f; }
        if ((start.position - at).sqrMagnitude > 9f || Vector3.Angle(Flat(fwd), ahead) > 45f)
        {
            var p = start.position;
            if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.1f;
            var rb = me.GetComponent<Rigidbody>();
            var rot = Quaternion.LookRotation(ahead.normalized, Vector3.up);
            if (rb != null) { rb.position = p; rb.rotation = rot; rb.linearVelocity = Vector3.zero; }
            me.transform.SetPositionAndRotation(p, rot);
        }
        tracker.Init(best);
        tracker.SetProgress(bestD);
        _autopilotRoute = best; _autopilotFrom = bestD;
        _wayAhead.Clear();
        for (float w = 20f; w <= 1200f && bestD + w < best.OpenLength; w += 20f) _wayAhead.Add((best.GetRoutePoint(bestD + w).position, 0.3f * w));
        aiCar.target = tracker.target;
        if (!me.TryGetComponent<AutomatedDriving>(out var automation)) automation = me.gameObject.AddComponent<AutomatedDriving>();
        automation.Mode = AutomatedDriving.DrivingMode.TrafficModel;
        automation.DesiredSpeedKmh = AutopilotKmh;
        automation.SpeedLimitFactor = AutopilotFixed ? 0f : 1f;
        ps.ActivatePlayerAICar();
        me.gameObject.AddComponent<MapRouteRecycler>().Recycle = false;   // (it stops at its route's end)
        Debug.Log($"MapTraffic: autopilot at {AutopilotKmh} km/h{(AutopilotFixed ? "" : " in town, the speed limit on faster roads")} on {best.name} from {bestD:F0} m, at {start.position} (the participant was at {at})");
    }

    /// <summary>The route from <paramref name="at"/> to destination=lat,lon (-destination) by the quickest way through the
    /// road network, with the distance along it nearest <paramref name="at"/>; null without a destination or a way there.</summary>
    WaypointCircuit RouteToDestination(Vector3 at, Vector3 fwd, out float startD, bool rider = false)
    {
        startD = 0f;
        var dest = Destination != null ? Destination.Split(',') : null;
        var roads = rider && _rideLines != null ? _rideLines : _lines;
        if (dest == null || dest.Length < 2 || roads == null || !double.TryParse(dest[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var dla)
            || !double.TryParse(dest[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var dlo)) return null;
        new CoupledSim.Maps.GeoProjection(OsmMap.Current.Info.Latitude, OsmMap.Current.Info.Longitude).ToLocal(dla, dlo, out var dx, out var dz);
        // (a rider from the requested start and its bearing: the second participant's spawn point is on the next road, on
        // the TU/e campus 20 m away on Den Dolech; the rider is then put at the beginning of the path)
        if (rider && OsmMap.Current.RequestedStartPose(out var sp, out var sh) && (new Vector2(sp.x - at.x, sp.z - at.z)).sqrMagnitude < 100f * 100f)
        {
            at = new Vector3(sp.x, at.y, sp.z);
            if (sh != Vector3.zero) fwd = sh;
        }
        var path = CoupledSim.Maps.MapRoutes.ShortestPath(roads, new System.Numerics.Vector3(at.x, at.y, at.z), new System.Numerics.Vector3(fwd.x, 0f, fwd.z),
            new System.Numerics.Vector3((float)dx, 0f, (float)dz), OsmMap.Current.Info.LeftHandTraffic);
        if (path.Count < 2)
        {
            Debug.LogWarning($"MapTraffic: no route from here to {Destination} ({CoupledSim.Maps.MapRoutes.Why}); the autopilot takes the nearest route");
            if (Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0)
                foreach (var r in OsmMap.Current.Generator.Roads)
                {
                    float m = r.Line.Min(q => (new Vector2(q.x - at.x, q.z - at.z)).magnitude);
                    if (m < 50f) Debug.Log($"TrafficDebug: road {r.Type} {m:F0} m away, from ({r.Line[0].x:F0},{r.Line[0].z:F0}) to ({r.Line[r.Line.Count - 1].x:F0},{r.Line[r.Line.Count - 1].z:F0}){(r.OneWay ? " one-way" : "")}{(r.NoThroughTraffic ? " no through traffic" : "")}{(r.PavedOver ? " on paving" : "")}");
                }
            return null;
        }
        // (the traffic's corrections: on the carriageway, smoothed, on it again, apart from the oncoming lane, clear of what
        // stands beside the road; with one pass, smoothed, it cut a corner over a lawn on the TU/e campus)
        var lane = CorrectLane(path.Select(q => new Vector3(q.X, q.Y, q.Z)).ToList());
        EaseBends(lane);
        var route = MakeCircuit(lane, 900);
        route.name = "Route to " + Destination;
        route.GetRoutePoint(0f);
        // (from the point of it nearest the participant: the path begins at the road's node behind them)
        float nearest = float.MaxValue;
        for (float d = 0f; d <= Mathf.Min(40f, route.OpenLength - 5f); d += 0.5f)
        {
            float e = (route.GetRoutePoint(d).position - at).sqrMagnitude;
            if (e < nearest) { nearest = e; startD = d; }
        }
        Debug.Log($"MapTraffic: the autopilot goes to {Destination}: {route.OpenLength / 1000f:F1} km");
        return route;
    }

    /// <summary>Pedestrians walk along the nearest long pavement; riders follow the route to their destination, else the nearest lane route, on its riding line (RideLine).</summary>
    void WalkOrRide(PlayerAvatar me)
    {
        var gen = OsmMap.Current.Generator;
        var at = me.transform.position; var fwd = me.transform.forward;
        List<Vector3> path = null;
        if (me.GetComponent<KeyboardPlayerMovement>() != null)
        {
            float best = float.MaxValue;
            foreach (var l in gen.Pavements)
            {
                if (l.Count < 2 || CoupledSim.Maps.MapRoutes.Length(l.Select(q => new System.Numerics.Vector3(q.x, q.y, q.z)).ToList()) < 40f) continue;
                foreach (var q in l) { float d = (q - at).sqrMagnitude; if (d < best) { best = d; path = l; } }
            }
        }
        else if (Routes.Count > 0)
        {
            // (to a destination by the quickest way there, as a driver; else the nearest route pointing the rider's way)
            var bestR = RouteToDestination(at, fwd, out float bestD, rider: true); float bestScore = float.MaxValue;
            bool toDestination = bestR != null;
            if (bestR == null)
            foreach (var r in Routes)
                for (float d = 0; d < r.OpenLength - 100f; d += 4f)
                {
                    var rp = r.GetRoutePoint(d);
                    // (near, pointing the rider's way, and with at least 300 m to go: the ride does not end round the corner)
                    float score = (rp.position - at).sqrMagnitude + (Vector3.Dot(rp.direction, fwd) < 0.3f ? 900f : 0f) + (r.OpenLength - d < 350f ? 2500f : 0f);
                    if (score < bestScore) { bestScore = score; bestR = r; bestD = d; }
                }
            if (bestR == null)
                foreach (var r in Routes)
                    if (r != null && (bestR == null || r.OpenLength > bestR.OpenLength)) { bestR = r; bestD = 0f; }
            if (bestR != null)
            {
                // bicycles, e-scooters and 25 km/h mopeds on the cycle path or track beside the road where there is one;
                // 45 km/h mopeds on the road; race cyclists on the road up to 50 km/h, on the cycle path beside faster roads
                var tw = me.GetComponent<TwoWheelerController>();
                bool moped = tw != null && tw.Type == CoupledSim.TwoWheelers.TwoWheelerType.Moped45;
                bool racer = tw != null && tw.Type == CoupledSim.TwoWheelers.TwoWheelerType.RaceBike;
                // (to the destination itself; a traffic route's last 50 m left out, where it leaves the map)
                float end = Mathf.Max(bestD + 6f, toDestination ? bestR.OpenLength - 1f : bestR.OpenLength - 50f);
                path = racer ? RideLine(bestR, bestD, end, 3f, OsmMap.Current.Info.LeftHandTraffic, false, RaceOnRoadUpToKmh, 1.0f)
                    : RideLine(bestR, bestD, end, 3f, OsmMap.Current.Info.LeftHandTraffic, !moped);
                KeepRideClear(path, OsmMap.Current.Info.LeftHandTraffic);
                if (Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0 && _signPosts != null)
                {
                    // the sign posts nearest the riding line (its first 150 m), on the ground
                    var near = new List<(float d, Vector3 at)>();
                    foreach (var sp in _signPosts)
                    {
                        float best = float.MaxValue; float run = 0f;
                        for (int i = 0; i + 1 < path.Count && run < 150f; i++)
                        {
                            var a = path[i]; var b = path[i + 1]; run += Vector3.Distance(a, b);
                            var ab = b - a; ab.y = 0f; var aq = sp - a; aq.y = 0f;
                            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(aq, ab) / ab.sqrMagnitude) : 0f;
                            best = Mathf.Min(best, (aq - ab * t).magnitude);
                        }
                        if (best < 3f) near.Add((best, sp));
                    }
                    Debug.Log($"TrafficDebug: {_signPosts.Count} sign posts; nearest to the rider's line: " + string.Join("; ", near.OrderBy(x => x.d).Take(5).Select(x => $"{x.d:F2} m at ({x.at.x:F1},{x.at.z:F1})")));
                }
            }
        }
        if (path == null || path.Count < 2) { Debug.LogWarning("MapTraffic: no path for the autopilot"); return; }
        // start on the path, facing along it
        int i0 = 0; float bd = float.MaxValue;
        for (int i = 0; i < path.Count - 1; i++) { float d = (path[i] - at).sqrMagnitude; if (d < bd) { bd = d; i0 = i; } }
        if (bd > 1f)
        {
            var p0 = path[i0]; var dir = path[Mathf.Min(i0 + 1, path.Count - 1)] - p0; dir.y = 0;
            if (Physics.Raycast(p0 + Vector3.up * 3f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) p0.y = hit.point.y + 0.05f;
            var rot = Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir.normalized : me.transform.forward, Vector3.up);
            var rb = me.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = p0; rb.rotation = rot; rb.linearVelocity = Vector3.zero; }
            me.transform.SetPositionAndRotation(p0, rot);
            var tw = me.GetComponent<TwoWheelerController>();
            if (tw != null) tw.Teleport(p0, rot.eulerAngles.y);
        }
        me.gameObject.AddComponent<ParticipantAutopilot>().Init(path);
        SetWayAhead(path, i0);
        Debug.Log($"MapTraffic: autopilot for the {(me.GetComponent<KeyboardPlayerMovement>() != null ? "pedestrian" : "rider")} along {path.Count} points{(RideMoves > 0 ? $" ({RideMoves} of them moved clear of posts and pavement)" : "")}");
    }

    /// <summary>
    /// The lane routes come from OpenStreetMap's centre lines and widths; where the carriageway really is narrower or lies
    /// a little to the side (as the Dutch topography's surfaces show it), or has a painted cycle lane at its edge, a car on
    /// the lane would drive partly or wholly on the cycle path, cycle lane, pavement or parking strip beside it (and into the benches
    /// and posts there), or where the lanes of a junction cut across a grass verge or median (the Dutch topography's green
    /// areas), the corner of a canal's quay, or a bend's inside corner over the kerb (moved however short the stretch). Each point where the car's middle or either side (0.8 m out) is over a cycle path, cycle lane, pavement or
    /// such grass, as it is seen (<see cref="SeenGround"/>), is moved across by the least that keeps all three off them, at
    /// most 4.5 m (a lane on the far half of a two-way cycle path); the moves are smoothed (a median over 24 m, then an average over 12 m), so a lone point does not bend
    /// the lane and it does not zigzag. A cycle path or pavement that crosses the road (no clear carriageway beside it) is
    /// left alone. Returns how many points moved.
    /// </summary>
    static int KeepOnCarriageway(List<Vector3> lane)
    {
        if (lane.Count < 2) return 0;
        var shift = new float[lane.Count];
        var side = new Vector3[lane.Count];
        var grassOnly = new bool[lane.Count];
        bool any = false;
        bool Off(Ground g) => g == Ground.Cycle || g == Ground.Pavement || g == Ground.Green;
        // (and the car's middle over no drawn road at all: the map's bare ground or other paving, as at a corner where a
        // narrower street's road begins further over)
        bool Clear(Vector3 p, Vector3 s) => SeenGround(p) is var g && !Off(g) && g != Ground.Paving && g != Ground.None && g != Ground.Bare
            && !Off(SeenGround(p + s * 0.8f)) && !Off(SeenGround(p - s * 0.8f));
        for (int i = 0; i < lane.Count; i++)
        {
            var d = lane[Mathf.Min(i + 1, lane.Count - 1)] - lane[Mathf.Max(i - 1, 0)]; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) continue;
            side[i] = Vector3.Cross(Vector3.up, d.normalized);   // (to the right)
            if (Clear(lane[i], side[i])) continue;
            // (only grass or a quay in the way: a junction's lane cutting across a verge, a median or a canal's corner: moved
            // also for a short stretch)
            grassOnly[i] = !(SeenGround(lane[i]) is var g0 && (g0 == Ground.Cycle || g0 == Ground.Pavement))
                && !(SeenGround(lane[i] + side[i] * 0.8f) is var g1 && (g1 == Ground.Cycle || g1 == Ground.Pavement))
                && !(SeenGround(lane[i] - side[i] * 0.8f) is var g2 && (g2 == Ground.Cycle || g2 == Ground.Pavement));
            // the least move either way that clears the car of it
            for (int k = 1; k <= 18 && shift[i] == 0f; k++)   // (up to 4.5 m: a two-way cycle path is up to 4 m wide)
                foreach (int sgn in new[] { -1, 1 })
                    if (shift[i] == 0f && SeenGround(lane[i] + side[i] * (sgn * k * 0.25f)) == Ground.Road && Clear(lane[i] + side[i] * (sgn * k * 0.25f), side[i]))
                    { shift[i] = sgn * k * 0.25f; any = true; }
            // (the road within 3 m beside: a lane cutting a corner over the kerb and the pavement, as where it enters a
            // narrower street or rounds a bend; moved also for a short stretch, in a bend 0.3 m more for the vehicles' own
            // cut inside it; a pavement or cycle path that crosses the road leaves no road that near beside it)
            if (shift[i] != 0f && Mathf.Abs(shift[i]) <= 3f)
            {
                grassOnly[i] = true; CornerMoves++;
                if (i >= 2 && i + 2 < lane.Count)
                {
                    var a = lane[i] - lane[i - 2]; var b = lane[i + 2] - lane[i]; a.y = 0f; b.y = 0f;
                    if (Vector3.Angle(a, b) > 20f) shift[i] += Mathf.Sign(shift[i]) * 0.3f;
                }
            }
        }
        int moved = 0;
        // a median over 24 m (a lone point over a cycle path, as where one crosses the road, or a small patch, does
        // not bend the lane: only a stretch of some 15 m and more moves), then an average over 12 m, so the lane eases
        // across (the lanes' points are LaneSpacing, 2 m, apart)
        int mh = Mathf.Max(1, Mathf.RoundToInt(12f / CoupledSim.Maps.MapRoutes.LaneSpacing)), ah = Mathf.Max(1, Mathf.RoundToInt(6f / CoupledSim.Maps.MapRoutes.LaneSpacing));
        var median = new float[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            var w = new List<float>();
            for (int k = Mathf.Max(0, i - mh); k <= Mathf.Min(lane.Count - 1, i + mh); k++) w.Add(grassOnly[k] ? 0f : shift[k]);
            w.Sort(); median[i] = w[w.Count / 2];
        }
        var smooth = new float[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            float sum = 0f; int n = 0;
            for (int k = Mathf.Max(0, i - ah); k <= Mathf.Min(lane.Count - 1, i + ah); k++) { sum += median[k]; n++; }
            smooth[i] = sum / n;
        }
        // grass or a quay a junction's lane cuts across (moved by the least that clears it, as below), and parked cars (along the kerb, in parking bays and strips beside the road that OpenStreetMap maps as parking areas,
        // not as the road's own parking: its lane did not leave room for them): each point where a car on the lane (its
        // middle after the moves above) would come within 0.25 m of one is moved away from it by the least that clears it,
        // at most 2.5 m, onto the road; every move holds 6 m before and after the point (the gaps in a row of parked cars
        // do not bend the lane towards the kerb), and they are averaged as above
        var park = new float[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            if (side[i] == Vector3.zero) continue;
            if (grassOnly[i] && median[i] == 0f) { park[i] = shift[i]; continue; }
            var p = lane[i] + side[i] * median[i];
            var dir = Vector3.Cross(side[i], Vector3.up);
            if (!ParkedCarNear(p, dir)) continue;
            float before = OncomingDist(p, dir);   // (the move does not bring it closer to the oncoming lane than 2.3 m)
            for (int k = 1; k <= 10 && park[i] == 0f; k++)
                foreach (int sgn in new[] { -1, 1 })
                {
                    var q = p + side[i] * (sgn * k * 0.25f);
                    if (park[i] == 0f && !ParkedCarNear(q, dir) && SeenGround(q) == Ground.Road && Clear(q, side[i]) && OncomingDist(q, dir) >= Mathf.Min(2.3f, before)) park[i] = sgn * k * 0.25f;
                }
        }
        int ph = Mathf.Max(1, Mathf.RoundToInt(6f / CoupledSim.Maps.MapRoutes.LaneSpacing));
        var held = new float[lane.Count];
        for (int i = 0; i < lane.Count; i++)
            for (int k = Mathf.Max(0, i - ph); k <= Mathf.Min(lane.Count - 1, i + ph); k++)
                if (Mathf.Abs(park[k]) > Mathf.Abs(held[i])) held[i] = park[k];
        var total = new float[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            float sum = 0f; int n = 0;
            for (int k = Mathf.Max(0, i - ah); k <= Mathf.Min(lane.Count - 1, i + ah); k++) { sum += held[k]; n++; }
            total[i] = smooth[i] + sum / n;
        }
        if (DebugFocus != null)
            for (int i = 0; i < lane.Count; i++)
                if (Flat(lane[i] - DebugFocus.Value).sqrMagnitude < 12f * 12f)
                    Debug.Log($"TrafficDebug: lane point {i}/{lane.Count} at {lane[i]}: shift found {shift[i]:F2} (short {grassOnly[i]}), median {median[i]:F2}, applied {smooth[i]:F2}, parked cars {park[i]:F2}, in all {total[i]:F2}");
        // (no move brings the lane nearer than 2.3 m to the oncoming one, nor nearer than it was: two lanes of a two-way
        // street moved towards its middle came within 1.8 m, and a pickup and a tow truck met head-on)
        for (int i = 0; i < lane.Count; i++)
        {
            if (Mathf.Abs(total[i]) < 0.01f || side[i] == Vector3.zero) continue;
            var dir = Vector3.Cross(side[i], Vector3.up);
            // (a short move out of a corner or off a kerb may come to 1.9 m, as the narrowest streets' lanes: in a narrow
            // street entered round a corner a car keeps to its middle rather than to the pavement)
            float limit = Mathf.Min(grassOnly[i] ? 1.9f : 2.3f, OncomingDist(lane[i], dir));
            while (Mathf.Abs(total[i]) > 0.01f && OncomingDist(lane[i] + side[i] * total[i], dir) < limit - 0.01f)
                total[i] = Mathf.MoveTowards(total[i], 0f, 0.1f);
        }
        for (int i = 0; i < lane.Count; i++)
            if (Mathf.Abs(total[i]) > 0.01f) { _moves.Add((lane[i], total[i])); if (Mathf.Abs(park[i]) > 0f) ParkedMoves++; lane[i] += side[i] * total[i]; moved++; }
        return moved;
    }

    /// <summary>The ground at a point as it is seen: the highest surfaces under it that are drawn (on the maps with the
    /// national topography's surfaces, OpenStreetMap's roads are invisible driving surfaces under or over the topography's
    /// cycle paths and pavements, so the first collider is not always the surface one sees; and the topography's cycle
    /// paths can lie on its carriageway at the same height).</summary>
    /// <summary>Whether the surface one sees at a point is a carriageway (SeenGround; the collision audit's pedestrians on
    /// the road).</summary>
    public static bool OnCarriageway(Vector3 p) => SeenGround(p) == Ground.Road;

    static Ground SeenGround(Vector3 p)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 4f, Vector3.down, _groundHits, 10f, ~0, QueryTriggerInteraction.Ignore);
        float top = float.MinValue;
        for (int i = 0; i < n; i++) if (Seen(_groundHits[i]) && _groundHits[i].point.y > top) top = _groundHits[i].point.y;
        if (top == float.MinValue) return MapTraffic.Ground.None;
        // the surfaces at the top (within 5 cm: the topography's polygons overlap, a cycle path or cycle lane lies on the
        // carriageway's polygon at the same height, and the one drawn over it is what one sees): a cycle path or pavement
        // among them counts
        var g = MapTraffic.Ground.Paving; bool road = false, bare = false;
        for (int i = 0; i < n; i++)
        {
            var h = _groundHits[i];
            if (!Seen(h) || h.point.y < top - 0.05f) continue;
            var name = h.collider.name;
            if (name.IndexOf("ycle", StringComparison.Ordinal) >= 0) return MapTraffic.Ground.Cycle;
            // (and the topography's parking strips and bays beside the road: a lane over them ran through the parked cars)
            if (name.StartsWith("BGT pavements") || name.StartsWith("Pavements") || name.StartsWith("Kerbs") || name.StartsWith("BGT parking")) g = MapTraffic.Ground.Pavement;
            // (the Dutch topography's verges and medians, at their exact outlines, never under a carriageway; a canal's quay
            // and water)
            else if ((name.StartsWith("BGT green") || name.StartsWith("Quay") || name.StartsWith("Water")) && g != MapTraffic.Ground.Pavement) g = MapTraffic.Ground.Green;
            else if (name.StartsWith("Roads") || name.StartsWith("BGT roads") || name.StartsWith("Zebra") || name.StartsWith("Road ")) road = true;
            // (the map's own ground, grass, seen where no drawn surface covers it: on a map with the national topography,
            // where OpenStreetMap's invisible road runs over a verge or median the topography has as unpaved, or beyond the
            // topography's reach; not moved off, but a route over it is not the autopilot's first choice; on other maps
            // the ground beside a narrow road)
            else if (name.StartsWith("Ground")) bare = true;
        }
        bool topography = OsmMap.Current != null && OsmMap.Current.Generator != null && OsmMap.Current.Generator.TopographyGround;
        return g == MapTraffic.Ground.Pavement || g == MapTraffic.Ground.Green ? g : road ? MapTraffic.Ground.Road : bare && topography ? MapTraffic.Ground.Bare : g;
        // ground one sees: not a vehicle, not an invisible driving surface, not a wall or pole
        static bool Seen(RaycastHit h) => h.collider.attachedRigidbody == null && h.normal.y >= 0.7f
            && !(h.collider.TryGetComponent<MeshRenderer>(out var mr) && !mr.enabled);
    }
    static readonly RaycastHit[] _groundHits = new RaycastHit[16];
    static readonly Collider[] _near = new Collider[16];
    /// <summary>Lane points moved away from parked cars; out of a bend's inside corner.</summary>
    static int ParkedMoves, CornerMoves;

    // the lanes' points before the moves, in 4 m cells, with their directions: the oncoming lane beside a lane
    static Dictionary<long, List<(Vector3 p, Vector3 dir, Vector3 next)>> _oncoming;
    static void BuildOncomingIndex(List<List<Vector3>> lanes)
    {
        _oncoming = new Dictionary<long, List<(Vector3, Vector3, Vector3)>>();
        foreach (var l in lanes)
            for (int i = 0; i + 1 < l.Count; i++)
            {
                var d = l[i + 1] - l[i]; d.y = 0f; if (d.sqrMagnitude < 1e-6f) continue;
                var k = ((long)Mathf.FloorToInt(l[i].x / 4f) << 32) ^ (uint)Mathf.FloorToInt(l[i].z / 4f);
                if (!_oncoming.TryGetValue(k, out var c)) _oncoming[k] = c = new List<(Vector3, Vector3, Vector3)>();
                c.Add((l[i], d.normalized, l[i + 1]));
            }
    }

    /// <summary>How far a lane point at p (heading dir) is from the nearest oncoming lane's line (up to 4 m; more:
    /// infinity). A lane moved clear of parked cars comes no nearer than 2.3 m to the oncoming one (nor nearer than it
    /// was), so two vehicles of 2 m and more (a pickup, a tow truck) still pass each other: moved towards the road's middle,
    /// one came within 1.8 m of it, and they met head-on.</summary>
    static float OncomingDist(Vector3 p, Vector3 dir)
    {
        if (_oncoming == null) return float.PositiveInfinity;
        var f = new Vector3(dir.x, 0f, dir.z).normalized;
        float best = float.PositiveInfinity;
        int cx = Mathf.FloorToInt(p.x / 4f), cz = Mathf.FloorToInt(p.z / 4f);
        for (int x = cx - 1; x <= cx + 1; x++)
            for (int z = cz - 1; z <= cz + 1; z++)
            {
                if (!_oncoming.TryGetValue(((long)x << 32) ^ (uint)z, out var c)) continue;
                foreach (var (q, qd, qn) in c)
                {
                    if (Vector3.Dot(qd, f) >= -0.7f || Mathf.Abs(q.y - p.y) >= 3f) continue;
                    // (to the segment to its next point: the points are 2 m apart)
                    float ax = qn.x - q.x, az = qn.z - q.z, l2 = ax * ax + az * az;
                    float t = l2 > 1e-6f ? Mathf.Clamp01(((p.x - q.x) * ax + (p.z - q.z) * az) / l2) : 0f;
                    float dx = p.x - (q.x + ax * t), dz = p.z - (q.z + az * t);
                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
                }
            }
        return best;
    }

    /// <summary>How far apart (m) the two lanes of a two-way street are put, line to line, where the carriageway has room:
    /// two cars of 2 m pass each other with 0.6 m between them.</summary>
    const float OncomingApart = 2.6f;
    static float SpreadMax;

    /// <summary>The nearest oncoming lane's line from a lane point (as <see cref="OncomingDist"/>): its distance, and the
    /// point on it.</summary>
    static float OncomingNearest(Vector3 p, Vector3 dir, out Vector3 at)
    {
        at = p;
        if (_oncoming == null) return float.PositiveInfinity;
        var f = new Vector3(dir.x, 0f, dir.z).normalized;
        float best = float.PositiveInfinity;
        int cx = Mathf.FloorToInt(p.x / 4f), cz = Mathf.FloorToInt(p.z / 4f);
        for (int x = cx - 1; x <= cx + 1; x++)
            for (int z = cz - 1; z <= cz + 1; z++)
            {
                if (!_oncoming.TryGetValue(((long)x << 32) ^ (uint)z, out var c)) continue;
                foreach (var (q, qd, qn) in c)
                {
                    if (Vector3.Dot(qd, f) >= -0.7f || Mathf.Abs(q.y - p.y) >= 3f) continue;
                    float ax = qn.x - q.x, az = qn.z - q.z, l2 = ax * ax + az * az;
                    float t = l2 > 1e-6f ? Mathf.Clamp01(((p.x - q.x) * ax + (p.z - q.z) * az) / l2) : 0f;
                    var on = new Vector3(q.x + ax * t, q.y, q.z + az * t);
                    float dx = p.x - on.x, dz = p.z - on.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < best) { best = d; at = on; }
                }
            }
        return best;
    }

    /// <summary>How far (m, up to 2 m in steps of 0.25 m) a car on a lane at p could move towards <paramref name="s"/> and
    /// stay on the drawn carriageway, its side (1.1 m out) still over it and clear of parked cars.</summary>
    static float RoomBeside(Vector3 p, Vector3 s, Vector3 dir)
    {
        float room = -1f;
        for (float t = 0f; t <= 2.001f; t += 0.25f)
        {
            var c = p + s * t;
            if (SeenGround(c) != Ground.Road || SeenGround(c + s * 1.1f) != Ground.Road || ParkedCarNear(c, dir)) break;
            room = t;
        }
        return Mathf.Max(0f, room);
    }

    /// <summary>
    /// The lanes of a two-way street nearer to each other than <see cref="OncomingApart"/> (line to line) are moved apart
    /// where the drawn carriageway has room beside them: each point moves towards its kerb by its share of what is
    /// missing (half, or more where the oncoming lane has less room: all of it where that one runs along its kerb), at
    /// most by the room beside it. Both lanes are measured against the lanes before this pass, so their moves agree. The
    /// moves are smoothed (a median over 24 m, then an average over 12 m), so a junction's mouth or a gap between parked
    /// cars does not bend the lane. Returns the points moved.
    /// </summary>
    static int SpreadFromOncoming(List<Vector3> lane, bool lht)
    {
        if (lane.Count < 2) return 0;
        var move = new float[lane.Count];
        var side = new Vector3[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            var d = lane[Mathf.Min(i + 1, lane.Count - 1)] - lane[Mathf.Max(i - 1, 0)]; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) continue;
            d.Normalize();
            side[i] = Vector3.Cross(Vector3.up, d) * (lht ? -1f : 1f);   // (towards the kerb)
            float dist = OncomingNearest(lane[i], d, out var on);
            if (dist >= OncomingApart) continue;
            // (the oncoming lane on the side of the road's middle, not on the kerb's: a lane of another street)
            if (Vector3.Dot(Flat(lane[i] - on), side[i]) < dist * 0.7f) continue;
            float need = OncomingApart - dist;
            float mine = RoomBeside(lane[i], side[i], d);
            if (mine <= 0f) continue;
            float theirs = RoomBeside(on, -side[i], -d);
            move[i] = Mathf.Min(mine, Mathf.Max(need / 2f, need - theirs));
        }
        int mh = Mathf.Max(1, Mathf.RoundToInt(12f / CoupledSim.Maps.MapRoutes.LaneSpacing)), ah = Mathf.Max(1, Mathf.RoundToInt(6f / CoupledSim.Maps.MapRoutes.LaneSpacing));
        var median = new float[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            var w = new List<float>();
            for (int k = Mathf.Max(0, i - mh); k <= Mathf.Min(lane.Count - 1, i + mh); k++) w.Add(move[k]);
            w.Sort(); median[i] = w[w.Count / 2];
        }
        int moved = 0;
        var before = new List<Vector3>(lane);
        for (int i = 0; i < lane.Count; i++)
        {
            float sum = 0f; int n = 0;
            for (int k = Mathf.Max(0, i - ah); k <= Mathf.Min(lane.Count - 1, i + ah); k++) { sum += median[k]; n++; }
            float m = sum / n;
            if (m < 0.05f || side[i] == Vector3.zero) continue;
            // (no further than the room beside this point: the smoothing carries a move on past where it was wanted)
            var dir = Vector3.Cross(side[i], Vector3.up) * (lht ? -1f : 1f);
            m = Mathf.Min(m, RoomBeside(before[i], side[i], dir));
            if (m < 0.05f) continue;
            if (DebugFocus != null && Flat(before[i] - DebugFocus.Value).sqrMagnitude < 12f * 12f)
                Debug.Log($"TrafficDebug: lane point {i}/{lane.Count} at {before[i]}: {m:F2} m away from the oncoming lane (wanted {move[i]:F2})");
            lane[i] = before[i] + side[i] * m;
            SpreadMax = Mathf.Max(SpreadMax, m);
            moved++;
        }
        return moved;
    }

    /// <summary>Whether a vehicle placed already (its body or an articulated vehicle's rear section, which reaches 8 m
    /// behind its pivot) stands within <paramref name="halfLength"/> along and <paramref name="halfWidth"/> across of p:
    /// two cyclists were placed inside an articulated bus's rear section in Ghent and stood against it for good.</summary>
    static bool VehicleAt(Vector3 p, Vector3 dir, float halfLength, float halfWidth)
    {
        dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        Physics.SyncTransforms();
        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 1.0f, new Vector3(halfWidth, 0.8f, halfLength), _near, Quaternion.LookRotation(dir.normalized, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (_near[i].attachedRigidbody != null || _near[i].name.StartsWith("Rear section", StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Whether something fixed (not the ground, a kerb or a road's surface; not a vehicle) stands within
    /// <paramref name="clearance"/> of a car (5.2 by 1.8 m) at p heading along f, from 0.4 to 1.6 m above the road.</summary>
    static bool FixedBeside(Vector3 p, Vector3 f, float clearance) => FixedBeside(p, f, clearance, out _);
    static bool FixedBeside(Vector3 p, Vector3 f, float clearance, out string what)
    {
        what = null;
        if (f.sqrMagnitude < 1e-6f) return false;
        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 1.0f, new Vector3(0.9f + clearance, 0.6f, 2.6f), _near, Quaternion.LookRotation(f, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = _near[i];
            if (c.attachedRigidbody != null) continue;
            var nm = c.name;
            if (nm.StartsWith("Roads") || nm.StartsWith("Road ") || nm.StartsWith("BGT") || nm.StartsWith("Ground") || nm.StartsWith("Terrain") || nm.StartsWith("Zebra") || nm.StartsWith("Kerbs") || nm.StartsWith("Pavements") || nm.StartsWith("Cycle") || nm.StartsWith("Water")) continue;
            if (c.bounds.max.y < p.y + 0.4f) continue;   // (a surface under it)
            what = (c.transform.parent != null ? c.transform.parent.name + "/" : "") + nm;
            return true;
        }
        return false;
    }
    static int _unclearedLogged;

    /// <summary>Whether a rider's bicycle (1.8 by 0.7 m, 0.2 m to spare) placed at p heading along f stands clear of
    /// anything fixed from 0.3 to 1.6 m above the ground (a hedge, a post, a wall; not the ground, a kerb or a vehicle).</summary>
    static bool RiderClear(Vector3 p, Vector3 f)
    {
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f) return true;
        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 0.95f, new Vector3(0.55f, 0.65f, 1.1f), _near, Quaternion.LookRotation(f, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = _near[i];
            if (c.attachedRigidbody != null) continue;
            var nm = c.name;
            if (nm.StartsWith("Roads") || nm.StartsWith("Road ") || nm.StartsWith("BGT") || nm.StartsWith("Ground") || nm.StartsWith("Terrain") || nm.StartsWith("Zebra") || nm.StartsWith("Kerbs") || nm.StartsWith("Pavements") || nm.StartsWith("Cycle") || nm.StartsWith("Water")) continue;
            if (c.bounds.max.y < p.y + 0.3f) continue;
            return false;
        }
        return true;
    }

    /// <summary>
    /// A car lane clear of what stands beside the road (trees, lamp and sign posts, building fronts, walls, parked cars):
    /// where a car on it would come within 0.4 m of one, the point moves by the least that clears it (up to 2 m either
    /// way, on the carriageway, no nearer the oncoming lane than 2.3 m, nor nearer than it was); each move holds the two
    /// points either side (4 m) and the moves are averaged over 3 points, so the lane eases past. Points that cannot be
    /// cleared (a gap narrower than the car) are counted in <paramref name="left"/>. Returns the points moved.
    /// </summary>
    static int KeepLaneClear(List<Vector3> lane, ref int left)
    {
        // (0.4 m beside a 1.8 m car: 0.3 m beside a 2 m SUV or van)
        const float Clearance = 0.4f;
        if (lane.Count < 2) return 0;
        var shift = new float[lane.Count];
        var side = new Vector3[lane.Count];
        int found = 0;
        for (int i = 0; i < lane.Count; i++)
        {
            var f = lane[Mathf.Min(i + 1, lane.Count - 1)] - lane[Mathf.Max(i - 1, 0)]; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) continue;
            f.Normalize();
            side[i] = Vector3.Cross(Vector3.up, f);
            if (!FixedBeside(lane[i], f, Clearance, out var what)) continue;
            float before = OncomingDist(lane[i], f);
            for (int k = 1; k <= 8 && shift[i] == 0f; k++)
                foreach (float sgn in new[] { -1f, 1f })
                {
                    var q = lane[i] + side[i] * (sgn * k * 0.25f);
                    if (shift[i] == 0f && SeenGround(q) == Ground.Road && OncomingDist(q, f) >= Mathf.Min(2.3f, before) && !FixedBeside(q, f, Clearance))
                        shift[i] = sgn * k * 0.25f;
                }
            // (a street too narrow for that, house fronts on one side and the oncoming lane on the other: towards the
            // middle, down to 1 m from the oncoming lane; the cars then take turns there, TrafficCar.NarrowWait, rather
            // than one standing against a building's front for good)
            for (int k = 1; k <= 8 && shift[i] == 0f; k++)
                foreach (float sgn in new[] { -1f, 1f })
                {
                    var q = lane[i] + side[i] * (sgn * k * 0.25f);
                    if (shift[i] == 0f && SeenGround(q) == Ground.Road && OncomingDist(q, f) >= Mathf.Min(1.0f, before) && !FixedBeside(q, f, 0.25f))
                        shift[i] = sgn * k * 0.25f;
                }
            if (shift[i] != 0f) found++;
            else
            {
                left++;
                if (DebugFocus != null && _unclearedLogged++ < 25)
                {
                    // (what is in the way, and on which sides: 0.1 m clear to the left and right of the car)
                    bool l = FixedBeside(lane[i] - side[i] * 0.5f, f, 0.1f), r = FixedBeside(lane[i] + side[i] * 0.5f, f, 0.1f);
                    Debug.Log($"TrafficDebug: lane point at ({lane[i].x:F1},{lane[i].z:F1}) not clear of {what} (ground {SeenGround(lane[i])}, oncoming lane {before:F1} m away; 0.5 m left {(l ? "blocked" : "clear")}, right {(r ? "blocked" : "clear")})");
                }
            }
        }
        if (found == 0) return 0;
        var held = new float[lane.Count];
        for (int i = 0; i < lane.Count; i++)
            for (int k = Mathf.Max(0, i - 2); k <= Mathf.Min(lane.Count - 1, i + 2); k++)
                if (Mathf.Abs(shift[k]) > Mathf.Abs(held[i])) held[i] = shift[k];
        int moved = 0;
        for (int i = 0; i < lane.Count; i++)
        {
            float sum = 0f; int n = 0;
            for (int k = Mathf.Max(0, i - 1); k <= Mathf.Min(lane.Count - 1, i + 1); k++) { sum += held[k]; n++; }
            if (Mathf.Abs(sum / n) < 0.01f) continue;
            if (DebugFocus != null && Flat(lane[i] - DebugFocus.Value).sqrMagnitude < 12f * 12f)
                Debug.Log($"TrafficDebug: lane point {i}/{lane.Count} at {lane[i]}: {sum / n:F2} m clear of something fixed");
            lane[i] += side[i] * (sum / n);
            moved++;
        }
        return moved;
    }

    // the map's road junctions (points shared by three or more road ends or ways), in 10 m cells: where a turn is a turn
    // off the road, which drivers indicate, and not the road's own bend (TrafficCar's indicators)
    static Dictionary<long, List<Vector3>> _junctions;
    static void BuildJunctions(IList<OsmMapGenerator.Road> roads)
    {
        var degree = new Dictionary<long, (Vector3 p, int n)>();
        long Key(Vector3 q) => ((long)Mathf.RoundToInt(q.x * 2f) << 32) ^ (uint)Mathf.RoundToInt(q.z * 2f);
        foreach (var r in roads)
        {
            if (r.Line == null || r.Line.Count < 2) continue;
            for (int i = 0; i < r.Line.Count; i++)
            {
                var k = Key(r.Line[i]);
                int add = i == 0 || i == r.Line.Count - 1 ? 1 : 2;
                degree[k] = degree.TryGetValue(k, out var d) ? (d.p, d.n + add) : (r.Line[i], add);
            }
        }
        _junctions = new Dictionary<long, List<Vector3>>();
        foreach (var (p, n) in degree.Values)
        {
            if (n < 3) continue;
            var c = ((long)Mathf.FloorToInt(p.x / 10f) << 32) ^ (uint)Mathf.FloorToInt(p.z / 10f);
            if (!_junctions.TryGetValue(c, out var l)) _junctions[c] = l = new List<Vector3>();
            l.Add(p);
        }
    }

    /// <summary>Whether a road junction lies within <paramref name="r"/> m of p (null: the map's junctions are not known,
    /// as in a scene without a map).</summary>
    public static bool? JunctionNear(Vector3 p, float r)
    {
        if (_junctions == null) return null;
        int cx = Mathf.FloorToInt(p.x / 10f), cz = Mathf.FloorToInt(p.z / 10f), span = Mathf.CeilToInt(r / 10f);
        for (int x = cx - span; x <= cx + span; x++)
            for (int z = cz - span; z <= cz + span; z++)
                if (_junctions.TryGetValue(((long)x << 32) ^ (uint)z, out var l))
                    foreach (var q in l) { float dx = q.x - p.x, dz = q.z - p.z; if (dx * dx + dz * dz <= r * r) return true; }
        return false;
    }

    /// <summary>Whether a car (4.6 m by 1.8 m) at p heading along dir would come within 0.25 m of a parked car.</summary>
    static bool ParkedCarNear(Vector3 p, Vector3 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) return false;
        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 0.9f, new Vector3(0.9f + 0.25f, 0.6f, 2.3f + 0.25f), _near, Quaternion.LookRotation(dir, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (_near[i].attachedRigidbody != null) continue;
            for (var t = _near[i].transform; t != null; t = t.parent) if (t.name == "Parked car") return true;
        }
        return false;
    }
    static readonly List<(Vector3 at, float shift)> _moves = new List<(Vector3, float)>();
    /// <summary>-debugTraffic: the lane points within 30 m of here (the participant) log their moves.</summary>
    static Vector3? DebugFocus;

    public enum Ground { None, Road, Cycle, Paving, Pavement, Green, Bare }
    /// <summary>The ground one sees at a point (a cyclist's room to overtake: a cycle path or cycle lane).</summary>
    public static Ground SeenGroundAt(Vector3 p) => SeenGround(p);

    static Ground GroundAt(Vector3 p)
    {
        // (a sphere 0.7 m across, not a thin ray: a lamp or sign post standing in a cycle path is found, and ends it there)
        if (!Physics.SphereCast(p + Vector3.up * 4f, 0.35f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return Ground.None;
        if (hit.collider.attachedRigidbody != null) return Ground.None;   // a vehicle: unknown ground
        var n = hit.collider.name;
        if (n.IndexOf("ycle", StringComparison.Ordinal) >= 0) return Ground.Cycle;
        if (n.StartsWith("Roads") || n.StartsWith("BGT roads")) return Ground.Road;   // ("BGT parking" is paving: cars park there)
        return Ground.Paving;
    }

    /// <summary>
    /// Where a rider rides along a car route, every <paramref name="step"/> m: across the road towards its kerb (to the right,
    /// to the left in left-hand traffic) the ground is read every 0.5 m. A cycle path or cycle track within a few metres of
    /// the carriageway (up to 14 m beyond its edge) is taken (its kerb-side half when it is wide enough for two directions) when
    /// <paramref name="preferCycle"/>; otherwise the carriageway 0.8 m from its edge (in a painted cycle lane, if the road
    /// has one), never the pavement, 1 m clear of parked cars. The offsets are smoothed (a median over 7 points, then an average over 3), so short
    /// gaps (junctions, driveways, parked vehicles) do not make the line jump.
    /// </summary>
    public static bool DebugRide = Array.IndexOf(CommandLine.Args, "-debugRide") >= 0;

    /// <param name="cycleAboveKmh">With <paramref name="preferCycle"/> off: still the cycle path where the road's speed limit is
    /// over this (a race cyclist: on the road up to 50 km/h, the cycle path beside faster roads).</param>
    /// <param name="edge">How far from the carriageway's edge (m) a rider on it rides (a race cyclist a little further out).</param>
    static List<Vector3> RideLine(WaypointCircuit route, float from, float to, float step, bool lht, bool preferCycle, float cycleAboveKmh = float.MaxValue, float edge = 0.8f)
    {
        Physics.SyncTransforms();
        var pts = new List<(Vector3 p, Vector3 side)>();
        for (float d = from; d < to; d += step)
        {
            var rp = route.GetRoutePoint(d);
            var side = Vector3.Cross(Vector3.up, new Vector3(rp.direction.x, 0f, rp.direction.z)).normalized * (lht ? -1f : 1f);
            pts.Add((rp.position, side));
        }
        var off = new float[pts.Count];
        var limit = new float[pts.Count];   // on the carriageway: no closer than 1 m to what ends it (a parked car, a kerb)
        for (int i = 0; i < pts.Count; i++)
        {
            var (p, side) = pts[i];
            // the ground across the road, every 0.5 m from 3 m towards the road's centre to 6 m towards the kerb; the stretch
            // of open carriageway nearest the lane's centre (a parked car, a kerb or paving ends a stretch)
            const int inner = 6, outer = 12;
            var g = new Ground[inner + outer + 1];
            for (int k = -inner; k <= outer; k++) g[k + inner] = GroundAt(p + side * (k * 0.5f));
            int bestA = 0, bestB = -1, bestDist = int.MaxValue;
            for (int k = 0; k < g.Length; )
            {
                if (g[k] != Ground.Road && g[k] != Ground.None) { k++; continue; }
                int a = k; while (k < g.Length && (g[k] == Ground.Road || g[k] == Ground.None)) k++;
                int b2 = k - 1, dist = a > inner ? a - inner : b2 < inner ? inner - b2 : 0;
                if (b2 - a >= 2 && dist < bestDist) { bestDist = dist; bestA = a; bestB = b2; }
            }
            float roadEdge = bestB >= 0 ? (bestB - inner) * 0.5f : 0f, roadStart = bestB >= 0 ? (bestA - inner) * 0.5f : 0f;
            float pick = bestB >= 0 ? Mathf.Max(roadStart + 0.5f, Mathf.Min(roadEdge - edge, 4f)) : 0.5f;
            bool farSide = false;
            if (preferCycle || (cycleAboveKmh < float.MaxValue && RoadSpeedLimits.At(p) > cycleAboveKmh))
            {
                // a cycle path starting within 14 m of the carriageway's edge, at least 1 m wide (6 m missed the Dutch
                // two-way paths beyond a verge and a row of trees, and 10 m those beyond a parking strip too: the cyclists
                // rode on the road beside them)
                float start = -1f, end = -1f;
                for (int k = Mathf.Max(1, Mathf.RoundToInt(roadEdge / 0.5f)); k <= Mathf.RoundToInt((Mathf.Min(roadEdge, 6f) + 14f) / 0.5f); k++)
                {
                    bool cyc = GroundAt(p + side * (k * 0.5f)) == Ground.Cycle;
                    if (cyc && start < 0f) start = k * 0.5f;
                    if (cyc) end = k * 0.5f; else if (start >= 0f) break;
                }
                if (start >= 0f && end - start >= 0.9f) pick = end - start >= 2.4f ? start + 0.7f * (end - start) : (start + end) / 2f;
                else
                {
                    // none on this side: a two-way cycle path (2.4 m and wider) on the far side, within 14 m beyond the
                    // carriageway's far edge; the rider keeps to its right, the half towards the road
                    int k0 = Mathf.RoundToInt(roadStart / 0.5f) - 1;
                    while (k0 > -60 && GroundAt(p + side * (k0 * 0.5f)) is var gk && (gk == Ground.Road || gk == Ground.None)) k0--;
                    float fs = 1f, fe = 1f;
                    for (int k = k0; k >= k0 - 28; k--)
                    {
                        bool cyc = GroundAt(p + side * (k * 0.5f)) == Ground.Cycle;
                        if (cyc && fs > 0f) fs = k * 0.5f;
                        if (cyc) fe = k * 0.5f; else if (fs <= 0f) break;
                    }
                    if (fs <= 0f && fs - fe >= 2.4f) { pick = fs - 0.3f * (fs - fe); farSide = true; }
                }
            }
            limit[i] = farSide || pick > roadEdge + 0.4f ? float.MaxValue : roadEdge - 1.0f;   // (a cycle path beside the road: no limit)
            off[i] = pick;
            if (DebugRide && i % 10 == 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k <= 24; k++) { var h = Physics.Raycast(p + side * (k * 0.5f) + Vector3.up * 4f, Vector3.down, out var hh, 10f, ~0, QueryTriggerInteraction.Ignore); sb.Append(h ? hh.collider.name.Substring(0, Math.Min(6, hh.collider.name.Length)) : "-").Append('|'); }
                Debug.Log($"RideLine {i}: carriageway {roadStart}..{roadEdge}, pick {pick}: {sb}");
            }
        }
        // smoothing: median over 7 points, the clearance kept, then an average over 3
        var med = new float[off.Length];
        for (int i = 0; i < off.Length; i++)
        {
            var w = new List<float>();
            for (int k = Math.Max(0, i - 3); k <= Math.Min(off.Length - 1, i + 3); k++) w.Add(off[k]);
            w.Sort(); med[i] = w[w.Count / 2];
        }
        // keep the clearance of the points around (the median would let the line brush past the parked cars between gaps)
        for (int i = 0; i < med.Length; i++)
        {
            if (limit[i] == float.MaxValue) continue;   // (on a cycle path)
            float lim = float.MaxValue;
            for (int k = Math.Max(0, i - 3); k <= Math.Min(med.Length - 1, i + 3); k++) lim = Math.Min(lim, limit[k]);
            if (lim != float.MaxValue) med[i] = Math.Min(med[i], Math.Max(lim, -2.5f));
        }
        var line = new List<Vector3>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
        {
            float sum = 0f; int n = 0;
            for (int k = Math.Max(0, i - 1); k <= Math.Min(med.Length - 1, i + 1); k++) { sum += med[k]; n++; }
            line.Add(pts[i].p + pts[i].side * (sum / n));
        }
        return line;
    }

    /// <summary>
    /// The participant's riding line (the autopilot of a rider) clear of what stands in its way: the line is read across the road only every few metres, so a
    /// sign post, a lamp post or a bollard between two readings (one at the end of a zebra crossing, at a junction's
    /// corner) and a pavement corner the smoothed line cuts were ridden through. Every half metre along the line, a rider
    /// that would come within 1 m of a sign post or 0.6 m of anything else fixed (from 0.4 to 1.6 m above the ground; not the
    /// ground, not a vehicle), or ride on the pavement or grass, is moved across by the least that clears it, at most 2 m
    /// (towards the road's middle first); each move holds two points before and after (6-8 m), and they are averaged over 3
    /// points, so the line keeps the whole move past the obstacle and eases out and back.
    /// </summary>
    static void KeepRideClear(List<Vector3> line, bool lht)
    {
        // (a handlebar is 0.3 m to either side of the rider's middle, and the rider follows its line within some 0.3-0.4 m:
        // a post a metre from the line, other things 0.6 m)
        // (and a metre from a building's wall: cyclists keep off house fronts, where doors open and people step out; in
        // Ghent the rider rode half a metre from the façades)
        const float Clearance = 0.6f, PostClearance = 1f, WallClearance = 1f;
        if (line.Count < 2) return;
        // (towards the kerb: to the right, to the left in left-hand traffic)
        var side = new List<Vector3>(line.Count);
        for (int i = 0; i < line.Count; i++)
        {
            var d = line[Math.Min(i + 1, line.Count - 1)] - line[Math.Max(i - 1, 0)]; d.y = 0f;
            side.Add(d.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, d.normalized) * (lht ? -1f : 1f) : Vector3.zero);
        }
        // (traffic signs have no colliders: their posts, where they stand)
        if (_signPosts == null) _signPosts = FindObjectsByType<TrafficSign>(FindObjectsSortMode.None).Select(t => t.transform.position).ToList();
        bool Blocked(Vector3 p)
        {
            var g = SeenGround(p);
            if (g == Ground.Pavement || g == Ground.Green) return true;
            foreach (var sp in _signPosts) { float dx = sp.x - p.x, dz = sp.z - p.z; if (dx * dx + dz * dz < PostClearance * PostClearance && Mathf.Abs(sp.y - p.y) < 2f) return true; }
            int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.4f, p + Vector3.up * 1.6f, Clearance, _near, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _near[i];
                if (c.attachedRigidbody != null) continue;
                var nm = c.name;
                if (nm.StartsWith("Roads") || nm.StartsWith("Road ") || nm.StartsWith("BGT") || nm.StartsWith("Ground") || nm.StartsWith("Terrain") || nm.StartsWith("Zebra") || nm.StartsWith("Kerbs") || nm.StartsWith("Pavements") || nm.StartsWith("Cycle")) continue;
                return true;
            }
            n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.4f, p + Vector3.up * 1.6f, WallClearance, _near, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (_near[i].attachedRigidbody == null && _near[i].name.StartsWith("Building", StringComparison.Ordinal)) return true;
            return false;
        }
        var shift = new float[line.Count];
        int moved = 0;
        if (DebugFocus != null)
            for (int i = 0; i < line.Count; i++)
                if (Flat(line[i] - DebugFocus.Value).sqrMagnitude < 12f * 12f)
                    Debug.Log($"TrafficDebug: riding line point {i}/{line.Count} at {line[i]}: ground {SeenGround(line[i])}, blocked {Blocked(line[i])}");
        for (int i = 0; i < line.Count; i++)
        {
            // (this point, and halfway to the next one)
            var a = line[i]; var b = line[Math.Min(i + 1, line.Count - 1)];
            int steps = Math.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.5f));
            bool blocked = false;
            for (int k = 0; k < steps && !blocked; k++) blocked = Blocked(Vector3.Lerp(a, b, k / (float)steps));
            if (!blocked) continue;
            for (int k = 1; k <= 8 && shift[i] == 0f; k++)
                foreach (float sgn in new[] { -1f, 1f })   // (-: away from the kerb)
                {
                    float off = sgn * k * 0.25f;
                    bool clear = true;
                    for (int j = 0; j < steps && clear; j++) clear = !Blocked(Vector3.Lerp(a, b, j / (float)steps) + side[i] * off);
                    if (clear && shift[i] == 0f) shift[i] = off;
                }
            if (shift[i] != 0f) moved++;
        }
        if (moved == 0) return;
        var held = new float[line.Count];
        for (int i = 0; i < line.Count; i++)
            for (int k = Math.Max(0, i - 2); k <= Math.Min(line.Count - 1, i + 2); k++)
                if (Math.Abs(shift[k]) > Math.Abs(held[i])) held[i] = shift[k];
        for (int i = 0; i < line.Count; i++)
        {
            float sum = 0f; int n = 0;
            for (int k = Math.Max(0, i - 1); k <= Math.Min(line.Count - 1, i + 1); k++) { sum += held[k]; n++; }
            line[i] += side[i] * (sum / n);
        }
        RideMoves += moved;
    }
    /// <summary>Riding-line points moved clear of posts, pavement corners and the like.</summary>
    public static int RideMoves;
    static List<Vector3> _signPosts;

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static List<Vector3> SmoothMoved(List<Vector3> lane) =>
        CoupledSim.Maps.MapRoutes.Smooth(lane.Select(q => new System.Numerics.Vector3(q.x, q.y, q.z)).ToList(), 2.5f).Select(q => new Vector3(q.X, q.Y, q.Z)).ToList();

    // the car and cycle routes' points every 2 m, in 4 m cells: which lanes cross a car's path (TrafficCar.KeepLanesClear)
    Dictionary<long, List<(Vector3 p, Vector3 dir, WaypointCircuit route, bool cycle)>> _laneCells;
    /// <summary>The cycle routes that have cyclists.</summary>
    public readonly List<WaypointCircuit> CycleRoutes = new List<WaypointCircuit>();

    /// <summary>Whether a car lane (another route) or a cycle route that crosses the direction <paramref name="dir"/> (at
    /// more than 45°) runs where a car's body (<paramref name="halfWidth"/> to either side of p) would overlap it: within
    /// 1.2 m of a car lane's line, 0.4 m of a cyclist's.</summary>
    public bool CrossingLaneNear(Vector3 p, Vector3 dir, float halfWidth, WaypointCircuit mine)
    {
        if (_laneCells == null)
        {
            _laneCells = new Dictionary<long, List<(Vector3, Vector3, WaypointCircuit, bool)>>();
            foreach (var (r, cycle) in Routes.Select(r => (r, false)).Concat(CycleRoutes.Where(r => r != null).Select(r => (r, true))))
                for (float d = 0f; d < r.OpenLength; d += 2f)
                {
                    var rp = r.GetRoutePoint(d);
                    var k = ((long)Mathf.FloorToInt(rp.position.x / 4f) << 32) ^ (uint)Mathf.FloorToInt(rp.position.z / 4f);
                    if (!_laneCells.TryGetValue(k, out var l)) _laneCells[k] = l = new List<(Vector3, Vector3, WaypointCircuit, bool)>();
                    l.Add((rp.position, rp.direction, r, cycle));
                }
        }
        var f = Flat(dir).normalized;
        float radius = halfWidth + 1.2f;
        int cx = Mathf.FloorToInt(p.x / 4f), cz = Mathf.FloorToInt(p.z / 4f), reach = Mathf.CeilToInt(radius / 4f);
        for (int x = cx - reach; x <= cx + reach; x++)
            for (int z = cz - reach; z <= cz + reach; z++)
            {
                if (!_laneCells.TryGetValue(((long)x << 32) ^ (uint)z, out var l)) continue;
                foreach (var (q, qd, route, cycle) in l)
                {
                    if (route == mine || Mathf.Abs(q.y - p.y) > 3f) continue;
                    if (Mathf.Abs(Vector3.Dot(Flat(qd).normalized, f)) > 0.7f) continue;   // (along the same road)
                    float r = cycle ? halfWidth + 0.4f : radius;
                    float dx = q.x - p.x, dz = q.z - p.z;
                    if (dx * dx + dz * dz < r * r) return true;
                }
            }
        return false;
    }

    static float Normal(System.Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}

/// <summary>A map-traffic car at the end of its route (at the map's edge) starts again at its beginning, when that
/// is clear of other cars.</summary>
public class MapRouteRecycler : MonoBehaviour
{
    /// <summary>False: the vehicle stays at its route's end (the participant's autopilot).</summary>
    public bool Recycle = true;
    WaypointProgressTracker _tracker;
    Rigidbody _rb;
    float _nextCheck;
    static readonly List<(Vector3 pos, float time)> _recent = new List<(Vector3, float)>();

    void Start() { _tracker = GetComponent<WaypointProgressTracker>(); _rb = GetComponent<Rigidbody>(); }

    /// <summary>
    /// A cyclist: when it has ridden over FarMetres from every participant, away from them, it is moved back along its own
    /// route to where it is 150-220 m from the nearest participant and riding towards them (out of sight in a town, clear of
    /// others), so a busy street stays busy: placed nearest the participants, the cyclists all rode out of the stretch
    /// round the participant in under a minute, and nothing came after them (at rush hour on De Zaale in Eindhoven).
    /// </summary>
    public bool KeepNear;
    public const float FarMetres = 260f;
    /// <summary>Cyclists moved back near the participants so far (-debugTraffic's summary).</summary>
    public static int KeptNear;
    float _nextKeep = -1f;
    static readonly List<Transform> _participants = new List<Transform>();
    static float _participantsAt = -10f;

    static float NearestParticipant(Vector3 q, out Transform who)
    {
        if (Time.time - _participantsAt > 5f)
        {
            _participantsAt = Time.time;
            _participants.Clear();
            foreach (var a in FindObjectsByType<PlayerAvatar>())
                if (!a.name.StartsWith("Map ") && a.GetComponent<AIPedestrian>() == null) _participants.Add(a.transform);
        }
        float b = float.MaxValue; who = null;
        foreach (var t in _participants)
        {
            if (t == null) continue;
            var r = t.position - q; r.y = 0f;
            float d = r.magnitude;
            if (d < b) { b = d; who = t; }
        }
        return b;
    }

    void KeepNearParticipants(WaypointCircuit c)
    {
        var p = transform.position;
        float near = NearestParticipant(p, out var who);
        if (who == null || near < FarMetres) return;
        var v = _rb != null ? _rb.linearVelocity : transform.forward; v.y = 0f;
        var rel = p - who.position; rel.y = 0f;
        if (Vector3.Dot(v, rel) <= 0f) return;   // (riding towards them: on its way)
        // (the places of its route from a random point on, so the cyclists moved do not all come to the same one)
        float span = c.OpenLength - 70f; if (span < 20f) return;
        float offset = UnityEngine.Random.Range(0f, span);
        for (float k = 0f; k < span; k += 10f)
        {
            float d = 4f + Mathf.Repeat(offset + k, span);
            var q = c.GetRoutePoint(d).position;
            float dq = NearestParticipant(q, out _);
            if (dq < 150f || dq > 220f) continue;
            if (NearestParticipant(c.GetRoutePoint(d + 20f).position, out _) >= dq) continue;   // (then towards them)
            var rp = c.GetRoutePoint(d);
            if (!StartClear(rp)) continue;
            if (!Free(q)) continue;
            _recent.RemoveAll(r => Time.time - r.time > 3f);
            if (_recent.Exists(r => (r.pos - q).sqrMagnitude < 8f * 8f)) continue;
            _recent.Add((q, Time.time));
            MoveTo(rp, d);
            KeptNear++;
            return;
        }
    }

    /// <summary>Whether a place is free for a cyclist moved there: no vehicle, rider, person or animal within 12 m, and none
    /// that would get there in the next 5 s (moving towards it, passing within 3 m of it): put in front of a car that came on
    /// at 13 km/h, a cyclist was driven into (London).</summary>
    bool Free(Vector3 q)
    {
        foreach (var other in Physics.OverlapSphere(q, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            var rb = other.attachedRigidbody;
            if (rb == null || rb == _rb) continue;
            var rel = q - rb.position; rel.y = 0f;
            if (rel.sqrMagnitude < 12f * 12f) return false;
            var v = rb.linearVelocity;
            // (a kinematic body, a cyclist of the traffic: its model's speed along its heading)
            if (rb.isKinematic && rb.TryGetComponent<TrafficCar>(out var tc)) v = rb.transform.forward * tc.Speed;
            v.y = 0f;
            float speed = v.magnitude;
            if (speed < 0.5f) continue;
            float along = Vector3.Dot(rel, v / speed);
            if (along <= 0f || along / speed > 5f) continue;
            if ((rel - v / speed * along).magnitude < 3f) return false;
        }
        foreach (var a in AnimalAgent.All) if (a != null && (a.transform.position - q).sqrMagnitude < 12f * 12f) return false;
        return true;
    }

    /// <summary>Vehicles and riders moved on past something fixed so far (-debugTraffic's summary).</summary>
    public static int MovedOn;
    float _nextMoveOn;

    float HeldByFixedFor()
    {
        if (_tc == null) TryGetComponent(out _tc);
        return _tc != null ? _tc.HeldByFixedFor : 0f;
    }

    /// <summary>
    /// Held for over 10 s by something fixed it cannot get past (a building passage narrower than a cyclist's line keeps
    /// clear, a post or a wall corner its route passes too near; a cyclist stood 35 s against a passage's wall in Ho Chi Minh
    /// City), with no participant within 40 m: moved on along its route to the first place 6-40 m ahead where it stands
    /// clear, so it does not stand there for the rest of the session.
    /// </summary>
    void MoveOnPast(WaypointCircuit c, float at)
    {
        if (NearestParticipant(transform.position, out _) < 40f) return;
        for (float s = at + 6f; s <= at + 40f && s < c.OpenLength - 15f; s += 2f)
        {
            var rp = c.GetRoutePoint(s);
            if (NearestParticipant(rp.position, out _) < 40f || !StartClear(rp)) continue;
            bool taken = false;
            foreach (var other in Physics.OverlapSphere(rp.position, 6f, ~0, QueryTriggerInteraction.Ignore))
                if (other.attachedRigidbody != null && other.attachedRigidbody != _rb) { taken = true; break; }
            if (taken) continue;
            MoveTo(rp, s);
            MovedOn++;
            return;
        }
    }

    void MoveTo(WaypointCircuit.RoutePoint start, float progress)
    {
        var p = start.position;
        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.1f;
        var rot = Quaternion.LookRotation(new Vector3(start.direction.x, 0, start.direction.z), Vector3.up);
        float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
        if (_rb != null) { _rb.position = p; _rb.rotation = rot; _rb.linearVelocity = rot * Vector3.forward * speed; }
        transform.SetPositionAndRotation(p, rot);
        _tracker.SetProgress(progress);
    }

    void FixedUpdate()
    {
        if (_tracker == null || _tracker.Circuit == null || Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.5f;
        var c = _tracker.Circuit;
        float d = Mathf.Repeat(_tracker.ProgressDistance, c.Length);
        if (Recycle && Time.time >= _nextMoveOn && HeldByFixedFor() > 10f) { _nextMoveOn = Time.time + 5f; MoveOnPast(c, d); }
        if (KeepNear && Recycle && Time.time >= _nextKeep)
        {
            _nextKeep = Time.time + UnityEngine.Random.Range(2f, 3f);
            KeepNearParticipants(c);
        }
        // at the end of its route (TrafficCar stops there; a circuit's closing piece back to the start, straight across the
        // map, is never driven)
        if (d < c.OpenLength - 15f || !Recycle) return;
        // (a start where its body stands clear of walls, posts and parked cars: a cyclist recycled to its route's start
        // was put into a building's front)
        float from = 4f;
        while (from < 40f && !StartClear(c.GetRoutePoint(from))) from += 3f;
        if (from >= 40f) return;
        var start = c.GetRoutePoint(from);
        foreach (var other in Physics.OverlapSphere(start.position, 12f, ~0, QueryTriggerInteraction.Ignore))
            if (other.attachedRigidbody != null && other.attachedRigidbody != _rb && other.attachedRigidbody.GetComponent<AICar>() != null) return;
        // (cars moved to a start in this physics step are not in the overlap test yet)
        _recent.RemoveAll(r => Time.time - r.time > 3f);
        if (_recent.Exists(r => (r.pos - start.position).sqrMagnitude < 12f * 12f)) return;
        _recent.Add((start.position, Time.time));
        MoveTo(start, from);
    }

    TrafficCar _tc;
    bool StartClear(WaypointCircuit.RoutePoint rp)
    {
        if (_tc == null) TryGetComponent(out _tc);
        bool rider = _tc != null && _tc.IsRider;
        float hl = rider ? 0.9f : (_tc != null ? _tc.VehicleLength / 2f : 2.3f), hw = rider ? 0.35f : (_tc != null ? _tc.VehicleWidth / 2f : 0.9f);
        var f = new Vector3(rp.direction.x, 0f, rp.direction.z);
        if (f.sqrMagnitude < 1e-6f) return true;
        foreach (var c in Physics.OverlapBox(rp.position + Vector3.up * 1.0f, new Vector3(hw + 0.2f, 0.6f, hl + 0.2f), Quaternion.LookRotation(f, Vector3.up), ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.attachedRigidbody != null || c.transform.IsChildOf(transform)) continue;
            var nm = c.name;
            if (nm.StartsWith("Roads") || nm.StartsWith("Road ") || nm.StartsWith("BGT") || nm.StartsWith("Ground") || nm.StartsWith("Terrain") || nm.StartsWith("Zebra") || nm.StartsWith("Kerbs") || nm.StartsWith("Pavements") || nm.StartsWith("Cycle") || nm.StartsWith("Water")) continue;
            if (c.bounds.max.y < rp.position.y + 0.4f) continue;
            return false;
        }
        return true;
    }
}
