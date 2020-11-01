using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlBrakeForce : MonoBehaviour
{
    public float brakeForce = 1250f;
    public string brakeInput;

    private VehiclePhysics.VPVehicleController controller;

    private void Awake()
    {
        controller = gameObject.GetComponent<VehiclePhysics.VPVehicleController>();
    }
    // Update is called once per frame
    void Update()
    {
        float brake = Input.GetAxis(brakeInput);
        if (brake == 1) { controller.brakes.maxBrakeTorque = brakeForce; }
        else { controller.brakes.maxBrakeTorque = 0f; }
        
    }
}
