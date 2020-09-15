using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

[RequireComponent(typeof(RenderNavigation))]
public class Navigator : MonoBehaviour
{
    //Current target waypoint and waypoint tree
    public Transform navigation;
    
    public RoadParameters roadParameters;

    public Waypoint target;
    private NavigationHelper navigationHelper { get; set; }

    public bool navigationFinished = false;
    public float distanceAfterWaypoint = 5f;

    private float distanceTravelled=0f;
    private Vector3 lastPosition = Vector3.zero;

    [Range(0.01f, 1f)]
    public float _transparency = 0.3f; private float transparency = 0.3f;

/*    public bool _renderVirtualCable = true; private bool renderVirtualCable { get; set; }
    public bool _renderHighlightedRoad = true; private bool renderHighlightedRoad { get; set; }*/
    public bool _renderHUD = true; private bool renderHUD { get; set; }


    public GameObject HUD;
    public Material right;
    public Material left;
    public Material straight;
    public Material destination;

    private void Awake()
    {
        //Set some variables
        if (navigation == null && target == null) { return; }
        navigationHelper = navigation.GetComponent<NavigationHelper>();
        navigationFinished = false;
        CheckOptionInput();

        if (target == null) {target = navigationHelper.GetFirstTarget();}

        //Render the HUDs if needed
        if (renderHUD) { 
            RenderNavigationArrow();
            RenderNavigationDistance();
        }
    }
    void Update()
    {
        if (navigation == null && target == null) { return; }
        CheckOptionInput();

        if (GetNextTarget())
        {
            SetNextTarget();
            RenderNavigationArrow();
        }
        else if(target.operation == Operation.EndPoint)
        {
            float distanceToFinish = Vector3.Magnitude(target.transform.position - transform.position);
            if (distanceToFinish < 2)
            {
                navigationFinished = true;
            }

        }
        //Renders the instructions on HUD
        if (renderHUD) { RenderNavigationDistance(); }
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
        GetComponent<RenderNavigation>().SetNavigationObjects();
        navigationFinished = false;
        RenderNavigationArrow();
    }
    public Transform GetNavigation()
    {
        return navigation;
    }
    void CheckOptionInput()
    {
        //if resolution change --> update transparancy
        if (transparency != _transparency)
        {
            transparency = _transparency;
            ChangTransparancyHUDAndConformal();
        }
        //if render booleans change --> update mesh
       /* if (renderVirtualCable != _renderVirtualCable || renderHighlightedRoad != _renderHighlightedRoad)
        {
            renderVirtualCable = _renderVirtualCable;
            renderHighlightedRoad = _renderHighlightedRoad;

            navigation.GetComponent<SplineCreator>()._renderVirtualCable = renderVirtualCable;
            navigation.GetComponent<SplineCreator>()._renderHighlightedRoad = renderHighlightedRoad;
        }*/

        if(renderHUD != _renderHUD)
        {
            renderHUD = _renderHUD;
            HUD.SetActive(renderHUD);
        }
    }
    private void OnDrawGizmos()
    {
        CheckOptionInput();
    }
    void ChangTransparancyHUDAndConformal()
    {
        Color color;
        //ChangeHMI all HUD arrows transparancy
        color = right.color;    color.a = transparency;
        right.color = color;

        color = left.color; color.a = transparency;
        left.color = color;

        color = straight.color; color.a = transparency;
        straight.color = color;

        //Change transparancy of HUD text
        Transform text = HUD.transform.Find("Text");
        color = text.GetComponent<TextMesh>().color; color.a = transparency;
        text.GetComponent<TextMesh>().color = color;

        //change Conformal transparancy
        color = GetNavigation().GetComponent<SplineCreator>().navigationPartMaterial.color; color.a = transparency;
        GetNavigation().GetComponent<SplineCreator>().navigationPartMaterial.color = color;
    }
    void RenderNavigationArrow()
    {
        Transform arrows = HUD.transform.Find("Arrows");

        if (target.operation == Operation.TurnRightShort || target.operation == Operation.TurnRightLong) { arrows.gameObject.GetComponent<MeshRenderer>().material = right; }
        else if (target.operation == Operation.TurnLeftLong) { arrows.gameObject.GetComponent<MeshRenderer>().material = left; }
        else if (target.operation == Operation.Straight) { arrows.gameObject.GetComponent<MeshRenderer>().material = straight; }
        else if (target.operation == Operation.EndPoint) { arrows.gameObject.GetComponent<MeshRenderer>().material = destination; }
        
    }
    void RenderNavigationDistance()
    {
        
        Transform text = HUD.transform.Find("Text");
        TextMesh textMesh = text.gameObject.GetComponent<TextMesh>();

        float distanceToTarget = Vector3.Magnitude(target.transform.position - transform.position);
        int renderedDistance = ((int)distanceToTarget - ((int)distanceToTarget % 5));
        if (renderedDistance < 0) { renderedDistance = 0; }

        if (target.operation == Operation.TurnRightShort || target.operation == Operation.TurnRightLong) { textMesh.text = $"{renderedDistance}m"; }
        else if (target.operation == Operation.TurnLeftLong){ textMesh.text = $"{renderedDistance}m"; }
        else if (target.operation == Operation.Straight){ textMesh.text = $"{renderedDistance}m"; }
        else if (target.operation == Operation.EndPoint) { textMesh.text = $"{renderedDistance}m"; }
        
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
        if(distanceTravelled >= distanceAfterWaypoint)
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
