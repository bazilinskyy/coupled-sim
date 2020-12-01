using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
public class MainManager : MonoBehaviour
{

    public MyCameraType camType = MyCameraType.Leap;

    public List<MyExperimentSetting> experiments;

    public GameObject environment;
    public bool debug = true;

    public bool makeVirtualCable = true;
    public bool renderHUD = true;

    public string subjectDataFolder;
    public string subjectName = "Dummy";
    public int experimentOrder;

    public bool automateSpeed = true;
    public bool saveData = false;
    public float animationTime = 2.5f;

    public float FoVCamera = 87;
    public bool calibratedUsingHands = false;
    public float driverViewHorizontalDistance = 0f;
    public float driverViewVerticalDistance = 0f;
    public float driverViewSideDistance = 0f;

    [Header("Inputs")]
    public KeyCode myPermission = KeyCode.F1;
    public KeyCode resetHeadPosition = KeyCode.F2;
    public KeyCode spawnSteeringWheel = KeyCode.F3;
    public KeyCode calibrateGaze = KeyCode.F4;
    public KeyCode resetExperiment = KeyCode.Escape;

    public KeyCode toggleDriving = KeyCode.D;

    public KeyCode toggleSymbology = KeyCode.T;

    public KeyCode setToLastWaypoint = KeyCode.Backspace;
    public KeyCode inputNameKey = KeyCode.I;

    public KeyCode saveTheData = KeyCode.F7;

    private string environmentScene = "Environment";
    private string waitingRoomScene = "WaitingScene";
    private string drivingPractiseScene = "DrivingPractiseScene";
    private string targetScene = "Targets";
    private string calibrationScene = "CalibrationScene";
    private string experimentScene = "newEnvironment";

    [Header("Car Controls")]

    public string ParticpantInputAxisLeft = "SteerButtonLeft";
    public string ParticpantInputAxisRight = "SteerButtonRight";

    public string GasWithKeyboard = "GasKeyBoard";
    public string SteerWithKeyboard = "SteerKeyBoard";
    public string BrakeWithKeyboard = "BrakeKeyBoard";

    public string Gas = "GasKeyBoardas";
    public string Steer = "Steer";
    public string Brake = "BrakeKeyBoard";

    public string[] sceneArray;
    public int experimentIndex = 0;
    public string currentDrivingScene;

    private GameObject player;
    UnityEngine.UI.Image blackOutScreen;
    
    private bool loading = false;
    private void Awake()
    {
        player = gameObject;

        //Fade screen from black to transparent on awake
        blackOutScreen = GameObject.FindGameObjectWithTag("BlackOutScreen").GetComponent<UnityEngine.UI.Image>();
        blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(1f, 0f, true);
        blackOutScreen.CrossFadeAlpha(0, animationTime * 3, false);

        //Varjo stuff
        Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL);
        Varjo.VarjoPlugin.InitGaze();
        
        //Read general settings file
        ReadCSVSettingsFile();
        Debug.Log($"Subject Name = {subjectName}...");

        //Set input of data log folder:
        subjectDataFolder = string.Join("/", System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop).Replace("\\", "/"), "Data", System.DateTime.Now.ToString("MM-dd_HH-mm") + "_" + subjectName);
        System.IO.Directory.CreateDirectory(subjectDataFolder);
        
        Debug.Log($"Created {subjectDataFolder}...");

        AddDummyExperiments();
    }
    void AddDummyExperiments()
    {
        experiments = new List<MyExperimentSetting>();
        float random;
        for (int i = 0; i < 5; i++)
        { 
            MyExperimentSetting setting = new MyExperimentSetting();
            List<TurnType> turns = new List<TurnType>();

            
            for (int j = 0; j < 3; j++)
            {
                random = Random.Range(0, 100);
                if(random < 50) { turns.Add(TurnType.Left); }
                else { turns.Add(TurnType.Right); }

            }

            turns.Add(TurnType.EndPoint);
            setting.turns = turns;

            random = Random.Range(0, 100);
            if (random < 50) { setting.navigationType = NavigationType.VirtualCable; }
            else { setting.navigationType = NavigationType.HUD_high; }
            
            setting.experimentName += i.ToString();

            experiments.Add(setting);
        }
        
    }
    public MyExperimentSetting GetCurrentExperimentSettings()
    {
        return experiments[experimentIndex];
    }
    public bool IsNextExperiment()
    {
        if (experimentIndex < experiments.Count()) { return true; }
        else { return false; }
    }
    public int GetExperimentNumber()
    {
        return experimentIndex+1;
    }
    public MyExperimentSetting GetExperimentSettings() { return experiments[experimentIndex]; }
    public void SetCalibrationDistances(float horizontal, float vertical, float side)
    {
        calibratedUsingHands = true;
        driverViewHorizontalDistance = horizontal;
        driverViewVerticalDistance = vertical;
        driverViewSideDistance = side;
    }
    public bool ReadCSVSettingsFile()
    {
        string fileName = "experimentSettings.csv";
        string filePath = Application.dataPath + "/" + fileName;

        Debug.Log($"Trying to read {filePath}...");

        try
        {
            string fileData = System.IO.File.ReadAllText(filePath);
            string[] lines = fileData.Split('\n');

            //Data: [0] = Name, [1] = order
            string[] lineData = lines[0].Trim().Split(';');
            subjectName = lineData[0];
            experimentOrder = int.Parse(lineData[1]);

            return true;
        }
        catch { Debug.Log("Could not read file...."); return false; }
    }
    public string GetNextScene() { return calibrationScene; }
    public void CalibrationEnded()
    {
        if (!loading) { bool loadWhileFading = true; StartCoroutine(LoadSceneAsync(experimentScene, loadWhileFading)); }
    }
    public bool ExperimentFinished()
    {
        if (experimentIndex > experiments.Count()) { return false; }
        else { return true; }
    }
    public void ExperimentEnded()
    {
        if (!loading) { StartCoroutine(LoadSceneAsync(waitingRoomScene)); }
    }
    public void LoadNextExperiment()
    {
        if (!loading)
        {
            bool loadWhileFading = true;
            StartCoroutine(LoadSceneAsync(experimentScene, loadWhileFading));
            experimentIndex++;
        }
    }
    public void AddTargetScene() { SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive); }
    public void LoadCalibrationScene()
    {
        SceneManager.LoadSceneAsync(calibrationScene, LoadSceneMode.Single);
    }
    IEnumerator LoadSceneAsync(string scene, bool loadWhileFading = false)
    {
        loading = true; Debug.Log($"Loading {scene}...");

        blackOutScreen.CrossFadeAlpha(1f, animationTime, false);

        //Skip this waiting if we load while fading
        yield return new WaitForSeconds(animationTime);

        player.transform.parent = null;
        DontDestroyOnLoad(player);

        AsyncOperation operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        
        while (!operation.isDone) { yield return null; }

        blackOutScreen.CrossFadeAlpha(0, animationTime, false);

        loading = false;
    }
    public void MovePlayer(Transform position)
    {
        player.transform.parent = position;
        player.transform.position = position.position;
        player.transform.rotation = position.rotation;
    }

}

[System.Serializable]
public class MyExperimentSetting
{
    public string experimentName = "Experiment";
    public List<TurnType> turns;
    public NavigationType navigationType;

}
