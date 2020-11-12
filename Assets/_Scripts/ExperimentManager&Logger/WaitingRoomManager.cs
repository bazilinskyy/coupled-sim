using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaitingRoomManager : MonoBehaviour
{
    private MyCameraType camType;
    private MySceneLoader mySceneLoader;

    public TextMesh text;
    public Transform leapRig;
    public Transform normalCam;
    private Transform player;
    public ExperimentInput experimentInput;
    public UnityEngine.UI.Image blackOutScreen;

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

    private readonly int maxNumberOfRandomRayHits = 40;
    private float lastUserInputTime = 0f;
    public float thresholdUserInput = 0.15f; //The minimum time between user inputs (when within this time only the first one is used)

    private float userInputTime = 0f; private readonly float userInputThresholdTime = 0.2f;
    private void Start()
    {
        Debug.Log("Loaded waiting room...");
        StartingScene();
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(experimentInput.myPermission)) { mySceneLoader.LoadNextScene(); }
        
        //Looks for targets to appear in field of view and sets their visibility timer accordingly
        if (UserInput()) { ProcessUserInputTargetDetection(); }
    }
    void StartingScene()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        experimentInput = player.GetComponent<ExperimentInput>();

        DontDestroyOnLoad(player);

        blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(0f, 0f, true);
        GetVariablesFromSceneManager();

        SetText();
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
        else if (targetCount == 1) { visibleTargets[0].SetDetected(1f); }
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

}
