using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Navigator), typeof(VehiclePhysics.VPStandardInput))]
public class SpeedController : MonoBehaviour
{

    //Lets the vehicle drive and brake according to the set waypoint target using the VPStandardInput speed limit

    public float brakingDistance = 15f;
    public float cornerDistance = 5f; //distance before corner at which we want to be at corner speed
    public float setSpeedCorner = 6f;
    public float setSpeedLimit = 12f;
    public float setSpeedSpline = 10f;
    public float maxBrakeForce = 1000f;
    public float brakeIncrement = 30f;

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

    void Update()
    {

        if (navigator.target == null) { return; }
        if (!navigator.experimentManager.automateSpeed) { return; }
        if (!navigator.experimentManager.gameState.isExperiment()) { carInput.externalHandbrake = 1f; return; }
        else { carInput.externalHandbrake = 0f; }

        
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


        print($"Speed limit: {setSpeed}, current speed: {carRB.velocity.magnitude}, distance to target = {Vector3.Distance(transform.position, navigator.target.transform.position)}, brake {carController.brakes.maxBrakeTorque} ...");
        
        //Set speed limit to car controller
        if (setSpeed != speedSettings.speedLimit)
        {
            speedSettings.speedLimit = setSpeed;
            carController.speedControl = speedSettings;
        }

        //Regulate speed
        if (setSpeed < carRB.velocity.magnitude)
        {
            carInput.externalBrake = 1f;
            if (carController.brakes.maxBrakeTorque < maxBrakeForce) { carController.brakes.maxBrakeTorque += brakeIncrement; }
            carInput.externalThrottle = 0f;

            //print($"Braking with {carController.brakes.maxBrakeTorque} NM, speed: {carRB.velocity.magnitude} > {setSpeed}....");
        }
        else 
        {
            carInput.externalThrottle = 1f;
            carController.brakes.maxBrakeTorque = 0;
            carInput.externalBrake = 0;
            
        }
        

    }
    float CalculateSpeedEndPoint(float maxSpeed, float distanceToStartSlowing)
    {
        float setSpeed = maxSpeed;
        float distanceToEndPoint = Vector3.Distance(transform.position, navigator.target.transform.position);
        
        float equivalentAngle = Mathf.Clamp01((1 - (distanceToEndPoint / distanceToStartSlowing))) * Mathf.PI / 2;

        setSpeed = Mathf.Cos(equivalentAngle) * (maxSpeed);

        return setSpeed;
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
    }
   
}
