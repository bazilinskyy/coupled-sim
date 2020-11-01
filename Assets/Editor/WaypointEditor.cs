using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Leap.Unity.Animation;

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
        if ((gizmoType & GizmoType.Selected) != 0)
        {
            Gizmos.color = Color.yellow;
        }
        else
        {
            Gizmos.color = Color.yellow * 0.5f;
        }

        DrawGameObjectName(waypoint, gizmoType);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(waypoint.transform.position + new Vector3(0, 0.1f, 0), waypoint.transform.position + new Vector3(0, 0.1f, 0) + waypoint.transform.forward * 3f);

        Gizmos.DrawSphere(waypoint.transform.position, 0.35f);
        //Draw box where target is "just after corner"
        if (waypoint.operation == Operation.TurnLeft || waypoint.operation == Operation.TurnRight)
        {
            float sphereSize = 0.3f;
            float boxSize = 20f; float streetWidth = 10f;
            
            Vector3 center = waypoint.transform.position + waypoint.transform.forward * 7.5f + Vector3.up;

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(center, sphereSize*2);

            Vector3 direction;
            if (waypoint.operation == Operation.TurnRight) { direction = waypoint.transform.right; }
            else { direction = -waypoint.transform.right; }

            Vector3 point1 = center - waypoint.transform.forward * streetWidth;
            Vector3 point2 = center + waypoint.transform.forward * streetWidth;

            Vector3 point3 = point2 + direction * boxSize;
            Vector3 point4 = point1 + direction * boxSize;
            
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(point1, sphereSize); Gizmos.DrawSphere(point2, sphereSize); Gizmos.DrawSphere(point3, sphereSize); Gizmos.DrawSphere(point4, sphereSize);
            Gizmos.DrawLine(point1, point2); Gizmos.DrawLine(point2, point3); Gizmos.DrawLine(point3, point4); Gizmos.DrawLine(point4, point1);
        }
    }

}
