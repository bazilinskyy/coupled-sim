using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using VarjoExample;
using Valve.VR;

public class PositionSetterGaze : MonoBehaviour
{
    public GameObject SyncGaze;
    Vector3 _gazeRayOrigin;
    Vector3 _gazeRayDirection;

    void FixedUpdate()
    {
        // Check whether eye-tracking is established first
        if (SyncGaze.GetComponent<VarjoGazeRay_CS>().getGazeStatus() != VarjoPlugin.GazeStatus.INVALID)
        {
            // Get gazeRayOrigin and Direction
            _gazeRayOrigin = SyncGaze.GetComponent<VarjoGazeRay_CS>().gazeRayOrigin;
            _gazeRayDirection = SyncGaze.GetComponent<VarjoGazeRay_CS>().gazeRayDirection;

            // Test: Hardcoding pedestrian prevention
            Vector3 pos_pe = new Vector3(-116.0f, 2.2f, 17.2f);//(-116.32f, 0.2224625f, 17.19f);
            if (_gazeRayOrigin != pos_pe)
            {
                // Apply the right poses according to the gameobject name
                if (gameObject.name == "GazeOrigin" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8")
                {
                    transform.position = _gazeRayOrigin;
                    //Debug.LogError($"Gaze Origin found {_gazeRayOrigin}, transform from {transform.name} = {transform.position}");
                }
                if (gameObject.name == "GazeDirection" && SteamVR.instance.hmd_SerialNumber == "LHR-7863A1E8")
                {
                    transform.position = _gazeRayOrigin + _gazeRayDirection * 50.0f;
                    //Debug.LogError($"Gaze direction found {_gazeRayDirection}, transform from {transform.name} = {transform.position}");
                }
            }


        }
    }
}
