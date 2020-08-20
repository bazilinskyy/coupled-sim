using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class NavigationHelper : MonoBehaviour
{

    private void Awake()
    {
        UpdateOrderIds(); //Make sure order ids are correct at startup
        SetNextAndPreviousWaypoints();
    }
    private void Update()
    {
        if (CheckNextAndPreviousWaypoints())
        {
            SetNextAndPreviousWaypoints();
        }
    }
    public Transform GetStartPointNavigation()
    {

    return GetOrderedWaypointList()[0].gameObject.transform;
    }
    public Waypoint GetFirstTarget()
    {
        //Should give the second waypoint which is a none-splinepoint in the list 
        //( First waypoint is beginning point)
        Waypoint target=null;
        foreach (Waypoint waypoint in GetOrderedWaypointList())
        {
           //Skip first one
           if (waypoint.orderId == 0) { continue; }
           if(waypoint.operation != Operation.SplinePoint)
            {
                target = waypoint;
                break;
            }
        }
        if (target == null) { throw new System.Exception("Error in NavigationHelper -> First traget not found... The navigation list should start with oprderId 0, and contain at least 1 none-spline-point/begin.... "); }

        return target;
    }
    public List<Target> GetActiveTargets()
    {
        List<Target> targetList = new List<Target>();
        foreach (Waypoint waypoint in GetOrderedWaypointList())
        {
            //Means it is being rendered as well as its targets
            if (waypoint.renderMe)
            {
                foreach (Target target in waypoint.GetTargets())
                {
                    
                    //If not detected yet --> Add to potential target list
                    if (!target.IsDetected())
                    {
                        targetList.Add(target);
                    }
                }
            }
        }
        return targetList;
    }
    public List<Waypoint> GetOrderedWaypointList()
    {
        List<Waypoint> waypointList = new List<Waypoint>();
        foreach(Transform child in transform)
        {
            Waypoint waypoint = child.GetComponent<Waypoint>();
            if (waypoint != null) { waypointList.Add(waypoint); }             
        }
         
        //Order by id's
        List<Waypoint> orderedWaypointList = waypointList.OrderBy(a => a.orderId).ToList();
        return orderedWaypointList;

    }
    public void UpdateOrderIds()
    {
        List<Waypoint> waypointList = GetOrderedWaypointList();

        int OrderId = 0;
        foreach (Waypoint waypoint in waypointList)
        {
            waypoint.orderId = OrderId;
            OrderId++;
        }
    }
    public bool CheckNextAndPreviousWaypoints()
    {
        //Checks previous and next waypoints. IF a error is there we reSet them
        bool reSetWaypointNeihgbours = false;
        List<Waypoint> waypointList = GetOrderedWaypointList();

        foreach(Waypoint waypoint in waypointList)
        {
            if(waypoint.operation == Operation.EndPoint && waypoint.previousWaypoint !=null) { continue; }
            if (waypoint.nextWaypoint != null || waypoint.previousWaypoint != null) {  reSetWaypointNeihgbours = true;  }
        }

        return reSetWaypointNeihgbours;

    }
    public void SetNextAndPreviousWaypoints()
    {
        List<Waypoint> waypointList = GetOrderedWaypointList();
        for (int i =0; i<waypointList.Count; i++)
        {
            Waypoint waypoint = waypointList[i];
            if (i == 0) {waypoint.nextWaypoint = waypointList[i + 1];}
            else if(i == (waypointList.Count - 1)) { waypoint.previousWaypoint = waypointList[i - 1]; }
            else
            {
                waypoint.nextWaypoint = waypointList[i + 1];
                waypoint.previousWaypoint = waypointList[i - 1];
            }   
        }
    }
    public List<NavigationPart> GetListNavigationPart(NavigationType navigationType)
    {
        List<NavigationPart> navigationPartList = new List<NavigationPart>();
        Transform parentVirtualCable;
        foreach(Transform child in transform)
        {
            if (child.name == navigationType.ToString())
            {
                parentVirtualCable = child;
                break;
            }
        }

        if (parentVirtualCable =null) { throw new System.Exception("Error in NavigationHelper -> NavigationParts should be put parent gameObject with the same name as their navigationType..."); }

        //loop through children of virtualCableParent
        foreach(Transform child in parentVirtualCable)
        {
            NavigationPart navigationPart = child.GetComponent<NavigationPart>();
            if (navigationPart != null) { navigationPartList.Add(navigationPart); }
        }
        return navigationPartList;
    }
    public void RemoveNavigationPartsFromWaypoints()
    {
        List<Waypoint> waypointList = GetOrderedWaypointList();

        foreach(Waypoint waypoint in waypointList)
        {
            waypoint.RemoveNavigationParts();
        }
    }
    public void RenderNavigationType(NavigationType navigationType, bool active)
    {
        //Fins all children of this gameobject
        foreach (Transform child in GameObject.Find(transform.name).GetComponentsInChildren<Transform>(true))
        {
            //If we found the one matching the navigationType we set it to {active}
            if (child.name == $"{navigationType.ToString()}")
            {
                child.gameObject.SetActive(active);  break;
            }
        }
    }
    public void RenderAllWaypoints(bool render)
    {
        foreach(Waypoint waypoint in GetOrderedWaypointList())
        {
            waypoint.RenderMe(render);
        }
    }
}
