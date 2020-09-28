using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteeringWheelController : MonoBehaviour
{
    float steeringWheelAngle;
    float steering;
    string steeringAxis = "Steer";
    float steerSpeed = 0.2f;
    //float steeringWheelMul = -2f;
    private Transform car;
    private VehiclePhysics.VPStandardInput carInput;
    private void Start()
    {
        //The steering wheel should be in (car->physicalCar->Interior)
        car = transform.parent.parent.parent;
        carInput = car.GetComponent<VehiclePhysics.VPStandardInput>();
        if (carInput != null) { steeringAxis = carInput.steerAxis; }
    }
    private void FixedUpdate()
    {
        steering = Input.GetAxis(steeringAxis)*90f; //1 = 90 degrees
        steeringWheelAngle = Mathf.Lerp(steeringWheelAngle, -steering, steerSpeed);       
        transform.localRotation = Quaternion.AngleAxis(steeringWheelAngle, Vector3.forward);
        
    }
    
}
