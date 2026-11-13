using UnityEngine;
using System.Collections;
[ExecuteInEditMode]
public class PositionSetter : MonoBehaviour {
    public Transform target;

    void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position; //  + new Vector3(-0.03f,+0.16f,-0.7f);
        }
    }
}
