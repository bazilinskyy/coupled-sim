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

    // Motion related variables
    private float set_speed = 0;                                           // Velocity of cars in simulation
    private float set_acceleration = 0;                                     // Acceleration of cars in simulation
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
    public Transform model;

    public bool braking = false;
    public bool reset = false;

    private float triggerlocation;
    private float startlocation;
    private float delta_distance;

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

    void FixedUpdate()
    {
        // Every physics calculation involves the orientation and speed of the object.
        Vector3 new_position = transform.InverseTransformPoint(target.position);
        float psi = Mathf.Asin(new_position.x / (Mathf.Pow(new_position.x * new_position.x + new_position.z * new_position.z, 0.5f) + 0.001f));

        //  Update all required informations
        rotationAxis.rotation = Quaternion.Euler(0, rotationAxis.rotation.eulerAngles.y, 0);                     //heading

        //  Change of Ambient Traffic rotations based on current heading and target position
        theRigidbody.angularVelocity = new Vector3(0f, psi * turn_rate_degree * Mathf.PI / 360f, 0f);

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
                t += Mathf.Abs(jerk) / Mathf.Abs(set_acceleration) * Time.fixedDeltaTime;
                pitch = acceleration / 3 * HasPitch;
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(-pitch, 0, 0), 0.5f);
            }

            if (set_acceleration > 0f && set_speed > speed || set_acceleration < 0f && set_speed < speed)
            {
                speed = speed + set_acceleration * Time.fixedDeltaTime * conversion;
                pitch = acceleration / 3 * HasPitch;
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(-pitch, 0, 0), 0.5f);
            }

            if (acceleration != 0 && Mathf.Abs(speed) < Mathf.Abs(set_speed) * (1 + tolerance) + tolerance * 10 && Mathf.Abs(speed) > Mathf.Abs(set_speed) * (1 - tolerance) - tolerance * 10)
            {
                jerk = 0;
                speed = set_speed;
            }

            theRigidbody.velocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody

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
                    model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(pitch, 0, 0), 0.5f);
                }
                else
                {
                    model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(0.5f, 0, 0), 0.5f);
                }
            }

            else if (speed < 10f && delta_distance < 17f) // When speed smaller than 10 km/h, pitch slowely decreases from 0.5 degrees
            {
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                if (pitch >= 0)
                {
                    model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(pitch, 0, 0), 0.5f);
                }

            }

            else if (Double.IsNaN(speed) || speed > 0f && speed < 5f && delta_distance > 17f) // Once distance is far enough and speed is almost zero, decrease pitch even more
            {
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                if (pitch >= 0)
                {
                    model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(pitch, 0, 0), 0.5f);
                }

                speed = 0f;
                theRigidbody.velocity = new Vector3(0, 0, 0); // Apply zero velocity 
            }

            if (speed <= 0 && delta_distance > 16f)  // If car is standing still, change pitch back to zero.
            {
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                if (pitch >= 0) // Apply pitch only when larger than 0 degrees.
                {
                    model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(pitch, 0, 0), 0.5f);
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
            theRigidbody.velocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody 
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
            theRigidbody.velocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody 

            if (speed >= 10f)
            {
                reset = false;
                WaitInputX = false;
                WaitTrialZ = false;
                WaitTrialX = false;
            }
        }
    }

    // If gameObject is set invisible, destroy gameObject.
    void OnBecameInvisible()
    {
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("WP"))
        {
            // If WaypointNumber is one, take over settings
            if (other.GetComponent<SpeedSettings>().WaypointType == 1)
            {
                if (other.GetComponent<SpeedSettings>().causeToYield)
                {
                    set_speed = 0;
                    set_acceleration = other.GetComponent<SpeedSettings>().brakingAcceleration;
                    speedAfterYield = other.GetComponent<SpeedSettings>().speed;
                    accAfterYield = other.GetComponent<SpeedSettings>().acceleration;
                    yieldingTime = other.GetComponent<SpeedSettings>().yieldTime;
                    //PlayerLookAtPed.EnableTrackingWhileYielding = other.GetComponent<SpeedSettings>().lookAtPlayerWhileYielding;
                    shouldYield = true;
                    state = CarState.BRAKING;
                }
                else
                {
                    set_speed = other.GetComponent<SpeedSettings>().speed;
                    set_acceleration = other.GetComponent<SpeedSettings>().acceleration;
                }
            }
            // If WaypointNumber is two, destroy gameobject.
            else if (other.GetComponent<SpeedSettings>().WaypointType == 2)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                Destroy(target.gameObject);
            }
        }

        // Change of tag here that causes deceleration when hitting trigger in X direction
        else if (other.gameObject.CompareTag("WaitInput_X"))
        {
            triggerlocation = transform.position.x;
            braking = true;
            WaitInputX = true;
            set_speed = other.GetComponent<SpeedSettings>().speed;
            set_acceleration = other.GetComponent<SpeedSettings>().acceleration;
            jerk = -Mathf.Abs(other.GetComponent<SpeedSettings>().jerk);
        }
        // Change of tag here that causes deceleration when hitting trigger in Z direction
        else if (other.gameObject.CompareTag("StartTrial_Z"))
        {
            triggerlocation = transform.position.z; 
            braking = true;
            WaitTrialZ = true;
            set_speed = other.GetComponent<SpeedSettings>().speed;
            set_acceleration = other.GetComponent<SpeedSettings>().acceleration;
            jerk = -Mathf.Abs(other.GetComponent<SpeedSettings>().jerk);
        }

        // Change of tag here that causes deceleration when hitting trigger in Z direction
        else if (other.gameObject.CompareTag("StartTrial_X"))
        {
            triggerlocation = transform.position.x;
            braking = true;
            WaitTrialX = true;
            set_speed = other.GetComponent<SpeedSettings>().speed;
            set_acceleration = other.GetComponent<SpeedSettings>().acceleration;
            jerk = -Mathf.Abs(other.GetComponent<SpeedSettings>().jerk);
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
