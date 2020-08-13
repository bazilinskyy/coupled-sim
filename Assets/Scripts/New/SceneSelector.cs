using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSelector : MonoBehaviour
{
    LevelManager _lvlManager;
    public int sceneSelect;
    public int hostRole;
    public int n = 0;

    private void Update()
    {
        // This function should keep reloading the startscene.
        if (Input.GetKeyDown("1"))
        {
            nextExperiment();
        }
        if (Input.GetKeyDown("2"))
        {
            SceneManager.LoadSceneAsync("Varjotesting");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.tag == "NEXT")
        {
            Invoke("nextExperiment", 2);
            //Application.Quit(); // build version
            //UnityEditor.EditorApplication.isPlaying = false; // editor version

            PersistentManager.Instance.stopLogging = true;
            PersistentManager.Instance.nextScene = true;
        }
    }

    public void nextExperiment()
    {
        SceneManager.LoadSceneAsync("StartScene");
        n++;
        //PersistentManager.Instance.nextScene = true;
        Debug.LogError($"n = {n}");
    }

    public SceneSelector(LevelManager levelManager) // actually selects the experiment definition
    {
        _lvlManager = levelManager;

        // manually select the experiment definition nr for now
        sceneSelect = 1+n;
        Debug.LogError($"experiment nr = {sceneSelect}");
        if (sceneSelect > _lvlManager.Experiments.Length)
        {
            Debug.LogError("Selected experiment definition out of bounds.");
        }

        // manually select the role nr for the host for now
        //hostRole = 0;
    }
}
