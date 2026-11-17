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
        if (head == null) return;
        GetReflectionDirection(leftMirror);
        GetReflectionDirection(rightMirror);
        GetReflectionDirection(middleMirror);

    }

    /// <summary>
    /// Turns a car's mirrors on (their cameras render, aimed from the given eye) or off. Only the cars of the
    /// participants here have them on; a replay turns them on for the car it is seen from, as otherwise its mirrors
    /// showed whatever their render textures (shared by every car of that model) held last: a flat orange ellipse.
    /// </summary>
    public static void SetLive(GameObject car, Transform eye, bool on)
    {
        if (car == null) return;
        var m = car.GetComponentInChildren<RearMirrorsReflection>(true);
        if (m == null) return;
        if (eye != null) m.head = eye;
        foreach (var c in m.GetComponentsInChildren<Camera>(true)) c.enabled = on;
        m.enabled = on;
        m.gameObject.SetActive(on);
    }

    private void GetReflectionDirection(Mirror target)
    {
        if (target.mirror == null || target.mirrorCamera == null) return;
        Vector3 cameraForward = target.mirror.position - head.position;
        Vector3 targetDirection = Vector3.Reflect(cameraForward, target.mirror.forward);
        target.mirrorCamera.rotation = Quaternion.LookRotation(targetDirection, transform.up);
    }

}
