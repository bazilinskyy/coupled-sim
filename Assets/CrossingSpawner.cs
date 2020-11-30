using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CrossingSpawner : MonoBehaviour
{
    // Start is called before the first frame update

    public TurnType[] turns;
    private int turnIndex = 0;
    public GameObject crossingPrefab;
    public GameObject startCross;

    public Crossings crossings;

    private bool finalCrossing;
    private bool isTriggered = false;
    private int lastIndex = 0;

    private ExperimentInput experimentInput;
    void Start()
    {
        experimentInput = MyUtils.GetExperimentInput();

        GameObject nextCrossing = Instantiate(crossingPrefab);

        crossings = new Crossings(startCross, nextCrossing);

        startCross.GetComponent<CrossComponents>().SetUpCrossing(turns[turnIndex], turns[turnIndex + 1]);
        
        ConfigureNextCrossing();

    }

    // Update is called once per frame
    void Update()
    {
        if (turnIndex + 1 >= turns.Count()) { finalCrossing = true; }

        if(lastIndex != turnIndex) { Debug.Log($"Next turn is {turns[turnIndex]}..."); lastIndex = turnIndex; }
    }

    void InstantiateCrossing()
    {
        GameObject newCrossing = Instantiate(crossingPrefab);
        crossings.SetNextCrossing(newCrossing);
        
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
            turnIndex++; isTriggered = true;
        }
        else if(other.gameObject.CompareTag("CorrectTurn"))
        {
            Debug.Log("Centre trigger called...");
            turnIndex++; isTriggered = true;
        }

        else if (other.gameObject.CompareTag("WrongTurn"))
        {
            Debug.Log("Wrong turn trigger called...");
            HandleWrongTurn();
            isTriggered = true;
        }
        
    }
    private void OnTriggerExit(Collider other)
    {
        string[] triggerTags = { "EnterCrossing", "CorrectTurn", "WrongTurn" };
        
        if (triggerTags.Contains(other.gameObject.tag)) { isTriggered = false; }
    }
    void HandleWrongTurn()
    {
        Debug.Log("TOOK WRONG TURN!!!!");
    }
    void ConfigureNextCrossing() 
    {

        CrossComponents nextCrossing = crossings.nextCrossing.GetComponent<CrossComponents>();
        nextCrossing.makeVirtualCable = experimentInput.makeVirtualCable;

        TurnType turn1 = turns[turnIndex];
        TurnType turn2 = turns[turnIndex+1];

        if (turn1 == TurnType.Left && turn2 == TurnType.Left) { SetNextCrossingTransform(-96f, -96f, 180); }
        if (turn1 == TurnType.Left && turn2 == TurnType.Right) { SetNextCrossingTransform(96f, -96f, 0); }
        if (turn1 == TurnType.Straight && turn2 == TurnType.Left) { SetNextCrossingTransform(192f, -96f, -90); }
        if (turn1 == TurnType.Straight && turn2 == TurnType.Right) { SetNextCrossingTransform(192f, 96f, 90); }
        if (turn1 == TurnType.Right && turn2 == TurnType.Left) { SetNextCrossingTransform(96f, 96f, 0); }
        if (turn1 == TurnType.Right && turn2 == TurnType.Right) { SetNextCrossingTransform(-96f, 96f, 180); }

        //Set turns 
        if (turnIndex + 3 < turns.Count()){ nextCrossing.SetUpCrossing(turns[turnIndex + 2], turns[turnIndex + 3]); }
        else if (turnIndex + 2 < turns.Count()) { nextCrossing.SetUpCrossing(turns[turnIndex + 2],TurnType.None); }

        //Make virtual cable if nescesrry here.
        
    }
    void SetNextCrossingTransform(float forward, float right, int angle)
    {
        Vector3 addition = right * crossings.currentCrossing.transform.right + forward * crossings.currentCrossing.transform.forward;

        crossings.nextCrossing.transform.position = crossings.currentCrossing.transform.position + addition;
        crossings.nextCrossing.transform.rotation = crossings.currentCrossing.transform.rotation;
        crossings.nextCrossing.transform.Rotate(Vector3.up, angle);
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
}