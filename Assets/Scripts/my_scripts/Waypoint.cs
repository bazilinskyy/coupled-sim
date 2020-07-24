using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
[ExecuteInEditMode]
public class Waypoint : MonoBehaviour
{
    public Waypoint previousWaypoint;
    public Waypoint nextWaypoint;

    public bool shapePoint =false;
    public bool renderMe = true; //Enables rendering of this waypoiunt AND ITS TARGETS

    public Operation operation;
    public bool extraSplinePoint = true;
    public float firstPointDistance = 15f;
    public float secondPointDistance = 15f;

    public int orderId;
    
    public int TargetCount()
    {
        int count = 0;
        foreach(Transform child in transform)
        {
            Target target = child.gameObject.GetComponent<Target>();
            if(target != null)
            {
                count++;
            }
        }
        return count;
    }


    public List<GameObject> GetTargets()
    {
        List<GameObject> targetList = new List<GameObject>();
        foreach (Transform child in transform)
        {
            Target target = child.gameObject.GetComponent<Target>();
            if (target != null)
            {
                targetList.Add(target.gameObject);
            }
        }
        List<GameObject> orderedTargetList = targetList.OrderBy(d => d.name).ToList();
        return orderedTargetList;
    }

    public void SetMeshRendererTargets( bool enabled)
    {
        foreach(GameObject target in GetTargets())
        {
            target.GetComponent<MeshRenderer>().enabled = enabled;
        }
    }

    public void SetTargetIDsAndNames()
    {
        int idCount = 0;
        foreach (GameObject target in GetTargets())
        {
            target.GetComponent<Target>().ID = idCount;
            target.name = "Target " + idCount;
            idCount++;
        }
    }
}
