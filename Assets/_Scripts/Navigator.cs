using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
public class Navigator : MonoBehaviour
{
    //Current target waypoint and waypoint tree
    public Transform navigation;
    
    public RoadParameters roadParameters;

    public Waypoint target;
    private NavigationHelper navigationHelper { get; set; }

    public bool navigationFinished = false;
    public float metersAfterPassingWaypoint = 5f;

    private float distanceTravelled=0f;
    private Vector3 lastPosition = Vector3.zero;
   
    public GameObject HUD;
     private void Awake()
    {
        //Set some variables
        if (navigation == null && target == null) { return; }
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        navigationFinished = false;

        if (target == null) {target = navigationHelper.GetFirstTarget();}
    }
    void Update()
    {
        if (navigation == null && target == null) { return; }

        if (GetNextTarget())
        {
            SetNextTarget();
        }
        else if(target.operation == Operation.EndPoint)
        {
            float distanceToFinish = Vector3.Magnitude(target.transform.position - transform.position);
            if (distanceToFinish < 2){ navigationFinished = true;}
        }
    }
    public Waypoint GetCurrentTarget()
    {
        return target;
    }
    public void SetNewNavigation(Transform _navigation)
    {
        navigation = _navigation;
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        target = navigationHelper.GetFirstTarget();
        
        navigationFinished = false;
    }
    public Transform GetNavigation()
    {
        return navigation;
    }
    private bool GetNextTarget()
    {
        //Waits for a certain distance to be travelled after passing waypoint before passing true
        bool getNextTarget = false;
       
        if (PassedTargetWaypoint())
        {
            if (lastPosition == Vector3.zero) { lastPosition = transform.position; }
            distanceTravelled += Vector3.Distance(transform.position, lastPosition);
            lastPosition = transform.position;
        }
        //Reset variables and pass a true 
        if(distanceTravelled >= metersAfterPassingWaypoint)
        {
            getNextTarget = true;
            distanceTravelled = 0f;
            lastPosition = new Vector3(0, 0, 0);
        }
        return getNextTarget;
    }
    private bool PassedTargetWaypoint()
    {
        //Passed waypoint if 
        //(1) passes the plane made by the waypoint and its forward direction. 
        //(2) Is within 4*roadwidth distance of the waypoint
        // plane equation is A(x-a) + B(y-b) + C(z-c) = 0 = dot(Normal, planePoint - targetPoint)
        // Where normal vector = <A,B,Z>
        // pos = the cars position (x,y,z,)
        // a point on the plane Q= (a,b,c) i.e., waypoint position

        bool passedWaypoint;
        float sign = Vector3.Dot(target.transform.forward, (target.transform.position - transform.position));
        float distance = Vector3.Distance(target.transform.position, transform.position);
        
        if (sign <= 0) { passedWaypoint = true; }
        else { passedWaypoint = false; }
        
        return passedWaypoint;
    }
    private void SetNextTarget()
    {
        if(target.nextWaypoint != null && target.operation != Operation.EndPoint)
        {
            target = target.nextWaypoint;
            //Skip splinepoints
            while (target.operation == Operation.SplinePoint && target.nextWaypoint !=null){ target = target.nextWaypoint; }
        }
        else
        {
            navigationFinished = true;
        }
    }
    public NavigationHelper GetNavigationHelper() { return navigationHelper;}
}
