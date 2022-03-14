﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeadLockedPositionSetter : MonoBehaviour
{
    // This script is for setting the HUD position based on head movements and rotations
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
        iniViewPosDisplacement = new Vector3(-0.04f, 1.70f, 0.35f); //new Vector3(-0.04f, 1.73f, 0.35f);
        iniRotAngles = new Vector3(-5f, -12f, 0f);
        iniViewRotDisplacement = Quaternion.Euler(iniRotAngles);

        // Find initial Pedestrian position and rotations
        pedPos = GameObject.Find("Participant(Clone)").transform.position;
        pedRot = GameObject.Find("Participant(Clone)").transform.rotation;

        // Set initial position and rotation
        this.GetComponent<RectTransform>().position = pedPos + iniViewPosDisplacement;
        this.GetComponent<RectTransform>().rotation = pedRot * iniViewRotDisplacement;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // Grab current pedestrian rotations and positions:
        pedPos = GameObject.Find("Participant(Clone)").transform.position;
        pedRot = GameObject.Find("Participant(Clone)").transform.rotation;

        // Sets the new rotation based on head movement
        this.GetComponent<RectTransform>().rotation = pedRot * iniViewRotDisplacement;

        // Rotate the displacement vector to point to new gaze direction
        currentViewPosDisplacement = pedRot * iniViewPosDisplacement; 

        // Sets the new position of the head locked display
        this.GetComponent<RectTransform>().position = pedPos + currentViewPosDisplacement;        
    }
}