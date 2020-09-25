using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteeringWheelController : MonoBehaviour
{
    float steeringWheelAngle;
    float steering;
    float steerSpeed = 0.2f;
    //float steeringWheelMul = -2f;

    private void FixedUpdate()
    {
        steering = Input.GetAxis("Steer")*90f; //1 = 90 degrees
        Debug.Log(steering);
        steeringWheelAngle = Mathf.Lerp(steeringWheelAngle, -steering, steerSpeed);

       
        transform.localRotation = Quaternion.AngleAxis(steeringWheelAngle, Vector3.forward);
        
    }
    
}
