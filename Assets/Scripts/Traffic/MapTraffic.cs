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
    public int MaxCars = 70;
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
            .Select(r => new CoupledSim.Maps.MapRoutes.RoadLine { Line = r.Line.Select(p => new System.Numerics.Vector3(p.x, p.y, p.z)).ToList(), Width = r.Width, OneWay = r.OneWay }).ToList();
        var lanes = CoupledSim.Maps.MapRoutes.Build(lines, gen.HalfSize, OsmMap.Current.Info.LeftHandTraffic)
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
                p.SpawnPoint = spawn; p.Track = route; p.Car = prefabs[rng.Next(prefabs.Length)];
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

    /// <summary>Pedestrians walk along the nearest long pavement; riders follow the nearest lane route near its kerb.</summary>
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
            path = new List<Vector3>();
            for (float d = bestD; d < bestR.Length - 50f; d += 3f)
            {
                var rp = bestR.GetRoutePoint(d);
                path.Add(rp.position + Vector3.Cross(Vector3.up, rp.direction).normalized * (OsmMap.Current.Info.LeftHandTraffic ? -1.4f : 1.4f));
            }
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
