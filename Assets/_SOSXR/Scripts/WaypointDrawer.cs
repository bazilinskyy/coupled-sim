using System;
using UnityEngine;
using UnityStandardAssets.Utility;


[ExecuteAlways]
[RequireComponent(typeof(WaypointCircuit))]
public class WaypointDrawer : MonoBehaviour
{
    [SerializeField] private Color m_startPointColor = Color.green;
    [SerializeField] private Color m_pathColor = Color.blue;
    [SerializeField] private Color m_pointColor = Color.yellow;
    [SerializeField] private Color m_endPointColor = Color.red;
    [SerializeField] private bool m_loopToStart;
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
                if (m_loopToStart)
                {
                    Gizmos.color = m_pathColor;
                    Gizmos.DrawLine(_waypointCircuit.Waypoints[i].transform.position, _waypointCircuit.Waypoints[0].transform.position);
                }

                Gizmos.color = m_endPointColor;
                Gizmos.DrawCube(_waypointCircuit.Waypoints[i].transform.position, Vector3.one * 2);
            }
            else
            {
                Gizmos.color = m_pathColor;
                Gizmos.DrawLine(_waypointCircuit.Waypoints[i].transform.position, _waypointCircuit.Waypoints[i + 1].transform.position);
            }


            if (i == 0)
            {
                Gizmos.color = m_startPointColor;
                Gizmos.DrawCube(_waypointCircuit.Waypoints[i].transform.position, Vector3.one * 2);
            }
            else
            {
                Gizmos.color = m_pointColor;
                Gizmos.DrawSphere(_waypointCircuit.Waypoints[i].transform.position, 0.25f);
            }
        }
    }
}