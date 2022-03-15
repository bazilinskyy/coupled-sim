using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script allows rotation of the participant player head along the pitch-axis / X-axis and roll-axis / Z-axis. The yaw rotation is set to zero.
public class TrackRot_PitchRoll : MonoBehaviour
{
    private Quaternion Anchor_y;

    void Update()
    {
        Anchor_y = Quaternion.Euler(new Vector3(transform.rotation.eulerAngles.x, 0f, transform.rotation.eulerAngles.z));
        transform.rotation = Anchor_y;
    }
}
