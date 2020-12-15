using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaitingRoomManager : MonoBehaviour
{
    public TextMesh text;

    public Transform startPosition;
    public GameObject steeringWheel;
    private GameObject player;
    public MainManager mainManager;
    public UnityEngine.UI.Image blackOutScreen;

    public float thresholdUserInput = 0.15f; //The minimum time between user inputs (when within this time only the first one is used)
    private List<Target> targetList;
    private float userInputTime = 0f; private readonly float userInputThresholdTime = 0.2f;
    private void Start()
    {
        Debug.Log("Loaded waiting room...");
        StartUp();
    }
    
    private void Update()
    {

        if (Input.GetKeyDown(mainManager.MyPermission)) { mainManager.LoadExperiment(); }
        
        //Looks for targets to appear in field of view and sets their visibility timer accordingly
        if (UserInput()) { ProcessUserInputTargetDetection(); }
    }
    void StartUp()
    {
        player = MyUtils.GetPlayer();
        mainManager = MyUtils.GetMainManager();

        mainManager.MovePlayer(startPosition);

        targetList = ActiveTargets();
        SetText();

        SpawnSteeringWheel();
    }
    public void SpawnSteeringWheel()
    {
        if (mainManager.CalibratedUsingHands)
        {
            steeringWheel.transform.position = startPosition.position;
            steeringWheel.transform.position += startPosition.transform.forward * mainManager.DriverViewZToSteeringWheel;
            steeringWheel.transform.position -= Vector3.up * mainManager.DriverViewYToSteeringWheel;
            steeringWheel.transform.position -= startPosition.transform.right * mainManager.DriverViewXToSteeringWheel;

        }
    }
    public Transform CameraTransform()
    {
        if (mainManager.camType == MyCameraType.Leap || mainManager.camType == MyCameraType.Varjo) { return player.transform.Find("VarjoCameraRig").Find("VarjoCamera"); }
        else if (mainManager.camType == MyCameraType.Normal) { return player.transform; }
        else { throw new System.Exception("Error in retrieving used camera transform in Experiment Manager.cs..."); }
    }
    private bool UserInput()
    {
        //only sends true once every 0.1 seconds (axis returns 1 for multiple frames when a button is clicked)
        if ((userInputTime + userInputThresholdTime) > Time.time) { return false; }
        if (Input.GetAxis(mainManager.ParticpantInputAxisLeft) == 1 || Input.GetAxis(mainManager.ParticpantInputAxisRight) == 1) { userInputTime = Time.time; return true; }
        else { return false; }
    }

    void SetText()
    {
        MainExperimentSetting settings = mainManager.GetExperimentSettings();
        if (!mainManager.IsNextExperiment()) { text.text = "All experiments are completed. Thanks for participating!"; }
        else { text.text = $"Experiment {mainManager.GetExperimentIndex()} starts when you are ready!\nNavigationType: {settings.navigationType}, Target difficulty: {settings.targetDifficulty}"; }
        
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
        //When multiple targets are visible we base our decision on:
        //(1) On which target has been looked at most recently
        //(2) Or closest target
        Target targetChosen = null;
        float mostRecentTime = 0f;

        foreach (Target target in targetList)
        {
            if (target.IsDetected()) { continue; }
            float timeSinceFixation = Time.time - target.lastFixationTime;
            if (timeSinceFixation < mostRecentTime)
            {
                targetChosen = target;
                mostRecentTime = timeSinceFixation;
            }
        }

        //If previous method did not find a target ->
        //(2) Check using angle between general gaze direction and target 
        float minAngle = 10f;
        if (targetChosen == null && mainManager.camType != MyCameraType.Normal)
        {

            Varjo.VarjoPlugin.GazeData data = Varjo.VarjoPlugin.GetGaze();
            if (data.status == Varjo.VarjoPlugin.GazeStatus.VALID)
            {

                Debug.Log("Got valid data...");
                Vector3 gazeDirection = MyUtils.TransformToWorldAxis(data.gaze.forward, data.gaze.position);

                foreach (Target target in targetList)
                {
                    if (target.IsDetected()) { continue; }
                    Vector3 CamToTarget = target.transform.position - CameraTransform().position;
                    float angle = Vector3.Angle(gazeDirection, CamToTarget);

                    if (angle < minAngle) { minAngle = angle; targetChosen = target; }
                }
            }
        }
        if (minAngle != 10f) { Debug.Log($"Chose {targetChosen.gameObject.name} with angle {minAngle}..."); }

        /*

                foreach (Target target in visibleTargets)
                {

                    //(1)
                    if (target.firstFixationTime > mostRecentTime)
                    {
                        targetChosen = target;
                        mostRecentTime = target.firstFixationTime;
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
                else { Debug.Log($"Chose target based on fixation time: {Time.time - mostRecentTime}..."); }*/

        if (targetChosen != null) { targetChosen.SetDetected(1f); }
    }
}
