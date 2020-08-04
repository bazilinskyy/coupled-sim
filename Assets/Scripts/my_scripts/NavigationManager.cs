using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class NavigationManager : MonoBehaviour
{
   
    public Transform GetStartPointNavigation()
    {

    Transform startPoint = new GameObject().transform;
    foreach (Transform child in transform)
    {
        if (child.GetComponent<Waypoint>().operation == Operation.StartPoint)
        {
            //Reduce height by height of system (the waypoint system is above the ground....) 
            
            float heightSystem = this.GetComponent<SplineCreator>().heightSystem;
            startPoint.position = child.position - new Vector3(0, heightSystem, 0);
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

    public List<Waypoint> GetOrderedWaypointList()
    {
        List<Waypoint> waypointList = new List<Waypoint>();
        foreach(Transform child in transform)
        {
            waypointList.Add(child.GetComponent<Waypoint>());
        }
         
        //Order by id's
        List<Waypoint> orderedWaypointList = waypointList.OrderBy(a => a.orderId).ToList();
        return orderedWaypointList;

    }
}
