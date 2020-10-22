using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class NavigationPart : MonoBehaviour
{
    public bool renderMe;
    public Waypoint waypoint { get; set; }
    public NavigationType navigationType { get; set; }
    private Vector3 startPosition { get; set; } 

    public NavigationPart(Waypoint _waypoint, NavigationType _navigationType) 
    {
        waypoint = _waypoint;
        navigationType = _navigationType;

        startPosition = waypoint.transform.position;
        
    }


    public bool CorrectPosition()
    {
        bool correct;

        if (startPosition == waypoint.transform.position) { correct = true; }
        else { correct = false; }
        return correct;
    }
    
    public void RenderMe(bool _renderMe)
    {        
        renderMe = _renderMe;
        GetComponent<MeshRenderer>().enabled = _renderMe;
    }
}
