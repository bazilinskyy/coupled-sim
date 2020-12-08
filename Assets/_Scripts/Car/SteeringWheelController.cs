using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteeringWheelController : MonoBehaviour
{
    float steeringWheelAngle;
    float steering;
    string steeringAxis = "Steer";
    float steerSpeed = 0.2f;

    public Transform SteeringWheelObject;

    public Transform rotationAxis;

    private Transform car;
    private VehiclePhysics.VPStandardInput carInput;
    private void Start()
    {
        //The steering wheel should be in (car->physicalCar->Interior)
        try { car = transform.parent.parent.parent; }
        catch {}
        if (car != null) { carInput = car.GetComponent<VehiclePhysics.VPStandardInput>(); }
    }
    private void FixedUpdate()
    {
        if (carInput != null && steeringAxis != carInput.steerAxis) { steeringAxis = carInput.steerAxis; }
        steering = Input.GetAxis(steeringAxis) *100f; //1 = 100 degrees

        steeringWheelAngle = Mathf.Lerp(steeringWheelAngle, -steering, steerSpeed);
        SteeringWheelObject.transform.localRotation = Quaternion.AngleAxis(steeringWheelAngle, -rotationAxis.localPosition.normalized);
    }
    
}
