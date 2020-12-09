using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("EnterCrossing"))
        {
            if (other.GetComponent<MyCollider>().Triggered())
            {
                //Debug.Log("Enter trigger called, configuring next crossing...");
                experimentManager.LogCurrentCrossingTargets(crossings.CurrentCrossing().targetList);
                SetNextCrossing();
            }
        }
        if (other.gameObject.CompareTag("NavigationFinished"))
        {
            if ((other.GetComponent<MyCollider>().Triggered())) { experimentManager.LogCurrentCrossingTargets(crossings.CurrentCrossing().targetList); }
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

    public void SwitchStatus() { 
        isCurrentCrossing = !isCurrentCrossing;
        components.SetCurrentCrossing(isCurrentCrossing);
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

        crossing1.SwitchStatus(); crossing2.SwitchStatus();

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

        if (currentWaypoints[0].turn == TurnType.Left && currentWaypoints[1].turn == TurnType.Left) { SetNextCrossingTransform(crossing,-96f, -96f, 180); }
        if (currentWaypoints[0].turn == TurnType.Left && currentWaypoints[1].turn == TurnType.Right) { SetNextCrossingTransform(crossing,96f, -96f, 0); }
        if (currentWaypoints[0].turn == TurnType.Straight && currentWaypoints[1].turn == TurnType.Left) { SetNextCrossingTransform(crossing,96f, -96f, -90); }
        if (currentWaypoints[0].turn == TurnType.Straight && currentWaypoints[1].turn == TurnType.Right) { SetNextCrossingTransform(crossing,96f, 96f, 90); }
        if (currentWaypoints[0].turn == TurnType.Right && currentWaypoints[1].turn == TurnType.Left) { SetNextCrossingTransform(crossing,96f, 96f, 0); }
        if (currentWaypoints[0].turn == TurnType.Right && currentWaypoints[1].turn == TurnType.Right) { SetNextCrossingTransform(crossing,-96f, 96f, 180); }

        //Set turns 
        crossing.GetComponent<CrossComponents>().SetUpCrossing(nextWaypoints, settings, isCurrentCrossing);

    }

    void SetNextCrossingTransform(GameObject crossing, float forward, float right, int angle)
    {
/*
        Debug.Log($"currentCrossing.forward: {CurrentCrossing().obj.transform.forward}, CurrentCrossing().right: { CurrentCrossing().obj.transform.right}...");
        Debug.Log($"Transforming with {forward} forward and {right} right w.r.t. {CurrentCrossing().obj.transform.position}...");
*/
        Vector3 addition = right * CurrentCrossing().obj.transform.right + forward * CurrentCrossing().obj.transform.forward;
        crossing.transform.position = CurrentCrossing().obj.transform.position + addition;
        //Debug.Log($"Results: {crossing.transform.position}...");

        //Debug.Log($"Forward of next crossing before rotation: {crossing.transform.forward}...");
        crossing.transform.rotation = CurrentCrossing().obj.transform.rotation;
        crossing.transform.Rotate(crossing.transform.up, angle);
        //Debug.Log($"Forward of next crossing after rotation: {crossing.transform.forward}...");
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
        List<Target> targets = CurrentCrossing().targetList.Concat(NextCrossing().targetList).ToList();
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