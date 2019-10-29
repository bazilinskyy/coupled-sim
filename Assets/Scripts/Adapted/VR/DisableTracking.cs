using UnityEngine;
using System.Collections;

public class DisableTracking : MonoBehaviour
{
    public Camera cam;
    private Vector3 startPos;

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        startPos = transform.localPosition;
    }

    void Update()
    {
        transform.localPosition = startPos - cam.transform.localPosition;
        transform.localRotation = Quaternion.Inverse(cam.transform.localRotation);
    }
}