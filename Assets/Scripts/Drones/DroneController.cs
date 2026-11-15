using System;
using System.Globalization;
using System.IO;
using CoupledSim.Drones;
using UnityEngine;
using SVec3 = System.Numerics.Vector3;

/// <summary>
/// A drone flown by a participant (or an AI, through <see cref="Fly"/>), with the quadrotor dynamics and cascaded flight
/// controller of <see cref="Quadrotor"/> (rigid body, rotor thrust and torque ∝ ω², motor lag, drag; PX4-style
/// position → velocity → attitude → rate control), in the scene's wind with Dryden turbulence.
/// Participant controls (consumer drones' "mode 2"), in position mode: forward/back W/S, sideways A/D, climb and
/// descend ↑/↓ (or Space/Left Shift), turn ←/→ (or Q/E); releasing the sticks holds the position. -droneMode atti flies
/// in attitude mode (the sticks tilt the drone; it drifts with the wind). Views (C cycles; -droneView fpv|los|chase):
/// the stabilised gimbal camera (FPV, the default), the pilot's line of sight from the take-off point, a chase camera.
/// Hitting a wall or obstacle is a crash (rotors off, it falls). Gamepad axes can be set in the Inspector.
/// The flight is logged to ExperimentLogs/Drone-{date}.csv (50 Hz).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    public string Preset = "Consumer";
    public QuadrotorParameters Parameters = QuadrotorParameters.Consumer;
    [Tooltip("Read the participant's input (off for AI and remote drones).")]
    public bool ReadInput = true;
    [Tooltip("Sticks set by a script (tests, external devices): climb, yaw, forward, lateral, each -1..1.")]
    public bool UseExternalInput;
    public Vector4 ExternalSticks;
    public DroneMode PilotMode = DroneMode.Velocity;
    [Tooltip("Simulate here (off on clients: the pose comes from the host).")]
    public bool Simulate = true;

    [Header("Input axes (empty: the keyboard)")]
    public string ClimbAxis = "", YawAxis = "", ForwardAxis = "", LateralAxis = "";

    public enum View { Fpv, LineOfSight, Chase }
    [Header("Camera")]
    [Tooltip("Fpv: the gimbal camera's feed (what pilots watch on the controller); LineOfSight: from the pilot's eyes at the take-off point (visual line of sight); Chase: behind the drone.")]
    public View CameraView = View.Fpv;
    public Transform CameraRig;
    [Tooltip("Chase camera distance and height for the consumer drone (scaled with the airframe).")]
    public float ChaseDistance = 1.3f, ChaseHeight = 0.4f;

    [Header("Parts")]
    public Transform[] Propellers;
    public Renderer[] PropellerDiscs;

    [Header("State (read only)")]
    public float SpeedKmh;
    public float HeightAboveGround;
    public float BladePassingHz;
    public bool Landed = true;
    [Tooltip("Hit something (a wall, a tree, a vehicle): the rotors stop and it falls; lift off again with the climb stick once on the ground.")]
    public bool Crashed;
    public int Crashes;

    public Quadrotor Model { get; private set; }
    public float ThrustRatio => Model == null ? 0f : Model.State.Rotors.LengthSquared() / 4f / (Parameters.HoverRotorSpeed * Parameters.HoverRotorSpeed);

    DrydenTurbulence _gusts;
    DroneCommand? _ai;
    SVec3 _hold;
    Vector3 _pilot;
    bool _holding;
    float _propAngle;
    StreamWriter _log;
    float _nextLog;
    Collider[] _own;
    Rigidbody _rb;
    readonly RaycastHit[] _hits = new RaycastHit[8];

    /// <summary>An autopilot's command (AI drones); it then ignores the sticks.</summary>
    public void Fly(DroneCommand command) => _ai = command;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        Parameters = QuadrotorParameters.Preset(Preset);
        if (Propellers == null || Propellers.Length < 4 || Propellers[0] == null)
        {
            var (props, discs) = DroneBody.Build(transform, Parameters);
            Propellers = props; PropellerDiscs = discs;
        }
        _own = GetComponentsInChildren<Collider>();
        if (GetComponent<DroneSound>() == null) gameObject.AddComponent<DroneSound>();
    }

    void Start()
    {
        // clients show the host's poses as they come
        if (!Simulate) _rb.interpolation = RigidbodyInterpolation.None;
        var p = transform.position;
        Model = new Quadrotor(Parameters, QuadrotorState.AtRest(DroneBody.ToModel(p), DroneBody.YawFromHeading(transform.eulerAngles.y)));
        _gusts = new DrydenTurbulence(name.GetHashCode() ^ (int)(transform.position.x * 131 + transform.position.z * 977));
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-droneMode");
        if (i >= 0 && i + 1 < args.Length && args[i + 1].ToLowerInvariant().StartsWith("att")) PilotMode = DroneMode.Attitude;
        i = Array.IndexOf(args, "-droneView");
        if (i >= 0 && i + 1 < args.Length)
        {
            var v = args[i + 1].ToLowerInvariant();
            CameraView = v == "chase" ? View.Chase : v == "los" || v == "lineofsight" ? View.LineOfSight : View.Fpv;
        }
        _pilot = transform.position + Vector3.up * 1.7f - transform.forward * 2f;
    }

    void OnDestroy() => _log?.Dispose();

    float Axis(string name, KeyCode plus, KeyCode minus, KeyCode plus2 = KeyCode.None, KeyCode minus2 = KeyCode.None)
    {
        float v = 0f;
        if (!string.IsNullOrEmpty(name)) { try { v = Input.GetAxis(name); } catch (ArgumentException) { } }
        if (Input.GetKey(plus) || (plus2 != KeyCode.None && Input.GetKey(plus2))) v += 1f;
        if (Input.GetKey(minus) || (minus2 != KeyCode.None && Input.GetKey(minus2))) v -= 1f;
        return Mathf.Clamp(v, -1f, 1f);
    }

    Vector4 Sticks()
    {
        if (UseExternalInput) return ExternalSticks;
        if (!ReadInput) return Vector4.zero;
        return new Vector4(
            Axis(ClimbAxis, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.Space, KeyCode.LeftShift),
            Axis(YawAxis, KeyCode.RightArrow, KeyCode.LeftArrow, KeyCode.E, KeyCode.Q),
            Axis(ForwardAxis, KeyCode.W, KeyCode.S),
            Axis(LateralAxis, KeyCode.D, KeyCode.A));
    }

    void Update()
    {
        if (ReadInput && Input.GetKeyDown(KeyCode.C)) CameraView = (View)(((int)CameraView + 1) % 3);
        if (Model == null) return;
        // propellers: spin, and show their disc when fast
        var r = Model.State.Rotors;
        SpinPropellers(Simulate ? (r.X + r.Y + r.Z + r.W) / 4f : (Landed ? 0f : Parameters.HoverRotorSpeed));
    }

    /// <summary>Turns the propellers at a rotor speed (rad/s) and shows their discs when fast; also called by
    /// <see cref="DroneSound"/> for other participants' drones, whose controller is off.</summary>
    public void SpinPropellers(float w)
    {
        _propAngle = (_propAngle + w * Time.deltaTime * Mathf.Rad2Deg) % 360f;
        if (Propellers != null)
            for (int i = 0; i < Propellers.Length; i++)
            {
                if (Propellers[i] == null) continue;
                Propellers[i].localRotation = Quaternion.Euler(0, (i % 2 == 0 ? 1 : -1) * _propAngle, 0);
                if (PropellerDiscs != null && i < PropellerDiscs.Length && PropellerDiscs[i] != null) PropellerDiscs[i].enabled = w > 60f;
            }
    }

    void FixedUpdate()
    {
        if (Model == null || !Simulate) return;
        float dt = Time.fixedDeltaTime;
        var s = Model.State;
        var pos = DroneBody.ToUnity(s.Position);
        // the ground (or roof) under the drone
        float ground = pos.y - 50f;
        int n = Physics.RaycastNonAlloc(pos + Vector3.up * 0.5f, Vector3.down, _hits, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            if (Array.IndexOf(_own, _hits[i].collider) >= 0 || _hits[i].rigidbody != null) continue;
            if (_hits[i].point.y > best) best = _hits[i].point.y;
        }
        if (best > float.MinValue) ground = best;
        Model.GroundHeight = ground;
        HeightAboveGround = pos.y - ground;
        // wind: the environment's, with Dryden gusts for the height (W20 = the wind at 6 m)
        var env = EnvironmentController.Instance;
        var mean = env != null ? DroneBody.ToModel(env.Wind) : SVec3.Zero;
        float airspeed = (s.Velocity - mean).Length();
        Model.Wind = mean + _gusts.Step(dt, Mathf.Max(1f, HeightAboveGround), env != null ? env.WindSpeed : 0f, airspeed);
        var before = DroneBody.ToUnity(s.Position);
        Model.Command = _ai ?? PilotCommand(Sticks(), dt);
        if (Crashed)
        {
            // down to the ground with the rotors off; then ready to fly again
            if (Model.State.OnGround) Crashed = false;
            else Model.Command = new DroneCommand { Mode = DroneMode.Off };
        }
        Model.Step(dt);
        s = Model.State;
        // collisions with the world (a sphere of the drone's size along the step)
        var after = DroneBody.ToUnity(s.Position);
        var step = after - before;
        float radius = Parameters.ArmLength + Parameters.RotorRadius * 0.5f;
        if (!s.OnGround && step.sqrMagnitude > 1e-6f &&
            Physics.SphereCast(before + Vector3.up * radius, radius * 0.8f, step.normalized, out var hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore) &&
            Array.IndexOf(_own, hit.collider) < 0 && hit.normal.y < 0.7f)
        {
            // a wall or an obstacle: stop against it, rotors off
            var back = hit.point + hit.normal * radius - Vector3.up * radius;
            var st = Model.State;
            st.Position = DroneBody.ToModel(new Vector3(back.x, after.y, back.z));
            st.Velocity = SVec3.Zero; st.Rates = SVec3.Zero;
            Model.State = st;
            s = st;
            if (!Crashed) { Crashed = true; Crashes++; Debug.Log($"{name}: crashed into {hit.collider.name} at {hit.point} ({SpeedKmh:F0} km/h)"); }
        }
        // through the kinematic body, so that its interpolation gives smooth motion between physics steps
        _rb.MovePosition(DroneBody.ToUnity(s.Position));
        _rb.MoveRotation(DroneBody.ToUnity(s));
        SpeedKmh = s.Velocity.Length() * 3.6f;
        BladePassingHz = Model.BladePassingFrequency;
        Landed = s.OnGround;
        if (ReadInput && !UseExternalInput || (UseExternalInput && _ai == null)) LogFlight();
    }

    /// <summary>The participant's sticks to a command: velocities in the heading's frame (position mode, holding the
    /// position when released) or tilts (attitude mode).</summary>
    DroneCommand PilotCommand(Vector4 st, float dt)
    {
        var p = Parameters;
        var s = Model.State;
        float yaw = s.Yaw;
        var fwd = new SVec3(Mathf.Cos(yaw), Mathf.Sin(yaw), 0); var right = new SVec3(Mathf.Sin(yaw), -Mathf.Cos(yaw), 0);
        float climb = st.x >= 0 ? st.x * p.MaxClimb : st.x * p.MaxDescent;
        var cmd = new DroneCommand { UseYawRate = true, YawRate = -st.y * p.MaxYawRate };   // right turns clockwise (negative yaw)
        if (s.OnGround && st.x <= 0.05f) { cmd.Mode = DroneMode.Off; _holding = false; return cmd; }
        if (PilotMode == DroneMode.Attitude)
        {
            cmd.Mode = DroneMode.Attitude;
            cmd.Pitch = st.z * p.MaxTilt; cmd.Roll = st.w * p.MaxTilt; cmd.Climb = climb;
            return cmd;
        }
        var v = fwd * (st.z * p.MaxSpeed) + right * (st.w * p.MaxSpeed) + new SVec3(0, 0, climb);
        bool still = Mathf.Abs(st.x) < 0.05f && Mathf.Abs(st.z) < 0.05f && Mathf.Abs(st.w) < 0.05f;
        if (still)
        {
            // sticks centred: hold where it came to rest (braking first)
            if (!_holding) { _hold = s.Position + s.Velocity * 0.6f; _holding = true; }
            cmd.Mode = DroneMode.Position; cmd.Position = _hold;
        }
        else { _holding = false; cmd.Mode = DroneMode.Velocity; cmd.Velocity = v; }
        return cmd;
    }

    void LateUpdate()
    {
        if (CameraRig == null || Model == null) return;
        var s = Model.State;
        float heading = transform.eulerAngles.y;
        if (CameraView == View.LineOfSight)
        {
            // the pilot, standing 2 m behind the take-off point, follows the drone with the eyes
            CameraRig.SetPositionAndRotation(_pilot, Quaternion.LookRotation(transform.position - _pilot, Vector3.up));
        }
        else if (CameraView == View.Fpv)
        {
            // the gimbal: under the nose, below the propellers, level (pitch and roll stabilised), turning with the drone,
            // tilted a little down
            float size = Parameters.ArmLength / 0.19f;
            CameraRig.SetPositionAndRotation(transform.position + Quaternion.Euler(0, heading, 0) * new Vector3(0, 0.005f * size, 0.2f * size),
                                             Quaternion.Euler(12f, heading, 0));
        }
        else
        {
            float size = Parameters.ArmLength / 0.19f;
            var back = Quaternion.Euler(0, heading, 0) * new Vector3(0, ChaseHeight * size, -ChaseDistance * size);
            var target = transform.position + back;
            CameraRig.position = Vector3.Lerp(CameraRig.position, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
            // not through walls or roofs: when something is between the drone and the camera, look down from above it,
            // else stay in front of the obstacle
            var from = transform.position + Vector3.up * 0.3f; var dir = CameraRig.position - from;
            if (Blocked(from, dir, out float free))
            {
                var above = transform.position + Quaternion.Euler(0, heading, 0) * new Vector3(0, 2.5f * size, -1.2f * size);
                if (!Blocked(from, above - from, out _)) CameraRig.position = above;
                else CameraRig.position = from + dir.normalized * Mathf.Max(0.5f, free - 0.1f);
            }
            CameraRig.rotation = Quaternion.LookRotation(transform.position + Vector3.up * 0.3f - CameraRig.position, Vector3.up);
        }
    }

    bool Blocked(Vector3 from, Vector3 dir, out float free)
    {
        free = dir.magnitude;
        var hits = Physics.SphereCastAll(from, 0.2f, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
        bool any = false;
        foreach (var h in hits)
            if (Array.IndexOf(_own, h.collider) < 0 && h.distance < free) { free = h.distance; any = true; }
        return any;
    }

    void LogFlight()
    {
        if (Time.time < _nextLog) return;
        _nextLog = Time.time + 0.02f;
        if (_log == null)
        {
            Directory.CreateDirectory("ExperimentLogs");
            _log = new StreamWriter("ExperimentLogs/Drone-" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv");
            _log.WriteLine("time,x,y,z,vx,vy,vz,heading,pitch,roll,height_above_ground,rotor1,rotor2,rotor3,rotor4,blade_passing_hz,mode,climb,yaw,forward,lateral");
        }
        var ci = CultureInfo.InvariantCulture;
        var s = Model.State; var p = transform.position; var v = DroneBody.ToUnity(s.Velocity); var e = transform.eulerAngles; var st = Sticks();
        string F(float x) => x.ToString("F3", ci);
        _log.WriteLine(string.Join(",", F(Time.time), F(p.x), F(p.y), F(p.z), F(v.x), F(v.y), F(v.z), F(e.y), F(Mathf.DeltaAngle(0, e.x)), F(Mathf.DeltaAngle(0, e.z)),
            F(HeightAboveGround), F(s.Rotors.X), F(s.Rotors.Y), F(s.Rotors.Z), F(s.Rotors.W), F(BladePassingHz), Crashed ? "Crashed" : Model.Command.Mode.ToString(), F(st.x), F(st.y), F(st.z), F(st.w)));
    }

    /// <summary>Clients: the pose from the host.</summary>
    public void ShowPose(Vector3 position, Quaternion rotation, bool landed)
    {
        transform.SetPositionAndRotation(position, rotation);
        Landed = landed;
    }
}
