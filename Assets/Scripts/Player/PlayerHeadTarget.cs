using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerHeadTarget : MonoBehaviour
{
    public Transform CTargetAnchor;
    public Transform RTargetAnchor;
    public Transform LTargetAnchor;
    public GameObject Player;
    public Transform SteeringWheel;
    public float LookAtPlayerSpeed;
    public MultiAimConstraint Constraint;
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
            
        }
        else
        {
            transform.position = Vector3.Slerp(transform.position, playerLookAtPed.TargetPed.position, LookAtPlayerSpeed * Time.deltaTime);
        }
    }




}
