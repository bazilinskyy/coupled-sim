using UnityEngine;
using UnityStandardAssets.Utility;

public class AIPedestrian : MonoBehaviour
{
    WaypointProgressTracker _tracker;
    public float moveSpeed = 1;
    float currentSpeed;
    public float HeightDampingFactor = 0.05f;
    public float SpeedDampingFactor = 0.05f;
    public float referenceSpeed = 1.6f;
    public Animator animator;
    public void Init(WaypointCircuit circuit)
    {
        enabled = true;
        _tracker = GetComponent<WaypointProgressTracker>();
        _tracker.enabled = true;
        _tracker.Init(circuit);
        foreach (var waypoint in circuit.Waypoints)
        {
            var speedSettings = waypoint.GetComponent<PedestrianWaypoint>();
            if (speedSettings != null)
            {
                speedSettings.target = this;
            }
        }
        currentSpeed = moveSpeed;
    }

    // Smoothing rate dictates the proportion of source remaining after one second
    public static float Damp(float source, float target, float smoothing, float dt)
    {
        return Mathf.Lerp(source, target, 1 - Mathf.Pow(smoothing, dt));
    }

    private void Update()
    {
        currentSpeed = Damp(currentSpeed, moveSpeed, SpeedDampingFactor, Time.deltaTime);
        animator.SetFloat("Speed", currentSpeed / referenceSpeed);
        var steer = Quaternion.LookRotation(_tracker.target.position - transform.position, Vector3.up).eulerAngles;
        var rot = transform.eulerAngles;
        rot.y = steer.y;
        transform.eulerAngles = rot;
        var pos = transform.position;
        pos += transform.forward * currentSpeed * Time.deltaTime;
        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out RaycastHit hitInfo, 2))
        {
            pos.y = Damp(pos.y, hitInfo.point.y, HeightDampingFactor, Time.deltaTime);
        }
        transform.position = pos;
    }

    void OnTriggerEnter(Collider other)
    {
        PedestrianWaypoint speedSettings = other.GetComponent<PedestrianWaypoint>();
        if (speedSettings == null || (speedSettings.target != null && speedSettings.target != this))
        {
            return;
        }
        moveSpeed = speedSettings.targetSpeed;
    }
}
