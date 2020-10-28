using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using VarjoExample;
using Valve.VR;

public class Network_remote_varjo : MonoBehaviour
{
    public GameObject SyncGazePedestrian;

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
            if (gameObject.name == "NetworkObject_1" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8") //LHR-7863A1E8 //LHR-85C3EF8C
            {
                transform.position = new Vector3(status_pe, 0, 0);
                transform.localScale = new Vector3(distance_pe, Frame_pe, CaptureTime_pe);
                Debug.LogError($"1 - status = {status_pe} and dis,fra,cap = {distance_pe}, {Frame_pe}, {CaptureTime_pe}");
                Debug.LogError($"1 - trans pos = {transform.position} and transform localscale = {transform.localScale}");
            }
            if (gameObject.name == "NetworkObject_2" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8") //LHR-7863A1E8 //LHR-85C3EF8C 
            {
                transform.position = HmdPos_pe;
                transform.localScale = HmdRot_pe;
                Debug.LogError($"2 - hmdpos = {HmdPos_pe} and hmdrot = {HmdRot_pe}");
                Debug.LogError($"2 - trans pos = {transform.position} and trans scale = {transform.localScale}");
            }
            if (gameObject.name == "NetworkObject_3" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8") //LHR-7863A1E8 //LHR-85C3EF8C 
            {
                transform.position = new Vector3((float)LeftEyePupilSize_pe, (float)RightEyePupilSize_pe, 0);
                transform.localScale = new Vector3((float)FocusDistance_pe, (float)FocusStability_pe, 0);
                Debug.LogError($"3 - eyesize = {(float)LeftEyePupilSize_pe}, {(float)RightEyePupilSize_pe} and foc dist = {(float)FocusDistance_pe} and foc stab = {(float)FocusStability_pe}");
                Debug.LogError($"3 - trans pos = {transform.position} and trans scale = {transform.localScale}");
            }
            if (gameObject.name == "NetworkObject_4" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8") //LHR-7863A1E8 //LHR-85C3EF8C 
            {
                transform.position = gazeRayForward_pe;
                transform.localScale = gazeRayDirection_pe;
                Debug.LogError($"4 - gazerayfor = {gazeRayForward_pe} and gazeraydir = {gazeRayDirection_pe}");
                Debug.LogError($"4 - trans pos = {transform.position} and trans scale = {transform.localScale}");
            }
            if (gameObject.name == "NetworkObject_5" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8") //LHR-7863A1E8 //LHR-85C3EF8C 
            {
                transform.position = gazePosition_pe;
                transform.localScale = gazeRayOrigin_pe;
                Debug.LogError($"5 - gazepos = {gazePosition_pe} and gazeori = {gazeRayOrigin_pe}");
                Debug.LogError($"5 - trans pos = {transform.position} and trans scale = {transform.localScale}");
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
