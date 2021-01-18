using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class moveCam : MonoBehaviour
{
    private KeyCode forward = KeyCode.W;
    private KeyCode backward = KeyCode.S;
    private KeyCode left = KeyCode.A;
    private KeyCode right = KeyCode.D;

    public float speed = 5;
    private float rotationSpeed = 0.2f;

    bool fixedUpdate = true;
    bool lateUpdate = false;

    Quaternion rotateTo;

    private void Update()
    {
        Steer();

        if (rotateTo != transform.rotation) { transform.rotation = Quaternion.Slerp(transform.rotation, rotateTo, Time.time * rotationSpeed); }
    }
    /*// Update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) { 
            fixedUpdate = !fixedUpdate; 
            lateUpdate = !lateUpdate;

            string updateMethod = fixedUpdate ? "Fixed Update" : "Late Update";
            Debug.Log($"Using {updateMethod}...");
        }
        if (lateUpdate) { Move(); }
    }
    void FixedUpdate()
    {
        if (fixedUpdate) { Steer(); }
    }*/

    /*    private void Move()
        {
            if (Input.GetKey(forward)) { transform.position += transform.forward * Time.deltaTime * speed; }
            if (Input.GetKey(backward)) { transform.position -= transform.forward * Time.deltaTime * speed; }
            if (Input.GetKey(right)) { transform.position += transform.right * Time.deltaTime * speed; }
            if (Input.GetKey(left)) { transform.position -= transform.right * Time.deltaTime * speed; }
        }*/

    private void Steer()
    {

        if(Input.GetAxis("Steer") > 0.01 || Input.GetAxis("Steer") < -0.01)
        {
            Debug.Log(Input.GetAxis("Steer"));
            Vector3 towards = transform.forward + transform.right * Input.GetAxis("Steer");
            rotateTo = Quaternion.LookRotation(towards, Vector3.up);
        }
        
    }
}

