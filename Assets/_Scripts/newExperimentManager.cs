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

    private void Start()
    {
        StartUpFunctions();
    }
    void StartUpFunctions()
    {
        player = MyUtils.GetPlayer().transform;
        blackOutScreen = MyUtils.GetBlackOutScreen();
        mainManager = MyUtils.GetMainManager();
        carUI = MyUtils.GetCarUI();

        //Set DataManager
        SetDataManager();

        //Beun settings
        experimentSettings = mainManager.GetExperimentSettings();
        
        crossingSpawner.turnsList = experimentSettings.turns.ToArray();
        crossingSpawner.StartUp();

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
        if (experimentSettings.navigationType == NavigationType.HUD_high)
        {
            float[] HUDPlacerInputs = { -6f, 0, 2f };
            return HUDPlacerInputs;
        }
        else if (experimentSettings.navigationType == NavigationType.HUD_low)
        {
            float[] HUDPlacerInputs = { 20f, 3.5f, 2f };
            return HUDPlacerInputs;
        }
        else { return new float[] { 0, 0, 0 }; }
    }

    private void LateUpdate()
    {
        //We dot this as late update to make sure gaze data (which is used to determine which target the participant is detecting) is processed 
        //before this code is exectured

        bool userInput = UserInput();
        //Target detection when we already started driving
        if (car.GetComponent<SpeedController>().IsDriving() && userInput) { ProcessUserInputTargetDetection(); }
        //First input will be the start driving command (so if not already driving we will start driving)
        else if (!car.GetComponent<SpeedController>().IsDriving() && userInput && mainManager.automateSpeed) { car.GetComponent<SpeedController>().StartDriving(true); }
    }
    void Update()
    {
        experimentSettings.experimentTime += Time.deltaTime;
        //Looks for targets to appear in field of view and sets their visibility timer accordingly

        SetTargetVisibilityTime();

        //When I am doing some TESTING
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { car.GetComponent<SpeedController>().StartDriving(true); }
        if (Input.GetKeyDown(KeyCode.Space)) { car.GetComponent<SpeedController>().ToggleDriving(); }
        if (Input.GetKeyDown(mainManager.CalibrateGaze)) { Varjo.VarjoPlugin.RequestGazeCalibration(); }
        if (Input.GetKeyDown(mainManager.ResetHeadPosition)) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }
        if (Input.GetKeyDown(KeyCode.LeftControl)) { StartCoroutine(PlaceAtTargetWaypoint()); }
        //Researcher inputs
        if (Input.GetKeyDown(mainManager.ToggleSymbology)) {ToggleSymbology(); }
        if (Input.GetKeyDown(mainManager.MyPermission)) { car.navigationFinished = true; } //Finish navigation early
        //if (Input.GetKeyDown(mainManager.setToLastWaypoint)) { SetCarToLastWaypoint(); }
        //if (Input.GetKeyDown(experimentInput.resetHeadPosition)) { SetCameraPosition(driverView.position, driverView.rotation); }
        if (Input.GetKeyDown(mainManager.ResetExperiment)) { ResetExperiment(); }
        if (Input.GetKeyDown(KeyCode.LeftShift)) { TeleportToNextWaypoint(); }

        if (car.navigationFinished)
        {
            LogCurrentCrossingTargets(crossingSpawner.crossings.NextCrossing().targetList);

            if (car.GetComponent<Rigidbody>().velocity.magnitude < 0.5f) { mainManager.ExperimentEnded(); }
        }

        if (experimentSettings.practiseDrive && lastWaypointIndex != car.waypointIndex )
        {
            lastWaypointIndex = car.waypointIndex;
            if ((car.waypointIndex) % (int)Mathf.Floor(experimentSettings.turns.Count()/3) == 0 ) { ToggleSymbology(); }
            
        }
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
        dataManager.TookWrongTurn(car.GetComponent<newNavigator>().waypoint);


        carUI.text = "Wrong turn... No problem!";

        //Setting next waypoint
        car.GetComponent<newNavigator>().SetNextWaypoint();

        //Putting car at next waypoint
        StartCoroutine(PlaceAtTargetWaypoint(true));

        //We wait for the car to be placeced at the next waypoint before setting next crossing
        StartCoroutine(SetNextWaypointCrossing());
    }
    IEnumerator SetNextWaypointCrossing()
    {
        while (!setCarAtTarget)
        {
            yield return new WaitForEndOfFrame();
        }

        //Telporting to next waypoint will skip the triggers for next crossing to be made...
        //New crossings are made after every UNeven waypoint so if we made a wrong turn at an uneven waypoint we manually set next crossing here...
        //After a wrong turn AT a uneven waypoint we will be set at the NEXT waypoint. This one will thus be even
        if ((car.GetComponent<newNavigator>().waypointIndex % 2) == 0) { crossingSpawner.SetNextCrossing(); Debug.Log("Enabled next crossing!"); }

        setCarAtTarget = false;
    }
    IEnumerator PlaceAtTargetWaypoint(bool _setAtCarTargetBoolean = false)
    {
        car.GetComponent<SpeedController>().StartDriving(false);

        yield return new WaitForSeconds(1f);

        blackOutScreen.CrossFadeAlpha(1f, mainManager.animationTime, false);

        //Skip this waiting if we load while fading
        yield return new WaitForSeconds(mainManager.animationTime);

        WaypointStruct target= car.waypoint;

        Vector3 newStartPosition = target.waypoint.transform.position - target.waypoint.transform.forward * 20f;

        StartCoroutine(SetCarSteadyAt(newStartPosition, target.waypoint.transform.forward, false, _setAtCarTargetBoolean));

        car.GetComponent<newNavigator>().RenderNavigationArrow();
        blackOutScreen.CrossFadeAlpha(0f, mainManager.animationTime*2f, false);

        carUI.text = "Try again!";

        yield return new WaitForSeconds(mainManager.animationTime);

        carUI.text = "";

        car.GetComponent<SpeedController>().StartDriving(true);
        
        
    }
    private void TeleportToNextWaypoint()
    {
        
        Vector3 targetView;  WaypointStruct targetWaypoint; Vector3 targetPos; Quaternion targetRot;

        //If very close to target waypoint we teleport to the next waypoint
        if (Vector3.Magnitude(car.transform.position - car.waypoint.waypoint.transform.position) < 10f) { targetWaypoint = car.GetNextWaypoint(); }
        else { targetWaypoint = car.waypoint; }

        targetPos = targetWaypoint.waypoint.transform.position + car.transform.forward * 7.5f;

        if (targetWaypoint.turn == TurnType.Left) { targetView = -targetWaypoint.waypoint.transform.right; }
        else if (targetWaypoint.turn == TurnType.Right) { targetView = targetWaypoint.waypoint.transform.right; }
        else { targetView = targetWaypoint.waypoint.transform.forward; }

        targetRot = targetWaypoint.waypoint.rotation;
        targetRot.SetLookRotation(targetView);
        

        StartCoroutine(SetCarSteadyAt(targetPos, targetView, true));
        
        
    }
    public void LogCurrentCrossingTargets(List<Target> targets)
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

        Debug.Log($"Returning {navigationList[indexNextType]}...");
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
    void ResetExperiment()
    {
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
        Debug.Log("Setting up car...");

        //Put head position at the right place
        if (mainManager.calibratedUsingHands)
        {
            driverView.position = steeringWheel.transform.position;
            driverView.position -= car.transform.forward * mainManager.driverViewZToSteeringWheel;
            driverView.position += Vector3.up * mainManager.driverViewYToSteeringWheel;
            driverView.position += car.transform.right * mainManager.driverViewXToSteeringWheel;

        }
        //Place HUD
        PlaceHUD();

        //Put car in right position
        Transform startLocation = crossingSpawner.crossings.CurrentCrossing().components.startPoint.transform;       

        StartCoroutine(SetCarSteadyAt(startLocation.position, startLocation.forward));
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
        Debug.Log("Returning to car...");
        SetCameraPosition(driverView.position, driverView.rotation);
    }
    void ProcessUserInputTargetDetection()
    {

        //When multiple targets are visible we base our decision on:
        //(1) On which target has been looked at most recently
        //(2) Target closest to looking direction

        //if there is a target visible which has not already been detected
        List<Target> targetList = GetActiveTargets();
        List<Target> visibleTargets = new List<Target>();

        //Check if there are any visible targets
        foreach (Target target in targetList) { if (target.HasBeenVisible() && target.InFoV(CameraTransform(), mainManager.FoVCamera)) { visibleTargets.Add(target); } }

        if (visibleTargets.Count() == 0) { dataManager.AddFalseAlarm(); }
        else if (visibleTargets.Count() == 1) { dataManager.AddTrueAlarm(visibleTargets[0]); visibleTargets[0].SetDetected(experimentSettings.experimentTime); }
        else
        {
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
            if (targetChosen == null && mainManager.camType != MyCameraType.Normal)
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

            /*//(3)
            if (targetChosen == null) { 
                Debug.Log("Chosing target based on distance...");
                float smallestDistance = 100000f;
                float currentDistance;
                foreach (Target target in visibleTargets) {

                    //(2) Stops this when mostRecentTime variables gets set to something else then 0
                    currentDistance = Vector3.Distance(CameraTransform().position, target.transform.position);
                    if (currentDistance < smallestDistance && mostRecentTime == 1f)
                    {
                        targetChosen = target;
                        smallestDistance = currentDistance;
                    } 
                }
            }
            else { Debug.Log($"Chose target based on fixation time: {Time.time - mostRecentTime}..."); }
*/
            if (targetChosen == null) { dataManager.AddFalseAlarm(); }
            else
            {
                dataManager.AddTrueAlarm(targetChosen);
                targetChosen.SetDetected(experimentSettings.experimentTime);
            }
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

        Vector3 vectorToTarget = target.transform.position - CameraTransform().position;
        //(1) Not in sight of camera
        float angle = Mathf.Abs(Vector3.Angle(CameraTransform().forward, vectorToTarget));
        if (angle > mainManager.FoVCamera) { return false; }

        //(2) If in sight we check if it is not occluded by buildings and such
        bool isVisible = false; Vector3 currentDirection; RaycastHit hit;
        float targetRadius = target.GetComponent<SphereCollider>().radius;

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
                    isVisible = true;
                    break;
                }
                //else { Debug.Log($"Hit {hit.collider.gameObject.name}...."); }
            }
        }

        return isVisible;
    }
    bool TargetInFOV(Target target)
    {
        Vector3 backOfCar = car.transform.position - 4 * car.transform.forward;
        float sign = Vector3.Dot(car.transform.forward, (backOfCar - target.transform.position));

        if (sign >= 0) { return false; }
        else { return true; }
    }
    void SetTargetVisibilityTime()
    {
        //Number of ray hits to be used. We user a smaller amount than when the user actually presses the detection button. Since this function is called many times in Update() 
        int numberOfRandomRayHits = 3;

        foreach (Target target in GetActiveTargets())
        {
            //If we havent seen the target before
            if (!target.HasBeenVisible())
            {
                if (TargetIsVisible(target, numberOfRandomRayHits))
                {
                    //Debug.Log($"Target {target.GetID()} became visible at {experimentSettings.experimentTime}s ...");
                    target.SetVisible(true, experimentSettings.experimentTime);
                }
            }
        }
    }
    private List<Target> GetActiveTargets()
    {
        return car.GetComponent<CrossingSpawner>().crossings.GetAllTargets();

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
    IEnumerator SetCarSteadyAt(Vector3 targetPos, Vector3 targetRot, bool superSonic = false, bool _setCarAtTargetBool = false)
    {
        //Somehow car did some back flips when not keeping it steady for some time after repositioning.....
        float step = 0.005f;
        float totalSeconds = 0.25f;
        float count = 0;


        if (superSonic)
        {
            float totalDistance = Vector3.Magnitude(targetPos - car.gameObject.transform.position);
            float rotationSpeed = Vector3.Angle(car.transform.forward, targetRot) / Mathf.PI / (totalSeconds / step) / 2f;
            Debug.Log($"Rotating with {rotationSpeed} rad/s...");
            while (count < totalSeconds)
            {

                Vector3 direction = targetPos - car.gameObject.transform.position;

                car.transform.position += direction.normalized * totalDistance * step / totalSeconds;
                count += step;
                yield return new WaitForSeconds(step);
            }
            count = 0;

            
            while (count < totalSeconds/3)
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
            while (count < totalSeconds)
            {
                car.transform.position = targetPos;
                car.transform.rotation = Quaternion.LookRotation(targetRot);

                car.GetComponent<Rigidbody>().velocity = Vector3.zero;
                car.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

                count += step;
                yield return new WaitForSeconds(step);
            }
        }
        setCarAtTarget = _setCarAtTargetBool;
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