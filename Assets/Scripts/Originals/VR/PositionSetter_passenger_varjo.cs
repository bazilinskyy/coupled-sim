using UnityEngine;
using System.Collections;
[ExecuteInEditMode]
public class PositionSetter_passenger_varjo : MonoBehaviour {
    public Transform target;

	void LateUpdate () {
        transform.position = target.position + new Vector3(0.0f, -0.20f, 0.0f); ; //  + new Vector3(-0.03f,+0.16f,-0.7f);
	}
}
