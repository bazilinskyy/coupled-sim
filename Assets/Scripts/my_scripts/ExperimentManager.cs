using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class ExperimentManager : MonoBehaviour
{
    public Transform navigationRoot;
    private List<Experiment> experimentList;
    public Navigator car;
    private Experiment activeExperiment;
    private NavigationManager activeNavigationManager;

    public XMLManager XMLManager;
    

    public bool usingVarjo;
    public Transform varjoCam;
    public Transform normalCam;
    private Transform usedCam;
    public Transform headPosition;
    //UI objects
    public Text UIText;
    public KeyCode key = KeyCode.F1;

    public GameState gameState;
    

    public Transform waitingRoom;

    // Start is called before the first frame update
    void Awake()
    {
        
        //_gameState = GameStates.Experiment;
        if (usingVarjo) { usedCam = varjoCam; }
        else { usedCam = normalCam; }

        gameState.SetGameState(GameStates.Waiting);

        GoToWaitingRoom();
        SetUpExperiments();
        SetUpCar();

        
    }

    // Update is called once per frame
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
            if (Input.GetKeyDown(key)){
                //should dim lights in here and than start experiment
                gameState.SetGameState(GameStates.Experiment);
                ReturnToCar(); 
            }
        }
        //if we finished a navigation we go to the waiting room
        if (NavigationFinished())
         {
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

    void SetUpExperiments()
    {
        //Get ordered lit of navigations
        experimentList = new List<Experiment>();
        List<Experiment> unOrderedList = new List<Experiment>();

        foreach ( Transform child in navigationRoot)
        {
            Experiment experiment = new Experiment(); ;
            experiment.navigation = child.gameObject;
            experiment.SetActive(false);
            unOrderedList.Add(experiment);
        }
        experimentList = unOrderedList.OrderBy(a => a.navigation.name).ToList();

        // Set active experiment
        activeExperiment = experimentList[0];
        activeExperiment.SetActive(true);
        activeNavigationManager = activeExperiment.navigation.GetComponent<NavigationManager>();
    }

    bool NavigationFinished()
    {
        //Checks wheter current navigation path is finished.
        if(car.target.operation == Operation.EndPoint && car.reachedTarget && car.navigation == activeExperiment.navigation) { return true; }
        else { return false; }
    }


    void SetupNextExperiment()
    {
        //Activate next experiment (should only get here if we actually hjave anext experiment)
        ActivateNextExperiment();

        //set up car (Should always be after activating the new experiment!)
        SetUpCar();


        //TODO do stuff with XML manager
        //change rootwaypoints and datafile names etc etc
    }

    void SetUpCar()
    {
        Debug.Log("Setting up car...");
        //Set new navigation for car
        car.navigation = activeExperiment.navigation;

        //Set new waypoint target
        Waypoint target = activeNavigationManager.GetFirstTarget();
        car.target = target;

        car.reachedTarget = false;

        //Put car in right position
        Transform startLocation = activeNavigationManager.GetStartPointNavigation();

        car.transform.position = startLocation.position;
        car.transform.rotation = startLocation.rotation;
    }
    IEnumerator RenderStartScreenText()
    {
        Debug.Log("Rendering startscreen...");
        UIText.GetComponent<CanvasRenderer>().SetAlpha(0f);
        yield return new WaitForSeconds(2.0f);
        UIText.CrossFadeAlpha(1, 2.5f, false);
    }
    void ReturnToCar()
    {
        Debug.Log("Returning to car...");
        usedCam.transform.position = headPosition.position;
        usedCam.transform.rotation = headPosition.rotation;
        //usedCam.transform.Rotate(new Vector3(0, 1f, 0), -90);

    }

    void GoToWaitingRoom()
    {
        Debug.Log("Going to waiting room...");
        usedCam.transform.position = waitingRoom.transform.position + new Vector3(0, 1f, 0);

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
        activeNavigationManager = activeExperiment.navigation.GetComponent<NavigationManager>();

        Debug.Log("Experiment " + activeExperiment.navigation.name + " loaded");
    }
}
[System.Serializable]
public class Experiment
{
    public GameObject navigation;
    private bool active;
    public void SetActive(bool _active)
    {
        active = _active;
        navigation.GetComponent<MeshRenderer>().enabled = _active;
    }

}
 