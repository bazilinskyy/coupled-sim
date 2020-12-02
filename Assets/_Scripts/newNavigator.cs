using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(CrossingSpawner))]
public class newNavigator : MonoBehaviour
{
    public HUDMaterials HUDMaterials;
    public GameObject HUD;
    public newExperimentManager experimentManager;

    CrossingSpawner crossingSpawner;
    

    public List<WaypointStruct> targetList;
    public WaypointStruct target;
    int targetIndex = 0;

    public bool navigationFinished = false;

    private bool isTriggered = false;
    private float triggerTime = 0f; private float timeOutTime = 2f;
    private bool renderZero = false;
    public bool atWaypoint = false;
    private void Start()
    {
        crossingSpawner = GetComponent<CrossingSpawner>();

        if (!experimentManager.renderHUD) { HUD.SetActive(false); }

        //Add first four targets to list
        targetList = new List<WaypointStruct>();

        WaypointStruct[] targetsCurrent = crossingSpawner.crossings.GetWaypoints("Current");
        foreach(WaypointStruct target in targetsCurrent) { targetList.Add(target); }

        targetsCurrent = crossingSpawner.crossings.GetWaypoints("Next");
        foreach (WaypointStruct target in targetsCurrent) { targetList.Add(target); }

        target = targetList[targetIndex];
        
        RenderNavigationArrow();
    }

    private void Update()
    {
        RenderNavigationDistance();

        ResetTriggerBoolean();
    }
    
    void ResetTriggerBoolean()
    {
        if (!isTriggered) { return; }
        if ((triggerTime + timeOutTime) < Time.time) { isTriggered = false; }
    }
    void RenderNavigationDistance()
    {
        if (!experimentManager.renderHUD) { return; }

        if (target.waypoint == null) { return; }

        Transform text = HUD.transform.Find("Text");
        TextMesh textMesh = text.gameObject.GetComponent<TextMesh>();
        
        float distanceToTarget = Vector3.Magnitude(target.waypoint.transform.position- transform.position);
        int renderedDistance = ((int)distanceToTarget - ((int)distanceToTarget % 5));
        if (renderedDistance <= 0) { renderedDistance = 0; atWaypoint = true; }
        if (atWaypoint) { renderedDistance = 0; }
        textMesh.text = $"{renderedDistance}m";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) { return; }
        if (other.gameObject.CompareTag("EnterCrossing"))
        {
            isTriggered = true; triggerTime = Time.time;
            StartCoroutine(AddNextTargets());
        }
        else if (other.gameObject.CompareTag("CorrectTurn"))
        {
            targetIndex++;
            isTriggered = true; triggerTime = Time.time;
            atWaypoint = false;

            target = targetList[targetIndex];
            Debug.Log($"Next target = {target.turn}...");
            RenderNavigationArrow();
        }
        else if (other.gameObject.CompareTag("WrongTurn"))
        {
            isTriggered = true; triggerTime = Time.time;
            targetIndex++;  target = targetList[targetIndex];

            experimentManager.TookWrongTurn();
        }
        else if (other.gameObject.CompareTag("NavigationFinished"))
        {
            navigationFinished = true;
        }

    }

    IEnumerator AddNextTargets()
    {
        yield return new WaitForSeconds(1f);
        WaypointStruct[] newTargets = crossingSpawner.crossings.GetWaypoints("Next");
        foreach (WaypointStruct target in newTargets) { targetList.Add(target); }
    }
    public void RenderNavigationArrow()
    {  
        if (!experimentManager.renderHUD) { return; }
        
        Debug.Log("Rendering navigation arrow!");

        Transform arrows = HUD.transform.Find("Arrows");
        if (arrows == null) { Debug.Log("Arrows= null...."); return; }
        if (target.turn == TurnType.Right) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.right; arrows.GetComponent<MoveCollider>().RightArrow(); }
        else if (target.turn == TurnType.Left) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.left; arrows.GetComponent<MoveCollider>().LeftArrow(); }
        else if (target.turn == TurnType.Straight) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.straight; arrows.GetComponent<MoveCollider>().CenterArrow(); }
        else if (target.turn == TurnType.EndPoint) { arrows.GetComponent<MeshRenderer>().material = HUDMaterials.destination; arrows.GetComponent<MoveCollider>().CenterArrow(); }
    }

}


