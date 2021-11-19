using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script is for the body locked behaviour
public class WaypointArrow_BodyLocked : MonoBehaviour
{
    // Declare parameters
    private Vector3 carPos;
    private Vector3 arrowLocation;
    private Vector3 waypointCarDistance;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        // Grab current AICar position
        carPos = GameObject.FindWithTag("ManualCar").transform.position;

        // Define current arrow location
        arrowLocation = this.GetComponent<Transform>().position;

        // Find distance vector between waypointArrow and AICar
        waypointCarDistance = carPos - arrowLocation;

        // Rotate arrow to point to AICar
        this.GetComponent<Transform>().rotation = Quaternion.LookRotation(waypointCarDistance);     
    }
}