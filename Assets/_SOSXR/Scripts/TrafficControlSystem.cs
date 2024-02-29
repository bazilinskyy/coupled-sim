using UnityEngine;
using UnityStandardAssets.Utility;


[RequireComponent(typeof(WaypointProgressTracker))]
[RequireComponent(typeof(AICar))]
public class TrafficControlSystem : MonoBehaviour
{
    private AICar m_aiCar;
    private WaypointProgressTracker m_tracker;


    private void Awake()
    {
        m_aiCar = GetComponent<AICar>();
        m_tracker = GetComponent<WaypointProgressTracker>();
    }


    private void Start()
    {
        ActivateWayPointProgressTracker();
    }


    public void ActivateWayPointProgressTracker()
    {
        m_aiCar.enabled = true;
        m_tracker.enabled = true;

        foreach (var waypoint in m_tracker.Circuit.Waypoints)
        {
            var speedSettings = waypoint.GetComponent<SpeedSettings>();

            if (speedSettings != null)
            {
                speedSettings.targetAICar = m_aiCar;
            }
        }

        m_tracker.Init(m_tracker.Circuit);
    }
}