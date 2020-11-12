using Leap.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class CalibrationManager : MonoBehaviour
{
    
    public GameObject steeringWheel;
    public GameObject cross;
    public Transform startPosition;
    private ExperimentInput experimentInput;
    
    private MyCameraType camType;
    public TextMesh instructions;
    private Transform player;
    public UnityEngine.UI.Image blackOutScreen;
    private MySceneLoader mySceneLoader;

    public KeyCode myPermission = KeyCode.F1;
    public KeyCode resetHeadPosition = KeyCode.F2;
    public KeyCode spawnSteeringWheel = KeyCode.F3;
    public KeyCode calibrateGaze = KeyCode.F4;
    public KeyCode resetExperiment = KeyCode.Escape;

    public KeyCode keyToggleDriving = KeyCode.Space;

    public KeyCode keyToggleSymbology = KeyCode.Tab;

    public KeyCode setToLastWaypoint = KeyCode.R;
    public KeyCode inputNameKey = KeyCode.Y;

    public KeyCode saveTheData = KeyCode.F7;

    private bool lastUserInput = false;

    private readonly int maxNumberOfRandomRayHits = 40;
    private bool addedTargets;
    
    // Start is called before the first frame update
    private void Start()
    {
        StartScene();
    }
    void StartScene()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        experimentInput = player.GetComponent<ExperimentInput>();

        //Spawn steeringwheel beneath plane.
        steeringWheel.transform.rotation = startPosition.rotation;
        steeringWheel.transform.position = -Vector3.up * 1;

        Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL);
        GetVariablesFromSceneManager();

        if (!experimentInput.ReadCSVSettingsFile()) 
        {
            instructions.text = "Error in reading the experimentSettings file....\nPlease tell Marc :)";
        }

    }
    
    void Update()
    {
        bool userInput = UserInput();

        //Looks for targets to appear in field of view and sets their visibility timer accordingly
        if ( userInput && addedTargets) { ProcessUserInputTargetDetection(); }
        if (Input.GetKeyDown(resetExperiment)) {mySceneLoader.LoadCalibrationScene(); }
        if (Input.GetKeyDown(resetHeadPosition)) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }
        if (Input.GetKeyDown(calibrateGaze)) { Varjo.VarjoPlugin.RequestGazeCalibration(); }
        if (Input.GetKeyDown(myPermission)) { mySceneLoader.LoadNextScene(); }
        if (Varjo.VarjoPlugin.IsGazeCalibrated() && !addedTargets) { 
            GetComponent<MySceneLoader>().AddTargetScene(); 
            addedTargets = true; cross.SetActive(false);
            instructions.text = "Look at the targets above!";
        }
        if ((userInput && !addedTargets ) || Input.GetKeyDown(spawnSteeringWheel))  { CalibrateHands(); }
    }
    void CalibrateHands()
    {
        if (experimentInput.camType == MyCameraType.Leap) {
            Debug.Log("Trying to calibrate...");            
            CalibrateUsingHands steeringWheelCalibration = startPosition.GetComponent<CalibrateUsingHands>();

            steeringWheelCalibration.driverView = startPosition;
            steeringWheelCalibration.leftHand = player.Find("Hand Models").Find("Hand_Left").GetComponent<RiggedHand>();
            steeringWheelCalibration.rightHand = player.Find("Hand Models").Find("Hand_Right").GetComponent<RiggedHand>();
            steeringWheelCalibration.steeringWheel = steeringWheel.transform;

            
            
            bool success = steeringWheelCalibration.SetPositionUsingHands();
            if (success) 
            {
                float horizontalDistance = Mathf.Abs(startPosition.position.z - steeringWheel.transform.position.z);
                float verticalDistance = Mathf.Abs(startPosition.position.y - steeringWheel.transform.position.y);
                float sideDistance = startPosition.position.x - steeringWheel.transform.position.x;
                experimentInput.SetCalibrationDistances(horizontalDistance, verticalDistance, sideDistance);
                Debug.Log($"Calibrated steeringhweel with horizontal and vertical distances of, {horizontalDistance} and {verticalDistance}, respectively...");
            }
        }
    }
    private bool UserInput()
    {
        bool input = (Input.GetAxis(experimentInput.ParticpantInputAxisLeft) == 1 || Input.GetAxis(experimentInput.ParticpantInputAxisRight) == 1);
        if (!input) {  lastUserInput = false; return false; }
        else if (input && lastUserInput) {  lastUserInput = true; return false; }
        else { lastUserInput = true; return true; }
    }

    void GetVariablesFromSceneManager()
    {
        mySceneLoader = GetComponent<MySceneLoader>(); 
        camType = experimentInput.camType;
        
        myPermission = experimentInput.myPermission;
        resetHeadPosition = experimentInput.resetHeadPosition;
        spawnSteeringWheel = experimentInput.spawnSteeringWheel;
        calibrateGaze = experimentInput.calibrateGaze;
        
        resetExperiment = experimentInput.resetExperiment;
        
        keyToggleDriving = experimentInput.toggleDriving;
        keyToggleSymbology = experimentInput.toggleSymbology;

        setToLastWaypoint = experimentInput.setToLastWaypoint;

        inputNameKey = experimentInput.inputNameKey;
        saveTheData = experimentInput.saveTheData;

    }
    List<Target> ActiveTargets()
    {
        List<Target> targetList = new List<Target>();
        GameObject[] objects = GameObject.FindGameObjectsWithTag("Target");
        foreach (GameObject obj in objects) {
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
        else { Debug.Log($"Chose target based on fixation time: {Time.time - mostRecentTime}..."); }

        if (targetChosen != null) { targetChosen.SetDetected(1f); }
    }
    public Transform CameraTransform()
    {
        if (camType == MyCameraType.Leap) { return player.Find("VarjoCameraRig").Find("VarjoCamera"); }
        else if(camType == MyCameraType.Varjo) { return player.Find("VarjoCamera"); }
        else if (camType == MyCameraType.Normal) { return player; }
        else { throw new System.Exception("Error in retrieving used camera transform in Experiment Manager.cs..."); }
    }
}
