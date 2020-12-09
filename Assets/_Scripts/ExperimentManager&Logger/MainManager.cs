using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
public class MainManager : MonoBehaviour
{

    public MyCameraType camType = MyCameraType.Leap;

    public List<MainExperimentSetting> experiments;

    public bool debug = true;

    public bool makeVirtualCable = true;
    public bool renderHUD = true;

    public string subjectDataFolder ="nothing...";
    public string subjectName = "Dummy";
    public int experimentOrder;

    public bool automateSpeed = true;
    public bool saveData = false;
    public float animationTime = 2.5f;

    public float FoVCamera = 87;
    public bool calibratedUsingHands = false;
    public float driverViewZToSteeringWheel = 0f;
    public float driverViewYToSteeringWheel = 0f;
    public float driverViewXToSteeringWheel = 0f;

    [Header("Inputs")]
    private KeyCode myPermission = KeyCode.F1;
    private KeyCode resetHeadPosition = KeyCode.F2;
    private KeyCode spawnSteeringWheel = KeyCode.F3;
    private KeyCode calibrateGaze = KeyCode.F4;
    private KeyCode resetExperiment = KeyCode.Escape;

    private KeyCode toggleDriving = KeyCode.D;

    private KeyCode toggleSymbology = KeyCode.T;

    private KeyCode setToLastWaypoint = KeyCode.Backspace;
    private KeyCode inputNameKey = KeyCode.I;

    private KeyCode saveTheData = KeyCode.F7;

    private string waitingRoomScene = "WaitingScene";
    private string targetScene = "Targets";
    private string calibrationScene = "CalibrationScene";
    private string experimentScene = "ExperimentScene";

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
    

    private GameObject player;
    UnityEngine.UI.Image blackOutScreen;
    
    private bool loading = false; 
    private bool readyToSaveData = false;

    public KeyCode MyPermission { get => myPermission; set => myPermission = value; }
    public KeyCode ResetHeadPosition { get => resetHeadPosition; set => resetHeadPosition = value; }
    public KeyCode SpawnSteeringWheel { get => spawnSteeringWheel; set => spawnSteeringWheel = value; }
    public KeyCode CalibrateGaze { get => calibrateGaze; set => calibrateGaze = value; }
    public KeyCode ResetExperiment { get => resetExperiment; set => resetExperiment = value; }
    public KeyCode ToggleDriving { get => toggleDriving; set => toggleDriving = value; }
    public KeyCode ToggleSymbology { get => toggleSymbology; set => toggleSymbology = value; }
    public KeyCode SetToLastWaypoint { get => setToLastWaypoint; set => setToLastWaypoint = value; }
    public KeyCode InputNameKey { get => inputNameKey; set => inputNameKey = value; }
    public KeyCode SaveTheData { get => saveTheData; set => saveTheData = value; }

    private void Awake()
    {
        Random.InitState(42);
        
        player = gameObject;
        //Fade screen from black to transparent on awake
        blackOutScreen = GameObject.FindGameObjectWithTag("BlackOutScreen").GetComponent<UnityEngine.UI.Image>();
        blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(1f, 0f, true);
        blackOutScreen.CrossFadeAlpha(0, animationTime * 3, false);

        //Varjo stuff
        if (camType != MyCameraType.Normal)
        {
            Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL);
            Varjo.VarjoPlugin.InitGaze();
        }
        
        //Read general settings file
        ReadCSVSettingsFile();
        

        //Set input of data log folder:
        subjectDataFolder = string.Join("/", System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop).Replace("\\", "/"), "Data", System.DateTime.Now.ToString("MM-dd_HH-mm") + "_" + subjectName);
        System.IO.Directory.CreateDirectory(subjectDataFolder);

        Debug.Log($"Settings file received...\nSubject ID = {subjectName}, DataFolder: {subjectDataFolder}..."); 

        AddDummyExperiments();
    }
    void AddDummyExperiments()
    {
        experiments = new List<MainExperimentSetting>();
        float random;
        for (int i = 0; i < 5; i++)
        {
            MainExperimentSetting setting = new MainExperimentSetting();
            List<TurnType> turns = new List<TurnType>();


            for (int j = 0; j < 5; j++)
            {
                random = Random.Range(0, 100);
                if (random < 50) { turns.Add(TurnType.Left); }
                else { turns.Add(TurnType.Right); }

            }

            turns.Add(TurnType.EndPoint);
            setting.turns = turns;

            random = Random.Range(0, 1000);
            if (random < 333) { setting.navigationType = NavigationType.HUD_low; }
            else if (random < 666) { setting.navigationType = NavigationType.HUD_high; }
            else { setting.navigationType = NavigationType.VirtualCable; }

            random = Random.Range(0, 1000);
            if (random < 500) { setting.targetDifficulty = TargetDifficulty.easy; }
            else { setting.targetDifficulty = TargetDifficulty.hard; }

            if (i == 0) { setting.name = "PractiseDrive"; setting.targetDifficulty = TargetDifficulty.EasyAndMedium; setting.practiseDrive = true; }
            else { setting.name += i.ToString(); }

            experiments.Add(setting);
        }
        
    }
    public MainExperimentSetting GetCurrentExperimentSettings()
    {
        return experiments[experimentIndex];
    }
    public bool IsNextExperiment()
    {
        if (experimentIndex < experiments.Count()) { return true; }
        else { return false; }
    }
    public int GetExperimentIndex()
    {
        return experimentIndex;
    }
    public MainExperimentSetting GetExperimentSettings() { return experiments[experimentIndex]; }
    public void SetCalibrationDistances(float horizontal, float vertical, float side)
    {
        calibratedUsingHands = true;
        driverViewZToSteeringWheel = horizontal;
        driverViewYToSteeringWheel = vertical;
        driverViewXToSteeringWheel = side;
    }
    public bool ReadCSVSettingsFile()
    {
        string fileName = "experimentSettings.csv";
        string filePath = Application.dataPath + "/" + fileName;

        //Debug.Log($"Trying to read {filePath}...");

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
        catch { Debug.Log($"Could not read settings file: {filePath}...."); return false; }
    }
    public string GetNextScene() { return calibrationScene; }
    public bool ExperimentFinished()
    {
        if (experimentIndex > experiments.Count()) { return false; }
        else { return true; }
    }
    public void ExperimentEnded()
    {
        
        if (!loading) {
            loading = true; bool loadWhileFading = false;
            StartCoroutine(SaveDataWhenReady());
            StartCoroutine(LoadSceneAsync(waitingRoomScene, loadWhileFading)); 
            experimentIndex++;
        }
        
    }
    public void LoadExperiment()
    {
        if (!loading)
        {
            MainExperimentSetting setting = experiments[experimentIndex];

            if (setting.practiseDrive) { Debug.Log($"Loading {experiments[experimentIndex].name}...\nTurns: {setting.turns.Count()}, Navigation: All, Targets/Turn: [{setting.minTargets},{setting.maxTargets}], Difficutly: All "); }
            else { Debug.Log($"Loading {experiments[experimentIndex].name}...\nTurns: {setting.turns.Count()}, Navigation:{setting.navigationType}, Targets/Turn: [{setting.minTargets},{setting.maxTargets}], Difficutly: {setting.targetDifficulty}"); }

            loading = true; bool loadWhileFading = true;
            StartCoroutine(LoadSceneAsync(experimentScene, loadWhileFading));
            
        }
    }
    public void ReloadCurrentExperiment()
    {
        if (!loading)
        {
            MainExperimentSetting setting = experiments[experimentIndex];

            if (setting.practiseDrive) { Debug.Log($"Reloading {experiments[experimentIndex].name}...\nTurns: {setting.turns.Count()}, Navigation: All, Targets/Turn: [{setting.minTargets},{setting.maxTargets}], Difficutly: All "); }
            else { Debug.Log($"Reloading {experiments[experimentIndex].name}...\nTurns: {setting.turns.Count()}, Navigation:{setting.navigationType}, Targets/Turn: [{setting.minTargets},{setting.maxTargets}], Difficutly: {setting.targetDifficulty}"); }

            loading = true; bool loadWhileFading = true;
            StartCoroutine(LoadSceneAsync(experimentScene, loadWhileFading));
        }
    }
    public void AddTargetScene() { SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive); }
    public void LoadCalibrationScene()
    {
        SceneManager.LoadSceneAsync(calibrationScene, LoadSceneMode.Single);
    }
    IEnumerator LoadSceneAsync(string scene, bool loadWhileFading = false)
    {
        blackOutScreen.CrossFadeAlpha(1f, animationTime, false);

        //Skip this waiting if we load while fading
        if (!loadWhileFading) { yield return new WaitForSeconds(animationTime); }

        //When screen is black Yield one frame to give other coroutine time to save the data
        readyToSaveData = true; yield return new WaitForEndOfFrame();

        player.transform.parent = null;
        DontDestroyOnLoad(player);

        AsyncOperation operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);

        while (!operation.isDone) { yield return new WaitForEndOfFrame(); }

        blackOutScreen.CrossFadeAlpha(0, animationTime, false);

        loading = false; readyToSaveData = false;
        yield break;
    }
    public void MovePlayer(Transform position)
    {

        player.transform.parent = position;
        player.transform.position = position.position;
        player.transform.rotation = position.rotation;
        //Debug.Log("Moved player!");
    }

    IEnumerator SaveDataWhenReady()
    {
        
        while (!readyToSaveData)
        {
            yield return null;
        }

        readyToSaveData = false;
        MyUtils.GetExperimentManager().SaveData();
        yield break;

    }
}

[System.Serializable]
public class MainExperimentSetting
{
    public string name = "Experiment-";
    public List<TurnType> turns;
    public NavigationType navigationType;
    public float transparency = 0.4f;
    public TargetDifficulty targetDifficulty = TargetDifficulty.easy;
    public int minTargets=1;
    public int maxTargets=3;
    public float experimentTime = 0f;
    public bool practiseDrive = false;
    public int LeftTurns()
    {
        return turns.Where(s => s == TurnType.Left).Count();
    }
    public int RightTurns()
    {
        return turns.Where(s => s == TurnType.Right).Count();
    }

}
