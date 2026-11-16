using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EyeContact : MonoBehaviour
{
    private Transform targetPed;

    public GameObject[] Peds;

    public Rigidbody CarRigidbody;
    public AICar aiCar;
    public Transform PlayerHead;
    public float MaxTrackingDistance;//this distance is wrt to car
    public float MinTrackingDistance;//this distance is wrt to car
    float headPosition => CarRigidbody.transform.InverseTransformPoint(PlayerHead.position).z;
    float maxTrackingDistanceHead => MaxTrackingDistance - headPosition;//this distance is wrt to drivers head
    float minTrackingDistanceHead => MinTrackingDistance - headPosition;//this distance is wrt to drivers head
    public float MaxHeadRotation;//this is wrt drivers head
    [FormerlySerializedAs("EnableTracking")]
    public bool Tracking;
    [HideInInspector]
    public bool TrackingWhileYielding;

    public Transform TargetPed { get => targetPed; }

    private float targetAngle;


    private void Start()
    {
        Peds = GameObject.FindGameObjectsWithTag("Pedestrian");
        if (CarRigidbody != null) aiCar = CarRigidbody.GetComponent<AICar>();
    }

    private void FixedUpdate()
    {
        if (aiCar == null || CarRigidbody == null || PlayerHead == null) return;
        bool yielding = aiCar.state == AICar.CarState.STOPPED;
        bool eyeContactTracking = (yielding ? TrackingWhileYielding : Tracking);

        float minDist = float.MaxValue;
        targetPed = null;
        if (eyeContactTracking && Peds != null)
        {
            foreach (GameObject ped in Peds)
            {
                if (ped == null || !ped.activeInHierarchy) continue;
                float distance = FlatDistance(PlayerHead.position, ped.transform.position);
                if (minDist > distance)
                {
                    minDist = distance;
                    targetPed = ped.transform;
                }
            }
        }

        if ((minDist > maxTrackingDistanceHead || minDist < minTrackingDistanceHead) && !yielding)
        {
            targetPed = null;
        }

        if (targetPed != null)
        {
            Vector3 sourcePos = PlayerHead.position;
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
        var distance = diffVector.magnitude;
        return distance;
    }
}
