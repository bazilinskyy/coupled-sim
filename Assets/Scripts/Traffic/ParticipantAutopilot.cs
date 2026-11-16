using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves a walking or riding participant along a path by itself, through the same inputs a person would give (for
/// demonstrations and videos; -autopilot): a pedestrian walks along the pavement (turning towards a point a few
/// metres ahead), a rider follows its riding line (MapTraffic.RideLine: a cycle path beside the road, else near the
/// kerb; steering towards a point ahead, pedalling or riding
/// at a steady effort, braking for the bends). At the end of the path it turns round (pedestrians) or stops.
/// </summary>
public class ParticipantAutopilot : MonoBehaviour
{
    public List<Vector3> Path;
    public float LookAhead = 5f;
    [Tooltip("Rider's effort (0..1).")]
    public float Effort = 0.55f;
    KeyboardPlayerMovement _walker;
    TwoWheelerController _rider;
    int _index;
    bool _back;

    public void Init(List<Vector3> path)
    {
        Path = path;
        _walker = GetComponent<KeyboardPlayerMovement>();
        _rider = GetComponent<TwoWheelerController>();
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
            float effort = bend > 50f ? 0.2f : Effort;
            _rider.Input = new CoupledSim.TwoWheelers.TwoWheelerInput { Drive = effort, Steer = Mathf.Clamp(err / 25f, -1f, 1f), Brake = bend > 70f ? 0.3f : 0f };
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
