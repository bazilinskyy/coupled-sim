using System.Collections.Generic;
using System.Linq;
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
    public float VehicleWidth = 1.8f;

    /// <summary>The vehicle's length and width from its body (the box collider on its root that VehicleBuilder fits to
    /// the model): a van, a truck or a bus keeps its gap to the vehicle ahead from its own front bumper, and turns in its
    /// own wider circle (AICar.SteerPursuit), not a 4.5 m car's.</summary>
    public void FitToBody()
    {
        if (!TryGetComponent<BoxCollider>(out var box) || box.isTrigger) return;
        var s = transform.lossyScale;
        float length = box.size.z * Mathf.Abs(s.z), width = box.size.x * Mathf.Abs(s.x);
        if (length < 2.5f || length > 25f || width < 1.2f || width > 3.2f) return;   // (not a car's body)
        VehicleLength = length; VehicleWidth = width;
    }
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
            float off = LaneOffset();
            if (Mathf.Abs(off) > Mathf.Abs(_maxOff)) { _maxOff = off; _maxOffAt = transform.position; }
            var line = $"t={Time.time:F2} prog={_tracker.ProgressDistance:F1} v={Speed:F1} rb={rbv:F1} pos=({transform.position.x:F1},{transform.position.z:F1}) off={off:F2} {LeaderDescription}";
            _history.Enqueue(line);
            while (_history.Count > 12) _history.Dequeue();
            // the participant's autopilot: every 5 s
            if (Time.time >= _nextParticipantLog && GetComponent<AutomatedDriving>() != null)
            {
                _nextParticipantLog = Time.time + 5f;
                Debug.Log($"TrafficDebug: autopilot {line} limit {LimitDesired() * 3.6f:F0} km/h");
            }
            if (Time.time >= _nextSignalLog && GetComponent<AutomatedDriving>() != null)
            {
                _nextSignalLog = Time.time + 1f;
                LogSignalsInView();
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
        var prevP = _circuit.GetRoutePoint(start).position;
        for (float d = step; d <= LookAhead; d += step)
        {
            var p = _circuit.GetRoutePoint(start + d).position;
            var segFrom = prevP; prevP = p;
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
                // (within the lane anywhere along the 2 m since the last sample, not only near the samples: a vehicle up to
                // a metre between two of them and 0.8 m beside the line, as one overtaken on a parallel route, was missed)
                if (SegmentDist2(c.pos, segFrom, p, out float u) < lw2)
                {
                    float at = along - step * (1f - u);   // (where along the segment it is)
                    if (at < 1f) continue;   // (beside this vehicle: the side-by-side rule above)
                    float leaderSpeed = c.tc != null ? c.tc.Speed : SpeedOf(c.t);
                    what = $"car {c.name} at {at:F0} m";
                    // (centre to centre: half of each one's length; a participant's car without TrafficCar taken as 4.5 m)
                    return Leader.Vehicle(Mathf.Max(0.1f, at - (VehicleLength + (c.tc != null ? c.tc.VehicleLength : 4.5f)) / 2f), leaderSpeed);
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
                    // the head hangs over the approach at its stop line, facing the traffic: taken at the sample level with
                    // it (within a metre along the route), where its distance along the route is known (the first sample
                    // within reach of it would be some 5 m short of the line, and cars stopped 8-10 m before it)
                    if (Flat2(l.pos - p) > 6f * 6f) continue;
                    float ahead = Vector3.Dot(Flat(l.pos - p), Flat(_circuit.GetRoutePoint(start + d).direction).normalized);
                    if (ahead > 1f || ahead <= -1f) continue;
                    // turn arrows: an arrow head is for the cars turning its way, the main head of that approach for the others
                    var turn = TurnAfter(start + d);
                    bool hasTurnArrow = (turn == CarTrafficLight.TurnArrow.Left && l.hasLeftArrow) || (turn == CarTrafficLight.TurnArrow.Right && l.hasRightArrow);
                    if (l.arrow != CarTrafficLight.TurnArrow.None ? turn != l.arrow : turn != CarTrafficLight.TurnArrow.None && hasTurnArrow) continue;
                    // the front of the car stops a metre before the line (the model keeps its minimum gap, 2 m, to the
                    // obstacle: the obstacle is put 1 m past the line)
                    float toLine = along + ahead - VehicleLength / 2f - 1f;
                    if (toLine < -0.5f) continue;   // over the line already: clear the junction
                    if (l.state == LightState.YELLOW && Speed * Speed / (2f * 3f) > toLine)
                        continue; // too close to stop comfortably: drive on
                    what = $"{l.state} light at {along + ahead:F0} m";
                    return Leader.Stationary(Mathf.Max(0.1f, toLine + 2f));
                }
            }
        }
        return Leader.None;
    }

    readonly List<Vector3> _ahead = new List<Vector3>();
    float _stoppedSince;
    static float HalfWidth(Transform v) => v.TryGetComponent<TrafficCar>(out var tc) ? (tc.LaneHalfWidth < 1f ? 0.4f : tc.VehicleWidth / 2f)
        : v.GetComponent<TwoWheelerController>() != null ? 0.4f : 0.9f;
    static int _ranks;
    /// <summary>-noYield: no yielding to crossing traffic (for comparisons); -debugTraffic: logs overlaps and yielding every 10 s.</summary>
    public static readonly bool NoYield = System.Array.IndexOf(CommandLine.Args, "-noYield") >= 0;
    float _nextParticipantLog, _nextSignalLog;

    /// <summary>-debugTraffic, the participant's autopilot: every second, the signal heads within 35 m ahead that shine
    /// towards it (car and pedestrian heads), with their state, so a head that faces the wrong way or shows the wrong
    /// phase can be found from a video's time.</summary>
    void LogSignalsInView()
    {
        var me = transform.position; var fwd = Flat(transform.forward).normalized;
        var sb = new System.Text.StringBuilder();
        void Add(Transform t, string kind, LightState st)
        {
            var d = Flat(t.position - me);
            if (d.sqrMagnitude > 35f * 35f || Vector3.Dot(d, fwd) < 2f) return;
            float facing = Vector3.Dot(Flat(LightFacing(t)).normalized, fwd);
            if (facing > -0.5f) return;   // (not shining towards the participant)
            sb.Append($"; {kind} {t.name} {st} at {Vector3.Dot(d, fwd):F0} m ahead, {Vector3.Dot(d, Vector3.Cross(Vector3.up, fwd)):F1} m right, facing {facing:F2}");
        }
        foreach (var l in _lights) if (l != null) Add(l.transform, l.Arrow != CarTrafficLight.TurnArrow.None ? "arrow" : "car", l.State);
        foreach (var l in FindObjectsByType<PedestrianTrafficLight>(FindObjectsSortMode.None)) if (l != null) Add(l.transform, "pedestrian", l.State);
        // oncoming vehicles within 30 m ahead: how far over they are, and the angle between their heading and their motion
        foreach (var c in _cars)
        {
            if (c.t == null || c.t == transform) continue;
            var d = Flat(c.pos - me);
            if (d.sqrMagnitude > 30f * 30f || Vector3.Dot(d, fwd) < 0f || Vector3.Dot(c.fwd, fwd) > -0.3f) continue;
            float slip = c.t.TryGetComponent<Rigidbody>(out var orb) && Flat(orb.linearVelocity).sqrMagnitude > 0.25f ? Vector3.Angle(Flat(c.fwd), Flat(orb.linearVelocity)) : 0f;
            sb.Append($"; oncoming {c.name} at {Vector3.Dot(d, fwd):F0} m, {Vector3.Dot(d, Vector3.Cross(Vector3.up, fwd)):F1} m right, heading {Vector3.SignedAngle(-fwd, Flat(c.fwd), Vector3.up):F0}°, slip {slip:F0}°{(c.tc != null ? " [" + c.tc.LeaderDescription + "]" : "")}");
        }
        Debug.Log($"TrafficDebug: participant t={Time.time:F1} at ({me.x:F1},{me.z:F1}) heading {Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg:F0} v={Speed:F1} off={LaneOffset():F2} {LeaderDescription}{sb}");
    }
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
            // how high the vehicles stand on the ground under them (a two-wheeler's wheels sunk into the road: below 0)
            var heights = new List<(string kind, float h)>();
            foreach (var cc in _cars)
            {
                if (cc.t == null || cc.tc == null) continue;
                var hits = Physics.RaycastAll(cc.pos + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
                float ground = float.NegativeInfinity;
                foreach (var h in hits) if (h.collider.attachedRigidbody == null && h.normal.y > 0.7f && h.point.y < cc.pos.y + 1f && h.point.y > ground) ground = h.point.y;
                if (ground > float.NegativeInfinity) heights.Add((cc.tc.LaneHalfWidth < 1f ? "two-wheelers" : "cars", cc.pos.y - ground));
            }
            foreach (var g in heights.GroupBy(x => x.kind))
                Debug.Log($"TrafficDebug: {g.Key} above the ground: {g.Count()}, from {g.Min(x => x.h):F2} to {g.Max(x => x.h):F2} m, mean {g.Average(x => x.h):F2} m");
            LogWheelContact();
            // the vehicles furthest beside their lanes in the last 10 s (a wide turn: over the line into the next lane)
            var wide = _cars.Where(x => x.tc != null && x.t != null).Select(x => x.tc).OrderByDescending(x => Mathf.Abs(x._maxOff)).Take(6).ToList();
            Debug.Log("TrafficDebug: furthest beside their lanes: " + string.Join("; ", wide.Select(x => $"{x.name} {x._maxOff:F2} m at ({x._maxOffAt.x:F0},{x._maxOffAt.z:F0})")));
            int over1 = _cars.Count(x => x.tc != null && Mathf.Abs(x.tc._maxOff) > 1f);
            Debug.Log($"TrafficDebug: vehicles more than 1 m beside their lanes in the last 10 s: {over1} of {_cars.Count(x => x.tc != null)}");
            foreach (var x in _cars) if (x.tc != null) x.tc._maxOff = 0f;
            int n = 0;
            foreach (var cc in _cars)
                if (cc.t is Transform c && c != null && c.TryGetComponent<TrafficCar>(out var tc) && tc._stoppedSince > 0f && Time.time - tc._stoppedSince > 20f && n++ < 8)
                    Debug.Log($"TrafficDebug: stopped {Time.time - tc._stoppedSince:F0} s: {c.name} at {c.position}: {tc.LeaderDescription}");
        }
    }
    float _maxOff; Vector3 _maxOffAt;
    /// <summary>-debugTraffic: how far the vehicle is beside its route's line (m, to the right positive), from the nearest
    /// point of the route within 15 m of its progress point.</summary>
    float LaneOffset()
    {
        var me = transform.position; float best = float.MaxValue, bd = 0f;
        for (float d = -15f; d <= 15f; d += 0.5f)
        {
            float e = Flat2(_circuit.GetRoutePosition(_tracker.ProgressDistance + d) - me);
            if (e < best) { best = e; bd = d; }
        }
        var rp = _circuit.GetRoutePoint(_tracker.ProgressDistance + bd);
        var right = Vector3.Cross(Vector3.up, Flat(rp.direction).normalized);
        return Vector3.Dot(Flat(me - rp.position), right);
    }

    /// <summary>-debugTraffic: per vehicle model, how far the bottoms of its wheel models are above the ground under them
    /// (below 0: sunk into the road), and where its wheel colliders hold it.</summary>
    static readonly Dictionary<string, (Vector3 hub, float radius)> _wheelShape = new Dictionary<string, (Vector3, float)>();
    static void LogWheelContact()
    {
        var rows = new List<(string model, float gap, float hubDiff, float rModel, float rCol, float hubLocal, float colTop)>();
        foreach (var cc in _cars)
        {
            if (cc.t == null || cc.tc == null || cc.tc.LaneHalfWidth < 1f) continue;
            var pa = cc.t.GetComponent<PlayerAvatar>();
            string model = pa != null && !string.IsNullOrEmpty(pa.PrefabName) ? pa.PrefabName : cc.t.name;
            if (cc.t.GetComponent<AutomatedDriving>() != null) model += " (participant)";
            foreach (var s in cc.t.GetComponentsInChildren<VehicleBehaviour.Suspension>(true))
            {
                if (s.wheelModel == null || !s.TryGetComponent<WheelCollider>(out var wc)) continue;
                // the wheel's centre and radius in the body, from its meshes' vertices (once per model and wheel: turning
                // about its axle does not change them)
                var key = model + "|" + s.name;
                if (!_wheelShape.TryGetValue(key, out var shape))
                {
                    Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
                    foreach (var mf in s.wheelModel.GetComponentsInChildren<MeshFilter>())
                    {
                        if (mf.sharedMesh == null) continue;
                        var m = cc.t.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                        foreach (var v in mf.sharedMesh.vertices) { var q = m.MultiplyPoint3x4(v); lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                    }
                    shape = _wheelShape[key] = hi.y >= lo.y ? ((lo + hi) / 2f, (hi.y - lo.y) / 2f) : (Vector3.zero, -1f);
                }
                if (shape.radius <= 0f) continue;
                var centre = cc.t.TransformPoint(shape.hub); float radius = shape.radius;
                var hits = Physics.RaycastAll(centre + Vector3.up * 1.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                float ground = float.NegativeInfinity;
                foreach (var h in hits) if (h.collider.attachedRigidbody == null && h.normal.y > 0.7f && h.point.y < centre.y + 0.5f && h.point.y > ground) ground = h.point.y;
                if (float.IsNegativeInfinity(ground)) continue;
                wc.GetWorldPose(out var hub, out _);
                var up = cc.t.up;
                rows.Add((model, centre.y - radius - ground, Vector3.Dot(hub - centre, up), radius, wc.radius * wc.transform.lossyScale.y,
                    cc.t.InverseTransformPoint(hub).y, cc.t.InverseTransformPoint(wc.transform.TransformPoint(wc.center)).y));
            }
        }
        foreach (var g in rows.GroupBy(x => x.model).OrderBy(g => g.Average(x => x.gap)))
            Debug.Log($"TrafficDebug: wheels of {g.Key}: {g.Count()}, bottom above the ground {g.Min(x => x.gap):F3} to {g.Max(x => x.gap):F3} m (mean {g.Average(x => x.gap):F3}); collider hub above the model's {g.Average(x => x.hubDiff):F3} m, radius model {g.Average(x => x.rModel):F3} collider {g.Average(x => x.rCol):F3}, hub at {g.Average(x => x.hubLocal):F3} in the body (suspension top {g.Average(x => x.colTop):F3})");
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
    /// A car turning across the oncoming traffic (left; right in left-hand traffic) gives way to it as drivers do: to
    /// an oncoming vehicle that would reach the point where it crosses its lane (in the next 6 s) before this car has
    /// crossed it with a gap to spare (the time to clear the lane, its length and 4 m at its speed, at least 3 m/s, and
    /// 1.5 s more), not only to one that would be there first; else it turns across just in front of the oncoming car,
    /// which has to brake hard.
    /// </summary>
    float Conflict(float start, float offset, out string what)
    {
        what = null;
        float best = float.MaxValue;
        var me = transform.position;
        _ahead.Clear();
        for (float d = 2f; d <= 30f; d += 2f) _ahead.Add(_circuit.GetRoutePoint(start + d).position);
        var across = OsmMap.Current != null && OsmMap.Current.Info.LeftHandTraffic ? CarTrafficLight.TurnArrow.Right : CarTrafficLight.TurnArrow.Left;
        bool turnsAcross = TurnAfter(start) == across;
        float clearLane = (VehicleLength + 4f) / Mathf.Max(Speed, 3f) + 1.5f;
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
            // turning across an oncoming vehicle's lane: further ahead, in steps of at most 1.5 m of its way (so its
            // positions do not skip the narrow strip where this car's path crosses its lane)
            bool gap = turnsAcross && facing < -0.7f && vc >= 1f;
            float horizon = gap ? 6f : 3f, dt = gap ? Mathf.Clamp(1.5f / vc, 0.1f, 0.5f) : 0.5f;
            // (a vehicle standing beside the path, oncoming or across, is in the way when the two bodies would overlap: half
            // of each one's width, more than the lane's half width a moving one is reckoned in; a car turning across in
            // front of one waiting for it drove into it)
            float standReach = HalfWidth(transform) + HalfWidth(c); standReach *= standReach;
            for (float tt = 0f; tt <= horizon; tt += dt)
            {
                var q = c.position + fwd * Mathf.Min(vc * tt, further);
                float r2 = tt == 0f && vc < 1f ? Mathf.Max(reach, standReach) : reach;
                for (int k = 0; k < _ahead.Count; k++)
                {
                    if (Flat2(q - _ahead[k]) > r2) continue;
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
                    if (gap) theyFirst = tt < mine + clearLane;
                    if (theyFirst) { best = along; what = $"yielding to {c.name} at {along:F0} m{(gap ? " (turning across)" : "")}"; }
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

    /// <summary>Squared distance on the ground from q to the segment a-b (t: where along it, 0 at a, 1 at b).</summary>
    static float SegmentDist2(Vector3 q, Vector3 a, Vector3 b, out float t)
    {
        float abx = b.x - a.x, abz = b.z - a.z, l2 = abx * abx + abz * abz;
        t = l2 > 1e-6f ? Mathf.Clamp01(((q.x - a.x) * abx + (q.z - a.z) * abz) / l2) : 1f;
        float dx = q.x - (a.x + abx * t), dz = q.z - (a.z + abz * t);
        return dx * dx + dz * dz;
    }
}
