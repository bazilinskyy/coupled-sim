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
    private Vector3 iniRotAngles;
    private Quaternion iniViewRotDisplacement;
    private Vector3 waypointCarDistance;

    // Start is called before the first frame update
    void Start()
    {
        // Set Initial View Position and Rotation Displacement
        iniViewPosDisplacement = new Vector3(-0.1f, 1.8f, 0.6f);
        iniRotAngles = new Vector3(-5f, -12f, 0f);
        iniViewRotDisplacement = Quaternion.Euler(iniRotAngles);

        // Find initial Pedestrian position and rotations
        pedPos = GameObject.Find("Participant(Clone)").transform.position;
        pedRot = GameObject.Find("Participant(Clone)").transform.rotation;

        // Set initial position and rotation
        this.GetComponent<Transform>().position = pedPos + iniViewPosDisplacement;
        this.GetComponent<Transform>().rotation = pedRot * iniViewRotDisplacement;
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