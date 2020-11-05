﻿using System.Collections;
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
    public float throttleIncrement = 0.002f;
    public float maxThrottle = 0.6f;
    public float maxBrake = 0.3f;

    public float brakeSpeedMargin = 2f;
    public float letGoBrakeSpeed = 2f;

    private float externalThrottle = 0f;
    private float externalBrake = 0f;

    private bool startDriving = false;
    private float setSpeed;

    private Waypoint targetWaypoint;
    private Navigator navigator;
    private Rigidbody carRB;
    private VehiclePhysics.VPVehicleController carController;
    private VehiclePhysics.VPStandardInput carInput;
    private VehiclePhysics.SpeedControl.Settings speedSettings;

    private void Start()
    {
        StartUpFunction();
    }
    void StartUpFunction()
    {
        navigator = GetComponent<Navigator>();
        carRB = GetComponent<Rigidbody>();
        carController = GetComponent<VehiclePhysics.VPVehicleController>();
        carInput = GetComponent<VehiclePhysics.VPStandardInput>();

        speedSettings = new VehiclePhysics.SpeedControl.Settings();
        speedSettings.speedLimiter = true;
    }
    public bool IsDriving() { return startDriving; }
    public void StartDriving(bool input)
    {
        if (input != startDriving) { Debug.Log("Starting speed controller..."); }
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

        if (navigator.target == null) { return; }
        if (navigator.experimentManager == null) { return; }
        if (!navigator.experimentManager.automateSpeed) { return; }
        if (navigator.navigationFinished) { Brake(); return; }

        //This bool is adjusted by the expriment manager using StartDriving()
        if (!startDriving) { Brake(); return; }


        if (targetWaypoint == null) { targetWaypoint = navigator.target; }

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

            //Brake to get down to desired speed
            if (carInput.externalThrottle == 0) { BrakeForCorner(brakeIncrement, speedLimitCorner, carRB, carInput); }

            //Debug.Log($"Aproaching corner {carRB.velocity.magnitude}, braking with {carInput.externalBrake} * {carController.brakes.maxBrakeTorque}...");

        }
        else if (OnCorner())
        {
            setSpeed = speedLimitCorner;
            ToggleGas(setSpeed < carRB.velocity.magnitude, throttleIncrement);
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
            if (Vector3.Distance(transform.position, targetWaypoint.transform.position) < 7.5f) { Brake(); }
        }
        else { Debug.LogError("Loop hole should never get here! (SpeedController.cs)..."); }

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

    void Brake()
    {
        externalBrake = Mathf.Clamp(externalBrake + brakeIncrement, 0, maxBrake);
        carInput.externalBrake = externalBrake;
        carInput.externalThrottle = 0f;
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

        //On corner when navigator.PassedTarget()
        if (navigator.PassedTargetWaypoint()) { return true; }
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
