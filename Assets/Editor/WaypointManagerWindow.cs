using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
public class WaypointManagerWindow : EditorWindow
{
    [MenuItem("Tools/Waypoint Editor")]
    public static void Open()
    {
        GetWindow<WaypointManagerWindow>();
    }

    public Transform waypointRoot;
    public GameObject targetPrefab;

    private void OnGUI()
    {
        SerializedObject obj = new SerializedObject(this);
        EditorGUILayout.PropertyField(obj.FindProperty("waypointRoot"));
        EditorGUILayout.PropertyField(obj.FindProperty("targetPrefab"));

        if (waypointRoot == null) { EditorGUILayout.HelpBox("Root transform must be selected. Please assign a root transform", MessageType.Warning); }
        else
        {
            //ALlow creating of waypoints but warn for not working target functionaility
            if (targetPrefab == null) { EditorGUILayout.HelpBox("A target prefab needs to be assigned if you want to use the attached target system.", MessageType.Warning); }
            else { EditorGUILayout.HelpBox("Always place taret between current and next waypoint!", MessageType.Info); }

            EditorGUILayout.BeginVertical("box");
            DrawButtons();
            EditorGUILayout.EndVertical();
        }
        obj.ApplyModifiedProperties();

        //Adjust waypoint root automatically based on which waypoint is selected
        if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Waypoint>() != null && Selection.activeGameObject.transform.parent != waypointRoot) { waypointRoot = Selection.activeGameObject.transform.parent; }
    }
    void DrawButtons()
    {
        if (Selection.activeGameObject != null && (Selection.activeGameObject.GetComponent<Waypoint>()))
        {
            if (GUILayout.Button("Create straight")) { CreateNewWaypoint(Operation.Straight); }

            //if (GUILayout.Button("Create short right turn")) { CreateNewWaypoint(Operation.TurnRightShort); }

            if (GUILayout.Button("Create right")) { CreateNewWaypoint(Operation.TurnRight); }
            
            if (GUILayout.Button("Create left")) { CreateNewWaypoint(Operation.TurnLeft); }

            if (GUILayout.Button("Create spline point")) { CreateNewWaypoint(Operation.SplinePoint); }

            if (GUILayout.Button("Create end point")) { CreateEndWaypoint(Operation.EndPoint); }

            if (GUILayout.Button("Add target")) { AddTarget(); }

            if (GUILayout.Button("Remove Waypoint")) { RemoveWaypoint(); }
        }
        else if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Target>())
        {
            if (GUILayout.Button("Add target")) { AddTarget(); }
        }
        else
        {
            if (GUILayout.Button("Start navigation tree"))
            {
                CreateFirstWaypoint();
                CreateNewWaypoint(Operation.StartPoint);
            }
        }
    }
    void SetTargetAttributes(GameObject target, Waypoint selectedWaypoint)
    {
        target.name = "Target " + (selectedWaypoint.TargetCount() + 1);
        target.transform.SetParent(selectedWaypoint.transform, true);
        target.GetComponent<Target>().waypoint = selectedWaypoint;
    }
    void AddTarget()
    {
        //Let user know if target prefab is missing for some reason
        if(targetPrefab == null){ EditorUtility.DisplayDialog("No target Prefab", "No target prefab available!", "ok", "cancel"); }

        //Make target associated with the selected waypoint or target
        Waypoint parentWaypoint;
        if(Selection.activeGameObject.GetComponent<Waypoint>() != null) { parentWaypoint = Selection.activeGameObject.GetComponent<Waypoint>(); }
        else if(Selection.activeGameObject.transform.parent.GetComponent<Waypoint>() != null) { parentWaypoint = Selection.activeGameObject.transform.parent.GetComponent<Waypoint>(); }
        else { throw new System.Exception("No target or waypoint is selected... Cant add target..."); }

        GameObject target = Instantiate(targetPrefab, Selection.activeGameObject.transform.position, Quaternion.identity);
        
        //Set height to 1;
        Vector3 targetPosition = target.transform.position; targetPosition.y = 1f;
        target.transform.position = targetPosition;
        //set name, waypoint and parent
        SetTargetAttributes(target, parentWaypoint);


        //Set all IDs and names for the targets associated to this wayu point.
        //This way we make sure they are unique even if we deleted some targets 
        parentWaypoint.SetTargetIDsAndNames();

        //Select it
        Selection.activeGameObject = target;

        }
    void CreateFirstWaypoint()

    {
        //Make the starting point only if there are no children
        if (waypointRoot.childCount < 1)
        {
            GameObject newWaypointObject = new GameObject("Waypoint " + waypointRoot.childCount, typeof(Waypoint));

            newWaypointObject.transform.SetParent(waypointRoot, false);
            Waypoint waypoint = newWaypointObject.GetComponent<Waypoint>();

            waypoint.operation = Operation.StartPoint;

            waypoint.orderId = 0;
            //Select this object
            Selection.activeGameObject = waypoint.gameObject;
        }
    }
    void CreateEndWaypoint(Operation operation)
    {
        Waypoint selectedWaypoint = Selection.activeGameObject.GetComponent<Waypoint>();
        selectedWaypoint.operation = operation;
    }
    bool IsRightTurn(Operation operation)
    {
        Operation[] turns = { Operation.TurnRightShort, Operation.TurnRightLong, Operation.TurnRight };
        if (turns.Contains(operation)) { return true; }
        else { return false; }
    }
    bool IsLeftTurn(Operation operation)
    {
        Operation[] turns = { Operation.TurnLeftLong, Operation.TurnLeft };
        if (turns.Contains(operation)) { return true; }
        else { return false; }
    }
    void SetAttributesNewWaypoint(Waypoint newWaypoint, Waypoint selectedWaypoint, Operation operation)
    {
        //set attributes of new waypoint
        newWaypoint.transform.SetParent(waypointRoot, false);

        newWaypoint.transform.SetSiblingIndex(selectedWaypoint.transform.GetSiblingIndex());
        newWaypoint.transform.forward = selectedWaypoint.transform.forward;
        newWaypoint.transform.rotation = selectedWaypoint.transform.rotation;

        //Turn waypoint and set position based on operation and road radius
        //If last point was a spline point we change rotaiton based on this;
        RoadParameters roadParameters = waypointRoot.GetComponent<SplineCreator>().roadParameters;
        if (IsRightTurn(operation))
        { 
            newWaypoint.transform.Rotate(Vector3.up * 90, Space.World);
            newWaypoint.transform.position = selectedWaypoint.transform.position + roadParameters.radiusLong * (newWaypoint.transform.forward + selectedWaypoint.transform.forward);
        }
        else if (IsLeftTurn(operation))
        {
            newWaypoint.transform.Rotate(Vector3.up * -90, Space.World);
            newWaypoint.transform.position = selectedWaypoint.transform.position + roadParameters.radiusLong * (newWaypoint.transform.forward + selectedWaypoint.transform.forward);
        }
        else { newWaypoint.transform.position = selectedWaypoint.transform.position + 5 * newWaypoint.transform.forward; }
      
        newWaypoint.orderId = selectedWaypoint.orderId + 1;
        newWaypoint.previousWaypoint = selectedWaypoint;
        newWaypoint.operation = Operation.None;

        if (selectedWaypoint.nextWaypoint) { newWaypoint.nextWaypoint = selectedWaypoint.nextWaypoint; }
        //Set to spline point if we are creating a spline
        //if (selectedWaypoint.operation == Operation.SplinePoint) { newWaypoint.operation = Operation.SplinePoint; }
        //else { newWaypoint.operation = Operation.None; }
    }
    void SetAttributesSelectedWaypoint( Waypoint newWaypoint, Waypoint selectedWaypoint, Operation operation)
    {
        if (selectedWaypoint.nextWaypoint) { selectedWaypoint.nextWaypoint.previousWaypoint = newWaypoint; }
        selectedWaypoint.nextWaypoint = newWaypoint;
        selectedWaypoint.operation = operation;
        
        
    }
    void CreateNewWaypoint(Operation operation)
    {
        //create new game object with a waypoint
        GameObject newWaypointObject = new GameObject("Waypoint " + waypointRoot.childCount, typeof(Waypoint));

        Debug.Log($"Created {newWaypointObject.name}...");

        Waypoint newWaypoint = newWaypointObject.GetComponent<Waypoint>();
        Waypoint selectedWaypoint = Selection.activeGameObject.GetComponent<Waypoint>();

        //Set attributes of new waypoint
        SetAttributesNewWaypoint(newWaypoint, selectedWaypoint, operation);

        //Set attributes slected waypoint
        SetAttributesSelectedWaypoint(newWaypoint, selectedWaypoint, operation);

        //select the new Waypoint
        Selection.activeGameObject = newWaypoint.gameObject;

        //Update the order id's
        waypointRoot.GetComponent<NavigationHelper>().UpdateOrderIds();
    }
    void RemoveWaypoint()
    {
        Waypoint selectedWaypoint = Selection.activeGameObject.GetComponent<Waypoint>();

        if (selectedWaypoint.nextWaypoint != null)
        {
            selectedWaypoint.nextWaypoint.previousWaypoint = selectedWaypoint.previousWaypoint;

        };
        if (selectedWaypoint.previousWaypoint != null)
        {
            selectedWaypoint.previousWaypoint.nextWaypoint = selectedWaypoint.nextWaypoint;
            Selection.activeGameObject = selectedWaypoint.previousWaypoint.gameObject;
        };

        DestroyImmediate(selectedWaypoint.gameObject);

        waypointRoot.gameObject.GetComponent<NavigationHelper>().UpdateOrderIds();
    }
}

