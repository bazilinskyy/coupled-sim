using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CrossingSpawner : MonoBehaviour
{
    // Start is called before the first frame update

    public TurnType[] turnsList;
    private int turnIndex = 0;

    public GameObject cross1;
    public GameObject cross2;

    public Crossings crossings;
    public newExperimentManager experimentManager;
    public void StartUp()
    {
        if(turnsList.Length < 4) { Debug.LogError("Set atleast 4 turnsList...."); enabled = false; return; }

        TurnType[] turns1 = { turnsList[0], turnsList[1] };
        TurnType[] turns2 = { turnsList[2], turnsList[3] };
        turnIndex = 4;

        crossings = new Crossings(cross1, turns1, cross2, turns2, experimentManager.experimentSettings);

        GetComponent<CreateVirtualCable>().MakeVirtualCable(crossings, experimentManager.makeVirtualCable);
    }


    public void SetNextCrossing()
    {
        if (NextTurns())
        {
            crossings.SetNextCrossing(GetNextTurns(), experimentManager.experimentSettings);
            turnIndex += 2;
            //Make virtual cable if nescesrry here.
            GetComponent<CreateVirtualCable>().MakeVirtualCable(crossings, experimentManager.makeVirtualCable);
        }

    }
    bool NextTurns()
    {
        if(turnIndex >= turnsList.Count()) { return false; }
        else { return true; }
    }
    TurnType[] GetNextTurns()
    {
        TurnType[] turns = new TurnType[2];
        turns[0] = TurnType.None;  turns[1] = TurnType.None;
        if (turnIndex >= turnsList.Count()) { return turns; }
        if (turnIndex + 1 >= turnsList.Count()) { turns[0] = turnsList[turnIndex];  return turns; }
        else { turns[0] = turnsList[turnIndex]; turns[1] = turnsList[turnIndex+1]; return turns; }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("EnterCrossing"))
        {
            if (other.GetComponent<MyCollider>().Triggered())
            {
                Debug.Log("Enter trigger called, configuring next crossing...");
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

        targetList = components.targetList;

        turns[0] = components.turn1;
        turns[1] = components.turn2;

        waypoints[0].waypoint = components.waypoints.waypoint1;
        waypoints[0].turn = components.turn1;

        waypoints[1].waypoint = components.waypoints.waypoint2;
        waypoints[1].turn = components.turn2;
    }

   public void UpdateVariables()
    {
        targetList = components.targetList;

        turns[0] = components.turn1;
        turns[1] = components.turn2;

        waypoints[0].waypoint = components.waypoints.waypoint1;
        waypoints[0].turn = components.turn1;

        waypoints[1].waypoint = components.waypoints.waypoint2;
        waypoints[1].turn = components.turn2;
    }

    public void SwitchStatus() { isCurrentCrossing = !isCurrentCrossing; }
}
[System.Serializable]
public class Crossings
{
    public Crossing crossing1 = null;
    public Crossing crossing2 = null;

    public Crossings(GameObject _crossing1, TurnType[] turns1, GameObject _crossing2, TurnType[] turns2, MainExperimentSetting settings)
    {

        bool isFirstCrossing = true;

        _crossing1.GetComponent<CrossComponents>().SetUpCrossing(turns1[0], turns1[1], settings, isFirstCrossing);
        crossing1 = new Crossing(_crossing1, true);

        ConfigureNextCrossing(_crossing2, turns1, turns2, settings);;
        crossing2 = new Crossing(_crossing2, false);
    }
    public void SetNextCrossing(TurnType[] nextTurns, MainExperimentSetting settings)
    {
        Crossing crossToConfigure = null;

        if (crossing1.isCurrentCrossing) { crossToConfigure = crossing1; }
        else { crossToConfigure = crossing2; }

        crossing1.SwitchStatus(); crossing2.SwitchStatus();

        ConfigureNextCrossing(crossToConfigure.obj, CurrentCrossing().turns, nextTurns, settings);
        
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
    void ConfigureNextCrossing(GameObject crossing, TurnType[] currentTurns,  TurnType[] nextTurns, MainExperimentSetting settings)
    {
        Debug.Log("Configuring next crossing..");
        Debug.Log($"Got turns {nextTurns[0]} and {nextTurns[1]}...");

        if (currentTurns[0] == TurnType.Left && currentTurns[1] == TurnType.Left) { SetNextCrossingTransform(crossing,-96f, -96f, 180); }
        if (currentTurns[0] == TurnType.Left && currentTurns[1] == TurnType.Right) { SetNextCrossingTransform(crossing,96f, -96f, 0); }
        if (currentTurns[0] == TurnType.Straight && currentTurns[1] == TurnType.Left) { SetNextCrossingTransform(crossing,96f, -96f, -90); }
        if (currentTurns[0] == TurnType.Straight && currentTurns[1] == TurnType.Right) { SetNextCrossingTransform(crossing,96f, 96f, 90); }
        if (currentTurns[0] == TurnType.Right && currentTurns[1] == TurnType.Left) { SetNextCrossingTransform(crossing,96f, 96f, 0); }
        if (currentTurns[0] == TurnType.Right && currentTurns[1] == TurnType.Right) { SetNextCrossingTransform(crossing,-96f, 96f, 180); }

        //Set turns 
        crossing.GetComponent<CrossComponents>().SetUpCrossing(nextTurns[0], nextTurns[1], settings);

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
}