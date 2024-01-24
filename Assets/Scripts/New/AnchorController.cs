using UnityEngine;
using System.Collections;

// Script obtained from MVN to reset the camera of the Oculus onto the head of the MVN Avatar.
// Used in combination with CameraController.cs

public class AnchorController : MonoBehaviour
{
    public Transform Camera;
    private Quaternion Anchor_y;
        

    private void Start()
    {
        Debug.Log("This used to rotate the Y coordinates, possibly for MVN suit. However it appeared broken, so now turned off");
    }



    private void LateUpdate()
    {
        // todo: check if in suit mode
        // Anchor_y = Quaternion.Euler(new Vector3(0f, -transform.rotation.eulerAngles.y, 0f));
        
        // transform.localRotation = Anchor_y;               
    }
}
