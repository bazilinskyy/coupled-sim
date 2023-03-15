using UnityEngine;
using UnityStandardAssets.Utility;

public class AIPedestrian : MonoBehaviour
{
    WaypointProgressTracker _tracker;
    public float moveSpeed = 1;
    public float dampingFactor = 0.1f;

    public void Init(WaypointCircuit circuit)
    {
        enabled = true;
        _tracker = GetComponent<WaypointProgressTracker>();
        _tracker.enabled = true;
        _tracker.Init(circuit);
    }

    // Smoothing rate dictates the proportion of source remaining after one second
    public static float Damp(float source, float target, float smoothing, float dt)
    {
        return Mathf.Lerp(source, target, 1 - Mathf.Pow(smoothing, dt));
    }

    private void Update()
    {
        var steer = Quaternion.LookRotation(_tracker.target.position - transform.position, Vector3.up).eulerAngles;
        var rot = transform.eulerAngles;
        rot.y = steer.y;
        transform.eulerAngles = rot;
        var pos = transform.position;
        pos += transform.forward * moveSpeed * Time.deltaTime;
        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out RaycastHit hitInfo, 2))
        {
            pos.y = Damp(pos.y, hitInfo.point.y, dampingFactor, Time.deltaTime);
        }
        transform.position = pos;
    }
}
