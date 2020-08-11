using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderNavigation : MonoBehaviour
{
    //Makes sure only the near navigatoin is rendered and not the whole path.
    public int numberOfWaypoints = 2;
    private Navigator navigator;
    private Transform navigation;
    private NavigationHelper navigationHelper;
    private Waypoint target;

    private List<Waypoint> waypoints;
    // Start is called before the first frame update

    private void Awake()
    {
        navigator = transform.GetComponent<Navigator>();
        //Set appropriate navigation helper, navigation and target
        ResetRendering();
    }
    void Update()
    {
        if(target != navigator.target)
        {
            target = navigator.target;
            SetAllRenderMeToFalse();
            SetRenderMeAttributes();
        }
    }
    public void ResetRendering()
    {
        //Set navigation variables and such
        navigationHelper = navigator.navigation.GetComponent<NavigationHelper>();
        navigation = navigator.navigation;
        target = navigator.target;
        waypoints = navigationHelper.GetOrderedWaypointList();

        //Set rendering attributes
        SetAllRenderMeToFalse();
        SetRenderMeAttributes();

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
