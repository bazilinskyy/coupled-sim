using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

//updates speedometer 
//it can be configured with as analog (with arrow) and digital "display"
public class Speedometer : MonoBehaviour
{
    [SerializeField]
    private Rigidbody carBody;

    [Header("Digital")]
    private float lastUpdate;
    [SerializeField]
    private Text speedometerText;

    [Header("Analog")]
    [SerializeField]
    private Transform pivot; //arrow pivot
    [SerializeField]
    private float pivotMinSpeedAngle = 120f; //arrow inclination when speed = 0
    [SerializeField]
    private float pivotMaxSpeedAngle = -120f; //arrow inclination when speed = maxSpeed
    [SerializeField]
    private float maxSpeed = 160f;

    private void Update()
    {
        float v = carBody.linearVelocity.magnitude * SpeedConvertion.Mps2Kmph;

        if (lastUpdate + 0.5f < Time.time)
        {
            lastUpdate = Time.time;
            speedometerText.text = Mathf.RoundToInt(v).ToString();
        }
        var angle = Mathf.Lerp(pivotMinSpeedAngle, pivotMaxSpeedAngle, v/maxSpeed);
        if (pivot != null) {
            pivot.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}
