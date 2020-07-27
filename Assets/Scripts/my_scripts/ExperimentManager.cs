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
    public GameObject startScreen;

    //UI objects
    private Image background;
    private Button button;
    private Text text;
    //private Navigator navigator;


    // Start is called before the first frame update
    void Awake()
    {
        SetUIObjects();
        SetupFirstExperiment();
    }

    // Update is called once per frame
    void Update()
    {
        if (NavigationFinished())
        {
            Debug.Log("Navigation finished...");
            if (IsNextNavigation())
            {
                Debug.Log("Loading next experiment...");
                //RenderUI
                StartCoroutine(RenderStartScreen());
                SetupNextExperiment();
            }
            else
            {
                StartCoroutine(RenderEndSimulation());
                EndSimulation();
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
    
    void EndSimulation()
    {
        Debug.Log("Simulation finished");
        activeExperiment = null;

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

    void SetUIObjects()
    {
        Debug.Log("Setting up UI objects...");

        //Find background object
        background = startScreen.transform.GetChild(0).gameObject.GetComponent<Image>();
        if (background.gameObject.name != "Background") { throw new System.Exception("Something went wrong in Experiment Manager --> RenderStartScreen()"); }

        //set background to black
        background.color = Color.black;


        button = startScreen.transform.GetChild(1).GetComponent<Button>();
        if (button.gameObject.name != "Button") { throw new System.Exception("Something went wrong in Experiment Manager --> RenderStartScreen()"); }
        
        //Add event listener to button
        button.onClick.AddListener(ClickedStartExperiment);


        //find text object
        text = startScreen.transform.GetChild(2).GetComponent<Text>();
        if (text.gameObject.name != "Text") { throw new System.Exception("Something went wrong in Experiment Manager --> RenderStartScreen()"); }
        text.gameObject.SetActive(false);
    }
    IEnumerator RenderStartScreen()
    {
        Debug.Log("Rendering startscreen...");
        background.CrossFadeAlpha(1, 2.5f, false);

        yield return new WaitForSeconds(2.0f);
        text.gameObject.SetActive(true);
        text.CrossFadeAlpha(1, 2f, false);
        yield return new WaitForSeconds(2f);
        button.gameObject.SetActive(true);
   
    }

    
    void ClickedStartExperiment()
    {
        Debug.Log("Clicked button...");
        button.gameObject.SetActive(false);
        background.CrossFadeAlpha(0, 2.5f, false);
        text.CrossFadeAlpha(0, 2f, false);
        StartExperiment();
    }

    IEnumerator RenderEndSimulation()
    {
        background.CrossFadeAlpha(1, 2.5f, false);

        yield return new WaitForSeconds(2.0f);
        text.text = "End of experiment\nThank you!";
        text.gameObject.SetActive(true);
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
 