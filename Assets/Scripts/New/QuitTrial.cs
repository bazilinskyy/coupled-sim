using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shut down the running application

public class QuitTrial : MonoBehaviour
{
    private float timeValue = 0;

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
        timeValue += Time.deltaTime;

        // Only enter the loop after 5 seconds to avoid "GameObject not found" errors
        if (timeValue > 5.0f)
        {
            // Grab current AICar position
            carPos = GameObject.FindWithTag("ManualCar").transform.position;

            // Stop logic
            if ((carPos - waypointPos).magnitude < 0.1f)
            {
                Debug.Log("Stop logic triggered");
                UnityEditor.EditorApplication.isPlaying = false;
                //Application.Quit();
            }
        }
    }
}