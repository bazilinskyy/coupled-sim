using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Debug keyboard controller for pedestrian rotations
public class DebugRotation : MonoBehaviour
{
    [SerializeField]
    float _rotateSpeed = 1;
    float rot_yaw = 0;
    float rot_pitch = 0;
    float rot_roll = 0;

    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.Q))
        {
            rot_yaw -= _rotateSpeed;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            rot_yaw += _rotateSpeed;
        }
        transform.rotation = Quaternion.Euler(new Vector3(rot_pitch, rot_yaw * Time.fixedDeltaTime, rot_roll));

        if (Input.GetKey(KeyCode.UpArrow))
        {
            rot_pitch -= _rotateSpeed;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            rot_pitch += _rotateSpeed;
        }
        transform.rotation = Quaternion.Euler(new Vector3(rot_pitch * Time.fixedDeltaTime, rot_yaw, rot_roll));

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            rot_roll += _rotateSpeed;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            rot_roll -= _rotateSpeed;
        }
        transform.rotation = Quaternion.Euler(new Vector3(rot_pitch, rot_yaw, rot_roll * Time.fixedDeltaTime));
    }
}
