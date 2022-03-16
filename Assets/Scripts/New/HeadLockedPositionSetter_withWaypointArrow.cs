using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeadLockedPositionSetter_withWaypointArrow : MonoBehaviour
{
    // This script is for the HUD with the guiding Waypoint Arrow on top of it
    // Declare parameters
    private Vector3 pedPos;
    private Quaternion pedRot;
    private Vector3 iniViewPosDisplacement;
    private Vector3 currentViewPosDisplacement;
    private Vector3 iniRotAngles;
    private Quaternion iniViewRotDisplacement;

    // Start is called before the first frame update
    void Start()
    {
        // Set Initial View Position and Rotation Displacement
        iniViewPosDisplacement = new Vector3(-0.04f, 0.02f, 0.35f); //new Vector3(-0.04f, 1.73f, 0.35f);
        iniRotAngles = new Vector3(-2f, -10f, 0f);
        iniViewRotDisplacement = Quaternion.Euler(iniRotAngles);

        // Find initial Pedestrian position and rotations
        pedPos = GameObject.FindWithTag("ParticipantCam").transform.position;
        pedRot = GameObject.FindWithTag("ParticipantCam").transform.rotation;

        // Set initial position and rotation
        this.GetComponent<RectTransform>().position = pedPos + iniViewPosDisplacement;
        this.GetComponent<RectTransform>().rotation = pedRot * iniViewRotDisplacement;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // Grab current pedestrian rotations and positions:
        pedPos = GameObject.FindWithTag("ParticipantCam").transform.position;
        pedRot = GameObject.FindWithTag("ParticipantCam").transform.rotation;

        // Sets the new rotation based on head movement
        this.GetComponent<RectTransform>().rotation = pedRot * iniViewRotDisplacement;

        // Rotate the displacement vector to point to new gaze direction
        currentViewPosDisplacement = pedRot * iniViewPosDisplacement; 

        // Sets the new position of the head locked display
        this.GetComponent<RectTransform>().position = pedPos + currentViewPosDisplacement;        
    }
}