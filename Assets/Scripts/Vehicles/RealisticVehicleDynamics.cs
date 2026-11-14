using System.Linq;
using CoupledSim.Vehicles;
using UnityEngine;

/// <summary>
/// Realistic driving physics for the participant's car, on top of the WheelVehicle (Arcade Car
/// Physics) set-up: real mass, centre of mass and inertia; suspension tuned to the car's mass;
/// tyre grip curves for asphalt that drop on wet roads; per-wheel grip and roughness from the
/// road surface under each wheel (<see cref="RoadSurfaces"/>: bricks, cobblestones, gravel, grass); an engine with torque curve, automatic
/// gearbox and engine braking (<see cref="Powertrain"/>); front/rear brake balance with ABS;
/// anti-roll bars; Ackermann steering geometry. When this component is on a car, WheelVehicle
/// hands drive, braking and steering to it.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RealisticVehicleDynamics : MonoBehaviour
{
    public enum CarPreset { SmartFortwo, Hatchback }

    public CarPreset Car = CarPreset.SmartFortwo;
    [Tooltip("Tyre grip on wet roads relative to dry (rain from EnvironmentController).")]
    [Range(0.4f, 1f)] public float WetGrip = 0.7f;
    [Tooltip("Anti-roll bar stiffness (N per m of suspension travel difference).")]
    public float AntiRollFront = 6000f, AntiRollRear = 4000f;
    [Tooltip("Suspension natural frequency (Hz) and damping ratio.")]
    public float SuspensionFrequency = 1.6f, DampingRatio = 0.35f;
    public bool Abs = true;
    [Tooltip("Grip and vertical jolts from the surface under each wheel (map roads named by surface, or -roadSurface).")]
    public bool SurfaceEffects = true;

    [Header("Tyres (Magic Formula)")]
    [Tooltip("Set the tyre curves from Pacejka Magic Formula coefficients (e.g. from tyre data) instead of the built-in asphalt curves.")]
    public bool UseMagicFormula;
    public MagicFormula Longitudinal = MagicFormula.DryLongitudinal;
    public MagicFormula Lateral = MagicFormula.DryLateral;

    [Header("State (read only)")]
    public int Gear;
    public float Rpm;
    public float SpeedKmh;
    public float Grip = 1f;
    [Tooltip("The surface under the front-left wheel.")]
    public string Surface = "asphalt";
    [Tooltip("Vertical acceleration of the body from road roughness (RMS over about 1 s, m/s²).")]
    public float RoughnessRms;

    public Powertrain Powertrain { get; private set; }
    public CarSpec Spec { get; private set; }

    Rigidbody _rb;
    WheelCollider[] _wheels, _front, _rear, _driven;
    AntiLockBrakes[] _abs;
    WheelCollider _fl, _fr, _rl, _rr;
    bool _configured;
    float[] _wheelGrip;
    float _accSq;
    float[] _phase;
    readonly float[] _vyHistory = new float[5];
    int _vyIndex;

    void Awake()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-magicFormulaTyres") >= 0) UseMagicFormula = true;
        Spec = Car == CarPreset.Hatchback ? CarSpec.Hatchback : CarSpec.SmartFortwo;
        Powertrain = new Powertrain(Spec);
    }

    public void Configure()
    {
        if (_configured) return;
        _configured = true;
        _rb = GetComponent<Rigidbody>();
        _wheels = GetComponentsInChildren<WheelCollider>(true);
        if (_wheels.Length < 4) return;
        // classify wheels by their position in the car
        float midZ = _wheels.Average(w => transform.InverseTransformPoint(w.transform.position).z);
        _front = _wheels.Where(w => transform.InverseTransformPoint(w.transform.position).z > midZ).ToArray();
        _rear = _wheels.Where(w => transform.InverseTransformPoint(w.transform.position).z <= midZ).ToArray();
        _fl = _front.OrderBy(w => transform.InverseTransformPoint(w.transform.position).x).First();
        _fr = _front.OrderBy(w => transform.InverseTransformPoint(w.transform.position).x).Last();
        _rl = _rear.OrderBy(w => transform.InverseTransformPoint(w.transform.position).x).First();
        _rr = _rear.OrderBy(w => transform.InverseTransformPoint(w.transform.position).x).Last();
        _driven = Spec.RearWheelDrive ? _rear : _front;
        _abs = _wheels.Select(_ => new AntiLockBrakes()).ToArray();
        _wheelGrip = _wheels.Select(_ => -1f).ToArray();
        _phase = new float[_wheels.Length];

        _rb.mass = Spec.Mass;
        // centre of mass: at the spec height above the ground, between the axles (weight distribution)
        float groundY = _wheels.Average(w => transform.InverseTransformPoint(w.transform.position).y) - _wheels[0].radius;
        float frontZ = _front.Average(w => transform.InverseTransformPoint(w.transform.position).z);
        float rearZ = _rear.Average(w => transform.InverseTransformPoint(w.transform.position).z);
        float frontShare = Spec.RearWheelDrive ? 0.42f : 0.6f;
        _rb.centerOfMass = new Vector3(0f, groundY + Spec.CentreOfMassHeight, Mathf.Lerp(rearZ, frontZ, frontShare));
        float l = Spec.Wheelbase + 1.2f, w = Spec.TrackWidth + 0.3f, h = 1.4f;
        _rb.inertiaTensor = new Vector3(w * w + h * h, l * l + w * w, l * l + h * h) * (Spec.Mass / 12f);
        _rb.inertiaTensorRotation = Quaternion.identity;

        foreach (var wc in _wheels)
        {
            bool front = _front.Contains(wc);
            float quarterMass = Spec.Mass * (front ? frontShare : 1f - frontShare) / 2f;
            float k = quarterMass * Mathf.Pow(2f * Mathf.PI * SuspensionFrequency, 2f);
            var spring = wc.suspensionSpring;
            spring.spring = k;
            spring.damper = 2f * DampingRatio * Mathf.Sqrt(k * quarterMass);
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;
            wc.mass = 18f;
            wc.wheelDampingRate = 0.5f;
        }
        SetGrip(1f);
    }

    void SetGrip(float grip)
    {
        Grip = grip;
        for (int i = 0; i < _wheels.Length; i++) SetWheelGrip(i, grip);
    }

    void SetWheelGrip(int i, float grip)
    {
        _wheelGrip[i] = grip;
        var wc = _wheels[i];
        if (UseMagicFormula && Longitudinal.IsSet && Lateral.IsSet)
        {
            // slips: longitudinal slip ratio up to 1, lateral slip angle up to ~30 degrees (0.5 rad)
            wc.forwardFriction = Longitudinal.ToWheelFriction(wc.forwardFriction, 1f, grip);
            wc.sidewaysFriction = Lateral.ToWheelFriction(wc.sidewaysFriction, 0.5f, grip);
            return;
        }
        // asphalt: peak friction at ~10-15% slip, sliding friction ~75% of the peak
        var f = wc.forwardFriction;
        f.extremumSlip = 0.2f; f.extremumValue = 1.0f; f.asymptoteSlip = 0.8f; f.asymptoteValue = 0.75f; f.stiffness = grip * 1.2f;
        wc.forwardFriction = f;
        var s = wc.sidewaysFriction;
        s.extremumSlip = 0.25f; s.extremumValue = 1.0f; s.asymptoteSlip = 0.5f; s.asymptoteValue = 0.75f; s.stiffness = grip * 1.4f;
        wc.sidewaysFriction = s;
    }

    /// <summary>Ackermann steering: the inner front wheel turns more than the outer one.</summary>
    public void Steer(float angleDeg)
    {
        if (!_configured || _fl == null) return;
        if (Mathf.Abs(angleDeg) < 0.01f) { _fl.steerAngle = _fr.steerAngle = 0f; return; }
        float radius = Spec.Wheelbase / Mathf.Tan(Mathf.Abs(angleDeg) * Mathf.Deg2Rad);
        float inner = Mathf.Atan(Spec.Wheelbase / (radius - Spec.TrackWidth / 2f)) * Mathf.Rad2Deg;
        float outer = Mathf.Atan(Spec.Wheelbase / (radius + Spec.TrackWidth / 2f)) * Mathf.Rad2Deg;
        float sign = Mathf.Sign(angleDeg);
        // turning right (positive): the right wheel is the inner one
        _fr.steerAngle = sign * (sign > 0 ? inner : outer);
        _fl.steerAngle = sign * (sign > 0 ? outer : inner);
    }

    /// <summary>Drive and brake for this physics step: throttle and brake 0..1.</summary>
    public void Drive(float throttle, float brake, bool reverse)
    {
        if (!_configured) Configure();
        if (_driven == null) return;
        float dt = Time.fixedDeltaTime;
        float speed = Vector3.Dot(_rb.linearVelocity, transform.forward);
        SpeedKmh = speed * 3.6f;
        // reverse gear only engages at (almost) standstill
        if (reverse != Powertrain.Reverse && Mathf.Abs(speed) < 1f) Powertrain.Reverse = reverse;
        float torque = Powertrain.WheelTorque(speed, throttle, dt);
        Gear = Powertrain.Reverse ? -1 : Powertrain.Gear;
        Rpm = Powertrain.Rpm;
        foreach (var wc in _driven) wc.motorTorque = torque / _driven.Length + 0.0001f;
        foreach (var wc in _wheels) if (!_driven.Contains(wc)) wc.motorTorque = 0.0001f;

        // hold the car at standstill without throttle (like an automatic with auto-hold)
        if (Mathf.Abs(speed) < 0.3f && throttle < 0.05f) brake = Mathf.Max(brake, 0.3f);
        for (int i = 0; i < _wheels.Length; i++)
        {
            var wc = _wheels[i];
            bool front = _front.Contains(wc);
            float bias = front ? Spec.FrontBrakeBias : 1f - Spec.FrontBrakeBias;
            float requested = brake * Spec.MaxBrakeTorque * bias * 2f;
            float slip = wc.GetGroundHit(out var hit) ? hit.forwardSlip * Mathf.Sign(speed) : 0f;
            wc.brakeTorque = Abs ? _abs[i].Modulate(requested, slip, dt) : requested;
        }
    }

    void FixedUpdate()
    {
        if (!_configured || _fl == null) return;
        // wet roads: less grip; each wheel by the surface under it
        var env = EnvironmentController.Instance;
        float wet = env != null ? Mathf.Clamp01(env.Rain * 1.5f) : 0f;
        float speed = _rb.linearVelocity.magnitude;
        float sum = 0f;
        for (int i = 0; i < _wheels.Length; i++)
        {
            var wc = _wheels[i];
            bool grounded = wc.GetGroundHit(out var hit);
            var surface = SurfaceEffects && grounded ? RoadSurfaces.Of(hit.collider) : RoadSurfaces.Asphalt;
            float grip = surface.GripAt(wet, WetGrip);
            if (Mathf.Abs(grip - _wheelGrip[i]) > 0.01f) SetWheelGrip(i, grip);
            sum += _wheelGrip[i];
            if (wc == _fl) Surface = surface.Name;
            if (grounded && surface.Roughness > 0f) Jolt(i, surface, speed);
        }
        Grip = sum / _wheels.Length;
        // body vertical acceleration over 0.1 s (what is felt and seen, without the physics steps'
        // own jitter), RMS over about 1 s; e.g. for logs and motion platforms
        float vy = Vector3.Dot(_rb.linearVelocity, transform.up);
        float ay = (vy - _vyHistory[_vyIndex]) / (Time.fixedDeltaTime * _vyHistory.Length);
        _vyHistory[_vyIndex] = vy;
        _vyIndex = (_vyIndex + 1) % _vyHistory.Length;
        _accSq = Mathf.Lerp(_accSq, ay * ay, Time.fixedDeltaTime);
        RoughnessRms = Mathf.Sqrt(_accSq);
        AntiRoll(_fl, _fr, AntiRollFront);
        AntiRoll(_rl, _rr, AntiRollRear);
    }

    // a rough surface: a smooth random vertical force at each wheel, stronger with speed, varying
    // faster the smaller the stones (at most 15 Hz: what the body and seat pass on; faster stones
    // are felt as that). Each wheel has its own noise, so the car also rolls and pitches a little.
    void Jolt(int i, RoadSurface surface, float speed)
    {
        if (speed < 0.5f) return;
        float rate = Mathf.Min(speed / surface.Wavelength, 15f);
        _phase[i] += rate * Time.fixedDeltaTime;
        // Perlin noise around 0.5: centred and scaled to about -1..1
        float n = (Mathf.PerlinNoise(_phase[i], i * 7.31f + 0.5f) - 0.5f) * 3f;
        float scale = surface.Roughness * Mathf.Sqrt(Mathf.Min(speed, 25f) / 13.9f);
        float quarter = _rb.mass / _wheels.Length;
        _rb.AddForceAtPosition(transform.up * (quarter * scale * n), _wheels[i].transform.position);
    }

    void AntiRoll(WheelCollider left, WheelCollider right, float stiffness)
    {
        float travelL = 1f, travelR = 1f;
        bool groundedL = left.GetGroundHit(out var hitL), groundedR = right.GetGroundHit(out var hitR);
        if (groundedL) travelL = (-left.transform.InverseTransformPoint(hitL.point).y - left.radius) / left.suspensionDistance;
        if (groundedR) travelR = (-right.transform.InverseTransformPoint(hitR.point).y - right.radius) / right.suspensionDistance;
        float force = (travelL - travelR) * stiffness;
        if (groundedL) _rb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
        if (groundedR) _rb.AddForceAtPosition(right.transform.up * force, right.transform.position);
    }
}
