using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDPlacer : MonoBehaviour
{
    public float visualAngleVertical; 
    public float visualAngleHorizontal;
    public float distance;
    public Transform headPosition;

    public bool _pressMeToPlace; private bool pressMeToPlace;
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

    void OnDrawGizmos()
    {
        CheckForChanges();
    }
    void PlaceHUD()
    {
        float visualAngleVerticalRadians = visualAngleVertical * Mathf.PI / 180;
        float visualAngleHorizontalRadians = visualAngleHorizontal * Mathf.PI / 180;
        Vector3 forwardVector = new Vector3(0, Mathf.Tan(visualAngleVerticalRadians) * distance, distance);
        Vector3 sideVector = new Vector3(Mathf.Tan(visualAngleHorizontalRadians), 0, 0);
        transform.position = headPosition.position + forwardVector + sideVector;
    }
}
