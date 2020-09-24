using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;
using VarjoExample;

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

            // Apply the right poses according to the gameobject name
            if (gameObject.name == "GazeOrigin")
            {
                transform.position = _gazeRayOrigin;
                //Debug.LogError($"Gaze Origin found {_gazeRayOrigin}, transform = {transform.position}");
            }
            if (gameObject.name == "GazeDirection")
            {
                transform.position = _gazeRayOrigin + _gazeRayDirection * 12.0f;
                //Debug.LogError($"Gaze direction found {_gazeRayDirection}, transform = {transform.position}");
            }
        }
    }
}
