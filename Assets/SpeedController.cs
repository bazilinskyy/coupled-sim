using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Navigator), typeof(VehiclePhysics.VPStandardInput))]
public class SpeedController : MonoBehaviour
{
    public float startSlowDownCorner = 25f;
    public float setSpeedCorner = 6f;
    public float setSpeedStraight = 12.5f;
    public float setSpeedSpline = 6f;
    
    [Header("PID constants")]
    public float KProportional = 0.01f;
    public float KIntegral = 0.01f;
    public float KDifferential = 0.01f;

    private Navigator navigator;
    private Rigidbody carRB;
    private VehiclePhysics.VPStandardInput carController;


    private float lastError;
    private float intergralSum;

    private void Start()
    {
        navigator = GetComponent<Navigator>();
        carRB = GetComponent<Rigidbody>();
        carController = GetComponent<VehiclePhysics.VPStandardInput>();

        /*//Get input axis
        if(navigator.experimentManager != null) { 
            List<string> inputsCar = navigator.experimentManager.GetCarControlInput();
            steer = inputsCar[0]; gas = inputsCar[1]; brake = inputsCar[2];
        }*/
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        if(transform.position.z >= -20) { carController.externalThrottle = 0; }
        else { carController.externalThrottle = 1; }
        if (navigator.target == null) { return; }
        if (!navigator.experimentManager.automateSpeed) { return; }

        float setSpeed;
        if (navigator.target.operation == Operation.Straight || navigator.target.operation == Operation.EndPoint || navigator.target.operation == Operation.None) { setSpeed = setSpeedStraight; }
        else if (navigator.target.operation == Operation.SplinePoint) { setSpeed = setSpeedSpline; }
        else 
        {
            //Upcoming corner -> set speed depended on distance to corner
            float distanceToCorner = Vector2.Distance(transform.position, navigator.target.transform.position);
            if (distanceToCorner <= startSlowDownCorner) { setSpeed = setSpeedCorner; }
            else { setSpeed = setSpeedStraight; }
        }


        
        float check1 = Mathf.Min(PIDController(setSpeed, carRB.velocity.magnitude, Time.deltaTime), (1f - carController.externalThrottle));
        float controlledPID = Mathf.Max(check1, 0f);

        Debug.Log($"1f - {carController.externalThrottle} = {1f - carController.externalThrottle}...");
        Debug.Log($"Min({PIDController(setSpeed, carRB.velocity.magnitude, Time.deltaTime)}, {1f - carController.externalThrottle}) = {check1}...");
        Debug.Log($"Min({check1}, {0}) = {controlledPID}...");

        carController.externalThrottle += controlledPID;
        //Keep between 0 and 1
      /*  if (carController.externalThrottle >=1f) { carController.externalThrottle = 1f; }
        if (carController.externalThrottle <= 0) { carController.externalThrottle = 0.01f; }
        //Add to maximum 1
        else 
        {
             }*/


        Debug.Log($"Current speed, setspeed, throttle = {carRB.velocity.magnitude}, {setSpeed}, {carController.externalThrottle}, want to add; {controlledPID}");
    }

    public float PIDController(float setpoint, float actual, float timeFrame)
    {
        float presentError = setpoint - actual;
        intergralSum += presentError * timeFrame;
        float deriv = (presentError - lastError) / timeFrame;
        lastError = presentError;
        return presentError * KProportional + intergralSum * KIntegral + deriv * KDifferential;
    }
}
