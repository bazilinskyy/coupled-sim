using UnityEngine;
using UnityStandardAssets.Utility;


[ExecuteAlways]
[RequireComponent(typeof(WaypointCircuit))]
public class WaypointDrawer : MonoBehaviour
{
    private WaypointCircuit _waypointCircuit;


    private void Awake()
    {
        _waypointCircuit = GetComponent<WaypointCircuit>();
    }


    private void OnDrawGizmos()
    {
        for (var i = 0; i < _waypointCircuit.Waypoints.Length; i++)
        {
            if (i == _waypointCircuit.Waypoints.Length - 1)
            {
                Gizmos.DrawLine(_waypointCircuit.Waypoints[i].transform.position, _waypointCircuit.Waypoints[0].transform.position);
            }
            else
            {
                Gizmos.DrawLine(_waypointCircuit.Waypoints[i].transform.position, _waypointCircuit.Waypoints[i + 1].transform.position);
            }

            if (i == 0)
            {
                Gizmos.DrawCube(_waypointCircuit.Waypoints[i].transform.position, Vector3.one);
            }
            else
            {
                Gizmos.DrawSphere(_waypointCircuit.Waypoints[i].transform.position, 0.25f);
            }
        }
    }
}