using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Navigator), typeof(VehiclePhysics.VPStandardInput))]
public class SpeedController : MonoBehaviour
{

    //Lets the vehicle drive and brake according to the set waypoint target using the VPStandardInput speed limit
    [Header("Speed Limits")]
    public float speedLimitCorner = 5f;
    public float speedLimit = 10f;
    public float speedLimitSpline = 8f;

    [Header("Tweaking Variables")]
    public float cornerBrakingDistance = 20f;
    public float metersLetGoGasEndPoint = 30f;
    public float brakeIncrement = 0.005f;
    public float throttleIncrement = 0.005f;
    public float maxThrottle = 0.8f;
    public float maxBrake = 0.3f;

    public float brakeSpeedMargin = 2f;
    public float letGoBrakeSpeed = 2f;

    private float externalThrottle = 0f;
    private float externalBrake = 0f;

    private bool startDriving = false;

    private Waypoint targetWaypoint;
    private Navigator navigator;
    private Rigidbody carRB;
    private VehiclePhysics.VPVehicleController carController;
    private VehiclePhysics.VPStandardInput carInput;
    private VehiclePhysics.SpeedControl.Settings speedSettings;

    private void Start()
    {
        navigator = GetComponent<Navigator>();
        carRB = GetComponent<Rigidbody>();
        carController = GetComponent<VehiclePhysics.VPVehicleController>();
        carInput = GetComponent<VehiclePhysics.VPStandardInput>();

        speedSettings = new VehiclePhysics.SpeedControl.Settings();
        speedSettings.speedLimiter = true;
    }

    public void StartDriving()
    {
        startDriving = true;
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
            carInput.externalThrottle = 0f;
            startDriving = false;
            return;
        }
        //This bool is adjusted by the expriment manager using StartDriving()
        if (!startDriving) { return; }


        if (targetWaypoint == null) { targetWaypoint = navigator.target; }

        float setSpeed = speedLimit;
        if (OnStraight())
        {
            //Debug.Log($"On straight {carRB.velocity.magnitude}...");
            setSpeed = speedLimit;
            ToggleGas(true, throttleIncrement);
        }
        else if (OnCornerApproach())
        {

            setSpeed = speedLimitCorner;

            //Let go gas fast
            ToggleGas(false, 0.05f);

            //Brake to get down to desired speed
            if (carInput.externalThrottle == 0) { BrakeForCorner(brakeIncrement, speedLimitCorner, carRB, carInput); }

            //Debug.Log($"Aproaching corner {carRB.velocity.magnitude}, braking with {carInput.externalBrake} * {carController.brakes.maxBrakeTorque}...");

        }
        else if (OnCorner())
        {
            ToggleGas(false, throttleIncrement);
            setSpeed = speedLimitCorner;
            //Debug.Log($"On corner {carRB.velocity.magnitude}...");
        }
        else if (OnSpline())
        {
            //Debug.Log($"On spline {carRB.velocity.magnitude}...");
            setSpeed = speedLimitSpline;
            ToggleGas(true, throttleIncrement);
        }
        else if (OnEndPointApproach())
        {
            //Debug.Log($"On end point {carRB.velocity.magnitude}...");
            setSpeed = speedLimitSpline;
            //Let go gas when close
            if (Vector3.Distance(transform.position, targetWaypoint.transform.position) < metersLetGoGasEndPoint) { ToggleGas(false, throttleIncrement); }
            else { ToggleGas(true, throttleIncrement); }

            //Starts brakign when at end point.
            BrakeForEndPoint();
        }
        else
        { Debug.Log("Loop hole should never get here! (SpeedController.cs)..."); }
        
            if (setSpeed != speedSettings.speedLimit)
            {
                //Debug.Log($"Speed Limit: {setSpeed}, current speed {carRB.velocity.magnitude}...");

                speedSettings.speedLimit = setSpeed;
                carController.speedControl = speedSettings;
            }

            //Update target waypoint
            targetWaypoint = navigator.target;
        }

    void BrakeForCorner(float increment, float desiredSpeed, Rigidbody car, VehiclePhysics.VPStandardInput carInput)
    {
        float sign;
        if (car.velocity.magnitude > (desiredSpeed + brakeSpeedMargin)) { sign = 1; }
        else { sign = -1 * letGoBrakeSpeed; }

        externalBrake = Mathf.Clamp(externalBrake + sign * increment, 0, maxBrake);

        carInput.externalBrake = externalBrake;

        //Debug.Log($"Braking with {externalBrake}.....");
    }
    void BrakeForEndPoint()
    {
        if (Vector3.Distance(transform.position, targetWaypoint.transform.position) < 7.5f) { externalBrake = Mathf.Clamp(externalBrake + brakeIncrement, 0, maxBrake); }
        carInput.externalBrake = externalBrake;

    }
    void ToggleGas(bool toggle, float increment)
    {
        //If already at max por at zero we can return
        if (toggle && carInput.externalThrottle == maxThrottle) { return; }
        if (!toggle && carInput.externalThrottle == 0) { return; }

        //else we adjust the toggleing appropriately
        int sign;
        if (toggle) { sign = 1; }
        else { sign = -1; }

        //Increase/decrease throttle with boundaries [0, maxThrottle]
        externalThrottle = Mathf.Clamp(externalThrottle + sign * increment, 0, maxThrottle);

        //Set throttle
        carInput.externalThrottle = externalThrottle;
        carInput.externalBrake = 0f;
    }

    bool OnStraight()
    {
        //On straight when distance to corner is less than straightDistance
        if (targetWaypoint.operation == Operation.Straight) { return true; }
        if (Vector3.Distance(targetWaypoint.transform.position, transform.position) > cornerBrakingDistance) { return true; }
        else { return false; }
    }
    bool OnCornerApproach()
    {
        //On corner approach when closer than straightDistance and did not pass waypoint target
        if (!targetWaypoint.operation.IsTurn()) { return false; }
        if (navigator.PassedTargetWaypoint()) { return false; }
        if (Vector3.Distance(targetWaypoint.transform.position, transform.position) > cornerBrakingDistance) { return false; }
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

    bool OnEndPointApproach()
    {
        if (targetWaypoint.operation == Operation.EndPoint) { return true; }
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
