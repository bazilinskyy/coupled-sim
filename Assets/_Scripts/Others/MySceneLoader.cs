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
    private bool environmentLoaded = false;
    private void Awake()
    {
        player = gameObject; // MyUtils.GetPlayer();
        experimentInput = player.GetComponent<ExperimentInput>(); // MyUtils.GetExperimentInput();

        blackOutScreen = GameObject.FindGameObjectWithTag("BlackOutScreen").GetComponent<UnityEngine.UI.Image>();
        blackOutScreen.color = new Color(0, 0, 0, 1f); blackOutScreen.CrossFadeAlpha(1f, 0f, true);
        
        blackOutScreen.CrossFadeAlpha(0, experimentInput.animationTime * 3, false);

        if (player == null) { Debug.LogError("Could not find player...."); return; }

        //DontDestroyOnLoad(player); Debug.Log("Found player!");
/*        
        if (startingPosition == null) { return; }

        
        StartCoroutine(MovePlayerToDestination());*/
    }

    public void MovePlayer(Transform position)
    {
        player.transform.parent = position;
        player.transform.position = position.position;
        player.transform.rotation = position.rotation;
    }
    IEnumerator MovePlayerToDestination()
    {
        /*
        int NScenes = SceneManager.GetAllScenes().Length;
        //The environemtn scenes gets put in the DontDestroy on Load scene. So we always have 2 scenes
        while (NScenes > 2 && timeWaited < 5f) 
        {
            Debug.Log($"Waiting, currently on {NScenes} scenes...");
            if (timeWaited == 0f) { Debug.Log($"Waiting, currently on {NScenes} scenes..."); } 
            timeWaited += 0.01f; 
            yield return new WaitForSeconds(0.01f); 
        }*/
        timeWaited = 0f;

        player.transform.parent = startingPosition;
        player.transform.position = startingPosition.position;
        player.transform.rotation = startingPosition.rotation;

        //if (experimentInput.camType == MyCameraType.Leap) { Varjo.VarjoPlugin.ResetPose(true, Varjo.VarjoPlugin.ResetRotation.ALL); }

        
        Debug.Log("Moved player!");
        yield return null;
    }

    public void LoadEnvironment() {

        StartCoroutine(LoadEnvironementScene());

    }

    public void LoadCalibrationScene()
    {
        if (!loading) { SceneManager.LoadSceneAsync(experimentInput.calibrationScene, LoadSceneMode.Single); }
    }
    public void LoadNextDrivingScene(bool unloadCalibrationScene, bool unloadWaitingRoomScene)
    {
        if (!loading && experimentInput.IsNextScene()) { StartCoroutine(LoadExperiment(experimentInput.GetNextScene(), unloadCalibrationScene, unloadWaitingRoomScene)); }
        //if (environmentLoaded) { StartCoroutine(LoadExperiment(experimentInput.GetNextScene())); }
        //operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    } 

    public void LoadWaitingScene()
    {
        if (!loading) { StartCoroutine(LoadWaitingSceneAsync()); }
        
    }

    public void AddTargetScene()
    {
        SceneManager.LoadSceneAsync(experimentInput.targetScene, LoadSceneMode.Additive);
    }

    IEnumerator LoadEnvironementScene()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(experimentInput.environmentScene, LoadSceneMode.Additive);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;

        bool foundEnvironement = false;
        while (!foundEnvironement)
        {
            GameObject env = GameObject.FindGameObjectWithTag("Environment");
            if(env != null) {
                Debug.Log("Found environement!");
                experimentInput.environment = env; 
                foundEnvironement = true;
                env.SetActive(false);
                 }
            else { yield return null; }
        }

    }

    IEnumerator LoadExperiment(string sceneName, bool unloadCalibrationScene, bool unloadWaitingRoomScene)
    {
        loading = true;  Debug.Log($"Loading next scene: {sceneName}...");
        player.transform.parent = null;

        DontDestroyOnLoad(player);
        
        blackOutScreen.CrossFadeAlpha(1f, experimentInput.animationTime, false);
        yield return new WaitForSeconds(experimentInput.animationTime);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (!operation.isDone)
        {
            yield return null;
        }
        experimentInput.environment.SetActive(true);
        //Scene newScene = SceneManager.GetSceneByName(sceneName);
        //SceneManager.MoveGameObjectToScene(player, newScene);

        if (unloadCalibrationScene) { operation = SceneManager.UnloadSceneAsync(experimentInput.calibrationScene); SceneManager.UnloadSceneAsync(experimentInput.targetScene); }
        if (unloadWaitingRoomScene) { operation = SceneManager.UnloadSceneAsync(experimentInput.waitingRoomScene); }

        while (!operation.isDone)
        {
            yield return null;
        }

        blackOutScreen.CrossFadeAlpha(0f, experimentInput.animationTime*2f, false);
        yield return new WaitForSeconds(experimentInput.animationTime);

        loading = false;

    }
    /*IEnumerator LoadEnvironment()
    {
        
        player.transform.parent = null;

        DontDestroyOnLoad(player);
        blackOutScreen.CrossFadeAlpha(1f, experimentInput.animationTime, false);
        yield return new WaitForSeconds(experimentInput.animationTime);


        AsyncOperation operation = SceneManager.LoadSceneAsync("Environment", LoadSceneMode.Single);

        while (!operation.isDone)
        {
            yield return null;
        }
        environmentLoaded = true;

    }*/

    IEnumerator LoadWaitingSceneAsync()
    {
       loading = true; Debug.Log($"Loading next scene: {experimentInput.waitingRoomScene}...");
        player.transform.parent = null;

        DontDestroyOnLoad(player);


        blackOutScreen.CrossFadeAlpha(1f, experimentInput.animationTime, false);
        yield return new WaitForSeconds(experimentInput.animationTime);

        AsyncOperation operation = SceneManager.LoadSceneAsync(experimentInput.waitingRoomScene, LoadSceneMode.Additive);

        while (!operation.isDone)
        {
            yield return null;
        }

        string drivingScene = experimentInput.currentDrivingScene;
        Scene oldScene = SceneManager.GetSceneByName(drivingScene);

        operation = SceneManager.UnloadSceneAsync(oldScene);

        while (!operation.isDone)
        {
            yield return null;
        }
        experimentInput.environment.SetActive(false);
        loading = false;

        blackOutScreen.CrossFadeAlpha(0f, experimentInput.animationTime*2, false);
    }
   /* IEnumerator LoadExperiment(string sceneName, bool enableEnvironment, bool unloadCalibrationScene = false, bool unloadWaitingRoomScene = false)
    {
        loading = true; Debug.Log($"Loading next scene: {sceneName}...");
        player.transform.parent = null;

        DontDestroyOnLoad(player);
        DontDestroyOnLoad(experimentInput.environment);

        blackOutScreen.CrossFadeAlpha(1f, experimentInput.animationTime, false);
        yield return new WaitForSeconds(experimentInput.animationTime);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            
        while (!operation.isDone)
        {
            yield return null;
        }
        Scene newScene = SceneManager.GetSceneByName(sceneName);
        Debug.Log($"Setting environment to {enableEnvironment}...");
        experimentInput.environment.SetActive(enableEnvironment);

        SceneManager.MoveGameObjectToScene(player, newScene);
        //SceneManager.MoveGameObjectToScene(environment, newScene);

        SceneManager.UnloadSceneAsync("CalibrationScene");
    }*/

    IEnumerator LoadYourAsyncScene(string sceneName, bool enableEnvironment, bool unloadCalibrationScene=false, bool unloadWaitingRoomScene=false)
    {
        return null;
        /*loading = true; Debug.Log($"Loading next scene: {sceneName}...");
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
        */
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

