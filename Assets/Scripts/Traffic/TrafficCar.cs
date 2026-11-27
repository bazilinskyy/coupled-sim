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
        float length = 0f, width = 0f;
        if (TryGetComponent<BoxCollider>(out var box) && !box.isTrigger)
        {
            var s = transform.lossyScale;
            length = box.size.z * Mathf.Abs(s.z); width = box.size.x * Mathf.Abs(s.x);
        }
        // (a root box that is not the body, as the 1 m cube many models carry: the size of what one sees, its meshes,
        // so an SUV or a van is not taken for a 4.5 by 1.8 m car and brushed past)
        var seen = SeenSize(out var centre);
        if (seen.x > 0f && (length < 2.5f || length > 25f || width < 1.2f || width > 3.2f || seen.y > length + 0.1f || seen.x > width + 0.1f))
        { length = Mathf.Max(length < 2.5f || length > 25f ? 0f : length, seen.y); width = Mathf.Max(width < 1.2f || width > 3.2f ? 0f : width, seen.x); }
        if (length < 2.5f || length > 25f || width < 1.2f || width > 3.2f) return;   // (not a car's body)
        VehicleLength = length; VehicleWidth = width;
        if (DebugTraffic && _sizeLogged.Add(name.Split('(')[0]))
            Debug.Log($"TrafficDebug: size of {name}: {length:F2} by {width:F2} m (box {(box != null ? $"{box.size.z * Mathf.Abs(transform.lossyScale.z):F2} by {box.size.x * Mathf.Abs(transform.lossyScale.x):F2}" : "none")}, meshes {seen.y:F2} by {seen.x:F2}, their centre {centre.x:F2} m right, {centre.y:F2} m ahead of the pivot)");
    }
    static readonly HashSet<string> _sizeLogged = new HashSet<string>();

    /// <summary>
    /// A vehicle of the map's traffic whose body lies well ahead of or behind its pivot (the articulated bus: its pivot
    /// at the front section's rear axle, the body's middle 3.7 m ahead) gets its pivot moved to the body's middle: the
    /// traffic's checks (gaps, guards, stops clear of crossing lanes) and the collision audit take a vehicle's body as
    /// centred on its pivot, and took the bus's rear 3.7 m behind where it is (stopping for, and counting contacts with,
    /// a lamp post and a tree behind it in São Paulo) and its front 3.7 m short. Nothing moves in the world: the root
    /// moves forward and its parts, colliders and the hitch of a rear section back. Not for a participant's vehicle,
    /// whose position is logged.
    /// </summary>
    public void CentrePivot()
    {
        float ahead;
        if (TryGetComponent<BoxCollider>(out var box) && !box.isTrigger && Mathf.Abs(box.size.z * Mathf.Abs(transform.lossyScale.z) - VehicleLength) < 0.2f)
            ahead = box.center.z * transform.lossyScale.z;
        else { SeenSize(out var c); ahead = c.y; }
        if (Mathf.Abs(ahead) < 0.5f) return;
        float local = ahead / transform.lossyScale.z;
        var shift = new Vector3(0f, 0f, local);
        for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).localPosition -= shift;
        foreach (var c in GetComponents<Collider>())
        {
            if (c is BoxCollider b) b.center -= shift;
            else if (c is CapsuleCollider cc) cc.center -= shift;
            else if (c is SphereCollider sc) sc.center -= shift;
            else if (c is WheelCollider wc) wc.center -= shift;
        }
        if (TryGetComponent<ArticulatedTrailer>(out var art)) art.HitchLocal -= shift;
        var to = transform.position + transform.forward * ahead;
        transform.position = to;
        if (TryGetComponent<Rigidbody>(out var rb)) { rb.position = to; if (!rb.automaticCenterOfMass) rb.centerOfMass -= shift; }
        Physics.SyncTransforms();
        if (DebugTraffic) Debug.Log($"TrafficDebug: {name}: pivot moved {ahead:F2} m ahead, to its body's middle");
    }

    /// <summary>The width (x) and length (y) of the vehicle's visible meshes, in its own frame (wheels, mirrors and all;
    /// not lights' glow, shadows or anything over 25 m), and their centre's offset from the pivot (x right, y ahead).</summary>
    Vector2 SeenSize(out Vector2 centre)
    {
        centre = Vector2.zero;
        var w2l = transform.worldToLocalMatrix;
        bool any = false; Vector3 min = Vector3.zero, max = Vector3.zero;
        // (an articulated vehicle's rear section, a child until it starts: not the front's body)
        var rear = TryGetComponent<ArticulatedTrailer>(out var art) ? art.Trailer : null;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (rear != null && r.transform.IsChildOf(rear)) continue;
            Bounds lb;
            if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null) lb = mf.sharedMesh.bounds;
            else if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null) lb = sk.sharedMesh.bounds;
            else continue;
            string n = r.name.ToLowerInvariant();
            if (n.Contains("shadow") || n.Contains("glow") || n.Contains("flare") || n.Contains("beam") || n.Contains("driver") || n.Contains("bip")) continue;
            var m = w2l * r.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var q = m.MultiplyPoint3x4(c);
                if (!any) { min = max = q; any = true; } else { min = Vector3.Min(min, q); max = Vector3.Max(max, q); }
            }
        }
        if (!any) return Vector2.zero;
        var size = max - min;
        if (size.z > 25f || size.x > 4f) return Vector2.zero;
        centre = new Vector2((min.x + max.x) / 2f, (min.z + max.z) / 2f);
        return new Vector2(size.x, size.z);
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
    static readonly List<CachedCar> _standing = new List<CachedCar>();
    /// <summary>The vehicle this one stopped or slowed for at its last look ahead (null: none, or not a vehicle), and the
    /// one it gave way to.</summary>
    Transform _blocker, _conflictWith;
    /// <summary>-debugTraffic: rings of vehicles waiting for each other, broken by the one giving way going on.</summary>
    static int RingsBroken;

    /// <summary>Whether <paramref name="t"/> waits for <paramref name="target"/>, directly or through up to eight others
    /// (<paramref name="lowest"/>: the lowest rank of those between them that give way, t included; not of those standing
    /// for a body in their path: a car giving way to the participant's car, which stood for that car's body in its way,
    /// never went on, the participant's rank being lower, and both stood for good).</summary>
    static bool WaitsFor(TrafficCar t, Transform target, out int lowest)
    {
        lowest = int.MaxValue;
        for (int i = 0; i < 8 && t != null; i++)
        {
            if (t._conflictWith != null && t._blocker == t._conflictWith) lowest = Mathf.Min(lowest, t._rank);
            var b = t._blocker;
            if (b == null) return false;
            if (b == target) return true;
            if (!b.TryGetComponent(out t)) return false;
        }
        return false;
    }

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
        if (IsRider && RideLanes) UpdateRideLane(dt);
        var leader = FindLeader(out LeaderDescription);
        LeaderGap = leader.Present ? leader.Gap : float.PositiveInfinity;
        if (Speed > 0.3f) _stoppedSince = 0f; else if (_stoppedSince == 0f) _stoppedSince = Time.time;
        Acceleration = Model.Acceleration(Speed, leader, dt);
        Speed = Mathf.Max(0f, Speed + Acceleration * dt);
        // the model's speed stays close to the body's: a car held by something (a pole, a kerb, another car) does not
        // go on believing it drives on, and the cars behind it, which follow its speed, stop behind it
        float body = _rb != null && !_rb.isKinematic ? Mathf.Max(0f, Vector3.Dot(_rb.linearVelocity, transform.forward)) : Speed;
        Speed = Mathf.Min(Speed, body + 1.5f);
        // the last check: the body swept along its path, whatever the rules above decided, touches nothing (another
        // vehicle, a rider, a pedestrian, an animal, a post, a tree, a wall, a parked car): no faster than stops before it
        if (AuditOn) GuardState = $"v {Speed:F2} m/s, not checked (standing)";
        if (Speed > 0.01f)   // (also creeping: a cyclist crept into a bollard at 5 cm/s)
        {
            float free = ClearAhead(out var guardWhat, out var guardWhom);
            if (AuditOn) GuardState = $"v {Speed:F2} m/s, free {free:F2} m{(guardWhat != null ? " short of " + guardWhat : "")}, rules: {LeaderDescription}";
            if (free < float.PositiveInfinity)
            {
                float vmax = Mathf.Sqrt(2f * GuardBraking * Mathf.Max(0f, free - 0.3f));
                if (Speed > vmax || vmax < 0.3f)
                {
                    if (_guardSince < 0f) _guardSince = Time.time;
                    _guardWhat = guardWhat;
                    Acceleration = Mathf.Min(Acceleration, (vmax - Speed) / dt);
                    Speed = Mathf.Min(Speed, vmax); GuardFrames++;
                    // (held by a vehicle: that one is what it waits for, so a ring of vehicles waiting for each other, one of
                    // them giving way to this one, is seen and broken: the one giving way goes on; a car that had right of
                    // way at a junction stood for good against one waiting for it)
                    if (guardWhom != null) _blocker = guardWhom;
                    _guardHeldByFixed = guardWhom == null && guardWhat != null && !guardWhat.StartsWith("a pedestrian");
                    if (_guardHeldByFixed && _guardSince >= 0f && Time.time - _guardSince > 3f) _squeezeUntil = Time.time + 4f;
                    LeaderDescription = $"{LeaderDescription} [stopping {free:F1} m short of {guardWhat}]";
                    if (DebugTraffic && _guardLogged.Add(name + "|" + guardWhat) && _guardLogged.Count <= 40)
                        Debug.Log($"TrafficDebug: guard: {name} at {Speed * 3.6f:F0} km/h kept {free:F1} m short of {guardWhat} at ({transform.position.x:F1},{transform.position.z:F1}); its rules: {LeaderDescription}");
                }
            }
        }
        if (!LeaderDescription.Contains("[stopping ")) _guardSince = -1f;
        if (Squeezing) Speed = Mathf.Min(Speed, 1.4f);
        // two vehicles each held by the other for over 4 s (one stopped to let the other through a narrow street, its nose
        // in the other's turn): the one placed later backs up, up to 4 m at walking pace into room clear behind it, and then
        // waits until the other has gone by, as a driver does; else both stood for good and the queue behind them too
        // (the other held by its last check, or standing over 4 s by its rules for this one in its path: two cars nose to
        // nose in a narrow street in London, one stopped by its check, the other by its rules for the first, stood 20 s)
        if (!IsRider && _reverseLeft <= 0f && Time.time >= _reverseAgain && _blocker != null && _blocker.TryGetComponent<TrafficCar>(out var other)
            && other._blocker == transform && (other.HeldLong || other.WaitingLong)
            // (this one held by its check, or standing by its rules while the other, held by its check, has no room to back
            // up: in Montreal a car turning across stood with its nose in another's lane, a cyclist waiting behind it, and the
            // other, with the road clear behind it, never backed up; both stood 41 s)
            && (HeldLong || _narrowFor == other.transform || (WaitingLong && other.HeldLong && other.ClearBehind() < 1f))
            && ClearBehind() >= 1f && (_narrowFor == other.transform || !other.HeldLong || _rank > other._rank || other.ClearBehind() < 1f))
        {
            _reverseLeft = 4f; _backFor = other.transform; Reversals++;
            if (DebugTraffic) Debug.Log($"TrafficDebug: {name} backs up for {other.name} at ({transform.position.x:F1},{transform.position.z:F1}): each held by the other for {Time.time - _guardSince:F0} s");
        }
        // held by something fixed for over 6 s while well beside its lane (a wide turn or a swerve that ended against a
        // hedge or a post): it backs up 2.5 m at walking pace and then steers back onto its lane, as a driver does
        // (inching past with 3 cm to spare did not free a car 1.4 m beside its lane against a hedge in Eindhoven at rush
        // hour, and the queue behind it stood for good)
        if (!IsRider && _reverseLeft <= 0f && Time.time >= _reverseAgain && _guardHeldByFixed && _guardSince >= 0f && Time.time - _guardSince > 6f
            && _circuit != null && ClearBehind() >= 1.5f && Mathf.Abs(LaneOffset()) > 0.5f)
        {
            _reverseLeft = 2.5f; _backFor = null; Reversals++;
            if (DebugTraffic) Debug.Log($"TrafficDebug: {name} backs up off something fixed at ({transform.position.x:F1},{transform.position.z:F1}), {LaneOffset():F2} m beside its lane, held for {Time.time - _guardSince:F0} s");
        }
        if (_reverseLeft > 0f)
        {
            float room = ClearBehind();
            float back = room > 0.5f ? Mathf.Min(1f, Mathf.Sqrt(2f * 2f * (room - 0.5f))) : 0f;
            if (back < 0.05f) _reverseLeft = 0f; else _reverseLeft -= back * dt;
            if (_reverseLeft <= 0f) { _waitFor = _backFor; _waitUntil = Time.time + 10f; _reverseAgain = Time.time + 12f; }
            Speed = 0f; Acceleration = 0f;
            LeaderDescription = $"backing up for {(_backFor != null ? _backFor.name : "a vehicle")}";
            _car.ExternalSpeed = -back; _car.ExternalAcceleration = 0f;
            return;
        }
        // (after backing up: waiting while the other comes by, until it is 8 m away or 10 s have gone)
        if (_waitFor != null && Time.time < _waitUntil && Flat2(_waitFor.position - transform.position) < 8f * 8f)
        { Speed = 0f; Acceleration = Mathf.Min(Acceleration, 0f); LeaderDescription = $"letting {_waitFor.name} by"; }
        else _waitFor = null;
        // held: nothing ahead to stop for, wanting to drive, but not moving
        bool held = body < 0.5f && Model.DesiredSpeed > 2f && LeaderGap > 6f;
        if (!held) _heldSince = -1f; else if (_heldSince < 0f) _heldSince = Time.time;
        if (DebugTraffic && Time.time >= _nextHistory)
        {
            _nextHistory = Time.time + 0.25f;
            var rbv = TryGetComponent<Rigidbody>(out var rb0) ? rb0.linearVelocity.magnitude : -1f;
            float off = LaneOffset() - LateralRight;   // (a cyclist: from its own place across the path)
            if (Mathf.Abs(off) > Mathf.Abs(_maxOff)) { _maxOff = off; _maxOffAt = transform.position; }
            if (!IsRider && Speed > 1f) { LaneSamples++; if (Mathf.Abs(off) > 0.5f) LaneOff05++; if (Mathf.Abs(off) > 1f) LaneOff10++; }
            var line = $"t={Time.time:F2} prog={_tracker.ProgressDistance:F1} v={Speed:F1} rb={rbv:F1} pos=({transform.position.x:F1},{transform.position.z:F1}) off={off:F2} {LeaderDescription}";
            _history.Enqueue(line);
            while (_history.Count > 12) _history.Dequeue();
            // (first more than 0.8 m off its lane: the last 3 s, with what it was doing and what was round it)
            if (Mathf.Abs(off) > 0.8f && !_offLogged && _offLogs < 40)
            {
                _offLogged = true; _offLogs++;
                string near = string.Join(", ", _cars.Where(c => c.t != transform && Flat2(c.pos - transform.position) < 6f * 6f).Select(c => $"{c.name} {Mathf.Sqrt(Flat2(c.pos - transform.position)):F1} m"));
                // (the lane's tightest bend within 10 m, and whether the vehicle is off to its inside or outside)
                float tight = float.MaxValue, bendSign = 0f;
                for (float d = -10f; d <= 10f; d += 1f)
                {
                    var a = Flat(_circuit.GetRoutePosition(_tracker.ProgressDistance + d - 3f)); var b = Flat(_circuit.GetRoutePosition(_tracker.ProgressDistance + d)); var c = Flat(_circuit.GetRoutePosition(_tracker.ProgressDistance + d + 3f));
                    float cr = Vector3.Cross(b - a, c - b).y, twice = Vector3.Cross(b - a, c - a).magnitude;
                    if (twice < 1e-4f) continue;
                    float rad = (b - a).magnitude * (c - b).magnitude * (a - c).magnitude / (2f * twice);
                    if (rad < tight) { tight = rad; bendSign = Mathf.Sign(cr); }
                }
                string bend = tight < 200f ? $"bend {tight:F1} m to the {(bendSign > 0 ? "right" : "left")}, off to its {(Mathf.Sign(off) == bendSign ? "inside" : "outside")}" : "straight";
                Debug.Log($"TrafficDebug: off its lane: {name} ({(IsRider ? "rider" : $"{VehicleLength:F1} m")}) {off:F2} m at ({transform.position.x:F1},{transform.position.z:F1}), {bend}; near: {(near.Length > 0 ? near : "nothing")}; the last 3 s: " + string.Join(" | ", _history));
            }
            if (Mathf.Abs(off) < 0.4f) _offLogged = false;
            // (-debugCar "Map traffic Map route 24 15": that vehicle's every line, with its heading)
            if (DebugCar != null && name == DebugCar) Debug.Log($"TrafficDebug: {name}: {line} heading {transform.eulerAngles.y:F0} yaw rate {(rb0 != null ? rb0.angularVelocity.y * Mathf.Rad2Deg : 0f):F0}°/s");
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
        UpdateIndicators();
        _car.ExternalSpeed = Speed;
        _car.ExternalAcceleration = Acceleration;
    }

    CarBlinkers _blinkers; bool _blinkersChecked;
    float _nextSignal, _signalSince;

    /// <summary>
    /// The indicators, as drivers use them: before a turn off the road (a heading change of more than 35° over 20 m of
    /// the route at a junction, not the road's own bend; any such turn where the map's junctions are unknown, from 60°)
    /// and a move across to another lane (1.8 m sideways within 30 m without a turn), from 3 s or 30 m before it,
    /// whichever is further, until it is done; about 1.5 flashes a second. A van turned at a junction in Barcelona
    /// without indicating.
    /// </summary>
    static Transform[] _autopilots; static float _autopilotsAt = -10f;
    /// <summary>-debugTraffic: this vehicle is the participant's autopilot or within <paramref name="r"/> m of it.</summary>
    bool NearAutopilot(float r)
    {
        if (Time.time - _autopilotsAt > 5f) { _autopilotsAt = Time.time; _autopilots = FindObjectsByType<AutomatedDriving>().Select(a => a.transform).ToArray(); }
        foreach (var a in _autopilots) if (a != null && (a.position - transform.position).sqrMagnitude < r * r) return true;
        return false;
    }

    void UpdateIndicators()
    {
        if (IsRider || Time.time < _nextSignal) return;
        _nextSignal = Time.time + 0.2f;
        if (!_blinkersChecked)
        {
            _blinkersChecked = true;
            _blinkers = TryGetComponent<PlayerAvatar>(out var pa) ? pa.CarBlinkers : null;
            if (_blinkers == null) _blinkers = GetComponentInChildren<CarBlinkers>(true);
            if (_blinkers != null) _blinkers.BlinkInterval = 0.33f;
        }
        if (_blinkers == null || _circuit == null || _tracker == null) return;
        var want = BlinkerState.None;
        float start = _tracker.ProgressDistance;
        float reach = Mathf.Max(30f, Speed * 3f);
        if (_endsRoute > 0) reach = Mathf.Min(reach, _circuit.OpenLength - Mathf.Repeat(start, _circuit.Length) - 20f);
        Vector3 Dir(float d) { var v = _circuit.GetRoutePoint(start + d).direction; v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Flat(transform.forward).normalized; }
        // (from just behind the car, so a turn it is in keeps the lamps going until it is out of it)
        for (float d = -6f; d <= reach && want == BlinkerState.None; d += 3f)
        {
            float a = Vector3.SignedAngle(Dir(d), Dir(d + 20f), Vector3.up);
            if (Mathf.Abs(a) < 45f) continue;
            var apex = _circuit.GetRoutePoint(start + d + 10f).position;
            bool? atJunction = MapTraffic.JunctionNear(apex, 15f);
            if (atJunction == false || (atJunction == null && Mathf.Abs(a) < 60f)) continue;
            want = a < 0f ? BlinkerState.Left : BlinkerState.Right;
        }
        if (want == BlinkerState.None)
        {
            // a move across to another lane: the route a full lane (2.8 m) to one side within 40 m, its heading the same
            var p0 = _circuit.GetRoutePoint(start).position; var f0 = Dir(0f);
            for (float d = 10f; d <= Mathf.Min(reach, 40f) && want == BlinkerState.None; d += 10f)
            {
                var p = _circuit.GetRoutePoint(start + d).position;
                if (Vector3.Angle(f0, Dir(d)) > 12f) break;
                float side = Vector3.Dot(Flat(p - p0), Vector3.Cross(Vector3.up, f0));
                if (Mathf.Abs(side) > 2.8f) want = side < 0f ? BlinkerState.Left : BlinkerState.Right;
            }
        }
        // (a few tenths of a second's hold, so a lamp does not flick off between two turns' readings)
        if (want == BlinkerState.None && _blinkers.State != BlinkerState.None && Time.time - _signalSince < 1f) return;
        if (want != _blinkers.State)
        {
            _blinkers.SwitchToState(want); _signalSince = Time.time;
            if (DebugTraffic && NearAutopilot(40f)) Debug.Log($"TrafficDebug: signal {want} t={Time.time:F1} {name} at ({transform.position.x:F0},{transform.position.z:F0}), {Speed * 3.6f:F0} km/h");
        }
        else if (want != BlinkerState.None) _signalSince = Time.time;
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

    float _guardSince = -1f; string _guardWhat; bool _guardHeldByFixed;
    float _reverseLeft, _reverseAgain, _waitUntil; Transform _backFor, _waitFor;
    /// <summary>How long (s) the last check has held it for something fixed (a wall, a post, a parked car), 0 when not.</summary>
    public float HeldByFixedFor => _guardHeldByFixed && _guardSince >= 0f ? Time.time - _guardSince : 0f;
    /// <summary>Held by the last check for over 4 s.</summary>
    bool HeldLong => _guardSince >= 0f && Time.time - _guardSince > 4f;
    /// <summary>Standing over 4 s (by its rules or its last check).</summary>
    bool WaitingLong => _stoppedSince > 0f && Time.time - _stoppedSince > 4f;
    /// <summary>-debugTraffic: deadlocks broken by one vehicle backing up.</summary>
    public static int Reversals;

    /// <summary>How far (m, up to 5) this vehicle can back up straight before its body (0.15 m more at the sides, 0.3 m
    /// behind) touches another agent or something fixed.</summary>
    float ClearBehind()
    {
        float hl = VehicleLength / 2f, hw = VehicleWidth / 2f;
        var me = transform.position; var f = Flat(transform.forward).normalized;
        for (float sd = 0.5f; sd <= 5f; sd += 0.5f)
        {
            var pos = me - f * sd - f * 0.15f;
            foreach (var c in _cars)
            {
                if (c.t == transform || Flat2(c.pos - me) > 15f * 15f) continue;
                float chl = (c.tc != null ? (c.tc.IsRider ? 1.8f : c.tc.VehicleLength) : 4.5f) / 2f;
                if (BoxesGap(pos, f, hl + 0.15f, hw + 0.15f, c.pos, Flat(c.fwd).normalized, chl, HalfWidth(c.t)) <= 0f
                    && BoxesGap(pos, f, hl, hw, c.pos, Flat(c.fwd).normalized, chl, HalfWidth(c.t)) < BoxesGap(me, f, hl, hw, c.pos, Flat(c.fwd).normalized, chl, HalfWidth(c.t))) return sd - 0.5f;
            }
            foreach (var q in _pedestrians) if (Flat2(q.pos - me) < 15f * 15f && BodyDist(q.pos, pos, f, hl + 0.15f, hw + 0.15f) < 0.3f) return sd - 0.5f;
            if (FixedAt(pos, f, hl + 0.15f, hw + FixedSide, out _, out _)) return sd - 0.5f;
        }
        return 5f;
    }
    float _squeezeUntil;
    /// <summary>Inching past something fixed (held by it for over 3 s), for 4 s after that last held it.</summary>
    bool Squeezing => Time.time < _squeezeUntil;
    /// <summary>-debugCollisions: what the last check found in this physics step.</summary>
    [System.NonSerialized] public string GuardState;
    static readonly bool AuditOn = System.Array.IndexOf(CommandLine.Args, "-debugCollisions") >= 0;
    float _nextNarrow, _narrowWait = float.MaxValue; string _narrowWhat; Transform _narrowFor;
    readonly List<Vector3> _theirWay = new List<Vector3>();

    /// <summary>
    /// Where (m ahead of the progress point, its front 1 m short) this car waits for an oncoming vehicle (within 40 m
    /// ahead) to come through a part of the street too narrow for both: where their paths come nearer than their half
    /// widths and 0.3 m. The one in the narrow part already goes on; with neither in it, the one that reaches it later (by
    /// its distance at its speed, at least 1 m/s; the one placed later on a tie) waits; with both in it, neither (the last
    /// check keeps them apart). MaxValue: no waiting. As drivers do in a street lined with parked cars or house fronts: in
    /// Montreal two cars met in a street with lanes 1.9 m apart and scraped past each other.
    /// </summary>
    float NarrowWait(float start, Vector3 me, Vector3 myFwd, out string what, out Transform whom)
    {
        what = null; whom = null;
        float best = float.MaxValue;
        var f = Flat(myFwd).normalized;
        var right = Vector3.Cross(Vector3.up, f);
        float hl = VehicleLength / 2f, hw = VehicleWidth / 2f;
        for (int i = 0; i < _cars.Count; i++)
        {
            var c = _cars[i];
            if (c.t == transform || c.tc == null || c.tc.IsRider || c.tc._circuit == null || c.tc._tracker == null) continue;
            if (Vector3.Dot(Flat(c.fwd).normalized, f) > -0.7f || Mathf.Abs(c.pos.y - me.y) > 3f) continue;
            var d = Flat(c.pos - me);
            float along = Vector3.Dot(d, f);
            if (along < 0f || along > 40f || Mathf.Abs(Vector3.Dot(d, right)) > 6f) continue;
            float need = hw + c.tc.VehicleWidth / 2f + 0.3f;
            // (its way towards this car, every 2 m; this car's way towards it, the parts nearer than need)
            _theirWay.Clear();
            float cStart = c.tc._tracker.ProgressDistance;
            for (float u = 0f; u <= along + 6f; u += 2f) _theirWay.Add(c.tc._circuit.GetRoutePoint(cStart + u).position);
            float from = -1f, to = -1f;
            for (float sd = 0f; sd <= along + 2f; sd += 2f)
            {
                var p = _circuit.GetRoutePoint(start + sd).position;
                float g = float.MaxValue;
                for (int j = 0; j < _theirWay.Count; j++) g = Mathf.Min(g, Flat2(p - _theirWay[j]));
                if (g < need * need) { if (from < 0f) from = sd; to = sd; }
            }
            if (from < 0f) continue;
            float chl = c.tc.VehicleLength / 2f;
            bool meIn = from <= hl + 0.5f, theyIn = to >= along - chl - 0.5f;
            bool wait;
            if (meIn != theyIn) wait = theyIn;
            else if (meIn) wait = false;
            else
            {
                float mine = (from - hl) / Mathf.Max(Speed, 1f), theirs = (along - to - chl) / Mathf.Max(c.tc.Speed, 1f);
                wait = mine > theirs + 0.2f || (Mathf.Abs(mine - theirs) <= 0.2f && _rank > c.tc._rank);
            }
            // (the other waiting for this one too: each measured the narrow part from its own way and found the other in
            // it, and both stood for good in a Montreal street; the one placed first goes on)
            if (wait && c.tc._narrowFor == transform && c.tc._narrowWait < float.MaxValue && _rank < c.tc._rank) wait = false;
            if (!wait) continue;
            float at = from - hl - 1f;
            if (at < best) { best = at; what = $"waiting for oncoming {c.name} to come through the narrow street"; whom = c.t; }
        }
        return best;
    }

    /// <summary>Braking (m/s²) the last check allows for: hard, short of an emergency stop.</summary>
    const float GuardBraking = 6f;
    /// <summary>Room (m) the last check keeps beside a vehicle's sides from what stands fixed (a parked car, a tree, a
    /// wall): drivers squeeze past a parked car slowly, not with their mirror on it.</summary>
    const float FixedSide = 0.15f;
    /// <summary>Physics steps in which the last check slowed a vehicle (-debugTraffic reports them).</summary>
    public static int GuardFrames;
    static readonly HashSet<string> _guardLogged = new HashSet<string>();
    static readonly Collider[] _guardHits = new Collider[24];
    float _guardFixedAt = float.PositiveInfinity, _guardFixedTime = -1f; string _guardFixedWhat;

    /// <summary>
    /// How far (m) this vehicle can go on along its path before its body (its length and width, 5 cm more at the sides
    /// and 0.3 m at the front) touches anything: the other vehicles and riders where they are, the pedestrians and
    /// animals (0.3 m round), and what stands fixed from 0.4 to 1.6 m above the road (posts, trees, walls, parked cars;
    /// not the road, kerbs or pavements). The body moves from where it is onto its path within 6 m, turning with it.
    /// Up to what it could stop in at <see cref="GuardBraking"/> and 2 m more; infinity when nothing is in reach. Something
    /// it touches already does not count unless the path takes it nearer.
    /// </summary>
    float ClearAhead(out string what, out Transform whom)
    {
        what = null; whom = null;
        float reach = Speed * Speed / (2f * GuardBraking) + 2f;
        float hl = (IsRider ? 1.8f : VehicleLength) / 2f, hw = IsRider ? 0.35f : VehicleWidth / 2f;
        var me = transform.position; var fwd0 = Flat(transform.forward).normalized;
        float start = _tracker.ProgressDistance;
        var path0 = _circuit.GetRoutePoint(start).position + RideOffset(start);
        var offset = Flat(me - path0);
        // (the agents within reach)
        float near2 = (reach + hl + 6f) * (reach + hl + 6f);
        float best = float.PositiveInfinity;
        _guardCars.Clear(); _guardPeds.Clear();
        _guardSize.Clear();
        for (int i = 0; i < _cars.Count; i++)
        {
            var c = _cars[i];
            if (c.t == transform || Flat2(c.pos - me) > near2 || Mathf.Abs(c.pos.y - me.y) > 3f) continue;
            _guardCars.Add(c);
            // (its half length and width, and its heading, once per check, not at every sample)
            _guardSize.Add(((c.tc != null ? (c.tc.IsRider ? 1.8f : c.tc.VehicleLength) : 4.5f) / 2f, HalfWidth(c.t), Flat(c.fwd).normalized));
        }
        for (int i = 0; i < _pedestrians.Count; i++) { var pd = _pedestrians[i]; if (Flat2(pd.pos - me) <= near2 && Mathf.Abs(pd.pos.y - me.y) <= 2.5f) _guardPeds.Add(pd.pos); }
        for (int i = 0; i < _animals.Count; i++) { var an = _animals[i]; if (Flat2(an.pos - me) <= near2 && Mathf.Abs(an.pos.y - me.y) <= 2.5f) _guardPeds.Add(an.pos); }
        float step = Mathf.Max(0.5f, reach / 16f);
        var prev = me;
        if (_guardCars.Count > 0 || _guardPeds.Count > 0)
        for (float sd = step; sd <= reach + 1e-3f; sd += step)
        {
            var rp = _circuit.GetRoutePoint(start + sd);
            float k = Mathf.Clamp01(1f - sd / 6f);
            var pos = rp.position + RideOffset(start + sd) + offset * k;
            var dir = Flat(pos - prev); prev = pos;
            var f = dir.sqrMagnitude > 1e-6f ? dir.normalized : fwd0;
            f = Vector3.Slerp(fwd0, f, Mathf.Clamp01(sd / 3f)).normalized;
            // (the front 0.3 m further: the body's centre moved on by that much, its half length kept)
            var front = pos + f * 0.15f;
            for (int i = 0; i < _guardCars.Count; i++)
            {
                var c = _guardCars[i];
                var (chl, chw, cf) = _guardSize[i];
                // (out of reach of this sample: centres further apart than both bodies' half diagonals and the margins)
                if (Flat2(c.pos - front) > (hl + hw + chl + chw + 0.6f) * (hl + hw + chl + chw + 0.6f)) continue;
                // (0.15 m beside its body, a vehicle passing one oncoming in a narrow street 5 cm: the lanes there are 1.9 m
                // apart, and they pass slowly)
                float side = Vector3.Dot(cf, f) < -0.7f ? 0.05f : 0.15f;
                // the body along its path; over the first 3 m also at the heading halfway between its present one and the
                // path's (slow in a turn, a body lags its path's heading, and its rear corner swung into a car waiting
                // beside it); over the first 1.5 m also straight on (pressed against another, a car slid along it while its
                // progress along the path stood still)
                float gap = BoxesGap(front, f, hl + 0.15f, hw + side, c.pos, cf, chl, chw), trueGap = BoxesGap(pos, f, hl, hw, c.pos, cf, chl, chw);
                if (sd <= 3f)
                {
                    var fm = (fwd0 + f).normalized;
                    gap = Mathf.Min(gap, BoxesGap(pos + fm * 0.15f, fm, hl + 0.15f, hw + side, c.pos, cf, chl, chw));
                    trueGap = Mathf.Min(trueGap, BoxesGap(pos, fm, hl, hw, c.pos, cf, chl, chw));
                }
                if (sd <= 1.5f)
                {
                    var ahead = me + fwd0 * sd;
                    gap = Mathf.Min(gap, BoxesGap(ahead + fwd0 * 0.15f, fwd0, hl + 0.15f, hw + side, c.pos, cf, chl, chw));
                    trueGap = Mathf.Min(trueGap, BoxesGap(ahead, fwd0, hl, hw, c.pos, cf, chl, chw));
                }
                if (gap > 0f) continue;
                // (within its margin already: only if the step brings the bodies no nearer, by the gap between them, not
                // their middles: a car creeping past one waiting nose to nose slid its corner into it, its middle moving
                // away; and then only with 5 cm between the bodies themselves, or parting them: a car crept along a waiting
                // cyclist at a hand's breadth, the gap within its margin but no smaller)
                float trueNow = BoxesGap(me, fwd0, hl, hw, c.pos, cf, chl, chw);
                if (BoxesGap(me + fwd0 * 0.15f, fwd0, hl + 0.15f, hw + side, c.pos, cf, chl, chw) is var gapNow && gapNow <= 0f && gap >= gapNow - 1e-3f
                    && (trueGap >= 0.05f || trueGap > trueNow + 0.01f)) continue;
                // (free up to the last sample that was clear, not this one: blocked at the first sample, a car was let on
                // at 1.5 m/s as though half a metre were free, and it pushed into the one waiting there)
                if (sd - step < best) { best = sd - step; what = c.name; whom = c.t; }
                break;
            }
            if (best < sd) break;
            for (int i = 0; i < _guardPeds.Count && best >= sd; i++)
            {
                var q = _guardPeds[i];
                if (BodyDist(q, front, f, hl + 0.15f, hw + 0.05f) >= 0.3f) continue;
                if (BodyDist(q, me, fwd0, hl, hw) < 0.3f && Flat2(pos - q) >= Flat2(me - q) - 1e-4f) continue;
                best = sd - step; what = "a pedestrian or animal"; whom = null;
            }
            if (best < sd) break;
        }
        // fixed things: every 0.2 s (they do not move), along the path at 1 m steps
        if (Time.time - _guardFixedTime >= 0.2f)
        {
            _guardFixedTime = Time.time; _guardFixedAt = float.PositiveInfinity; _guardFixedWhat = null;
            prev = me;
            // (what it touches already, a tree or a wall it is held against: not counted, so it can move off it; that one
            // collider, not all of the same name: every parked car is "Parked car")
            // (held by something fixed for over 3 s, a post or a parked car its lane passes too near: it inches past with
            // 3 cm to spare, no faster than 5 km/h, as drivers do, rather than standing there for good)
            float fs = Squeezing ? 0.03f : FixedSide;
            FixedAt(me, fwd0, hl, hw + fs, out var nowName, out var nowCollider);
            // (every 0.5 m over the first 3 m, then every metre; a hit found to 1/8 m between the last clear sample and it)
            bool HitAt(float sd, out string fw)
            {
                var pos = _circuit.GetRoutePoint(start + sd).position + RideOffset(start + sd) + offset * Mathf.Clamp01(1f - sd / 6f);
                var back = _circuit.GetRoutePoint(start + Mathf.Max(0f, sd - 0.5f)).position + RideOffset(start + Mathf.Max(0f, sd - 0.5f)) + offset * Mathf.Clamp01(1f - Mathf.Max(0f, sd - 0.5f) / 6f);
                var dir = Flat(pos - back);
                var f = dir.sqrMagnitude > 1e-6f ? dir.normalized : fwd0;
                // (along the path, and over the first 2 m also straight on: a car slow in a tight turn cuts inside its path)
                if (FixedAt(pos + f * 0.15f, f, hl + 0.15f, hw + fs, out fw, out _, nowCollider)) return true;
                var fm = (fwd0 + f).normalized;
                if (sd <= 3f && fm.sqrMagnitude > 0.5f && FixedAt(pos + fm * 0.15f, fm, hl + 0.15f, hw + fs, out fw, out _, nowCollider)) return true;
                if (sd <= 2f && FixedAt(me + fwd0 * (sd + 0.15f), fwd0, hl + 0.15f, hw + fs, out fw, out _, nowCollider)) return true;
                // (the one within its margin now: only touching it, 2 cm, counts)
                if (nowCollider != null && Overlaps(pos + f * 0.15f, f, hl + 0.15f, hw + 0.02f, nowCollider)) { fw = nowName; return true; }
                return false;
            }
            float clear = 0f;
            for (float sd = 0.5f; sd <= reach + 1e-3f; sd += sd < 3f ? 0.5f : 1f)
            {
                if (!HitAt(sd, out var fw)) { clear = sd; continue; }
                float lo = clear, hi = sd; string w = fw;
                for (int it = 0; it < 3; it++) { float mid = (lo + hi) / 2f; if (HitAt(mid, out var fm)) { hi = mid; w = fm; } else lo = mid; }
                _guardFixedAt = lo; _guardFixedWhat = w; _guardFixedStart = start;
                break;
            }
        }
        if (_guardFixedWhat != null)
        {
            // (measured from where the vehicle was then)
            float at = _guardFixedAt - (_tracker.ProgressDistance - _guardFixedStart);
            if (at < best) { best = Mathf.Max(0f, at); what = _guardFixedWhat; whom = null; }
        }
        return best;
    }
    float _guardFixedStart;
    static readonly List<CachedCar> _guardCars = new List<CachedCar>();
    static readonly List<(float hl, float hw, Vector3 fwd)> _guardSize = new List<(float, float, Vector3)>();
    static readonly List<Vector3> _guardPeds = new List<Vector3>();

    /// <summary>The gap (m) between two boxes on the ground (half lengths and widths): their largest separation along the
    /// four axes; negative when they overlap (the least it would take to part them).</summary>
    static float BoxesGap(Vector3 ca3, Vector3 fa3, float ahl, float ahw, Vector3 cb3, Vector3 fb3, float bhl, float bhw)
    {
        Vector2 ca = new Vector2(ca3.x, ca3.z), cb = new Vector2(cb3.x, cb3.z), fa = new Vector2(fa3.x, fa3.z), fb = new Vector2(fb3.x, fb3.z);
        if (fa.sqrMagnitude < 1e-6f) fa = Vector2.up; if (fb.sqrMagnitude < 1e-6f) fb = Vector2.up;
        Vector2 ra = new Vector2(fa.y, -fa.x), rb = new Vector2(fb.y, -fb.x);
        var d = cb - ca;
        float Sep(Vector2 ax) => Mathf.Abs(Vector2.Dot(d, ax)) - (Mathf.Abs(Vector2.Dot(fa, ax)) * ahl + Mathf.Abs(Vector2.Dot(ra, ax)) * ahw + Mathf.Abs(Vector2.Dot(fb, ax)) * bhl + Mathf.Abs(Vector2.Dot(rb, ax)) * bhw);
        return Mathf.Max(Mathf.Max(Sep(fa), Sep(ra)), Mathf.Max(Sep(fb), Sep(rb)));
    }

    static bool BoxesTouch(Vector3 ca3, Vector3 fa3, float ahl, float ahw, Vector3 cb3, Vector3 fb3, float bhl, float bhw)
    {
        Vector2 ca = new Vector2(ca3.x, ca3.z), cb = new Vector2(cb3.x, cb3.z), fa = new Vector2(fa3.x, fa3.z), fb = new Vector2(fb3.x, fb3.z);
        if (fa.sqrMagnitude < 1e-6f) fa = Vector2.up; if (fb.sqrMagnitude < 1e-6f) fb = Vector2.up;
        Vector2 ra = new Vector2(fa.y, -fa.x), rb = new Vector2(fb.y, -fb.x);
        var d = cb - ca;
        if ((ahl + ahw + bhl + bhw) * (ahl + ahw + bhl + bhw) < d.sqrMagnitude) return false;
        bool Apart(Vector2 ax) => Mathf.Abs(Vector2.Dot(d, ax)) > Mathf.Abs(Vector2.Dot(fa, ax)) * ahl + Mathf.Abs(Vector2.Dot(ra, ax)) * ahw + Mathf.Abs(Vector2.Dot(fb, ax)) * bhl + Mathf.Abs(Vector2.Dot(rb, ax)) * bhw;
        return !(Apart(fa) || Apart(ra) || Apart(fb) || Apart(rb));
    }

    /// <summary>Whether a body's box from 0.4 to 1.6 m above <paramref name="p"/> overlaps that collider.</summary>
    static bool Overlaps(Vector3 p, Vector3 f, float hl, float hw, Collider c)
    {
        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 1.0f, new Vector3(hw, 0.6f, hl), _guardHits, Quaternion.LookRotation(f, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) if (_guardHits[i] == c) return true;
        return false;
    }

    /// <summary>Something fixed (not a vehicle, a person or an animal; not the ground, a kerb or a road's surface) in a
    /// body's box from 0.4 to 1.6 m above <paramref name="p"/>.</summary>
    bool FixedAt(Vector3 p, Vector3 f, float hl, float hw, out string what, out Collider which, Collider ignore = null)
    {
        what = null; which = null;
        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 1.0f, new Vector3(hw, 0.6f, hl), _guardHits, Quaternion.LookRotation(f, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = _guardHits[i];
            if (c == ignore || c.transform.IsChildOf(transform)) continue;
            // (vehicles are the agents' check; but an articulated vehicle's rear section, a kinematic body, counts here,
            // other than its own)
            if (c.attachedRigidbody != null && (!c.attachedRigidbody.isKinematic || !c.attachedRigidbody.name.StartsWith("Rear section")
                || (TryGetComponent<ArticulatedTrailer>(out var own) && own.Trailer != null && c.transform.IsChildOf(own.Trailer)))) continue;
            var nm = c.name;
            if (nm.StartsWith("Roads") || nm.StartsWith("Road ") || nm.StartsWith("BGT") || nm.StartsWith("Ground") || nm.StartsWith("Terrain") || nm.StartsWith("Zebra") || nm.StartsWith("Kerbs") || nm.StartsWith("Pavements") || nm.StartsWith("Cycle") || nm.StartsWith("Water")) continue;
            if (c.bounds.max.y < p.y + 0.4f) continue;
            if (c.GetComponentInParent<PlayerAvatar>() != null || c.GetComponentInParent<AnimalAgent>() != null) continue;
            what = (c.transform.parent != null ? c.transform.parent.name + "/" : "") + nm; which = c;
            return true;
        }
        return false;
    }

    /// <summary>Walks along the own path in 2 m steps and returns the closest obstacle.</summary>
    Leader FindLeader(out string what)
    {
        RefreshCaches();
        what = "free road";
        _blocker = null;
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

        // vehicles standing within 40 m, whatever their heading (one waiting across this car's lane for a cyclist, with its
        // nose in the lane, was driven into: its middle was off the path, and it faced across): their bodies, against the
        // path below
        _standing.Clear();
        for (int i = 0; i < _cars.Count; i++)
        {
            var c = _cars[i];
            if (c.t == transform || Flat2(c.pos - me) > 40f * 40f || Mathf.Abs(c.pos.y - me.y) > 3.5f) continue;
            float vc = c.tc != null ? c.tc.Speed : SpeedOf(c.t);
            if (vc >= 1f) continue;
            // (also one that gives way to this car: the one giving way then sees the ring and goes on, below; but not one
            // beside it that makes room for it, the side-by-side rule above: it waits for this one to go on ahead, and a
            // car and a cyclist merging stood beside each other for ever)
            if (c.tc != null && c.tc.LeaderDescription != null && c.tc.LeaderDescription.StartsWith("making room for ") && c.tc.LeaderDescription.EndsWith(name)) continue;
            // (two standing in each other's way, as two cars meeting in a street too narrow for both: the one placed first
            // goes on; else both waited for ever)
            if (c.tc != null && c.tc.LeaderDescription != null && c.tc.LeaderDescription.StartsWith(name + " standing in the way") && _rank < c.tc._rank) continue;
            _standing.Add(c);
        }

        // a street too narrow for this car and an oncoming one to pass: the one not yet in the narrow part waits at its
        // mouth (every 0.25 s)
        if (!IsRider && Time.time >= _nextNarrow) { _nextNarrow = Time.time + 0.25f; _narrowWait = NarrowWait(start, me, myFwd, out _narrowWhat, out _narrowFor); }
        if (!IsRider && _narrowWait < float.MaxValue) { what = _narrowWhat; _blocker = _narrowFor; return Leader.Stationary(Mathf.Max(0.1f, _narrowWait)); }

        // a rider whose line moves across the road ahead (from the kerb towards the middle, onto another line) moves over
        // only with a clear gap: not while a car close behind or beside it on that side would have to brake for it (one
        // swerved from the edge across the lane in front of the autopilot's bonnet); it keeps its line and waits
        if (IsRider && _rideBlockUntil > Time.time) { what = "keeping to its line for a car behind"; return Leader.Stationary(3f); }
        if (IsRider)
        {
            var ahead = _circuit.GetRoutePoint(start + 8f).position;
            var myRight = Vector3.Cross(Vector3.up, Flat(myFwd).normalized);
            float lat = Vector3.Dot(Flat(ahead - me), myRight);
            if (Mathf.Abs(lat) > 0.8f)
            {
                float side = Mathf.Sign(lat);
                foreach (var c in _cars)
                {
                    if (c.t == transform || RiderOf(c.tc, c.t) || Vector3.Dot(c.fwd, myFwd) < 0.5f || Mathf.Abs(c.pos.y - me.y) > 3.5f) continue;
                    var rel = Flat(c.pos - me);
                    float along = Vector3.Dot(rel, Flat(myFwd).normalized), across = Vector3.Dot(rel, myRight) * side;
                    if (along < -25f || along > 4f || across < -0.5f || across > Mathf.Abs(lat) + 2.5f) continue;
                    // (where the rider's line goes is on that car's way: within its half width, the rider's and 0.5 m of
                    // its path; in a bend both turn together, and the rider rides on)
                    if (c.tc == null || c.tc._circuit == null || c.tc._tracker == null) continue;
                    float near = float.MaxValue, cs = c.tc._tracker.ProgressDistance;
                    for (float k = 0f; k <= 30f; k += 2f) near = Mathf.Min(near, Flat2(c.tc._circuit.GetRoutePosition(cs + k) - ahead));
                    float r = HalfWidth(c.t) + 0.4f + 0.5f;
                    if (near > r * r) continue;
                    float vc = c.tc != null ? c.tc.Speed : SpeedOf(c.t);
                    // (beside it, or coming up behind it within 3 s)
                    if (along > -6f && vc > 0.5f || vc > Speed + 0.5f && (-along - 4f) / (vc - Speed) < 3f)
                    {
                        _rideBlockUntil = Time.time + 1f;
                        what = $"keeping to its line for {c.name}";
                        return Leader.Stationary(3f);
                    }
                }
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
                _blocker = c.t; return Leader.Stationary(0.1f);
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
            if (IsRider && o.IsRider && Mathf.Abs(o.LateralRight - LateralRight) >= 0.8f) continue;   // (riding beside it, or past it)
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
        // (not giving way to a vehicle that itself waits, through others, for this one: a ring, as a pickup waiting across
        // a lane for the car in it, which waited for the car ahead of it, which waited for the pickup standing in its way;
        // the one giving way goes on)
        // (only the vehicle of the lowest rank in the ring, so that two giving way in it do not both go on and meet)
        if (conflict < float.MaxValue && _conflictWith != null && _conflictWith.TryGetComponent<TrafficCar>(out var yieldTo) && WaitsFor(yieldTo, transform, out int lowest) && _rank < lowest)
        { conflict = float.MaxValue; RingsBroken++; }
        // (closer than the first sample, which is 2 m past the progress point: now; else when the samples pass it)
        if (sameCar != null && sameAlong <= conflict && sameAlong < step + offset)
        {
            what = $"car {sameCar.name} at {sameAlong:F0} m (same route)";
            _blocker = sameCar.transform; return Leader.Vehicle(Mathf.Max(0.1f, BehindRider(sameAlong - (VehicleLength + sameCar.VehicleLength) / 2f, sameCar, sameCar.transform)), sameCar.Speed);
        }
        var prevP = _circuit.GetRoutePoint(start).position;
        var prevSeg = _circuit.GetRoutePoint(start - step).position;
        float myReach = HalfWidth(transform) + 0.05f;
        float riderReach2 = (HalfWidth(transform) + 0.4f + 1.5f) * (HalfWidth(transform) + 0.4f + 1.5f);
        for (float d = step; d <= LookAhead; d += step)
        {
            var p = _circuit.GetRoutePoint(start + d).position + RideOffset(start + d);
            var segFrom = prevP; prevP = p;
            // (in a bend tighter than this vehicle can turn, a lorry or a bus, it runs wide of its path by about the
            // difference of the radii: its body sweeps that much further out, and what stands there is in its way)
            float wide = 0f;
            if (d > step && Vector3.Angle(Flat(p - segFrom), Flat(segFrom - prevSeg)) is var bend && bend > 1f)
                wide = Mathf.Clamp(MinTurnRadius - step / (bend * Mathf.Deg2Rad), 0f, 2.5f);
            prevSeg = segFrom;
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
                float gap = conflict - VehicleLength / 2f - 2f;
                // (not waiting with the body across another lane: the car stops before a lane it crosses on its way to the
                // conflict, as drivers keep a junction clear, unless it is in that lane already)
                float back = KeepLanesClear(start, offset, gap);
                if (back > 0f) what += $" (clear of the lane it crosses, {back:F0} m back)";
                _blocker = _conflictWith; return Leader.Stationary(Mathf.Max(0.1f, gap - back));
            }
            if (along >= sameAlong && sameCar != null)
            {
                what = $"car {sameCar.name} at {sameAlong:F0} m (same route)";
                _blocker = sameCar.transform; return Leader.Vehicle(Mathf.Max(0.1f, BehindRider(sameAlong - (VehicleLength + sameCar.VehicleLength) / 2f, sameCar, sameCar.transform)), sameCar.Speed);
            }

            if (along <= 42f)
                for (int i = 0; i < _standing.Count; i++)
                {
                    var c = _standing[i];
                    // (this car's body, half its width and 5 cm to either side of the path, would touch the other's; two cars
                    // in the narrowest two-way streets' lanes, 1.9 m apart, pass)
                    if (BodyDist(p, c) >= myReach + wide && BodyDist((segFrom + p) / 2f, c) >= myReach + wide) continue;
                    if (along - step < 0.5f && Vector3.Dot(Flat(c.pos - me), myFwd) < 0f) continue;   // (behind it)
                    what = $"{c.name} standing in the way at {along:F0} m";
                    _blocker = c.t; return Leader.Stationary(Mathf.Max(0.1f, BehindRider(along - step / 2f - VehicleLength / 2f, c.tc, c.t)));
                }

            for (int i = 0; i < _nearCars.Count; i++)
            {
                var c = _nearCars[i];
                // (within the lane anywhere along the 2 m since the last sample, not only near the samples: a vehicle up to
                // a metre between two of them and 0.8 m beside the line, as one overtaken on a parallel route, was missed)
                // (a rider ahead: within this car's half width, the rider's 0.4 m and 1.5 m beside the path a car does not
                // pass it but follows it: it passes a cyclist only with 1.5 m to spare)
                // (a rider and a car ahead: the rider passes it only with half a metre between the car's body and its own,
                // else it follows; by the car's middle alone a race cyclist overtook a car slowing beside it a hand's
                // breadth from its side)
                float carReach = HalfWidth(c.t) + 0.4f + 0.5f;
                float reach2 = !IsRider && RiderOf(c.tc, c.t) ? riderReach2 : IsRider && !RiderOf(c.tc, c.t) ? Mathf.Max(lw2, carReach * carReach) : lw2;
                // (and two cars: the other's body within 0.25 m of this one's along the path, not only its middle within the
                // lane: a car on a route 2 m beside this one's, the same way along a narrow street, was let pass a hand's
                // breadth from its side and the autopilot drove into it)
                bool bodyIn = !IsRider && !RiderOf(c.tc, c.t) && (BodyDist(p, c) < myReach + 0.25f || BodyDist((segFrom + p) / 2f, c) < myReach + 0.25f);
                if (SegmentDist2(c.pos, segFrom, p, out float u) < reach2 || bodyIn)
                {
                    float at = along - step * (1f - u);   // (where along the segment it is)
                    if (at < 1f) continue;   // (beside this vehicle: the side-by-side rule above)
                    float leaderSpeed = c.tc != null ? c.tc.Speed : SpeedOf(c.t);
                    what = $"car {c.name} at {at:F0} m";
                    // (centre to centre: half of each one's length; a participant's car without TrafficCar taken as 4.5 m)
                    _blocker = c.t; return Leader.Vehicle(Mathf.Max(0.1f, BehindRider(at - (VehicleLength + (c.tc != null ? c.tc.VehicleLength : 4.5f)) / 2f, c.tc, c.t)), leaderSpeed);
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
    public bool IsRider => LaneHalfWidth < 1f;

    // ---- riders: a place of their own across the path, and overtaking
    /// <summary>The map's cyclists: each keeps a place of its own across the path (0-0.5 m in from its line along the
    /// kerb) and overtakes a slower one ahead, 1.1 m out, where the path has room (a cycle path or cycle lane, the place
    /// beside clear, no one coming the other way near): Dutch cyclists rode in single file, each on the same line, and
    /// queued behind the slower ones.</summary>
    public bool RideLanes;
    /// <summary>How far (m) to the right of its route's line the rider rides now (to the left of it, negative).</summary>
    public float LateralRight;
    float _baseLateral, _lateralTarget, _overtakeUntil, _nextOvertakeCheck; Transform _overtaking; int _middleSign = -1;

    public void InitRideLane(System.Random rng, bool leftHandTraffic)
    {
        RideLanes = true;
        _middleSign = leftHandTraffic ? 1 : -1;   // (towards the middle of the path, away from the kerb)
        _baseLateral = _middleSign * (float)rng.NextDouble() * 0.5f;
        _lateralTarget = LateralRight = _baseLateral;
    }

    /// <summary>The rider's offset from its route's line at route distance s, in the world.</summary>
    Vector3 RideOffset(float s)
    {
        if (!IsRider || Mathf.Abs(LateralRight) < 0.01f || _circuit == null) return Vector3.zero;
        var d = _circuit.GetRoutePoint(s).direction; d.y = 0f;
        return d.sqrMagnitude < 1e-6f ? Vector3.zero : Vector3.Cross(Vector3.up, d.normalized) * LateralRight;
    }

    Vector3 RidePoint(float s, float lateral)
    {
        var rp = _circuit.GetRoutePoint(s); var d = rp.direction; d.y = 0f;
        return d.sqrMagnitude < 1e-6f ? rp.position : rp.position + Vector3.Cross(Vector3.up, d.normalized) * lateral;
    }

    void UpdateRideLane(float dt)
    {
        if (_circuit == null || _tracker == null) return;
        float start = _tracker.ProgressDistance;
        var me = transform.position; var fwd = Flat(transform.forward).normalized;
        if (_overtaking != null)
        {
            // past it (2.5 m ahead of it), or it has gone, or 15 s: back to its own place
            var rel = _overtaking != null ? Flat(_overtaking.position - me) : Vector3.zero;
            if (_overtaking == null || !_overtaking.gameObject.activeInHierarchy || Vector3.Dot(rel, fwd) < -2.5f || Time.time > _overtakeUntil || rel.magnitude > 20f)
            { _overtaking = null; _lateralTarget = _baseLateral; }
        }
        else if (Time.time >= _nextOvertakeCheck)
        {
            _nextOvertakeCheck = Time.time + 0.5f;
            _lateralTarget = _baseLateral;
            TrafficCar slow = null; float slowAt = float.MaxValue;
            foreach (var c in _cars)
            {
                var o = c.tc;
                if (o == null || o == this || !o.IsRider || o._circuit != _circuit || Vector3.Dot(c.fwd, fwd) < 0.5f) continue;
                var rel = Flat(c.pos - me); float along = Vector3.Dot(rel, fwd);
                if (along < 1.5f || along > 9f || Mathf.Abs(o.LateralRight - LateralRight) >= 0.8f) continue;
                // (slower than this one wants to ride, and riding: not one stopped at a light or in a queue)
                if (o.Speed < 1f || Model.DesiredSpeed < o.Speed + 0.8f) continue;
                if (along < slowAt) { slowAt = along; slow = o; }
            }
            if (slow != null)
            {
                float target = slow.LateralRight + _middleSign * 1.1f;
                bool room = true;
                for (float a = 0f; a <= 10f && room; a += 2.5f)
                {
                    var q = RidePoint(start + a, target);
                    if (MapTraffic.SeenGroundAt(q) != MapTraffic.Ground.Cycle) room = false;
                    foreach (var c in _cars)
                    {
                        if (!room) break;
                        if (c.t == transform || c.tc == slow) continue;
                        // (someone there, or coming the other way within 20 m near that place)
                        float r = Vector3.Dot(c.fwd, fwd) < -0.3f ? 1.4f : 1.0f;
                        if (Flat2(c.pos - q) < r * r) room = false;
                        else if (Vector3.Dot(c.fwd, fwd) < -0.3f && Flat2(c.pos - me) < 20f * 20f && Mathf.Abs(Vector3.Dot(Flat(c.pos - q), Vector3.Cross(Vector3.up, fwd))) < 1.3f) room = false;
                    }
                }
                if (room) { _overtaking = slow.transform; _lateralTarget = target; _overtakeUntil = Time.time + 15f; Overtakes++; }
            }
        }
        LateralRight = Mathf.MoveTowards(LateralRight, _lateralTarget, dt * 0.6f);
    }
    /// <summary>-debugTraffic: overtakes begun by the map's cyclists.</summary>
    public static int Overtakes;
    /// <summary>-debugTraffic: samples (every 0.25 s) of the moving cars, and of those more than 0.5 m and 1 m off their lane.</summary>
    public static int LaneSamples, LaneOff05, LaneOff10;
    /// <summary>Its smallest turning circle (as AICar.SteerPursuit steers it): about 4.7 m for a car, 8-9 m for a lorry or bus.</summary>
    float MinTurnRadius => Mathf.Clamp(0.6f * VehicleLength + 2f, 2.5f, 9f);
    float _rideBlockUntil;
    static bool RiderOf(TrafficCar tc, Transform t) => tc != null ? tc.LaneHalfWidth < 1f : t != null && t.GetComponent<TwoWheelerController>() != null;
    /// <summary>The gap a car keeps behind a cyclist (or a moped rider): the model's own gap less 3 m and 0.8 s at its speed, so
    /// it stays 5 m and about 2 s back and slows early, as drivers do behind a rider (a rider behind a rider: its own gap).</summary>
    float BehindRider(float gap, TrafficCar otc, Transform ot) => !IsRider && RiderOf(otc, ot) ? gap - 3f - 0.8f * Speed : gap;

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
    static readonly string DebugCar = System.Array.IndexOf(CommandLine.Args, "-debugCar") is var dci && dci >= 0 && dci + 1 < CommandLine.Args.Length ? CommandLine.Args[dci + 1] : null;
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
                if (d.sqrMagnitude > 14f * 14f) continue;
                // overlapping footprints: the two bodies (rectangles of their lengths and widths, a little smaller: 0.15 m
                // all round) overlap; by their own sizes, so a lorry's side driven into is counted (by their middles alone,
                // 3 m apart at most, it was not)
                if (BodiesOverlap(_cars[i], _cars[j], 0.15f))
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
            Debug.Log($"TrafficDebug: t={Time.time:F0} s, {_frames} frames, car-overlap frames {_overlapFrames}, yielding car-frames {_yieldFrames}, freed from obstacles {Freed}, stops kept clear of crossing lanes {KeptClear}, rings of waiting vehicles broken {RingsBroken}, steps slowed by the last check {GuardFrames}, deadlocks broken by backing up {Reversals}, overtakes by cyclists {Overtakes}, moving car samples {LaneSamples} ({LaneOff05} over 0.5 m and {LaneOff10} over 1 m off their lane), lane points eased out of too tight bends {MapTraffic.BendsEased}, cyclists moved back near the participants {MapRouteRecycler.KeptNear}, moved on past something fixed {MapRouteRecycler.MovedOn}");
            // how high the vehicles stand on the ground under them (a two-wheeler's wheels sunk into the road: below 0)
            var heights = new List<(string kind, float h)>();
            string lowest = null; float lowestH = 0f;
            foreach (var cc in _cars)
            {
                if (cc.t == null || cc.tc == null) continue;
                var hits = Physics.RaycastAll(cc.pos + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
                float ground = float.NegativeInfinity;
                foreach (var h in hits) if (h.collider.attachedRigidbody == null && h.normal.y > 0.7f && h.point.y < cc.pos.y + 1f && h.point.y > ground) ground = h.point.y;
                if (ground > float.NegativeInfinity) heights.Add((cc.tc.LaneHalfWidth < 1f ? "two-wheelers" : "cars", cc.pos.y - ground));
                if (ground > float.NegativeInfinity && cc.tc.LaneHalfWidth < 1f && cc.pos.y - ground < lowestH - 0.05f)
                {
                    lowestH = cc.pos.y - ground;
                    var gname = hits.Where(h => h.collider.attachedRigidbody == null && Mathf.Abs(h.point.y - ground) < 1e-3f).Select(h => h.collider.name).FirstOrDefault();
                    cc.t.TryGetComponent<Rigidbody>(out var rrb);
                    lowest = $"{cc.name} {lowestH:F2} m under {gname} at ({cc.pos.x:F0},{cc.pos.z:F0}), gravity {(rrb != null && rrb.useGravity)}, kinematic {(rrb != null && rrb.isKinematic)}, external {(cc.t.TryGetComponent<AICar>(out var ac) && ac.ExternalSpeedControl)}, controller {cc.t.GetComponent<TwoWheelerController>() != null}";
                }
            }
            foreach (var g in heights.GroupBy(x => x.kind))
                Debug.Log($"TrafficDebug: {g.Key} above the ground: {g.Count()}, from {g.Min(x => x.h):F2} to {g.Max(x => x.h):F2} m, mean {g.Average(x => x.h):F2} m");
            if (lowest != null) Debug.Log($"TrafficDebug: two-wheeler lowest: {lowest}");
            LogWheelContact();
            // (the vehicles indicating now, by side)
            int il = 0, ir = 0;
            foreach (var x in _cars) if (x.tc != null && x.tc._blinkers != null) { if (x.tc._blinkers.State == BlinkerState.Left) il++; else if (x.tc._blinkers.State == BlinkerState.Right) ir++; }
            Debug.Log($"TrafficDebug: indicating now: {il} left, {ir} right; pedestrians {_pedestrians.Count}, animals {_animals.Count}");
            // the vehicles the last check has held for more than 10 s (its rules wanted to go on: a deadlock, or a path
            // through something fixed)
            var heldLong = _cars.Where(x => x.tc != null && x.tc._guardSince >= 0f && Time.time - x.tc._guardSince > 10f).Select(x => x.tc).ToList();
            if (heldLong.Count > 0) Debug.Log($"TrafficDebug: held by the last check for over 10 s: {heldLong.Count}: " + string.Join("; ", heldLong.Take(8).Select(x => $"{x.name} {Time.time - x._guardSince:F0} s short of {x._guardWhat} at ({x.transform.position.x:F0},{x.transform.position.z:F0})")));
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
            // (and every vehicle stopped for 5 s and more within 60 m of the participant, with what it waits for: rings of
            // vehicles waiting for each other show here)
            var me = _cars.FirstOrDefault(x => x.t != null && x.t.GetComponent<AutomatedDriving>() != null);
            if (me.t != null)
                foreach (var cc in _cars)
                    if (cc.tc != null && cc.t != me.t && cc.tc._stoppedSince > 0f && Time.time - cc.tc._stoppedSince > 5f && Flat2(cc.pos - me.pos) < 60f * 60f)
                        Debug.Log($"TrafficDebug: near the participant, stopped {Time.time - cc.tc._stoppedSince:F0} s: {cc.name} at ({cc.pos.x:F1},{cc.pos.z:F1}): {cc.tc.LeaderDescription}");
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
    bool _offLogged; static int _offLogs;
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
            Debug.Log($"TrafficDebug: {name} held by {path} (model {Speed:F1} m/s, body {rbv:F1} m/s) at {transform.position}"
                + (c.collider.attachedRigidbody != null && c.collider.attachedRigidbody.TryGetComponent<TrafficCar>(out var other)
                    ? $"; this one ({VehicleLength:F1} m long): {LeaderDescription}; the other ({other.VehicleLength:F1} m): {other.LeaderDescription}; its last 3 s:\n  {string.Join("\n  ", _history)}" : ""));
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
        what = null; _conflictWith = null;
        float best = float.MaxValue;
        var me = transform.position;
        _ahead.Clear();
        for (float d = 2f; d <= 30f; d += 2f) _ahead.Add(_circuit.GetRoutePoint(start + d).position);
        var across = OsmMap.Current != null && OsmMap.Current.Info.LeftHandTraffic ? CarTrafficLight.TurnArrow.Right : CarTrafficLight.TurnArrow.Left;
        bool turnsAcross = TurnAfter(start) == across;
        float clearLane = (VehicleLength + 4f) / Mathf.Max(Speed, 3f) + 1.5f;
        float myHalfWidth = HalfWidth(transform);
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
            // (a vehicle standing beside the path, oncoming or across, or crossing it, is in the way where its body comes
            // within half this one's width and 0.3 m of the path, whatever its heading: by its middle alone, a cyclist rode
            // up to the nose of a pickup standing across its way, and a car turning across drove into one waiting for it)
            float myHalf = myHalfWidth + 0.3f, hlOther = (tc != null ? tc.VehicleLength : 4.5f) / 2f, hwOther = HalfWidth(c);
            for (float tt = 0f; tt <= horizon; tt += dt)
            {
                var q = c.position + fwd * Mathf.Min(vc * tt, further);
                // (by body: one standing, oncoming or across, and one crossing, at its place then; an oncoming one passing
                // in its own lane by its middle, as before)
                bool byBody = (tt == 0f && vc < 1f) || facing >= -0.7f;
                for (int k = 0; k < _ahead.Count; k++)
                {
                    if (Flat2(q - _ahead[k]) > reach && (!byBody || BodyDist(_ahead[k], q, fwd, hlOther, hwOther) >= myHalf)) continue;
                    float along = (k + 1) * 2f + offset;
                    if (along >= best) break;
                    // committed: its front is at the conflict point already, or it could not stop before it braking at
                    // 4 m/s²; it clears the junction (the other car, which has it on its path, gives way) instead of
                    // stopping in the middle of it
                    // (not for a vehicle there now, standing or crossing: that is an obstacle, not traffic that will have
                    // passed, and this car brakes for it; and not when this car stands: it can stay where it is; a tow truck
                    // reckoning itself committed drove into a pickup crossing in front of it)
                    if (tt > 0f && Speed > 0.5f && along - VehicleLength / 2f < 1f + Speed * Speed / 8f) break;
                    float mine = along / Mathf.Max(Speed, 0.5f);
                    bool slowBoth = vc < 1f && Speed < 1f;
                    // (a tie: the earlier spawned car goes first; a participant's vehicle before any traffic car)
                    int rank = tc != null ? tc._rank : int.MinValue;
                    bool theyFirst = slowBoth ? rank < _rank : tt < mine - 0.4f || Mathf.Abs(tt - mine) <= 0.4f && rank < _rank;
                    if (gap) theyFirst = tt < mine + clearLane;
                    if (theyFirst) { best = along; _conflictWith = c; what = $"yielding to {c.name} at {along:F0} m{(gap ? " (turning across)" : "")}"; }
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

    /// <summary>How much further back than <paramref name="gap"/> (from its front, along its path) this car has to stop so
    /// that, standing, its body is clear of other car lanes and cycle routes that cross its path (MapTraffic.CrossingLaneNear); 0 when
    /// the stop is clear, or when no clear place lies between the car and it (it is in that lane already).</summary>
    float _keepBack, _keepFor = float.NaN, _nextKeep;
    /// <summary>-debugTraffic: stops moved back clear of a crossing lane (counted every quarter second while they last).</summary>
    static int KeptClear;
    float KeepLanesClear(float start, float offset, float gap)
    {
        var mt = MapTraffic.Current;
        if (mt == null || gap <= 0.5f) return 0f;
        // (every quarter second, or when the place to stop at has moved by more than a metre)
        float stopAt = start - offset + gap;
        if (Time.time < _nextKeep && Mathf.Abs(stopAt - _keepFor) < 1f) return Mathf.Min(_keepBack, gap - 0.1f);
        _nextKeep = Time.time + 0.25f; _keepFor = stopAt;
        _keepBack = KeepLanesClearNow(start, offset, gap);
        if (_keepBack > 0f) KeptClear++;
        return _keepBack;
    }

    float KeepLanesClearNow(float start, float offset, float gap)
    {
        var mt = MapTraffic.Current;
        // the car's middle where it would stop (IDM keeps 2 m short of the obstacle), then back in 1 m steps
        float stop = gap - 2f + VehicleLength / 2f;
        float half = VehicleWidth / 2f;
        for (float back = 0f; stop - back >= VehicleLength / 2f; back += 1f)
        {
            float centre = stop - back;
            bool clear = true;
            for (float b = -VehicleLength / 2f; b <= VehicleLength / 2f + 0.01f && clear; b += 1f)
            {
                var rp = _circuit.GetRoutePoint(start - offset + centre + b);
                if (mt.CrossingLaneNear(rp.position, rp.direction, half, _circuit)) clear = false;
            }
            if (clear) return back;
        }
        return 0f;
    }

    /// <summary>Distance on the ground from p to the body of a vehicle (a rectangle of its length and width; 4.5 by 1.8 m
    /// for one without TrafficCar, a participant's car), 0 inside it.</summary>
    static float BodyDist(Vector3 p, CachedCar c)
    {
        float hl = (c.tc != null ? c.tc.VehicleLength : 4.5f) / 2f, hw = HalfWidth(c.t);
        return BodyDist(p, c.pos, c.fwd, hl, hw);
    }

    /// <summary>Whether the bodies of two vehicles (rectangles of their lengths and widths, each shrunk by
    /// <paramref name="inset"/> all round) overlap on the ground (separating axis test).</summary>
    static bool BodiesOverlap(CachedCar a, CachedCar b, float inset)
    {
        static void Box(CachedCar c, out Vector2 centre, out Vector2 f, out float hl, out float hw)
        {
            centre = new Vector2(c.pos.x, c.pos.z);
            f = new Vector2(c.fwd.x, c.fwd.z); if (f.sqrMagnitude < 1e-6f) f = Vector2.up; f.Normalize();
            hl = (c.tc != null ? c.tc.VehicleLength : 4.5f) / 2f; hw = HalfWidth(c.t);
        }
        Box(a, out var ca, out var fa, out var la, out var wa); Box(b, out var cb, out var fb, out var lb, out var wb);
        la -= inset; wa -= inset; lb -= inset; wb -= inset;
        var ra = new Vector2(fa.y, -fa.x); var rb = new Vector2(fb.y, -fb.x);
        var d = cb - ca;
        foreach (var axis in new[] { fa, ra, fb, rb })
        {
            float pa = la * Mathf.Abs(Vector2.Dot(fa, axis)) + wa * Mathf.Abs(Vector2.Dot(ra, axis));
            float pb = lb * Mathf.Abs(Vector2.Dot(fb, axis)) + wb * Mathf.Abs(Vector2.Dot(rb, axis));
            if (Mathf.Abs(Vector2.Dot(d, axis)) > pa + pb) return false;
        }
        return true;
    }

    /// <summary>Distance on the ground from p to a rectangle (centre, forward, half length, half width), 0 inside it.</summary>
    public static float BodyDist(Vector3 p, Vector3 centre, Vector3 forward, float halfLength, float halfWidth)
    {
        var f = new Vector3(forward.x, 0f, forward.z);
        if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
        f.Normalize();
        var r = new Vector3(f.z, 0f, -f.x);
        var d = new Vector3(p.x - centre.x, 0f, p.z - centre.z);
        float a = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(d, f)) - halfLength), b = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(d, r)) - halfWidth);
        return Mathf.Sqrt(a * a + b * b);
    }

    /// <summary>Squared distance on the ground from q to the segment a-b (t: where along it, 0 at a, 1 at b).</summary>
    static float SegmentDist2(Vector3 q, Vector3 a, Vector3 b, out float t)
    {
        float abx = b.x - a.x, abz = b.z - a.z, l2 = abx * abx + abz * abz;
        t = l2 > 1e-6f ? Mathf.Clamp01(((q.x - a.x) * abx + (q.z - a.z) * abz) / l2) : 1f;
        float dx = q.x - (a.x + abx * t), dz = q.z - (a.z + abz * t);
        return dx * dx + dz * dz;
    }
}
