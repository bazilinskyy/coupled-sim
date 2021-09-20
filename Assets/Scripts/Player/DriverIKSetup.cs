using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DriverIKSetup : MonoBehaviour
{
    public Transform LeftFoot;
    public Transform RightFoot;
    public Transform LeftHand;
    public Transform LeftElbowHint;
    public Transform RightHand;
    public Transform RightElbowHint;
    Animator _anim;

    void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        _anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);
        _anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);
        _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
        _anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
        _anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1);
        _anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1);
        _anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
        _anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
        _anim.SetIKPosition(AvatarIKGoal.LeftFoot, LeftFoot.position);
        _anim.SetIKPosition(AvatarIKGoal.RightFoot, RightFoot.position);
        _anim.SetIKPosition(AvatarIKGoal.LeftHand, LeftHand.position);
        _anim.SetIKPosition(AvatarIKGoal.RightHand, RightHand.position);
        _anim.SetIKRotation(AvatarIKGoal.LeftFoot, LeftFoot.rotation);
        _anim.SetIKRotation(AvatarIKGoal.RightFoot, RightFoot.rotation);
        _anim.SetIKRotation(AvatarIKGoal.LeftHand, LeftHand.rotation);
        _anim.SetIKRotation(AvatarIKGoal.RightHand, RightHand.rotation);
        _anim.SetIKHintPosition(AvatarIKHint.LeftElbow, LeftElbowHint.position);
        _anim.SetIKHintPosition(AvatarIKHint.RightElbow, RightElbowHint.position);
        _anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0.5f);
        _anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0.5f);
    }
}
