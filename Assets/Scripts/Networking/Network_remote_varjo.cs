using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using VarjoExample;
using Valve.VR;

public class Network_remote_varjo : MonoBehaviour
{
    public GameObject SyncGazePedestrian;
    public string hmdserial = "LHR-85C3EF8C"; //LHR-7863A1E8 //LHR-85C3EF8C



    // Define data to network for NetworkObject_1
    public float status_pe; float distance_pe;  long Frame_pe;

    // Define data to network for NetworkObject_2
    long CaptureTime_pe; double LeftEyePupilSize_pe; double RightEyePupilSize_pe;

    // Define data to network for NetworkObject_3
    double FocusDistance_pe; double FocusStability_pe; int client_nextScene;

    // Define data to network for NetworkObject_4
    Vector3 HmdPos_pe;

    // Define data to network for NetworkObject_5
    Vector3 HmdRot_pe;

    // Define data to network for NetworkObject_6
    Vector3 gazeRayForward_pe;

    // Define data to network for NetworkObject_7
    Vector3 gazeRayDirection_pe;

    // Define data to network for NetworkObject_8
    Vector3 gazePosition_pe;

    // Define data to network for NetworkObject_9
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
            distance_pe = script.getGazeRayHit().distance; Frame_pe = script.Frame;

            // Get data for NetworkObject_2
            CaptureTime_pe = script.CaptureTime; LeftEyePupilSize_pe = script.LeftPupilSize; RightEyePupilSize_pe = script.RightPupilSize;

            // Get data for NetworkObject_3
            FocusDistance_pe = script.FocusDistance; FocusStability_pe = script.FocusStability; //client_nextScene = PersistentManager.Instance.client_nextScene; // Last point was used to network the switch, now using messages instead

            // Get data for NetworkObject_4
            HmdPos_pe = script.hmdposition;

            // Get data for NetworkObject_5
            HmdRot_pe = script.hmdrotation;

            // Get data for NetworkObject_6
            gazeRayForward_pe = script.gazeRayForward;

            // Get data for NetworkObject_7
            gazeRayDirection_pe = script.gazeRayDirection;

            // Get data for NetworkObject_8
            gazePosition_pe = script.gazePosition;

            // Get data for NetworkObject_9
            gazeRayOrigin_pe = script.gazeRayOrigin;

            // Apply the right poses according to the gameobject name
            if (gameObject.name == "NetworkObject_1" && SteamVR.instance.hmd_SerialNumber == hmdserial) 
            {
                transform.position = new Vector3(status_pe, distance_pe, Frame_pe);
                //Debug.LogError($"1 - status = {status_pe}, dis = {distance_pe}, and frame = {Frame_pe}");
                //Debug.LogError($"1 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_2" && SteamVR.instance.hmd_SerialNumber == hmdserial) 
            {
                transform.position = new Vector3(CaptureTime_pe, (float)LeftEyePupilSize_pe, (float)RightEyePupilSize_pe);
                //Debug.LogError($"2 - capturetime = {CaptureTime_pe}, lefteyesize = {(float)LeftEyePupilSize_pe}, and right = {(float)RightEyePupilSize_pe}");
                //Debug.LogError($"2 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_3" && SteamVR.instance.hmd_SerialNumber == hmdserial)  
            {
                transform.position = new Vector3((float)FocusDistance_pe, (float)FocusStability_pe, 0); //client_nextScene);
                //Debug.LogError($"3 - foc dist = {(float)FocusDistance_pe} and foc stab = {(float)FocusStability_pe}");
                //Debug.LogError($"3 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_4" && SteamVR.instance.hmd_SerialNumber == hmdserial)  
            {
                transform.position = HmdPos_pe;
                //Debug.LogError($"4 - hmdpos = {HmdPos_pe}");
                //Debug.LogError($"4 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_5" && SteamVR.instance.hmd_SerialNumber == hmdserial) 
            {
                transform.position = HmdRot_pe;
                //Debug.LogError($"5 - hmdrot = {HmdRot_pe}");
                //Debug.LogError($"5 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_6" && SteamVR.instance.hmd_SerialNumber == hmdserial)
            {
                transform.position = gazeRayForward_pe;
                //Debug.LogError($"6 - gazerayforward = {gazeRayForward_pe}");
                //Debug.LogError($"6 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_7" && SteamVR.instance.hmd_SerialNumber == hmdserial)
            {
                transform.position = gazeRayDirection_pe;
                //Debug.LogError($"7 - gazeraydirection = {gazeRayDirection_pe}");
                //Debug.LogError($"7 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_8" && SteamVR.instance.hmd_SerialNumber == hmdserial)
            {
                transform.position = gazePosition_pe;
                //Debug.LogError($"8 - gazeposition = {gazePosition_pe}");
                //Debug.LogError($"8 - trans pos = {transform.position}");
            }
            if (gameObject.name == "NetworkObject_9" && SteamVR.instance.hmd_SerialNumber == hmdserial)
            {
                transform.position = gazeRayOrigin_pe;
                //Debug.LogError($"9 - gazerayorigin = {gazeRayOrigin_pe}");
                //Debug.LogError($"9 - trans pos = {transform.position}");
            }

        }
        else if(script.getGazeStatus() == VarjoPlugin.GazeStatus.INVALID)
        {
            // Change gaze status to invalid
            if (gameObject.name == "NetworkObject_1" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8") //LHR-7863A1E8 //LHR-85C3EF8C
            {
                transform.position = new Vector3(status_pe, 0, 0);
                //Debug.LogError($"1.2 - status = {status_pe}");
                //Debug.LogError($"1.2 - trans pos = {transform.position}");
            }
        }
    }
}
