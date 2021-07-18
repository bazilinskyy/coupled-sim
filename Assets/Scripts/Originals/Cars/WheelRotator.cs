using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelRotator : MonoBehaviour {

    public Transform FrontLeft;
    public Transform FrontRight;
    public Transform RearLeft;
    public Transform RearRight;

    VehicleBehaviour.Suspension FrontLeftSuspension;
    VehicleBehaviour.Suspension FrontRightSuspension;
    VehicleBehaviour.Suspension RearLeftSuspension;
    VehicleBehaviour.Suspension RearRightSuspension;

    public float WheelDiameter = 0.65f;

    private float RotationSpeed;  
    private float speed = 0;

    private void Start()
    {
        FrontLeft.GetComponent<WheelCollider>().wheelDampingRate = 1000;
        FrontRight.GetComponent<WheelCollider>().wheelDampingRate = 1000;
        RearLeft.GetComponent<WheelCollider>().wheelDampingRate = 1000;
        RearRight.GetComponent<WheelCollider>().wheelDampingRate = 1000;

        FrontLeftSuspension = FrontLeft.GetComponent<VehicleBehaviour.Suspension>();
        FrontRightSuspension = FrontRight.GetComponent<VehicleBehaviour.Suspension>();
        RearLeftSuspension = RearLeft.GetComponent<VehicleBehaviour.Suspension>();
        RearRightSuspension = RearRight.GetComponent<VehicleBehaviour.Suspension>();

        FrontLeftSuspension.enabled = false;
        FrontRightSuspension.enabled = false;
        RearLeftSuspension.enabled = false;
        RearRightSuspension.enabled = false;
    }

    void Update () {
        
        speed = Mathf.Clamp(GetComponent<Rigidbody>().velocity.magnitude,-30,30); // Not Needed but might be useful, if rotation looks weird because of framerate
        if (speed < 0.05f && speed < 0.05f)
        {
            speed = 0f;
        }
        RotationSpeed = 360f * speed / 3.6f / Mathf.PI / WheelDiameter;

        //Front Left
        FrontLeftSuspension.wheelModel.transform.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        //Front Right
        FrontRightSuspension.wheelModel.transform.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        //Rear Left
        RearLeftSuspension.wheelModel.transform.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        //Rear Right
        RearRightSuspension.wheelModel.transform.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
    }
}
