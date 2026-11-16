using System.Collections.Generic;
using CoupledSim.Traffic;
using UnityEngine;
using UnityStandardAssets.Utility;

/// <summary>
/// Drives an AI car along its waypoint circuit with a car-following model (IDM, Gipps or Krauss).
/// It looks ahead along its own path for the nearest obstacle - another car, a pedestrian on the
/// road, or the stop line of a red/yellow traffic light - and gives AICar the resulting speed.
/// Added at spawn time by <see cref="TrafficSpawner"/>; runs on the host only.
/// </summary>
[RequireComponent(typeof(AICar))]
public class TrafficCar : MonoBehaviour
{
    public CarFollowingModelType ModelType = CarFollowingModelType.IDM;
    public float DesiredSpeed = 13.9f;
    [Tooltip("How far ahead along the path the driver looks (m).")]
    public float LookAhead = 80f;
    [Tooltip("Sideways distance (m) from the path within which something counts as being in the lane.")]
    public float LaneHalfWidth = 1.4f;
    [Tooltip("Stop for pedestrians on the road ahead.")]
    public bool YieldToPedestrians = true;
    [Tooltip("Stop at red (and yellow, if it can stop comfortably) traffic lights.")]
    public bool ObeyTrafficLights = true;
    public float VehicleLength = 4.5f;
    [Tooltip("Comfortable lateral acceleration in bends (m/s²); drivers slow down before curves to stay below it.")]
    public float MaxLateralAcceleration = 2.5f;

    [Header("State (read only)")]
    public float Speed;
    public float Acceleration;
    public string LeaderDescription;
    [Tooltip("Gap to what this car follows or stops for (m; infinite on a free road): how far it will go on.")]
    public float LeaderGap = float.PositiveInfinity;

    public ICarFollowingModel Model { get; private set; }

    /// <summary>
    /// SafetyOnly: the car keeps its own (scripted) speed profile; only when a car is close ahead
    /// is its speed capped to a safe following speed (Krauss safe speed), so scripted AVs do not
    /// run into traffic. Pedestrians and lights are left to the script in this mode.
    /// </summary>
    public bool SafetyOnly { get; private set; }

    AICar _car;
    WaypointProgressTracker _tracker;
    WaypointCircuit _circuit;

    // shared lists, rebuilt at most once per physics step
    static readonly List<Transform> _cars = new List<Transform>();
    static readonly List<Transform> _pedestrians = new List<Transform>();
    static readonly List<CarTrafficLight> _lights = new List<CarTrafficLight>();
    static int _cacheFrame = -1;
    static float _nextLightScan;

    public void Init(CarFollowingModelType type, float desiredSpeed, System.Random rng)
    {
        ModelType = type;
        DesiredSpeed = desiredSpeed;
        Model = CarFollowingModels.Create(type, desiredSpeed, rng);
        _car = GetComponent<AICar>();
        _tracker = GetComponent<WaypointProgressTracker>();
        _circuit = _tracker != null ? _tracker.Circuit : null;
        _car.ExternalSpeedControl = true;
        Speed = 0f;
    }

    /// <summary>Collision-avoidance supervisor for a car that follows its own script (e.g. the AV).</summary>
    public void InitSafetyOnly()
    {
        SafetyOnly = true;
        YieldToPedestrians = false;
        ObeyTrafficLights = false;
        _car = GetComponent<AICar>();
        _tracker = GetComponent<WaypointProgressTracker>();
        _circuit = _tracker != null ? _tracker.Circuit : null;
    }

    static void RefreshCaches()
    {
        if (_cacheFrame == Time.frameCount) return;
        _cacheFrame = Time.frameCount;
        _cars.Clear();
        _pedestrians.Clear();
        foreach (var a in FindObjectsByType<PlayerAvatar>())
        {
            if (!a.gameObject.activeInHierarchy || a.Type == AvatarType.Drone) continue;
            (a.Type == AvatarType.Driver ? _cars : _pedestrians).Add(a.transform);
        }
        if (DebugTraffic) DebugCount();
        if (Time.time >= _nextLightScan)
        {
            _nextLightScan = Time.time + 2f;
            _lights.Clear();
            _lights.AddRange(FindObjectsByType<CarTrafficLight>());
        }
    }

    void FixedUpdate()
    {
        if (SafetyOnly)
        {
            if (_circuit == null) return;
            Speed = TryGetComponent<Rigidbody>(out var rb) ? Mathf.Max(0f, Vector3.Dot(rb.linearVelocity, transform.forward)) : 0f;
            var lead = FindLeader(out LeaderDescription);
            if (lead.Present)
            {
                // Krauss safe speed with tau = 1 s and b = 4 m/s²
                const float tau = 1f, b = 4f;
                float g = Mathf.Max(0f, lead.Gap - 2f);
                float vMean = 0.5f * (Speed + lead.Speed);
                _car.SpeedCap = Mathf.Max(0f, lead.Speed + (g - lead.Speed * tau) / (vMean / b + tau));
            }
            else
            {
                _car.SpeedCap = float.PositiveInfinity;
            }
            return;
        }
        if (Model == null || _circuit == null) return;
        float dt = Time.fixedDeltaTime;
        Model.DesiredSpeed = Mathf.Min(DesiredSpeed, CurveSpeed());
        var leader = FindLeader(out LeaderDescription);
        LeaderGap = leader.Present ? leader.Gap : float.PositiveInfinity;
        if (Speed > 0.3f) _stoppedSince = 0f; else if (_stoppedSince == 0f) _stoppedSince = Time.time;
        Acceleration = Model.Acceleration(Speed, leader, dt);
        Speed = Mathf.Max(0f, Speed + Acceleration * dt);
        _car.ExternalSpeed = Speed;
        _car.ExternalAcceleration = Acceleration;
    }

    /// <summary>
    /// Highest speed now that still lets the car take the bends ahead with at most
    /// MaxLateralAcceleration, braking comfortably (2 m/s²) before them.
    /// </summary>
    float CurveSpeed()
    {
        float start = _tracker.ProgressDistance;
        float limit = float.PositiveInfinity;
        const float step = 3f, comfortableBraking = 2f;
        var prevDir = _circuit.GetRoutePoint(start).direction;
        for (float d = step; d <= 60f; d += step)
        {
            var dir = _circuit.GetRoutePoint(start + d).direction;
            float angle = Vector3.Angle(Flat(prevDir), Flat(dir)) * Mathf.Deg2Rad;
            prevDir = dir;
            if (angle < 1e-3f) continue;
            float radius = step / angle;
            float vCurve = Mathf.Sqrt(MaxLateralAcceleration * radius);
            limit = Mathf.Min(limit, Mathf.Sqrt(vCurve * vCurve + 2f * comfortableBraking * Mathf.Max(0f, d - step)));
        }
        return Mathf.Max(2f, limit);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    /// <summary>Walks along the own path in 2 m steps and returns the closest obstacle.</summary>
    Leader FindLeader(out string what)
    {
        RefreshCaches();
        what = "free road";
        float start = _tracker.ProgressDistance;
        var me = transform.position;
        // distance from the car to its progress point (the tracker lags a little behind)
        float offset = Vector3.Dot(_circuit.GetRoutePoint(start).position - me, transform.forward);
        float lw2 = LaneHalfWidth * LaneHalfWidth;
        const float step = 2f;
        // side by side with another vehicle heading the same way (two routes merging into one lane or cycle path): the
        // earlier spawned one goes on, this one holds back
        foreach (var c in _cars)
        {
            if (c == transform || Vector3.Dot(c.forward, transform.forward) <= 0.3f) continue;
            var dv = c.position - me; dv.y = 0f;
            if (dv.sqrMagnitude > VehicleLength * VehicleLength) continue;
            float al = Vector3.Dot(dv, transform.forward), ac = Mathf.Abs(Vector3.Dot(dv, transform.right));
            int rk = c.TryGetComponent<TrafficCar>(out var tc2) ? tc2._rank : int.MinValue;
            // the one further back waits (the other ahead by up to three quarters of a vehicle length); level with it
            // (within 0.3 m): the earlier spawned goes on
            bool beside = Mathf.Abs(al) <= 0.3f && rk < _rank, justAhead = al > 0.3f && al < VehicleLength * 0.75f;
            // (overlapping: closer across than their half widths together; a car is about 1.8 m wide, a bicycle 0.6 m)
            float halfWidths = HalfWidth(transform) + HalfWidth(c);
            if (ac < halfWidths && (beside || justAhead))
            {
                what = $"making room for {c.name}";
                return Leader.Stationary(0.1f);
            }
        }
        string conflictWhat = null;
        float conflict = NoYield ? float.MaxValue : Conflict(start, offset, out conflictWhat);
        for (float d = step; d <= LookAhead; d += step)
        {
            var p = _circuit.GetRoutePoint(start + d).position;
            float along = d + offset;
            if (along < 1f) continue;
            if (along >= conflict)
            {
                what = conflictWhat;
                return Leader.Stationary(Mathf.Max(0.1f, conflict - VehicleLength / 2f - 2f));
            }

            foreach (var c in _cars)
            {
                if (c == transform) continue;
                var q = c.position;
                if (Flat2(q - p) < lw2 && Vector3.Dot(c.forward, transform.forward) > 0.3f)
                {
                    var tracked = c.GetComponent<TrafficCar>();
                    float leaderSpeed = tracked != null ? tracked.Speed : SpeedOf(c);
                    what = $"car {c.name} at {along:F0} m";
                    return Leader.Vehicle(Mathf.Max(0.1f, along - VehicleLength), leaderSpeed);
                }
            }
            if (YieldToPedestrians)
            {
                foreach (var animal in AnimalAgent.All)
                {
                    if (animal.Current == AnimalAgent.State.Fly) continue;
                    var ap = animal.transform.position;
                    if (Flat2(ap - p) < lw2 * 1.2f && Mathf.Abs(ap.y - p.y) < 1.5f)
                    {
                        what = $"{animal.Species} at {along:F0} m";
                        return Leader.Stationary(Mathf.Max(0.1f, along - VehicleLength / 2f - 1.5f));
                    }
                }
                foreach (var ped in _pedestrians)
                {
                    if (Flat2(ped.position - p) < lw2 * 1.2f && Mathf.Abs(ped.position.y - p.y) < 1.5f)
                    {
                        what = $"pedestrian {ped.name} at {along:F0} m";
                        return Leader.Stationary(Mathf.Max(0.1f, along - VehicleLength / 2f - 1.5f));
                    }
                }
            }
            if (ObeyTrafficLights)
            {
                foreach (var light in _lights)
                {
                    if (light.State != LightState.RED && light.State != LightState.YELLOW && light.State != LightState.RED_AND_YELLOW) continue;
                    // lights stand beside the road, a few metres past the stop line, facing the traffic
                    var lp = light.transform.position;
                    if (Flat2(lp - p) > 6f * 6f) continue;
                    if (!FacesTraffic(light.transform)) continue;
                    // turn arrows: an arrow head is for the cars turning its way, the main head of that approach for the others
                    var turn = TurnAfter(start + d);
                    if (light.Arrow != CarTrafficLight.TurnArrow.None ? turn != light.Arrow : turn != CarTrafficLight.TurnArrow.None && HasArrow(light, turn)) continue;
                    float stopGap = Mathf.Max(0.1f, along - VehicleLength / 2f - 2f);
                    if (light.State == LightState.YELLOW && Speed * Speed / (2f * 3f) > stopGap)
                        continue; // too close to stop comfortably: drive on
                    what = $"{light.State} light at {along:F0} m";
                    return Leader.Stationary(stopGap);
                }
            }
        }
        return Leader.None;
    }

    readonly List<Vector3> _ahead = new List<Vector3>();
    float _stoppedSince;
    static float HalfWidth(Transform v) => v.TryGetComponent<TrafficCar>(out var tc) ? (tc.LaneHalfWidth < 1f ? 0.4f : 0.9f)
        : v.GetComponent<TwoWheelerController>() != null ? 0.4f : 0.9f;
    static int _ranks;
    /// <summary>-noYield: no yielding to crossing traffic (for comparisons); -debugTraffic: logs overlaps and yielding every 10 s.</summary>
    public static readonly bool NoYield = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-noYield") >= 0;
    static readonly bool DebugTraffic = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-debugTraffic") >= 0;
    static int _overlapFrames, _yieldFrames, _frames; static float _nextReport = 10f;
    static void DebugCount()
    {
        _frames++;
        for (int i = 0; i < _cars.Count; i++)
        {
            var a = _cars[i]; if (a == null) continue;
            if (a.TryGetComponent<TrafficCar>(out var ta) && ta.LeaderDescription != null && ta.LeaderDescription.StartsWith("yielding")) _yieldFrames++;
            for (int j = i + 1; j < _cars.Count; j++)
            {
                var b = _cars[j]; if (b == null) continue;
                var d = b.position - a.position; d.y = 0f;
                if (d.sqrMagnitude > 9f) continue;
                // overlapping footprints: closer than half their widths across and half their lengths along
                float along = Mathf.Abs(Vector3.Dot(d, a.forward)), across = Mathf.Abs(Vector3.Dot(d, a.right));
                bool bikes = a.name.Contains("cyclist") || b.name.Contains("cyclist") || a.GetComponent<TwoWheelerController>() != null || b.GetComponent<TwoWheelerController>() != null;
                // footprints: two cars closer than 3.2 m along and 1.4 m across; with a bicycle 1.6 m along and 0.8 m across
                if (bikes ? along < 1.6f && across < 0.8f : along < 3.2f && across < 1.4f)
                {
                    _overlapFrames++;
                    if (_overlapFrames <= 10) Debug.Log($"TrafficDebug: {a.name} and {b.name} overlap at {a.position}, facing {Vector3.Dot(a.forward, b.forward):F2}, {(a.TryGetComponent<TrafficCar>(out var x1) ? x1.LeaderDescription : "-")} / {(b.TryGetComponent<TrafficCar>(out var x2) ? x2.LeaderDescription : "-")}");
                }
            }
        }
        if (Time.time >= _nextReport)
        {
            _nextReport += 10f;
            Debug.Log($"TrafficDebug: t={Time.time:F0} s, {_frames} frames, car-overlap frames {_overlapFrames}, yielding car-frames {_yieldFrames}");
            int n = 0;
            foreach (var c in _cars)
                if (c != null && c.TryGetComponent<TrafficCar>(out var tc) && tc._stoppedSince > 0f && Time.time - tc._stoppedSince > 20f && n++ < 8)
                    Debug.Log($"TrafficDebug: stopped {Time.time - tc._stoppedSince:F0} s: {c.name} at {c.position}: {tc.LeaderDescription}");
        }
    }
    readonly int _rank = ++_ranks;

    /// <summary>
    /// Crossing, turning and merging traffic: the distance along the own path to the first point that a nearby car
    /// heading another way will reach before this car (its next 3 s, straight on at its speed), or is on now; at about
    /// the same time the car with the lower id goes first. Followed cars ahead (same direction) are FindLeader's.
    /// </summary>
    float Conflict(float start, float offset, out string what)
    {
        what = null;
        float best = float.MaxValue;
        var me = transform.position;
        _ahead.Clear();
        for (float d = 2f; d <= 30f; d += 2f) _ahead.Add(_circuit.GetRoutePoint(start + d).position);
        foreach (var c in _cars)
        {
            if (c == transform || Flat2(c.position - me) > 40f * 40f) continue;
            float facing = Vector3.Dot(c.forward, transform.forward);
            if (facing > 0.3f) continue;
            // oncoming traffic counts only when it is in this lane (a head-on); crossing traffic from 0.6 m beside it
            float reach = facing < -0.7f ? LaneHalfWidth * LaneHalfWidth : (LaneHalfWidth + 0.6f) * (LaneHalfWidth + 0.6f);
            var tc = c.GetComponent<TrafficCar>();
            float vc = tc != null ? tc.Speed : SpeedOf(c);
            var fwd = Flat(c.forward).normalized;
            // (a car stopping at its red light or behind its leader goes no further than that)
            float further = tc != null ? tc.LeaderGap + 1f : float.PositiveInfinity;
            for (float tt = 0f; tt <= 3f; tt += 0.5f)
            {
                var q = c.position + fwd * Mathf.Min(vc * tt, further);
                for (int k = 0; k < _ahead.Count; k++)
                {
                    if (Flat2(q - _ahead[k]) > reach) continue;
                    float along = (k + 1) * 2f + offset;
                    if (along >= best) break;
                    float mine = along / Mathf.Max(Speed, 0.5f);
                    bool slowBoth = vc < 1f && Speed < 1f;
                    // (a tie: the earlier spawned car goes first; a participant's vehicle before any traffic car)
                    int rank = tc != null ? tc._rank : int.MinValue;
                    bool theyFirst = slowBoth ? rank < _rank : tt < mine - 0.4f || Mathf.Abs(tt - mine) <= 0.4f && rank < _rank;
                    if (theyFirst) { best = along; what = $"yielding to {c.name} at {along:F0} m"; }
                    break;
                }
            }
        }
        return best;
    }

    readonly Dictionary<Transform, (Vector3 pos, float time, float speed)> _speedCache = new Dictionary<Transform, (Vector3, float, float)>();

    // speed of a car without TrafficCar (player car, AV, SUMO car), from its position change
    float SpeedOf(Transform c)
    {
        if (c.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
            return Mathf.Max(0f, Vector3.Dot(rb.linearVelocity, c.forward));
        float now = Time.time;
        if (_speedCache.TryGetValue(c, out var prev) && now - prev.time > 0.05f)
        {
            float s = Mathf.Max(0f, Vector3.Dot(c.position - prev.pos, c.forward) / (now - prev.time));
            _speedCache[c] = (c.position, now, s);
            return s;
        }
        if (!_speedCache.ContainsKey(c)) _speedCache[c] = (c.position, now, 0f);
        return _speedCache[c].speed;
    }

    // A signal head for this car faces against the driving direction (its axis along the road).
    bool FacesTraffic(Transform light) => Vector3.Dot(LightFacing(light), transform.forward) < -0.7f;

    /// <summary>Which way the own path turns just after <paramref name="dist"/> (more than 35° within 30 m).</summary>
    CarTrafficLight.TurnArrow TurnAfter(float dist)
    {
        var a = Flat(_circuit.GetRoutePoint(dist).direction); var b = Flat(_circuit.GetRoutePoint(dist + 30f).direction);
        float angle = Vector3.SignedAngle(a, b, Vector3.up);   // positive: to the right
        return angle < -35f ? CarTrafficLight.TurnArrow.Left : angle > 35f ? CarTrafficLight.TurnArrow.Right : CarTrafficLight.TurnArrow.None;
    }

    /// <summary>Whether the approach of a main head also has a turn-arrow head for this turn.</summary>
    static bool HasArrow(CarTrafficLight main, CarTrafficLight.TurnArrow turn)
    {
        foreach (var l in _lights)
            if (l != null && l.Arrow == turn && (l.transform.position - main.transform.position).sqrMagnitude < 8f * 8f && Vector3.Dot(LightFacing(l.transform), LightFacing(main.transform)) > 0.9f) return true;
        return false;
    }

    /// <summary>Direction the lights of a signal head shine to (towards the traffic it controls).</summary>
    public static Vector3 LightFacing(Transform light) => light.right; // the signal heads in this project shine along their local x axis

    static float Flat2(Vector3 v) => v.x * v.x + v.z * v.z;
}
