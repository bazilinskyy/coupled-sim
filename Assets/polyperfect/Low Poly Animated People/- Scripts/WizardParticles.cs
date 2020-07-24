using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class WizardParticles : MonoBehaviour {

    Animator animator;

    public Transform leftHand, rightHand;

    public Vector3 direction, midPoint;
    public Quaternion rotation;
    public float scale;

    public ParticleSystem particleSystem;

    public Vector3 sizeMultiplier = Vector3.right;

	// Use this for initialization
	void Start ()
    {
        animator = GetComponentInParent<Animator>();
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        particleSystem = GetComponent<ParticleSystem>();
    }

    // Update is called once per frame
    void Update ()
    {
        direction = leftHand.position - rightHand.position;

        midPoint = (rightHand.position + leftHand.position) / 2;
        scale = direction.magnitude/2;

        midPoint = midPoint - transform.position;

        //rotation = Quaternion.LookRotation(direction, midPoint);
        // the second argument, upwards, defaults to Vector3.up

        //var shape = particleSystem.shape;
        //shape.position = midPoint;
        //shape.rotation = rotation.eulerAngles;
        //shape.scale = new Vector3(scale * sizeMultiplier.x, scale * sizeMultiplier.y, scale * sizeMultiplier.z);

    }

    void OnDrawGizmos()
    {

    }
}
