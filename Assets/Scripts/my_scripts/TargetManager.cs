using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class TargetManager : MonoBehaviour
{

    public GameObject car;
    public GameObject cam;
    public string pressKeyCode;
    private Navigator navigator;
    public GameObject waypoints;
    public int maxRandomRayHits = 40;
    public XMLManager dataManager;

    // Start is called before the first frame update
    void Awake()
    {
        navigator = car.GetComponent<Navigator>();
        HideAllTargets();
    }

    // Update is called once per frame
    void Update()
    {
 
        if (Input.GetKeyDown(pressKeyCode))
        {
            ProcessUserInput();
            print("You pressed " + pressKeyCode);
        }
    }

    void ProcessUserInput()
    {
        //if there is a target visible which has not already been detected
        List<GameObject>  targetList = GetActiveTargets();
        GameObject seenTarget = null;
        int targetCount = 0;

        //Check if there are any visible targets
        foreach(GameObject target in targetList)
        {
            Debug.Log("Target: " + target.name);
            if (VisibleTarget(target))
            {
                seenTarget = target;
                targetCount++;
            }
        }
        //We do not accept multiple visible targets at the same time.
        if (targetCount == 0 )
        {
            dataManager.AddFalseAlarm();
        }
        else if(targetCount == 1)
        {
            dataManager.AddTrueAlarm(seenTarget);
            seenTarget.GetComponent<Target>().SetDetected();
            
        }
        else
        {
            throw new System.Exception("Counting two visible targets.... This is not implemented yet");
        }
    }

   


    Vector3 GetRandomPerpendicularVector(Vector3 vec) {
        
        vec = Vector3.Normalize(vec);
        
        float v1 = Random.Range(-1f, 1f);
        float v2 = Random.Range(-1f, 1f);

        float x;float y;float z;
      
        int caseSwitch = Random.Range(0, 3); //outputs 0,1 or, 2
        

        if (caseSwitch == 0)
        {
            // v1 = x, v2 = y, v3 = z
            x = v1; y = v2;
            z = -(x * vec.x + y * vec.y) / vec.z;
        }
        else if (caseSwitch == 1)
        {
            // v1 = y, v2 = z, v3 = x
            y = v1; z = v2;
            x = -(y * vec.y + z * vec.z) / vec.x;
        }
        else if (caseSwitch == 2)
        {
            // v1 = z, v2 = x, v3 = y
            z = v1; x = v2;
            y = -(z * vec.z + x * vec.x) / vec.y;
        }
        else
        {
            throw new System.Exception("Something went wrong in TargetManager -> GetRandomPerpendicularVector() ");
        }

        float mag = Mathf.Sqrt(x * x + y * y + z * z);
        Vector3 normal = new Vector3(x / mag, y / mag, z / mag);
        return normal;
    }

    bool VisibleTarget(GameObject target)
    {
        //We will cast rays to the outer edges of the sphere (the edges are determined based on how we are looking towards the sphere)
        //I.e., with the perpendicular vector to the looking direction of the sphere

        bool isVisible = false;
        Vector3 direction = target.transform.position - cam.transform.position;
        Vector3 currentDirection;
        RaycastHit hit;
        float targetRadius = target.GetComponent<SphereCollider>().radius;

        //If renderer.isVisible we also got to check if it is not occluided by any other objects
        if (target.GetComponent<Renderer>().isVisible)
        {
            //Vary the location of the raycast over the edge of the potentially visible target
            for ( int i =0; i < maxRandomRayHits; i++)
            {
                Vector3 randomPerpendicularDirection = GetRandomPerpendicularVector(direction);
                currentDirection = (target.transform.position + randomPerpendicularDirection * targetRadius)   - cam.transform.position;

                if (Physics.Raycast(cam.transform.position, currentDirection, out hit, 10000f, Physics.AllLayers))
                {
                    Debug.DrawRay(cam.transform.position, currentDirection, Color.green);
                    // print("HIT " + hit.collider.gameObject.name);
                    if (hit.collider.gameObject.tag == "Target")
                    {
                        print(target.name + " is visible");
                        isVisible = true;
                        break;
                    }
                    
                }
            }
        }
       
        return isVisible;
    }

    List<GameObject> GetActiveTargets()
    {
        List<GameObject> targetList = new List<GameObject>();
        foreach(Transform child in waypoints.transform)
        {
            Waypoint waypoint = child.GetComponent<Waypoint>();
            //Means it is being rendered as well as its targets
            if (waypoint != null && waypoint.renderMe)
            {
                foreach(GameObject target in waypoint.GetTargets())
                {
                    //If not detected yet --> Add to potential target list
                    if (!target.GetComponent<Target>().IsDetected())
                    {
                        targetList.Add(target);
                    }

                }
                
            }
        }

        return targetList;
    }


    public List<Waypoint> GetWaypointList()
    {
        List<Waypoint> waypointOrderList = new List<Waypoint>();
        foreach (Transform child in transform)
        {
            Waypoint waypoint = child.gameObject.GetComponent<Waypoint>();
            if (waypoint != null)
            {
                waypointOrderList.Add(waypoint);
            }

        }
        List<Waypoint> orderedWaypointOrderList = waypointOrderList.OrderBy(d => d.orderId).ToList();
        return orderedWaypointOrderList;
    }


    void HideAllTargets()
    {
        List<Waypoint> waypointList = GetWaypointList();

        foreach ( Waypoint waypoint in waypointList)
        {
            waypoint.SetMeshRendererTargets(false);
        }
    }

}
