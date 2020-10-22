using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDPlacer : MonoBehaviour
{
    public float visualAngleVertical;
    public float visualAngleHorizontal;
    public float distance;
    public Transform headPosition;

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
        forwardVector = new Vector3(0, Mathf.Tan(visualAngleVerticalRadians) * distance, distance);
        sideVector = new Vector3(Mathf.Tan(visualAngleHorizontalRadians), 0, 0);
        transform.position = headPosition.position + forwardVector + sideVector;
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
