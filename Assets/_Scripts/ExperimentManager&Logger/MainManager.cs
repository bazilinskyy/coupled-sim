using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System;
public class MainManager : MonoBehaviour
{

    public MyCameraType camType = MyCameraType.Leap;

    public List<MainExperimentSetting> experiments;

    private bool debugMode = true;

    private bool makeVirtualCable = true;
    private bool renderHUD = true;

    private string subjectDataFolder = "nothing...";
    private string subjectID = "Dummy";

    private bool automateSpeed = true;
    private bool saveData = true;
    private float animationTime = 2.5f;

    private float foVCamera = 87;
    private bool calibratedUsingHands = false;
    private float driverViewZToSteeringWheel = 0f;
    private float driverViewYToSteeringWheel = 0f;
    private float driverViewXToSteeringWheel = 0f;

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

    public int[] experimentOrder = { 0, 1, 2, 3, 4, 5 };
    public int numberTurns = 12;
    private ExperimentType experimentType = ExperimentType.Real;

    private GameObject player;
    UnityEngine.UI.Image blackOutScreen;
    
    private bool loading = false; 
    private bool readyToSaveData = false;
    private bool isInExperiment = false;
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
    public bool DebugMode { get => debugMode; set => debugMode = value; }
    public bool MakeVirtualCable { get => makeVirtualCable; set => makeVirtualCable = value; }
    public bool RenderHUD { get => renderHUD; set => renderHUD = value; }
    public string SubjectDataFolder { get => subjectDataFolder; set => subjectDataFolder = value; }
    public string SubjectName { get => subjectID; set => subjectID = value; }
    public bool AutomateSpeed { get => automateSpeed; set => automateSpeed = value; }
    public bool SaveData { get => saveData; set => saveData = value; }
    public float AnimationTime { get => animationTime; set => animationTime = value; }
    public float FoVCamera { get => foVCamera; set => foVCamera = value; }
    public bool CalibratedUsingHands { get => calibratedUsingHands; set => calibratedUsingHands = value; }
    public float DriverViewZToSteeringWheel { get => driverViewZToSteeringWheel; set => driverViewZToSteeringWheel = value; }
    public float DriverViewYToSteeringWheel { get => driverViewYToSteeringWheel; set => driverViewYToSteeringWheel = value; }
    public float DriverViewXToSteeringWheel { get => driverViewXToSteeringWheel; set => driverViewXToSteeringWheel = value; }
    public bool IsInExperiment { get => isInExperiment; set => isInExperiment = value; }

    private void Awake()
    {
        UnityEngine.Random.InitState(42);
        
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
        subjectDataFolder = string.Join("/", System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop).Replace("\\", "/"), "Data", System.DateTime.Now.ToString("MM-dd_HH-mm") + "_" + subjectID);
        System.IO.Directory.CreateDirectory(subjectDataFolder);

        Debug.Log($"Settings file received...\nSubject ID = {subjectID}, DataFolder: {subjectDataFolder}...");
        
        experiments = new List<MainExperimentSetting>();

        //If we do a real experiment add the practise drive and the experiments (based on the given order in the settings file)
        if (experimentType.IsReal())
        {
            AddPractiseDrive();
            AddExperiments(experimentOrder, numberTurns);
        }
        else { AddTargetCalibrationExperiment(); }
        
    }
    void AddTargetCalibrationExperiment()
    {
        MainExperimentSetting setting = new MainExperimentSetting();
        setting.name = "TargetCalibration";
        setting.conditionNumber = 1111;
        setting.minTargets = 3;
        setting.maxTargets = 3;
        setting.experimentType = ExperimentType.TargetCalibration;
        setting.targetDifficulty = TargetDifficulty.easy;
        setting.navigationType = NavigationType.HighlightedRoad;

        List<TurnType> turns = new List<TurnType>
        {
            TurnType.Straight,
            TurnType.Left,
            TurnType.Straight,
            TurnType.EndPoint
        };

        setting.turns = turns;
        experiments.Add(setting);
    }

    void AddPractiseDrive()
    {
        MainExperimentSetting setting = new MainExperimentSetting();
        //Make settings for practise drive
   
        setting.name = "PractiseDrive";
        setting.targetDifficulty = TargetDifficulty.EasyAndMedium;
        setting.navigationType = NavigationType.HUD_low;
        setting.experimentType = ExperimentType.Practise;
        setting.minTargets = 3;
        setting.maxTargets = 3;
        setting.turns = GetRandomTurns(18);
        experiments.Add(setting);
     }

    Condition MakeCondition(NavigationType navType, TargetDifficulty difficulty) { Condition condition = new Condition() { navigationType = navType, targetDifficulty = difficulty }; return condition; }
   
    void AddExperiments(int[] order, int numberTurns)
    {
        List<Condition> conditions = new List<Condition>(); 

        //conditions 1 - 6
        conditions.Add(MakeCondition(NavigationType.HUD_high, TargetDifficulty.easy));
        conditions.Add(MakeCondition(NavigationType.HUD_high, TargetDifficulty.hard));
        conditions.Add(MakeCondition(NavigationType.HUD_low, TargetDifficulty.easy));
        conditions.Add(MakeCondition(NavigationType.HUD_low, TargetDifficulty.hard));
        conditions.Add(MakeCondition(NavigationType.VirtualCable, TargetDifficulty.easy));
        conditions.Add(MakeCondition(NavigationType.VirtualCable, TargetDifficulty.hard));

        MainExperimentSetting setting = new MainExperimentSetting();

        for (int i = 0; i < conditions.Count(); i++)
        {
            setting = new MainExperimentSetting();

            int conditionNumber = order[i];
            
            setting.name += (i+1).ToString();
            setting.conditionNumber = conditionNumber;
            setting.navigationType = conditions[conditionNumber].navigationType;
            setting.targetDifficulty = conditions[conditionNumber].targetDifficulty;

            //Add turns
            List<TurnType> turns = GetRandomTurns(numberTurns);
            setting.turns = turns;

            experiments.Add(setting);
        }
    }

    List<TurnType> GetRandomTurns(int number)
    {
        List<TurnType> turns = new List<TurnType>();
        int randomInt;
        for (int j = 0; j < number; j++)
        {
            randomInt = UnityEngine.Random.Range(0, 100);
            if (randomInt < 50) { turns.Add(TurnType.Left); }
            else { turns.Add(TurnType.Right); }
        }
        turns.Add(TurnType.EndPoint);
        return turns;
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

        try
        {
            string fileData = System.IO.File.ReadAllText(filePath);
            string[] lines = fileData.Split('\n');

            //Line1: subject ID; Line2: experimentType [0 == real, 1== target calibration]; Line3: experimentOrder; Line4: number of turns
            //First row is name of object, second is actual input

            //Data: [0] = Name, [1] = order
            string[] lineData;
            lineData = lines[0].Trim().Split(';'); subjectID = lineData[1];
            lineData = lines[1].Trim().Split(';'); experimentType = int.Parse(lineData[1]) == 0 ? ExperimentType.Real : ExperimentType.TargetCalibration;

            //There is no order and number of turns neededd for the target calibration experiment
            if (!experimentType.IsReal()) { return true; }

            lineData = lines[2].Trim().Split(';'); experimentOrder = Array.ConvertAll(lineData[1].Split(','), s => int.Parse(s));
            lineData = lines[3].Trim().Split(';'); numberTurns = int.Parse(lineData[1]);


            return true;
        }
        catch { Debug.Log($"Could not read settings file: {filePath}...."); return false; }
    }
    public string GetNextScene() { return calibrationScene; }
    public void ExperimentEnded()
    {
        
        if (!loading) {
            loading = true;
            StartCoroutine(SaveDataWhenReady());
            StartCoroutine(LoadSceneAsync(waitingRoomScene));
            experimentIndex++;
        }
        
    }
    public void LoadExperiment()
    {
        if (!loading)
        {
            MainExperimentSetting setting = experiments[experimentIndex];

            if (setting.experimentType.IsPractise()) { Debug.Log($"Loading {experiments[experimentIndex].name}...\nTurns: {setting.turns.Count()}, Navigation: All, Targets/Turn: [{setting.minTargets},{setting.maxTargets}], Difficutly: All "); }
            else { Debug.Log($"Loading {experiments[experimentIndex].name}...\nTurns: {setting.turns.Count()}, Navigation:{setting.navigationType}, Targets/Turn: [{setting.minTargets},{setting.maxTargets}], Difficutly: {setting.targetDifficulty}"); }

            loading = true;
            StartCoroutine(LoadSceneAsync(experimentScene));          
        }
    }
    public void ReloadCurrentExperiment()
    {
        if (!loading)
        {
            MainExperimentSetting setting = experiments[experimentIndex];

            if (setting.experimentType.IsPractise()) { Debug.Log($"Reloading {experiments[experimentIndex].name}...\nTurns: {setting.turns.Count()}, Navigation: All, Targets/Turn: [{setting.minTargets},{setting.maxTargets}], Difficutly: All "); }
            else { Debug.Log($"Reloading {experiments[experimentIndex].name}...\nTurns: {setting.turns.Count()}, Navigation:{setting.navigationType}, Targets/Turn: [{setting.minTargets},{setting.maxTargets}], Difficutly: {setting.targetDifficulty}"); }

            loading = true;
            StartCoroutine(LoadSceneAsync(experimentScene));
        }
    }
    public void AddTargetScene() { SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive); }
    public void LoadCalibrationScene()
    {
        IsInExperiment = false;
        SceneManager.LoadSceneAsync(calibrationScene, LoadSceneMode.Single);
    }
    IEnumerator LoadSceneAsync(string scene)
    {
        blackOutScreen.CrossFadeAlpha(1f, animationTime, false);

        //Skip this waiting if we load while fading
        yield return new WaitForSeconds(animationTime);

        //When screen is black Yield one frame to give other coroutine time to save the data
        readyToSaveData = true; yield return new WaitForEndOfFrame();

        player.transform.parent = null;
        DontDestroyOnLoad(player);

        AsyncOperation operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
/*        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            // Check if the load has finished
            if (operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
                break;
            }
            yield return new WaitForEndOfFrame();
        }*/
        while (!operation.isDone) { yield return new WaitForEndOfFrame(); }

        blackOutScreen.CrossFadeAlpha(0, animationTime, false);

        loading = false; readyToSaveData = false;

        if(scene == experimentScene) { IsInExperiment = true; }
        else { IsInExperiment = false; }

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

public struct Condition
{
    public NavigationType navigationType;
    public TargetDifficulty targetDifficulty;
}
[System.Serializable]
public class MainExperimentSetting
{
    public string name = "Experiment-";
    public int conditionNumber = 0;
    public List<TurnType> turns;
    public NavigationType navigationType;
    public float transparency = 0.4f;
    public TargetDifficulty targetDifficulty = TargetDifficulty.easy;
    public int minTargets=1;
    public int maxTargets=3;
    public float experimentTime = 0f;
    public ExperimentType experimentType = ExperimentType.Real;
    public int LeftTurns()
    {
        return turns.Where(s => s == TurnType.Left).Count();
    }
    public int RightTurns()
    {
        return turns.Where(s => s == TurnType.Right).Count();
    }
}

