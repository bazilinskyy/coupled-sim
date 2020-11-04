using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class PlayerLookAtPed : MonoBehaviour
{
    public Transform PlayerHead;
    public Transform TargetPed;
    public Transform PlayerLookAtTarget;
    public GameObject[] Peds;
    public float MaxTrackingDistance;
    public float MaxHeadRotation;

    public bool isTargettingPed;
    public bool canLookAtPed;
    public float targetAngle;

    private void Start()
    {
        Peds = GameObject.FindGameObjectsWithTag("Pedestrian");
    }

    private void Update()
    {
        LookForPeds();

        if (isTargettingPed)
        {
            Ray targetRay = new Ray(PlayerHead.position, (TargetPed.position - PlayerHead.position) + (new Vector3(0.0f,PlayerHead.position.y,0.0f) - new Vector3(0.0f,TargetPed.position.y,0.0f)));
            targetAngle = Vector3.Angle(transform.forward, targetRay.direction);
            if (targetAngle < MaxHeadRotation)
            {
                canLookAtPed = true;
            }
            else
            {
                canLookAtPed = false;
            }
        }
    }

    private void LookForPeds()
    {
        if (!isTargettingPed)
        {
            foreach (GameObject ped in Peds)
            {
                if (Vector3.Distance(transform.position, ped.transform.position) < MaxTrackingDistance)
                {
                    TargetPed = ped.transform;
                    isTargettingPed = true;
                }
            }
        }
        else
        if (Vector3.Distance(transform.position, TargetPed.position) > MaxTrackingDistance)
        {
            TargetPed = null;
            isTargettingPed = false;
        }
    }

}
