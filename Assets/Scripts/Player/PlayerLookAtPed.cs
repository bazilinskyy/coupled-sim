using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLookAtPed : MonoBehaviour
{
    private Transform targetPed;

    public GameObject[] Peds;

    public Rigidbody CarRigidbody;
    public Transform PlayerHead;
    public float MaxTrackingDistance;
    public float MinTrackingDistance;
    public float MaxHeadRotation;
    public bool EnableTracking;
    [HideInInspector]
    public bool trackingEnabledWhenYielding;

    public Transform TargetPed { get => targetPed; }

    private float targetAngle;


    private void Start()
    {
        Peds = GameObject.FindGameObjectsWithTag("Pedestrian");
    }

    private void Update()
    {
        bool yielding = CarRigidbody.velocity.magnitude < 0.1f && CarRigidbody.velocity.magnitude > -0.1f;
        bool trackingEnabled = (yielding ? trackingEnabledWhenYielding : EnableTracking);

        float minDist = float.MaxValue;
        if (trackingEnabled)
        {
            foreach (GameObject ped in Peds)
            {
                float distance = FlatDistance(CarRigidbody.transform.position, ped.transform.position);
                if (minDist > distance)
                {
                    minDist = distance;
                    targetPed = ped.transform;
                }
                Debug.Log(distance);
            }
        } else
        {
            targetPed = null;
        }

        if ((minDist > MaxTrackingDistance || minDist < MinTrackingDistance) && !yielding)
        {
            targetPed = null;
        }

        if (targetPed != null)
        {
            Vector3 sourcePos = CarRigidbody.transform.position;
            Vector3 targetPos = targetPed.transform.position;
            Vector3 direction = (targetPos - sourcePos);
            direction = new Vector3(direction.x, 0.0f, direction.z);

            Ray targetRay = new Ray(sourcePos, direction);
            targetAngle = Vector3.Angle(transform.forward, targetRay.direction);
            if (targetAngle > MaxHeadRotation)
            {
                targetPed = null;
            }
        }
    }

    private float FlatDistance(Vector3 pos1, Vector3 pos2)
    {
        var diffVector = pos1 - pos2;
        diffVector.y = 0;
        diffVector.z = 0;
        var distance = diffVector.magnitude;
        return distance;
    }
}
