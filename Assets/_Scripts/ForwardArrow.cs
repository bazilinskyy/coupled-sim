using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForwardArrow : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 start = transform.position + new Vector3(0, 0.5f, 0);
        Vector3 end = start + transform.forward;
        Gizmos.DrawLine(start, end);
    }
}
