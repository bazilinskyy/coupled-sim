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

    private void Start()
    {
        Debug.Log("Loaded waiting room...");
        StartingScene();
    }
    void StartingScene()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
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
    private void Update()
    {
        if (Input.GetKeyDown(StaticSceneManager.myPermission)) { mySceneLoader.LoadNextScene(); }
        
    }
    void GetVariablesFromSceneManager()
    {
        mySceneLoader = GetComponent<MySceneLoader>();
        camType = StaticSceneManager.camType;

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

    void SetText()
    {
        if (!StaticSceneManager.IsNextScene()) { text.text = "All experiments are completed. Thanks for participating!"; }
        
        text.text = $"Experiment {StaticSceneManager.sceneIndex - 1} starts when you are ready!";
        
    }
}
