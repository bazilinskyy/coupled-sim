using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaitingRoomManager : MonoBehaviour
{
    private MyCameraType camType;
    private MySceneLoader mySceneLoader;

    public TextMesh text;

    public Transform startPosition;
    public GameObject steeringWheel;
    private Transform player;
    public ExperimentInput experimentInput;
    public UnityEngine.UI.Image blackOutScreen;

    public float thresholdUserInput = 0.15f; //The minimum time between user inputs (when within this time only the first one is used)

    private float userInputTime = 0f; private readonly float userInputThresholdTime = 0.2f;
    private void Start()
    {
        Debug.Log("Loaded waiting room...");
        StartingScene();
    }
    
    private void Update()
    {

        if (Input.GetKeyDown(experimentInput.myPermission)) { mySceneLoader.LoadNextDrivingScene(false,true); }
        
        //Looks for targets to appear in field of view and sets their visibility timer accordingly
        if (UserInput()) { ProcessUserInputTargetDetection(); }
    }
    void StartingScene()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        experimentInput = player.GetComponent<ExperimentInput>();        

        mySceneLoader = player.GetComponent<MySceneLoader>();
        mySceneLoader.MovePlayer(startPosition);

        SetText();

        SpawnSteeringWheel();
    }
    public void SpawnSteeringWheel()
    {
        if (experimentInput.calibratedUsingHands)
        {
            steeringWheel.transform.position = startPosition.position;
            steeringWheel.transform.position += startPosition.transform.forward * experimentInput.driverViewHorizontalDistance;
            steeringWheel.transform.position -= Vector3.up * experimentInput.driverViewVerticalDistance;
            steeringWheel.transform.position -= startPosition.transform.right * experimentInput.driverViewSideDistance;

        }
    }
    public Transform CameraTransform()
    {
        if (camType == MyCameraType.Leap || camType == MyCameraType.Varjo) { return player.Find("VarjoCameraRig").Find("VarjoCamera"); }
        else if (camType == MyCameraType.Normal) { return player; }
        else { throw new System.Exception("Error in retrieving used camera transform in Experiment Manager.cs..."); }
    }
    private bool UserInput()
    {
        //only sends true once every 0.1 seconds (axis returns 1 for multiple frames when a button is clicked)
        if ((userInputTime + userInputThresholdTime) > Time.time) { return false; }
        if (Input.GetAxis(experimentInput.ParticpantInputAxisLeft) == 1 || Input.GetAxis(experimentInput.ParticpantInputAxisRight) == 1) { userInputTime = Time.time; return true; }
        else { return false; }
    }

    void SetText()
    {
        if (!experimentInput.IsNextScene()) { text.text = "All experiments are completed. Thanks for participating!"; }
        
        text.text = $"Experiment {experimentInput.GetExperimentNumber()} starts when you are ready!";
        
    }

    List<Target> ActiveTargets()
    {
        List<Target> targetList = new List<Target>();
        GameObject[] objects = GameObject.FindGameObjectsWithTag("Target");
        foreach (GameObject obj in objects)
        {
            Target target = obj.GetComponent<Target>();
            if (!target.IsDetected()) { targetList.Add(target); }
        }
        return targetList;
    }
    void ProcessUserInputTargetDetection()
    {
        //if there is a target visible which has not already been detected   
        List<Target> visibleTargets = ActiveTargets();


        //When multiple targets are visible we base our decision on:
        //(1) On which target has been looked at most recently
        //(2) Or closest target
        Target targetChosen = null;
        float mostRecentTime = 0f;
        float smallestDistance = 100000f;
        float currentDistance;

        foreach (Target target in visibleTargets)
        {
            //(1)
            if (target.lastFixationTime > mostRecentTime)
            {
                targetChosen = target;
                mostRecentTime = target.lastFixationTime;
            }
            //(2) Stops this when mostRecentTime variables gets set to something else then 0
            currentDistance = Vector3.Distance(CameraTransform().position, target.transform.position);
            if (currentDistance < smallestDistance && mostRecentTime == 0f)
            {
                targetChosen = target;
                smallestDistance = currentDistance;
            }
        }
        if (mostRecentTime == 0f) { Debug.Log("Chose target based on distance..."); }
        else { Debug.Log($"Chose target based on fixation time: {Time.time - mostRecentTime}..."); }

        if (targetChosen != null) { targetChosen.SetDetected(1f); }
    }
}
