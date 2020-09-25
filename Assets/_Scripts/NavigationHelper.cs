using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(SplineCreator),typeof(RenderNavigation))]
public class NavigationHelper : MonoBehaviour
{
    [Header("Required Objects")]
    public HUDMaterials HUDMaterials;
    public Navigator car;

    [Header("Navigation settings")]
    
    //booleans for each navigation type
    public bool _renderVirtualCable; private bool renderVirtualCable;
    public bool _renderHighlightedRoad; private bool renderHighlightedRoad;
    public bool _renderHUD = true; private bool renderHUD { get; set; }
    
    public bool _pressMeToRerender = false; private bool pressMeToRerender = false;

    [Range(0.01f, 1f)]
    public float _transparency = 0.3f; private float transparency = 0.3f;

    private SplineCreator splineCreator;
    private float time;
    private float f_time;
    private void Awake()
    {
        renderVirtualCable = _renderVirtualCable;
        renderHighlightedRoad = _renderHighlightedRoad;

        splineCreator = gameObject.GetComponent<SplineCreator>();

        RenderNavigation();
       
        UpdateOrderIds(); //Make sure order ids are correct at startup
        SetNextAndPreviousWaypoints();
    }
    private void Update()
    {
        time = Time.realtimeSinceStartup;
        CheckChanges();
        f_time = Time.realtimeSinceStartup - time ;

        //Debug.Log($"CheckChanges(): {f_time}s");

        time = Time.realtimeSinceStartup;
        bool checkWaypoints = CheckNextAndPreviousWaypoints();
        f_time = Time.realtimeSinceStartup - time;
        
        //Debug.Log($"CheckNextAndPreviousWaypoints(): {f_time}s");

        if (checkWaypoints)
        {
            time = Time.realtimeSinceStartup;
            SetNextAndPreviousWaypoints();
            f_time = Time.realtimeSinceStartup - time;

            //Debug.Log($"SetNextAndPreviousWaypoints(): {f_time}s");
            
        }
        //Render the HUDs if needed
        if (renderHUD)
        {
            RenderNavigationArrow();
            RenderNavigationDistance();
        }
    }
    private void OnDrawGizmos()
    {
        //So that it also works in scene mode
        CheckChanges();
    }
    public (Vector3[], bool, bool, bool, float) GetNavigationInformation()
    {
        return (GetNavigationLine(), renderVirtualCable, renderHighlightedRoad, renderHUD, transparency);
    }
    public void PrepareNavigationForExperiment()
    {
        if (renderHUD) { RenderNavigationArrow(); }

        RenderNavigation();
        gameObject.GetComponent<RenderNavigation>().SetNavigationObjects();
        //Make take transparancy from the the experiment manager?
    }
    void CheckChanges()
    {
       /* if (splineCreator == null){ splineCreator = gameObject.GetComponent<SplineCreator>(); }
        if (renderVirtualCable != _renderVirtualCable || renderHighlightedRoad != _renderHighlightedRoad)
        {
            renderVirtualCable = _renderVirtualCable;
            renderHighlightedRoad = _renderHighlightedRoad;

            RenderNavigation();
        }
        //if render booleans change --> update mesh
        if (pressMeToRerender != _pressMeToRerender)
        {
            pressMeToRerender = _pressMeToRerender;
            
            splineCreator.MakeNavigation();
        }

        if (renderHUD != _renderHUD)
        {
            renderHUD = _renderHUD;
            car.HUD.SetActive(renderHUD);
        }
        if (transparency != _transparency)
        {
            transparency = _transparency;
            ChangTransparancyHUDAndConformal();
        }*/
    }
    void RenderNavigationArrow()
    {
        Transform arrows = car.HUD.transform.Find("Arrows");
        if (arrows == null) { Debug.Log("Arrows= null...."); return; }
        if (car.target.operation == Operation.TurnRightShort || car.target.operation == Operation.TurnRightLong) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.right; }
        else if (car.target.operation == Operation.TurnLeftLong) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.left; }
        else if (car.target.operation == Operation.Straight) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.straight; }
        else if (car.target.operation == Operation.EndPoint) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.destination; }

    }
    void RenderNavigationDistance()
    {

        Transform text = car.HUD.transform.Find("Text");
        TextMesh textMesh = text.gameObject.GetComponent<TextMesh>();

        float distanceToTarget = Vector3.Magnitude(car.target.transform.position - car.transform.position);
        int renderedDistance = ((int)distanceToTarget - ((int)distanceToTarget % 5));
        if (renderedDistance < 0) { renderedDistance = 0; }
        
        textMesh.text = $"{renderedDistance}m";
    }
    void ChangTransparancyHUDAndConformal()
    {
        Debug.Log($"Setting transpaarancy to {transparency}");
        Color color;
        //ChangeHMI all HUD arrows transparancy
        color = HUDMaterials.right.color; color.a = transparency;
        HUDMaterials.right.color = color;

        color = HUDMaterials.left.color; color.a = transparency;
        HUDMaterials.left.color = color;

        color = HUDMaterials.straight.color; color.a = transparency;
        HUDMaterials.straight.color = color;

        //Change transparancy of HUD text
        Transform text = car.HUD.transform.Find("Text");
        color = text.GetComponent<TextMesh>().color; color.a = transparency;
        text.GetComponent<TextMesh>().color = color;

        //change Conformal transparancy
        color = splineCreator.navigationPartMaterial.color; color.a = transparency;
        splineCreator.navigationPartMaterial.color = color;
    }
    private void RenderNavigation()
    {
        //Remove reference of navigatoinparts frm waypoints
        //RemoveNavigationPartsFromWaypoints();
        
        RenderNavigationType(NavigationType.VirtualCable, renderVirtualCable);
        RenderNavigationType(NavigationType.HighlightedRoad, renderHighlightedRoad);
        
        
    }
    public Vector3 [] GetNavigationLine()
    {
        return gameObject.GetComponent<SplineCreator>().GetNavigationLine();
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
        Waypoint waypoint = null;

        List<Waypoint> waypointList = new List<Waypoint>();
        List<Waypoint> orderedWaypointList = new List<Waypoint>();
        foreach (Transform child in transform)
        {
            waypoint = child.GetComponent<Waypoint>();
            if (waypoint != null) { waypointList.Add(waypoint); }
        }
        
        if(waypointList.Count() == 0) { return waypointList; }

        waypointList = waypointList.OrderBy(a => a.gameObject.name).ToList();
        waypoint = waypointList[0];
        int orderId = 0;
        while (waypoint!= null)
        {
            waypointList.Add(waypoint);
            waypoint.gameObject.name = "Waypoint " + orderId.ToString();
            waypoint.orderId = orderId;
            orderedWaypointList.Add(waypoint);
            
            waypoint = waypoint.nextWaypoint;
            orderId++;

        }
        return orderedWaypointList;
    }
    public void UpdateOrderIds()
    {
        //Updating order ids is now done in GetOrderedWaypointList;
        List<Waypoint> waypointList = GetOrderedWaypointList();
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
        //Finds all children of this gameobject
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
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
    public void SetUp(NavigationType navigationType, float _transparency, Navigator _car)
    {
        splineCreator.MakeNavigation();

        UpdateOrderIds(); //Make sure order ids are correct at startup
        SetNextAndPreviousWaypoints();

        transparency = _transparency;
        car = _car;
        ChangTransparancyHUDAndConformal();
        
        Vector3 HUD_low = car.transform.position + new Vector3(-0.3f,1.026f,1.56f);
        Vector3 HUD_high = car.transform.position + new Vector3(-0.3f, 1.474f, 1.56f);

        if (navigationType == NavigationType.VirtualCable) {
            renderHUD = false;
            car.HUD.SetActive(false); 
            RenderNavigationType(NavigationType.VirtualCable, true);
            RenderNavigationType(NavigationType.HighlightedRoad, false);
        }
        if (navigationType == NavigationType.HighlightedRoad) { 
            car.HUD.SetActive(false);
            renderHUD = false;
            RenderNavigationType(NavigationType.VirtualCable, false);
            RenderNavigationType(NavigationType.HighlightedRoad, true);
        }
        if(navigationType == NavigationType.HUD_low)
        {
            car.HUD.transform.position = HUD_low;
            car.HUD.SetActive(true);
            renderHUD = true;
            RenderNavigationType(NavigationType.VirtualCable, false);
            RenderNavigationType(NavigationType.HighlightedRoad, false);
        }
        if (navigationType == NavigationType.HUD_high)
        {
            car.HUD.SetActive(true);
            renderHUD = true;
            car.HUD.transform.position = HUD_high;
            RenderNavigationType(NavigationType.VirtualCable, false);
            RenderNavigationType(NavigationType.HighlightedRoad, false);
        }
    }
}
