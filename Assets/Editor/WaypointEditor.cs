using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad()]
public class WaypointEditor
{
    
    static void DrawGameObjectName(Waypoint waypoint, GizmoType gizmoType)
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        var name = waypoint.gameObject.name;
        var waypointNumber = name.Substring(8, name.Length - 8);

        Handles.Label(waypoint.transform.position, waypointNumber, style);
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    public static void OnDrawSceneGizmo(Waypoint waypoint, GizmoType gizmoType)
    {
        if((gizmoType & GizmoType.Selected) != 0)
        {
            Gizmos.color = Color.yellow;
        }
        else
        {
            Gizmos.color = Color.yellow * 0.5f;
        }

        DrawGameObjectName(waypoint, gizmoType);

        Gizmos.DrawSphere(waypoint.transform.position, .1f);
        Gizmos.color = Color.green;
       
        if(waypoint.nextWaypoint != null){

            Gizmos.DrawLine(waypoint.transform.position, waypoint.nextWaypoint.transform.position);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawLine(waypoint.transform.position, waypoint.transform.position + waypoint.transform.forward * 1f);
    }

}
