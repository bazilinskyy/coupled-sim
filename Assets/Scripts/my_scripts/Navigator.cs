using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
public class Navigator : MonoBehaviour
{
    //Current target waypoint and waypoint tree
    public GameObject navigation;
    public Waypoint target;

    //Did it reach the target?
    public float marginWaypointReached = 2;   
    public bool reachedTarget = false;
    private float distance_to_target;

    [Range(0.01f, 1f)]
    public float _transparency = 0.3f; private float transparency = 0.3f;

    public bool _renderConformal1 = true; private bool renderConformal1 = true;
    public bool _renderConformal2 = true; private bool renderConformal2 = true;
    public bool _renderConformal3 = true; private bool renderConformal3 = true;
    public bool _renderHUD = true; private bool renderHUD = true;

    //Kinda outdated navError stuff
    //private float initial_distance_to_target = 0;
    //public bool navError = false;
    //public float nav_error_margin = 2;

    //All variables related tot he HUD

    public GameObject HUD;
    public Material right;
    public Material left;
    public Material straight;
    public Material destination;

    //Determines which waypoints of the navigation get rendered
    public int renderDistanceNavigation = 4;

    //Render distance of other road-users
    public bool renderPedestrians = true;
    public bool renderVehicles = true;
    public int renderDistanceOthers = 50;

    private GameObject[] pedestrianList;
    private GameObject[] vehicleList;

    private bool foundOthers =false;
    private void Awake()
    {
        if (renderHUD) { 
            RenderNavigationArrow();
            RenderNavigationDistance();
        }
        

        
    }
    private void Start()
    {
        SetGameObjectsOthers();
        UpdateNavigationRendering();
    }
    void Update()
    {

        //Debug.Log(Vector3.Magnitude(new Vector3(0, 0, 1) + new Vector3(0, 0, -1)));

        CheckOptionInput();


        //Debug.Log("Coming up a " + target.operation);
        if (PassedTarget())
        {
            GetNextTarget();
            //initial_distance_to_target = 0;
            RenderNavigationArrow();
            UpdateNavigationRendering();
        }
        //if (NavigationError())
        //{
        //    navError = true;
        //    Debug.Log("NAV ERROR");
        //}

        //Renders the instructions on HUD
        RenderNavigationDistance();

        //Renders other road users within distance RenderDistanceOThers
        RenderOthers();
    }

    void SetGameObjectsOthers()
    {
        pedestrianList = GameObject.FindGameObjectsWithTag("Pedestrians");
        vehicleList = GameObject.FindGameObjectsWithTag("Vehicles");

        Debug.Log("Found " + pedestrianList.Length + " pedestrians");
        Debug.Log("Found " + vehicleList.Length + " vehicles");
    }
    void RenderOthers()
    {

        if (renderPedestrians)
        {
            foreach (GameObject obj in pedestrianList)
            {
                float distance = Vector3.Magnitude(obj.transform.position - this.transform.position);
                
                //Too far -->  skip
                if (distance >= renderDistanceOthers * 1.1){continue; }
                
                bool inFront = InFrontOfCar(obj.transform.position);


                string pedestrianName = obj.name.Split(' ')[0];
                Transform geo = obj.transform.Find("Geometry");
                Transform child = geo.Find(pedestrianName);

                if ( inFront && distance <= renderDistanceOthers)
                {
                    child.gameObject.SetActive(true);

                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            };
        }
        if (renderVehicles)
        {
            RenderObjects(vehicleList);
        }
    }

    public bool InFrontOfCar(Vector3 pos)
    {
        bool inFront;

        // plane equation is A(x-a) + B(y-b) + C(z-c) = 0
        // Where normal vector = <A,B,Z>
        // pos = (x,y,z,)
        // a point on the plane Q= (a,b,c)

        //We start from a little behind the car to make sure we dont see anything when heads are turned
        int metersBehindCar = 5;
        Vector3 planePosition = this.transform.position - this.transform.forward * metersBehindCar;
        float sign = Vector3.Dot(this.transform.forward, (pos - planePosition));
        if(sign >= 0) { inFront = true; }
        else { inFront = false; }

        return inFront;
    }
    void RenderObjects(GameObject[] objectList)
    {
        foreach (GameObject obj in objectList)
        {
            float distance = Vector3.Magnitude( obj.transform.position - this.transform.position);
            if (distance <= renderDistanceOthers) { obj.SetActive(true); }
            else { obj.SetActive(false); }
        }
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
        if (renderConformal1 != _renderConformal1 || renderConformal2 != _renderConformal2 || renderConformal3 != _renderConformal3)
        {
            renderConformal1 = _renderConformal1;
            renderConformal2 = _renderConformal2;
            renderConformal3 = _renderConformal3;

            navigation.GetComponent<SplineCreator>()._renderConformal1 = renderConformal1;
            navigation.GetComponent<SplineCreator>()._renderConformal2 = renderConformal2;
            navigation.GetComponent<SplineCreator>()._renderConformal3 = renderConformal3;
        }

        if(renderHUD != _renderHUD)
        {
            renderHUD = _renderHUD;
            HUD.SetActive(renderHUD);
        }
    }
    private void OnDrawGizmos()
    {
        CheckOptionInput();
        //if resolution change --> update transparancy
        if (transparency != _transparency)
        {
            transparency = _transparency;
            ChangTransparancyHUDAndConformal();
        }

        Color color = Color.yellow; color.a = .2f;
        Gizmos.color = color;
        Gizmos.DrawSphere(this.transform.position, renderDistanceOthers);
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
        color = navigation.GetComponent<MeshRenderer>().sharedMaterial.color; color.a = transparency;
        navigation.GetComponent<MeshRenderer>().sharedMaterial.color = color;
    }


    void UpdateNavigationRendering()
    {
        //Enables rendering of the waypoint and associated targets
        bool rendering = false;
        int renderCount = 0;
        int startRenderID = target.orderId - 2 ;
        if (startRenderID < 0) { startRenderID = 0; }

        //get ordered waypoints in nvaigation tree
        List<Waypoint> orderedWaypointOrderList = GetOrderedWaypointList();

        //Render from (target-2) to (target+render distance + 1) 
        //The mesh should start from waypoint (target-1) and go further than the actually wanted distance due to the spline creator script which uses all the points except
        //The beginning and endpoint to make the spline
        //we do not count waypoints which are simply spline points only real waypoints 
        foreach(Waypoint waypoint in orderedWaypointOrderList)
        {
            //start rendering
            if(waypoint.orderId == startRenderID)
            {
                rendering = true;
            }
            if(renderCount > startRenderID + renderDistanceNavigation) { rendering = false; }

            if (rendering)
            {
                //Enables Spline creator to use this waypoint for the navigation spline
                waypoint.renderMe = true;
                //render the targets
                waypoint.SetMeshRendererTargets(true);
                //only count if not a spline point
                if (!waypoint.shapePoint) { renderCount++; }
                
            }
            else
            {
                waypoint.renderMe = false;
                waypoint.SetMeshRendererTargets(false);

            }
        }
        //Update the mesh and control points
        navigation.GetComponent<SplineCreator>().UpdateControlPoints();
        navigation.GetComponent<SplineCreator>().UpdateMesh();
    }


    private List<Waypoint> GetOrderedWaypointList()
    {
        List<Waypoint> waypointOrderList = new List<Waypoint>();
        foreach (Transform child in navigation.transform)
        {
            Waypoint waypoint = child.gameObject.GetComponent<Waypoint>();
            waypointOrderList.Add(waypoint);

        }
        List<Waypoint> orderedWaypointOrderList = waypointOrderList.OrderBy(d => d.orderId).ToList();
        return orderedWaypointOrderList;
    }

    void RenderNavigationArrow()
    {
        Transform arrows = HUD.transform.Find("Arrows");
        if (target.operation == Operation.TurnRight)
        {
            //Debug.Log("Should render right");
            arrows.gameObject.GetComponent<MeshRenderer>().material = right;
        }
        else if (target.operation == Operation.TurnLeft)
        {
            //Debug.Log("Should render left");
            arrows.gameObject.GetComponent<MeshRenderer>().material = left;
        }
        else if (target.operation == Operation.Straight)
        {
            //Debug.Log("Should render straight");
            arrows.gameObject.GetComponent<MeshRenderer>().material = straight;
        }
        else if (target.operation == Operation.EndPoint)
        {
            //Debug.Log("Should render destination");
            arrows.gameObject.GetComponent<MeshRenderer>().material = destination;
        }
    }

    void RenderNavigationDistance()
    {

        Transform text = HUD.transform.Find("Text");

        TextMesh text_mesh = text.gameObject.GetComponent<TextMesh>();

        int rendered_distance = ((int)distance_to_target - ((int)distance_to_target % 5));

        if (rendered_distance < 0)
        {
            rendered_distance = 0;
        }
        if (target.operation == Operation.TurnRight)
        {
            text_mesh.text = rendered_distance + "m";
        }
        else if (target.operation == Operation.TurnLeft)
        {
            text_mesh.text = rendered_distance + "m";
        }
        else if (target.operation == Operation.Straight)
        {
            text_mesh.text = rendered_distance + "m";
        }
        else if (target.operation == Operation.EndPoint)
        {
            text_mesh.text = rendered_distance + "m";
        }
        
    }
    private bool PassedTarget()
    {
        bool passed_target = false;
        Vector3 distance_vector = target.transform.position - transform.position;
        //remove y component
        distance_vector.y = 0;
        distance_to_target = distance_vector.magnitude;
        if (distance_to_target < marginWaypointReached)
        {
            reachedTarget = true;
            //Debug.Log("At current target d = " + distance_to_target + " < " + marginWaypointReached);
        }

        if (reachedTarget && distance_to_target > marginWaypointReached/2)
        {
            passed_target = true;
            reachedTarget = false;

        }

        return passed_target;
    }

    private void GetNextTarget()
    {
        if(target.nextWaypoint != null)
        {

            target = target.nextWaypoint;
            while (target.shapePoint)
            {
                target = target.nextWaypoint;
            }
            
        }

    }



    //public void SetNewNavigationSystem(Waypoint waypoint)
    //{
    //    target = waypoint;
    //    initial_distance_to_target = 0;
    //    reachedTarget = false;
    //    navError = false;
    //    RenderNavigationArrow();
    //}


    //private bool NavigationError()
    //{
    //    bool navError = false;
         
    //    if(initial_distance_to_target == 0)
    //    {
    //        initial_distance_to_target = Vector3.Magnitude(target.transform.position - transform.position);
    //    }
    //    float current_distance_to_target = Vector3.Magnitude(target.transform.position - transform.position);

    //    //Debug.Log("Current Distance: " + current_distance_to_target + ", initial distance: " + initial_distance_to_target + " Nav error when " + current_distance_to_target + " > "+(initial_distance_to_target + nav_error_margin));
    //    if(current_distance_to_target > initial_distance_to_target + nav_error_margin)
    //    {
    //        navError = true;
    //    }
    //    return navError;
    //}

}
