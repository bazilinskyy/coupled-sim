using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//debug keyboard controller for pedestrian role
public class KeyboardPlayerMovement : MonoBehaviour
{
    Rigidbody _rb;
    [SerializeField]
    float _speed;
    [SerializeField]
    float _rotateSpeed = 50;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Vector3 move = default(Vector3);
        if (Input.GetKey(KeyCode.W))
        {
            move.z += _speed;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            move.z -= _speed;
        }
        if (Input.GetKey(KeyCode.A))
        {
            move.x -= _speed;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            move.x += _speed;
        }
        float rot = 0;
        if (Input.GetKey(KeyCode.Q))
        {
            rot -= _rotateSpeed;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            rot += _rotateSpeed;
        }
        _rb.MovePosition(_rb.position + _rb.rotation * move * Time.fixedDeltaTime);
        _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0, rot * Time.fixedDeltaTime, 0));

        // Extra keyboard rotation for pitch and roll rotations
        float rot_pitch = 0;
        float rot_roll = 0;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            rot_pitch -= _rotateSpeed;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            rot_pitch += _rotateSpeed;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            rot_roll += _rotateSpeed;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            rot_roll -= _rotateSpeed;
        }
        _rb.MoveRotation(_rb.rotation * Quaternion.Euler(rot_pitch * Time.fixedDeltaTime, 0, 0));
        _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0, 0, rot_roll * Time.fixedDeltaTime));
    }
}
