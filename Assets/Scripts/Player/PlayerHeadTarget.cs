using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHeadTarget : MonoBehaviour
{
    public Transform CTargetAnchor;
    public Transform RTargetAnchor;
    public Transform LTargetAnchor;
    public GameObject Player;
    public Transform SteeringWheel;
    public float LookAtPlayerSpeed;

    public float steeringRotation;
    public float steeringPercentage;


    private PlayerLookAtPed playerLookAtPed;

    private void Start()
    {
        playerLookAtPed = Player.GetComponent<PlayerLookAtPed>();
    }

    private void Update()
    {
        steeringRotation = SteeringWheel.localRotation.z;
        steeringPercentage = steeringRotation / 60.0f;
        
        if (!playerLookAtPed.canLookAtPed)
        {
            if (steeringPercentage > 0.0f)
            {
                transform.position = Vector3.Lerp(CTargetAnchor.position, LTargetAnchor.position, steeringPercentage);
            }
            else
            if (steeringPercentage < 0.0f)
            {
                transform.position = Vector3.Lerp(CTargetAnchor.position, RTargetAnchor.position, steeringPercentage);
            }
            else
            if (steeringPercentage == 0.0f)
            {
                transform.position = Vector3.Slerp(transform.position, CTargetAnchor.position, LookAtPlayerSpeed * Time.deltaTime);
            }
        }
        else
        {
            transform.position = Vector3.Slerp(transform.position, playerLookAtPed.TargetPed.position, LookAtPlayerSpeed * Time.deltaTime);
        }
    }




}
