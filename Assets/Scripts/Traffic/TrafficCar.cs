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
    struct CachedCar { public Transform t; public Vector3 pos, fwd; public TrafficCar tc; public string name; }
    struct CachedPed { public Vector3 pos; public string name; }
    struct CachedAnimal { public Vector3 pos; public string species; }
    struct CachedLight
    {
        public CarTrafficLight light;
        public Vector3 pos, facing;
        public LightState state;
        public CarTrafficLight.TurnArrow arrow;
        public bool hasLeftArrow, hasRightArrow;
    }

    static readonly List<PlayerAvatar> _avatarObjects = new List<PlayerAvatar>();
    static readonly List<CarTrafficLight> _lights = new List<CarTrafficLight>();
    static readonly List<CachedCar> _cars = new List<CachedCar>();
    static readonly List<CachedPed> _pedestrians = new List<CachedPed>();
    static readonly List<CachedAnimal> _animals = new List<CachedAnimal>();
    static readonly List<CachedLight> _activeLights = new List<CachedLight>();

    static readonly List<CachedCar> _nearCars = new List<CachedCar>();
    static readonly List<CachedPed> _nearPeds = new List<CachedPed>();
    static readonly List<CachedAnimal> _nearAnimals = new List<CachedAnimal>();
    static readonly List<CachedLight> _nearLights = new List<CachedLight>();

    static int _cacheFrame = -1;
    static float _cacheFixedTime = -1f;
    static float _nextAvatarScan;
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
        float fixedNow = Time.fixedTime;
        if (_cacheFrame == Time.frameCount && _cacheFixedTime == fixedNow) return;
        _cacheFrame = Time.frameCount;
        _cacheFixedTime = fixedNow;

        float now = Time.time;
        if (now >= _nextAvatarScan || _avatarObjects.Count == 0)
        {
            _nextAvatarScan = now + 0.25f;
            _avatarObjects.Clear();
            _avatarObjects.AddRange(FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None));
        }
        if (now >= _nextLightScan)
        {
            _nextLightScan = now + 2f;
            _lights.Clear();
            _lights.AddRange(FindObjectsByType<CarTrafficLight>(FindObjectsSortMode.None));
        }

        _cars.Clear();
        _pedestrians.Clear();
        for (int i = 0; i < _avatarObjects.Count; i++)
        {
            var a = _avatarObjects[i];
            if (a == null || !a.gameObject.activeInHierarchy || a.Type == AvatarType.Drone) continue;
            var tr = a.transform;
            if (a.Type == AvatarType.Driver)
            {
                a.TryGetComponent<TrafficCar>(out var tc);
                _cars.Add(new CachedCar { t = tr, pos = tr.position, fwd = tr.forward, tc = tc, name = a.name });
            }
            else
            {
                _pedestrians.Add(new CachedPed { pos = tr.position, name = a.name });
            }
        }

        _animals.Clear();
        var allAnimals = AnimalAgent.All;
        for (int i = 0; i < allAnimals.Count; i++)
        {
            var animal = allAnimals[i];
            if (animal == null || animal.Current == AnimalAgent.State.Fly) continue;
            _animals.Add(new CachedAnimal { pos = animal.transform.position, species = animal.Species.ToString() });
        }

        _activeLights.Clear();
        for (int i = 0; i < _lights.Count; i++)
        {
            var light = _lights[i];
            if (light == null) continue;
            var st = light.State;
            if (st != LightState.RED && st != LightState.YELLOW && st != LightState.RED_AND_YELLOW) continue;
            var lt = light.transform;
            var lpos = lt.position;
            var lfacing = LightFacing(lt);
            bool hasLeft = false, hasRight = false;
            if (light.Arrow == CarTrafficLight.TurnArrow.None)
            {
                for (int j = 0; j < _lights.Count; j++)
                {
                    var o = _lights[j];
                    if (o == null || o.Arrow == CarTrafficLight.TurnArrow.None) continue;
                    if ((o.transform.position - lpos).sqrMagnitude < 8f * 8f && Vector3.Dot(LightFacing(o.transform), lfacing) > 0.9f)
                    {
                        if (o.Arrow == CarTrafficLight.TurnArrow.Left) hasLeft = true;
                        else if (o.Arrow == CarTrafficLight.TurnArrow.Right) hasRight = true;
                    }
                }
            }
            _activeLights.Add(new CachedLight
            {
                light = light,
                pos = lpos,
                facing = lfacing,
                state = st,
                arrow = light.Arrow,
                hasLeftArrow = hasLeft,
                hasRightArrow = hasRight,
            });
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
        var myFwd = transform.forward;
        // distance from the car to its progress point (the tracker lags a little behind)
        float offset = Vector3.Dot(_circuit.GetRoutePoint(start).position - me, myFwd);
        float lw2 = LaneHalfWidth * LaneHalfWidth;
        float maxRange2 = (LookAhead + 10f) * (LookAhead + 10f);

        _nearCars.Clear();
        for (int i = 0; i < _cars.Count; i++)
        {
            var c = _cars[i];
            if (c.t != transform && Flat2(c.pos - me) <= maxRange2 && Vector3.Dot(c.fwd, myFwd) > 0.3f)
                _nearCars.Add(c);
        }

        _nearAnimals.Clear();
        _nearPeds.Clear();
        if (YieldToPedestrians)
        {
            for (int i = 0; i < _animals.Count; i++)
                if (Flat2(_animals[i].pos - me) <= maxRange2) _nearAnimals.Add(_animals[i]);
            for (int i = 0; i < _pedestrians.Count; i++)
                if (Flat2(_pedestrians[i].pos - me) <= maxRange2) _nearPeds.Add(_pedestrians[i]);
        }

        _nearLights.Clear();
        if (ObeyTrafficLights)
        {
            for (int i = 0; i < _activeLights.Count; i++)
            {
                var l = _activeLights[i];
                if (Flat2(l.pos - me) <= maxRange2 && Vector3.Dot(l.facing, myFwd) < -0.7f)
                    _nearLights.Add(l);
            }
        }

        if (_nearCars.Count == 0 && _nearAnimals.Count == 0 && _nearPeds.Count == 0 && _nearLights.Count == 0)
            return Leader.None;

        const float step = 2f;
        for (float d = step; d <= LookAhead; d += step)
        {
            var p = _circuit.GetRoutePoint(start + d).position;
            float along = d + offset;
            if (along < 1f) continue;

            for (int i = 0; i < _nearCars.Count; i++)
            {
                var c = _nearCars[i];
                if (Flat2(c.pos - p) < lw2)
                {
                    float leaderSpeed = c.tc != null ? c.tc.Speed : SpeedOf(c.t);
                    what = $"car {c.name} at {along:F0} m";
                    return Leader.Vehicle(Mathf.Max(0.1f, along - VehicleLength), leaderSpeed);
                }
            }
            if (YieldToPedestrians)
            {
                for (int i = 0; i < _nearAnimals.Count; i++)
                {
                    var animal = _nearAnimals[i];
                    if (Flat2(animal.pos - p) < lw2 * 1.2f && Mathf.Abs(animal.pos.y - p.y) < 1.5f)
                    {
                        what = $"{animal.species} at {along:F0} m";
                        return Leader.Stationary(Mathf.Max(0.1f, along - VehicleLength / 2f - 1.5f));
                    }
                }
                for (int i = 0; i < _nearPeds.Count; i++)
                {
                    var ped = _nearPeds[i];
                    if (Flat2(ped.pos - p) < lw2 * 1.2f && Mathf.Abs(ped.pos.y - p.y) < 1.5f)
                    {
                        what = $"pedestrian {ped.name} at {along:F0} m";
                        return Leader.Stationary(Mathf.Max(0.1f, along - VehicleLength / 2f - 1.5f));
                    }
                }
            }
            if (ObeyTrafficLights)
            {
                for (int i = 0; i < _nearLights.Count; i++)
                {
                    var l = _nearLights[i];
                    // lights stand beside the road, a few metres past the stop line, facing the traffic
                    if (Flat2(l.pos - p) > 6f * 6f) continue;
                    // turn arrows: an arrow head is for the cars turning its way, the main head of that approach for the others
                    var turn = TurnAfter(start + d);
                    bool hasTurnArrow = (turn == CarTrafficLight.TurnArrow.Left && l.hasLeftArrow) || (turn == CarTrafficLight.TurnArrow.Right && l.hasRightArrow);
                    if (l.arrow != CarTrafficLight.TurnArrow.None ? turn != l.arrow : turn != CarTrafficLight.TurnArrow.None && hasTurnArrow) continue;
                    float stopGap = Mathf.Max(0.1f, along - VehicleLength / 2f - 2f);
                    if (l.state == LightState.YELLOW && Speed * Speed / (2f * 3f) > stopGap)
                        continue; // too close to stop comfortably: drive on
                    what = $"{l.state} light at {along:F0} m";
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
