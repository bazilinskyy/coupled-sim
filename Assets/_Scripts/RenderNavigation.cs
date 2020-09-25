using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NavigationHelper))]
public class RenderNavigation : MonoBehaviour
{
    //Makes sure only the near navigatoin is rendered and not the whole path.
    public int numberOfWaypoints = 1;
    private Navigator navigator;
    private NavigationHelper navigationHelper;
    private Waypoint target;

    private List<Waypoint> waypoints;
    // Start is called before the first frame update


    void Update()
    {
        if(navigationHelper == null) { navigationHelper = gameObject.GetComponent<NavigationHelper>(); }
        if(navigator == null) { navigator = navigationHelper.car; }
        if(navigator == null) { return; } //No navigation set -> free driving
        if(waypoints == null) { waypoints = navigationHelper.GetOrderedWaypointList(); }
        
        if(target != navigator.GetCurrentTarget())
        {
            target = navigator.GetCurrentTarget();
            RenderNavigationSymbology();
        }
        
    }
    public void RenderNavigationSymbology()
    {
        SetAllRenderMeToFalse();
        SetRenderMeAttributes();
    }
    public void SetNavigationObjects()
    {
        //Set navigation variables and such
        navigationHelper = gameObject.GetComponent<NavigationHelper>();
        navigator = navigationHelper.car;
        
        target = navigator.GetCurrentTarget();
        waypoints = navigationHelper.GetOrderedWaypointList();

        RenderNavigationSymbology();

    }
    void SetRenderMeAttributes()
    {
        //Sets renderMe to true for previous waypoint up to next numberOfWaypoints (spline points are not counted)
        Waypoint currentWaypoint = target;
        
        SetPreviousWaypointToTrue();
        
        int renderedWaypoints = 0;

        while (currentWaypoint != null && renderedWaypoints <= numberOfWaypoints)
        {
            currentWaypoint.RenderMe(true);
            //Skip spline points in the count
            if (currentWaypoint.operation != Operation.SplinePoint) { renderedWaypoints++; }

            currentWaypoint = currentWaypoint.nextWaypoint;
            
        }
    }
    void SetPreviousWaypointToTrue()
    {
        //Set previous waypoint to true, spline points dont count
        if (target.previousWaypoint == null) { return; } //skip if no pevious waypoint

        //Set first previouswaypoint to rendering
        Waypoint previousWaypoint = target.previousWaypoint;
        previousWaypoint.RenderMe(true);
        
        //Check if spline point --> also do the one before the previous one
        while (previousWaypoint.operation == Operation.SplinePoint)
        {
            previousWaypoint = previousWaypoint.previousWaypoint;
            previousWaypoint.RenderMe(true);
        }
    }
    void SetAllRenderMeToFalse()
    {
        foreach( Waypoint waypoint in waypoints)
        {
            waypoint.RenderMe(false);
        }
    }
}
