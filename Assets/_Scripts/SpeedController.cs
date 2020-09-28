using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Navigator), typeof(VehiclePhysics.VPStandardInput))]
public class SpeedController : MonoBehaviour
{

    //Lets the vehicle drive and brake according to the set waypoint target using the VPStandardInput speed limit
    [Header("Speed Limits")]
    public float speedLimitCorner = 6f;
    public float speedLimit = 12f;
    public float speedLimitSpline = 10f;

    [Header("Tweaking Variables")]
    public float straightDistance = 30f;

    public float maxBrakeForce = 1000f;
    public float brakeIncrement = 30f;

    
    private Waypoint target;

    public float maxThrottle = 0.2f;
    private float externalThrottle = 0f;
    
    private Waypoint targetWaypoint;
    private Navigator navigator;
    private Rigidbody carRB;
    private VehiclePhysics.VPVehicleController carController;
    private VehiclePhysics.VPStandardInput carInput;
    private VehiclePhysics.SpeedControl.Settings speedSettings;
    private ControlBrakeForce carBrake;



    private void Start()
    {
        navigator = GetComponent<Navigator>();
        carRB = GetComponent<Rigidbody>();
        carController = GetComponent<VehiclePhysics.VPVehicleController>();
        carInput = GetComponent<VehiclePhysics.VPStandardInput>();
        carBrake = GetComponent<ControlBrakeForce>();
        speedSettings = new VehiclePhysics.SpeedControl.Settings();
        speedSettings.speedLimiter = true;
    }

    void FixedUpdate()
    {

        if (navigator.target == null) { return; }
        if (navigator.experimentManager == null) { return; }
        if (!navigator.experimentManager.automateSpeed) { return; }
        //If at finish of experiment set handbrake.
        if (!navigator.experimentManager.gameState.isExperiment()) 
        { 
            carInput.externalBrake = 1f; 
            carController.brakes.maxBrakeTorque = 1000f; 
            return; 
        }
        else { carInput.externalBrake = 0f; carController.brakes.maxBrakeTorque = 0f; }
        
        if(targetWaypoint == null) { targetWaypoint = navigator.target; }

        float setSpeed = speedLimit;
        if (OnStraight())
        {
            Debug.Log("On straight...");
            setSpeed = speedLimit;
            ToggleGas(true);
        }
        else if (OnCornerApproach())
        {
            Debug.Log("Aproaching corner...");
            ToggleGas(false);
            setSpeed = speedLimitCorner;
            /*float distanceToCorner = Vector3.Distance(transform.position, navigator.target.transform.position) - maxDistanceToCorner;
            //Close to corner is angle = 0, far is angle =pi/2
            float equivalentAngle = Mathf.Clamp01((1 - (distanceToCorner / distanceToStartSlowing))) * Mathf.PI / 2;
            setSpeed = cornerSpeed + Mathf.Cos(equivalentAngle) * (maxSpeed - cornerSpeed);*/
        }
        else if (OnCorner())
        {
            ToggleGas(false);
            setSpeed = speedLimitCorner;
            Debug.Log("On corner...");
        }
        else if (OnSpline())
        {
            Debug.Log("On spline...");
            setSpeed = speedLimitSpline;
            ToggleGas(true);
        }
        else { Debug.Log("Loop hole should never get here! (SpeedController.cs)..."); }

        if (setSpeed != speedSettings.speedLimit)
        {
            Debug.Log($"Speed Limit: {setSpeed}, current speed {carRB.velocity.magnitude}...");

            speedSettings.speedLimit = setSpeed;
            carController.speedControl = speedSettings;
        }

        //Update target waypoint
        targetWaypoint = navigator.target;
    }
 
    void ToggleGas(bool toggle)
    {
        float throttleIncrement;
        if (toggle) { throttleIncrement = 0.005f; }
        else { throttleIncrement = -0.01f; }

        //Increase/decrease throttle with boundaries
        if (externalThrottle < maxThrottle && externalThrottle >= 0) { externalThrottle += throttleIncrement; }
        else if (externalThrottle >= maxThrottle) { externalThrottle = maxThrottle; }
        else { externalThrottle = 0; }

        //Set throttle
        carInput.externalThrottle = externalThrottle;
    }

    bool OnStraight() 
    {
        //On straight when distance to corner is less than straightDistance
        if(targetWaypoint.operation == Operation.Straight) { return true; }
        if(Vector3.Distance(targetWaypoint.transform.position, transform.position) > straightDistance) { return true; }
        else { return false; }
    }
    bool OnCornerApproach()
    {
        //On corner approach when closer than straightDistance and did not pass waypoint target
        if (!targetWaypoint.operation.IsTurn()) { return false; }
        if (navigator.PassedTargetWaypoint()) { return false; }
        if (Vector3.Distance(targetWaypoint.transform.position, transform.position) > straightDistance) { return false; }
        else { return true; }
    }
    bool OnCorner()
    {
        //Navigator only changes target waypoint after a certain distance. So when we passed target but no new target is set we are in on the corner

        //On corner when navigator.target == target and navigator.PAssedTarget()
        if (targetWaypoint == navigator.target && navigator.PassedTargetWaypoint()) { return true; }
        else { return false; }
    }

    bool OnSpline()
    {
        if (targetWaypoint.previousWaypoint.operation == Operation.SplinePoint) { return true; }
        else { return false; }
    }
}
/*
              float setSpeed = setSpeedLimit;
              //The target will never be a splinepoint (as it is just to simulate a beneded road though speed should be different at these points)
              if (navigator.target.previousWaypoint.operation == Operation.SplinePoint) { setSpeed = setSpeedSpline; }

              else if (navigator.target.operation == Operation.Straight || navigator.target.operation == Operation.None) {setSpeed = setSpeedLimit; }

              //If a corner is upcoming we check how cloase we are and brake if nescerry
              if (navigator.target.operation == Operation.TurnLeftLong || navigator.target.operation == Operation.TurnRightLong || navigator.target.operation == Operation.TurnRightShort) 
              {
                  //Upcoming corner -> set speed depended on distance to corner
                  setSpeed = CalculateSpeedCorner(setSpeed, setSpeedCorner, brakingDistance, cornerDistance);
              }

              //Endpoint and finishewd navigation logic
              if (navigator.target.operation == Operation.EndPoint) { setSpeed = CalculateSpeedEndPoint(setSpeed, brakingDistance); }
              if (navigator.navigationFinished){ setSpeed = 0f; }


              //print($"Speed limit: {setSpeed}, current speed: {carRB.velocity.magnitude}, distance to target = {Vector3.Distance(transform.position, navigator.target.transform.position)}, brake {carController.brakes.maxBrakeTorque} ...");

              //Set speed limit to car controller
              if (setSpeed != speedSettings.speedLimit)
              {
                  speedSettings.speedLimit = setSpeed;
                  carController.speedControl = speedSettings;
              }

              //Regulate speed
              if (setSpeed < carRB.velocity.magnitude)
              {
                  //carController.engine.frictionTorque += 50;

                  if(externalThrottle > 0 ){ externalThrottle -= 0.02f;}
                  else { externalThrottle = 0f; }

                  carInput.externalThrottle = externalThrottle;
              }
              else 
              {
                  if (externalThrottle < maxThrottle) { externalThrottle += 0.005f; }
                  else { externalThrottle = maxThrottle; }

                  carInput.externalThrottle = externalThrottle;
              }*/


/*  float CalculateSpeedEndPoint(float maxSpeed, float distanceToStartSlowing)
   {
       //Aim to be at zero speed 5meters before end point
       float distanceToEndPoint = Vector3.Distance(transform.position, navigator.target.transform.position) - 5f;

       float equivalentAngle = Mathf.Clamp01((1 - (distanceToEndPoint / distanceToStartSlowing))) * Mathf.PI / 2;

       return Mathf.Cos(equivalentAngle) * (maxSpeed);
   }
   float CalculateSpeedCorner(float maxSpeed, float cornerSpeed,float distanceToStartSlowing, float maxDistanceToCorner)
   {
       float setSpeed;

       float distanceToCorner = Vector3.Distance(transform.position, navigator.target.transform.position) - maxDistanceToCorner;

       //Using cosine, lower down speed limit if close enough to corner
       if (distanceToCorner <= distanceToStartSlowing)
       {
           //Close to corner is angle = 0, far is angle =pi/2
           float equivalentAngle = Mathf.Clamp01((1 - (distanceToCorner / distanceToStartSlowing))) * Mathf.PI/2;

           setSpeed = cornerSpeed + Mathf.Cos(equivalentAngle) * (maxSpeed - cornerSpeed) ;
       }
       else { setSpeed = maxSpeed; }

       return setSpeed;
   }*/
