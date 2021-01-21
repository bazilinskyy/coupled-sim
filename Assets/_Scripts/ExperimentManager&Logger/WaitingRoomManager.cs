using Leap.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaitingRoomManager : MonoBehaviour
{
    public TMPro.TextMeshPro text;

    public Transform startPosition;
    public GameObject steeringWheel;
    private GameObject player;
    public MainManager mainManager;
    public UnityEngine.UI.Image blackOutScreen;

    public float thresholdUserInput = 0.15f; //The minimum time between user inputs (when within this time only the first one is used)
    private List<Target> targetList;
    
    private bool lastUserInput = false;
    private bool reCalibrateHands = false;

    public TMPro.TextMeshPro gazeQuality;
    private void Start()
    {
        Debug.Log("Loaded waiting room...");
        StartUp();
    }
    
    private void Update()
    {

        if (Input.GetKeyDown(mainManager.MyPermission)) { mainManager.LoadExperiment(); }
        if (Input.GetKeyDown(mainManager.CalibrateGaze)) { RequestGazeCalibration(); }
        if (Input.GetKeyDown(mainManager.ResetHeadPosition)) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }
        if (Input.GetKeyDown(mainManager.ToggleReCalibrateHands)) { reCalibrateHands = !reCalibrateHands; Debug.Log($"Recalbirating hands = {reCalibrateHands}..."); }

        if(Input.GetKeyDown(mainManager.ReRunPractiseDrive)) { mainManager.LoadPractiseDrive(); }

        bool userInput = UserInput();
        //Looks for targets to appear in field of view and sets their visibility timer accordingly
        if (userInput && !reCalibrateHands) { ProcessUserInputTargetDetection(); }

        if (userInput && reCalibrateHands) { CalibrateHands(); }

        if (Varjo.VarjoPlugin.IsGazeCalibrated())
        {
            Varjo.VarjoPlugin.GazeEyeCalibrationQuality left = Varjo.VarjoPlugin.GetGazeCalibrationQuality().left;
            Varjo.VarjoPlugin.GazeEyeCalibrationQuality right = Varjo.VarjoPlugin.GetGazeCalibrationQuality().right;
            gazeQuality.text = $"Calibration Quality:\nLeft:{left} - Right:{right}";
        }

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
    void CalibrateHands()
    {
        if (mainManager.camType == MyCameraType.Leap)
        {
            Transform player = MyUtils.GetPlayer().transform;
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
                mainManager.SetCalibrationDistances(horizontalDistance, verticalDistance, sideDistance);
                Debug.Log($"Calibrated steeringhweel with horizontal and vertical distances of, {horizontalDistance} and {verticalDistance}, respectively...");
            }
        }
    }

    private bool UserInput()
    {
        bool input = (Input.GetAxis(mainManager.ParticpantInputAxisLeft) == 1 || Input.GetAxis(mainManager.ParticpantInputAxisRight) == 1);

        if (!input) { lastUserInput = false; return false; }
        else if (input && lastUserInput) { lastUserInput = true; return false; }
        else { lastUserInput = true; return true; }
    }
    void RequestGazeCalibration()
    {

        Varjo.VarjoPlugin.GazeCalibrationParameters[] parameters = new Varjo.VarjoPlugin.GazeCalibrationParameters[2];

        parameters[0] = new Varjo.VarjoPlugin.GazeCalibrationParameters();
        parameters[0].key = "GazeCalibrationType";
        parameters[0].value = "Legacy"; //"Fast"; 

        parameters[1] = new Varjo.VarjoPlugin.GazeCalibrationParameters();
        parameters[1].key = "OutputFilterType";
        parameters[1].value = "Standard";

        Varjo.VarjoPlugin.RequestGazeCalibrationWithParameters(parameters);

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

    void SetText()
    {
        /*if (!mainManager.IsNextExperiment()) { text.text = "All experiments are completed. Thanks for participating!"; }
        else
        {
            MainExperimentSetting settings = mainManager.GetExperimentSettings();

            string navigationType = "";

            if (settings.navigationType == NavigationType.HUD_low) { navigationType = "low HUD"; }
            if (settings.navigationType == NavigationType.HUD_high) { navigationType = "high HUD"; }
            if (settings.navigationType == NavigationType.VirtualCable) { navigationType = "Virtual cable"; }

            text.text = $"Experiment {mainManager.GetExperimentIndex()} starts when you are ready!\nNavigationType: {navigationType}"; 
        }*/

        List<int> score = mainManager.GetSubjectScore();
        text.text = $"You got {score[1]}/{score[0]} targets!";

        if (!mainManager.IsNextExperiment()) 
        {
            score = mainManager.GetTotalSubjectScore();
            text.text += $"\n\nYour total score is: { score[1]}/{ score[0]}!\nThanks for participating <3"; 
        }


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
