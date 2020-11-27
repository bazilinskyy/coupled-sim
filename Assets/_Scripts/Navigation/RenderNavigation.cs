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

    private void Awake()
    {
        navigationHelper = gameObject.GetComponent<NavigationHelper>();
    }
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
    public void SetUpNavigationRenderer(Navigator _navigator, List<Waypoint> _waypoints, Waypoint _target)
    {
        //Set navigation variables and such

        navigator = _navigator;
        waypoints = _waypoints;
        target = _target; 
        

        RenderNavigationSymbology();

    }
    void SetRenderMeAttributes()
    {
        //Sets renderMe to true for previous waypoint up to next numberOfWaypoints (spline points are not counted)
        Waypoint currentWaypoint = target;
        
        SetTwoPreviousWaypointToTrue();
        
        int renderedWaypoints = 0;

        while (currentWaypoint != null && renderedWaypoints <= numberOfWaypoints)
        {
            currentWaypoint.RenderMe(true);
            if(currentWaypoint.previousWaypoint != null) { currentWaypoint.previousWaypoint.RenderMe(true); }
            //Skip spline points in the count
            if (currentWaypoint.operation != Operation.SplinePoint) { renderedWaypoints++; }

            currentWaypoint = currentWaypoint.nextWaypoint;
            
        }
    }
    void SetTwoPreviousWaypointToTrue()
    {
        //Set previous waypoint to true, spline points dont count
        if (target.previousWaypoint == null) { return; } //skip if no pevious waypoint

        Waypoint previousWaypoint = target;
        int renderCount = 0;

        //Set two previous waypoints to still render (not counting splinepoints)
        while (renderCount <2)
        {
            previousWaypoint = previousWaypoint.previousWaypoint;
            
            if (previousWaypoint == null) { break; }

            previousWaypoint.RenderMe(true);
            if (previousWaypoint.operation != Operation.SplinePoint) { renderCount++; }
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
