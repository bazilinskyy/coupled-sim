using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestHandCalibration : MonoBehaviour
{
    // Start is called before the first frame update
    public Transform LeapRig;
    public Transform cam;
    public GameObject steeringWheelPrefab;

    public Transform sphereL;
    public Transform sphereR;

    public Transform leftHandSteering;
    public Transform rightHandSteering;

    private GameObject steeringWheelObject;
    // Update is called once per frame
    void Update()
    {
        if(Input.GetAxis("SteerButtonLeft") == 1) {
            bool success = GetComponent<CalibrateUsingHands>().SetPositionUsingHands();
            if (success) {
                SpawnSteeringWheel();
               /* LeapRig.position = (transform.position + (LeapRig.position - cam.position));
                LeapRig.rotation = (transform.rotation);
                float angleCorrection = Quaternion.Angle(LeapRig.rotation, cam.rotation);
                LeapRig.Rotate(LeapRig.position.normalized, -angleCorrection);*/
            } 
        }

        if (sphereL != null && sphereR != null)
        {
            GetComponent<CalibrateUsingHands>().SetLeftHand();
            GetComponent<CalibrateUsingHands>().SetRightHand();
            sphereL.transform.position = GetComponent<CalibrateUsingHands>().GetLeftHandPos();
            sphereR.transform.position = GetComponent<CalibrateUsingHands>().GetRightHandPos();
        }
    }

/*    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GetComponent<CalibrateUsingHands>().GetRightHandPos(), 0.05f);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(GetComponent<CalibrateUsingHands>().GetLeftHandPos(), 0.05f);

        Gizmos.color = Color.yellow;
        
        Gizmos.DrawSphere(steeringWheelObject.transform.Find("LeftHandPosition").position, 0.05f);
        Gizmos.DrawSphere(steeringWheelObject.transform.Find("RightHandPosition").position, 0.05f);

    }*/
    void SpawnSteeringWheel()
    {
        //if (camType != MyCameraType.Leap) { return; } //This function is made for when we are using leap motion controller with hand tracking
        if (steeringWheelPrefab == null) { return; }
        if (steeringWheelObject != null) { Destroy(steeringWheelObject); }
        Vector3 steeringWheelToCam = GetComponent<CalibrateUsingHands>().GetSteeringWheelToCam();

        steeringWheelObject = Instantiate(steeringWheelPrefab);

        //Make desired rotation for the steeringwheel
        Vector3 handVector = GetComponent<CalibrateUsingHands>().GetRightToLeftHand();//steeringWheelObject.transform.Find("LeftHandPosition").transform.position - steeringWheelObject.transform.Find("RightHandPosition").transform.position;
        Quaternion desiredRot = Quaternion.LookRotation(Vector3.Cross(handVector, Vector3.up), Vector3.up);

        steeringWheelObject.transform.rotation = desiredRot;// CameraTransform().rotation;

        //Set position of steeringWheel
        Vector3 leftHand = GetComponent<CalibrateUsingHands>().GetLeftHandPos();
        Vector3 rightHand = GetComponent<CalibrateUsingHands>().GetRightHandPos();
        Vector3 posLeft = leftHand + (steeringWheelObject.transform.position - steeringWheelObject.transform.Find("LeftWristPosition").position);
        Vector3 posRight = rightHand + (steeringWheelObject.transform.position - steeringWheelObject.transform.Find("RightWristPosition").position);

        steeringWheelObject.transform.position = (posLeft + posRight ) / 2;

    }

}
