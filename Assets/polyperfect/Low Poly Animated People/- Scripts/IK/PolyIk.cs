using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PolyIk : MonoBehaviour
{
    public List<HumanBoneOffset> rotationOffsets = new List<HumanBoneOffset>();

    //Ik Targets
    public Transform leftFootTarget, rightFootTarget, LeftHandTarget, rightHandTarget, lookAtTarget;
    public Transform leftFootPole, rightFootPole, LeftHandPole, rightHandPole;

    //Weights
    [Range(0, 1)]
    public float rotationWeightLeftFoot, rotationWeightRightFoot, rotationWeightLeftHand, rotationWeightRightHand;

    [Range(0, 1)]
    public float lookAtWeight, lookAtHeadWeight, LookAtEyesWeight, rightHandWeight, leftHandWeight, rightFootWeight, leftFootWeight;

    public bool leftFootIk, lookAtIk, rightHandIk, leftHandIk, rightFootIk;

    public Animator animator;
    // Use this for initialization
    void Start()
    {
        animator = GetComponent<Animator>();
        if(!animator.isHuman)
        {
            this.enabled = false;

            Debug.Log("The rig needs to be humanoid for this script to work");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Application.isEditor)
        {
            animator.Update(0);
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        SetIkTargetsAndWeights();

        foreach (var item in rotationOffsets)
        {
            if (item.active)
            {
                OffsetSpine(item.bone, item.rotationOffset);
            }
        }
    }

    void SetIkTargetsAndWeights()
    {
        if (leftFootTarget != null && leftFootIk)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftFootTarget.rotation);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, rotationWeightLeftFoot);

            animator.SetIKHintPosition(AvatarIKHint.LeftKnee, leftFootPole.position);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, leftFootWeight);
        }

        if (rightFootTarget != null && rightFootIk)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, rightFootWeight);
            animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightFoot, rightFootTarget.rotation);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, rotationWeightRightFoot);

            animator.SetIKHintPosition(AvatarIKHint.RightKnee, rightFootPole.position);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, rightFootWeight);
        }

        if (LeftHandTarget != null && leftHandIk)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, LeftHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, LeftHandTarget.rotation);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, rotationWeightLeftHand);

            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, LeftHandPole.position);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, leftHandWeight);
        }

        if (rightHandTarget != null && rightHandIk)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandTarget.rotation);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rotationWeightRightHand);

            animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightHandPole.position);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, rightHandWeight);
        }

        if (lookAtTarget != null && lookAtIk)
        {
            animator.SetLookAtWeight(lookAtWeight, 0, lookAtHeadWeight, LookAtEyesWeight, 0);
            animator.SetLookAtPosition(lookAtTarget.position);
        }
    }

    public void OffsetSpine(HumanBodyBones bone, Vector3 target)
    {
        Quaternion startRotation = animator.GetBoneTransform(bone).localRotation;
        Quaternion targetRotation = Quaternion.Euler(target);
        animator.SetBoneLocalRotation(bone, Quaternion.Inverse(startRotation) * targetRotation);
    }
}
