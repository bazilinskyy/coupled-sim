using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestHandCalibration : MonoBehaviour
{
    // Start is called before the first frame update
    public Transform LeapRig;
    public Transform cam;
    public Transform driverView;
    public GameObject steeringWheelPrefab;

    public Transform sphereL;
    public Transform sphereR;

    public Transform sphere1;
    public Transform sphere2;

    private CalibrateUsingHands calibration;
    private GameObject steeringWheelObject;

    private void Awake()
    {
        calibration = driverView.GetComponent<CalibrateUsingHands>();
        Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL);
    }
    // Update is called once per frame
    void Update()
    {


        if (Input.GetAxis("SteerButtonLeft") == 1)
        {
            bool success = calibration.SetPositionUsingHands();
            if (success) { SpawnSteeringWheel(); }
        }

        if (sphereL != null && sphereR != null)
        {
            calibration.SetLeftHand();
            calibration.SetRightHand();
            sphereL.transform.position = calibration.GetLeftHandPos();
            sphereR.transform.position = calibration.GetRightHandPos();

            if (steeringWheelObject != null)
            {
                sphere1.transform.position = steeringWheelObject.transform.Find("CentreWrists").position;
                sphere2.transform.position = (calibration.GetLeftHandPos() + calibration.GetRightHandPos()) / 2;
            }
        }

        if (Input.GetAxis("SteerButtonRight") == 1)
        {
            SetCameraPosition(driverView.position, driverView.rotation);
            return;
            if (steeringWheelObject == null) { Debug.Log("No steeringwheel...."); return; }
            //Debug.Log($"left and right hand off set: {calibration.GetLeftHandPos() - steeringWheelObject.transform.Find("LeftWristPosition").position}, {calibration.GetRightHandPos() - steeringWheelObject.transform.Find("RightWristPosition").position}...");
            Vector3 error = (calibration.GetLeftHandPos() + calibration.GetRightHandPos()) / 2 - steeringWheelObject.transform.Find("CentreWrists").position;
            /*Vector3 relRight = calibration.GetRightHandPos() - steeringWheelObject.transform.position;
            Debug.Log($"Left hand pos: ({relLeft.x}, {relLeft.y}, {relLeft.z})...");*/
            Debug.Log($"error pos: ({error.x}, {error.y}, {error.z})...");
            //SetCameraPosition(driverView.position, driverView.rotation);
        }
    }

    void SetCameraPosition(Vector3 goalPos, Quaternion goalRot)
    {
        LeapRig.position = goalPos;
        LeapRig.rotation = goalRot;
        //Set camera position with correction from Rig to actual varjo cam.

        //Varjo.VarjoPlugin.ResetPose(false, Varjo.VarjoPlugin.ResetRotation.ALL);

        Vector3 correctedGoalPos = goalPos - cam.position;
        LeapRig.position = goalPos + correctedGoalPos;

        Debug.Log($"Verification setCamera: {cam.position}, {driverView.position}...");


    }
    void SpawnSteeringWheel()
    {
        //if (camType != MyCameraType.Leap) { return; } //This function is made for when we are using leap motion controller with hand tracking
        if (steeringWheelPrefab == null) { return; }
        if (steeringWheelObject != null) { Destroy(steeringWheelObject); }

        steeringWheelObject = Instantiate(steeringWheelPrefab);

        //Make desired rotation for the steeringwheel
        Vector3 handVector = calibration.GetRightToLeftHand();//steeringWheelObject.transform.Find("LeftHandPosition").transform.position - steeringWheelObject.transform.Find("RightHandPosition").transform.position;
        Quaternion desiredRot = Quaternion.LookRotation(Vector3.Cross(handVector, Vector3.up), Vector3.up);

        //steeringWheelObject.transform.rotation = desiredRot;// CameraTransform().rotation;
        steeringWheelObject.transform.position = cam.position - calibration.GetSteeringWheelToCam();

    }
}

