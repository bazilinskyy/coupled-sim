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
    public List<Waypoint> waypointOrderList;

    private void OnGUI()
    {
        SerializedObject obj = new SerializedObject(this);
        EditorGUILayout.PropertyField(obj.FindProperty("waypointRoot"));
        EditorGUILayout.PropertyField(obj.FindProperty("targetPrefab"));

        if (waypointRoot == null)
        {
            EditorGUILayout.HelpBox("Root transform must be selected. Please assign a root transform", MessageType.Warning);
        }
        else
        {
            //ALlow creating of waypoints but warn for not working target functionaility
            if (targetPrefab == null)
            {
                EditorGUILayout.HelpBox("A target prefab needs to be assigned if you want to use the attached target system.", MessageType.Warning);
            }
            EditorGUILayout.BeginVertical("box");
            DrawButtons();
            EditorGUILayout.EndVertical();
        }


        obj.ApplyModifiedProperties();
    }

    void DrawButtons()
    {
        if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Waypoint>())
        {
            if (GUILayout.Button("Create straight"))
            {
                CreateNewWaypoint(Operation.Straight);
                UpdateWaypointOrderList();
            }
            if (GUILayout.Button("Create right"))
            {
                CreateNewWaypoint(Operation.TurnRight);
                UpdateWaypointOrderList();
            }
            if (GUILayout.Button("Create left"))
            {
                CreateNewWaypoint(Operation.TurnLeft);
                UpdateWaypointOrderList();
            }
            if (GUILayout.Button("Create spline point"))
            {
                CreateNewWaypoint(Operation.TurnLeft, true);
                UpdateWaypointOrderList();
            }

            if (GUILayout.Button("Remove Waypoint"))
            {
                RemoveWaypoint();
                UpdateWaypointOrderList();
            }

            if (GUILayout.Button("Create end point"))
            {
                CreateEndWaypoint(Operation.EndPoint);
            }

            if(GUILayout.Button("Add target"))
            {
                AddTarget();
            }
        }
        else
        {
            if (GUILayout.Button("Start navigation tree"))
            {
                CreateFirstWaypoint();
                
                CreateNewWaypoint(Operation.Straight);
                UpdateWaypointOrderList();
            }
            if(GUILayout.Button("Add children waypoints"))
            {
                waypointOrderList = new List<Waypoint>();
                UpdateWaypointOrderList();
            }
        }

    }
    void AddTarget()
    {
        //Let user know if target prefab is missing for some reason
        if(targetPrefab == null)
        {
            EditorUtility.DisplayDialog("No target Prefab", "No target prefab available!", "ok", "cancel");
        }

        //Make target associated with the selected waypoint
        Waypoint selectedWaypoint = Selection.activeGameObject.GetComponent<Waypoint>();
        GameObject targetObject = Instantiate(targetPrefab, selectedWaypoint.transform.position, Quaternion.identity);

        //set name, waypoint and parent
        targetObject.name = "Target " + (selectedWaypoint.TargetCount() + 1);
        targetObject.transform.SetParent(selectedWaypoint.gameObject.transform, true);
        targetObject.GetComponent<Target>().waypoint = selectedWaypoint;

        //Set all IDs and names for the targets associated to this wayu point.
        //This way we make sure they are unique even if we deleted some targets 
        selectedWaypoint.SetTargetIDsAndNames();

        //Select it
        Selection.activeGameObject = targetObject;

        }
    void CreateFirstWaypoint()

    {
        //Make the starting point and the next point
        if (waypointRoot.transform.childCount < 1)
        {
            GameObject newWaypointObject = new GameObject("Waypoint " + waypointRoot.childCount, typeof(Waypoint));
            newWaypointObject.transform.SetParent(waypointRoot, false);
            Waypoint waypoint = newWaypointObject.GetComponent<Waypoint>();

            waypoint.operation = Operation.StartPoint;
            Selection.activeGameObject = newWaypointObject;

            //Clear any old residues of order list
            if (waypointOrderList != null)
            {
                waypointOrderList.Clear();
            }
            waypointOrderList = new List<Waypoint>();
            waypointOrderList.Add(waypoint);
            waypoint.orderId = 0;
            waypoint.extraSplinePoint = false;

            Selection.activeGameObject = waypoint.gameObject;
        }
    }

    void CreateEndWaypoint(Operation operation)
    {
        Waypoint selectedWaypoint = Selection.activeGameObject.GetComponent<Waypoint>();
        selectedWaypoint.operation = operation;
        selectedWaypoint.extraSplinePoint = false;

    }

    void CreateNewWaypoint(Operation operation, bool splinePoint= false)
    {
        //create new game object with a waypoint
        GameObject newWaypointObject = new GameObject("Waypoint " + waypointRoot.childCount, typeof(Waypoint));

        Debug.Log("CVhild count: " + waypointRoot.childCount);
        newWaypointObject.transform.SetParent(waypointRoot, false);

        Waypoint newWaypoint = newWaypointObject.GetComponent<Waypoint>();
        Waypoint selectedWaypoint = Selection.activeGameObject.GetComponent<Waypoint>();


        //Set attributes of selected waypoint
        selectedWaypoint.nextWaypoint = newWaypoint;
        if (selectedWaypoint.operation != Operation.StartPoint) { 
        selectedWaypoint.operation = operation; }
       
        //set extra spline points to false for start and end points automatically
        if (operation == Operation.EndPoint || operation ==Operation.StartPoint ) {
            selectedWaypoint.extraSplinePoint = false;
        }

        if (operation == Operation.TurnRight)
        {
            //Quaternion rotation = selectedWaypoint.previousWaypoint.transform.rotation;
            //selectedWaypoint.transform.rotation = Quaternion.Euler(rotation.x, rotation.y + 90, rotation.z);
            selectedWaypoint.transform.Rotate(Vector3.up * 90, Space.World);
            selectedWaypoint.extraSplinePoint = true;
        }
        else if (operation == Operation.TurnLeft)
        {
            //Quaternion rotation = selectedWaypoint.previousWaypoint.transform.rotation;
            //selectedWaypoint.transform.rotation = Quaternion.Euler(rotation.x, rotation.y - 90, rotation.z);
            selectedWaypoint.transform.Rotate(Vector3.up * -90, Space.World);
            selectedWaypoint.extraSplinePoint = true;
        }

        //set attributes of new waypoint
        newWaypointObject.transform.SetSiblingIndex(selectedWaypoint.transform.GetSiblingIndex());
        newWaypointObject.transform.forward = selectedWaypoint.transform.forward;
        newWaypointObject.transform.position = selectedWaypoint.transform.position + selectedWaypoint.transform.forward;
        newWaypointObject.transform.rotation = selectedWaypoint.transform.rotation;
        newWaypoint.orderId = selectedWaypoint.orderId + 1;
        newWaypoint.previousWaypoint = selectedWaypoint;
        newWaypoint.operation = Operation.None;
        newWaypoint.extraSplinePoint = false;

        //If this is a spline point then we set shapePoint attribute to true so that it gets skipped during the navigation of the car.
        if (splinePoint)
        {
            selectedWaypoint.shapePoint = true;
            selectedWaypoint.extraSplinePoint = false;
        }
        //newWaypoint
        Selection.activeGameObject = newWaypoint.gameObject;

        //update orderids
        waypointOrderList.Add(newWaypoint);
        UpdateWaypointOrderList();

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

        //remove from order list
        int listIndexSelectedWaypoint = waypointOrderList.FindIndex(a => a == selectedWaypoint);
        waypointOrderList.RemoveAt(listIndexSelectedWaypoint);
        UpdateWaypointOrderList();

        DestroyImmediate(selectedWaypoint.gameObject);
    }
    void UpdateWaypointOrderList()
    {
        if (waypointRoot.transform.childCount != waypointOrderList.Count)
        {
            List<Waypoint> sortedWaypointOrderList = new List<Waypoint>();
            foreach (Transform child in waypointRoot.transform)
            {
                Waypoint waypoint = child.gameObject.GetComponent<Waypoint>();
                int index = waypoint.orderId;
                sortedWaypointOrderList.Add(waypoint);

            }
            waypointOrderList.Clear();
            waypointOrderList = sortedWaypointOrderList.OrderBy(d => d.orderId).ToList();
            UpdateWaypointOrderIds();
        }
    }
    void UpdateWaypointOrderIds()
    {
        if (waypointOrderList.Count > 0)
        {
            int index = 0;

            foreach (Waypoint waypoint in waypointOrderList)
            {
                if (waypoint != null)
                {
                    waypoint.orderId = index;
                    //string nameWaypoint = new string("waypoint " + waypoint.orderId);
                    waypoint.transform.name = "waypoint " + waypoint.orderId;
                    waypoint.transform.gameObject.name = "waypoint " + waypoint.orderId;
                    index++;
                }
            }
        }
    }
}

