using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSelector : MonoBehaviour
{
    LevelManager _lvlManager;
    public int sceneSelect;
    private int hostRole;

    public void Update()
    {
        if (Input.GetKeyDown("r"))
        {
            //SceneManager.UnloadSceneAsync($"{SceneManager.GetActiveScene().name}");
            SceneManager.LoadScene("StartScene 1");
            if (sceneSelect == 4)
            {
                sceneSelect = 5;
            }
        }
    }

    public SceneSelector(LevelManager levelManager) // actually selects the experiment definition
    {
        _lvlManager = levelManager;

        // manually select the experiment definition nr for now
        //sceneSelect = 4; 
        if (sceneSelect > _lvlManager.Experiments.Length)
        {
            Debug.LogError("Selected experiment definition out of bounds.");
        }

        // manually select the role nr for the host for now
        hostRole = 0;
    }

    public int getSceneSelect()
    {
        return sceneSelect;
    }

    public int getHostRole()
    {
        return hostRole;
    }
}
