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
    [Tooltip("Cars per km of route lane.")]
    public float CarsPerKm = 14f;
    public int MaxCars = 90;
    [Tooltip("Cyclists per km of route, near the kerb (Dutch and Belgian maps: twice as many).")]
    public float CyclistsPerKm = 6f;
    public int MaxCyclists = 40;
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
    List<CoupledSim.Maps.MapRoutes.RoadLine> _lines;
    WaypointCircuit _autopilotRoute;
    float _autopilotFrom;

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
            if (n == "maptraffic" && float.TryParse(p.value, NumberStyles.Float, ci, out var d)) { CarsPerKm = d; _forced = true; }
            if (n == "autopilot" && float.TryParse(p.value, NumberStyles.Float, ci, out var k)) { AutopilotKmh = k; AutopilotFixed = true; }
            if (n == "destination") Destination = p.value;
        }
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

        var gen = OsmMap.Current.Generator;
        var lines = _lines = gen.Roads.Where(r => !r.NoThroughTraffic)
            .Select(r => new CoupledSim.Maps.MapRoutes.RoadLine { Line = r.Line.Select(p => new System.Numerics.Vector3(p.x, p.y, p.z)).ToList(), Width = r.Width, OneWay = r.OneWay, ParkLeft = r.ParkLeft, ParkRight = r.ParkRight, SpeedKmh = r.SpeedLimitKmh, Lanes = r.Lanes }).ToList();
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
        Physics.SyncTransforms();
        int moved = 0; _moves.Clear(); ParkedMoves = 0;
        var someone = FindAnyObjectByType<PlayerAvatar>();
        DebugFocus = someone != null && System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0 ? someone.transform.position : (Vector3?)null;
        foreach (var lane in lanes) moved += KeepOnCarriageway(lane);
        if (moved > 0) Debug.Log($"MapTraffic: {moved} route points moved off cycle paths, cycle lanes and pavements onto the carriageway ({ParkedMoves} of them off grass verges or clear of parked cars)");
        if (moved > 0 && System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0)
        {
            var geo = new CoupledSim.Maps.GeoProjection(OsmMap.Current.Info.Latitude, OsmMap.Current.Info.Longitude);
            Debug.Log("TrafficDebug: the largest moves onto the carriageway: " + string.Join("; ", _moves.OrderByDescending(m => Mathf.Abs(m.shift)).Take(8)
                .Select(m => { geo.ToGeo(m.at.x, m.at.z, out var la, out var lo); return $"{m.shift:F2} m at {m.at} ({la.ToString("F6", CultureInfo.InvariantCulture)},{lo.ToString("F6", CultureInfo.InvariantCulture)})"; })));
        }
        foreach (var lane in lanes) Routes.Add(MakeCircuit(lane, Routes.Count));
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
            : _syncSystem.Prefabs.Where(p => p != null && !p.name.Contains("Bicycle") && !p.name.Contains("Scooter") && !p.name.Contains("Moped") && !p.name.Contains("Bus")).ToArray();
        if (prefabs.Length == 0) { Debug.LogWarning("MapTraffic: no car prefabs"); yield break; }
        var rng = new System.Random(Seed);
        var players = FindObjectsByType<PlayerAvatar>().Select(a => a.transform.position).ToList();
        // a realistic mix: mostly cars, some vans, few trucks, an occasional bus
        float Weight(AICar p) { var n = p.name.ToLowerInvariant(); return n.Contains("bus") ? 0.25f : n.Contains("truck") || n.Contains("daf") ? 0.5f : n.Contains("van") || n.Contains("delivery") ? 1.2f : n.Contains("ambulance") || n.Contains("police") || n.Contains("taxi") ? 0.6f : 3f; }
        float total = prefabs.Sum(Weight);
        AICar Pick() { double x = rng.NextDouble() * total; foreach (var p in prefabs) { x -= Weight(p); if (x <= 0) return p; } return prefabs[prefabs.Length - 1]; }
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
        var centre = (players.Count > 0 ? players : new List<Vector3> { Vector3.zero }).Select(c => (c, 0f)).ToList();
        if (_autopilotRoute != null)
            for (float s = 20f; s <= 1200f && _autopilotFrom + s < _autopilotRoute.OpenLength; s += 20f)
                centre.Add((_autopilotRoute.GetRoutePoint(_autopilotFrom + s).position, 0.3f * s));
        float Near(Vector3 q) { float b = float.MaxValue; foreach (var (c, h) in centre) b = Mathf.Min(b, (c - q).magnitude + h); return b * b; }
        if (places.Count > MaxCars) places = places.OrderBy(x => Near(x.at)).ToList();
        foreach (var (route, i, d, at) in places)
        {
            if (count >= MaxCars) break;
            var rp = route.GetRoutePoint(d);
            if (players.Any(p => (p - rp.position).sqrMagnitude < 15f * 15f)) continue;
            if (placed.Any(q => (q - rp.position).sqrMagnitude < 10f * 10f)) continue;
            placed.Add(rp.position);
            var pos = rp.position;
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
            var dir = rp.direction; dir.y = 0;
            spawn.SetPositionAndRotation(pos + Vector3.up * 0.1f, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var p = spawnParams;
            p.SpawnPoint = spawn; p.Track = route; p.Car = Pick();
            p.color = Colors[rng.Next(Colors.Length)]; p.SpawnDriver = true;
            var car = Spawn(p, false);
            car.GetComponent<WaypointProgressTracker>().SetProgress(d);
            car.name = $"Map traffic {route.name} {i}";
            var traffic = car.gameObject.AddComponent<TrafficCar>();
            traffic.FitToBody();
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
        Debug.Log($"MapTraffic: {count} cars{(places.Count > MaxCars && placed.Count > 0 ? $" (of {places.Count} places at {CarsPerKm} per km: those within {Mathf.Sqrt(placed.Max(Near)):F0} m of the participants{(_autopilotRoute != null ? " and of the autopilot's way ahead" : "")})" : "")}");
        yield return SpawnCyclists();
    }

    /// <summary>Cyclists along the routes: on a cycle path or cycle track beside the road where there is one, else on the
    /// carriageway near its kerb (RideLine), not on roads over 60 km/h (motorways, trunk roads). The routes nearest the
    /// participants first, and on them the places nearest the participants (up to MaxCyclists), so the cyclists ride
    /// where they are seen, also on a map with long corridors.</summary>
    IEnumerator SpawnCyclists()
    {
        var sumo = FindAnyObjectByType<SumoBridge>();
        var bikes = sumo != null && sumo.BicyclePrefabs != null && sumo.BicyclePrefabs.Length > 0 ? sumo.BicyclePrefabs : _syncSystem.Prefabs.Where(p => p != null && p.name.Contains("Bicycle")).ToArray();
        if (bikes.Length == 0 || CyclistsPerKm <= 0f || MaxCyclists <= 0) yield break;
        var country = OsmMap.Current.Info.Country ?? "";
        float perKm = CyclistsPerKm * (country == "Netherlands" || country == "Belgium" || country == "Denmark" ? 2f : 1f);
        bool lht = OsmMap.Current.Info.LeftHandTraffic;
        var rng = new System.Random(Seed + 11);
        var players = FindObjectsByType<PlayerAvatar>().Select(a => a.transform.position).ToList();
        if (players.Count == 0) players.Add(Vector3.zero);
        float Near(Vector3 q) { float b = float.MaxValue; foreach (var c in players) b = Mathf.Min(b, (c - q).sqrMagnitude); return b; }
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
        int index = 0;
        foreach (var (route, from, to, near) in stretches)
        {
            // (enough places near the participants: the further routes would not get any)
            if (places.Count >= MaxCyclists * 2 && near > places.Select(x => Near(x.at)).OrderBy(x => x).ElementAt(MaxCyclists - 1)) break;
            var kerb = RideLine(route, from, to, 4f, lht, true);
            if (kerb.Count < 10) continue;
            var bikeRoute = MakeCircuit(kerb, Routes.Count + 1000 + index++);
            bikeRoute.name = "Map cycle route " + index;
            bikeRoute.GetRoutePoint(0f);
            float bikeLength = bikeRoute.OpenLength;
            int n = Mathf.RoundToInt(bikeLength / 1000f * perKm);
            for (int i = 0; i < n; i++)
            {
                float d = bikeLength * (i + 0.3f) / n;
                if (d <= bikeLength - 60f) places.Add((bikeRoute, i, d, bikeRoute.GetRoutePoint(d).position));
            }
        }
        var spawn = new GameObject("Map cyclists spawn point").transform;
        int count = 0;
        var placed = Cars.Select(c => c.transform.position).ToList();   // (6 m from every car and cyclist placed)
        var used = new HashSet<WaypointCircuit>();
        foreach (var (bikeRoute, i, d, at) in places.OrderBy(x => Near(x.at)))
        {
            if (count >= MaxCyclists) break;
            var rp = bikeRoute.GetRoutePoint(d);
            if (placed.Any(q => (q - rp.position).sqrMagnitude < 6f * 6f)) continue;
            placed.Add(rp.position);
            used.Add(bikeRoute);
            var pos = rp.position;
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
            var dir = rp.direction; dir.y = 0;
            spawn.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var p = spawnParams;
            p.SpawnPoint = spawn; p.Track = bikeRoute; p.Car = bikes[rng.Next(bikes.Length)]; p.color = Colors[rng.Next(Colors.Length)]; p.SpawnDriver = true;
            var bike = Spawn(p, false);
            bike.GetComponent<WaypointProgressTracker>().SetProgress(d);
            bike.name = $"Map cyclist {bikeRoute.name.Substring("Map cycle route ".Length)} {i}";
            var t = bike.gameObject.AddComponent<TrafficCar>();
            t.VehicleLength = 1.8f; t.LaneHalfWidth = 0.8f;
            float v = (16f + Normal(rng) * 3f) / 3.6f;
            t.Init(CoupledSim.Traffic.CarFollowingModelType.IDM, Mathf.Clamp(v, 2.5f, 7f), new System.Random(rng.Next()));
            t.Speed = v * 0.7f;
            bike.gameObject.AddComponent<MapRouteRecycler>();
            Cars.Add(t);
            count++;
            if (count % 10 == 0) yield return null;
        }
        // (the cycle routes without a cyclist)
        int withCyclists = used.Count;
        foreach (var (bikeRoute, _, _, _) in places) if (used.Add(bikeRoute)) Destroy(bikeRoute.gameObject);
        Destroy(spawn.gameObject);
        Debug.Log($"MapTraffic: {count} cyclists on {withCyclists} cycle routes");
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
        var dest = Destination != null ? Destination.Split(',') : null;
        if (dest != null && dest.Length >= 2 && double.TryParse(dest[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var dla)
            && double.TryParse(dest[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var dlo) && _lines != null)
        {
            new CoupledSim.Maps.GeoProjection(OsmMap.Current.Info.Latitude, OsmMap.Current.Info.Longitude).ToLocal(dla, dlo, out var dx, out var dz);
            var path = CoupledSim.Maps.MapRoutes.ShortestPath(_lines, new System.Numerics.Vector3(at.x, at.y, at.z), new System.Numerics.Vector3(fwd.x, 0f, fwd.z),
                new System.Numerics.Vector3((float)dx, 0f, (float)dz), OsmMap.Current.Info.LeftHandTraffic);
            if (path.Count >= 2)
            {
                var lane = path.Select(q => new Vector3(q.X, q.Y, q.Z)).ToList();
                KeepOnCarriageway(lane);
                best = MakeCircuit(lane, 900);
                best.name = "Route to " + Destination;
                best.GetRoutePoint(0f);
                bestScore = 0f;
                Debug.Log($"MapTraffic: the autopilot drives to {Destination}: {best.OpenLength / 1000f:F1} km");
            }
            else Debug.LogWarning($"MapTraffic: no route from here to {Destination} ({CoupledSim.Maps.MapRoutes.Why}); the autopilot takes the nearest route");
        }
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
        if ((start.position - at).sqrMagnitude > 9f)
        {
            var p = start.position;
            if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.1f;
            var rb = me.GetComponent<Rigidbody>();
            var rot = Quaternion.LookRotation(new Vector3(start.direction.x, 0, start.direction.z), Vector3.up);
            if (rb != null) { rb.position = p; rb.rotation = rot; rb.linearVelocity = Vector3.zero; }
            me.transform.SetPositionAndRotation(p, rot);
        }
        tracker.Init(best);
        tracker.SetProgress(bestD);
        _autopilotRoute = best; _autopilotFrom = bestD;
        aiCar.target = tracker.target;
        if (!me.TryGetComponent<AutomatedDriving>(out var automation)) automation = me.gameObject.AddComponent<AutomatedDriving>();
        automation.Mode = AutomatedDriving.DrivingMode.TrafficModel;
        automation.DesiredSpeedKmh = AutopilotKmh;
        automation.SpeedLimitFactor = AutopilotFixed ? 0f : 1f;
        ps.ActivatePlayerAICar();
        me.gameObject.AddComponent<MapRouteRecycler>().Recycle = false;   // (it stops at its route's end)
        Debug.Log($"MapTraffic: autopilot at {AutopilotKmh} km/h{(AutopilotFixed ? "" : " in town, the speed limit on faster roads")} on {best.name} from {bestD:F0} m, at {start.position} (the participant was at {at})");
    }

    /// <summary>Pedestrians walk along the nearest long pavement; riders follow the nearest lane route on its riding line (RideLine).</summary>
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
            WaypointCircuit bestR = null; float bestD = 0f, bestScore = float.MaxValue;
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
                // 45 km/h mopeds on the road
                var tw = me.GetComponent<TwoWheelerController>();
                bool moped = tw != null && tw.Type == CoupledSim.TwoWheelers.TwoWheelerType.Moped45;
                path = RideLine(bestR, bestD, Mathf.Max(bestD + 6f, bestR.OpenLength - 50f), 3f, OsmMap.Current.Info.LeftHandTraffic, !moped);
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
        Debug.Log($"MapTraffic: autopilot for the {(me.GetComponent<KeyboardPlayerMovement>() != null ? "pedestrian" : "rider")} along {path.Count} points{(RideMoves > 0 ? $" ({RideMoves} of them moved clear of posts and pavement)" : "")}");
    }

    /// <summary>
    /// The lane routes come from OpenStreetMap's centre lines and widths; where the carriageway really is narrower or lies
    /// a little to the side (as the Dutch topography's surfaces show it), or has a painted cycle lane at its edge, a car on
    /// the lane would drive partly or wholly on the cycle path, cycle lane, pavement or parking strip beside it (and into the benches
    /// and posts there), or where the lanes of a junction cut across a grass verge or median (the Dutch topography's green
    /// areas) or the corner of a canal's quay (moved however short the stretch). Each point where the car's middle or either side (0.8 m out) is over a cycle path, cycle lane, pavement or
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
        bool Clear(Vector3 p, Vector3 s) => !Off(SeenGround(p)) && !Off(SeenGround(p + s * 0.8f)) && !Off(SeenGround(p - s * 0.8f));
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
        }
        int moved = 0;
        // a median over 24 m (a lone point over a cycle path, as where one crosses the road, or a small patch, does
        // not bend the lane: only a stretch of some 15 m and more moves), then an average over 12 m, so the lane eases
        // across (the lanes' points are LaneSpacing, 3 m, apart)
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
            for (int k = 1; k <= 10 && park[i] == 0f; k++)
                foreach (int sgn in new[] { -1, 1 })
                {
                    var q = p + side[i] * (sgn * k * 0.25f);
                    if (park[i] == 0f && !ParkedCarNear(q, dir) && SeenGround(q) == Ground.Road && Clear(q, side[i])) park[i] = sgn * k * 0.25f;
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
                if ((lane[i] - DebugFocus.Value).sqrMagnitude < 30f * 30f)
                    Debug.Log($"TrafficDebug: lane point {i}/{lane.Count} at {lane[i]}: shift found {shift[i]:F2}, median {median[i]:F2}, applied {smooth[i]:F2}, parked cars {park[i]:F2}, in all {total[i]:F2}");
        for (int i = 0; i < lane.Count; i++)
            if (Mathf.Abs(total[i]) > 0.01f) { _moves.Add((lane[i], total[i])); if (Mathf.Abs(park[i]) > 0f) ParkedMoves++; lane[i] += side[i] * total[i]; moved++; }
        return moved;
    }

    /// <summary>The ground at a point as it is seen: the highest surfaces under it that are drawn (on the maps with the
    /// national topography's surfaces, OpenStreetMap's roads are invisible driving surfaces under or over the topography's
    /// cycle paths and pavements, so the first collider is not always the surface one sees; and the topography's cycle
    /// paths can lie on its carriageway at the same height).</summary>
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
    /// <summary>Lane points moved away from parked cars.</summary>
    static int ParkedMoves;

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

    enum Ground { None, Road, Cycle, Paving, Pavement, Green, Bare }

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
    /// the carriageway is taken (its kerb-side half when it is wide enough for two directions) when
    /// <paramref name="preferCycle"/>; otherwise the carriageway 0.8 m from its edge (in a painted cycle lane, if the road
    /// has one), never the pavement, 1 m clear of parked cars. The offsets are smoothed (a median over 7 points, then an average over 3), so short
    /// gaps (junctions, driveways, parked vehicles) do not make the line jump.
    /// </summary>
    public static bool DebugRide = Array.IndexOf(CommandLine.Args, "-debugRide") >= 0;

    static List<Vector3> RideLine(WaypointCircuit route, float from, float to, float step, bool lht, bool preferCycle)
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
            float pick = bestB >= 0 ? Mathf.Max(roadStart + 0.5f, Mathf.Min(roadEdge - 0.8f, 4f)) : 0.5f;
            if (preferCycle)
            {
                // a cycle path starting within 6 m of the carriageway's edge, at least 1 m wide
                float start = -1f, end = -1f;
                for (int k = Mathf.Max(1, Mathf.RoundToInt(roadEdge / 0.5f)); k <= Mathf.RoundToInt((Mathf.Min(roadEdge, 6f) + 6f) / 0.5f); k++)
                {
                    bool cyc = GroundAt(p + side * (k * 0.5f)) == Ground.Cycle;
                    if (cyc && start < 0f) start = k * 0.5f;
                    if (cyc) end = k * 0.5f; else if (start >= 0f) break;
                }
                if (start >= 0f && end - start >= 0.9f) pick = end - start >= 2.4f ? start + 0.7f * (end - start) : (start + end) / 2f;
            }
            limit[i] = pick > roadEdge + 0.4f ? float.MaxValue : roadEdge - 1.0f;   // (a cycle path beside the road: no limit)
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
        const float Clearance = 0.6f, PostClearance = 1f;
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
            return false;
        }
        var shift = new float[line.Count];
        int moved = 0;
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

    void FixedUpdate()
    {
        if (_tracker == null || _tracker.Circuit == null || Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.5f;
        var c = _tracker.Circuit;
        float d = Mathf.Repeat(_tracker.ProgressDistance, c.Length);
        // at the end of its route (TrafficCar stops there; a circuit's closing piece back to the start, straight across the
        // map, is never driven)
        if (d < c.OpenLength - 15f || !Recycle) return;
        var start = c.GetRoutePoint(4f);
        foreach (var other in Physics.OverlapSphere(start.position, 12f, ~0, QueryTriggerInteraction.Ignore))
            if (other.attachedRigidbody != null && other.attachedRigidbody != _rb && other.attachedRigidbody.GetComponent<AICar>() != null) return;
        // (cars moved to a start in this physics step are not in the overlap test yet)
        _recent.RemoveAll(r => Time.time - r.time > 3f);
        if (_recent.Exists(r => (r.pos - start.position).sqrMagnitude < 12f * 12f)) return;
        _recent.Add((start.position, Time.time));
        var p = start.position;
        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.1f;
        var rot = Quaternion.LookRotation(new Vector3(start.direction.x, 0, start.direction.z), Vector3.up);
        float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
        if (_rb != null) { _rb.position = p; _rb.rotation = rot; _rb.linearVelocity = rot * Vector3.forward * speed; }
        transform.SetPositionAndRotation(p, rot);
        _tracker.SetProgress(4f);
    }
}
