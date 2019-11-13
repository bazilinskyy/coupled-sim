using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Animator based HMI implementation
public class AnimatorHMI : HMI
{
    [SerializeField]
    MeshRenderer meshRenderer;
    Material material;
    [SerializeField]
    Animator animator;

    //Animator triggers
    [SerializeField]
    string stopTrigger = "stop";
    [SerializeField]
    string walkTrigger = "walk";
    [SerializeField]
    string disabledTrigger = "disabled";

    //texture to be set on certain state changes
    [SerializeField]
    Texture2D stop;
    [SerializeField]
    Texture2D walk;
    [SerializeField]
    Texture2D disabled;

    private void Awake()
    {
        material = meshRenderer.material;
    }

    public override void Display(HMIState state)
    {
        base.Display(state);
        switch (state)
        {
            case HMIState.STOP:
                material.mainTexture = stop;
                animator.SetTrigger(stopTrigger);
                break;
            case HMIState.WALK:
                material.mainTexture = walk;
                animator.SetTrigger(walkTrigger);
                break;
            default:
                material.mainTexture = disabled;
                animator.SetTrigger(disabledTrigger);
                break;
        }
    }
}