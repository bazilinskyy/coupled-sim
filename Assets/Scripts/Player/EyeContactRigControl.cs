using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using VehicleBehaviour;

public class EyeContactRigControl : MonoBehaviour
{
    [FormerlySerializedAs("LookAtPlayerSpeed")]
    public float HeadRotationLerpingSpeed;
    [FormerlySerializedAs("CenterAnchor")]
    public Transform NeutralPositionHeadTrackingTarget;
    [FormerlySerializedAs("Constraint")]
    public MultiAimConstraint HeadTrackingAnimationConstraint;
    [FormerlySerializedAs("PlayerRigBuilder")]
    public RigBuilder DriverAnimationRig;

    private EyeContact playerLookAtPed;
    private GameObject target;

    private void Start()
    {
        playerLookAtPed = GetComponent<EyeContact>();
        target = new GameObject();
        var data = HeadTrackingAnimationConstraint.data.sourceObjects;
        data.Clear();
        data.Add(new WeightedTransform(target.transform, 1.0f));
        HeadTrackingAnimationConstraint.data.sourceObjects = data;
        DriverAnimationRig.Build();
        target.transform.position = NeutralPositionHeadTrackingTarget.position;
        target.name = "Head Tracking Target";
    }

    private void LateUpdate()
    {
        if(playerLookAtPed.TargetPed != null)
        {
            target.transform.parent = null;
            target.transform.position = Vector3.Lerp(target.transform.position, playerLookAtPed.TargetPed.position, HeadRotationLerpingSpeed * Time.deltaTime);
        } 
        else
        {
            target.transform.parent = NeutralPositionHeadTrackingTarget;
            target.transform.position = Vector3.Lerp(target.transform.position, NeutralPositionHeadTrackingTarget.position, HeadRotationLerpingSpeed * Time.deltaTime);
        }
    }




}
