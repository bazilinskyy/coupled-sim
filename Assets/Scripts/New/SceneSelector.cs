using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSelector : MonoBehaviour
{
    LevelManager _lvlManager;
    public int sceneSelect;
    public int hostRole;

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
            Invoke("nextExperiment", 4);
            //Application.Quit(); // build version
            //UnityEditor.EditorApplication.isPlaying = false; // editor version

            PersistentManager.Instance.stopLogging = true;
            PersistentManager.Instance.nextScene = true;
            Debug.LogError("Hit, going to the next scene");
            PersistentManager.Instance.experimentnr++;
            Debug.LogError($"persistent experiment nr = {PersistentManager.Instance.experimentnr}");
        }
    }

    public void nextExperiment()
    {
        SceneManager.LoadSceneAsync("StartScene");
    }

    public SceneSelector(LevelManager levelManager) // actually selects the experiment definition
    {
        _lvlManager = levelManager;
        //Debug.LogError($"persistent experiment nr = {PersistentManager.Instance.experimentnr}");

        // manually select the experiment definition nr for now
        sceneSelect = PersistentManager.Instance.experimentnr;
        if (sceneSelect > _lvlManager.Experiments.Length)
        {
            Debug.LogError("Selected experiment definition out of bounds.");
            Debug.LogError($"experiment nr = {sceneSelect}");
        }
        

        // manually select the role nr for the host for now
        //hostRole = 0;
    }

    public void GazeEffectOnAV()
    {
        if (PersistentManager.Instance.experimentnr < 4 || PersistentManager.Instance.experimentnr > 8)
        {
            PersistentManager.Instance._StopWithEyeGaze = false;
        }
        if (PersistentManager.Instance.experimentnr >= 4 && PersistentManager.Instance.experimentnr <= 8)
        {
            PersistentManager.Instance._StopWithEyeGaze = true;
        }
    }
}
