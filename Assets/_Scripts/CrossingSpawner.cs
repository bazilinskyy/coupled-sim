using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

[RequireComponent(typeof(CreateVirtualCable), typeof(newNavigator))]
public class CrossingSpawner : MonoBehaviour
{
    // Start is called before the first frame update

    public TurnType[] turnsList;
    private int turnIndex = 0;

    public GameObject cross1;
    public GameObject cross2;

    public Crossings crossings;
    private newExperimentManager experimentManager;
    private int waypointID = 0;
    public void StartUp()
    {

        experimentManager = MyUtils.GetExperimentManager();

        if(experimentManager == null) { Debug.Log("Experiment manager == null crosingSpawner......."); }

        if(turnsList.Length < 4) { Debug.LogError("Set atleast 4 turnsList...."); enabled = false; return; }

        //Mkae first set of waypoints
        WaypointStruct[] waypoints1 = new WaypointStruct[2];
        WaypointStruct[] waypoints2 = new WaypointStruct[2];

        waypoints1[0].turn = turnsList[0]; waypoints1[0].waypointID = WaypointID();
        waypoints1[1].turn = turnsList[1]; waypoints1[1].waypointID = WaypointID();
        waypoints2[0].turn = turnsList[2]; waypoints2[0].waypointID = WaypointID();
        waypoints2[1].turn = turnsList[3]; waypoints2[1].waypointID = WaypointID();

        turnIndex = 4;

        crossings = new Crossings(cross1, cross2, waypoints1, waypoints2, experimentManager.experimentSettings);

        GetComponent<CreateVirtualCable>().MakeVirtualCable(crossings);
    }

    public int WaypointID() { waypointID++; return waypointID - 1; }

    public void SetNextCrossing()
    {
        if (NextTurns())
        {
            //Set up next crossing
            crossings.SetNextCrossing(GetNextWaypoints(), experimentManager.experimentSettings);
            
            //Add new waypoints to the navigator
            GetComponent<newNavigator>().AddWaypoints(crossings.GetWaypoints("Next"));
            
            //Make virtual cable if nescesrry here.
            GetComponent<CreateVirtualCable>().MakeVirtualCable(crossings);

            turnIndex += 2;
        }
        else { crossings.CurrentCrossing().components.DisableVariableBlocks(); }

    }

    bool NextTurns()
    {
        if(turnIndex >= turnsList.Count()) { return false; }
        else { return true; }
    }
    WaypointStruct[] GetNextWaypoints()
    {
        WaypointStruct[] waypoints = new WaypointStruct[2];
        waypoints[0].turn = TurnType.None;  waypoints[1].turn = TurnType.None;
        if (turnIndex >= turnsList.Count()) { return waypoints; }
        if (turnIndex + 1 >= turnsList.Count()) 
        { 
            waypoints[0].turn = turnsList[turnIndex];  
            waypoints[0].waypointID = WaypointID(); 
            return waypoints; 
        }
        else 
        { 
            waypoints[0].turn = turnsList[turnIndex]; waypoints[0].waypointID = WaypointID(); 
            waypoints[1].turn = turnsList[turnIndex+1]; waypoints[1].waypointID = WaypointID(); 
            return waypoints; 
        }
    } 
    
}

[System.Serializable]
public class Crossing
{
    public GameObject obj;
    public CrossComponents components;
    public List<Target> targetList;
    public TurnType[] turns = new TurnType[2];
    public WaypointStruct[] waypoints = new WaypointStruct[2];

    public bool isCurrentCrossing;

    public Crossing( GameObject _obj, bool _isCurrentCrossing)
    {
        obj = _obj;
        components = _obj.GetComponent<CrossComponents>();
        isCurrentCrossing = _isCurrentCrossing;

        components.isCurrentCrossing = _isCurrentCrossing;

        targetList = components.targetList;

        turns[0] = components.waypoints[0].turn;
        turns[1] = components.waypoints[1].turn;

        waypoints = components.waypoints;
    }

   public void UpdateVariables()
    {
        targetList = components.targetList;

        turns[0] = components.waypoints[0].turn;
        turns[1] = components.waypoints[1].turn;

        waypoints = components.waypoints;
    }

    public void SwitchStatus(Transform otherCrossing) { 
        isCurrentCrossing = !isCurrentCrossing;
        components.SetCurrentCrossing(isCurrentCrossing, otherCrossing);
    }
}
[System.Serializable]
public class Crossings
{
    public Crossing crossing1 = null;
    public Crossing crossing2 = null;

    public Crossings(GameObject _crossing1, GameObject _crossing2, WaypointStruct[] waypoints1, WaypointStruct[] waypoints2, MainExperimentSetting settings)
    {

        bool isFirstCrossing = true; bool isCurrentCrossing = true;
        _crossing1.GetComponent<CrossComponents>().SetUpCrossing(waypoints1, settings, isCurrentCrossing, isFirstCrossing);
        crossing1 = new Crossing(_crossing1, true);

        ConfigureNextCrossing(_crossing2, waypoints1, waypoints2, settings);
        crossing2 = new Crossing(_crossing2, false);
    }
    public void SetNextCrossing(WaypointStruct[] nextWaypoints, MainExperimentSetting settings)
    {
        Crossing crossToConfigure;

        if (crossing1.isCurrentCrossing) { crossToConfigure = crossing1; }
        else { crossToConfigure = crossing2; }

        crossing1.SwitchStatus(crossing2.components.transform); 
        crossing2.SwitchStatus(crossing1.components.transform);

        ConfigureNextCrossing(crossToConfigure.obj, CurrentCrossing().waypoints, nextWaypoints, settings);
        
        crossToConfigure.UpdateVariables();
        }

    public Crossing CurrentCrossing()
    {
        if (crossing1.isCurrentCrossing) { return crossing1; }
        else { return crossing2; }
    }
    public Crossing NextCrossing()
    {
        if (!crossing1.isCurrentCrossing) { return crossing1; }
        else { return crossing2; }
    }
    void ConfigureNextCrossing(GameObject crossing, WaypointStruct[] currentWaypoints, WaypointStruct[] nextWaypoints, MainExperimentSetting settings)
    {
/*        Debug.Log("Configuring next crossing..");
        Debug.Log($"Got turns {nextTurns[0]} and {nextTurns[1]}...");*/
        bool isCurrentCrossing = false;
        bool isFirstCrossing = false;
        Vector3 nextPosition = new Vector3();
        float rotationAngleY = 0f;
        if (currentWaypoints[0].turn == TurnType.Left && currentWaypoints[1].turn == TurnType.Left) { nextPosition = GetNextCrossingPosition(-96f, -96f); rotationAngleY = 180f; }
        if (currentWaypoints[0].turn == TurnType.Left && currentWaypoints[1].turn == TurnType.Right) { nextPosition = GetNextCrossingPosition(96f, -96f); rotationAngleY = 0f; }
        if (currentWaypoints[0].turn == TurnType.Straight && currentWaypoints[1].turn == TurnType.Left) { nextPosition = GetNextCrossingPosition(96f, -96f); rotationAngleY = -90f; }
        if (currentWaypoints[0].turn == TurnType.Straight && currentWaypoints[1].turn == TurnType.Right) { nextPosition = GetNextCrossingPosition(96f, 96); rotationAngleY = 90f; }
        if (currentWaypoints[0].turn == TurnType.Right && currentWaypoints[1].turn == TurnType.Left) { nextPosition = GetNextCrossingPosition(96f, 96f); rotationAngleY = 0f; }
        if (currentWaypoints[0].turn == TurnType.Right && currentWaypoints[1].turn == TurnType.Right) { nextPosition = GetNextCrossingPosition(-96f, 96f); rotationAngleY = 180f; }

        //Set turns 
        crossing.GetComponent<CrossComponents>().SetUpCrossing(nextWaypoints, settings, isCurrentCrossing, isFirstCrossing, nextPosition, CurrentCrossing().obj.transform, rotationAngleY);
    }

    Vector3 GetNextCrossingPosition(float forward, float right)
    {
/*
        Debug.Log($"currentCrossing.forward: {CurrentCrossing().obj.transform.forward}, CurrentCrossing().right: { CurrentCrossing().obj.transform.right}...");
        Debug.Log($"Transforming with {forward} forward and {right} right w.r.t. {CurrentCrossing().obj.transform.position}...");
*/
        Vector3 addition = right * CurrentCrossing().obj.transform.right + forward * CurrentCrossing().obj.transform.forward;
        return CurrentCrossing().obj.transform.position + addition;

    }

    public TurnType[] GetTurns(string crossing)
    {
        if (crossing == "current") { return CurrentCrossing().turns; }
        else if (crossing == "next") { return NextCrossing().turns; }
        else { Debug.LogError("Wrong input for GetTurns...."); return null; }
    }

    public WaypointStruct[] GetWaypoints(string crossing)
    {
        if (crossing == "current" || crossing == "Current") { return CurrentCrossing().waypoints; }
        else if (crossing == "next" || crossing == "Next") { return NextCrossing().waypoints; }
        else { Debug.LogError("Wrong input for GetTurns...."); return null; }
    }

    public List<Target> GetAllTargets()
    {
        List<Target> targets = CurrentCrossing().components.targetList.Concat(NextCrossing().components.targetList).ToList();
        return targets;
    }
}

[System.Serializable]
public struct WaypointStruct
{
    public Transform waypoint;
    public TurnType turn;
    public int waypointID;
}