using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Trigger to move to the next scene
public class NextTrialManual : MonoBehaviour
{
    public SceneChange changerObject;

    // Update is called once per frame
    void Update()
    {
        // Stop logic
        if (Input.GetKeyDown(KeyCode.Z))
        {
            Debug.Log("Stop logic triggered");
            changerObject.StartSwitch();
            //UnityEditor.EditorApplication.isPlaying = false;
            //Application.Quit();
        }
    }
}