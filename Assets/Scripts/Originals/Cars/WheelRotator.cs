using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelRotator : MonoBehaviour {

    public Transform FrontLeft;
    public Transform FrontRight;
    public Transform RearLeft;
    public Transform RearRight;

    public float WheelDiameter = 0.65f;

    private float RotationSpeed;  
    private float speed = 0;
	
	void Update () {
        
        speed = Mathf.Clamp(GetComponent<Rigidbody>().velocity.magnitude,-30,30); // Not Needed but might be useful, if rotation looks weird because of framerate
        if (speed < 0.05f && speed < 0.05f)
        {
            speed = 0f;
        }
        RotationSpeed = 360f * speed / 3.6f / Mathf.PI / WheelDiameter;

        //Front Left
        FrontLeft.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        //Front Right
        FrontRight.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        //Rear Left
        RearLeft.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        //Rear Right
        RearRight.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
    }
}
