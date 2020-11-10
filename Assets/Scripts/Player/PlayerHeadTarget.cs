using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering;
using VehicleBehaviour;

public class PlayerHeadTarget : MonoBehaviour
{
    public GameObject Player;
    public float LookAtPlayerSpeed;
    public Transform CenterAnchor;
    public MultiAimConstraint Constraint;
    public RigBuilder PlayerRigBuilder;

    private PlayerLookAtPed playerLookAtPed;
    private GameObject target;

    private void Start()
    {
        playerLookAtPed = Player.GetComponent<PlayerLookAtPed>();
        target = new GameObject();
        var data = Constraint.data.sourceObjects;
        data.Clear();
        data.Add(new WeightedTransform(target.transform, 1.0f));
        Constraint.data.sourceObjects = data;
        PlayerRigBuilder.Build();
        target.transform.position = CenterAnchor.position;
    }

    private void LateUpdate()
    {
        if (!playerLookAtPed.CanLookAtPed)
        {
            if (target.transform.parent is null)
            {
                target.transform.parent = CenterAnchor;
            }
            target.transform.position = Vector3.Slerp(target.transform.position, CenterAnchor.position, LookAtPlayerSpeed * Time.deltaTime);
        }
        else if(playerLookAtPed.IsTargettingPed)
        {
            if (!(target.transform.parent is null))
            {
                target.transform.parent = null;
            }    
            target.transform.position = Vector3.Slerp(target.transform.position, playerLookAtPed.TargetPed.position, LookAtPlayerSpeed * Time.deltaTime);
        }
    }




}
