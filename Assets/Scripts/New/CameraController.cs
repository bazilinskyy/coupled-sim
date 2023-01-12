using UnityEngine;
using UnityEngine.XR;
using System.Collections;

// Script obtained from MVN to reset the camera of the Oculus onto the head of the MVN Avatar.
// Used in combination with AnchorController.cs

public class CameraController : MonoBehaviour
{
    Transform
        childCamera;

    // Use this for initialization
    void Start()
    {
        childCamera = transform.parent;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.XR.InputTracking.Recenter();
        }
    }
    // Update is called once per frame
    void LateUpdate()
    {
        Vector3 invertedPosition = -childCamera.localPosition;
        Quaternion invertedRotation = Quaternion.Inverse(childCamera.localRotation);
        transform.localPosition = invertedPosition;
        transform.localRotation = invertedRotation;

    }
}
