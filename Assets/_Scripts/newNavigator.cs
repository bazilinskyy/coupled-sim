using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

//[RequireComponent(typeof(CrossingSpawner))]
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
    private bool started = false;
    void StartUp()
    {
        crossingSpawner = GetComponent<CrossingSpawner>();

        if (!experimentManager.RenderHUD()) { HUD.SetActive(false); }

        //Add first four targets to list
        waypointList = new List<WaypointStruct>();

        WaypointStruct[] targetsCurrent = crossingSpawner.crossings.GetWaypoints("Current");
        foreach (WaypointStruct target in targetsCurrent) { waypointList.Add(target); }

        targetsCurrent = crossingSpawner.crossings.GetWaypoints("Next");
        foreach (WaypointStruct target in targetsCurrent) { waypointList.Add(target); }

        waypoint = waypointList[waypointIndex];

        RenderNavigationArrow();
        started = true;
    }

    private void Update()
    {
        if (!started) { StartUp(); }

        if (experimentManager.RenderHUD()) { HUD.SetActive(true); }
        else { HUD.SetActive(false); }

        RenderNavigationDistance();

    }
   
    public void ResetWaypoints()
    {
        waypointIndex = 0;
        waypoint = waypointList[waypointIndex];

    }
    public WaypointStruct GetNextWaypoint()
    {
        if (waypointIndex + 1 < waypointList.Count()) { return waypointList[waypointIndex + 1]; }
        else { return waypointList[waypointIndex]; }
    }
    void RenderNavigationDistance()
    {
        if (!experimentManager.RenderHUD()) { return; }

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
        string[] triggers = { "CorrectTurn", "WrongTurn",  "OutOfBounce", "NavigationFinished", "EndStraight", "EnterCrossing" };

        if (!triggers.Contains(other.tag)) { return; }
        //Check the trigger (this pscripts prevents multiple trigger events happening in quick succesion)
        if (!other.GetComponent<MyCollider>().Triggered()){ return; }
        //Handle all triggers

        if (other.CompareTag("CorrectTurn"))
        {
            atWaypoint = false;
            SetNextWaypoint();
            RenderNavigationArrow();
            
        }
        else if (other.CompareTag("WrongTurn")){ experimentManager.TookWrongTurn();}
        else if (other.CompareTag("OutOfBounce")) {experimentManager.CarOutOfBounce(); }
        else if (other.gameObject.CompareTag("NavigationFinished")){
            navigationFinished = true;
            //As we dont spawn the next crossing (since this is the last waypoint) we need to manually add the targets of NEXT crossing 
            //Normally they are added automatically when entering the new crossing....
            experimentManager.LogTargets(crossingSpawner.crossings.NextCrossing().components.targetList);
        }
        else if (other.gameObject.CompareTag("EndStraight")) { experimentManager.EndOfStraight(); }
        else if (other.gameObject.CompareTag("EnterCrossing"))
        {
            if (!experimentManager.experimentSettings.experimentType.IsTargetCalibration())
            {
                //Debug.Log("Enter trigger called, configuring next crossing...");
                experimentManager.LogTargets(crossingSpawner.crossings.CurrentCrossing().components.targetList);
                crossingSpawner.SetNextCrossing();
            }
            
        }

    }

    public void SetNextWaypoint()
    {
        waypointIndex++;

        if (waypointIndex < waypointList.Count()) { waypoint = waypointList[waypointIndex]; }
        //else { Debug.Log("Finished waypoint List..."); }

        //Debug.Log($"Next target = {waypoint.turn}...");
    }
    public void AddWaypoints(WaypointStruct[] waypoints)
    {
        foreach (WaypointStruct waypoint in waypoints) { waypointList.Add(waypoint); }
    }
    public void RenderNavigationArrow()
    {  
        if (!experimentManager.RenderHUD()) { return; }

        Transform arrows = HUD.transform.Find("Arrows");
        if (arrows == null) { Debug.Log("Arrows= null...."); return; }
        if (waypoint.turn == TurnType.Right) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.right; arrows.GetComponent<MoveCollider>().RightArrow(); }
        else if (waypoint.turn == TurnType.Left) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.left; arrows.GetComponent<MoveCollider>().LeftArrow(); }
        else if (waypoint.turn == TurnType.Straight) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.straight; arrows.GetComponent<MoveCollider>().CenterArrow(); }
        else if (waypoint.turn == TurnType.EndPoint) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.destination; arrows.GetComponent<MoveCollider>().CenterArrow(); }
    }

}


