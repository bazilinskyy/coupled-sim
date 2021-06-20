using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteeringWheel : MonoBehaviour
{
    public Transform steeringWheel;
    public Transform pivot;

    public void LateUpdate()
    {
        steeringWheel.transform.parent = pivot;
    }
}
