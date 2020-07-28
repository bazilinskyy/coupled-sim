using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExperimentManager : MonoBehaviour
{
    
    public List<Experiment> experimentList;
    public Navigator car;
    private Experiment activeExperiment;
    private NavigationManager activeNavigationManager;

    public XMLManager XMLManager;
    

    public bool usingVarjo;
    public Transform varjoCam;
    public Transform normalCam;
    private Transform usedCam;
    //UI objects
    public Text UIText;
    public KeyCode key = KeyCode.F1;

    private GameStates gameState;
    public GameStates _gameState;

    public Transform waitingRoom;

    public enum GameStates{
        Experiment,
        Waiting,
        Transition,
        Ended
    }

    // Start is called before the first frame update
    void Awake()
    {
        
        //_gameState = GameStates.Experiment;
        if (usingVarjo) { usedCam = varjoCam; }
        else { usedCam = normalCam; }

        
        GoToWaitingRoom();
        SetupFirstExperiment();
        
    }

    // Update is called once per frame
    void Update()
    {
       
        if (gameState == GameStates.Transition)
        {
            gameState = GameStates.Waiting;
            GoToWaitingRoom();
        }
        
        if (gameState == GameStates.Waiting)
        {
            if (Input.GetKeyDown(key)){
                //should dim lights in here and than start experiment
                gameState = GameStates.Experiment;
                StartExperiment();
            }
        }
        //if we finished a navigation we go to the waiting room
        if (NavigationFinished())
         {
            gameState = GameStates.Transition;
            //Should dim lights and then go to waiting room
            
            Debug.Log("Navigation finished...");
            if (IsNextNavigation())
            {
                Debug.Log("Loading next experiment...");
                //RenderUI
                //StartCoroutine(RenderStartScreenText());
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

    void SetupFirstExperiment()
    {
        activeExperiment = experimentList[0];
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
        //Set current navigation to inactive
        activeExperiment.SetActive(false);

        //Activate next experiment
        ActivateNextExperiment();
        
        //TODO do stuff with XML manager
        //change rootwaypoints and datafile names etc etc
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
        usedCam.transform.position = car.transform.position + new Vector3(0,0.2f,0);
        usedCam.transform.rotation = car.transform.rotation;
        usedCam.transform.Rotate(new Vector3(0, 1f, 0), -90);

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

    void StartExperiment()
    {
        Debug.Log("Starting Experiment....");

        //Set new navigation for car
        car.navigation = activeExperiment.navigation;

        //Set new waypoint target
        Waypoint target = activeNavigationManager.GetFirstTarget();
        car.target = target;

        //Put car in right position
        Transform startLocation = activeNavigationManager.GetStartPointNavigation();
        car.transform.position = startLocation.position;
        car.transform.rotation = startLocation.rotation;

        ReturnToCar();
        
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
        int currentIndex = GetIndexCurrentExperiment();
        activeExperiment = experimentList[currentIndex + 1];
        if (activeExperiment == null) { throw new System.Exception("Something went wrong in ExperimentManager --> ActivateNextExperiment "); }

        activeExperiment.SetActive(true);
        activeNavigationManager = activeExperiment.navigation.GetComponent<NavigationManager>();
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
 