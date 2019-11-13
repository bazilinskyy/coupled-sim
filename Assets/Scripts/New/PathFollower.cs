using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class PathFollower : MonoBehaviour {

    CheckID obj;
    public Transform[] PathNode;
    public GameObject Player;
    public float MoveSpeed = 35f;
    float Timer;
    int CurrentNode;
    static Vector3 CurrentPositionHolder;
    static Quaternion CurrentOrientationHolder;
    private Vector3 startPosition;
    private Quaternion startOrientation;
    private bool Left = false;
    private bool Right = false;
    private bool FirstChoice = false;
    private bool moveStraight = true;
    private int Counter = 0;

    private Transform rotationAxis;
    

    // Called by the Host after the level was fully loaded
    public void Start()
    {
        //obj = GameObject.FindObjectOfType<CheckID>();
        //netID = obj.GetComponent<NetworkIdentity>().netId;
        //Player = NetworkServer.FindLocalObject(netID);
        //Debug.Log("Fout");
        PathNode = GetComponentsInChildren<Transform>();
        CheckNode();        
        
        rotationAxis = this.transform;        
    }
    private void Update()
    {
        //obj = GameObject.FindObjectOfType<CheckID>();
        //netID = obj.GetComponent<NetworkIdentity>().netId;
        //Player = CheckID.myObject;
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        //Player = GameObject.FindGameObjectWithTag("EvasiveCar");
        Timer += Time.deltaTime * MoveSpeed;
        float moveHorizontal = Input.GetAxis("Horizontal");
        if (CurrentNode >= 44 && CurrentNode <= 45)
        {
            if (moveHorizontal < 0f && FirstChoice == false)
            {
                Left = true;
                Right = false;
                FirstChoice = true;
            }
        }
        if (CurrentNode >= 44 && CurrentNode <= 45)
        {
            if (moveHorizontal > 0f && FirstChoice == false)
            {
                Left = false;
                Right = true;
                FirstChoice = true;
            }
        }
        if (CurrentNode == 45 && Left == true && moveStraight == true)
        {
            moveStraight = false;
            Left = true;
            CurrentNode = 50;            

        }
        else if (CurrentNode ==45  && Right == true && moveStraight == true)
        {
            moveStraight = false;
            Right = true;
            CurrentNode = 58;
        }
        else
        
        if (Left == true)
        {
            if (CurrentNode >= 57)
            {
                Left = false;
                CurrentNode = 65;
            }
        }
        else if (Right == true)
        {
            if (CurrentNode >= 50)
            {
                Right = false;
               
            }
        }

        MovePlayer();
    }
    void CheckNode()
    {
        Timer = 0;
        startPosition = Player.transform.position;
        startOrientation = Player.transform.rotation;
        CurrentPositionHolder = PathNode[CurrentNode].transform.position;
        CurrentOrientationHolder = PathNode[CurrentNode].transform.rotation;
    }
    void MovePlayer()
    {
        if (Player.transform.position != CurrentPositionHolder)
        {
           //var forward = rotationAxis.forward;
           // forward.y = 0;
           //Player.transform.position = forward.normalized * MoveSpeed / 3.6f;

            //Vector3 forward = Vector3.Lerp(startPosition, CurrentPositionHolder, Timer);
            //Player.transform.position = forward.normalized * MoveSpeed / 3.6f;
            //Player.transform.rotation = Quaternion.Euler(0, rotationAxis.rotation.eulerAngles.y, 0);
            Player.transform.position = Vector3.Lerp(startPosition, CurrentPositionHolder, Timer);
            Player.transform.rotation = Quaternion.Lerp(startOrientation, CurrentOrientationHolder, Timer);
           
            
        }
        else
        {

            if (CurrentNode < PathNode.Length - 1)
            {
                CurrentNode++;
                CheckNode();
            }
        }
    }
}
