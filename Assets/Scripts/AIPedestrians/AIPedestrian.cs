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

    [Header("State (read only)")]
    public CrossingPhase Phase;
    public string DecisionReason;

    static readonly List<AIPedestrian> _active = new List<AIPedestrian>();
    static readonly List<SocialAgent> _agents = new List<SocialAgent>();
    static readonly List<ApproachingVehicle> _vehicles = new List<ApproachingVehicle>();

    GapAcceptanceModel _crossingModel;
    PedestrianCrossing _crossing;
    Vector3 _crossFrom, _crossTo;
    SysVector2 _velocity;
    float _speedFactor = 1f; // from PedestrianWaypoint relative to the authored speed
    float _authoredSpeed;

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
            _crossingModel = new GapAcceptanceModel(Profile, rng);
            if (SampleWalkingSpeed)
            {
                moveSpeed = Profile.WalkingSpeed;
            }
            _velocity = ToSys(transform.forward) * moveSpeed;
        }
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
            _crossing.GetApproachingVehicles(_crossFrom, _crossTo, _vehicles);
            bool walk = _crossingModel.Update(dt, _vehicles);
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
                if (walk) PedestrianDecisionLog.Write(this, "start crossing", _crossingModel, _vehicles);
            }
            if (_crossingModel != null)
            {
                Phase = _crossingModel.Phase;
                DecisionReason = _crossingModel.Reason;
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
        float blend = animationBlendFactor * Mathf.Clamp(speed / Mathf.Max(0.1f, moveSpeed), 0f, 1.5f);
        currentBlendFactor = Damp(currentBlendFactor, blend, SpeedDampingFactor, dt);
        animator.SetFloat("Speed", currentBlendFactor);
    }

    void SnapToGround(ref Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out RaycastHit hitInfo, 2))
        {
            pos.y = Damp(pos.y, hitInfo.point.y, HeightDampingFactor, Time.deltaTime);
        }
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
                if (crossing.Zebra) profile.CriticalGap *= 0.85f;
                _crossingModel.Profile = profile;
                _crossingModel.Reset();
                _crossingModel.ArriveAtKerb();
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
