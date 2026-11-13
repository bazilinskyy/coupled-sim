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
            if (!a.gameObject.activeInHierarchy) continue;
            (a.Type == AvatarType.Driver ? _cars : _pedestrians).Add(a.transform);
        }
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
        for (float d = step; d <= LookAhead; d += step)
        {
            var p = _circuit.GetRoutePoint(start + d).position;
            float along = d + offset;
            if (along < 1f) continue;

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

    /// <summary>Direction the lights of a signal head shine to (towards the traffic it controls).</summary>
    public static Vector3 LightFacing(Transform light) => light.right; // the signal heads in this project shine along their local x axis

    static float Flat2(Vector3 v) => v.x * v.x + v.z * v.z;
}
