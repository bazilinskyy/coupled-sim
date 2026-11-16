using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves a walking or riding participant along a path by itself, through the same inputs a person would give (for
/// demonstrations and videos; -autopilot): a pedestrian walks along the pavement (turning towards a point a few
/// metres ahead), a rider follows its riding line (MapTraffic.RideLine: a cycle path beside the road, else near the
/// kerb; steering towards a point ahead, pedalling or riding
/// at a steady effort, braking for the bends and for vehicles, people and animals ahead). At the end of the path it turns
/// round (pedestrians) or stops.
/// </summary>
public class ParticipantAutopilot : MonoBehaviour
{
    public List<Vector3> Path;
    public float LookAhead = 5f;
    [Tooltip("Rider's effort (0..1).")]
    public float Effort = 0.55f;
    KeyboardPlayerMovement _walker;
    TwoWheelerController _rider;
    Rigidbody _body;
    int _index;
    bool _back;

    public void Init(List<Vector3> path)
    {
        Path = path;
        _walker = GetComponent<KeyboardPlayerMovement>();
        _rider = GetComponent<TwoWheelerController>();
        _body = GetComponent<Rigidbody>();
        // start at the nearest point of the path
        float best = float.MaxValue;
        for (int i = 0; i < Path.Count; i++) { float d = (Path[i] - transform.position).sqrMagnitude; if (d < best) { best = d; _index = i; } }
        if (_walker != null) _walker.UseExternalInput = true;
        if (_rider != null) _rider.UseExternalInput = true;
        if (_rider != null) LookAhead = 7f;
    }

    void FixedUpdate()
    {
        if (Path == null || Path.Count < 2) return;
        var p = transform.position;
        // the point to head for: the first point of the path at least LookAhead ahead
        int step = _back ? -1 : 1;
        while (_index + step >= 0 && _index + step < Path.Count && Flat(Path[_index] - p).magnitude < LookAhead) _index += step;
        bool end = _index + step < 0 || _index + step >= Path.Count;
        if (end && Flat(Path[_index] - p).magnitude < 1.5f)
        {
            if (_walker != null) { _back = !_back; return; }   // a pedestrian turns round
            if (_rider != null) { _rider.Input = new CoupledSim.TwoWheelers.TwoWheelerInput { Brake = 1f }; return; }
        }
        var to = Flat(Path[_index] - p);
        float err = Vector3.SignedAngle(Flat(transform.forward), to, Vector3.up);
        if (_walker != null)
        {
            _walker.ExternalTurnRate = Mathf.Clamp(err * 2.5f, -100f, 100f);
            _walker.ExternalMove = new Vector2(0f, Mathf.Abs(err) > 70f ? 0.2f : 1f);
        }
        if (_rider != null)
        {
            // bends ahead: slow down for sharp ones
            float bend = 0f;
            for (int k = 1; k < 4; k++)
            {
                int a = Mathf.Clamp(_index + step * k, 0, Path.Count - 1), b = Mathf.Clamp(a + step, 0, Path.Count - 1);
                bend = Mathf.Max(bend, Vector3.Angle(Flat(Path[a] - p), Flat(Path[b] - Path[a])));
            }
            float effort = bend > 50f ? 0.2f : Effort, brake = bend > 70f ? 0.3f : 0f;
            // something ahead (a vehicle, a person, an animal, a parked car): ease off, brake, wait 1.5 m behind it
            float gap = ObstacleAhead();
            if (gap < float.MaxValue)
            {
                float speed = _body != null ? _body.linearVelocity.magnitude : 0f;
                float stopping = speed * speed / (2f * 3f) + 1.5f;   // braking at 3 m/s², stopping 1.5 m short
                if (gap < stopping + 1f) { effort = 0f; brake = Mathf.Clamp01((stopping + 1f - gap) / 2f + 0.3f); }
                else effort *= Mathf.Clamp01((gap - stopping) / 6f);
            }
            _rider.Input = new CoupledSim.TwoWheelers.TwoWheelerInput { Drive = effort, Steer = Mathf.Clamp(err / 25f, -1f, 1f), Brake = brake };
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    readonly Collider[] _near = new Collider[24];
    float _nextWaitLog;

    /// <summary>
    /// The distance along the path to the nearest vehicle, person, animal or parked car within 0.55 m of it (every metre,
    /// up to 12 m ahead, at a rider's height), float.MaxValue when the way is clear. Following the path, not a straight line
    /// ahead, so the parked cars on the inside of a bend do not count; kerbs, walls and posts beside it are not counted.
    /// </summary>
    float ObstacleAhead()
    {
        int step = _back ? -1 : 1;
        var prev = transform.position; float along = 0f;
        int k = _index;
        // from the rider's front along the path: the rest of the way to the next point, then point by point
        var points = new List<Vector3>();
        for (int i = 0; i < 6 && k >= 0 && k < Path.Count; i++, k += step) points.Add(Path[k]);
        foreach (var q in points)
        {
            var seg = Flat(q - prev); float len = seg.magnitude;
            for (float s = 1f; s <= len; s += 1f)
            {
                float d = along + s;
                if (d < 1.2f) continue;   // (the rider's own length)
                if (d > 12f) return float.MaxValue;
                var c = prev + seg * (s / len) + Vector3.up * 0.9f;
                int n = Physics.OverlapSphereNonAlloc(c, 0.55f, _near, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var col = _near[i];
                    if (col == null || col.transform.IsChildOf(transform)) continue;
                    bool mover = col.attachedRigidbody != null || col.GetComponentInParent<PlayerAvatar>() != null || col.GetComponentInParent<AnimalAgent>() != null;
                    bool parked = false;
                    for (var x = col.transform; x != null && !parked; x = x.parent) parked = x.name.StartsWith("Parked");
                    if (mover || parked)
                    {
                        if (Time.time > _nextWaitLog && _body != null && _body.linearVelocity.magnitude < 0.3f)
                        { _nextWaitLog = Time.time + 10f; Debug.Log($"ParticipantAutopilot: waiting for {col.name} ({col.transform.root.name}) {d:F1} m ahead: {(col.GetComponentInParent<TrafficCar>() is TrafficCar w ? w.LeaderDescription + ", " + w.Speed.ToString("F1") + " m/s" : "")}"); }
                        return Mathf.Max(0f, d - 1.2f);
                    }
                }
            }
            along += len; prev = new Vector3(q.x, prev.y, q.z);
        }
        return float.MaxValue;
    }
}
