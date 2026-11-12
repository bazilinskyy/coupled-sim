using CoupledSim.TwoWheelers;
using UnityEngine;

/// <summary>
/// Rides a bicycle, e-bike, e-scooter or moped with <see cref="TwoWheelerDynamics"/>. Input comes
/// from the same axes as the car (keyboard W/S or arrows, Space to brake, A/D to steer; steering
/// wheel and pedals), so existing input set-ups work. The vehicle follows the ground (it can mount
/// kerbs up to MaxStepHeight at low speed) and stops when it runs into something.
/// Visual parts: wheels spin, the handlebar turns, frame and rider lean, bicycle cranks turn.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TwoWheelerController : MonoBehaviour
{
    public TwoWheelerType Type = TwoWheelerType.Bicycle;
    public TwoWheelerParameters Parameters = TwoWheelerParameters.Preset(TwoWheelerType.Bicycle);
    [Tooltip("Read the participant's input. Off for remote players (their pose comes over the network).")]
    public bool ReadInput = true;
    [Tooltip("Input is set by a script (e.g. a test or an external device) instead of the input axes.")]
    public bool UseExternalInput;

    [Header("Input axes (Edit ➡️ Project Settings ➡️ Input Manager)")]
    public string DriveAxis = "Throttle";
    public string BrakeAxis = "Break";
    public string SteerAxis = "Horizontal";

    [Header("Visual parts")]
    public Transform LeanPivot;
    public Transform SteeringPivot;
    public Transform FrontWheel;
    public Transform RearWheel;
    public Transform Crank;
    public float WheelRadius = 0.35f;
    [Tooltip("Crank revolutions per wheel revolution (inverse gear ratio).")]
    public float CrankRatio = 0.4f;
    [Tooltip("How much of the lean the camera follows (1 = all; less is more comfortable in VR).")]
    [Range(0f, 1f)] public float CameraLeanFactor = 0.4f;
    public Transform CameraRig;

    [Header("Ground")]
    public float MaxStepHeight = 0.18f;
    public LayerMask GroundMask = ~0;

    [Header("State (read only)")]
    public float SpeedKmh;
    public float LeanDeg;
    public float SteerDeg;
    public bool Collided;

    public TwoWheelerState State;
    public TwoWheelerInput Input;
    Rigidbody _rb;
    float _wheelAngle, _crankAngle, _groundY;
    readonly RaycastHit[] _hits = new RaycastHit[8];

    /// <summary>Current speed (m/s).</summary>
    public float Speed => State.Speed;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        Parameters.Type = Type;
    }

    void Start()
    {
        var p = transform.position;
        State = new TwoWheelerState { X = p.x, Z = p.z, Yaw = transform.eulerAngles.y * Mathf.Deg2Rad };
        _groundY = p.y;
    }

    void Update()
    {
        if (!ReadInput || UseExternalInput) return;
        Input.Drive = Mathf.Clamp01(Axis(DriveAxis));
        // the car's throttle axis is negative for "back"; on a two-wheeler that means brake
        Input.Brake = Mathf.Clamp01(Mathf.Max(Axis(BrakeAxis), -Axis(DriveAxis)));
        Input.Steer = Mathf.Clamp(Axis(SteerAxis), -1f, 1f);
    }

    static float Axis(string name)
    {
        try { return string.IsNullOrEmpty(name) ? 0f : UnityEngine.Input.GetAxis(name); }
        catch (System.ArgumentException) { return 0f; }
    }

    void FixedUpdate()
    {
        if (!ReadInput) return;
        float dt = Time.fixedDeltaTime;
        float slope = GroundSlope();
        var next = TwoWheelerDynamics.Step(State, Parameters, Input, dt, slope);

        // obstacles: sweep from the current to the next position
        var from = transform.position + Vector3.up * 0.6f;
        var move = new Vector3(next.X - State.X, 0f, next.Z - State.Z);
        Collided = false;
        if (move.sqrMagnitude > 1e-8f)
        {
            int n = Physics.SphereCastNonAlloc(from, 0.3f, move.normalized, _hits, move.magnitude + 0.5f, GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform)) continue;
                // walls, cars, poles: everything that is not walkable ground in front of us
                if (Vector3.Dot(_hits[i].normal, Vector3.up) < 0.7f && _hits[i].distance > 0f)
                {
                    next.X = State.X;
                    next.Z = State.Z;
                    next.Speed = 0f;
                    Collided = true;
                    break;
                }
            }
        }

        // ground: follow it, allowing kerbs up to MaxStepHeight
        float targetY = _groundY;
        if (Physics.Raycast(new Vector3(next.X, _groundY + MaxStepHeight + 0.3f, next.Z), Vector3.down, out var hit, MaxStepHeight + 2f, GroundMask, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
        {
            float step = hit.point.y - _groundY;
            if (step > MaxStepHeight)
            {
                next.X = State.X; next.Z = State.Z; next.Speed = 0f; Collided = true; // kerb too high
            }
            else
            {
                targetY = hit.point.y;
            }
        }
        _groundY = Mathf.Lerp(_groundY, targetY, 1f - Mathf.Exp(-dt * 20f));
        State = next;
        _rb.MovePosition(new Vector3(State.X, _groundY, State.Z));
        _rb.MoveRotation(Quaternion.Euler(0f, State.Yaw * Mathf.Rad2Deg, 0f));
    }

    float GroundSlope()
    {
        var p = transform.position;
        var f = transform.forward * (Parameters.Wheelbase * 0.5f);
        if (Physics.Raycast(p + f + Vector3.up, Vector3.down, out var front, 2f, GroundMask, QueryTriggerInteraction.Ignore) &&
            Physics.Raycast(p - f + Vector3.up, Vector3.down, out var rear, 2f, GroundMask, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Atan2(front.point.y - rear.point.y, Parameters.Wheelbase);
        }
        return 0f;
    }

    void LateUpdate()
    {
        SpeedKmh = State.Speed * 3.6f;
        LeanDeg = State.Lean * Mathf.Rad2Deg;
        SteerDeg = State.Steer * Mathf.Rad2Deg;
        float dt = Time.deltaTime;
        _wheelAngle += State.Speed / Mathf.Max(0.05f, WheelRadius) * Mathf.Rad2Deg * dt;
        if (FrontWheel != null) FrontWheel.localRotation = Quaternion.Euler(_wheelAngle, 0, 0);
        if (RearWheel != null) RearWheel.localRotation = Quaternion.Euler(_wheelAngle, 0, 0);
        if (SteeringPivot != null) SteeringPivot.localRotation = Quaternion.Euler(0, SteerDeg, 0);
        // positive lean = to the right = negative rotation around the forward (z) axis
        if (LeanPivot != null) LeanPivot.localRotation = Quaternion.Euler(0, 0, -LeanDeg);
        if (CameraRig != null) CameraRig.localRotation = Quaternion.Euler(0, 0, -LeanDeg * CameraLeanFactor);
        if (Crank != null && Input.Drive > 0.05f)
        {
            _crankAngle += State.Speed / Mathf.Max(0.05f, WheelRadius) * Mathf.Rad2Deg * CrankRatio * dt;
            Crank.localRotation = Quaternion.Euler(_crankAngle, 0, 0);
        }
    }

    /// <summary>Places the vehicle (e.g. at a spawn point) and stops it.</summary>
    public void Teleport(Vector3 position, float yawDeg)
    {
        transform.SetPositionAndRotation(position, Quaternion.Euler(0, yawDeg, 0));
        State = new TwoWheelerState { X = position.x, Z = position.z, Yaw = yawDeg * Mathf.Deg2Rad };
        _groundY = position.y;
    }
}
