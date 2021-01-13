using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDPlacer : MonoBehaviour
{
    public float visualAngleVertical;
    public float visualAngleHorizontal;
    public float distance;
    public Transform car;
    public Transform headPosition;

    public BoxCollider boxCollider;

    private bool pressMeToPlace; public bool _pressMeToPlace; 

    private Vector3 forwardVector;
    private Vector3 sideVector;
    private void Start()
    {
        PlaceHUD();
    }
    void CheckForChanges() {
        if (pressMeToPlace != _pressMeToPlace)
        {
            pressMeToPlace = _pressMeToPlace;
            PlaceHUD();
        }
    }
    private void Update()
    {
        CheckForChanges();
    }
    void OnDrawGizmos()
    {
        CheckForChanges();
        DrawLines();
    }
    public void PlaceHUD()
    {
        float visualAngleVerticalRadians = visualAngleVertical * Mathf.PI / 180;
        float visualAngleHorizontalRadians = visualAngleHorizontal * Mathf.PI / 180;

        float up = Mathf.Tan(visualAngleVerticalRadians);
        float right = Mathf.Tan(visualAngleHorizontalRadians) * distance;
        float forward = distance;

        transform.position = headPosition.position + car.forward * forward + car.right * right + car.up * up;
    }

    public void SetBoxCollider(TurnType turn)
    {
        Vector3 sizeBoxTurns = new Vector3(5.2f, 0, 10.5f);
        Vector3 sizeBoxStraight = new Vector3(2.5f, 0, 10.5f);
        Vector3 sizeBoxEndPoint = new Vector3(4f, 0, 10.5f);

        if (turn.IsLeftTurn()) { boxCollider.center = new Vector3(2.25f, 0, 0); boxCollider.size = sizeBoxTurns; }
        else if (turn.IsRightTurn()){ boxCollider.center = new Vector3(-2.5f, 0, 0); boxCollider.size = new Vector3(5.7f,0,10.5f); }
        else if (turn.IsStraight()){ boxCollider.center = new Vector3(0,0,0); boxCollider.size = sizeBoxStraight; }
        else if (turn.IsEndPoint()) { boxCollider.center = new Vector3(-0.5f,0,0); boxCollider.size = sizeBoxEndPoint; }
    }
    public void SetAngles(float vertical, float horizontal, float _distance)
    {
        visualAngleVertical = vertical;
        visualAngleHorizontal = horizontal;
        distance = _distance;
    }
    void DrawLines() 
    { 
        if(!(forwardVector == Vector3.zero) && !(sideVector == Vector3.zero))
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(headPosition.position, transform.position);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(headPosition.position, headPosition.position + headPosition.forward);
        }
    }

}
