using System.Collections.Generic;
using CoupledSim.Pedestrians;
using UnityEngine;

/// <summary>
/// Keeps an estimate of position, velocity and acceleration of every car in the scene, as AI
/// pedestrians perceive them. Works for host-driven, player-driven and remote (kinematic) cars
/// alike, because it differentiates positions rather than reading rigidbody velocities.
/// </summary>
public class VehicleTracker : MonoBehaviour
{
    public struct TrackedVehicle
    {
        public PlayerAvatar Avatar;
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Velocity;
        /// <summary>Longitudinal acceleration (m/s²), smoothed.</summary>
        public float Acceleration;
        public float Speed;
        public EhmiSignal Ehmi;
        public bool Active;
    }

    static VehicleTracker _instance;
    public static VehicleTracker Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<VehicleTracker>();
                if (_instance == null)
                {
                    _instance = new GameObject("VehicleTracker").AddComponent<VehicleTracker>();
                }
            }
            return _instance;
        }
    }

    [Tooltip("How often (s) the scene is searched for newly spawned cars.")]
    public float RescanInterval = 0.5f;
    [Tooltip("Smoothing time constant (s) of the speed and acceleration estimates.")]
    public float SmoothingTime = 0.3f;

    readonly Dictionary<PlayerAvatar, TrackedVehicle> _vehicles = new Dictionary<PlayerAvatar, TrackedVehicle>();
    readonly List<PlayerAvatar> _toRemove = new List<PlayerAvatar>();
    float _nextScan;

    public IEnumerable<TrackedVehicle> Vehicles => _vehicles.Values;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
    }

    void Rescan()
    {
        foreach (var avatar in FindObjectsByType<PlayerAvatar>())
        {
            if (avatar.Type != AvatarType.Driver || _vehicles.ContainsKey(avatar)) continue;
            var t = avatar.transform;
            _vehicles[avatar] = new TrackedVehicle
            {
                Avatar = avatar,
                Position = t.position,
                Forward = t.forward,
            };
        }
    }

    void FixedUpdate()
    {
        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + RescanInterval;
            Rescan();
        }
        float dt = Time.fixedDeltaTime;
        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(1e-3f, SmoothingTime));
        _toRemove.Clear();
        var keys = new List<PlayerAvatar>(_vehicles.Keys);
        foreach (var avatar in keys)
        {
            if (avatar == null)
            {
                _toRemove.Add(avatar);
                continue;
            }
            var v = _vehicles[avatar];
            var t = avatar.transform;
            v.Active = avatar.gameObject.activeInHierarchy;
            var pos = t.position;
            var vel = (pos - v.Position) / dt;
            if (vel.sqrMagnitude > 100f * 100f) vel = v.Velocity; // teleport (respawn, pooling)
            var newVel = Vector3.Lerp(v.Velocity, vel, alpha);
            float newSpeed = Vector3.Dot(newVel, t.forward);
            float acc = (newSpeed - v.Speed) / dt;
            v.Acceleration = Mathf.Lerp(v.Acceleration, acc, alpha);
            v.Velocity = newVel;
            v.Speed = newSpeed;
            v.Position = pos;
            v.Forward = t.forward;
            v.Ehmi = ReadEhmi(avatar);
            _vehicles[avatar] = v;
        }
        foreach (var r in _toRemove) _vehicles.Remove(r);
    }

    static EhmiSignal ReadEhmi(PlayerAvatar avatar)
    {
        var slots = avatar.HMISlots;
        var signal = EhmiSignal.None;
        foreach (var hmi in new[] { slots.HoodHMI, slots.TopHMI, slots.WindshieldHMI })
        {
            if (hmi == null) continue;
            if (hmi.CurrentState == HMIState.WALK) return EhmiSignal.Walk;
            if (hmi.CurrentState == HMIState.STOP) signal = EhmiSignal.DontWalk;
        }
        return signal;
    }
}
