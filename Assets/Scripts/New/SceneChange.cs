using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChange : MonoBehaviour
{
    //SceneChange changerObject;
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
    private void Update()
    {
        if (PersistentManager.Instance.stopLogging == true)
        {
            Debug.LogWarning("End Logging, destroy objects.");
            _logger.EndLog();
            _fixedLogger.EndLog();
            //_playerSystem.destroyPlayers();
            _aiCarSystem.destroyCars();
            PersistentManager.Instance.stopLogging = false;
            Debug.LogWarning("Logging stopped, objects destroyed.");
        }
    }

    public void StartSwitch()
    {
        Debug.LogWarning("Preparing scene change");
        Invoke("switchLogic", 0.5f);
        
        PersistentManager.Instance.stopLogging = true;
        
        //PersistentManager.Instance.switchScene = true;
        
    }

    public void switchLogic()
    {
        Debug.LogWarning("switchLogic() entered");
        // Switch logic here
        string name;
        name = "DR-3-transparent";
        nextExperiment(name);
        //Invoke("nextExperiment", 1.0f);
    }
    
    /// Load next experiment
    public void nextExperiment(string expName)
    {
        Debug.LogWarning("Switching Scenes");
        SceneManager.LoadScene(expName);

        foreach (var carSpawner in _lvlManager.ActiveExperiment.CarSpawners)
        {
            Debug.LogWarning("Loading carSpawner in _aiCarSystem");
            carSpawner.Init(_aiCarSystem);
        }

        //Debug.Log("LoadLevelWithLocalPlayer");
        //_lvlManager.LoadLevelWithLocalPlayer(2, 0, new List<int> {-1});


        /*AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(expName, LoadSceneMode.Single);
        
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