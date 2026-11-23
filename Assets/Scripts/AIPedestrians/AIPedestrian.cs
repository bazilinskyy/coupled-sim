using System.Collections.Generic;
using CoupledSim.Pedestrians;
using UnityEngine;
using UnityStandardAssets.Utility;
using SysVector2 = System.Numerics.Vector2;

public class AIPedestrian : MonoBehaviour
{
    public enum BehaviourMode
    {
        /// <summary>Original behaviour: follow the waypoints at the authored speed, ignore traffic and people.</summary>
        WaypointsOnly,
        /// <summary>Individual walking speed, social-force avoidance, gap acceptance at <see cref="PedestrianCrossing"/>s.</summary>
        BehaviourModels,
    }

    WaypointProgressTracker _tracker;
    public float moveSpeed = 1.6f;
    public float animationBlendFactor = 1f;
    float currentBlendFactor = 1f;
    public float HeightDampingFactor = 0.05f;
    public float SpeedDampingFactor = 0.05f;
    public Animator animator;

    [Header("Behaviour")]
    [Tooltip("WaypointsOnly keeps old experiments exactly as they were.")]
    public BehaviourMode Behaviour = BehaviourMode.WaypointsOnly;
    public AgeGroup AgeGroup = AgeGroup.Adult;
    [Tooltip("Seed for this pedestrian's individual parameters; -1 derives it from the object name, so trials repeat.")]
    public int Seed = -1;
    [Tooltip("Use the sampled walking speed instead of moveSpeed (PedestrianWaypoint speed changes still apply as factors).")]
    public bool SampleWalkingSpeed = true;
    public PedestrianProfile Profile = PedestrianProfile.Default;
    public SocialForceParameters SocialForce = SocialForceParameters.Default;

    [Header("Animation")]
    [Tooltip("Blend and playback speed follow the actual walking speed, so the feet do not slide.")]
    public bool MatchAnimationToSpeed = true;
    [Tooltip("Head and eyes follow approaching traffic at the kerb and while crossing (needs IK Pass on the animator layer).")]
    public bool LookAtTraffic = true;
    [Tooltip("Place the feet on kerbs and uneven ground (needs IK Pass on the animator layer).")]
    public bool FootIK = true;
    [Tooltip("Walking speed (m/s) of the walk animation at full blend; 0 = measure it from the clip.")]
    public float WalkClipSpeed = 0f;

    [Header("State (read only)")]
    public CrossingPhase Phase;
    public string DecisionReason;

    static readonly List<AIPedestrian> _active = new List<AIPedestrian>();
    static readonly List<SocialAgent> _agents = new List<SocialAgent>();
    static readonly List<ApproachingVehicle> _vehicles = new List<ApproachingVehicle>();
    static readonly List<Vector3> _vehiclePositions = new List<Vector3>();

    GapAcceptanceModel _crossingModel;
    PedestrianCrossing _crossing;
    float _redWait;
    Vector3 _crossFrom, _crossTo;
    SysVector2 _velocity;
    float _speedFactor = 1f; // from PedestrianWaypoint relative to the authored speed
    float _authoredSpeed;

    Vector3 _lookTarget;
    float _lookWeight;
    float _scanTimer;
    int _scanSide = 1;

    public void Init(WaypointCircuit circuit)
    {
        enabled = true;
        _tracker = GetComponent<WaypointProgressTracker>();
        _tracker.enabled = true;
        _tracker.Init(circuit);
        foreach (var waypoint in circuit.Waypoints)
        {
            var speedSettings = waypoint.GetComponent<PedestrianWaypoint>();
            if (speedSettings != null)
            {
                speedSettings.target = this;
            }
        }
        currentBlendFactor = animationBlendFactor;
        _authoredSpeed = Mathf.Max(0.01f, moveSpeed);

        if (Behaviour == BehaviourMode.BehaviourModels)
        {
            int seed = Seed >= 0 ? Seed : StableHash(name);
            var rng = new System.Random(seed);
            Profile = PedestrianProfile.Sample(rng, AgeGroup);
            Profile.SampleSignalCompliance(new System.Random(seed ^ 0x5eed51));
            _crossingModel = new GapAcceptanceModel(Profile, rng);
            if (SampleWalkingSpeed)
            {
                moveSpeed = Profile.WalkingSpeed;
            }
            _velocity = ToSys(transform.forward) * moveSpeed;
            if (WalkClipSpeed <= 0f) WalkClipSpeed = MeasureWalkClipSpeed();
        }
    }

    // Speed of the walk clip: humanoid clips store their root speed normalised by the avatar's scale.
    float MeasureWalkClipSpeed()
    {
        float speed = 0f;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.ToLower().Contains("walk"))
                {
                    float s = clip.averageSpeed.magnitude * (animator.isHuman ? animator.humanScale : 1f);
                    speed = Mathf.Max(speed, s);
                }
            }
        }
        return speed > 0.3f ? speed : 1.4f; // in-place clips: typical walking speed
    }

    static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    void OnEnable() => _active.Add(this);
    void OnDisable() => _active.Remove(this);

    // Smoothing rate dictates the proportion of source remaining after one second
    public static float Damp(float source, float target, float smoothing, float dt)
    {
        return Mathf.Lerp(source, target, 1 - Mathf.Pow(smoothing, dt));
    }

    private void Update()
    {
        if (_tracker == null || _tracker.target == null) return;
        if (Behaviour == BehaviourMode.WaypointsOnly)
        {
            UpdateWaypointsOnly();
        }
        else
        {
            UpdateBehaviourModels(Time.deltaTime);
        }
    }

    void UpdateWaypointsOnly()
    {
        currentBlendFactor = Damp(currentBlendFactor, animationBlendFactor, SpeedDampingFactor, Time.deltaTime);
        animator.SetFloat("Speed", currentBlendFactor);
        var steer = Quaternion.LookRotation(_tracker.target.position - transform.position, Vector3.up).eulerAngles;
        var rot = transform.eulerAngles;
        rot.y = steer.y;
        transform.eulerAngles = rot;
        var pos = transform.position;
        pos += transform.forward * moveSpeed * Time.deltaTime;
        SnapToGround(ref pos);
        transform.position = pos;
    }

    void UpdateBehaviourModels(float dt)
    {
        if (dt <= 0) return;
        Vector3 desiredDir;
        float desiredSpeed = moveSpeed * _speedFactor;

        if (_crossing != null)
        {
            _crossing.GetApproachingVehicles(_crossFrom, _crossTo, _vehicles, _vehiclePositions);
            var phaseBefore = _crossingModel.Phase;
            // a signalised crossing: wait for green (opportunists: until their patience with the red runs out)
            bool held = false;
            var sig = _crossing.Signal;
            if (sig != null && phaseBefore != CrossingPhase.Crossing)
            {
                bool green = sig.State == LightState.GREEN, blink = sig.State == LightState.BLINK_GREEN;
                if (green) _redWait = 0f; else _redWait += dt;
                held = !Profile.MayStartAtSignal(green, blink, _redWait);
            }
            bool walk = !held && _crossingModel.Update(dt, _vehicles);
            if (walk && phaseBefore != CrossingPhase.Crossing)
            {
                PedestrianDecisionLog.Write(this, "start crossing", _crossingModel, _vehicles);
            }
            if (_crossingModel.Phase == CrossingPhase.Crossing)
            {
                desiredDir = _crossTo - transform.position;
                desiredSpeed = Profile.CrossingSpeed * _speedFactor * _crossingModel.CrossingSpeedFactor(_vehicles);
                var flat = desiredDir; flat.y = 0;
                if (flat.magnitude < _crossing.ArrivalRadius)
                {
                    _crossingModel.FinishCrossing();
                    PedestrianDecisionLog.Write(this, "crossed", _crossingModel, _vehicles);
                    _crossing = null;
                }
            }
            else
            {
                // wait at the kerb, facing the road
                desiredDir = _crossTo - transform.position;
                desiredSpeed = 0f;
            }
            if (_crossingModel != null)
            {
                Phase = _crossingModel.Phase;
                DecisionReason = held ? "waiting for green" : _crossingModel.Reason;
            }
        }
        else
        {
            desiredDir = _tracker.target.position - transform.position;
            Phase = CrossingPhase.Approaching;
        }
        desiredDir.y = 0;

        // social force: other AI pedestrians and player-controlled pedestrians
        _agents.Clear();
        int self = -1;
        foreach (var p in _active)
        {
            if (p == this) self = _agents.Count;
            _agents.Add(new SocialAgent { Position = ToSys(p.transform.position), Velocity = p._velocity, Radius = p.Profile.Radius });
        }
        var system = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem : null;
        if (system != null)
        {
            foreach (var ped in system.Pedestrians)
            {
                if (ped == null) continue;
                _agents.Add(new SocialAgent { Position = ToSys(ped.transform.position), Velocity = SysVector2.Zero, Radius = 0.25f });
            }
        }
        // and the vehicles near (within 8 m, off a crossing): each as circles along its body, so a pedestrian steps round a
        // car standing across the pavement (a driveway) rather than walking into it
        if (_crossing == null && VehicleTracker.Instance != null)
        {
            var here = transform.position;
            foreach (var v in VehicleTracker.Instance.Vehicles)
            {
                if (v.Avatar == null || (v.Position - here).sqrMagnitude > 8f * 8f || Mathf.Abs(v.Position.y - here.y) > 2.5f) continue;
                float len = 4.5f, wid = 1.8f;
                if (v.Avatar.TryGetComponent<TrafficCar>(out var tc)) { len = tc.IsRider ? 1.8f : tc.VehicleLength; wid = tc.IsRider ? 0.6f : tc.VehicleWidth; }
                var f = v.Forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) continue; f.Normalize();
                int k = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(wid, 0.5f)));
                for (int j = 0; j < k; j++)
                {
                    var c = v.Position + f * ((j + 0.5f) / k - 0.5f) * Mathf.Max(0f, len - wid);
                    _agents.Add(new SocialAgent { Position = ToSys(c), Velocity = ToSys(v.Velocity), Radius = wid / 2f + 0.1f });
                }
            }
        }
        var me = new SocialAgent { Position = ToSys(transform.position), Velocity = _velocity, Radius = Profile.Radius };
        var acc = SocialForceModel.Acceleration(me, self, ToSys(desiredDir), desiredSpeed, _agents, null, SocialForce);
        _velocity = SocialForceModel.Integrate(_velocity, acc, Mathf.Max(desiredSpeed, 0.1f), dt, SocialForce);
        if (desiredSpeed <= 0.01f && _velocity.Length() < 0.05f) _velocity = SysVector2.Zero;

        var pos = transform.position;
        pos.x += _velocity.X * dt;
        pos.z += _velocity.Y * dt;
        SnapToGround(ref pos);
        transform.position = pos;

        // face the walking direction, or the road while waiting
        var look = _velocity.Length() > 0.2f ? new Vector3(_velocity.X, 0, _velocity.Y) : desiredDir;
        if (look.sqrMagnitude > 1e-4f)
        {
            var target = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-dt * 6f));
        }

        float speed = _velocity.Length();
        float reference = MatchAnimationToSpeed ? WalkClipSpeed : Mathf.Max(0.1f, moveSpeed);
        float blend = animationBlendFactor * Mathf.Clamp01(speed / reference);
        currentBlendFactor = Damp(currentBlendFactor, blend, SpeedDampingFactor, dt);
        animator.SetFloat("Speed", currentBlendFactor);
        // faster than the clip: play it faster instead of sliding
        animator.speed = MatchAnimationToSpeed && speed > reference ? Mathf.Min(2f, speed / reference) : 1f;

        UpdateLookTarget(dt, desiredDir);
    }

    // Where the pedestrian looks: the most urgent approaching car at the kerb (or scanning both
    // ways), a close non-yielding car while crossing, otherwise ahead.
    float _glanceTimer = 2f, _glanceUntil; Vector3 _glanceAt; System.Random _glanceRng;

    void UpdateLookTarget(float dt, Vector3 desiredDir)
    {
        var head = transform.position + Vector3.up * 1.6f;
        Vector3 target = head + (desiredDir.sqrMagnitude > 1e-4f ? desiredDir.normalized : transform.forward) * 10f;
        float weight = 0.3f;
        var phase = _crossingModel != null ? _crossingModel.Phase : CrossingPhase.Approaching;
        if (_crossing != null && (phase == CrossingPhase.Waiting || phase == CrossingPhase.Starting || phase == CrossingPhase.Crossing))
        {
            int best = -1;
            float bestTta = phase == CrossingPhase.Crossing ? 4f : 30f;
            for (int i = 0; i < _vehicles.Count && i < _vehiclePositions.Count; i++)
            {
                var a = _crossingModel.Assess(_vehicles[i], _crossingModel.WaitingTime);
                if (phase == CrossingPhase.Crossing && a.Yielding) continue;
                if (a.TimeToArrival < bestTta) { bestTta = a.TimeToArrival; best = i; }
            }
            if (best >= 0)
            {
                target = _vehiclePositions[best] + Vector3.up * 1.2f;
                weight = 1f;
            }
            else if (phase != CrossingPhase.Crossing)
            {
                // no traffic: look left and right along the road
                _scanTimer -= dt;
                if (_scanTimer <= 0f) { _scanSide = -_scanSide; _scanTimer = 1.2f; }
                var across = _crossTo - _crossFrom; across.y = 0;
                var along = Vector3.Cross(Vector3.up, across.normalized);
                target = head + along * (15f * _scanSide) + across.normalized * 3f;
                weight = 1f;
            }
        }
        else if (_crossing == null)
        {
            // walking along: now and then a glance (every 3-8 s, for about a second) at a vehicle passing close by, or
            // aside, as people walking along a street do, rather than staring ahead
            _glanceTimer -= dt;
            if (_glanceTimer <= 0f)
            {
                _glanceTimer = 3f + (float)(_glanceRng ??= new System.Random(StableHash(name) ^ 0x6a1))!.NextDouble() * 5f;
                _glanceUntil = Time.time + 0.8f + (float)_glanceRng.NextDouble() * 0.7f;
                _glanceAt = Vector3.zero;
                float bestD = 20f;
                if (VehicleTracker.Instance != null)
                    foreach (var v in VehicleTracker.Instance.Vehicles)
                    {
                        if (!v.Active || v.Velocity.magnitude < 2f) continue;
                        var rel = v.Position - transform.position; rel.y = 0f;
                        if (rel.magnitude < bestD && Vector3.Dot(rel.normalized, transform.forward) > -0.3f) { bestD = rel.magnitude; _glanceAt = v.Position + Vector3.up * 1.1f; }
                    }
                if (_glanceAt == Vector3.zero)
                    _glanceAt = head + (transform.forward + transform.right * (_glanceRng.NextDouble() < 0.5 ? -1.2f : 1.2f)).normalized * 6f + Vector3.down * 0.3f;
            }
            if (Time.time < _glanceUntil && _glanceAt != Vector3.zero) { target = _glanceAt; weight = 0.6f; }
        }
        _lookTarget = Vector3.Lerp(_lookTarget == Vector3.zero ? target : _lookTarget, target, 1f - Mathf.Exp(-dt * 5f));
        _lookWeight = Mathf.Lerp(_lookWeight, weight, 1f - Mathf.Exp(-dt * 3f));
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (Behaviour != BehaviourMode.BehaviourModels || animator == null) return;
        if (LookAtTraffic && _lookWeight > 0.01f)
        {
            animator.SetLookAtWeight(_lookWeight, 0.15f, 0.8f, 1f, 0.5f);
            animator.SetLookAtPosition(_lookTarget);
        }
        if (FootIK)
        {
            PlaceFoot(AvatarIKGoal.LeftFoot);
            PlaceFoot(AvatarIKGoal.RightFoot);
        }
    }

    // Shifts a foot by the difference between the ground under it and under the body (kerbs, slopes).
    void PlaceFoot(AvatarIKGoal foot)
    {
        var p = animator.GetIKPosition(foot);
        if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 1.0f, ~0, QueryTriggerInteraction.Ignore)) return;
        float delta = hit.point.y - transform.position.y;
        if (Mathf.Abs(delta) < 0.02f || Mathf.Abs(delta) > 0.35f) return;
        // feet in the air (swing phase) are higher above the root: apply less
        float lift = p.y - transform.position.y;
        float w = Mathf.Clamp01(1f - lift / 0.2f);
        animator.SetIKPositionWeight(foot, w);
        animator.SetIKPosition(foot, p + Vector3.up * delta);
    }

    static readonly RaycastHit[] _groundHits = new RaycastHit[12];

    /// <summary>Onto the ground under the pedestrian: the highest surface no more than a kerb's step (0.35 m) above
    /// their feet, not a trigger, a vehicle or their own body (the first collider under a ray from 1 m up put them on top
    /// of a bollard or a tree's pit edge they walked past, in a jump).</summary>
    void SnapToGround(ref Vector3 pos)
    {
        int n = Physics.RaycastNonAlloc(pos + Vector3.up, Vector3.down, _groundHits, 2f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            var h = _groundHits[i];
            if (h.collider.attachedRigidbody != null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.point.y > pos.y + 0.35f || h.point.y <= best) continue;
            best = h.point.y;
        }
        if (best > float.NegativeInfinity) pos.y = Damp(pos.y, best, HeightDampingFactor, Time.deltaTime);
    }

    static SysVector2 ToSys(Vector3 v) => new SysVector2(v.x, v.z);

    void OnTriggerEnter(Collider other)
    {
        if (Behaviour == BehaviourMode.BehaviourModels)
        {
            var crossing = other.GetComponent<PedestrianCrossing>();
            if (crossing != null && _crossing == null && _crossingModel != null)
            {
                _crossing = crossing;
                // cross from wherever we reached the kerb, perpendicular-ish to the far kerb point
                _crossFrom = transform.position;
                _crossTo = crossing.FarPoint;
                var profile = Profile;
                if (crossing.Zebra && crossing.Signal == null) profile.CriticalGap *= 0.85f;   // drivers give way at a zebra, not at a green light
                _crossingModel.Profile = profile;
                _crossingModel.Reset();
                _crossingModel.ArriveAtKerb();
                _redWait = 0f;
                PedestrianDecisionLog.Write(this, "arrived at kerb", _crossingModel, _vehicles);
                return;
            }
        }

        PedestrianWaypoint speedSettings = other.GetComponent<PedestrianWaypoint>();
        if (speedSettings == null || (speedSettings.target != null && speedSettings.target != this))
        {
            return;
        }
        if (Behaviour == BehaviourMode.BehaviourModels && SampleWalkingSpeed)
        {
            // keep the authored speed change, relative to this pedestrian's own walking speed
            _speedFactor = speedSettings.targetSpeed / _authoredSpeed;
        }
        else
        {
            moveSpeed = speedSettings.targetSpeed;
        }
        animationBlendFactor = speedSettings.targetBlendFactor;
    }
}
