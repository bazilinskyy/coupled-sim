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
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(forward)) { transform.position += transform.forward*Time.deltaTime*speed;}
        if (Input.GetKey(backward)) { transform.position -= transform.forward * Time.deltaTime * speed; }
        if (Input.GetKey(right)) { transform.position += transform.right * Time.deltaTime * speed; }
        if (Input.GetKey(left)) { transform.position -= transform.right* Time.deltaTime * speed; }
    }
}
