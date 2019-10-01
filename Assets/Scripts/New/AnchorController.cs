using UnityEngine;
using System.Collections;

// Script obtained from MVN to reset the camera of the Oculus onto the head of the MVN Avatar.
// Used in combination with CameraController.cs

public class AnchorController : MonoBehaviour
{
    public Transform Camera;
    private Quaternion Anchor_y;
        

    private void LateUpdate()
    {
        Anchor_y = Quaternion.Euler(new Vector3(0f, -transform.rotation.eulerAngles.y, 0f));
        
        transform.localRotation = Anchor_y;               
    }
}
