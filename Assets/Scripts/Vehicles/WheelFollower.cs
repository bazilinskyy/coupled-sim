using UnityEngine;

/// <summary>
/// A wheel without its own wheel collider (the second axle of a tandem, a bus's extra axle): turns
/// and moves up and down with the wheel the physics drives (its Source, usually an empty pivot
/// halfway between the axles of the group).
/// </summary>
public class WheelFollower : MonoBehaviour
{
    public Transform Source;
    Vector3 _offset;      // from the source's rest position, in the parent's space
    Vector3 _sourceRest;

    void Start()
    {
        if (Source == null) { enabled = false; return; }
        _sourceRest = Source.localPosition;
        _offset = transform.localPosition - _sourceRest;
    }

    void LateUpdate()
    {
        transform.localRotation = Source.localRotation;
        transform.localPosition = Source.localPosition + _offset;
    }
}
