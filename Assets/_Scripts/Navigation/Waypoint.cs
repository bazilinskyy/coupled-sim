using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
[ExecuteInEditMode]
public class Waypoint : MonoBehaviour
{
    public Waypoint previousWaypoint;
    public Waypoint nextWaypoint;

    public bool renderMe = true; //Enables rendering of this waypoiunt AND ITS TARGETS

    public Operation operation;

    public int orderId;

    public List<NavigationPart> navigationPartList;
    public int TargetCount()
    {
        int count = 0;
        foreach (Transform child in transform)
        {
            Target target = child.gameObject.GetComponent<Target>();
            if (target != null)
            {
                count++;
            }
        }
        return count;
    }
    public List<Target> GetTargets()
    {
        List<Target> targetList = new List<Target>();
        foreach (Transform child in transform)
        {
            Target target = child.gameObject.GetComponent<Target>();
            if (target != null)
            {
                targetList.Add(target);
            }
        }
        return targetList;
    }
    private void SetMeshRendererNavigationParts(bool renderMe)
    {
        if (navigationPartList == null) { return; }

        foreach (NavigationPart navigationPart in navigationPartList)
        {
            navigationPart.RenderMe(renderMe);
        }
    }
    private void SetMeshRendererTargets(bool enabled)
    {
        foreach (Target target in GetTargets())
        {
            if (!target.IsDetected()) { target.GetComponent<MeshRenderer>().enabled = enabled; }
        }
    }
    public void RenderMe(bool _renderMe)
    {
        renderMe = _renderMe;
        SetMeshRendererTargets(_renderMe);
        SetMeshRendererNavigationParts(_renderMe);
    }
    public void SetTargetIDsAndNames()
    {
        int idCount = 0;
        foreach (Target target in GetTargets())
        {
            target.ID = idCount;
            target.name = "Target " + idCount;
            idCount++;
        }
    }
    public void RemoveNavigationParts()
    {
        navigationPartList = new List<NavigationPart>();
    }
    public void AddNavigationPart(NavigationPart navigationPart)
    {
        //If not list -> make it
        if (navigationPartList == null) { navigationPartList = new List<NavigationPart>(); }
        //If not already in the list add it.
        if (!navigationPartList.Contains(navigationPart)) { navigationPartList.Add(navigationPart); }

    }
}
