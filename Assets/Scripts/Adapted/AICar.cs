using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

// This script was adapted from the study of De Clercq.
// This script is further modified by Johnson Mok

public class AICar : MonoBehaviour
{
    // Motion related variables
    public float set_speed = 30;                                           // Velocity of cars in simulation
    public float set_acceleration = 2;                                     // Acceleration of cars in simulation
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

    public bool braking = false;
    public bool reset = false;

    private float triggerlocation;
    private float startlocation;
    private float delta_distance;

    public bool WaitTrialX = false;
    public bool WaitTrialZ = false;
    public bool BrakeX = false;
    public bool BrakeZ = false;
    public bool SpaceBar = false;

    private GameObject ManualCarTrigger;
    private bool InitiateAV;

    // Game related variables
    public Transform target;                                               // Target on waypoint circuit that cars will follow
    private Rigidbody theRigidbody;                                        // Variable for the rigid body this script is attached to

    // Animation related variables
    private int layer;

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

        // Brake using spacebar
        if (Input.GetKeyDown("space") == true)
        {
            Brake_Spacebar();
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
        
        // This statement is applied when the car is just driving.
        if ((braking == false) && (reset == false))
        {
            Car_Driving_Behaviour();
        }

        // This statement is applied when the car starts braking
        else if ((braking == true) && (reset == false))
        {
            Decelerate_Car();
        }
        // This statement is applied when the car stood still and is resetting its speed.
        else if ((braking == false) && (reset == true))
        {
            Reset_Speed_After_Stopping();
        }
    }

    // If gameObject is set invisible, destroy gameObject.
    void OnBecameInvisible()
    {
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        // Do nothing if trigger isn't enabled
        if (this.enabled == false)
        {
            return;
        }
        // Take over Waypoint Data
        else if (other.gameObject.CompareTag("WP"))
        {
            // If WaypointNumber is one, take over settings
            if (other.GetComponent<SpeedSettings>().WaypointNumber == 1)
            {
                set_speed = other.GetComponent<SpeedSettings>().speed;
                set_acceleration = other.GetComponent<SpeedSettings>().acceleration;
            }
            // If WaypointNumber is two, destroy gameobject.
            else if (other.GetComponent<SpeedSettings>().WaypointNumber == 2)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                Destroy(target.gameObject);
            }
        }
        // Change of tag here that causes deceleration when hitting a trigger.
        else
        {
            Brake_AV(other);
        }
    }

    /////////////////////////////   LOCAL FUNCTIONS     /////////////////////////////
    
    void Brake_AV(Collider other)
    {
        if (other.gameObject.CompareTag("StartTrial_Z"))                // Change tag, resume driving after stopping for 2 seconds in the Z direction.
        {
            WaitTrialZ = true;
            triggerlocation = other.gameObject.transform.position.z;
        }
        else if (other.gameObject.CompareTag("Brake_Z"))                // Change tag, stop completely.
        {
            BrakeZ = true;
            triggerlocation = other.gameObject.transform.position.z;
        }
        else if (other.gameObject.CompareTag("StartTrial_X"))           // Change tag, resume driving after stopping for 2 seconds in the X direction.
        {
            WaitTrialX = true;
            triggerlocation = other.gameObject.transform.position.x;
        }
        else if (other.gameObject.CompareTag("Brake_X"))                // Change tag, stop completely.
        {
            BrakeX = true;
            triggerlocation = other.gameObject.transform.position.x;
        }
        Brake_Set_Variables(other);
    }

    void Brake_Set_Variables(Collider other)
    {
        braking = true;
        set_speed = other.GetComponent<SpeedSettings>().speed;
        set_acceleration = other.GetComponent<SpeedSettings>().acceleration;
        jerk = -Mathf.Abs(other.GetComponent<SpeedSettings>().jerk);
    }

    // Break using spacebar input
    void Brake_Spacebar()
    {
        SpaceBar = true;
        triggerlocation = this.gameObject.transform.position.z;
        braking = true;
        set_speed = 0;
        set_acceleration = -2;
        jerk = -Mathf.Abs(-6);
    }


    // Function to set car behaviour during driving
    void Car_Driving_Behaviour()
    {
        if (jerk != 0)
        {
            acceleration = set_acceleration;
            t += Mathf.Abs(jerk) / Mathf.Abs(set_acceleration) * Time.fixedDeltaTime;
            pitch = acceleration / 3 * HasPitch;
            this.transform.Rotate(-pitch, 0, 0);
        }

        if (set_acceleration > 0f && set_speed > speed || set_acceleration < 0f && set_speed < speed)
        {
            speed = speed + set_acceleration * Time.fixedDeltaTime * conversion;
            pitch = acceleration / 3 * HasPitch;
            this.transform.Rotate(-pitch, 0, 0);
        }

        if (acceleration != 0 && Mathf.Abs(speed) < Mathf.Abs(set_speed) * (1 + tolerance) + tolerance * 10 && Mathf.Abs(speed) > Mathf.Abs(set_speed) * (1 - tolerance) - tolerance * 10)
        {
            jerk = 0;
            speed = set_speed;
        }

        theRigidbody.velocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody
    }

    void Compute_Pitch_Deceleration()
    {
        // When speed larger than 10 km/h, pitch increases to 0.5 degrees
        if (speed > 10f && delta_distance < 17f) 
        {
            Timer1 += Time.deltaTime;
            pitch = Timer1 * 6;
            
            if (pitch < 0.5f)                           // Pitches larger than 0.5 degrees are capped at 0.5 degrees.
            {
                this.transform.Rotate(pitch, 0, 0);
            }
            else
            {
                this.transform.Rotate(0.5f, 0, 0);
            }
        }

        // When speed smaller than 10 km/h, pitch slowely decreases from 0.5 degrees
        else if (speed < 10f && delta_distance < 17f) 
        {
            Timer2 += Time.deltaTime;
            pitch = 0.5f - (Timer2);

            if (pitch >= 0)
            {
                this.transform.Rotate(pitch, 0, 0);
            }

        }

        // Once distance is far enough and speed is almost zero, decrease pitch even more
        else if (Double.IsNaN(speed) || speed > 0f && speed < 5f && delta_distance > 17f) 
        {
            Timer2 += Time.deltaTime;
            pitch = 0.5f - (Timer2);

            if (pitch >= 0)
            {
                this.transform.Rotate(pitch, 0, 0);
            }

            speed = 0f;
            theRigidbody.velocity = new Vector3(0, 0, 0);   // Apply zero velocity 
        }

        // If car is standing still, change pitch back to zero.
        if (speed <= 0 && delta_distance > 16f)  
        {
            Timer2 += Time.deltaTime;
            pitch = 0.5f - (Timer2);

            if (pitch >= 0)                                 // Apply pitch only when larger than 0 degrees.
            {
                this.transform.Rotate(pitch, 0, 0);
            }
            Debug.Log(Timer2);

            // After standing still for two seconds, passenger can initiate driving again by pressing space.
            Resume_Driving_After_Stop(2f);
        }
    }

    // Function to resume driving after stopping for 'breaktime' seconds.
    void Resume_Driving_After_Stop(float breaktime)
    {
        if (Timer2 >= breaktime)
        {
            if (WaitTrialZ == true || SpaceBar == true) 
            {
                braking = false;
                reset = true;
                set_acceleration = 1f;
                set_speed = 30;
                startlocation = this.gameObject.transform.position.z - 0.10f;
                SpaceBar = false;
            }
            else if (WaitTrialX == true)
            {
                braking = false;
                reset = true;
                set_acceleration = 1f;
                set_speed = 30;
                startlocation = this.gameObject.transform.position.x - 0.10f;
            }
        }
    }

    // Function to decelerate car when braking
    void Decelerate_Car()
    {
        // Compute delta distance for deceleration
        if (WaitTrialX == true || BrakeX == true)
        {
            delta_distance = Mathf.Abs(this.gameObject.transform.position.x - triggerlocation);
        }

        else if (WaitTrialZ == true || BrakeZ == true || SpaceBar == true) // John: input key only put in the z direction for now, since in the experiment the car drives in the z-direction
        {
            delta_distance = Mathf.Abs(this.gameObject.transform.position.z - triggerlocation);
        }

        // Apply delta_distance for deceleration 
        // Formula: v = sqrt(u^2 + 2*a*s) with v = final velocity; u = initial velocity; a = acceleration; s = distance covered. 
        speed = Mathf.Sqrt(900 + 2 * set_acceleration * Mathf.Pow(conversion, 2) * delta_distance); // Application of conversion of km/h to m/s which needs to be squared

        // Slowing down            
        // Compute pitch for deceleration
        Compute_Pitch_Deceleration();
        theRigidbody.velocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody 
    }

    void Reset_Speed_After_Stopping()
    {
        // Accelerating in the Z direction
        if (WaitTrialZ == true || SpaceBar)
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
            WaitTrialZ = false;
            WaitTrialX = false;
        }
    }
}
