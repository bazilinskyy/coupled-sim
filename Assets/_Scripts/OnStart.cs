using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnStart : MonoBehaviour
{
    public bool loadCalibration = false;
    // Start is called before the first frame update
    void Awake()
    {
        Application.targetFrameRate = 300;
        if (loadCalibration) { SceneManager.LoadSceneAsync("CalibrationScene", LoadSceneMode.Additive);  }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
