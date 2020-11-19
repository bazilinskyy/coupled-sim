using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;
using System;
using System.Collections.Generic;

public class MySceneLoader : MonoBehaviour
{
    public GameObject player;
    private ExperimentInput experimentInput;

    public Transform startingPosition;
    private bool loading=false;
    UnityEngine.UI.Image blackOutScreen;
    private float timeWaited = 0f;
    private void Start()
    {
        player = MyUtils.GetPlayer();
        experimentInput = MyUtils.GetExperimentInput();

        blackOutScreen = GameObject.FindGameObjectWithTag("BlackOutScreen").GetComponent<UnityEngine.UI.Image>();
        blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(1f, 0f, true);

        if (player == null) { Debug.LogError("Could not find player...."); return; }

        //DontDestroyOnLoad(player); Debug.Log("Found player!");
        
        if (startingPosition == null) { return; }
        StartCoroutine(MovePlayerToDestination());
    }

    IEnumerator MovePlayerToDestination()
    {
        int NScenes = SceneManager.GetAllScenes().Length;
        //The environemtn scenes gets put in the DontDestroy on Load scene. So we always have 2 scenes
        while (NScenes > 2 && timeWaited < 5f) 
        {
            Debug.Log($"Waiting, currently on {NScenes} scenes...");
            if (timeWaited == 0f) { Debug.Log($"Waiting, currently on {NScenes} scenes..."); } 
            timeWaited += 0.01f; 
            yield return new WaitForSeconds(0.01f); 
        }
        timeWaited = 0f;

        player.transform.parent = startingPosition;
        player.transform.position = startingPosition.position;
        player.transform.rotation = startingPosition.rotation;

        //if (experimentInput.camType == MyCameraType.Leap) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }

        blackOutScreen.CrossFadeAlpha(0, experimentInput.animationTime*2, false);
        Debug.Log("Moved player!");
    }

    public void LoadCalibrationScene()
    {
        if (!loading) { StartCoroutine(LoadYourAsyncScene(experimentInput.calibrationScene, false,false,false)); }
    }
    public void LoadNextDrivingScene(bool unloadCalibrationScene, bool unloadWaitingRoomScene)
    {
        if (!loading && experimentInput.IsNextScene()) { StartCoroutine(LoadYourAsyncScene(experimentInput.GetNextScene(), true, unloadCalibrationScene, unloadWaitingRoomScene)); }
    } 

    public void LoadWaitingScene()
    {
        if (!loading) { StartCoroutine(LoadYourAsyncScene(experimentInput.waitingRoomScene, false, false, false)); }
    }

    public void AddTargetScene()
    {
        SceneManager.LoadSceneAsync(experimentInput.targetScene, LoadSceneMode.Additive);
    }

    IEnumerator LoadYourAsyncScene(string sceneName, bool enableEnvironment, bool unloadCalibrationScene=false, bool unloadWaitingRoomScene=false)
    {
        loading = true; Debug.Log($"Loading next scene: {sceneName}...");
        player.transform.parent = null;

        DontDestroyOnLoad(player);
        DontDestroyOnLoad(experimentInput.environment);

        blackOutScreen.CrossFadeAlpha(1f, experimentInput.animationTime, false);
        yield return new WaitForSeconds(experimentInput.animationTime);

        //Make a list of scene names and to  be made operations
        List<MyScenes> sceneList = new List<MyScenes>();
        
        sceneList.Add( new MyScenes(sceneName, true));

        //Enable or disable environment and unload last scene
        if (enableEnvironment)
        {
            experimentInput.environment.SetActive(true);
            if (unloadCalibrationScene)
            {
                sceneList.Add(new MyScenes(experimentInput.calibrationScene, false));
                try { sceneList.Add(new MyScenes(experimentInput.targetScene, false)); }
                catch { Debug.Log("Target scene was not loaded...."); }
            }
            if (unloadWaitingRoomScene) { sceneList.Add(new MyScenes(experimentInput.waitingRoomScene, false)); }
        }
        else
        {
            experimentInput.environment.SetActive(false);
            sceneList.Add(new MyScenes(experimentInput.currentDrivingScene, false));
        }

        //Wait till all scenes are loaded/unloaded
        foreach (MyScenes scene in sceneList)
         {
            AsyncOperation operation;
            if (scene.load) { operation = SceneManager.LoadSceneAsync(scene.name); }
            else { operation = SceneManager.UnloadSceneAsync(scene.name); }

            while (!operation.isDone)
            {
                yield return null;
            }
        }
            
        Scene newScene = SceneManager.GetSceneByName(sceneName);

        SceneManager.MoveGameObjectToScene(player, newScene);
    }
}

public class MyScenes
{
    public string name;
    public bool load;

    public MyScenes(string _name, bool _load)
    {
        name = _name;
        load = _load;
    }
}

