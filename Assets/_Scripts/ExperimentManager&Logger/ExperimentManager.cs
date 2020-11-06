using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(DataLogger))]
public class ExperimentManager : MonoBehaviour
{
    //Attachted to the object with the experiment manager should be a XMLManager for handling the saving of the data.
    [Header("Experiment Input")]
    public string subjectName;
    public bool automateSpeed;
    public bool saveData;
    public Color navigationColor;
    public MyCameraType camType;
    public List<ExperimentSetting> experiments;
    private MySceneLoader mySceneLoader;

    [Header("Inputs")]
    public KeyCode myPermission;
    public KeyCode resetHeadPosition;
    public KeyCode spawnSteeringWheel;
    public KeyCode calibrateGaze;
    public KeyCode resetExperiment;
    public KeyCode keyboardTargetDetection;

    public KeyCode keyToggleDriving;

    public KeyCode keyToggleSymbology;

    public KeyCode setToLastWaypoint;
    public KeyCode inputNameKey;

    public KeyCode saveTheData;

    public string ParticpantInputAxisLeft = "SteerButtonLeft";
    public string ParticpantInputAxisRight = "SteerButtonRight";

    [Header("Car Controls")]
    public string GasWithKeyboard = "GasKeyBoard";
    public string SteerWithKeyboard = "SteerKeyBoard";
    public string BrakeWithKeyboard = "BrakeKeyBoard";

    public string Gas = "Gas";
    public string Steer = "Steer";
    public string Brake = "Brake";

    [Header("GameObjects")]
    // expriment objects and lists
    public Experiment activeExperiment;
    public NavigationHelper activeNavigationHelper;

    public Material conformal;
    public HUDMaterials HUDMaterials;
    public LayerMask layerToIgnoreForTargetDetection;
    
    public Navigator car;

    //Mirror cameras from car
    private Camera rearViewMirror;
    private Camera leftMirror;
    private Camera rightMirror;

    //The camera used and head position inside the car
    [HideInInspector]
    public Transform driverView;
    private Transform player;
    private Transform steeringWheel;

   
    //The data manger handling the saving of vehicle and target detection data Should be added to the experiment manager object 
    private DataLogger dataManager;
    //Maximum raycasts used in determining visbility:  We use Physics.RayCast to check if we can see the target. We cast this to a random positin on the targets edge to see if it is partly visible.
    private readonly int maxNumberOfRandomRayHits = 40;
    private float lastUserInputTime = 0f;
    public float thresholdUserInput = 0.15f; //The minimum time between user inputs (when within this time only the first one is used)

    private float userInputTime = 0f; private readonly float userInputThresholdTime = 0.2f;

    private void Start()
    {
        StartUpFunctions();
    }
    void StartUpFunctions()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        //Set rotation of varjo cam to be zero w.r.t. rig
        GetVariablesFromSceneManager();

        //Get all gameobjects we intend to use from the car (and do some setting up)
        SetGameObjectsFromCar();

        //Set camera (uses the gameobjects set it SetGameObjectsFromCar()) 
        SetCamera();

        //Set car to be ignored by raycast of gaze logger
        MyVarjoGazeRay gazeLogger = GetComponent<MyVarjoGazeRay>();

        if (gazeLogger) { gazeLogger.layerMask = ~layerToIgnoreForTargetDetection; }
//        if (camType == MyCameraType.Varjo || camType == MyCameraType.Leap) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }

        SetCarControlInput();

        activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car, HUDMaterials);

        //Set DataManager
        SetDataManager();

        SetColors();
        //Set up car
        SetUpCar();

        GoToCar();

        //Activate mirror cameras (When working with the varjo it deactivates all other cameras....)
        //Does not work when in Start() or in Awake()...
        ActivateMirrorCameras();
    }
    void GetVariablesFromSceneManager()
    {
        mySceneLoader = GetComponent<MySceneLoader>();
        camType = StaticSceneManager.camType;

        keyboardTargetDetection = StaticSceneManager.keyboardTargetDetection;
        myPermission = StaticSceneManager.myPermission;
        resetHeadPosition = StaticSceneManager.resetHeadPosition;
        spawnSteeringWheel = StaticSceneManager.spawnSteeringWheel;
        calibrateGaze = StaticSceneManager.calibrateGaze;

        resetExperiment = StaticSceneManager.resetExperiment;

        keyToggleDriving = StaticSceneManager.keyToggleDriving;
        keyToggleSymbology = StaticSceneManager.keyToggleSymbology;

        setToLastWaypoint = StaticSceneManager.setToLastWaypoint;

        inputNameKey = StaticSceneManager.inputNameKey;
        saveTheData = StaticSceneManager.saveTheData;
}
    void Update()
    {
        activeExperiment.experimentTime += Time.deltaTime;
        
        //Looks for targets to appear in field of view and sets their visibility timer accordingly
        SetTargetVisibilityTime();

        //When I am doing some TESTING
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { car.GetComponent<SpeedController>().StartDriving(true); }

        //Target detection when we already started driving
        if ((UserInput() || Input.GetKeyDown(keyboardTargetDetection))) { ProcessUserInputTargetDetection(); }
        //car.GetComponent<SpeedController>().IsDriving() && 
        //First input will be the start driving command (so if not already driving we will start driving)
        else if (!car.GetComponent<SpeedController>().IsDriving() && UserInput() && automateSpeed) { car.GetComponent<SpeedController>().StartDriving(true); }

        //Researcher inputs
        if (Input.GetKeyDown(keyToggleDriving)) { car.GetComponent<SpeedController>().ToggleDriving(); }
        if (Input.GetKeyDown(keyToggleSymbology)) { ToggleSymbology(); }
        if (Input.GetKeyDown(myPermission)) { car.navigationFinished = true; } //Finish navigation early
        if (Input.GetKeyDown(setToLastWaypoint)) { SetCarToLastWaypoint(); }
        if (Input.GetKeyDown(resetHeadPosition)){ SetCameraPosition(driverView.position, driverView.rotation); }
        if (Input.GetKeyDown(resetExperiment)) { ResetExperiment(); }
        if (Input.GetKeyDown(KeyCode.LeftShift)) { TeleportToNextWaypoint(); }
        //Request gaze calibration
        //if (Input.GetKeyDown(calibrateGaze)) { GetComponent<VarjoExample.VarjoGazeCalibrationRequest>().RequestGazeCalibration(); }

        if (car.navigationFinished)
        {
            if (StaticSceneManager.saveData) { dataManager.SaveData(); }

            if (car.GetComponent<Rigidbody>().velocity.magnitude < 0.01f) { mySceneLoader.LoadWaitingScene(); }
        }
    }
    private void TeleportToNextWaypoint()
    {
        if (car.target.nextWaypoint != null)
        {
            StartCoroutine(SetCarSteadyAt(car.target.nextWaypoint.transform.position, car.target.nextWaypoint.transform.rotation));
            car.SetNextTarget();
            
        }
    }
    private void ToggleSymbology()
    {
        //Get a list f navigation types
        NavigationType nextNavType = GetNextNavigationType();
        activeExperiment.navigationType = nextNavType;

        if (nextNavType == NavigationType.HighlightedRoad) { nextNavType = GetNextNavigationType(); activeExperiment.navigationType = nextNavType; }

        
        activeNavigationHelper.SetUp(nextNavType, activeExperiment.transparency, car, HUDMaterials);
        activeNavigationHelper.RenderNavigationArrow();
        Debug.Log($"Switched to {nextNavType}...");
    }
    NavigationType GetNextNavigationType()
    {
        IEnumerable<NavigationType> navigationTypes = EnumUtil.GetValues<NavigationType>();
        List<NavigationType> navigationList = new List<NavigationType>();
        foreach (NavigationType type in navigationTypes) { navigationList.Add(type); }

        int index = navigationList.IndexOf(activeExperiment.navigationType);

        int indexNextType = ((index + 1) == navigationList.Count ? 0 : index + 1);

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

        if (leftMirror == null || rightMirror == null || rearViewMirror == null )
        {
            Debug.Log("Couldnt set all cameras....");
        }
    }
    public Transform CameraTransform()
    {
        if (camType == MyCameraType.Leap) { return player.Find("VarjoCameraRig").Find("VarjoCamera"); }
        else if (camType == MyCameraType.Varjo) { return player.Find("VarjoCamera"); }
        else if (camType == MyCameraType.Normal) { return player; }
        else { throw new System.Exception("Error in retrieving used camera transform in Experiment Manager.cs..."); }
    }
    void ResetExperiment()
    {
        if (StaticSceneManager.saveData) { dataManager.SaveData(); dataManager.StartNewMeasurement(); }

        activeNavigationHelper.SetUp(activeExperiment.navigationType, activeExperiment.transparency, car, HUDMaterials);
        car.GetComponent<SpeedController>().StartDriving(false);
        SetUpCar();
    }
    public List<string> GetCarControlInput()
    {
        //Used in the XMLManager to save user input
        List<string> output = new List<string>();

        if (camType == MyCameraType.Normal)
        {
            output.Add(SteerWithKeyboard);
            output.Add(GasWithKeyboard);
            output.Add(BrakeWithKeyboard);
        }
        else
        {
            output.Add(Steer);
            output.Add(Gas);
            output.Add(Brake);
        }
        return output;
    }
    void SetCarToLastWaypoint()
    {
        //Get previouswwaypoint which is not a splinepoint
        Waypoint previousWaypoint = car.target.previousWaypoint;
        while (previousWaypoint.operation == Operation.SplinePoint) { previousWaypoint = previousWaypoint.previousWaypoint; }

        Vector3 targetPos = previousWaypoint.transform.position;
        Quaternion targetRot = previousWaypoint.transform.rotation;

        StartCoroutine(SetCarSteadyAt(targetPos, targetRot));
    }
    void SetUpCar()
    {
        Debug.Log("Setting up car...");

        //Put head position at the right place
        
        if (StaticSceneManager.calibratedUsingHands)
        {
            driverView.position = steeringWheel.transform.position;
            driverView.position -= steeringWheel.transform.forward * StaticSceneManager.driverViewHorizontalDistance;
            driverView.position += Vector3.up * StaticSceneManager.driverViewVerticalDistance;
        }

        //Put car in right position
        Transform startLocation = activeNavigationHelper.GetStartPointNavigation();
        
        car.SetNewNavigation(activeExperiment.navigation);

        StartCoroutine(SetCarSteadyAt(startLocation.position, startLocation.rotation));
    }
    void GoToCar()
    {
        Debug.Log("Returning to car...");
        SetCameraPosition(driverView.position, driverView.rotation);
    }
    void ProcessUserInputTargetDetection()
    {
        //Double inputs within thresholdUserInput time are discarded
        if (Time.time < (lastUserInputTime + thresholdUserInput)) { return; }
        lastUserInputTime = Time.time;
        //if there is a target visible which has not already been detected
        List<Target> targetList = activeNavigationHelper.GetActiveTargets();
        List<Target> visibleTargets = new List<Target>();
        int targetCount = 0;

        //Check if there are any visible targets
        foreach (Target target in targetList)
        {
            if (target.HasBeenLookedAt()) { visibleTargets.Add(target); }
            else if (TargetIsVisible(target, maxNumberOfRandomRayHits))
            {
                visibleTargets.Add(target);
                targetCount++;
                Debug.Log($"{target.GetID()} visible...");
            }
        }

        if (targetCount == 0) { dataManager.AddFalseAlarm(); }
        else if (targetCount == 1) { dataManager.AddTrueAlarm(visibleTargets[0]); visibleTargets[0].SetDetected(activeExperiment.experimentTime); }
        else
        {
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
                if (target.fixationTime > mostRecentTime)
                {
                    targetChosen = target;
                    mostRecentTime = target.fixationTime;
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

            dataManager.AddTrueAlarm(targetChosen);
            targetChosen.SetDetected(activeExperiment.experimentTime);
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
    private bool PassedTarget(Target target)
    {
        //Passed target if 
        //(1) passes the plane made by the waypoint and its forward direction. 
        // plane equation is A(x-a) + B(y-b) + C(z-c) = 0 = dot(Normal, planePoint - targetPoint)
        // Where normal vector = <A,B,Z>
        // pos = the cars position (x,y,z,)
        // a point on the plane Q= (a,b,c) i.e., target position

        float sign = Vector3.Dot(CameraTransform().forward, (CameraTransform().position - target.transform.position));
        if (sign >= 0) { return true; }
        else { return false; }
    }
    bool TargetIsVisible(Target target, int maxNumberOfRayHits)
    {
        //We will cast rays to the outer edges of the sphere (the edges are determined based on how we are looking towards the sphere)
        //I.e., with the perpendicular vector to the looking direction of the sphere

        bool isVisible = false;
        Vector3 direction = target.transform.position - CameraTransform().position;
        Vector3 currentDirection;
        RaycastHit hit;
        float targetRadius = target.GetComponent<SphereCollider>().radius;

        //If in front of camera we do raycast
        if (!PassedTarget(target))
        {
            //Vary the location of the raycast over the edge of the potentially visible target
            for (int i = 0; i < maxNumberOfRayHits; i++)
            {
                Vector3 randomPerpendicularDirection = GetRandomPerpendicularVector(direction);
                currentDirection = (target.transform.position + randomPerpendicularDirection * targetRadius) - CameraTransform().position;

                if (Physics.Raycast(CameraTransform().position, currentDirection, out hit, 10000f, ~layerToIgnoreForTargetDetection))
                {
                    Debug.DrawRay(CameraTransform().position, currentDirection, Color.green);
                    if (hit.collider.gameObject.tag == "Target")
                    {
                        Debug.DrawLine(CameraTransform().position, CameraTransform().position + currentDirection * 500, Color.cyan, Time.deltaTime, false);
                        isVisible = true;
                        break;
                    }
                }
            }
        }
        return isVisible;
    }
    void SetTargetVisibilityTime()
    {
        //Number of ray hits to be used. We user a smaller amount than when the user actually presses the detection button. Since this function is called many times in Update() 
        int numberOfRandomRayHits = 5;
        foreach (Target target in activeNavigationHelper.GetActiveTargets())
        {
            //If we didnt set start v isibility timer yet 
            if (target.startTimeVisible == target.defaultVisibilityTime)
            {
                if (TargetIsVisible(target, numberOfRandomRayHits))
                {
                    Debug.Log($"Target {target.GetID()} became visible at {activeExperiment.experimentTime}s ...");
                    target.startTimeVisible = activeExperiment.experimentTime;
                }
            }
        }
    }
    void SetCameraPosition(Vector3 goalPos, Quaternion goalRot)
    {
        player.position = goalPos;
        player.rotation = goalRot;
    }
    private bool UserInput()
    {
        //only sends true once every 0.1 seconds (axis returns 1 for multiple frames when a button is clicked)
        if ((userInputTime + userInputThresholdTime) > Time.time) { return false; }
        if (Input.GetAxis(ParticpantInputAxisLeft) == 1 || Input.GetAxis(ParticpantInputAxisRight) == 1) { userInputTime = Time.time; return true; }
        else { return false; }
    }
    private void ActivateMirrorCameras()
    {
        rearViewMirror.enabled = true; rearViewMirror.cullingMask = -1; // -1 == everything

        rightMirror.enabled = true; rightMirror.cullingMask = -1;

        leftMirror.enabled = true; leftMirror.cullingMask = -1;
    }
    private void SetColors()
    {
        navigationColor.a = activeExperiment.transparency;
        conformal.color = navigationColor;

        foreach (Material material in HUDMaterials.GetMaterials())
        {
            material.color = navigationColor;
        }
    }
    private void SetDataManager()
    {
        //Get attatched XMLManager
        dataManager = gameObject.GetComponent<DataLogger>();
        //Throw error if we dont have an xmlManager
        if (dataManager == null) { throw new System.Exception("Error in Experiment Manager -> A XMLManager should be attatched if you want to save data..."); }

        if (StaticSceneManager.saveData) { dataManager.StartNewMeasurement(); }
    }
    void SetCarControlInput()
    {
        VehiclePhysics.VPStandardInput carController = car.GetComponent<VehiclePhysics.VPStandardInput>();
        if (camType == MyCameraType.Normal)
        {
            carController.steerAxis = SteerWithKeyboard;
            carController.throttleAndBrakeAxis = GasWithKeyboard;
        }
        else
        {
            carController.steerAxis = Steer;
            carController.throttleAndBrakeAxis = Gas;
        }
    }
    void SetCamera()
    {
       /* blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(0f, 0f, true);
        //Get the to be used camera, destroy the others, and set this camera to be used for the reflection script of the mirrors
        if (camType == MyCameraType.Leap || camType == MyCameraType.Varjo) {
            player = leapRig;
            blackOutScreen = CameraTransform().Find("Canvas").Find("BlackOutScreen").GetComponent<UnityEngine.UI.Image>();
            blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(0f, 0f, true);
        }
        else if (camType == MyCameraType.Normal) { player = normalCam; }*/

        RearMirrorsReflection[] reflectionScript = car.GetComponentsInChildren<RearMirrorsReflection>(true);
        if (reflectionScript != null && player != null) { reflectionScript[0].head = CameraTransform(); }
        else { Debug.Log("Could not set head position for mirro reflection script..."); }
    }
    IEnumerator SetCarSteadyAt(Vector3 targetPos, Quaternion targetRot)
    {
        //Somehow car did some back flips when not keeping it steady for some time after repositioning.....
        float step = 0.01f;
        float totalSeconds = 0.25f;
        float count = 0;

        while (count < totalSeconds)
        {
            car.gameObject.transform.position = targetPos;
            car.gameObject.transform.rotation = targetRot;

            car.GetComponent<Rigidbody>().velocity = Vector3.zero;
            car.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

            count += step;
            yield return new WaitForSeconds(step);
        }
    }
}

[System.Serializable]
public class Experiment
{
    public Transform navigation;
    public NavigationHelper navigationHelper;
    public float experimentTime;
    public NavigationType navigationType;
    public float transparency = 0.3f;

    public bool active;

    public Experiment(Transform _navigation, NavigationType _navigationType, float _transparency, bool _active)
    {
        active = _active;
        navigation = _navigation;
        navigationType = _navigationType;
        transparency = _transparency;
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        experimentTime = 0f;
    }
    public void SetActive(bool _active)
    {
        active = _active;
        navigation.gameObject.SetActive(_active);
    }
}
[System.Serializable]
public class ExperimentSetting
{
    public Transform navigation;
    public NavigationType navigationType;
    [Range(0.01f, 1f)]
    public float transparency = 0.3f;
}
