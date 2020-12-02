using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Navigator), typeof(VehiclePhysics.VPStandardInput))]
public class SpeedController : MonoBehaviour
{

    //Lets the vehicle drive and brake according to the set waypoint target using the VPStandardInput speed limit
    [Header("Speed Limits")]
    private readonly float speedLimitCorner = 5f;
    private readonly float speedLimit = 10f;
    private readonly float speedLimitSpline = 8f;

    [Header("Tweaking Variables")]
    private readonly float cornerBrakingDistance = 20f;
    private readonly float metersLetGoGasEndPoint = 30f;
    private readonly float brakeIncrement = 0.005f;
    private readonly float throttleIncrement = 0.002f;
    private readonly float maxThrottle = 0.6f;
    private readonly float maxBrake = 0.2f;

    private readonly float brakeSpeedMargin = 2f;
    private readonly float letGoBrakeSpeed = 2f;

    private float externalThrottle = 0f;
    private float externalBrake = 0f;

    public bool startDriving = false;
    private float setSpeed;

    private MainManager mainManager;
    private WaypointStruct target;
    private newNavigator navigator;
    private Rigidbody carRB;
    private VehiclePhysics.VPVehicleController carController;
    private VehiclePhysics.VPStandardInput carInput;
    private VehiclePhysics.SpeedControl.Settings speedSettings;
    private bool gaveError=false;
    private void Start()
    {
        StartUpFunction();
    }
    void StartUpFunction()
    {
        mainManager = MyUtils.GetMainManager();
        navigator = GetComponent<newNavigator>();
        carRB = GetComponent<Rigidbody>();
        carController = GetComponent<VehiclePhysics.VPVehicleController>();
        carInput = GetComponent<VehiclePhysics.VPStandardInput>();

        speedSettings = new VehiclePhysics.SpeedControl.Settings();
        speedSettings.speedLimiter = true;
        carController.speedControl = speedSettings;
    }
    public bool IsDriving() { return startDriving; }
    public void StartDriving(bool input)
    {
        if (input != startDriving) { Debug.Log($"Speed controller = {input}..."); }
        startDriving = input;
    }
    public void ToggleDriving()
    {
        startDriving = !startDriving;

        if (startDriving) { Debug.Log("Starting speed controller..."); }
        else { Debug.Log("Stopping speed controller..."); }
    }
    void FixedUpdate()
    {

        if (GetComponent<newNavigator>().target.waypoint == null) { return; }
        if (!mainManager.automateSpeed) { return; }
        if (GetComponent<newNavigator>().navigationFinished) { Brake(); return; }

        //This bool is adjusted by the expriment manager using StartDriving()
        if (!startDriving) { Brake(); return; }

        target = GetComponent<newNavigator>().target;
        if (target.waypoint == null && !gaveError) { Debug.LogError("Could not find target Waypoint...."); gaveError = true; return; }

        setSpeed = speedLimit;
        if (OnStraight())
        {
            //Debug.Log($"On straight {carRB.velocity.magnitude}...");
            setSpeed = speedLimit;
            ToggleGas(true, throttleIncrement);
        }
        else if (OnCornerApproach())
        {
            setSpeed = speedLimitCorner;

            //Let go gas fastm
            if (carRB.velocity.magnitude > setSpeed) { ToggleGas(false, 0.05f); }
            else { ToggleGas(true, throttleIncrement); }

            float distanceToCorner = Vector3.Magnitude(target.waypoint.transform.position - transform.position);
            //Brake to get down to desired speed
            if (carInput.externalThrottle == 0 && distanceToCorner < cornerBrakingDistance) { BrakeForCorner(brakeIncrement, speedLimitCorner, carRB, carInput); }

            //Debug.Log($"Aproaching corner {carRB.velocity.magnitude}, braking with {carInput.externalBrake} * {carController.brakes.maxBrakeTorque}...");

        }
        else if (OnCorner())
        {
            setSpeed = speedLimitCorner;
            ToggleGas(setSpeed > carRB.velocity.magnitude, throttleIncrement);
            //Debug.Log($"On corner {carRB.velocity.magnitude}...");
        }
        else if (OnEndPointApproach())
        {
            //Debug.Log($"On end point {carRB.velocity.magnitude}...");
            setSpeed = speedLimitSpline;
            //Let go gas when close
            if (Vector3.Distance(transform.position, target.waypoint.transform.position) < metersLetGoGasEndPoint) { ToggleGas(false, 0.05f); }
            else { ToggleGas(true, throttleIncrement); }

            //Starts brakign when at end point.
            if (Vector3.Distance(transform.position, target.waypoint.transform.position) < 7.5f) { Brake(); GetComponent<Navigator>().navigationFinished = true; }
        }
        else { Debug.LogError("Loop hole should never get here! (SpeedController.cs)..."); }

        if (setSpeed != speedSettings.speedLimit)
        {
            //Debug.Log($"Speed Limit: {setSpeed}, current speed {carRB.velocity.magnitude}...");

            speedSettings.speedLimit = setSpeed;
            carController.speedControl = speedSettings;
        }

        //Update target waypoint
        target = navigator.target;
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
    void ToggleGas(bool toggle, float increment)
    {
        //return;
        //If already at max por at zero we can return
        if (toggle && carInput.externalThrottle == maxThrottle) { return; }
        if (!toggle && carInput.externalThrottle == 0) { return; }

        //else we adjust the toggleing appropriately
        int sign  = 1;
        if (!toggle) { sign = -1; }

        //Increase/decrease throttle with boundaries [0, maxThrottle]
        externalThrottle = Mathf.Clamp(externalThrottle + sign * increment, 0, maxThrottle);

        //Set throttle
        carInput.externalThrottle = externalThrottle;
        carInput.externalBrake = 0f;
    }
    void Brake()
    {
        externalBrake = Mathf.Clamp(externalBrake + brakeIncrement, 0, maxBrake);
        carInput.externalBrake = externalBrake;
        carInput.externalThrottle = 0f;
    }
    bool OnStraight()
    {
        //On straight when distance to corner is less than straightDistance
        if (target.turn == TurnType.Straight) { return true; }
        else if ((target.turn == TurnType.Right || target.turn == TurnType.Left) && Vector3.Distance(target.waypoint.transform.position, transform.position) > cornerBrakingDistance) { return true; }
        else { return false; }
    }
    bool OnCornerApproach()
    {
        //On corner approach when closer than straightDistance and did not pass waypoint target
        if (target.turn == TurnType.Straight || target.turn == TurnType.None || target.turn == TurnType.EndPoint) { return false; }
        if (Vector3.Distance(target.waypoint.transform.position, transform.position) > cornerBrakingDistance) { return false; }
        else { return true; }
    }
    bool OnCorner()
    {
        //Navigator only changes target waypoint after a certain distance. So when we passed target but no new target is set we are in on the corner
        if (navigator.atWaypoint) { return true; }
        else { return false; }
    }
    bool OnEndPointApproach()
    {
        if (target.turn == TurnType.EndPoint) { return true; }
        else { return false; }
    }

    public TrackProgression GetTrackProgression()
    {
        if(target.waypoint == null) { return TrackProgression.Unkown; }
        if (OnStraight()) { return TrackProgression.Straight; }
        else if (OnCorner()) { return TrackProgression.Corner; }
        else if (OnCornerApproach()) { return TrackProgression.CornerApproach; }
        else { return TrackProgression.EndPointApproach; }

    }
}

public enum TrackProgression
{
    Unkown,
    Straight,
    CornerApproach,
    Corner,
    EndPointApproach
}
