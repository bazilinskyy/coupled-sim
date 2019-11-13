using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathStop : MonoBehaviour
{
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
    private bool Left = false;
    private bool Right = false;
    private bool FirstChoice = false;
    private bool moveStraight = true;
    private int Counter = 0;
    private Transform rotationAxis;
    // Use this for initialization
    void Start()
    {
        PathNode = GetComponentsInChildren<Transform>();
        CheckNode();
        rotationAxis = this.transform;
    }
    // Update is called once per frame
    void Update()
    {
        Timer += Time.deltaTime * MoveSpeed;
        float moveHorizontal = Input.GetAxis("Horizontal");
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
            //forward.y = 0;
            //Player.transform.position = forward.normalized * speed / 3.6f;

            //Vector3 forward = Vector3.Lerp(startPosition, CurrentPositionHolder, Timer);
            //Debug.Log(forward);
            //Vector3 richting = new vector3 (forward.normalize);
            //Debug.Log(richting);
            //Vector3 richtingxyz = new Vector3(CurrentPositionHolder.x - startPosition.x, CurrentPositionHolder.y - startPosition.y, CurrentPositionHolder.z - startPosition.z);
            //Vector3 DRIVE = richtingxyz.normalized;
            //Player.transform.position = DRIVE * MoveSpeed / 3.6f;
            //Player.transform.position = Vector3.Lerp(startPosition, CurrentPositionHolder, Timer);

          //  Player.transform.position =  new Vector3(
          //        Mathf.SmoothStep(startPosition.x, CurrentPositionHolder.x, Timer),
          //        Mathf.SmoothStep(startPosition.y, CurrentPositionHolder.y, Timer),
          //        Mathf.SmoothStep(startPosition.z, CurrentPositionHolder.z, Timer));
            

            //Player.transform.rotation = Quaternion.Lerp(startOrientation, CurrentOrientationHolder, Timer);
            //Player.transform.rotation = Quaternion.Euler(0, rotationAxis.rotation.eulerAngles.y, 0);
            











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
