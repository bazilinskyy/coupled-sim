using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script allows rotation of the participant player body along the yaw-axis / Y-axis. The pitch and roll rotations are set to zero.
public class TrackRot_Yaw : MonoBehaviour
{
    private Quaternion Anchor_xz;

    void LateUpdate()
    {
        Anchor_xz = Quaternion.Euler(new Vector3(0f, transform.rotation.eulerAngles.y, 0f));
        transform.rotation = Anchor_xz;
    }
}
