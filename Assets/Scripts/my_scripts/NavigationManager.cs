using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NavigationManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public Transform GetStartPointNavigation()
    {

    Transform startPoint = new GameObject().transform;
    foreach (Transform child in transform)
    {
        if (child.GetComponent<Waypoint>().operation == Operation.StartPoint)
        {
            //Reduce height by height of system (the waypoint system is above the ground....) 
            startPoint = child;
            float heightSystem = this.GetComponent<SplineCreator>().heightSystem;
            startPoint.position = startPoint.position - new Vector3(0, heightSystem, 0);
        }
    }
    return startPoint;
    }

    public Waypoint GetFirstTarget()
    {
        Waypoint waypoint = null;
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Waypoint>().orderId == 1)
            {
                //Reduce height by height of system (the waypoint system is above the ground....) 
                waypoint =  child.GetComponent<Waypoint>();
            }
        }

        if (waypoint == null) { throw new System.Exception("Something went wrong in NavigationManager --> GetFirstTarget()"); }

        return waypoint;
    }
}
