using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathFollower : MonoBehaviour {

    public Transform[] PathNode;
    public GameObject Player;
    public float MoveSpeed;
    float Timer;
    int CurrentNode;
    static Vector3 CurrentPositionHolder;
    static Quaternion CurrentOrientationHolder;
    private Vector3 startPosition;
    private Quaternion startOrientation;
    private bool moveLeft = false;
    private bool moveRight = false;
    private bool moveStraight = true;
    private int Counter = 0;

    // Use this for initialization
    void Start ()
    {
        PathNode = GetComponentsInChildren<Transform>();
        CheckNode();
        Player = GameObject.FindGameObjectWithTag("Active");
	}
    // Update is called once per frame
    void Update()
    {
        Timer += Time.deltaTime * MoveSpeed;
        float moveHorizontal = Input.GetAxis("Horizontal");

        if (CurrentNode > 9 && moveHorizontal < 0f && moveStraight == true)
        {
            moveStraight = false;
            moveLeft = true;
            CurrentNode = 14;            

        }
        else if (CurrentNode > 9 && moveHorizontal > 0f && moveStraight == true)
        {
            moveStraight = false;
            moveRight = true;
            CurrentNode = 18;
        }
        else
        
        if (moveLeft == true)
        {
            if (CurrentNode >= 18)
            {
                moveLeft = false;
                CurrentNode = 22;
            }
        }
        else if (moveRight == true)
        {
            if (CurrentNode >= 21)
            {
                moveRight = false;
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
