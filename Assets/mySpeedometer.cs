using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mySpeedometer : MonoBehaviour
{
    [SerializeField]
    private Rigidbody carBody;

    [Header("Analog")]
    [SerializeField]
    private bool isRPMNeedle = false;
    [SerializeField]
    private float pivotMinSpeedAngle = 0f; //arrow inclination when speed = 0
    [SerializeField]
    private float pivotMaxSpeedAngle = -240f; //arrow inclination when speed = maxSpeed
    [SerializeField]
    private float maxSpeed = 160f;
    [SerializeField]
    private float needleSmoothing = 1f;
    private void Update()
    {
        float v = carBody.velocity.magnitude * SpeedConvertion.Mps2Kmph;
        //Vector3 Eulers = transform.localEulerAngles;

        var angle = Mathf.Lerp(pivotMinSpeedAngle, pivotMaxSpeedAngle, v / maxSpeed);

        //Vector3 temp = new Vector3(Eulers.x, Eulers.y, angle);
        transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
}