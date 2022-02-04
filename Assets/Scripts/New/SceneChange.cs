using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChange : MonoBehaviour
{
    SceneChange changerObject;
    LevelManager _lvlManager;
    WorldLogger _logger;
    WorldLogger _fixedLogger;
    PlayerSystem _playerSystem;
    AICarSyncSystem _aiCarSystem;

    private void Awake()
    {
        PersistentManager.Instance.stopLogging = false;
        PersistentManager.Instance.switchScene = false;
    }

    public void StartSwitch()
    {
        Debug.LogError("Preparing scene change");

        Debug.LogError("    - Setting stopLogging to true");
        // This triggers in NetworkingManager to stop logging and destroy players and cars
        PersistentManager.Instance.stopLogging = true;

        Debug.LogError("    - Setting switchScene to true");
        // This triggers a case in Host.cs
        PersistentManager.Instance.switchScene = true;

    }

    /*public void switchLogic()
    {
        Debug.LogWarning("switchLogic() entered");
        // Switch logic here
        string name;
        name = "DR-3-transparent";
        nextExperiment(name);
        //Invoke("nextExperiment", 1.0f);
    }*/
    
    /// Load next experiment
    public void nextExperiment()
    {
        Debug.LogError("Switching Scenes");
        //SceneManager.LoadSceneAsync("StartScene");
        /*AsyncOperation asyncOperation = SceneManager.LoadSceneAsync("StartScene", LoadSceneMode.Single);

        Debug.Log("Scene loading progress :" + asyncOperation.progress);

        // Don't let the Scene activate until you allow it to
        asyncOperation.allowSceneActivation = false;

        // Check if the load has finished and scene is ready
        if (asyncOperation.progress >= 0.9f)
        {
            Debug.LogError("Next scene allowed to load");
            asyncOperation.allowSceneActivation = true;
        }*/
    }

    public SceneChange(LevelManager levelManager) // actually selects the experiment definition
    {
        _lvlManager = levelManager;
    }
}