using UnityEngine;
using UnityStandardAssets.Utility;

public class AIPedestrian : MonoBehaviour
{
    WaypointProgressTracker _tracker;
    float _startYPosition;

    public void Init(WaypointCircuit circuit)
    {
        enabled = true;
        _tracker = GetComponent<WaypointProgressTracker>();
        _tracker.enabled = true;
        _tracker.Init(circuit);
        _startYPosition = transform.position.y;
    }

    private void Update()
    {
        var steer = Quaternion.LookRotation(_tracker.target.position - transform.position, Vector3.up).eulerAngles;
        var rot = transform.eulerAngles;
        rot.y = steer.y;
        transform.eulerAngles = rot;
        var pos = transform.position;
        pos.y = _startYPosition;
        transform.position = pos;
    }
}
