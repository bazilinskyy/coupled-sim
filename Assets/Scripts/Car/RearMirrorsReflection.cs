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

//updates cameras rendering mirror view to match position of players head
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
