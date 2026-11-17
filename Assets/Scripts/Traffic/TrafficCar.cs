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
    /// <summary>On roads over 55 km/h (rural roads, motorways): the speed limit times this (a driver's own factor);
    /// below: DesiredSpeed, at most the limit. 0: always DesiredSpeed (a fixed speed was asked for).</summary>
    public float SpeedLimitFactor = 1f;
    float _limitDesired = -1f, _nextLimit;
    int _endsRoute;
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
        _rb = GetComponent<Rigidbody>();
        Speed = 0f;
    }

    Rigidbody _rb;
    Collider[] _own;
    float _heldSince = -1f;
    /// <summary>Static obstacles (poles, trees, wall corners) this car was let through after being held by them.</summary>
    public static int Freed;

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
        if (DebugTraffic) DebugCount();

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
        Model.DesiredSpeed = Mathf.Min(LimitDesired(), CurveSpeed());
        var leader = FindLeader(out LeaderDescription);
        LeaderGap = leader.Present ? leader.Gap : float.PositiveInfinity;
        if (Speed > 0.3f) _stoppedSince = 0f; else if (_stoppedSince == 0f) _stoppedSince = Time.time;
        Acceleration = Model.Acceleration(Speed, leader, dt);
        Speed = Mathf.Max(0f, Speed + Acceleration * dt);
        // the model's speed stays close to the body's: a car held by something (a pole, a kerb, another car) does not
        // go on believing it drives on, and the cars behind it, which follow its speed, stop behind it
        float body = _rb != null && !_rb.isKinematic ? Mathf.Max(0f, Vector3.Dot(_rb.linearVelocity, transform.forward)) : Speed;
        Speed = Mathf.Min(Speed, body + 1.5f);
        // held: nothing ahead to stop for, wanting to drive, but not moving
        bool held = body < 0.5f && Model.DesiredSpeed > 2f && LeaderGap > 6f;
        if (!held) _heldSince = -1f; else if (_heldSince < 0f) _heldSince = Time.time;
        if (DebugTraffic && Time.time >= _nextHistory)
        {
            _nextHistory = Time.time + 0.25f;
            var rbv = TryGetComponent<Rigidbody>(out var rb0) ? rb0.linearVelocity.magnitude : -1f;
            var line = $"t={Time.time:F2} prog={_tracker.ProgressDistance:F1} v={Speed:F1} rb={rbv:F1} pos=({transform.position.x:F1},{transform.position.z:F1}) {LeaderDescription}";
            _history.Enqueue(line);
            while (_history.Count > 12) _history.Dequeue();
            // the participant's autopilot: every 5 s
            if (Time.time >= _nextParticipantLog && GetComponent<AutomatedDriving>() != null)
            {
                _nextParticipantLog = Time.time + 5f;
                Debug.Log($"TrafficDebug: autopilot {line} limit {LimitDesired() * 3.6f:F0} km/h");
            }
        }
        _car.ExternalSpeed = Speed;
        _car.ExternalAcceleration = Acceleration;
    }

    /// <summary>The desired speed from the speed limits here and ahead (slowing down comfortably, 1.5 m/s², before a
    /// lower limit), every half second.</summary>
    float LimitDesired()
    {
        if (SpeedLimitFactor <= 0f || !RoadSpeedLimits.Any || _circuit == null) return DesiredSpeed;
        if (Time.time < _nextLimit && _limitDesired > 0f) return _limitDesired;
        _nextLimit = Time.time + 0.5f;
        float Want(float kmh) => kmh <= 0f ? DesiredSpeed : kmh > 55f ? kmh / 3.6f * SpeedLimitFactor : Mathf.Min(DesiredSpeed, kmh / 3.6f);
        float start = _tracker.ProgressDistance;
        float v = Want(RoadSpeedLimits.At(_circuit.GetRoutePoint(start).position));
        for (float d = 25f; d <= 150f; d += 25f)
        {
            float ahead = Want(RoadSpeedLimits.At(_circuit.GetRoutePoint(start + d).position));
            v = Mathf.Min(v, Mathf.Sqrt(ahead * ahead + 2f * 1.5f * d));
        }
        return _limitDesired = v;
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
        // the end of the route (a city map's routes end at its edge; the car stops there until it is started again): an
        // obstacle like the others, found in order of distance in the walk below
        if (_endsRoute == 0) _endsRoute = GetComponent<MapRouteRecycler>() != null ? 1 : -1;
        float routeEnd = _endsRoute > 0 ? _circuit.OpenLength - Mathf.Repeat(start, _circuit.Length) : float.MaxValue;
        var me = transform.position;
        var myFwd = transform.forward;
        // distance from the car to its progress point (the tracker lags a little behind)
        float offset = Vector3.Dot(_circuit.GetRoutePoint(start).position - me, myFwd);
        float lw2 = LaneHalfWidth * LaneHalfWidth;
        float maxRange2 = (LookAhead + 10f) * (LookAhead + 10f);

        _nearCars.Clear();
        bool anyCarClose = false;   // (any direction within 40 m: crossing and merging traffic, Conflict below)
        for (int i = 0; i < _cars.Count; i++)
        {
            var c = _cars[i];
            if (c.t == transform) continue;
            float d2 = Flat2(c.pos - me);
            if (d2 <= 40f * 40f) anyCarClose = true;
            // (not a car on a bridge above or a road below)
            if (d2 <= maxRange2 && Vector3.Dot(c.fwd, myFwd) > 0.3f && Mathf.Abs(c.pos.y - me.y) < 3.5f) _nearCars.Add(c);
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

        if (routeEnd < LookAhead) anyCarClose = true;   // (not the shortcut below: the route ends ahead)
        if (!anyCarClose && _nearCars.Count == 0 && _nearAnimals.Count == 0 && _nearPeds.Count == 0 && _nearLights.Count == 0)
            return Leader.None;

        const float step = 2f;
        // side by side with another vehicle heading the same way (two routes merging into one lane or cycle path): the
        // earlier spawned one goes on, this one holds back
        foreach (var c in _nearCars)
        {
            var dv = c.pos - me; dv.y = 0f;
            if (dv.sqrMagnitude > VehicleLength * VehicleLength) continue;
            float al = Vector3.Dot(dv, transform.forward), ac = Mathf.Abs(Vector3.Dot(dv, transform.right));
            int rk = c.tc != null ? c.tc._rank : int.MinValue;
            // the one further back waits (the other ahead by up to three quarters of a vehicle length); level with it
            // (within 0.3 m): the earlier spawned goes on
            bool beside = Mathf.Abs(al) <= 0.3f && rk < _rank, justAhead = al > 0.3f && al < VehicleLength * 0.75f;
            // (overlapping: closer across than their half widths together; a car is about 1.8 m wide, a bicycle 0.6 m)
            float halfWidths = HalfWidth(transform) + HalfWidth(c.t);
            if (ac < halfWidths && (beside || justAhead))
            {
                what = $"making room for {c.name}";
                return Leader.Stationary(0.1f);
            }
        }
        // the nearest vehicle ahead on the same route, by distance along the route (each corrected for where the car
        // really is against its progress point): the 2 m path samples below miss a car standing off the route's line or
        // in a tight bend (a U-turn), where its heading differs from this car's
        float sameAlong = float.MaxValue; TrafficCar sameCar = null;
        float myS = start - offset;
        foreach (var c in _cars)
        {
            var o = c.tc;
            if (o == null || o == this || o._circuit != _circuit || o._tracker == null) continue;
            if (Vector3.Dot(c.fwd, myFwd) < -0.3f) continue;   // (a route that comes back along the same street: oncoming, Conflict's)
            if (Flat2(c.pos - me) > (LookAhead + 10f) * (LookAhead + 10f)) continue;
            float os = o._tracker.ProgressDistance;
            float oS = os - Vector3.Dot(_circuit.GetRoutePoint(os).position - c.pos, c.fwd);
            float diff = oS - myS;
            float len = _circuit.Length;
            if (len > 0f) { if (diff < -len / 2f) diff += len; else if (diff > len / 2f) diff -= len; }
            // (in a bend the cars cut the corner: no further than straight across)
            if (diff > 0f) diff = Mathf.Min(diff, Mathf.Sqrt(Flat2(c.pos - me)));
            if (diff > 0f && diff <= LookAhead && diff < sameAlong) { sameAlong = diff; sameCar = o; }
        }
        string conflictWhat = null;
        float conflict = NoYield ? float.MaxValue : Conflict(start, offset, out conflictWhat);
        // (closer than the first sample, which is 2 m past the progress point: now; else when the samples pass it)
        if (sameCar != null && sameAlong <= conflict && sameAlong < step + offset)
        {
            what = $"car {sameCar.name} at {sameAlong:F0} m (same route)";
            return Leader.Vehicle(Mathf.Max(0.1f, sameAlong - (VehicleLength + sameCar.VehicleLength) / 2f), sameCar.Speed);
        }
        for (float d = step; d <= LookAhead; d += step)
        {
            var p = _circuit.GetRoutePoint(start + d).position;
            float along = d + offset;
            if (along < 1f) continue;
            if (d >= routeEnd - 4f)
            {
                what = $"end of the route at {routeEnd:F0} m";
                return Leader.Stationary(Mathf.Max(0.1f, routeEnd - 4f + offset));
            }
            if (along >= conflict)
            {
                what = conflictWhat;
                return Leader.Stationary(Mathf.Max(0.1f, conflict - VehicleLength / 2f - 2f));
            }
            if (along >= sameAlong && sameCar != null)
            {
                what = $"car {sameCar.name} at {sameAlong:F0} m (same route)";
                return Leader.Vehicle(Mathf.Max(0.1f, sameAlong - (VehicleLength + sameCar.VehicleLength) / 2f), sameCar.Speed);
            }

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

    readonly List<Vector3> _ahead = new List<Vector3>();
    float _stoppedSince;
    static float HalfWidth(Transform v) => v.TryGetComponent<TrafficCar>(out var tc) ? (tc.LaneHalfWidth < 1f ? 0.4f : 0.9f)
        : v.GetComponent<TwoWheelerController>() != null ? 0.4f : 0.9f;
    static int _ranks;
    /// <summary>-noYield: no yielding to crossing traffic (for comparisons); -debugTraffic: logs overlaps and yielding every 10 s.</summary>
    public static readonly bool NoYield = System.Array.IndexOf(CommandLine.Args, "-noYield") >= 0;
    float _nextParticipantLog;
    static readonly bool DebugTraffic = System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0;
    static int _overlapFrames, _yieldFrames, _frames; static float _nextReport = 10f;
    static void DebugCount()
    {
        _frames++;
        for (int i = 0; i < _cars.Count; i++)
        {
            var a = _cars[i].t; if (a == null) continue;
            if (a.TryGetComponent<TrafficCar>(out var ta) && ta.LeaderDescription != null && ta.LeaderDescription.StartsWith("yielding")) _yieldFrames++;
            for (int j = i + 1; j < _cars.Count; j++)
            {
                var b = _cars[j].t; if (b == null) continue;
                var d = b.position - a.position;
                if (Mathf.Abs(d.y) > 3.5f) continue;   // (one on a bridge over the other)
                d.y = 0f;
                if (d.sqrMagnitude > 9f) continue;
                // overlapping footprints: closer than half their widths across and half their lengths along
                float along = Mathf.Abs(Vector3.Dot(d, a.forward)), across = Mathf.Abs(Vector3.Dot(d, a.right));
                bool bikes = a.name.Contains("cyclist") || b.name.Contains("cyclist") || a.GetComponent<TwoWheelerController>() != null || b.GetComponent<TwoWheelerController>() != null;
                // footprints: two cars closer than 3.2 m along and 1.4 m across; with a bicycle 1.6 m along and 0.8 m across
                if (bikes ? along < 1.6f && across < 0.8f : along < 3.2f && across < 1.4f)
                {
                    _overlapFrames++;
                    if (_reportedPairs.Add(a.name + "|" + b.name) && _reportedPairs.Count <= 12)
                        Debug.Log($"TrafficDebug: first overlap of {a.name} and {b.name}:\n  {a.name}: {string.Join("\n  " + a.name + ": ", a.TryGetComponent<TrafficCar>(out var h1) ? h1._history : new Queue<string>())}\n  {b.name}: {string.Join("\n  " + b.name + ": ", b.TryGetComponent<TrafficCar>(out var h2) ? h2._history : new Queue<string>())}");
                    if (_overlapFrames <= 10 || _overlapFrames % 50 == 0) Debug.Log($"TrafficDebug: {a.name} and {b.name} overlap at {a.position}, facing {Vector3.Dot(a.forward, b.forward):F2}, {Describe(a)} / {Describe(b)}");
                }
            }
        }
        if (Time.time >= _nextReport)
        {
            _nextReport += 10f;
            Debug.Log($"TrafficDebug: t={Time.time:F0} s, {_frames} frames, car-overlap frames {_overlapFrames}, yielding car-frames {_yieldFrames}, freed from obstacles {Freed}");
            int n = 0;
            foreach (var cc in _cars)
                if (cc.t is Transform c && c != null && c.TryGetComponent<TrafficCar>(out var tc) && tc._stoppedSince > 0f && Time.time - tc._stoppedSince > 20f && n++ < 8)
                    Debug.Log($"TrafficDebug: stopped {Time.time - tc._stoppedSince:F0} s: {c.name} at {c.position}: {tc.LeaderDescription}");
        }
    }
    readonly int _rank = ++_ranks;
    float _nextHistory;
    readonly Queue<string> _history = new Queue<string>();
    static readonly HashSet<string> _reportedPairs = new HashSet<string>();

    static readonly HashSet<string> _reportedHits = new HashSet<string>();
    void OnCollisionStay(Collision c)
    {
        // held for 1.5 s by something fixed that is not the ground (a pole, a tree, the corner of a building where the
        // route cuts a tight bend): drive on through it rather than block the street for every car behind
        if (_heldSince > 0f && Time.time - _heldSince > 1.5f && c.collider != null && c.collider.attachedRigidbody == null
            && c.contactCount > 0 && Mathf.Abs(c.GetContact(0).normal.y) < 0.6f && !c.collider.name.StartsWith("Quay"))   // (never into a canal)
        {
            if (_own == null) _own = GetComponentsInChildren<Collider>();
            foreach (var o in _own) if (o != null) Physics.IgnoreCollision(o, c.collider, true);
            Freed++;
            if (DebugTraffic) Debug.Log($"TrafficDebug: {name} freed from {c.collider.name} at {transform.position}");
            _heldSince = Time.time;
        }
        if (!DebugTraffic || Speed < 2f || c.collider == null) return;
        float rbv = TryGetComponent<Rigidbody>(out var rb) ? rb.linearVelocity.magnitude : -1f;
        if (rbv > Speed * 0.5f) return;
        var t = c.collider.transform; var path = t.name; for (var q = t.parent; q != null && path.Length < 120; q = q.parent) path = q.name + "/" + path;
        if (_reportedHits.Add(name + "|" + path) && _reportedHits.Count <= 40)
            Debug.Log($"TrafficDebug: {name} held by {path} (model {Speed:F1} m/s, body {rbv:F1} m/s) at {transform.position}");
    }

    static string Describe(Transform t)
    {
        if (!t.TryGetComponent<TrafficCar>(out var tc)) return t.GetComponent<TwoWheelerController>() != null ? "rider" : "participant";
        return $"[{tc.LeaderDescription}; v {tc.Speed:F1} m/s; at {(tc._tracker != null ? tc._tracker.ProgressDistance : -1f):F0} of {(tc._circuit != null ? tc._circuit.Length : 0f):F0} m on {(tc._circuit != null ? tc._circuit.name : "?")}]";
    }

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
        foreach (var cc in _cars)
        {
            var c = cc.t;
            if (c == null || c == transform || Flat2(cc.pos - me) > 40f * 40f || Mathf.Abs(cc.pos.y - me.y) > 3.5f) continue;
            // (the same route, heading this way: followed, FindLeader; coming back towards this car: oncoming traffic)
            if (cc.tc != null && cc.tc._circuit == _circuit && Vector3.Dot(cc.fwd, transform.forward) >= -0.3f) continue;
            float facing = Vector3.Dot(cc.fwd, transform.forward);
            if (facing > 0.3f) continue;
            // oncoming traffic counts only when it is in this lane (a head-on); crossing traffic from 0.6 m beside it
            float reach = facing < -0.7f ? LaneHalfWidth * LaneHalfWidth : (LaneHalfWidth + 0.6f) * (LaneHalfWidth + 0.6f);
            var tc = cc.tc;
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
                    // committed: its front is at the conflict point already, or it could not stop before it braking at
                    // 4 m/s²; it clears the junction (the other car, which has it on its path, gives way) instead of
                    // stopping in the middle of it
                    // (not for a vehicle standing there now: that is an obstacle, not traffic that will have passed)
                    bool standing = tt == 0f && vc < 1f;
                    if (!standing && along - VehicleLength / 2f < (Speed > 0.5f ? 1f + Speed * Speed / 8f : 0.5f)) break;
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
