using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

// This script was adapted from the study of De Clercq.

public class AICar : MonoBehaviour {

    // Motion related variables
    public float set_speed = 50;                                           // Velocity of cars in simulation
    public float set_acceleration = 2;                                     // Acceleration of cars in simulation
    public float jerk = 2;                                                 // Jerk of cars in simulation
    public float turn_rate_degree = 360;                                  
    public float speed;                                                    // Speed variable used for computations in this script
    public float acceleration;                                             // Acceleration variable used for computations in this script
    public float t = 0;                                                    // Time variable used for computations in this script
    private float pitch = 0;                                               // Pitch of the car
    private float tolerance = 0.05f;                                       // Used to determine if the changed variable reached its target value. 2% seems to fit. (De Clercq)
    public int HasPitch = 1;                                               
    private Transform rotationAxis; 
    public bool SpeedReset = true;

    // Game related variables
    public Transform target;                                               // Target on waypoint circuit that cars will follow
    private GameObject[] Clones;                                           // List of other cars
    private GameObject Participant;                                        // Variable for the participant
    private GameObject LastCar;                                            // Variable for the Last car in the train
    private Rigidbody theRigidbody;                                        // Variable for the rigid body this script is attached to
    [SerializeField]
    WheelCollider[] Wheels;
     
    public bool Accident;                                                  // Intended use in case of collission between participant and vehicle, not used in experiment of Kooijman
    public bool TrialEnd;                                                  // Used to know when Trial was at an end 
    public bool crashbutton = false;                                       // Intended use in case of collission between participant and vehicle, not used in experiment of Kooijman
    public bool trialbutton = false;                                       // Used to know when Trial was at an end 
    private Rect windowRect = new Rect(425, 225, 300, 50);                              
    public bool paused = false;                                            // Used to know when Trial was paused

    // Animation related variables
    public Animator anim;
    private int HMIref = 0;
    private int layer;
  
    // Use this for initialization
    void Start ()
    {        
        theRigidbody = GetComponent<Rigidbody>();                          // Grabs information of the rigid body this script is attached to.
        layer = gameObject.layer;                                          // Grabs layer of object this script is attached to. 
        rotationAxis = this.transform;                                     // Grabs orientation of the object this script is attached to.
        speed = set_speed;                                                 // Sets speed of object
        acceleration = set_acceleration;                                   // Sets acceleration of object
        target = GetComponent<WaypointProgressTracker>().target;           // Sets intermediate target on the circuit
        TrialEnd = false;                                                  // Bool to determine end of trial
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

                        
        if (jerk != 0)
        {
            acceleration = set_acceleration;
            t += Mathf.Abs(jerk) / Mathf.Abs(set_acceleration) * Time.fixedDeltaTime;
            pitch = acceleration / 3 * HasPitch;
        }

        if (set_acceleration > 0f && set_speed > speed || set_acceleration < 0f && set_speed < speed)
        {
            speed = speed + set_acceleration * Time.fixedDeltaTime * 3.6f;
            pitch = acceleration / 3 * HasPitch;
        }

        if (acceleration != 0 && Mathf.Abs(speed) < Mathf.Abs(set_speed) * (1 + tolerance) + tolerance * 10 && Mathf.Abs(speed) > Mathf.Abs(set_speed) * (1 - tolerance) - tolerance * 10)
        {
            jerk = 0;
            speed = set_speed;
            t = 0;
            pitch = acceleration / 3 * HasPitch;
        }

        if (acceleration == 0 && pitch != 0)
        {
            pitch = Mathf.Lerp(set_acceleration / 3, 0, t) * HasPitch;
            t += Time.fixedDeltaTime * 1.5f;
        }

        this.transform.Rotate(-pitch, 0, 0);

        if (speed == 0 && SpeedReset)    // If object is standing still, the co-routine slowly lets the object pick up speed again
        {
            StartCoroutine(ResetSpeed());
        }

        var forward = rotationAxis.forward;
        forward.y = 0;
        if (set_speed == 0)
        {
            foreach (var wheel in Wheels)
            {
                wheel.brakeTorque = 100;
            }
        }
        else
        {
            foreach (var wheel in Wheels)
            {
                wheel.brakeTorque = 0;
            }
        }
        
        if (speed == 0)
        {
            theRigidbody.isKinematic = true;
        }
        else
        {
            theRigidbody.isKinematic = false;
            theRigidbody.velocity = forward.normalized * speed / 3.6f; // Application of calculated velocity to Rigidbody 
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (this.enabled == false)
        {
            return;
        }

        else if (other.gameObject.CompareTag("WP"))
        {
            var speedSettings = other.GetComponent<SpeedSettings>();
            set_speed = speedSettings.speed;
            set_acceleration = speedSettings.acceleration;
            SpeedReset = speedSettings.resetSpeedAfterStop;
        }

        // Change of tag here that causes to change the eHMI animation and start deceleration.
        else if (other.gameObject.CompareTag("HMI"))
        {
            // HMI Animation         
            layer = gameObject.layer;
            if (anim != null && layer == (12))
            {
                HMIref = 1;
                anim.SetInteger("HMIRef", HMIref);
            }
            if (gameObject.layer == 12)
            {
                set_speed = 0;
                set_acceleration = -3.5f;
                jerk = -Mathf.Abs(other.GetComponent<SpeedSettings>().jerk);
            }
        }
        else if (other.gameObject.CompareTag("HMI2"))
        {
            // HMI Animation         
            layer = gameObject.layer;
            if (anim != null && layer == (13))
            {
                HMIref = 1;
                anim.SetInteger("HMIRef", HMIref);                
            }
            if (gameObject.layer == 13)
            {                
                set_speed = 0;
                set_acceleration = -4f;
                jerk = -Mathf.Abs(other.GetComponent<SpeedSettings>().jerk);
            }
        }

        else if (other.gameObject.CompareTag("HMI3"))
        {
            // HMI Animation         
            layer = gameObject.layer;
            if (anim != null && layer == (14))
            {
                HMIref = 1;
                anim.SetInteger("HMIRef", HMIref);              
            }
            if (gameObject.layer == 14)
            {                
                set_speed = 0;
                set_acceleration = -5f;
                jerk = -Mathf.Abs(other.GetComponent<SpeedSettings>().jerk);
            }
        }
        else if (other.gameObject.CompareTag("Pedestrian")) //Addition by Lars Kooijman, not used after all.
        {
            Accident = true;
        }
    }
   
    void OnGUI()
    {
        if (Accident == true)
        {
            // Register the window. 
            windowRect = GUI.Window(0, windowRect, DoMyWindow, "Crossing");
            paused = true;
        }
    }

    // Make the contents of the window
    void DoMyWindow(int windowID)
    {
        if (GUI.Button(new Rect(25, 20, 250, 20), "You've Crashed into the Car!"))
        {
            crashbutton = true;                      
        }
    }

    IEnumerator ResetSpeed()
    {        
        SpeedReset = true;
        if (gameObject.layer == 12)
        {
            yield return new WaitForSeconds(5);
            t = 0;
            set_speed = 50;
            set_acceleration = 3;
            jerk = 2;
            pitch = acceleration / 3 * HasPitch;
            if (anim != null)
            {
                anim.SetInteger("HMIRef", 0);
            }           
        }
        else if (gameObject.layer == 13)
        {
            yield return new WaitForSeconds(4.5f);
            t = 0;
            set_speed = 50;
            set_acceleration = 2;
            jerk = 2;
            pitch = acceleration / 3 * HasPitch;
            if (anim != null)
            {
                anim.SetInteger("HMIRef", 0);
            }
        }
        else if (gameObject.layer == 14)
        {
            yield return new WaitForSeconds(4f);
            t = 0;
            set_speed = 50;
            set_acceleration = 2;
            jerk = 2;
            pitch = acceleration / 3 * HasPitch;
            if (anim != null)
            {
                anim.SetInteger("HMIRef", 0);
            }
        }
    }
}
