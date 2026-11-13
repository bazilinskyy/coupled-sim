using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PedestrianWaypoint : MonoBehaviour
{
    [HideInInspector]
    public AIPedestrian target;
    public float targetSpeed = 1.6f;
    public float targetBlendFactor = 1f;
}
