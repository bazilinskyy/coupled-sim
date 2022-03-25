using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script allows rotation of the participant player body along the yaw-axis / Y-axis. The pitch and roll rotations are set to zero.
public class TrackRot_Yaw : MonoBehaviour
{
    // Declare parameters
    private Vector3 pedPos;
    private Quaternion pedRot;
    private Vector3 headPos;
    private Quaternion headRot;
    private Vector3 aligningVector;
    private Vector3 currentVector;
    private Vector3 rotatedVector;
    private Vector3 diffVector;
    private Vector3 planeVector;

    // Start is called before the first frame update
    void Start()
    {
        // Set Aligning Vector
        aligningVector = new Vector3(0f, 0f, 10f);
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // Grab current head rotations and positions:
        headPos = GameObject.FindWithTag("ParticipantCam").transform.position;
        headRot = GameObject.FindWithTag("ParticipantCam").transform.rotation;

        // Find rotatedVector, currentVector and diffVector
        currentVector = aligningVector - headPos;
        rotatedVector = headRot * currentVector;
        diffVector = rotatedVector - transform.position;

        // Plane vector with x and z points, so y-axis does not change
        planeVector = new Vector3(diffVector.x, 0f, diffVector.z);
        //planeVector = Vector3.Project(diffVector, (transform.position + aligningVector));

        // Rotate body
        transform.rotation = Quaternion.Inverse(pedRot) * Quaternion.LookRotation(planeVector);


        // Sets the new rotation based on head movement
        //this.GetComponent<RectTransform>().rotation = pedRot * iniViewRotDisplacement;

        // Rotate the displacement vector to point to new gaze direction
        //currentViewPosDisplacement = pedRot * iniViewPosDisplacement;

        // Sets the new position of the head locked display
        //this.GetComponent<RectTransform>().position = pedPos + currentViewPosDisplacement;
    }




    /*private Quaternion Anchor_xz;

    void LateUpdate()
    {
        simpleVector = new Vector3(0f, 0f, 1f);

        pedRot = GameObject.FindWithTag("ParticipantCam").transform.rotation;

        Anchor_xz = Quaternion.Euler(new Vector3(0f, transform.rotation.eulerAngles.y, 0f));
        transform.rotation = Anchor_xz;
    }*/
}
