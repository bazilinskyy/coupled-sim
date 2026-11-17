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
        int moved = 0; _moves.Clear();
        foreach (var lane in lanes) moved += KeepOnCarriageway(lane);
        if (moved > 0) Debug.Log($"MapTraffic: {moved} route points moved off cycle paths, cycle lanes and pavements onto the carriageway");
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
        var centre = players.Count > 0 ? players : new List<Vector3> { Vector3.zero };
        float Near(Vector3 q) { float b = float.MaxValue; foreach (var c in centre) b = Mathf.Min(b, (c - q).sqrMagnitude); return b; }
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
        Debug.Log($"MapTraffic: {count} cars{(places.Count > MaxCars && placed.Count > 0 ? $" (of {places.Count} places at {CarsPerKm} per km: those within {Mathf.Sqrt(placed.Max(Near)):F0} m of the participants)" : "")}");
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
        if (best == null)
        foreach (var r in Routes)
            for (float d = 0; d < r.OpenLength - 150f; d += 4f)
            {
                var rp = r.GetRoutePoint(d);
                float score = (rp.position - at).sqrMagnitude + (Vector3.Dot(rp.direction, fwd) < 0.5f ? 400f : 0f);
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
        Debug.Log($"MapTraffic: autopilot for the {(me.GetComponent<KeyboardPlayerMovement>() != null ? "pedestrian" : "rider")} along {path.Count} points");
    }

    /// <summary>
    /// The lane routes come from OpenStreetMap's centre lines and widths; where the carriageway really is narrower or lies
    /// a little to the side (as the Dutch topography's surfaces show it), or has a painted cycle lane at its edge, a car on
    /// the lane would drive partly or wholly on the cycle path, cycle lane or pavement beside it (and into the benches
    /// and posts there). Each point where the car's middle or either side (0.8 m out) is over a cycle path, cycle lane or
    /// pavement, as it is seen (<see cref="SeenGround"/>), is moved across by the least that keeps all three off them, at
    /// most 3 m; the moves are smoothed (a median over 5 points), so the lane does not zigzag. A cycle path or pavement
    /// that crosses the road (no clear carriageway beside it) is left alone. Returns how many points moved.
    /// </summary>
    static int KeepOnCarriageway(List<Vector3> lane)
    {
        if (lane.Count < 2) return 0;
        var shift = new float[lane.Count];
        var side = new Vector3[lane.Count];
        bool any = false;
        bool Off(Ground g) => g == Ground.Cycle || g == Ground.Pavement;
        bool Clear(Vector3 p, Vector3 s) => !Off(SeenGround(p)) && !Off(SeenGround(p + s * 0.8f)) && !Off(SeenGround(p - s * 0.8f));
        for (int i = 0; i < lane.Count; i++)
        {
            var d = lane[Mathf.Min(i + 1, lane.Count - 1)] - lane[Mathf.Max(i - 1, 0)]; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) continue;
            side[i] = Vector3.Cross(Vector3.up, d.normalized);   // (to the right)
            if (Clear(lane[i], side[i])) continue;
            // the least move either way that clears the car of it
            for (int k = 1; k <= 12 && shift[i] == 0f; k++)
                foreach (int sgn in new[] { -1, 1 })
                    if (shift[i] == 0f && SeenGround(lane[i] + side[i] * (sgn * k * 0.25f)) == Ground.Road && Clear(lane[i] + side[i] * (sgn * k * 0.25f), side[i]))
                    { shift[i] = sgn * k * 0.25f; any = true; }
        }
        if (!any) return 0;
        int moved = 0;
        var smooth = new float[lane.Count];
        for (int i = 0; i < lane.Count; i++)
        {
            var w = new List<float>();
            for (int k = Mathf.Max(0, i - 2); k <= Mathf.Min(lane.Count - 1, i + 2); k++) w.Add(shift[k]);
            w.Sort(); smooth[i] = w[w.Count / 2];
            // (a point over a cycle path keeps its own move even where its neighbours had none: the median would drop it)
            if (shift[i] != 0f && Mathf.Abs(smooth[i]) < Mathf.Abs(shift[i])) smooth[i] = shift[i];
        }
        for (int i = 0; i < lane.Count; i++)
            if (smooth[i] != 0f) { _moves.Add((lane[i], smooth[i])); lane[i] += side[i] * smooth[i]; moved++; }
        return moved;
    }

    /// <summary>The ground at a point as it is seen: the highest surface under it that is drawn (on the maps with the
    /// national topography's surfaces, OpenStreetMap's roads are invisible driving surfaces under or over the topography's
    /// cycle paths and pavements, so the first collider is not always the surface one sees).</summary>
    static Ground SeenGround(Vector3 p)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 4f, Vector3.down, _groundHits, 10f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue; Collider top = null;
        for (int i = 0; i < n; i++)
        {
            var h = _groundHits[i];
            if (h.collider.attachedRigidbody != null || h.point.y <= best) continue;
            if (h.collider.TryGetComponent<MeshRenderer>(out var mr) && !mr.enabled) continue;   // (an invisible driving surface)
            if (h.normal.y < 0.7f) continue;   // (a wall, a pole: not ground)
            best = h.point.y; top = h.collider;
        }
        if (top == null) return Ground.None;
        var name = top.name;
        if (name.IndexOf("ycle", StringComparison.Ordinal) >= 0) return Ground.Cycle;
        if (name.StartsWith("Roads") || name.StartsWith("BGT roads") || name.StartsWith("Zebra") || name.StartsWith("Road ")) return Ground.Road;
        if (name.StartsWith("BGT pavements") || name.StartsWith("Pavements") || name.StartsWith("Kerbs")) return Ground.Pavement;
        return Ground.Paving;
    }
    static readonly RaycastHit[] _groundHits = new RaycastHit[16];
    static readonly List<(Vector3 at, float shift)> _moves = new List<(Vector3, float)>();

    enum Ground { None, Road, Cycle, Paving, Pavement }

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
