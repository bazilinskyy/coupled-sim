using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using VarjoExample;
using Valve.VR;

public class Network_remote_varjo : MonoBehaviour
{
    public GameObject SyncGazePedestrian;
    public string hmdserial = "LHR-7863A1E8"; //LHR-7863A1E8 //LHR-85C3EF8C

    // Gaze status
    public float status_pe;

    // Define data to network for NetworkObject_1
    float distance_pe;  long Frame_pe;      long CaptureTime_pe;

    // Define data to network for NetworkObject_2
    Vector3 HmdPos_pe;
    Vector3 HmdRot_pe;

    // Define data to network for NetworkObject_3
    double LeftEyePupilSize_pe; double RightEyePupilSize_pe;
    double FocusDistance_pe; double FocusStability_pe;

    // Define data to network for NetworkObject_4
    Vector3 gazeRayForward_pe;
    Vector3 gazeRayDirection_pe;

    // Define data to network for NetworkObject_5
    Vector3 gazePosition_pe;
    Vector3 gazeRayOrigin_pe;

    void FixedUpdate()
    {
        var script = SyncGazePedestrian.GetComponent<VarjoGazeRay_CS_1>();

        // Gaze status
        status_pe = (float)script.getGazeStatus();

        // Check whether eye-tracking is established first
        if (script.getGazeStatus() != VarjoPlugin.GazeStatus.INVALID)
        {
            // Get data for NetworkObject_1
            distance_pe = script.getGazeRayHit().distance;      Frame_pe = script.Frame;    CaptureTime_pe = script.CaptureTime;

            // Get data for NetworkObject_2
            HmdPos_pe = script.hmdposition;
            HmdRot_pe = script.hmdrotation;

            // Get data for NetworkObject_3
            LeftEyePupilSize_pe = script.LeftPupilSize; RightEyePupilSize_pe = script.RightPupilSize;
            FocusDistance_pe = script.FocusDistance; FocusStability_pe = script.FocusStability;

            // Get data for NetworkObject_4
            gazeRayForward_pe = script.gazeRayForward;
            gazeRayDirection_pe = script.gazeRayDirection;

            // Get data for NetworkObject_5
            gazePosition_pe = script.gazePosition;
            gazeRayOrigin_pe = script.gazeRayOrigin;

            // Apply the right poses according to the gameobject name
            if (gameObject.name == "NetworkObject_1" && SteamVR.instance.hmd_SerialNumber == hmdserial) 
            {
                transform.position = new Vector3(status_pe, distance_pe, Frame_pe);
                Debug.LogError($"1 - status = {status_pe}, dis = {distance_pe}, and frame = {Frame_pe}");
                Debug.LogError($"1 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_2" && SteamVR.instance.hmd_SerialNumber == hmdserial) 
            {
                transform.position = new Vector3(CaptureTime_pe, (float)LeftEyePupilSize_pe, (float)RightEyePupilSize_pe);
                Debug.LogError($"2 - capturetime = {CaptureTime_pe}, lefteyesize = {(float)LeftEyePupilSize_pe}, and right = {(float)RightEyePupilSize_pe}");
                Debug.LogError($"2 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_3" && SteamVR.instance.hmd_SerialNumber == hmdserial)  
            {
                transform.position = new Vector3((float)FocusDistance_pe, (float)FocusStability_pe, 0);
                Debug.LogError($"3 - foc dist = {(float)FocusDistance_pe} and foc stab = {(float)FocusStability_pe}");
                Debug.LogError($"3 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_4" && SteamVR.instance.hmd_SerialNumber == hmdserial)  
            {
                transform.position = HmdPos_pe;
                Debug.LogError($"4 - hmdpos = {HmdPos_pe}");
                Debug.LogError($"4 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_5" && SteamVR.instance.hmd_SerialNumber == hmdserial) 
            {
                transform.position = HmdRot_pe;
                Debug.LogError($"5 - hmdrot = {HmdRot_pe}");
                Debug.LogError($"5 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_6" && SteamVR.instance.hmd_SerialNumber == hmdserial)
            {
                transform.position = gazeRayForward_pe;
                Debug.LogError($"6 - gazerayforward = {gazeRayForward_pe}");
                Debug.LogError($"6 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_7" && SteamVR.instance.hmd_SerialNumber == hmdserial)
            {
                transform.position = gazeRayDirection_pe;
                Debug.LogError($"7 - gazeraydirection = {gazeRayDirection_pe}");
                Debug.LogError($"7 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_8" && SteamVR.instance.hmd_SerialNumber == hmdserial)
            {
                transform.position = gazePosition_pe;
                Debug.LogError($"8 - gazeposition = {gazePosition_pe}");
                Debug.LogError($"8 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_9" && SteamVR.instance.hmd_SerialNumber == hmdserial)
            {
                transform.position = gazeRayOrigin_pe;
                Debug.LogError($"9 - gazerayorigin = {gazeRayOrigin_pe}");
                Debug.LogError($"9 - trans pos = {transform.position}");
            }

        }
        else if(script.getGazeStatus() == VarjoPlugin.GazeStatus.INVALID)
        {
            // Change gaze status to invalid
            if (gameObject.name == "NetworkObject_1" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8") //LHR-7863A1E8 //LHR-85C3EF8C
            {
                transform.position = new Vector3(status_pe, 0, 0);
                Debug.LogError($"1.2 - status = {status_pe}");
                Debug.LogError($"1.2 - trans pos = {transform.position}");
            }
        }
    }
}
