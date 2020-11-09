using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlBrakeForce : MonoBehaviour
{
    public float maxBrakeForce = 2000f;
    public string brakeInput;

    private VehiclePhysics.VPVehicleController controller;

    private void Awake()
    {
        controller = gameObject.GetComponent<VehiclePhysics.VPVehicleController>();
    }
    // Update is called once per frame
    void Update()
    {
        //Between -1 (no braking, and 1 full braking)
        float brake = Input.GetAxis(brakeInput);
        
        if (brake == -1) { controller.brakes.maxBrakeTorque = 0f; }
        else {
            float brakeForce = maxBrakeForce * (brake + 1) / 2; 
            controller.brakes.maxBrakeTorque = brakeForce; }

    }
}
