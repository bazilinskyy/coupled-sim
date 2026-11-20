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
            float obstacle = ObstacleAhead(), red = RedLightAhead();
            float gap = Mathf.Min(obstacle, red);
            if (DebugTraffic && Time.time >= _nextDebug)
            {
                _nextDebug = Time.time + 1f;
                Debug.Log($"TrafficDebug: rider t={Time.time:F1} at ({p.x:F1},{p.z:F1}) v={(_body != null ? _body.linearVelocity.magnitude : 0f):F1} obstacle {(obstacle < float.MaxValue ? obstacle.ToString("F1") : "-")} red light {(red < float.MaxValue ? red.ToString("F1") : "-")}");
            }
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
    static readonly bool DebugTraffic = System.Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0;
    float _nextDebug;

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
                if (d > 12f) return Crossing();
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
        return Crossing();
    }

    static readonly List<CarTrafficLight> _lights = new List<CarTrafficLight>();
    static float _nextLightScan = -1f;

    /// <summary>
    /// A red light (or a yellow one it can stop for at 3 m/s²) for the rider's way within 40 m: the distance to a metre
    /// before its stop line (the head hangs over the approach at the line, facing the traffic it controls), so the rider
    /// waits at the line; float.MaxValue when there is none. Riders on a cycle path beside the road keep the road's
    /// lights; turn-arrow heads are for the turning lane and do not count.
    /// </summary>
    float RedLightAhead()
    {
        if (Time.time >= _nextLightScan) { _nextLightScan = Time.time + 2f; _lights.Clear(); _lights.AddRange(FindObjectsByType<CarTrafficLight>()); }
        if (_lights.Count == 0) return float.MaxValue;
        int step = _back ? -1 : 1;
        var prev = transform.position; float along = 0f;
        float speed = _body != null ? _body.linearVelocity.magnitude : 0f;
        for (int i = 0, k = _index; i < 12 && k >= 0 && k < Path.Count && along < 40f; i++, k += step)
        {
            var seg = Flat(Path[k] - prev); float len = seg.magnitude;
            for (float s = 1f; s <= len; s += 1f)
            {
                var p = prev + seg * (s / len); float d = along + s;
                var dir = seg / Mathf.Max(len, 1e-3f);
                foreach (var light in _lights)
                {
                    if (light == null || (light.State != LightState.RED && light.State != LightState.YELLOW && light.State != LightState.RED_AND_YELLOW)) continue;
                    if (light.Arrow != CarTrafficLight.TurnArrow.None) continue;   // (a turn arrow is for the turning lane)
                    if (Flat(light.transform.position - p).sqrMagnitude > 6f * 6f) continue;
                    if (Vector3.Dot(Flat(TrafficCar.LightFacing(light.transform)).normalized, dir) > -0.7f) continue;
                    // the head hangs over the stop line: its distance along the path from the sample level with it (the
                    // samples are at most 2 m apart; the first one within reach of it is some 5 m short of the line)
                    float ahead = Vector3.Dot(Flat(light.transform.position - p), dir);
                    if (ahead > 1f || ahead <= -1f) continue;
                    if (d + ahead < 1f) continue;   // at the line already: ride on
                    // (the rider stops 1.5 m short of this: its front about 1.5 m before the line)
                    float stop = Mathf.Max(0f, d + ahead - 1f);
                    if (light.State == LightState.YELLOW && speed * speed / 6f > stop) continue;   // too close: ride on
                    return stop;
                }
            }
            along += len; prev = new Vector3(Path[k].x, prev.y, Path[k].z);
        }
        return float.MaxValue;
    }

    readonly Collider[] _around = new Collider[64];
    readonly HashSet<Rigidbody> _seen = new HashSet<Rigidbody>();
    readonly List<(Vector3 p, float d)> _samples = new List<(Vector3, float)>();

    /// <summary>
    /// Vehicles about to cross the path (from the side, at a junction): each moving vehicle within 25 m, straight on at
    /// its speed for the next 3 s, that reaches a point of the path within 1.3 m less than a second after the rider would
    /// (or before): the distance to that point, so the rider slows and gives way instead of riding into it.
    /// </summary>
    float Crossing()
    {
        if (_rider == null || _body == null) return float.MaxValue;
        int step = _back ? -1 : 1;
        _samples.Clear();
        var prev = transform.position; float along = 0f;
        for (int i = 0, k = _index; i < 8 && k >= 0 && k < Path.Count; i++, k += step)
        {
            var seg = Flat(Path[k] - prev); float len = seg.magnitude;
            for (float s = 1f; s <= len && along + s <= 20f; s += 1f) _samples.Add((prev + seg * (s / len), along + s));
            along += len; prev = new Vector3(Path[k].x, prev.y, Path[k].z);
            if (along > 20f) break;
        }
        float mySpeed = Mathf.Max(1f, _body.linearVelocity.magnitude);
        float best = float.MaxValue;
        _seen.Clear();
        int n = Physics.OverlapSphereNonAlloc(transform.position, 25f, _around, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var rb = _around[i].attachedRigidbody;
            if (rb == null || rb.transform.IsChildOf(transform) || transform.IsChildOf(rb.transform) || !_seen.Add(rb)) continue;
            var v = Flat(rb.linearVelocity);
            if (v.sqrMagnitude < 1f) continue;   // standing: ObstacleAhead's when on the path
            if (Vector3.Dot(v.normalized, Flat(transform.forward)) > 0.7f) continue;   // the same way: followed, not crossing
            for (float t = 0f; t <= 3f; t += 0.25f)
            {
                var q = rb.position + v * t;
                foreach (var (sp, sd) in _samples)
                {
                    if (sd >= best || Flat(q - sp).sqrMagnitude > 1.3f * 1.3f) continue;
                    if (t < sd / mySpeed + 1f) best = Mathf.Max(0f, sd - 2.5f);
                    break;
                }
            }
        }
        return best;
    }
}
