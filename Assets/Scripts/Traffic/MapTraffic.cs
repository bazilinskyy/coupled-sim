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
        if (OsmMap.Current == null || Current != null || Array.IndexOf(Environment.GetCommandLineArgs(), "-autopilot") < 0) return;
        var t = new GameObject("Map routes (autopilot)").AddComponent<MapTraffic>();
        t.ApplyCommandLine();
        t.CarsPerKm = 0f; t._forced = false;
        t.StartCoroutine(t.SpawnCoroutine());
    }

    void Awake() => Current = this;
    void OnDestroy() { if (Current == this) Current = null; }

    void ApplyCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        var ci = CultureInfo.InvariantCulture;
        int i = Array.IndexOf(args, "-mapTraffic");
        if (i >= 0) { _forced = true; if (i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, ci, out var d)) CarsPerKm = d; }
        i = Array.IndexOf(args, "-autopilot");
        if (i >= 0) AutopilotKmh = i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, ci, out var k) ? k : 35f;
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
            if (n == "autopilot" && float.TryParse(p.value, NumberStyles.Float, ci, out var k)) AutopilotKmh = k;
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
        var lines = gen.Roads.Where(r => !r.Bridge || r.BridgeLayer == 0)
            .Select(r => new CoupledSim.Maps.MapRoutes.RoadLine { Line = r.Line.Select(p => new System.Numerics.Vector3(p.x, p.y, p.z)).ToList(), Width = r.Width, OneWay = r.OneWay, ParkLeft = r.ParkLeft, ParkRight = r.ParkRight }).ToList();
        var lanes = CoupledSim.Maps.MapRoutes.Build(lines, gen.HalfSize, OsmMap.Current.Info.LeftHandTraffic, max: 80, variants: 3)
            .Select(l => l.Select(p => new Vector3(p.X, p.Y, p.Z)).ToList()).ToList();
        foreach (var lane in lanes) Routes.Add(MakeCircuit(lane, Routes.Count));
        foreach (var r in Routes) r.GetRoutePoint(0f);   // (the circuit works out its length on first use)
        Debug.Log("MapTraffic: " + CoupledSim.Maps.MapRoutes.LastStats);
        Debug.Log($"MapTraffic: {Routes.Count} routes ({Routes.Sum(r => r.Length) / 1000f:F1} km of lanes){(sumoRuns ? ", SUMO runs the traffic" : "")}");

        if (AutopilotKmh > 0f) Autopilot();
        if ((!sumoRuns || _forced) && CarsPerKm > 0f) yield return SpawnCars();
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
            w.position = lane[i] + Vector3.up * 0.2f;
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
        foreach (var route in Routes)
        {
            float length = route.Length > 0 ? route.Length : 1f;
            int n = Mathf.RoundToInt(length / 1000f * CarsPerKm);
            for (int i = 0; i < n && count < MaxCars; i++)
            {
                float d = length * (i + 0.5f) / n + (float)rng.NextDouble() * 6f;
                if (d > length - 60f) continue;   // not in the turnaround at the end
                var rp = route.GetRoutePoint(d);
                if (players.Any(p => (p - rp.position).sqrMagnitude < 15f * 15f)) continue;
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
                traffic.Speed = Mathf.Max(2f, v * 0.6f);
                car.gameObject.AddComponent<MapRouteRecycler>();
                Cars.Add(traffic);
                count++;
                if (count % 10 == 0) yield return null;
            }
        }
        Destroy(spawn.gameObject);
        Debug.Log($"MapTraffic: {count} cars");
        yield return SpawnCyclists();
    }

    /// <summary>Cyclists along the routes: on a cycle path or cycle track beside the road where there is one, else on the carriageway near its kerb (RideLine).</summary>
    IEnumerator SpawnCyclists()
    {
        var sumo = FindAnyObjectByType<SumoBridge>();
        var bikes = sumo != null && sumo.BicyclePrefabs != null && sumo.BicyclePrefabs.Length > 0 ? sumo.BicyclePrefabs : _syncSystem.Prefabs.Where(p => p != null && p.name.Contains("Bicycle")).ToArray();
        if (bikes.Length == 0 || CyclistsPerKm <= 0f) yield break;
        var country = OsmMap.Current.Info.Country ?? "";
        float perKm = CyclistsPerKm * (country == "Netherlands" || country == "Belgium" || country == "Denmark" ? 2f : 1f);
        bool lht = OsmMap.Current.Info.LeftHandTraffic;
        var rng = new System.Random(Seed + 11);
        var spawn = new GameObject("Map cyclists spawn point").transform;
        int count = 0, index = 0;
        foreach (var route in Routes.ToList())
        {
            if (count >= MaxCyclists) break;
            var kerb = RideLine(route, 0f, route.Length, 4f, lht, true);
            if (kerb.Count < 10) continue;
            var bikeRoute = MakeCircuit(kerb, Routes.Count + 1000 + index++);
            bikeRoute.name = "Map cycle route " + index;
            bikeRoute.GetRoutePoint(0f);
            int n = Mathf.Min(5, Mathf.RoundToInt(bikeRoute.Length / 1000f * perKm));   // (spread over the routes)
            for (int i = 0; i < n && count < MaxCyclists; i++)
            {
                float d = bikeRoute.Length * (i + 0.3f) / n;
                if (d > bikeRoute.Length - 60f) continue;
                var rp = bikeRoute.GetRoutePoint(d);
                var pos = rp.position;
                if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
                var dir = rp.direction; dir.y = 0;
                spawn.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.LookRotation(dir.normalized, Vector3.up));
                var p = spawnParams;
                p.SpawnPoint = spawn; p.Track = bikeRoute; p.Car = bikes[rng.Next(bikes.Length)]; p.color = Colors[rng.Next(Colors.Length)]; p.SpawnDriver = true;
                var bike = Spawn(p, false);
                bike.GetComponent<WaypointProgressTracker>().SetProgress(d);
                bike.name = $"Map cyclist {index} {i}";
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
        }
        Destroy(spawn.gameObject);
        Debug.Log($"MapTraffic: {count} cyclists");
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
        if (tracker == null || me.GetComponent<AICar>() == null) { Debug.LogWarning("MapTraffic: this car has no automation"); return; }
        WaypointCircuit best = null; float bestD = 0f, bestScore = float.MaxValue;
        var at = me.transform.position; var fwd = me.transform.forward;
        foreach (var r in Routes)
            for (float d = 0; d < r.Length - 150f; d += 4f)
            {
                var rp = r.GetRoutePoint(d);
                float score = (rp.position - at).sqrMagnitude + (Vector3.Dot(rp.direction, fwd) < 0.5f ? 400f : 0f);
                if (score < bestScore) { bestScore = score; best = r; bestD = d; }
            }
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
        if (!me.TryGetComponent<AutomatedDriving>(out var automation)) automation = me.gameObject.AddComponent<AutomatedDriving>();
        automation.Mode = AutomatedDriving.DrivingMode.TrafficModel;
        automation.DesiredSpeedKmh = AutopilotKmh;
        ps.ActivatePlayerAICar();
        me.gameObject.AddComponent<MapRouteRecycler>();
        Debug.Log($"MapTraffic: autopilot at {AutopilotKmh} km/h on {best.name} from {bestD:F0} m");
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
                for (float d = 0; d < r.Length - 100f; d += 4f)
                {
                    var rp = r.GetRoutePoint(d);
                    float score = (rp.position - at).sqrMagnitude + (Vector3.Dot(rp.direction, fwd) < 0.3f ? 900f : 0f);
                    if (score < bestScore) { bestScore = score; bestR = r; bestD = d; }
                }
            // bicycles, e-scooters and 25 km/h mopeds on the cycle path or track beside the road where there is one;
            // 45 km/h mopeds on the road
            var tw = me.GetComponent<TwoWheelerController>();
            bool moped = tw != null && tw.Type == CoupledSim.TwoWheelers.TwoWheelerType.Moped45;
            path = RideLine(bestR, bestD, bestR.Length - 50f, 3f, OsmMap.Current.Info.LeftHandTraffic, !moped);
        }
        if (path == null || path.Count < 2) { Debug.LogWarning("MapTraffic: no path for the autopilot"); return; }
        // start on the path, facing along it
        int i0 = 0; float bd = float.MaxValue;
        for (int i = 0; i < path.Count - 1; i++) { float d = (path[i] - at).sqrMagnitude; if (d < bd) { bd = d; i0 = i; } }
        if (bd > 4f)
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

    enum Ground { None, Road, Cycle, Paving }

    static Ground GroundAt(Vector3 p)
    {
        if (!Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return Ground.None;
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
    public static bool DebugRide = Array.IndexOf(Environment.GetCommandLineArgs(), "-debugRide") >= 0;

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
    WaypointProgressTracker _tracker;
    Rigidbody _rb;
    float _nextCheck;

    void Start() { _tracker = GetComponent<WaypointProgressTracker>(); _rb = GetComponent<Rigidbody>(); }

    void FixedUpdate()
    {
        if (_tracker == null || _tracker.Circuit == null || Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.5f;
        var c = _tracker.Circuit;
        float d = Mathf.Repeat(_tracker.ProgressDistance, c.Length);
        if (d < c.Length - 45f) return;
        var start = c.GetRoutePoint(4f);
        foreach (var other in Physics.OverlapSphere(start.position, 12f, ~0, QueryTriggerInteraction.Ignore))
            if (other.attachedRigidbody != null && other.attachedRigidbody != _rb && other.attachedRigidbody.GetComponent<AICar>() != null) return;
        var p = start.position;
        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.1f;
        var rot = Quaternion.LookRotation(new Vector3(start.direction.x, 0, start.direction.z), Vector3.up);
        float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
        if (_rb != null) { _rb.position = p; _rb.rotation = rot; _rb.linearVelocity = rot * Vector3.forward * speed; }
        transform.SetPositionAndRotation(p, rot);
        _tracker.SetProgress(4f);
    }
}
