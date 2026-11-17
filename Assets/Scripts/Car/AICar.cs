using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

// This script was adapted from the study of De Clercq.

public class AICar : MonoBehaviour, IVehicle
{
    public enum CarState
    {
        DRIVING,
        BRAKING,
        STOPPED,
        TAKEOFF,
    }

    public CarState state = CarState.DRIVING;
    public PlayerAvatar playerAvatar;
    // Motion related variables
    private float set_speed = 0;                                           // Velocity of cars in simulation
    private float set_acceleration = 0;                                     // Acceleration of cars in simulation
    const float breakingTreshold = -1;
                                    // Acceleration of cars in simulation
    public float jerk = 2;                                                 // Jerk of cars in simulation
    public float turn_rate_degree = 360;
    private float conversion = 3.6f;
    public float speed;                                                    // Speed variable used for computations in this script
    public float acceleration;                                             // Acceleration variable used for computations in this script
    public float t = 0;                                                    // Time variable used for computations in this script
    private float pitch = 0;                                               // Pitch of the car
    private float tolerance = 0.05f;                                       // Used to determine if the changed variable reached its target value. 2% seems to fit. (De Clercq)
    public int HasPitch = 1;
    private Transform rotationAxis;
    private float Timer1;
    private float Timer2;
    private Quaternion modelLocalRotation
    {
        get
        {
            return modelElements != null && modelElements.Length > 0 ? modelElements[0].localRotation : Quaternion.identity;   // (two-wheelers have no body to pitch)
        }
        set
        {
            foreach(var me in modelElements)
            {
                me.localRotation = value;
            }
        }
    }
    public Transform [] modelElements;


    public bool braking = false;
    public bool reset = false;

    private float triggerlocation;
    private float startlocation;
    private float delta_distance;

    public List<CustomBehaviour> CustomBehaviours = new List<CustomBehaviour>();
    internal void TriggerCustomBehaviours(CustomBehaviourData bd)
    {
        foreach(var cb in CustomBehaviours)
        {
            cb.Trigger(bd);
        }
    }

    private float speedAfterYield;
    private float accAfterYield;
    private float yieldingTime;
    private bool shouldYield;

    public bool WaitInputX = false;
    public bool WaitTrialX = false;
    public bool WaitTrialZ = false;

    private GameObject ManualCarTrigger;
    private bool InitiateAV;

    // Game related variables
    public Transform target;                                               // Target on waypoint circuit that cars will follow
    private Rigidbody theRigidbody;                                        // Variable for the rigid body this script is attached to

    // Animation related variables
    private int layer;

    public bool Handbrake => braking;

    public float Speed => speed;

    // Use this for initialization
    void Start()
    {
        theRigidbody = GetComponent<Rigidbody>();                          // Grabs information of the rigid body this script is attached to.
        layer = gameObject.layer;                                          // Grabs layer of object this script is attached to. 
        rotationAxis = this.transform;                                     // Grabs orientation of the object this script is attached to.
        speed = set_speed;                                                 // Sets speed of object
        acceleration = set_acceleration;                                   // Sets acceleration of object
        target = GetComponent<WaypointProgressTracker>().target;           // Sets intermediate target on the circuit

        ManualCarTrigger = GameObject.FindWithTag("StartAV");
    }
    void Update()
    {
        // TODO(jacek): This null check is a quick hack to fix the errors
        // we probably want a more elegant solution
        if (ManualCarTrigger != null)
        {
            InitiateAV = ManualCarTrigger.GetComponent<StartAV>().InitiateAV;
        }
    }

    /// <summary>
    /// When set (e.g. by TrafficCar), the speed comes from outside: AICar only steers along the
    /// waypoints, pitches the body and shows brake lights; waypoint speed settings are ignored.
    /// </summary>
    [NonSerialized] public bool ExternalSpeedControl;
    /// <summary>Speed (m/s) and acceleration (m/s²) set by the external controller.</summary>
    [NonSerialized] public float ExternalSpeed;
    [NonSerialized] public float ExternalAcceleration;
    /// <summary>
    /// Upper limit (m/s) on the scripted speed, set by a collision-avoidance supervisor
    /// (TrafficCar in SafetyOnly mode) when a car or pedestrian is close ahead; +inf otherwise.
    /// </summary>
    [NonSerialized] public float SpeedCap = float.PositiveInfinity;

    void FixedUpdateExternal()
    {
        speed = ExternalSpeed * conversion;
        acceleration = ExternalAcceleration;
        bool brakingNow = ExternalAcceleration < -0.5f;
        state = ExternalSpeed < 0.1f ? CarState.STOPPED : brakingNow ? CarState.BRAKING : CarState.DRIVING;
        braking = brakingNow;
        if (playerAvatar != null && playerAvatar.stopLights != null)
            playerAvatar.SetBreakLights(brakingNow || ExternalSpeed < 0.1f);
        // a little pitch: nose down when braking, up when accelerating
        pitch = Mathf.Clamp(-ExternalAcceleration * 0.4f, -1.5f, 1.5f) * HasPitch;
        modelLocalRotation = Quaternion.Slerp(modelLocalRotation, Quaternion.Euler(pitch, 0, 0), 0.1f);
        // (not upwards: a push from a participant's vehicle must not lift or launch the car; gravity keeps it down)
        // (along the slope it lies on; the vertical speed beyond the slope's is its own: falling, or a little bump)
        var fwd = rotationAxis.forward;
        float vyOwn = theRigidbody.linearVelocity.y - fwd.y * ExternalSpeed;
        theRigidbody.linearVelocity = fwd * ExternalSpeed + Vector3.up * Mathf.Min(vyOwn, 0.3f);
    }

    TrafficCar _trafficCar;
    WaypointProgressTracker _tracker;
    float _yawRate;

    /// <summary>
    /// Steering of traffic driven by an external speed controller (TrafficCar: the map's traffic, the autopilot):
    /// pure pursuit of the target point on the route. The path's curvature towards the target, 2x/L² (x: the target's
    /// sideways offset, L its distance), turns the vehicle at its own speed times that curvature, so it turns no faster
    /// than it moves (no turning on the spot, no orbiting a point it cannot reach), within its smallest turning circle
    /// (by length: about 3 m for a bicycle, 4.7 m for a car, 8-9 m for trucks and buses), and the steering takes
    /// about 0.2 s to follow (no jitter, no swerving).
    /// </summary>
    void SteerPursuit()
    {
        if (_trafficCar == null) _trafficCar = GetComponent<TrafficCar>();
        float length = _trafficCar != null ? _trafficCar.VehicleLength : 4.5f;
        float minRadius = Mathf.Clamp(0.6f * length + 2f, 2.5f, 9f);   // (car 4.7 m, bicycle 3 m, trucks and buses 8-9 m)
        // the point to pursue: on the route, 3 m plus a quarter of the speed (m/s) ahead of the progress point (the
        // tracker's own target, 5 m and more ahead, cut tight corners by a metre or two: on a canal-side street, over the
        // quay's edge)
        if (_tracker == null) _tracker = GetComponent<WaypointProgressTracker>();
        var aim = target.position;
        if (_tracker != null && _tracker.Circuit != null)
        {
            float vNow = Mathf.Max(0f, Vector3.Dot(theRigidbody.linearVelocity, transform.forward));
            aim = _tracker.Circuit.GetRoutePoint(_tracker.ProgressDistance + 3f + 0.25f * vNow).position;
        }
        Vector3 local = transform.InverseTransformPoint(aim);
        float l2 = local.x * local.x + local.z * local.z;
        float curvature;
        if (local.z < 0.3f) curvature = Mathf.Sign(local.x == 0f ? 1f : local.x) / minRadius;   // behind: turn round as tightly as it can
        else curvature = l2 > 1e-4f ? 2f * local.x / l2 : 0f;
        curvature = Mathf.Clamp(curvature, -1f / minRadius, 1f / minRadius);
        float v = Mathf.Max(0f, Vector3.Dot(theRigidbody.linearVelocity, transform.forward));
        if (v < 0.05f) v = Mathf.Max(0f, ExternalSpeed);
        float wanted = v * curvature;   // rad/s
        _yawRate = Mathf.Lerp(_yawRate, wanted, 1f - Mathf.Exp(-Time.fixedDeltaTime / 0.2f));
        theRigidbody.angularVelocity = new Vector3(0f, _yawRate, 0f);
    }

    float _groundPitch, _groundRoll;
    int _groundTick;
    static readonly RaycastHit[] _groundHits = new RaycastHit[8];

    /// <summary>The height of the ground (roads, pavements, terrain) under a point, not counting vehicles.</summary>
    static bool GroundY(Vector3 at, out float y)
    {
        y = 0f; float best = float.MaxValue; bool found = false;
        int n = Physics.RaycastNonAlloc(at + Vector3.up * 2f, Vector3.down, _groundHits, 5f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = _groundHits[i];
            if (h.collider.attachedRigidbody != null || h.distance >= best) continue;
            best = h.distance; y = h.point.y; found = true;
        }
        return found;
    }

    /// <summary>
    /// Pitch and roll of the ground under the vehicle (from the heights under its front and back, left and right), so
    /// on slopes (the maps' terrain) it lies on the road instead of staying level with its front or back wheels in it.
    /// Two-wheelers only pitch (their lean is their own).
    /// </summary>
    void FollowGround()
    {
        if ((++_groundTick & 1) != 0) return;   // every other physics step
        if (_trafficCar == null) _trafficCar = GetComponent<TrafficCar>();
        float length = _trafficCar != null ? _trafficCar.VehicleLength : 4.5f;
        float half = Mathf.Max(0.5f, length * 0.4f);
        var f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) return;
        f.Normalize();
        var r = new Vector3(f.z, 0f, -f.x);
        var p = transform.position;
        float k = 1f - Mathf.Exp(-2f * Time.fixedDeltaTime / 0.15f);
        if (GroundY(p + f * half, out var yf) && GroundY(p - f * half, out var yb))
            _groundPitch = Mathf.Lerp(_groundPitch, Mathf.Clamp(-Mathf.Atan2(yf - yb, 2f * half) * Mathf.Rad2Deg, -20f, 20f), k);
        if (length > 2.5f && GroundY(p + r * 0.8f, out var yr) && GroundY(p - r * 0.8f, out var yl))
            _groundRoll = Mathf.Lerp(_groundRoll, Mathf.Clamp(Mathf.Atan2(yr - yl, 1.6f) * Mathf.Rad2Deg, -10f, 10f), k);
    }

    void FixedUpdate()
    {
        if (ExternalSpeedControl)
        {
            // the heading, on the slope of the ground under it
            FollowGround();
            rotationAxis.rotation = Quaternion.Euler(_groundPitch, rotationAxis.rotation.eulerAngles.y, _groundRoll);
            SteerPursuit();
            FixedUpdateExternal();
            return;
        }

        //  Update all required informations
        rotationAxis.rotation = Quaternion.Euler(0, rotationAxis.rotation.eulerAngles.y, 0);                     //heading

        // Every physics calculation involves the orientation and speed of the object.
        Vector3 new_position = transform.InverseTransformPoint(target.position);
        float psi = Mathf.Asin(new_position.x / (Mathf.Pow(new_position.x * new_position.x + new_position.z * new_position.z, 0.5f) + 0.001f));

        //  Change of Ambient Traffic rotations based on current heading and target position
        theRigidbody.angularVelocity = new Vector3(0f, psi * turn_rate_degree * Mathf.PI / 360f, 0f);
        FixedUpdateScripted();
        if (speed / conversion > SpeedCap)
        {
            speed = SpeedCap * conversion;
            theRigidbody.linearVelocity = rotationAxis.forward * SpeedCap;
            if (playerAvatar != null && playerAvatar.stopLights != null) playerAvatar.SetBreakLights(true);
        }
    }

    void FixedUpdateScripted()
    {

        if (shouldYield && speed == 0.0f)
        {
            StartCoroutine(Yield(yieldingTime));
        }
        
        // This statement is applied when the car is just driving.
        if ((braking == false) && (reset == false))
        {

            if (jerk != 0)
            {
                acceleration = set_acceleration;
                if (Mathf.Abs(set_acceleration) > 1e-4f)
                {
                    t += Mathf.Abs(jerk) / Mathf.Abs(set_acceleration) * Time.fixedDeltaTime;
                }
                pitch = acceleration / 3 * HasPitch;
                modelLocalRotation = Quaternion.Slerp(modelLocalRotation, Quaternion.Euler(-pitch, 0, 0), 0.5f);
            }

            // towards the target speed at the waypoint's acceleration, whatever its sign (#55:
            // a positive acceleration with a lower target speed used to be ignored)
            float accelMagnitude = Mathf.Abs(set_acceleration);
            if (accelMagnitude > 0f && Mathf.Abs(set_speed - speed) > 1e-4f)
            {
                float dir = Mathf.Sign(set_speed - speed);
                float step = accelMagnitude * Time.fixedDeltaTime * conversion;
                speed = Mathf.Abs(set_speed - speed) <= step ? set_speed : speed + dir * step;
                acceleration = dir * accelMagnitude;
                pitch = acceleration / 3 * HasPitch;
                modelLocalRotation = Quaternion.Slerp(modelLocalRotation, Quaternion.Euler(-pitch, 0, 0), 0.5f);
            }

            playerAvatar.SetBreakLights(set_speed < speed - 0.01f || speed < 0.1f);

            if (acceleration != 0 && Mathf.Abs(speed) < Mathf.Abs(set_speed) * (1 + tolerance) + tolerance * 10 && Mathf.Abs(speed) > Mathf.Abs(set_speed) * (1 - tolerance) - tolerance * 10)
            {
                jerk = 0;
                speed = set_speed;
            }

            theRigidbody.linearVelocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody

        }

        // This statement is applied when the car starts braking
        else if ((braking == true) && (reset == false))
        {
            // Compute delta distance for deceleration
            if (WaitInputX == true)
            {
                delta_distance = Mathf.Abs(this.gameObject.transform.position.x - triggerlocation);
            }

            else if (WaitTrialZ == true)
            {
                delta_distance = Mathf.Abs(this.gameObject.transform.position.z - triggerlocation);
            }

            else if (WaitTrialX == true)
            {
                delta_distance = Mathf.Abs(this.gameObject.transform.position.x - triggerlocation);
            }
            // Apply delta_distance for deceleration 
            // Formula: v = sqrt(u^2 + 2*a*s) with v = final velocity; u = initial velocity; a = acceleration; s = distance covered. 
            speed = Mathf.Sqrt(900 + 2 * set_acceleration * Mathf.Pow(conversion, 2) * delta_distance); // Application of conversion of km/h to m/s which needs to be squared

            // Slowing down            
            // Compute pitch for deceleration
            if (speed > 10f && delta_distance < 17f) // When speed larger than 10 km/h, pitch increases to 0.5 degrees
            {
                Timer1 += Time.deltaTime;
                pitch = Timer1 * 6;
                // Pitches larger than 0.5 degrees are capped at 0.5 degrees.
                if (pitch < 0.5f)
                {
                    modelLocalRotation = Quaternion.Slerp(modelLocalRotation, Quaternion.Euler(pitch, 0, 0), 0.5f);
                }
                else
                {
                    modelLocalRotation = Quaternion.Slerp(modelLocalRotation, Quaternion.Euler(0.5f, 0, 0), 0.5f);
                }
            }

            else if (speed < 10f && delta_distance < 17f) // When speed smaller than 10 km/h, pitch slowely decreases from 0.5 degrees
            {
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                if (pitch >= 0)
                {
                    modelLocalRotation = Quaternion.Slerp(modelLocalRotation, Quaternion.Euler(pitch, 0, 0), 0.5f);
                }

            }

            else if (Double.IsNaN(speed) || speed > 0f && speed < 5f && delta_distance > 17f) // Once distance is far enough and speed is almost zero, decrease pitch even more
            {
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                if (pitch >= 0)
                {
                    modelLocalRotation = Quaternion.Slerp(modelLocalRotation, Quaternion.Euler(pitch, 0, 0), 0.5f);
                }

                speed = 0f;
                theRigidbody.linearVelocity = new Vector3(0, 0, 0); // Apply zero velocity 
            }

            if (speed <= 0 && delta_distance > 16f)  // If car is standing still, change pitch back to zero.
            {
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                if (pitch >= 0) // Apply pitch only when larger than 0 degrees.
                {
                    modelLocalRotation = Quaternion.Slerp(modelLocalRotation, Quaternion.Euler(pitch, 0, 0), 0.5f);
                }

                if (Timer2 >= 2f) // After standing still for two seconds, passenger can initiate driving again by pressing space.
                {
                    if (WaitInputX == true && Input.GetKeyDown(KeyCode.Space))
                    {
                        braking = false;
                        reset = true;
                        set_acceleration = 1f;
                        set_speed = 30;
                        startlocation = this.gameObject.transform.position.x - 0.10f;
                    }

                    else if (WaitTrialZ == true && InitiateAV == true) //Input.GetKeyDown(KeyCode.Space))
                    {
                        braking = false;
                        reset = true;
                        set_acceleration = 1f;
                        set_speed = 30;
                        startlocation = this.gameObject.transform.position.z - 0.10f;
                    }
                    else if (WaitTrialX == true && InitiateAV == true) //Input.GetKeyDown(KeyCode.Space))
                    {
                        braking = false;
                        reset = true;
                        set_acceleration = 1f;
                        set_speed = 30;
                        startlocation = this.gameObject.transform.position.x - 0.10f;
                    }
                }
            }
            theRigidbody.linearVelocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody 
        }
        // This statement is applied when the car stood still and is resetting its speed.
        else if ((braking == false) && (reset == true))
        {
            // Accelerating in the X direction
            if (WaitInputX == true)
            {
                delta_distance = Mathf.Abs(this.gameObject.transform.position.x - startlocation);
            }

            // Accelerating in the Z direction
            else if (WaitTrialZ == true)
            {
                delta_distance = Mathf.Abs(this.gameObject.transform.position.z - startlocation);
            }

            // Accelerating in the X direction
            else if (WaitTrialX == true)
            {
                delta_distance = Mathf.Abs(this.gameObject.transform.position.x - startlocation);
            }

            speed = Mathf.Sqrt(2 * set_acceleration * Mathf.Pow(conversion, 2) * delta_distance);
            if (speed < 2f)
            {
                speed = 2f;
            }
            theRigidbody.linearVelocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody 

            if (speed >= 10f)
            {
                reset = false;
                WaitInputX = false;
                WaitTrialZ = false;
                WaitTrialX = false;
            }
        }
    }

    // If gameObject is set invisible, remove the car.
    void OnBecameInvisible()
    {
        if (!enabled || ExternalSpeedControl) return;
        Despawn();
    }

    // Cars are hidden rather than destroyed, so the networked pose list keeps its indices.
    void Despawn()
    {
        var sync = AICarSyncSystem.HostInstance;
        if (sync != null && playerAvatar != null)
        {
            sync.SetCarActive(playerAvatar, false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        SpeedSettings speedSettings = other.GetComponent<SpeedSettings>();
        if (speedSettings == null || (speedSettings.targetAICar != null && speedSettings.targetAICar != this))
        {
            return;
        }
        // waypoints are tagged "WP"; untagged waypoints with speed settings work too
        if (other.gameObject.CompareTag("WP") || other.gameObject.CompareTag("Untagged"))
        {
            var avatar = playerAvatar != null ? playerAvatar : GetComponent<PlayerAvatar>();
            avatar?.CarBlinkers?.SwitchToState(speedSettings.BlinkerState);

            if (speedSettings.Type == SpeedSettings.WaypointType.InitialSetSpeed)
            {
                if (speedSettings.causeToYield)
                {
                    speed = 0;
                }
                else
                {
                    speed = speedSettings.speed;
                }
            }
            if (speedSettings.Type == SpeedSettings.WaypointType.SetSpeedTarget || speedSettings.Type == SpeedSettings.WaypointType.InitialSetSpeed)
            {
                if (speedSettings.causeToYield)
                {
                    set_speed = 0;
                    set_acceleration = speedSettings.brakingAcceleration;
                    speedAfterYield = speedSettings.speed;
                    accAfterYield = speedSettings.acceleration;
                    yieldingTime = speedSettings.yieldTime;
                    //PlayerLookAtPed.EnableTrackingWhileYielding = speedSettings.lookAtPlayerWhileYielding;
                    shouldYield = true;
                    state = CarState.BRAKING;
                }
                else
                {
                    set_speed = speedSettings.speed;
                    set_acceleration = speedSettings.acceleration;
                }
            }
            else if (speedSettings.Type == SpeedSettings.WaypointType.Delete)
            {
                Despawn();
            }
        }

        // Change of tag here that causes deceleration when hitting trigger in X direction
        else if (other.gameObject.CompareTag("WaitInput_X"))
        {
            triggerlocation = transform.position.x;
            braking = true;
            WaitInputX = true;
            set_speed = speedSettings.speed;
            set_acceleration = speedSettings.acceleration;
            jerk = -Mathf.Abs(speedSettings.jerk);
        }
        // Change of tag here that causes deceleration when hitting trigger in Z direction
        else if (other.gameObject.CompareTag("StartTrial_Z"))
        {
            triggerlocation = transform.position.z; 
            braking = true;
            WaitTrialZ = true;
            set_speed = speedSettings.speed;
            set_acceleration = speedSettings.acceleration;
            jerk = -Mathf.Abs(speedSettings.jerk);
        }

        // Change of tag here that causes deceleration when hitting trigger in Z direction
        else if (other.gameObject.CompareTag("StartTrial_X"))
        {
            triggerlocation = transform.position.x;
            braking = true;
            WaitTrialX = true;
            set_speed = speedSettings.speed;
            set_acceleration = speedSettings.acceleration;
            jerk = -Mathf.Abs(speedSettings.jerk);
        }
    }

    private IEnumerator Yield(float yieldTime)
    {
        state = CarState.STOPPED;
        yield return new WaitForSeconds(yieldTime);
        set_speed = speedAfterYield;
        set_acceleration = accAfterYield;
        shouldYield = false;
        state = CarState.TAKEOFF;

    }
}
