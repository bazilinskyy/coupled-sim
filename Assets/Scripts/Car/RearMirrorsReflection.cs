using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public struct Mirror
{
    public Transform mirrorCamera;
    public Transform mirror;
}

[ExecuteInEditMode]
public class RearMirrorsReflection : MonoBehaviour
{
    [SerializeField]
    private Mirror leftMirror;
    [SerializeField]
    private Mirror rightMirror;
    [SerializeField]
    private Mirror middleMirror;

    public Transform head;

    public void Awake()
    {
        if (Application.isPlaying)
        {
            head = Camera.main.transform;
        }
    }

    private void Update()
    {
        GetReflectionDirection(leftMirror);
        GetReflectionDirection(rightMirror);
        GetReflectionDirection(middleMirror);

    }

    private void GetReflectionDirection(Mirror target)
    {
        Vector3 cameraForward = target.mirror.position - head.position;
        Vector3 targetDirection = Vector3.Reflect(cameraForward, target.mirror.forward);
        target.mirrorCamera.rotation = Quaternion.LookRotation(targetDirection, transform.up);
    }

}
