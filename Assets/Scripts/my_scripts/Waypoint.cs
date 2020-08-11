﻿using System.Collections;
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

    private List<NavigationPart> navigationPartList;

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
    private void SetMeshRendererNavigationParts(bool renderMe)
    {
        if (navigationPartList == null) { return; }

        foreach (NavigationPart navigationPart in navigationPartList)
        {
            navigationPart.RenderMe(true);
        }
    }
    private void SetMeshRendererTargets(bool enabled)
    {
        foreach (GameObject target in GetTargets())
        {
            target.GetComponent<MeshRenderer>().enabled = enabled;
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
        foreach (GameObject target in GetTargets())
        {
            target.GetComponent<Target>().ID = idCount;
            target.name = "Target " + idCount;
            idCount++;
        }
    }
    public void RemoveNavigationParts()
    {
        if (navigationPartList != null) { navigationPartList.Clear(); }
    }
    public void AddNavigationPart(NavigationPart navigationPart)
    {
        //If not list -> make it
        if (navigationPartList == null) { navigationPartList = new List<NavigationPart>(); }
        //If not already in the list add it.
        if (!navigationPartList.Contains(navigationPart)) { navigationPartList.Add(navigationPart); }

    }
}
