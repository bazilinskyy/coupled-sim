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

    //Contact condition of wheels through fixing their y
    private float InitialY_FL;
    private float InitialY_FR;
    private float InitialY_RL;
    private float InitialY_RR;

    private AICar AICarRef;

    // Use this for initialization
    void Start () {
        InitialY_FL = FrontLeft.position.y;
        InitialY_FR = FrontRight.position.y;
        InitialY_RL = RearLeft.position.y;
        InitialY_RR = RearRight.position.y;
        //AICarRef = GetComponent<AICar>();
        AICarRef = GetComponentInParent<AICar>();
    }
	
	void Update () {
        
        speed = Mathf.Clamp(GetComponent<Rigidbody>().velocity.magnitude,-30,30); // Not Needed but might be useful, if rotation looks weird because of framerate
        if (speed < 0.05f && speed < 0.05f)
        {
            speed = 0f;
        }
        RotationSpeed = 360f * speed / 3.6f / Mathf.PI / WheelDiameter;

        //Front Left
        FrontLeft.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        FrontLeft.position = new Vector3 (FrontLeft.position.x, InitialY_FL ,FrontLeft.position.z);
        //Front Right
        FrontRight.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        FrontRight.position = new Vector3(FrontRight.position.x, InitialY_FR, FrontRight.position.z);
        //Rear Left
        RearLeft.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        RearLeft.position = new Vector3(RearLeft.position.x, InitialY_RL, RearLeft.position.z);
        //Rear Right
        RearRight.Rotate(RotationSpeed * Time.deltaTime, 0, 0);
        RearRight.position = new Vector3(RearRight.position.x, InitialY_RR, RearRight.position.z);
    }
}
