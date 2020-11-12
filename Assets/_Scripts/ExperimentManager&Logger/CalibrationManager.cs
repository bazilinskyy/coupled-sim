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
        List<Target> visibleTargets = new List<Target>();
        int targetCount = 0;

        //Check if there are any visible targets
        foreach (Target target in ActiveTargets())
        {
            if (target.HasBeenLookedAt()) { visibleTargets.Add(target); }
            else if (TargetIsVisible(target, maxNumberOfRandomRayHits))
            {
                visibleTargets.Add(target);
                targetCount++;
                //Debug.Log($"{target.GetID()} visible...")d;
            }
        }

        if (targetCount == 0) { }
        else if (targetCount == 1) {  visibleTargets[0].SetDetected(1f); }
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

            targetChosen.SetDetected(1f);
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

                if (Physics.Raycast(CameraTransform().position, currentDirection, out hit, 10000f))
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
  
    public Transform CameraTransform()
    {
        if (camType == MyCameraType.Leap) { return player.Find("VarjoCameraRig").Find("VarjoCamera"); }
        else if(camType == MyCameraType.Varjo) { return player.Find("VarjoCamera"); }
        else if (camType == MyCameraType.Normal) { return player; }
        else { throw new System.Exception("Error in retrieving used camera transform in Experiment Manager.cs..."); }
    }
}
