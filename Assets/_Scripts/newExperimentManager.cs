using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;

[RequireComponent(typeof(DataLogger), typeof(MyGazeLogger),typeof(MyVarjoGazeRay))]
public class newExperimentManager : MonoBehaviour
{
    
    [Header("Experiment Input")]
    public string subjectName;
    public Color navigationColor;
   
    public MainManager mainManager;
    public CrossingSpawner crossingSpawner;
    public Material easyMaterial;
    [Header("GameObjects")]
    public Material conformal;
    //public HUDMaterials HUDMaterials;
    public LayerMask layerToIgnoreForTargetDetection;

    public newNavigator car;

    //Mirror cameras from car
    private Camera rearViewMirror;
    private Camera leftMirror;
    private Camera rightMirror;

    //The camera used and head position inside the car
    [HideInInspector]
    public Transform driverView;
    private Transform player;
    private Transform steeringWheel;
    private Transform startPoint;
    private bool lastUserInput = false;
    //The data manger handling the saving of vehicle and target detection data Should be added to the experiment manager object 
    public DataLogger dataManager;
    //Maximum raycasts used in determining visbility:  We use Physics.RayCast to check if we can see the target. We cast this to a random positin on the targets edge to see if it is partly visible.
    public float thresholdUserInput = 0.15f; //The minimum time between user inputs (when within this time only the first one is used)

    private bool savedData = false;
    public MainExperimentSetting experimentSettings;

    private int targetCount = 0;
    UnityEngine.UI.Image blackOutScreen;
    UnityEngine.TextMesh carUI;
    public  bool setCarAtTarget = false;
    private int lastWaypointIndex = 0;
    private bool informedOnHardTargets = false;
    private bool informedOnEasyTargets = false;
    private bool startedDriving = false;


    private int targetCountCalibration=0;
    private float transparencyTargets = 0.1f;

    private float angleTreshold = 1.5f;
    private float maxAngle = 2f;

    private bool firstStraight = true;
    private int transparencyIndex = 0;
    private int sizeIndex = 0;

    //Transparencies and sizes for the target calibration experiment
    private float[] transparencies = { .1f, .05f, .02f, .01f, .005f};
    private float[] sizes = { 1f, 0.75f, .5f }; //

    public int leftTargets = 0;
    public int rightTargets = 0;

    private int navigationTypeIndex = 1;
    private void Start()
    {
        player = MyUtils.GetPlayer().transform;
        blackOutScreen = MyUtils.GetBlackOutScreen();
        mainManager = MyUtils.GetMainManager();
        carUI = MyUtils.GetCarUI();
        
        //experiment settings
        experimentSettings = mainManager.GetExperimentSettings();

        //Turn off eye visuals
        if (player.GetComponent<EyeTrackingVisuals>() != null) { player.GetComponent<EyeTrackingVisuals>().Disable(); }
        //Set DataManager
        SetDataManager();

        if (experimentSettings.experimentType.IsTargetCalibration()) { transparencyTargets = 0.2f; experimentSettings.targetSize = 1f; SetTransparencyEasyMaterial(transparencyTargets); }
        else { SetTransparencyEasyMaterial(experimentSettings.transparency); }

        crossingSpawner.turnsList = experimentSettings.turns.ToArray();
        crossingSpawner.StartUp();

        //If target calibration is active we do not want to use te entering trigger of the crossing and set the startpoint a bit a back. 
        //The entering trigger makes sure logging of the targets and setup of next crossing is performed. Both will be taken care of  by differnet functions when using targetCalibration ExperimentType
        if (experimentSettings.experimentType.IsTargetCalibration())
        {
            crossingSpawner.crossings.CurrentCrossing().components.triggerStart.SetActive(false);
            crossingSpawner.crossings.CurrentCrossing().components.triggerEnd.SetActive(true);
            //Remove targets of next crossing (they might confuse participnats);
            crossingSpawner.crossings.NextCrossing().components.RemoveTargets();
            startPoint = crossingSpawner.crossings.CurrentCrossing().components.startPointCalibration.transform;

            car.HUD.SetActive(false);
        }
        else { startPoint = crossingSpawner.crossings.CurrentCrossing().components.startPoint.transform; }
        //Get all gameobjects we intend to use from the car (and do some setting up)
        SetGameObjectsFromCar();

        mainManager.MovePlayer(driverView);

        //Set camera (uses the gameobjects set it SetGameObjectsFromCar()) 
        SetCamera();

        SetCarControlInput();
        //Set up car
        SetUpCar();

        GoToCar();
        //Activate mirror cameras (When working with the varjo it deactivates all other cameras....)
        //Does not work when in Start() or in Awake()...
        ActivateMirrorCameras();
    }
    private void LateUpdate()
    {
        //We dot this as late update to make sure gaze data (which is used to determine which target the participant is detecting) is processed 
        //before this code is executed

        //This is ina boolean so that if multiple things depend on userinput it stays true 
        //(calling the function userInput() multiple times leads to complications as it prevents double tapping i.e., multiple input signals within a short time frame
        bool userInput = UserInput();
        
        //Target detection processing (preventing inputs when the drive is finished or not started)
        if (userInput && !car.navigationFinished && startedDriving) { ProcessUserInputTargetDetection(); }
        
    }
    void Update()
    {
        experimentSettings.experimentTime += Time.deltaTime;
        //Looks for targets to appear in field of view and sets their visibility timer accordingly
        if (startedDriving) { SetTargetVisibilityTime(); }

        //Start of the experiment: Start driving after 2 seconds 
        if(experimentSettings.experimentTime > 2f && experimentSettings.experimentTime < 3f  && !startedDriving) { car.GetComponent<SpeedController>().StartDriving(true); startedDriving=true;}

        //When I am doing some TESTING
        //if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { car.GetComponent<SpeedController>().StartDriving(true); }
        if (Input.GetKeyDown(KeyCode.Space)) { car.GetComponent<SpeedController>().ToggleDriving(); }
        if (Input.GetKeyDown(mainManager.CalibrateGaze)) { RequestGazeCalibration(); }
        if (Input.GetKeyDown(mainManager.ResetHeadPosition)) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }
        if (Input.GetKeyDown(KeyCode.LeftControl)) { StartCoroutine(PlaceAtTargetWaypoint()); }
        //Researcher inputs
        //if (Input.GetKeyDown(mainManager.ToggleSymbology)) {ToggleSymbology(); }
        if (Input.GetKeyDown(mainManager.MyPermission)) { car.navigationFinished = true; } //Finish navigation early
        //if (Input.GetKeyDown(mainManager.setToLastWaypoint)) { SetCarToLastWaypoint(); }
        //if (Input.GetKeyDown(experimentInput.resetHeadPosition)) { SetCameraPosition(driverView.position, driverView.rotation); }
        if (Input.GetKeyDown(mainManager.ResetExperiment)) { StartCoroutine(ResetExperiment()); }
        if (Input.GetKeyDown(KeyCode.LeftShift)) { TeleportToNextWaypoint(); }
        if (Input.GetKeyDown(KeyCode.Return)) { EndOfCalibrationTrial(); }
        if (car.navigationFinished)
        {
            //Log the last targets (targets of last cross point are automatically logged when leaving the crossing)
            //As this crossing is the end point we will never leave and not trigger this automated mechanism
            //if (!loggedLastTargets) { LogCurrentCrossingTargets(crossingSpawner.crossings.NextCrossing().targetList); loggedLastTargets = true; }
            //Probably not needed anymore needs testing still.
            //When car is close to standstill end the experiment
            if (car.GetComponent<Rigidbody>().velocity.magnitude < 0.5f) { mainManager.ExperimentEnded(); }
        }

      /*  if (Input.GetKey(KeyCode.UpArrow)){ maxAngle += 0.01f; Debug.Log($"maxAngle = {maxAngle}..."); }
        if (Input.GetKey(KeyCode.DownArrow)){ maxAngle -= 0.01f; Debug.Log($"maxAngle = {maxAngle}..."); }

        if (Input.GetKey(KeyCode.RightArrow)) { angleTreshold += 0.01f; Debug.Log($"angleTreshold = {angleTreshold}..."); }
        if (Input.GetKey(KeyCode.LeftArrow)) { angleTreshold -= 0.01f; Debug.Log($"angleTreshold = {angleTreshold}..."); }
*/
        //Code for practise drive UI and changing navigation type automtically
        if (experimentSettings.experimentType.IsPractise() && lastWaypointIndex != car.waypointIndex && navigationTypeIndex < 3 )
        {
            float turnsPerNavigationType = experimentSettings.turns.Count() / 3;
            lastWaypointIndex = car.waypointIndex;
            if ((car.waypointIndex) > navigationTypeIndex * turnsPerNavigationType ) 
            { 
                ToggleSymbology(); 
                StartCoroutine(InformOnNavigationType());
                navigationTypeIndex++;
            }
            
            //if ((car.waypointIndex) >= (int)Mathf.Floor(experimentSettings.turns.Count() / 2) && !informedOnHardTargets) { StartCoroutine(InformOnTargetDifficulty("Hard")); informedOnHardTargets = true; } 
        }

        //if (experimentSettings.experimentType.IsPractise() && experimentSettings.experimentTime > 2f && !informedOnEasyTargets) { StartCoroutine(InformOnTargetDifficulty("Easy")); informedOnEasyTargets = true; }

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
    void SetTransparencyEasyMaterial(float transparency)
    {
        Color nextColor = easyMaterial.color;
        nextColor.a = transparency;
        easyMaterial.color = nextColor;
    }
    void SetNextTransparencyAndSize()
    {
        bool finishedTransparencies = (transparencyIndex == transparencies.Count() - 1);
        bool finishedSizes = (sizeIndex == sizes.Count() - 1);

        if (finishedTransparencies && !finishedSizes) { transparencyIndex = 0; sizeIndex++; }
        else if (!finishedTransparencies) { transparencyIndex++; }
        else { car.navigationFinished = true; return; }

        transparencyTargets = transparencies[transparencyIndex];
        experimentSettings.targetSize = sizes[sizeIndex]; 
        Debug.Log($"Target transparency:{transparencyTargets}, size:{experimentSettings.targetSize}...");

        
    }
    public void EndOfCalibrationTrial()
    {
        if (experimentSettings.experimentType.IsTargetCalibration())
        {
            //Debug.Log("Restarting striahgt...");
            
            CrossComponents components = crossingSpawner.crossings.CurrentCrossing().components;

            //Log Targets;
            SetProperTargetIDsCalibration(components.targetList);
            LogTargets(components.targetList);

            //Lower target visibility based on detection rate
            if (firstStraight)
            {
                transparencyTargets = transparencies[transparencyIndex];
                experimentSettings.targetSize = sizes[sizeIndex];
                firstStraight = false; Debug.Log($"Target transparency:{transparencyTargets}, size:{experimentSettings.targetSize}...");
            }
            else { SetNextTransparencyAndSize(); }
            
            if (transparencyTargets <= 0f) { car.navigationFinished = true; return; }

            SetTransparencyEasyMaterial(transparencyTargets);

            car.ResetWaypoints();
            StartCoroutine(RestartStraight());

            //Remove old targets and spawn new ones
            
            components.RemoveTargets();
            components.SpawnTargets(experimentSettings);
        }
    }
    void RestartCalibrationTrial()
    {
        if (experimentSettings.experimentType.IsTargetCalibration())
        {
            //Debug.Log("Restarting striahgt...");

            CrossComponents components = crossingSpawner.crossings.CurrentCrossing().components;

            car.ResetWaypoints();
            StartCoroutine(RestartStraight());

            //Reset targets
            foreach (Target target in components.targetList) { target.ResetTarget(); }
        }
    }
    void SetProperTargetIDsCalibration(List<Target> targets)
    {
        foreach(Target target in targets)
        {
            target.ID = targetCountCalibration;
            targetCountCalibration++;
        }
    }
    IEnumerator InformOnNavigationType()
    {
        string navigationType = "";

        if(experimentSettings.navigationType == NavigationType.HUD_low) { navigationType = "low HUD"; }
        if (experimentSettings.navigationType == NavigationType.HUD_high) { navigationType = "high HUD"; }
        if (experimentSettings.navigationType == NavigationType.VirtualCable) { navigationType = "Virtual cable"; }

        carUI.text = $"Switched to {navigationType}...";

        yield return new WaitForSeconds(3f);

        carUI.text = "";
    }
    public bool MakeVirtualCable()
    {
        if (experimentSettings.navigationType == NavigationType.VirtualCable) { return true; }
        else { return false; }
    }
    public bool RenderHUD()
    {
        if (experimentSettings.navigationType == NavigationType.HUD_low || experimentSettings.navigationType == NavigationType.HUD_high) { return true; }
        else { return false; }
    }
    public float[] GetHUDInputs()
    {
        if (experimentSettings.navigationType == NavigationType.HUD_low)
        {
            float[] HUDPlacerInputs = { -8f, 0, 2.5f };
            return HUDPlacerInputs;
        }
        else if (experimentSettings.navigationType == NavigationType.HUD_high)
        {
            float[] HUDPlacerInputs = { 20f, 3.5f, 2.5f };
            return HUDPlacerInputs;
        }
        else { return new float[] { 0, 0, 0 }; }
    }
    public void SaveData()
    {
        if (!savedData) { dataManager.SaveData(); }
    }
    public int GetNextTargetID()
    {
        int count = targetCount;
        targetCount++;
        return count;
    }
    public void TookWrongTurn() 
    {
        dataManager.LogIrregularity(Irregularity.WrongTurn);

        carUI.text = "Wrong turn... No problem!";

        //Putting car at next waypoint  or restarting calibration trial
        if (experimentSettings.experimentType.IsTargetCalibration()) { RestartCalibrationTrial(); }
        else { StartCoroutine(PlaceAtTargetWaypoint()); }
    }
    public void CarOutOfBounce()
    {
        dataManager.LogIrregularity(Irregularity.OutOfBounce);
        //Car got hit by one of the out of bounce triggers
        carUI.text = "Car out of bounce... No problem!";

        //Putting car at next waypoint  or restarting calibration trial
        if (experimentSettings.experimentType.IsTargetCalibration()) { RestartCalibrationTrial(); }
        else { StartCoroutine(PlaceAtTargetWaypoint()); }
    }
    IEnumerator RestartStraight()
    {
        car.GetComponent<SpeedController>().StartDriving(false);
        startedDriving = false; //Disables visibilty timer of targets
        yield return new WaitForSeconds(1f);

        blackOutScreen.CrossFadeAlpha(1f, mainManager.AnimationTime, false);

        //Skip this waiting if we load while fading
        yield return new WaitForSeconds(mainManager.AnimationTime*1f);

        StartCoroutine(SetCarSteadyAt(startPoint.position, startPoint.forward));

        yield return new WaitForSeconds(2f);

        startedDriving = true; //Enables visibilty timer of targets
        blackOutScreen.CrossFadeAlpha(0f, 0f, true);
        car.GetComponent<SpeedController>().StartDriving(true);
    }
    IEnumerator PlaceAtTargetWaypoint()
    {
        car.GetComponent<SpeedController>().StartDriving(false);

        yield return new WaitForSeconds(1f);

        blackOutScreen.CrossFadeAlpha(1f, mainManager.AnimationTime, false);

        //Skip this waiting if we load while fading
        yield return new WaitForSeconds(mainManager.AnimationTime);

        WaypointStruct target= car.waypoint;

        float distanceFromWaypoint = 20f;

        Vector3 newStartPosition = target.waypoint.transform.position - target.waypoint.transform.forward * distanceFromWaypoint;

        StartCoroutine(SetCarSteadyAt(newStartPosition, target.waypoint.transform.forward, false));

        car.GetComponent<newNavigator>().RenderNavigationArrow();
        blackOutScreen.CrossFadeAlpha(0f, mainManager.AnimationTime*2f, false);

        yield return new WaitForSeconds(mainManager.AnimationTime);

        carUI.text = "";

        car.GetComponent<SpeedController>().StartDriving(true);
        
        
    }
    private void TeleportToNextWaypoint()
    {
        
        Vector3 targetView;  WaypointStruct targetWaypoint; Vector3 targetPos; Quaternion targetRot;

        //If very close to target waypoint we teleport to the next waypoint
        if (Vector3.Magnitude(car.transform.position - car.waypoint.waypoint.transform.position) < 20f) { targetWaypoint = car.GetNextWaypoint(); }
        else { targetWaypoint = car.waypoint; }

        targetPos = targetWaypoint.waypoint.transform.position + car.transform.forward * 7.5f;

        if (targetWaypoint.turn == TurnType.Left) { targetView = -targetWaypoint.waypoint.transform.right; }
        else if (targetWaypoint.turn == TurnType.Right) { targetView = targetWaypoint.waypoint.transform.right; }
        else { targetView = targetWaypoint.waypoint.transform.forward; }

        targetRot = targetWaypoint.waypoint.rotation;
        targetRot.SetLookRotation(targetView);
        

        StartCoroutine(SetCarSteadyAt(targetPos, targetView, true));
    }
    public void LogTargets(List<Target> targets)
    {
        dataManager.LogTargets(targets);
    }
    private void ToggleSymbology()
    {

        //Get a list f navigation types
        NavigationType nextNavType = GetNextNavigationType();
        experimentSettings.navigationType = nextNavType;

        if (nextNavType == NavigationType.HighlightedRoad) { nextNavType = GetNextNavigationType(); experimentSettings.navigationType = nextNavType; }

        if(nextNavType == NavigationType.VirtualCable) { car.GetComponent<CreateVirtualCable>().MakeVirtualCable(); }
        else if(nextNavType == NavigationType.HUD_high || nextNavType == NavigationType.HUD_low) { PlaceHUD(); }
            //activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car, HUDMaterials, activeExperiment.difficulty, false);
            //activeNavigationHelper.RenderNavigationArrow();
            Debug.Log($"Switched to {nextNavType}...");
    }
    NavigationType GetNextNavigationType()
    {
        IEnumerable<NavigationType> navigationTypes = EnumUtil.GetValues<NavigationType>();
        List<NavigationType> navigationList = new List<NavigationType>();
        foreach (NavigationType type in navigationTypes) { navigationList.Add(type); }

        int index = navigationList.IndexOf(experimentSettings.navigationType);

        int indexNextType = ((index + 1) == navigationList.Count ? 0 : index + 1);

        //Debug.Log($"Returning {navigationList[indexNextType]}...");
        return navigationList[indexNextType];
    }
    private void SetGameObjectsFromCar()
    {
        //FindObjectOfType head position
        driverView = car.transform.Find("Driver View");
        steeringWheel = car.transform.Find("PhysicalCar").Find("Interior").Find("SteeringWheel");

        if (driverView == null) { throw new System.Exception("Could not find head position in the given car..."); }

        //WE have multiple cameras set-up (varjoRig, LeapRig, Normal camera, and three cameras for the mirrors)
        Camera[] cameras = car.GetComponentsInChildren<Camera>(true);

        foreach (Camera camera in cameras)
        {
            if (camera.name == "LeftCamera") { leftMirror = camera; }
            if (camera.name == "RightCamera") { rightMirror = camera; }
            if (camera.name == "MiddleCamera") { rearViewMirror = camera; }

        }

        if (leftMirror == null || rightMirror == null || rearViewMirror == null)
        {
            Debug.Log("Couldnt set all cameras....");
        }
    }
    public Transform CameraTransform()
    {
        if (mainManager.camType == MyCameraType.Leap) { return player.Find("VarjoCameraRig").Find("VarjoCamera"); }
        else if (mainManager.camType == MyCameraType.Varjo) { return player.Find("VarjoCamera"); }
        else if (mainManager.camType == MyCameraType.Normal) { return player; }
        else { throw new System.Exception("Error in retrieving used camera transform in Experiment Manager.cs..."); }
    }
    IEnumerator ResetExperiment()
    {
        car.GetComponent<SpeedController>().StartDriving(false);

        //Wait till car is at stand still 
        while(car.GetComponent<Rigidbody>().velocity.magnitude > 0.1f) { yield return new WaitForSeconds(0.1f); }

        mainManager.ReloadCurrentExperiment();
    }
    public List<string> GetCarControlInput()
    {
        //Used in the XMLManager to save user input
        List<string> output = new List<string>();

        if (mainManager.camType == MyCameraType.Normal)
        {
            output.Add(mainManager.SteerWithKeyboard);
            output.Add(mainManager.GasWithKeyboard);
            output.Add(mainManager.BrakeWithKeyboard);
        }
        else
        {
            output.Add(mainManager.Steer);
            output.Add(mainManager.Gas);
            output.Add(mainManager.Brake);
        }
        return output;
    }
    void SetUpCar()
    {
        //Put head position at the right place
        if (mainManager.CalibratedUsingHands)
        {
            driverView.position = steeringWheel.transform.position;
            driverView.position -= car.transform.forward * mainManager.DriverViewZToSteeringWheel;
            driverView.position += Vector3.up * mainManager.DriverViewYToSteeringWheel;
            driverView.position += car.transform.right * mainManager.DriverViewXToSteeringWheel;

        }
        //Place HUD
        PlaceHUD();

        //Put car in right position
        StartCoroutine(SetCarSteadyAt(startPoint.position, startPoint.forward));
    }
    void PlaceHUD()
    {
        if (RenderHUD())
        {
            HUDPlacer HUDPlacer = car.HUD.GetComponent<HUDPlacer>();
            float[] HUDInputs = GetHUDInputs();
            HUDPlacer.SetAngles(HUDInputs[0], HUDInputs[1], HUDInputs[2]);
            HUDPlacer.PlaceHUD();
        }
        car.RenderNavigationArrow();
    }
    void GoToCar()
    {
        SetCameraPosition(driverView.position, driverView.rotation);
    }
    void ProcessUserInputTargetDetection()
    {

        //When multiple targets are visible we base our decision on:
        //(1) On which target has been looked at most recently
        //(2) Target closest to looking direction

        //if there is a target visible which has not already been detected
        List<Target> visibleTargets = GetVisibleTargets();

        if (visibleTargets.Count() == 0) { dataManager.AddFalseAlarm(); }
        else if (visibleTargets.Count() == 1) { dataManager.AddTrueAlarm(visibleTargets[0]); visibleTargets[0].SetDetected(experimentSettings.experimentTime); }
        else { StartCoroutine(CheckTargetDetection(visibleTargets)); }
    }
    IEnumerator CheckTargetDetection(List<Target> visibleTargets)
    {
        Target targetChosen = null; 
        int frameCount = 0; 
        int maxFrames = 5;

        while (targetChosen == null && frameCount < maxFrames)
        {
            //When multiple targets are visible we base our decision on:
            //(1) On which target has been looked at most recently (max 1 second)
            float mostRecentTime = .5f;
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
            
            //float maxAngle = 3; float angleTreshold = 1f; 
            float smallestAngle = maxAngle;
            Varjo.VarjoPlugin.GazeData data = Varjo.VarjoPlugin.GetGaze();
            if (targetChosen == null && mainManager.camType != MyCameraType.Normal && data.status == Varjo.VarjoPlugin.GazeStatus.VALID)
            {
                List<float> angles = new List<float>();

                Vector3 gazeDirection = MyUtils.TransformToWorldAxis(data.gaze.forward, data.gaze.position);

                foreach (Target target in visibleTargets)
                {
                    Vector3 CamToTarget = target.transform.position - CameraTransform().position;
                    float angle = Vector3.Angle(gazeDirection, CamToTarget);
                    angles.Add(angle);
                }

                smallestAngle = angles.Min();
                if (smallestAngle < maxAngle) { targetChosen = visibleTargets[angles.IndexOf(smallestAngle)]; }

                //if there are angles close together we chose the target which is closest
                List<float> anglesCloseTogether = angles.Where(s => Mathf.Abs(s - smallestAngle) < angleTreshold).ToList();

                if (anglesCloseTogether.Count() > 1 && targetChosen != null)
                {
                    int index; float distanceTargetChosen = 0f; float distanceOfThisTarget;
                    foreach (float angle in anglesCloseTogether)
                    {
                        index = angles.IndexOf(angle);
                        Target targetOfAngle = visibleTargets[index];

                        distanceTargetChosen = Vector3.Magnitude(targetChosen.transform.position - CameraTransform().position);
                        distanceOfThisTarget = Vector3.Magnitude(targetOfAngle.transform.position - CameraTransform().position);
                        if (distanceTargetChosen > distanceOfThisTarget) { targetChosen = targetOfAngle; }

                    }

                    Debug.Log($"ANGLE-CLOSEST-method: {targetChosen.gameObject.name}, distance: {distanceTargetChosen},  angle: {smallestAngle}...");
                }
                else if (targetChosen != null) { Debug.Log($"ANGLE-method chose {targetChosen.gameObject.name}, angle {smallestAngle}..."); }
            }
            else if(targetChosen != null) { Debug.Log($"FIXATION-method chose {targetChosen.name}, time since fixation: {mostRecentTime}..."); }

            if (targetChosen == null) { frameCount++; yield return new WaitForEndOfFrame(); }
            else { break; }  //found a target 
        }

        //Log user input
        if (targetChosen == null) { dataManager.AddFalseAlarm(); }
        else
        {
            dataManager.AddTrueAlarm(targetChosen);
            targetChosen.SetDetected(experimentSettings.experimentTime);
        }
    }
    Vector3 GetRandomPerpendicularVector(Vector3 vec)
    {

        vec = Vector3.Normalize(vec);

        float v1 = Random.Range(-1f, 1f);
        float v2 = Random.Range(-1f, 1f);

        float x; float y; float z;

        int caseSwitch = Random.Range(0, 3); //outputs 0,1 or, 2


        if (caseSwitch == 0)
        {
            // v1 = x, v2 = y, v3 = z
            x = v1; y = v2;
            z = -(x * vec.x + y * vec.y) / vec.z;
        }
        else if (caseSwitch == 1)
        {
            // v1 = y, v2 = z, v3 = x
            y = v1; z = v2;
            x = -(y * vec.y + z * vec.z) / vec.x;
        }
        else if (caseSwitch == 2)
        {
            // v1 = z, v2 = x, v3 = y
            z = v1; x = v2;
            y = -(z * vec.z + x * vec.x) / vec.y;
        }
        else
        {
            throw new System.Exception("Something went wrong in TargetManager -> GetRandomPerpendicularVector() ");
        }

        float mag = Mathf.Sqrt(x * x + y * y + z * z);
        Vector3 normal = new Vector3(x / mag, y / mag, z / mag);
        return normal;
    }
    bool TargetIsVisible(Target target, int maxNumberOfRayHits)
    {
        //We will cast rays to the outer edges of the sphere (the edges are determined based on how we are looking towards the sphere)
        //I.e., with the perpendicular vector to the looking direction of the sphere
        if(target == null) { return false; }
        Vector3 vectorToTarget = target.transform.position - CameraTransform().position;
        //(1) Not in sight of camera
        float angle = Mathf.Abs(Vector3.Angle(CameraTransform().forward, vectorToTarget));
        if (angle > (mainManager.FoVCamera / 2 )) { return false; }

        //(2) If in sight we check if it is not occluded by buildings and such
        Vector3 currentDirection; RaycastHit hit;
        float targetRadius = target.GetComponent<SphereCollider>().radius* target.transform.localScale.x * 0.95f;

        //Vary the location of the raycast over the edge of the potentially visible target
        for (int i = 0; i < maxNumberOfRayHits; i++)
        {
            Vector3 randomPerpendicularDirection = GetRandomPerpendicularVector(vectorToTarget);
            currentDirection = (target.transform.position + randomPerpendicularDirection * targetRadius) - CameraTransform().position;

            if (Physics.Raycast(CameraTransform().position, currentDirection, out hit, 10000f, ~layerToIgnoreForTargetDetection))
            {
                Debug.DrawRay(CameraTransform().position, currentDirection, Color.green);
                if (hit.collider.CompareTag("Target"))
                {
                    Debug.DrawLine(CameraTransform().position, CameraTransform().position + currentDirection * 500, Color.cyan, Time.deltaTime, false);
                    return true;
                }
                else { return false; }
            }
        }

        return false;
    }
    void SetTargetVisibilityTime()
    {
        //Number of ray hits to be used. We user a smaller amount than when the user actually presses the detection button. Since this function is called many times in Update() 
        int numberOfRandomRayHits = 3;

        foreach (Target target in GetAllTargets())
        {
            //If we havent seen the target before
            if (!target.HasBeenVisible() && !target.IsDetected())
            {
                if (TargetIsVisible(target, numberOfRandomRayHits))
                {
                    //Debug.Log($"Target {target.GetID()} became visible at {experimentSettings.experimentTime}s ...");
                    target.SetVisible(true, experimentSettings.experimentTime);
                }
            }
        }
    }
    private List<Target> GetAllTargets()
    {
        return crossingSpawner.crossings.GetAllTargets();
    }
    private List<Target> GetVisibleTargets()
    {
        List<Target> visibleTargets = new List<Target>();

        //Check if there are any visible targets
        foreach (Target target in GetAllTargets()) { if (target.HasBeenVisible() && target.InFoV(CameraTransform(), mainManager.FoVCamera) && !target.IsDetected()) { visibleTargets.Add(target); } }

        return visibleTargets;
    }
    void SetCameraPosition(Vector3 goalPos, Quaternion goalRot)
    {
        player.position = goalPos;
        player.rotation = goalRot;
    }
    private bool UserInput()
    {
        bool input = (Input.GetAxis(mainManager.ParticpantInputAxisLeft) == 1 || Input.GetAxis(mainManager.ParticpantInputAxisRight) == 1);

        if (!input) { lastUserInput = false; return false; }
        else if (input && lastUserInput) { lastUserInput = true; return false; }
        else { lastUserInput = true; return true; }
    }
    private void ActivateMirrorCameras()
    {
        rearViewMirror.enabled = true; rearViewMirror.cullingMask = -1; // ~(1 << 2); // everything except layer 2

        rightMirror.enabled = true; rightMirror.cullingMask = -1;// -1 == everything

        leftMirror.enabled = true; leftMirror.cullingMask = -1;
    }
    /*private void SetColors()
    {
        navigationColor.a = activeExperiment.transparency;
        conformal.color = navigationColor;

        foreach (Material material in HUDMaterials.GetMaterials())
        {
            material.color = navigationColor;
        }
    }*/
    private void SetDataManager()
    {
        //Get attatched XMLManager
        dataManager = GetComponent<DataLogger>();
        
        //Throw error if we dont have an xmlManager
        if (dataManager == null) { throw new System.Exception("Error in Experiment Manager -> A XMLManager should be attatched if you want to save data..."); }

        dataManager.StartUp();        
    }
    void SetCarControlInput()
    {
        VehiclePhysics.VPStandardInput carController = car.GetComponent<VehiclePhysics.VPStandardInput>();
        if (mainManager.camType == MyCameraType.Normal)
        {
            carController.steerAxis = mainManager.SteerWithKeyboard;
            carController.throttleAndBrakeAxis = mainManager.GasWithKeyboard;
        }
        else
        {
            carController.steerAxis = mainManager.Steer;
            carController.throttleAndBrakeAxis = mainManager.Gas;
        }
    }
    void SetCamera()
    {
        RearMirrorsReflection[] reflectionScript = car.GetComponentsInChildren<RearMirrorsReflection>(true);
        if (reflectionScript != null && player != null) { reflectionScript[0].head = CameraTransform(); }
        else { Debug.Log("Could not set head position for mirro reflection script..."); }
    }
    IEnumerator SetCarSteadyAt(Vector3 targetPos, Vector3 targetRot, bool superSonic = false)
    {

        //Somehow car did some back flips when not keeping it steady for some time after repositioning.....
        float step = 0.02f;
        float totalSteps = .5f;
        float count = 0;
    
        if (superSonic)
        {
            float totalDistance = Vector3.Magnitude(targetPos - car.gameObject.transform.position);
            float rotationSpeed = Vector3.Angle(car.transform.forward, targetRot) / Mathf.PI / (totalSteps / step) / 2f;
            //Debug.Log($"Rotating with {rotationSpeed} rad/s...");
            while (count < totalSteps)
            {

                Vector3 direction = targetPos - car.gameObject.transform.position;

                car.transform.position += direction.normalized * totalDistance * step / totalSteps;

                //Keep car steady in the y direction;
                Vector3 newPos = car.transform.position; newPos = new Vector3(newPos.x, 0.08f, newPos.z);

                car.transform.position = newPos;



                count += step;
                yield return new WaitForSeconds(step);
            }
            count = 0;


            while (count < totalSteps / 3)
            {
                Vector3 newDirection = Vector3.RotateTowards(car.transform.forward, targetRot, rotationSpeed, 0.0f);
                car.transform.rotation = Quaternion.LookRotation(newDirection);

                car.transform.position = targetPos;
                car.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
                car.GetComponent<Rigidbody>().velocity = Vector3.zero;
                count += step;
                yield return new WaitForSeconds(step);
            }
        }
        else
        {
            while (count < totalSteps)
            {
                car.transform.position = targetPos;
                car.transform.rotation = Quaternion.LookRotation(targetRot);

                car.GetComponent<Rigidbody>().velocity = Vector3.zero;
                car.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

                count += step;
                yield return new WaitForSeconds(step);
            }
        }
    }
}

[System.Serializable]
public class Experiment
{
    public float experimentTime;
    public NavigationType navigationType;
    public float transparency = 0.4f;

    public TargetDifficulty difficulty = TargetDifficulty.easy;

    public Experiment(Transform _navigation, NavigationType _navigationType, float _transparency, TargetDifficulty _difficulty)
    {
        navigationType = _navigationType;
        transparency = _transparency;
        experimentTime = 0f;
        difficulty = _difficulty;
    }
}