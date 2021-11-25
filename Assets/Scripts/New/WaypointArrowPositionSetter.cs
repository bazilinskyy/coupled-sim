using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script is for the HUD behaviour
public class WaypointArrowPositionSetter : MonoBehaviour
{
    // Declare parameters
    private Vector3 pedPos;
    private Quaternion pedRot;
    private Vector3 carPos;
    private Vector3 arrowLocation;
    private Vector3 iniViewPosDisplacement;
    private Vector3 currentViewPosDisplacement;
    private Vector3 waypointCarDistance;

    // Start is called before the first frame update
    void Start()
    {
        // Set Initial View Position and Rotation Displacement
        iniViewPosDisplacement = new Vector3(-0.1f, 1.72f, 0.6f);

        // Find initial Pedestrian position and rotations
        pedPos = GameObject.Find("Participant(Clone)").transform.position;

        // Set initial position
        this.GetComponent<Transform>().position = pedPos + iniViewPosDisplacement;
    }

    // Update is called once per frame
    void Update()
    {
        // Grab current pedestrian rotations and positions:
        pedPos = GameObject.Find("Participant(Clone)").transform.position;
        pedRot = GameObject.Find("Participant(Clone)").transform.rotation;

        // Grab current AICar position
        carPos = GameObject.FindWithTag("ManualCar").transform.position;

        // Define current arrow location
        arrowLocation = this.GetComponent<Transform>().position;

        // Find distance vector between waypointArrow and AICar
        waypointCarDistance = carPos - arrowLocation;

        // Rotate arrow to point to AICar
        this.GetComponent<Transform>().rotation = Quaternion.LookRotation(waypointCarDistance);

        // Rotate the displacement vector to point to new gaze direction
        currentViewPosDisplacement = pedRot * iniViewPosDisplacement; 

        // Sets the new position of the waypointArrow based on head movements
        this.GetComponent<Transform>().position = pedPos + currentViewPosDisplacement;        
    }
}