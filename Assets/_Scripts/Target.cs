using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Target : MonoBehaviour
{
    //Targets for visual search task
    //Targets should always be placed between the parent waypoint and the next waypoint!
    public Waypoint waypoint;
    public TargetDifficulty difficulty = TargetDifficulty.easy_6;

    public Material easy_6;
    public Material easy_5;
    public Material medium_4;
    public Material medium_3;
    public Material hard_2;
    public Material hard_1;


    public int ID;
    public bool detected = false;
    [HideInInspector]
    public float reactionTime = 0;

    //Time at which this target was visible
    public float defaultVisibilityTime = -1f;
    public float startTimeVisible = -1f;


    // Start is called before the first frame update
    void Start()
    {
        SetUnDetected();
        startTimeVisible = -1f;
    }
    public void SetDifficulty( TargetDifficulty _difficulty)
    {
        //Default
        Material material = easy_6;
        //Adjust the setDiofficulty attribute
        difficulty = _difficulty;

        //Get appropriate material
        if (difficulty == TargetDifficulty.easy_6) { material = easy_6; }
        else if (difficulty == TargetDifficulty.easy_5) { material = easy_5; }
        else if(difficulty == TargetDifficulty.medium_4) { material = medium_4; }
        else if (difficulty == TargetDifficulty.medium_3) { material = medium_3; }
        else if(difficulty == TargetDifficulty.hard_2) { material = hard_2; }
        else if (difficulty == TargetDifficulty.hard_1) { material = hard_1; }

        GetComponent<MeshRenderer>().sharedMaterial = material;
    }
    public TargetDifficulty GetTargetDifficulty()
    {
        return difficulty;
    }

    public void SetDetected(float detectionTime)
    {
        detected = true;
        reactionTime = detectionTime - startTimeVisible;
        transform.GetComponent<MeshRenderer>().enabled = false;
    }

    public void SetUnDetected()
    {
        detected = false;
    }

    public bool IsDetected()
    {
        return detected;
    }

    public Side GetRoadSide()
    {
        //Targets should always be placed between the parent waypoint and the next waypoint!

        if(waypoint.nextWaypoint == null) { 
            Debug.Log("Can not determine side of the target as there is no next waypoint. (Targes should always be placed IN FRONT of the parent waypoint i.e., in between the parent and next waypoint");
            return Side.Undetermined; }

        Vector3 firstWaypointPos = waypoint.transform.position;
        //Adjust waypoint position
        //Waypoints are positioned just before the crossing (Left turn: ~8 meters, right turn: ~5meters). 
        //If this is a turn we are need to be in the middle of the road which we turn on i.e., a little bit more forward
        if (waypoint.operation.IsLeftTurn()) { firstWaypointPos += waypoint.transform.forward * 8; }
        if (waypoint.operation.IsRightTurn()) { firstWaypointPos += waypoint.transform.forward * 5; }
        Vector3 secondWaypointPos = waypoint.nextWaypoint.transform.position; 

        Vector3 splittingLine = firstWaypointPos - secondWaypointPos;
        Vector3 normal = Vector3.Cross(splittingLine, Vector3.up);

        //Side from spanned plane in the middle of the road        
        // plane equation is A(x-a) + B(y-b) + C(z-c) = 0 = dot(Normal, planePoint - targetPoint)
        // Where normal vector = <A,B,Z>
        // pos = the target position (x,y,z,)
        // a point on the plane Q= (a,b,c) i.e., waypoint

        float sign = Vector3.Dot(normal, (firstWaypointPos - transform.position));
        if (sign >= 0) {  return Side.Left; }
        else { return Side.Right; }

    }
}

public enum Side
{
    Right,
    Left,
    Undetermined,
}
