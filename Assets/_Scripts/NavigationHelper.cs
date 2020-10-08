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
    public bool _renderHUD = true; private bool renderHUD;
    
    public bool _pressMeToRerender = false; private bool pressMeToRerender = false;

    public TargetCountInfo targetCountInfo;
    public List<DifficultyCount> targetDifficultyList;

    [Range(0.01f, 1f)]
    public float _transparency = 0.3f; private float transparency = 0.3f;

    private SplineCreator splineCreator;
    private List<Waypoint> generalWaypointList;
    private void Awake()
    {
        renderVirtualCable = _renderVirtualCable;
        renderHighlightedRoad = _renderHighlightedRoad;

        splineCreator = gameObject.GetComponent<SplineCreator>();

        generalWaypointList = GetOrderedWaypointList();

        //RenderNavigation();
       
        UpdateOrderIds(); //Make sure order ids are correct at startup
        
    }
    private void Update()
    {
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
    void CheckChanges()
    {
        if (targetCountInfo.totalTargets != GetAllTargets().Count()) { targetCountInfo = GetTargetCountInfo(); targetDifficultyList = GetTargetDifficultyList(); }
        //Dont do this while application is running
        if (Application.isPlaying) { return; }

        if (splineCreator == null) { splineCreator = gameObject.GetComponent<SplineCreator>(); }
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
            if (car != null) { car.HUD.SetActive(renderHUD); } 
        }
        if (transparency != _transparency)
        {
            transparency = _transparency;
            ChangTransparancyHUDAndConformal();
        }
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
        if(car == null) { return; }
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
    public List<DifficultyCount> GetTargetDifficultyList()
    {
        List<Target> targets = GetAllTargets();
        List<DifficultyCount> countList = new List<DifficultyCount>();
        //Fill list with the avialable difficulties
        var difficulties = EnumUtil.GetValues<TargetDifficulty>();
        foreach(TargetDifficulty difficulty in difficulties) { countList.Add(new DifficultyCount(difficulty)); }

        //Count the difficulties set for this navigation
        foreach (Target target in targets)
        {
            foreach(DifficultyCount difficultyCount in countList) { if(difficultyCount.difficulty == target.difficulty) { difficultyCount.AddOne(); break; } }
        }
        
        return countList;
    }
    public TargetCountInfo GetTargetCountInfo()
    {
        TargetCountInfo info = new TargetCountInfo();
        List<Target> targets = GetAllTargets();
        info.totalTargets = targets.Count();

        foreach (Target target in targets)
        {
            if(target.GetRoadSide() == Side.Left) { info.AddLeft(); }
            if(target.GetRoadSide() == Side.Right) { info.AddRight(); }
        }
        return info;
    }
    public List<Target> GetAllTargets()
    {
        List<Target> targetList = new List<Target>();
        foreach (Waypoint waypoint in GetOrderedWaypointList())
        {
            //Means it is being rendered as well as its targets
            foreach (Target target in waypoint.GetTargets()){ targetList.Add(target); }
        }
        return targetList;
    }
    public List<Waypoint> GetOrderedWaypointList()
    {
        //If application is playing and we already defined waypointList once, simply return the already calculated list
        if (Application.isPlaying && generalWaypointList != null) { return generalWaypointList; }
        
        Waypoint waypoint = null;
        List<Waypoint> _waypointList = new List<Waypoint>();
        List<Waypoint> orderedWaypointList = new List<Waypoint>();

        //Get all waypoints
        foreach (Transform child in transform)
        {
            waypoint = child.GetComponent<Waypoint>();
            if (waypoint != null) { _waypointList.Add(waypoint); }
        }

        if (_waypointList.Count() == 0) { return _waypointList; }

        //OPrder them based on name
        _waypointList = _waypointList.OrderBy(a => a.gameObject.name).ToList();
        //Get the lowest one (e.g., waypoint 0)
        waypoint = _waypointList[0];
        
        //Order from e.g., waypoint 0, using next waypoint attribte. Also sets order Id's. Also sets names to be from Waypoint 0 up to Waypoint N.
        int orderId = 0;
        while (waypoint != null)
        {
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
        
        //Reset all targets
        foreach (Target target in GetAllTargets()) { target.ResetTarget(); }

        UpdateOrderIds(); //Make sure order ids are correct at startup

        transparency = _transparency;
        car = _car;
        ChangTransparancyHUDAndConformal();

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
            renderHUD = true;
            car.HUD.SetActive(true);
            HUDPlacer HUDPlacer = car.HUD.GetComponent<HUDPlacer>();
            HUDPlacer.SetAngles(-3f, 0, 2f); HUDPlacer.PlaceHUD();
            RenderNavigationType(NavigationType.VirtualCable, false);
            RenderNavigationType(NavigationType.HighlightedRoad, false);
        }
        if (navigationType == NavigationType.HUD_high)
        {
            renderHUD = true;
            car.HUD.SetActive(true);
            HUDPlacer HUDPlacer = car.HUD.GetComponent<HUDPlacer>();
            HUDPlacer.SetAngles(12f, 0, 2f); HUDPlacer.PlaceHUD();
            RenderNavigationType(NavigationType.VirtualCable, false);
            RenderNavigationType(NavigationType.HighlightedRoad, false);
        }
    }
}
[System.Serializable]
public class DifficultyCount
{
    public TargetDifficulty difficulty;
    public int count;

    public DifficultyCount(TargetDifficulty _difficulty)
    {
        difficulty = _difficulty;
        count = 0;
    }
    
    public void AddOne()
    {
        count++;
    }
}
[System.Serializable]
public class TargetCountInfo
{
    public int totalTargets;
    public int LeftPosition;
    public int rightPosition;

    public TargetCountInfo() { totalTargets = LeftPosition = rightPosition = 0; }
    public void AddLeft(){ LeftPosition++; }
    public void AddRight() { rightPosition++; }
}