using UnityEngine;

/// <summary>
/// The rear section of an articulated vehicle (articulated bus, tractor with trailer). The front
/// section is the physics vehicle; the rear section follows it with the kinematics of a trailer:
/// its axle moves towards the hitch, so it swings out less than the front and cuts corners like the
/// real thing. It needs no network sync (every copy of the vehicle computes it from the front's
/// movement) and is moved as a kinematic body, so other vehicles and people collide with it.
/// </summary>
public class ArticulatedTrailer : MonoBehaviour
{
    [Tooltip("The rear section (child of this vehicle in the prefab; detached at start)")]
    public Transform Trailer;
    [Tooltip("The hitch (articulation joint) in this vehicle's space; the trailer's pivot is there")]
    public Vector3 HitchLocal;
    [Tooltip("Distance from the hitch back to the trailer's axle (m)")]
    public float AxleDistance = 6f;
    [Tooltip("Largest angle between the two sections (degrees)")]
    public float MaxAngle = 55f;
    [Tooltip("The trailer's wheels (pivots at their centres): turned as the trailer rolls")]
    public Transform[] Wheels = new Transform[0];
    public float WheelRadius = 0.48f;

    Vector3 _axle;        // the trailer axle's world position
    bool _started;

    /// <summary>The angle between the sections, degrees (positive: trailer to the right of the front).</summary>
    public float Angle { get; private set; }

    void Start()
    {
        if (Trailer == null) { enabled = false; return; }
        Trailer.SetParent(null, true);
        if (!Trailer.TryGetComponent<Rigidbody>(out var rb)) rb = Trailer.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        Align();
        _started = true;
    }

    void Align()
    {
        var hitch = transform.TransformPoint(HitchLocal);
        _axle = hitch - transform.forward * AxleDistance;
        Place(hitch, transform.forward);
    }

    void Place(Vector3 hitch, Vector3 dir)
    {
        Trailer.SetPositionAndRotation(hitch, Quaternion.LookRotation(dir, transform.up));
    }

    void LateUpdate()
    {
        if (!_started || Trailer == null) return;
        var hitch = transform.TransformPoint(HitchLocal);
        // teleported (respawn, scenario start): straight behind again
        if ((hitch - _axle).sqrMagnitude > (AxleDistance + 5f) * (AxleDistance + 5f)) { Align(); return; }
        var before = _axle;
        var dir = hitch - _axle;
        dir -= transform.up * Vector3.Dot(dir, transform.up);    // in the front section's ground plane
        if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
        dir.Normalize();
        // the joint's limit
        float angle = Vector3.SignedAngle(transform.forward, dir, transform.up);
        if (Mathf.Abs(angle) > MaxAngle)
        {
            dir = Quaternion.AngleAxis(Mathf.Sign(angle) * MaxAngle, transform.up) * transform.forward;
            angle = Mathf.Sign(angle) * MaxAngle;
        }
        Angle = -angle;
        _axle = hitch - dir * AxleDistance;
        Place(hitch, dir);
        // wheels roll with the axle's movement along the trailer
        float rolled = Vector3.Dot(_axle - before, dir);
        float deg = rolled / Mathf.Max(0.05f, WheelRadius) * Mathf.Rad2Deg;
        foreach (var w in Wheels) if (w != null) w.Rotate(deg, 0f, 0f, Space.Self);
    }

    void OnEnable() { if (Trailer != null && _started) { Trailer.gameObject.SetActive(true); Align(); } }
    void OnDisable() { if (Trailer != null && _started) Trailer.gameObject.SetActive(false); }
    void OnDestroy() { if (Trailer != null && _started) Destroy(Trailer.gameObject); }
}
