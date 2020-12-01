using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CrossingSpawner : MonoBehaviour
{
    // Start is called before the first frame update

    public TurnType[] turnsList;
    private int turnIndex = 0;
    public GameObject crossingPrefab;
    public GameObject startCross;

    public Crossings crossings;

    private bool finalCrossing;
    private bool isTriggered = false; private float timeOutTime = 2f;
    private float triggerTime = 0f;
    private int lastIndex = 0;

    public newExperimentManager experimentManager;
    void Awake()
    {
        if(turnsList.Length < 4) { Debug.LogError("Set atleast 4 turnsList...."); enabled = false; return; }

        GameObject nextCrossing = Instantiate(crossingPrefab);

        crossings = new Crossings(startCross, nextCrossing);

        startCross.GetComponent<CrossComponents>().SetUpCrossing(turnsList[turnIndex], turnsList[turnIndex + 1]);
        
        ConfigureNextCrossing();
    }

    // Update is called once per frame
    void Update()
    {
        if (turnIndex + 1 >= turnsList.Count()) { finalCrossing = true; }

        if(lastIndex != turnIndex) { Debug.Log($"Next turn is {turnsList[turnIndex]}..."); lastIndex = turnIndex; }

        ResetTriggerBoolean();
    }

    void InstantiateCrossing()
    {
        GameObject newCrossing = Instantiate(crossingPrefab);
        crossings.SetNextCrossing(newCrossing);
        
    }

    
    void ResetTriggerBoolean()
    {
        if (!isTriggered) { return; }
        if ((triggerTime + timeOutTime) < Time.time) { isTriggered = false; }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (finalCrossing) { return; }
        if (isTriggered) { return; }

        if(other.gameObject.CompareTag("EnterCrossing"))
        {
            Debug.Log("Enter trigger called, deleting last crossing...");
            InstantiateCrossing();
            ConfigureNextCrossing();
            
            turnIndex++; isTriggered = true; triggerTime = Time.time;
        }
        else if(other.gameObject.CompareTag("CorrectTurn"))
        {
            Debug.Log("Centre trigger called...");
            turnIndex++; isTriggered = true; triggerTime = Time.time;
        }

        else if (other.gameObject.CompareTag("WrongTurn"))
        {
            Debug.Log("Wrong turn trigger called...");
            isTriggered = true; triggerTime = Time.time;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        string[] triggerTags = { "EnterCrossing", "CorrectTurn", "WrongTurn" };
        
        if (triggerTags.Contains(other.gameObject.tag)) { isTriggered = false; }
    }
    
    void ConfigureNextCrossing() 
    {
        CrossComponents nextCrossing = crossings.nextCrossing.GetComponent<CrossComponents>();

        TurnType[] turns = crossings.GetTurns("current");

        if (turns[0] == TurnType.Left && turns[1] == TurnType.Left) { SetNextCrossingTransform(-96f, -96f, 180); }
        if (turns[0] == TurnType.Left && turns[1] == TurnType.Right) { SetNextCrossingTransform(96f, -96f, 0); }
        if (turns[0] == TurnType.Straight && turns[1] == TurnType.Left) { SetNextCrossingTransform(96f, -96f, -90); }
        if (turns[0] == TurnType.Straight && turns[1] == TurnType.Right) { SetNextCrossingTransform(96f, 96f, 90); }
        if (turns[0] == TurnType.Right && turns[1] == TurnType.Left) { SetNextCrossingTransform(96f, 96f, 0); }
        if (turns[0] == TurnType.Right && turns[1] == TurnType.Right) { SetNextCrossingTransform(-96f, 96f, 180); }

        //Set turns 
        if (turnIndex + 3 < turnsList.Count()){ nextCrossing.SetUpCrossing(turnsList[turnIndex + 2], turnsList[turnIndex + 3]); }
        else if (turnIndex + 2 < turnsList.Count()) { nextCrossing.SetUpCrossing(turnsList[turnIndex + 2], TurnType.None); }

        //Make virtual cable if nescesrry here.
        if (experimentManager.makeVirtualCable) { GetComponent<CreateVirtualCable>().MakeVirtualCable(crossings); }
        
    }
    void SetNextCrossingTransform(float forward, float right, int angle)
    {

        Debug.Log($"currentCrossing.forward: {crossings.currentCrossing.transform.forward}, currentCrossing.right: { crossings.currentCrossing.transform.right}...");
        Debug.Log($"Transforming with {forward} forward and {right} right w.r.t. {crossings.currentCrossing.transform.position}...");

        Vector3 addition = right * crossings.currentCrossing.transform.right + forward * crossings.currentCrossing.transform.forward;

        crossings.nextCrossing.transform.position = crossings.currentCrossing.transform.position + addition;
        Debug.Log($"Results: {crossings.nextCrossing.transform.position}...");

        Debug.Log($"Forward of next crossing before rotation: {crossings.nextCrossing.transform.forward}...");
        crossings.nextCrossing.transform.rotation = crossings.currentCrossing.transform.rotation;
        crossings.nextCrossing.transform.Rotate(crossings.nextCrossing.transform.up, angle);
        Debug.Log($"Forward of next crossing after rotation: {crossings.nextCrossing.transform.forward}...");
    }
}
public enum TurnType
{
    Left,
    Right,
    Straight,
    None,
    EndPoint
}
[System.Serializable]
public class Crossings
{
    public GameObject currentCrossing = null;
    public GameObject nextCrossing = null;

    public Crossings(GameObject crossing1, GameObject crossing2)
    {
        currentCrossing = crossing1;
        nextCrossing = crossing2;
    }
    public void SetNextCrossing(GameObject newCrossing)
    {
        if (nextCrossing != null)
        {
            currentCrossing.SetActive(false);

            currentCrossing = nextCrossing;
            nextCrossing = newCrossing;
        }
        else { nextCrossing =newCrossing; }
    }

    public TurnType[] GetTurns(string crossing)
    {
        GameObject crossingObj = null;
        if(crossing == "current") { crossingObj = currentCrossing; }
        else if (crossing == "next") { crossingObj = nextCrossing; }
        else { Debug.LogError("Wrong input for GetTurns...."); return null; }
        
        CrossComponents components = crossingObj.GetComponent<CrossComponents>();

        TurnType[] array = { components.turn1, components.turn2 };
        return array;
    }

    public TargetStruct[] GetTargets(string crossing)
    {
        GameObject crossingObj = null;
        if (crossing == "current" || crossing == "Current") { crossingObj = currentCrossing; }
        else if (crossing == "next" || crossing == "Next") { crossingObj = nextCrossing; }
        else { Debug.LogError("Wrong input for GetTurns...."); return null; }

        CrossComponents components = crossingObj.GetComponent<CrossComponents>();

        TargetStruct[] targets = new TargetStruct[2];

        targets[0].target = components.waypoints.waypoint1;
        targets[0].turn = components.turn1;

        targets[1].target = components.waypoints.waypoint2;
        targets[1].turn = components.turn2;
        return targets;
    }
   
}

[System.Serializable]
public struct TargetStruct
{
    public Transform target;
    public TurnType turn;
}