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

    public void Awake()
    {
        if (Application.isPlaying)
        {
            head = Camera.main.transform;
        }
    }

    private void OnDrawGizmos()
    {
        DrawLines(leftMirror);
        DrawLines(rightMirror);
        DrawLines(middleMirror);
    }

    void DrawLines(Mirror target)
    {
        Vector3 cameraForward = target.mirror.position - head.position;
        Vector3 targetDirection = Vector3.Reflect(cameraForward, -target.mirror.forward);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(head.position, target.mirror.position);
        Gizmos.DrawLine(target.mirror.position, target.mirror.position + targetDirection / 2);


        Gizmos.color = Color.red;
        Gizmos.DrawLine(target.mirror.position, target.mirror.position + target.mirror.forward / 2);


    }
    private void Update()
    {
        SetMirror(leftMirror);
        SetMirror(rightMirror);
        //SetMirror(middleMirror);

    }

    private void SetMirror(Mirror target)
    {
        Vector3 cameraForward = target.mirror.position - head.position;
        Vector3 targetDirection = Vector3.Reflect(cameraForward, target.mirror.forward);
        target.mirrorCamera.rotation = Quaternion.LookRotation(targetDirection, transform.up);
        target.mirrorCamera.position = target.mirror.position;
    }

}
