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
    
    public TextMesh instructions;
    private Transform player;
    public UnityEngine.UI.Image blackOutScreen;
    private MySceneLoader mySceneLoader;
  
    private bool lastUserInput = false;

    private bool addedTargets;
    private bool unloadedEnvironmentScene = false;

    private float someTime= 0f;
    // Start is called before the first frame update
    private void Start()
    {
        StartScene();
    }
    void StartScene()
    {
        player = MyUtils.GetPlayer().transform;
        experimentInput = MyUtils.GetExperimentInput();

        //Spawn steeringwheel beneath plane.
        steeringWheel.transform.rotation = startPosition.rotation;
        steeringWheel.transform.position = -Vector3.up * 1;

        Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL);

        mySceneLoader = player.GetComponent<MySceneLoader>();
        
        mySceneLoader.MovePlayer(startPosition);
        /*
                experimentInput.environment = GameObject.FindGameObjectWithTag("Environment");

        */
        experimentInput.environment.SetActive(false);

        if (!experimentInput.ReadCSVSettingsFile()) 
        {
            instructions.text = "Error in reading the experimentSettings file....\nPlease tell Marc :)";
        }
    }
    private void LateUpdate()
    {
        //In late update because we firt want to process eyetracking data
        bool userInput = UserInput();

        //Looks for targets to appear in field of view and sets their visibility timer accordingly
        if (userInput && addedTargets) { ProcessUserInputTargetDetection(); }
        if ((userInput && !addedTargets) || Input.GetKeyDown(experimentInput.spawnSteeringWheel)) { CalibrateHands(); }
    }
    void Update()
    {
       
        if (Input.GetKeyDown(experimentInput.resetExperiment)) {mySceneLoader.LoadCalibrationScene(); }
        if (Input.GetKeyDown(experimentInput.resetHeadPosition)) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }
        if (Input.GetKeyDown(experimentInput.calibrateGaze)) { RequestGazeCalibration(); }
        if (Input.GetKeyDown(experimentInput.myPermission)) {
            if (!addedTargets)
            {
                mySceneLoader.AddTargetScene(); addedTargets = true;
            }
            mySceneLoader.LoadNextDrivingScene(true,false); 
        }
        /*if (Varjo.VarjoPlugin.IsGazeCalibrated() && (experimentInput.camType == MyCameraType.Normal || experimentInput.calibratedUsingHands) && !addedTargets) {
            mySceneLoader.AddTargetScene(); 
            addedTargets = true; cross.SetActive(false);
            instructions.text = "Look at the targets above!";
        }*/
        
        if (Input.GetKeyDown(KeyCode.T)) { 
            mySceneLoader.AddTargetScene(); 
            addedTargets = true; 
            cross.SetActive(false);
            instructions.text = "Look at the targets above!";
        }

        /*if(experimentInput.environment != null && !unloadedEnvironmentScene) 
        {
            //DontDestroyOnLoad(experimentInput.environment);
            //SceneManager.UnloadSceneAsync("Environment");
            unloadedEnvironmentScene = true;
        }*/
    }
    void RequestGazeCalibration()
    {

        Varjo.VarjoPlugin.GazeCalibrationParameters[] parameters = new Varjo.VarjoPlugin.GazeCalibrationParameters[2];

        parameters[0] = new Varjo.VarjoPlugin.GazeCalibrationParameters();
        parameters[0].key = "GazeCalibrationType";
        parameters[0].value = "Fast"; //"Legacy"

        parameters[1] = new Varjo.VarjoPlugin.GazeCalibrationParameters();
        parameters[1].key = "OutputFilterType";
        parameters[1].value = "Standard";

        Varjo.VarjoPlugin.RequestGazeCalibrationWithParameters(parameters);

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
        //(2) Target closest to looking direction
        //(3) Or closest target
   /*     Target targetChosen = null;
        float mostRecentTime = 0f;
        float smallestDistance = 100000f;
        float currentDistance;*/


        //When multiple targets are visible we base our decision on:
        //(1) On which target has been looked at most recently (max 1 second)
        Target targetChosen = null;
        float mostRecentTime = 1f;
        foreach (Target target in visibleTargets)
        {
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
        if (targetChosen == null && experimentInput.camType != MyCameraType.Normal)
        {

            Varjo.VarjoPlugin.GazeData data = Varjo.VarjoPlugin.GetGaze();
            if (data.status == Varjo.VarjoPlugin.GazeStatus.VALID)
            {

                Debug.Log("Got valid data...");
                Vector3 gazeDirection = MyUtils.TransformToWorldAxis(data.gaze.forward, data.gaze.position);

                foreach (Target target in visibleTargets)
                {
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
    public Transform CameraTransform()
    {
        if (experimentInput.camType == MyCameraType.Leap) { return player.Find("VarjoCameraRig").Find("VarjoCamera"); }
        else if(experimentInput.camType == MyCameraType.Varjo) { return player.Find("VarjoCamera"); }
        else if (experimentInput.camType == MyCameraType.Normal) { return player; }
        else { throw new System.Exception("Error in retrieving used camera transform in Experiment Manager.cs..."); }
    }
}
