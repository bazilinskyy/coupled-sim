using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(CrossingSpawner))]
public class newNavigator : MonoBehaviour
{
    public HUDMaterials HUDMaterials;
    public GameObject HUD;
    public newExperimentManager experimentManager;

    CrossingSpawner crossingSpawner;
    

    public List<WaypointStruct> waypointList;
    public WaypointStruct waypoint;
    public int waypointIndex = 0;

    public bool navigationFinished = false;
    public bool atWaypoint = false;

    int lastIndex = 0;
    private void Start()
    {
        crossingSpawner = GetComponent<CrossingSpawner>();

        if (!experimentManager.renderHUD) { HUD.SetActive(false); }

        //Add first four targets to list
        waypointList = new List<WaypointStruct>();

        WaypointStruct[] targetsCurrent = crossingSpawner.crossings.GetWaypoints("Current");
        foreach(WaypointStruct target in targetsCurrent) { waypointList.Add(target); }

        targetsCurrent = crossingSpawner.crossings.GetWaypoints("Next");
        foreach (WaypointStruct target in targetsCurrent) { waypointList.Add(target); }

        waypoint = waypointList[waypointIndex];
        
        RenderNavigationArrow();
    }

    private void Update()
    {
        RenderNavigationDistance();

        if (waypointIndex != lastIndex) { Debug.Log(waypointIndex); lastIndex = waypointIndex; }
    }
   
    void RenderNavigationDistance()
    {
        if (!experimentManager.renderHUD) { return; }

        if (waypoint.waypoint == null) { return; }

        Transform text = HUD.transform.Find("Text");
        TextMesh textMesh = text.gameObject.GetComponent<TextMesh>();
        
        float distanceToTarget = Vector3.Magnitude(waypoint.waypoint.transform.position- transform.position);
        int renderedDistance = ((int)distanceToTarget - ((int)distanceToTarget % 5));
        if (renderedDistance <= 0) { renderedDistance = 0; atWaypoint = true; }
        if (atWaypoint) { renderedDistance = 0; }
        textMesh.text = $"{renderedDistance}m";
    }

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("EnterCrossing"))
        {
            if (other.GetComponent<MyCollider>().Triggered()) { StartCoroutine(AddNextTargets()); }

        }
        else if (other.CompareTag("CorrectTurn"))
        {
            if (other.GetComponent<MyCollider>().Triggered())
            {
                waypointIndex++; atWaypoint = false;

                if (waypointIndex < waypointList.Count()) { waypoint = waypointList[waypointIndex]; }
                else { Debug.Log("Finished waypoint List..."); }

                Debug.Log($"Next target = {waypoint.turn}...");
                RenderNavigationArrow();
            }
        }
        else if (other.CompareTag("WrongTurn"))
        {
            if (other.GetComponent<MyCollider>().Triggered())
            {
                waypointIndex++;

                if (waypointIndex < waypointList.Count()) { waypoint = waypointList[waypointIndex]; }
                else { Debug.Log("Finished waypoint List..."); }

                Debug.Log("Wrong turn!!!!!");
                experimentManager.TookWrongTurn();
            }
        }
        else if (other.gameObject.CompareTag("NavigationFinished"))
        {
            navigationFinished = true;
        }

    }
    IEnumerator AddNextTargets()
    {
        yield return new WaitForSeconds(1f);
        WaypointStruct[] newWaypoints = crossingSpawner.crossings.GetWaypoints("Next");
        foreach (WaypointStruct waypoint in newWaypoints) { waypointList.Add(waypoint); }
    }
    public void RenderNavigationArrow()
    {  
        if (!experimentManager.renderHUD) { return; }

        Transform arrows = HUD.transform.Find("Arrows");
        if (arrows == null) { Debug.Log("Arrows= null...."); return; }
        if (waypoint.turn == TurnType.Right) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.right; arrows.GetComponent<MoveCollider>().RightArrow(); }
        else if (waypoint.turn == TurnType.Left) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.left; arrows.GetComponent<MoveCollider>().LeftArrow(); }
        else if (waypoint.turn == TurnType.Straight) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.straight; arrows.GetComponent<MoveCollider>().CenterArrow(); }
        else if (waypoint.turn == TurnType.EndPoint) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.destination; arrows.GetComponent<MoveCollider>().CenterArrow(); }
    }

}


