using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

[RequireComponent(typeof(XMLManager))]
public class ExperimentManager : MonoBehaviour
{
    //Attachted to the object with the experiment manager should be a XMLManager for handling the saving of the data.
    [Header("Experiment Input")]
    public string subjectName="You";
    public int experimentStartPoint;
    public bool saveData=true;

    [Header("Inputs")]
    public KeyCode keyUserReady = KeyCode.F1;
    public KeyCode keyTargetDetected = KeyCode.Space;

    [Header("GameObejcts")]
    public Transform navigationRoot;
    public Navigator car;

    //The camera used and head position inside the car
    public bool usingVarjo = false;
    public Transform normalCam;
    public Transform varjoCamRig;
    public Transform headPosition;
    private Transform usedCam;

    private Transform originalParentCamera;

    //UI objects
    public Text UIText;
    //Scriptable gameState object
    public GameState gameState;
    //Waiting room transform
    public Transform waitingRoom;

    // expriment objects and lists
    private Experiment activeExperiment;
    private List<Experiment> experimentList;
    private NavigationHelper activeNavigationHelper;
    //The data manger handling the saving of vehicle and target detection data Should be added to the experiment manager object 
    private XMLManager dataManager;
    //Maximum raycasts used in determining visbility:  We use Physics.RayCast to check if we can see the target. We cast this to a random positin on the targets edge to see if it is partly visible.
    private int maxRandomRayHits = 40;
    // Start is called before the first frame update
    void Awake()
    { 
        if (usingVarjo)
        {
            headPosition.position = headPosition.parent.position + new Vector3(0,0.2f, 0.1f);
            headPosition.rotation  = varjoCamRig.rotation;
            usedCam = varjoCamRig;
        }
        else { usedCam = normalCam; }
        
        
        //Set gamestate to waiting
        gameState.SetGameState(GameStates.Waiting);
        
        //Get main camera to waiting room
        GoToWaitingRoom();

        //Check exerimentStartPoint input
        if(experimentStartPoint >= navigationRoot.transform.childCount) { throw new System.Exception("Start point should be lower than the number of navigations available"); }
        
        //Set experiment
        //Set up all experiments
        SetUpExperiments(experimentStartPoint);

        //Set DataManager
        SetDataManager();

        SetUpCar();
    }
    void Update()
    {
        if (gameState.isTransition())
        {
            gameState.SetGameState(GameStates.Waiting);
            //should dim lights in here and than go to the waiting room
            GoToWaitingRoom();
        }
        if (gameState.isWaiting())
        {
            if (Input.GetKeyDown(keyUserReady))
            {
                Debug.Log("User is ready...");
                //should dim lights in here and than start experiment
                gameState.SetGameState(GameStates.Experiment);
                ReturnToCar();
            }
        }

        //During experiment check for target deteciton key to be pressed
        if (gameState.isExperiment()) {
            if (Input.GetKeyDown(keyTargetDetected))
            {
                ProcessUserInputTargetDetection();
            }
        }

        //if we finished a navigation we go to the waiting room
        if (NavigationFinished() && gameState.isExperiment())
         {
            //Save the data
            HandleData();
            //Set gamestate to transition
            gameState.SetGameState(GameStates.Transition);
            //Should dim lights and then go to waiting room
            
            Debug.Log("Navigation finished...");
            if (IsNextNavigation())
            {
                Debug.Log("Loading next experiment...");
                SetupNextExperiment();
            }
            else
            {
                StartCoroutine(RenderEndSimulation());
                Debug.Log("Simulation finished");
                activeExperiment = null;
            }
        }
    }
    private void SetOrderedExperiments()
    {
        experimentList = new List<Experiment>();
        List<Experiment> unOrderedList = new List<Experiment>();

        foreach (Transform child in navigationRoot)
        {
            Experiment experiment = new Experiment(child, false); ;
            unOrderedList.Add(experiment);
        }
        experimentList = unOrderedList.OrderBy(a => a.navigation.name).ToList();
    }
    void SetUpExperiments(int experimentStartPoint)
    {
        SetOrderedExperiments();
        // Set active experiment
        activeExperiment = experimentList[experimentStartPoint];
        activeExperiment.SetActive(true);
        activeNavigationHelper = activeExperiment.navigationHelper;
    }
    bool NavigationFinished()
    {
        //Checks wheter current navigation path is finished.
        if(car.GetCurrentTarget().operation == Operation.EndPoint && car.navigationFinished && car.navigation == activeExperiment.navigation) { return true; }
        else { return false; }
    }
    void SetupNextExperiment()
    {
        //Activate next experiment (should only get here if we actually hjave anext experiment)
        ActivateNextExperiment();

        //set up car (Should always be after activating the new experiment!)
        SetUpCar();

        //TODO do stuff with XML manager
        SetUpDataManagerNewExperiment();
    }
    void SetUpCar()
    {
        Debug.Log("Setting up car...");
        //Set new navigation for car

        car.SetNewNavigation(activeExperiment.navigation);


        //Put car in right position
        Transform startLocation = activeNavigationHelper.GetStartPointNavigation();

        car.transform.position = startLocation.position;
        car.transform.rotation = startLocation.rotation;
    }
    IEnumerator RenderStartScreenText()
    {
        Debug.Log("Rendering startscreen...");
        UIText.GetComponent<CanvasRenderer>().SetAlpha(0f);
        yield return new WaitForSeconds(1.0f);
        UIText.CrossFadeAlpha(1, 2.5f, false);
    }
    void ReturnToCar()
    {
        Debug.Log("Returning to car...");
        usedCam.position = headPosition.position;
        usedCam.rotation = headPosition.rotation;
        //Only do rotation when not using varjo
        //if (!usingVarjo) { usedCam.transform.rotation = headPosition.rotation; }
        usedCam.SetParent(originalParentCamera);
        //usedCam.transform.Rotate(new Vector3(0, 1f, 0), -90);
    }
    void GoToWaitingRoom()
    {
        Debug.Log("Going to waiting room...");
        if (originalParentCamera == null) { originalParentCamera = usedCam.parent; }
        
        usedCam.position = waitingRoom.position + new Vector3(0, 3f, -3f);
        usedCam.rotation = waitingRoom.rotation;
        if (usingVarjo) { usedCam.Rotate(new Vector3(0, 1f, 0), -90); }
        usedCam.SetParent(waitingRoom);
        StartCoroutine(RenderStartScreenText());
    }
    IEnumerator RenderEndSimulation()
    {
        UIText.GetComponent<CanvasRenderer>().SetAlpha(0f);
        yield return new WaitForSeconds(2.0f);
        UIText.text = "Thanks for participating!";
        UIText.CrossFadeAlpha(1, 2.5f, false);

    }
    bool IsNextNavigation()
    {
        //If this is not the last experiment in the list return true else false
        int index = GetIndexCurrentExperiment();

        if (index != (experimentList.Count - 1)) { return true; }
        else { return false; }
    }
    int GetIndexCurrentExperiment()
    {
        return experimentList.FindIndex(a => a.navigation == activeExperiment.navigation);
    }
    void ActivateNextExperiment()
    {
        //Deactivate current experiment
        activeExperiment.SetActive(false);

        int currentIndex = GetIndexCurrentExperiment();
        activeExperiment = experimentList[currentIndex + 1];
        if (activeExperiment == null) { throw new System.Exception("Something went wrong in ExperimentManager --> ActivateNextExperiment "); }

        activeExperiment.SetActive(true);
        activeNavigationHelper = activeExperiment.navigation.GetComponent<NavigationHelper>();

        Debug.Log("Experiment " + activeExperiment.navigation.name + " loaded");
    }
    void ProcessUserInputTargetDetection()
    {
        //if there is a target visible which has not already been detected
        List<GameObject> targetList = GetActiveTargets();
        GameObject seenTarget = null;
        int targetCount = 0;

        //Check if there are any visible targets
        foreach (GameObject target in targetList)
        {
            Debug.Log("Target: " + target.name);
            if (VisibleTarget(target))
            {
                seenTarget = target;
                targetCount++;
            }
        }
        //We do not accept multiple visible targets at the same time.
        if (targetCount == 0)
        {
            dataManager.AddFalseAlarm();
        }
        else if (targetCount == 1)
        {
            dataManager.AddTrueAlarm(seenTarget);
            seenTarget.GetComponent<Target>().SetDetected();

        }
        else
        {
            throw new System.Exception("Counting two visible targets.... This is not implemented yet");
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
    bool VisibleTarget(GameObject target)
    {
        //We will cast rays to the outer edges of the sphere (the edges are determined based on how we are looking towards the sphere)
        //I.e., with the perpendicular vector to the looking direction of the sphere

        bool isVisible = false;
        Vector3 direction = target.transform.position - usedCam.transform.position;
        Vector3 currentDirection;
        RaycastHit hit;
        float targetRadius = target.GetComponent<SphereCollider>().radius;

        //If renderer.isVisible we also got to check if it is not occluided by any other objects
        if (target.GetComponent<Renderer>().isVisible)
        {
            //Vary the location of the raycast over the edge of the potentially visible target
            for (int i = 0; i < maxRandomRayHits; i++)
            {
                Vector3 randomPerpendicularDirection = GetRandomPerpendicularVector(direction);
                currentDirection = (target.transform.position + randomPerpendicularDirection * targetRadius) - usedCam.transform.position;

                if (Physics.Raycast(usedCam.transform.position, currentDirection, out hit, 10000f, Physics.AllLayers))
                {
                    Debug.DrawRay(usedCam.transform.position, currentDirection, Color.green);
                    // print("HIT " + hit.collider.gameObject.name);
                    if (hit.collider.gameObject.tag == "Target")
                    {
                        Debug.DrawLine(usedCam.transform.position, usedCam.transform.position + currentDirection * 500, Color.cyan, Time.deltaTime, false);
                        print(target.name + " is visible");
                        isVisible = true;
                        break;
                    }

                }
            }
        }

        return isVisible;
    }
    List<GameObject> GetActiveTargets()
    {
        List<GameObject> targetList = new List<GameObject>();
        foreach (Transform child in activeExperiment.navigation.transform)
        {
            Waypoint waypoint = child.GetComponent<Waypoint>();
            //Means it is being rendered as well as its targets
            if (waypoint != null && waypoint.renderMe)
            {
                foreach (GameObject target in waypoint.GetTargets())
                {
                    //If not detected yet --> Add to potential target list
                    if (!target.GetComponent<Target>().IsDetected())
                    {
                        targetList.Add(target);
                    }
                }
            }
        }

        return targetList;
    }
    private void SetDataManager()
    {
        //Get attatched XMLManager
        dataManager = gameObject.GetComponent<XMLManager>(); 
        //Throw error if we dont have an xmlManager
        if(dataManager == null) { throw new System.Exception("Error in Experiment Manager -> A XMLManager should be attatched if you want to save data..."); }

        dataManager.SetAllInputs(car.gameObject, activeExperiment.navigation.transform, subjectName);
    }
    private void SetUpDataManagerNewExperiment()
    {
        //Skip if we dont save data
        if (!saveData) { return; }
        dataManager.SetNavigation(activeExperiment.navigation.transform);
        dataManager.StartNewMeasurement();
    }
    private void HandleData()
    {
        if (saveData) { dataManager.SaveData(); }
    }
}
[System.Serializable]
public class Experiment
{
    public Transform navigation;
    private bool active;
    public NavigationHelper navigationHelper;
    public Experiment( Transform _navigation, bool _active)
    {
        active = _active;
        navigation = _navigation;
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        navigationHelper.RenderAllWaypoints(_active);
    }
    public void SetActive(bool _active)
    {
        active = _active;
        navigation.gameObject.SetActive(_active);
    }

}
 