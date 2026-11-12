using System.Collections.Generic;
using CoupledSim.Pedestrians;
using UnityEngine;

/// <summary>
/// Marks a place where AI pedestrians cross the road (zebra or unmarked).
/// Put it on a GameObject with a trigger collider at the near kerb, on the pedestrians' path.
/// An AI pedestrian that enters the trigger stops and decides when to cross with the
/// gap-acceptance model; when it reaches <see cref="FarKerb"/> it walks on as before.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PedestrianCrossing : MonoBehaviour
{
    [Tooltip("Point on the opposite kerb. The crossing runs from this object's position to here.")]
    public Transform FarKerb;
    [Tooltip("Distance (m) from the far kerb at which a pedestrian counts as across.")]
    public float ArrivalRadius = 0.75f;
    [Tooltip("Vehicles farther away than this (m) are not considered.")]
    public float LookDistance = 150f;
    [Tooltip("How far (m) beyond the crossing line ends a vehicle path still counts as conflicting (car half-width + margin).")]
    public float LateralMargin = 1.5f;
    [Tooltip("Zebra crossing: drivers are expected to yield, so pedestrians are a bit less cautious.")]
    public bool Zebra = true;

    public Vector3 NearPoint => transform.position;
    public Vector3 FarPoint => FarKerb != null ? FarKerb.position : transform.position + transform.forward * 7f;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>
    /// Builds the list of vehicles approaching this crossing, from the point of view of a pedestrian
    /// standing at `from` and crossing towards `to`.
    /// </summary>
    public void GetApproachingVehicles(Vector3 from, Vector3 to, List<ApproachingVehicle> result)
    {
        result.Clear();
        var a = new Vector2(from.x, from.z);
        var b = new Vector2(to.x, to.z);
        var ab = b - a;
        float crossingLength = ab.magnitude;
        if (crossingLength < 0.1f) return;
        var abDir = ab / crossingLength;

        foreach (var v in VehicleTracker.Instance.Vehicles)
        {
            if (!v.Active) continue;
            var p = new Vector2(v.Position.x, v.Position.z);
            if ((p - a).sqrMagnitude > LookDistance * LookDistance) continue;
            var f = new Vector2(v.Forward.x, v.Forward.z);
            if (f.sqrMagnitude < 1e-6f) continue;
            f.Normalize();

            // Where does the vehicle's straight-ahead path cross the crossing line?
            float denom = Cross(f, abDir);
            if (Mathf.Abs(denom) < 0.2f) continue; // driving (nearly) along the crossing
            var ap = a - p;
            float tVehicle = Cross(ap, abDir) / denom; // distance along the vehicle's heading
            float sCrossing = Cross(ap, f) / denom;    // distance from the near kerb along the crossing
            if (sCrossing < -LateralMargin || sCrossing > crossingLength + LateralMargin) continue;
            // ignore vehicles that have passed (their rear is beyond the line)
            if (tVehicle < -3f) continue;

            result.Add(new ApproachingVehicle
            {
                Id = v.Avatar.GetInstanceID(),
                DistanceToConflict = tVehicle - 2.2f, // vehicle position is its centre; use the front
                Speed = Mathf.Max(0f, v.Speed),
                Acceleration = v.Acceleration,
                ClearDistance = Mathf.Clamp(sCrossing + LateralMargin, 0f, crossingLength),
                Ehmi = v.Ehmi,
            });
        }
    }

    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    void OnDrawGizmos()
    {
        Gizmos.color = Zebra ? Color.white : new Color(1f, 0.6f, 0f);
        Gizmos.DrawLine(NearPoint + Vector3.up * 0.1f, FarPoint + Vector3.up * 0.1f);
        Gizmos.DrawWireSphere(FarPoint, ArrivalRadius);
    }
}
