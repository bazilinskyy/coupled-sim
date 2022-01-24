using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shut down the running application

public class QuitTrial : MonoBehaviour
{
    public SceneChange changerObject;

    // Declare parameters
    private Vector3 waypointPos;
    private Vector3 carPos;

    // Start is called before the first frame update
    void Start()
    {
        // Find waypoint position at which the game stops
        waypointPos = GameObject.Find("Waypoint 008").transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        // Grab current AICar position
        carPos = GameObject.FindWithTag("ManualCar").transform.position;               

        // Stop logic
        if ((carPos - waypointPos).magnitude < 0.1f)
        {
            Debug.Log("Stop logic triggered");
            changerObject.StartSwitch();
            //UnityEditor.EditorApplication.isPlaying = false;
            //Application.Quit();
        }
    }
}