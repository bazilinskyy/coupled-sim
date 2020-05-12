using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

// This script was adapted from the study of De Clercq.

public class AICar : MonoBehaviour
{
    // Motion related variables
    public float set_speed = 50;                                           // Velocity of cars in simulation
    public float set_acceleration = 3.5f;                                     // Acceleration of cars in simulation
    public float jerk = 0;                                                 // Jerk of cars in simulation
    public float turn_rate_degree = 360;
    private float conversion = 3.6f;
    public float speed;                                                    // Speed variable used for computations in this script
    public float speed_m;
    public float acceleration;                                             // Acceleration variable used for computations in this script
    public float t = 0;                                                    // Time variable used for computations in this script
    private float pitch = 0;                                               // Pitch of the car
    private float tolerance = 0.05f;                                       // Used to determine if the changed variable reached its target value. 2% seems to fit. (De Clercq)
    public int HasPitch = 0;
    private Transform rotationAxis;
    private float Timer1;
    private float Timer2;

    private float TimerStop;
    private float TimerTest;

    public bool braking = false;
    public bool reset = false;

    private float triggerlocation;
    private float startlocation;
    private float delta_distance;

    public bool WaitInputX = false;
    public bool WaitTrialX = false;
    public bool WaitTrialZ = false;

    public static int Yield;


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
    }

    void FixedUpdate()
    {
        TimerTest += Time.deltaTime;
        // Debug.Log(TimerTest.ToString());
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
            speed = set_speed;
            //if (acceleration != 0 && Mathf.Abs(speed) < Mathf.Abs(set_speed) * (1 + tolerance) + tolerance * 10 && Mathf.Abs(speed) > Mathf.Abs(set_speed) * (1 - tolerance) - tolerance * 10)
            //{
            //    jerk = 0;
            //    speed = set_speed;
            //}

            theRigidbody.velocity = rotationAxis.forward * set_speed / conversion; // Application of calculated velocity to Rigidbody

        }

        if (TimerTest >= 8.64f && TimerTest < 8.7f && Yield != 1)
        {
            //Debug.Log(this.gameObject.transform.position.x.ToString());
            triggerlocation = -30;
            braking = true;
            WaitInputX = true;
           
        }

        if ((braking == true) && (reset == false))
        {

            if (speed > 0)
            {
                speed_m = (speed / conversion) - set_acceleration * Time.deltaTime;
                speed = speed_m * conversion;
                //  speed = Mathf.Sqrt(900 + 2 * set_acceleration * Mathf.Pow(conversion, 2) * delta_distance); // Application of conversion of km/h to m/s which needs to be squared
                Debug.Log(speed.ToString());
            }

            // Slowing down            
            // Compute pitch for deceleration
            if (speed > 10f) // When speed larger than 10 km/h, pitch increases to 0.5 degrees
            {
                Timer1 += Time.deltaTime;
                pitch = Timer1 * 6;
                // Pitches larger than 0.5 degrees are capped at 0.5 degrees.
                if (pitch < 0.5f)
                {
                    this.transform.Rotate(pitch, 0, 0);
                }
                else
                {
                    this.transform.Rotate(0.5f, 0, 0);
                }
            }

            else if (speed < 10f) // When speed smaller than 10 km/h, pitch slowely decreases from 0.5 degrees
            {
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                if (pitch >= 0)
                {
                    this.transform.Rotate(pitch, 0, 0);
                }

            }

            else if (Double.IsNaN(speed) || speed > 0f && speed < 5f && delta_distance > 17f) // Once distance is far enough and speed is almost zero, decrease pitch even more
            {
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                if (pitch >= 0)
                {
                    this.transform.Rotate(pitch, 0, 0);
                }

                // speed = 0f;
                theRigidbody.velocity = new Vector3(0, 0, 0); // Apply zero velocity 
            }

            //if (speed <= 0 && delta_distance > 16f)  // If car is standing still, change pitch back to zero.
            if (speed <= 0)  // If car is standing still, change pitch back to zero.

            {
                speed = 0f;
                Timer2 += Time.deltaTime;
                pitch = 0.5f - (Timer2);

                TimerStop += Time.deltaTime;
                Debug.Log(TimerStop.ToString());

                if (pitch >= 0) // Apply pitch only when larger than 0 degrees.
                {
                    this.transform.Rotate(pitch, 0, 0);
                }
                Debug.Log(Timer2);

                if (TimerStop >= 5f) // After standing still for two seconds, passenger can initiate driving again by pressing space.
                {

                    braking = false;
                    reset = true;
                    set_acceleration = 2f;
                    set_speed = 50;
                    startlocation = this.gameObject.transform.position.x - 0.10f;



                }
            }
            theRigidbody.velocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody 
        }
        // This statement is applied when the car stood still and is resetting its speed.
        else if ((braking == false) && (reset == true))
        {
            speed_m = (speed / conversion) + set_acceleration * Time.deltaTime;
            speed = speed_m * conversion;
            
            theRigidbody.velocity = rotationAxis.forward * speed / conversion; // Application of calculated velocity to Rigidbody 

        }
    }
}