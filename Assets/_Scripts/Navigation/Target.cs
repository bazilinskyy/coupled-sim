using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class Target : MonoBehaviour
{
    //Targets for visual search task
    //Targets should always be placed between the parent waypoint and the next waypoint!
    public WaypointStruct waypoint;

   
    public TargetDifficulty difficulty = TargetDifficulty.easy;

    public Material easy;
    public Material medium;
    public Material hard;


    public int ID;
    public bool detected = false;
    [HideInInspector]
    public float reactionTime = 0;
    [HideInInspector]
    public float totalFixationTime = 0f;
    [HideInInspector]
    public float firstFixationTime = 0f;
    public float lastFixationTime = 0f;
    [HideInInspector]
    public float detectionTime = 0f;
    public float detectionDistance = 0f;
    //Time at which this target was visible
    public float defaultVisibilityTime = -1f;
    public float startTimeVisible = -1f; // == defaultVisibilityTime
    public bool afterTurn = false;
    public Side side;
    public bool difficultPosition;
    public bool hasBeenVisible = false; //visible is set when we first see the target (gets unset when we pass it with the car, or when we detect it)
    public string GetID()
    {
        return ID.ToString();
        /*if (waypoint != null) { return waypoint.name.Last() + "-" + gameObject.name.Last(); }
        else { return gameObject.name.Last().ToString(); }*/
    }
    // Start is called before the first frame update
    void Awake()
    {
        ResetTarget();
    }
 
    private void OnDrawGizmos()
    {
       /* if (transform.hasChanged)
        {
            if (PositionedJustAfterTurn()) { afterTurn = true; }
            else { afterTurn = false; }
            side = GetRoadSide();

            if(afterTurn && ((side == Side.Right && waypoint.operation.IsRightTurn()) || (side == Side.Left && waypoint.operation.IsLeftTurn()))){ difficultPosition = true; }
            else { difficultPosition = false; }

            try
            {
                NavigationHelper navHelper = waypoint.transform.parent.GetComponent<NavigationHelper>();
                if (navHelper != null){ navHelper.CaluclateTargetInfo(); }
            }
            catch { }

        }*/
    }

    private void Update()
    {
        if (detected) { GetComponent<MeshRenderer>().enabled = false; }
        else { GetComponent<MeshRenderer>().enabled = true; }
    }

    public bool HasBeenLookedAt()
    {
        if(totalFixationTime > 0f) { return true; }
        else { return false; }
    }
    public void OnHit()
    {
        if (detected) { return; }
        
        //Record time of first fixation
        if(firstFixationTime == 0f) { firstFixationTime = Time.time; }

        totalFixationTime += Time.deltaTime; 
        lastFixationTime = Time.time;
    }
    public void SetDifficulty( TargetDifficulty _difficulty)
    {
        //Default
        Material material = easy;
        //Adjust the setDiofficulty attribute
        difficulty = _difficulty;

        //Get appropriate material
        if (difficulty == TargetDifficulty.easy) { material = easy; }
        else if (difficulty == TargetDifficulty.medium) { material = medium; }
        else if(difficulty == TargetDifficulty.hard) { material = hard; }


        GetComponent<MeshRenderer>().sharedMaterial = material;
    }
    public TargetDifficulty GetTargetDifficulty(){ return difficulty; }

    public void SetDetected(float _detectionTime)
    {

        detected = true;  hasBeenVisible = false;
        reactionTime = _detectionTime - startTimeVisible;
        detectionTime = _detectionTime;
        detectionDistance = Vector3.Magnitude(transform.position - MyUtils.GetPlayer().transform.position);
        GetComponent<MeshRenderer>().enabled = false;
        GetComponent<SphereCollider>().enabled = false;

        Debug.Log($"Target {GetID()} is detected: startTimeVisible {startTimeVisible}, detectionTime { detectionTime}, reactiontime: {reactionTime}");
    }
    public bool HasBeenVisible() { return hasBeenVisible; }

    public void SetVisible(bool input, float time) { 
        hasBeenVisible = input;
        startTimeVisible = time;
    }
    public void ResetTarget()
    {
        GetComponent<MeshRenderer>().enabled = true;
        GetComponent<SphereCollider>().enabled = true;
        detected = false;    hasBeenVisible = false;
        startTimeVisible = defaultVisibilityTime;
        reactionTime = 0f; totalFixationTime = 0f; firstFixationTime = 0f;
        detectionTime = 0f; lastFixationTime = 0f;
    }

    public bool InFoV(Transform cam, float FoV )
    {
        //Checks whether in FoV
        //Angle between forward vector and vector to target should be less then FoV/2
        Vector3 toTarget = transform.position - cam.position;
        float angleCamTarget = Vector3.Angle(cam.forward, toTarget);
        
        if(angleCamTarget > FoV / 2 || angleCamTarget < -FoV/2) {  return false; }
        else {  return true; }
    }
    public bool IsDetected(){ return detected; }
    public Side GetRoadSide()
    {
        return Side.Undetermined;
        /* //Targets should always be placed between the parent waypoint and the next waypoint!
         if (waypoint == null) { return Side.Undetermined; }
         if (waypoint.nextWaypoint == null) { 
             Debug.LogError($"Can not determine side of target {GetID()} as there is no next waypoint. (Targes should always be placed IN FRONT of the parent waypoint i.e., in between the parent and next waypoint");
             return Side.Undetermined; }

         Vector3 firstWaypointPos = waypoint.transform.position;

         //Adjust waypoint position
         //Waypoints are positioned just before the crossing (~8 meters)
         //If this is a turn we are need to be in the middle of the road which we turn on i.e., a little bit more forward
         if (waypoint.operation.IsTurn()) { firstWaypointPos += waypoint.transform.forward * 7.5f; }
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
        else { return Side.Right; }*/
    }

    public bool PositionedJustAfterTurn()
    {
        return true;
      /*  if (waypoint == null) { return false; }
        if (!waypoint.operation.IsTurn()) { return false; }
        //We define the target being just after the waypoint if within 20 meters
        float boxSize = 20f; float streetWidth = 10f;
      
        Vector3 center = waypoint.transform.position + waypoint.transform.forward * 7.5f + Vector3.up;
        Vector3 direction;
        if (waypoint.operation == Operation.TurnRight) { direction = waypoint.transform.right; }
        else { direction = -waypoint.transform.right; }

        Vector3 point1 = center - waypoint.transform.forward * streetWidth;
        Vector3 point2 = center + waypoint.transform.forward * streetWidth;

        Vector3 point3 = point2 + direction * boxSize;
        Vector3 point4 = point1 + direction * boxSize;

        //If target is within the box enclosed by these points it is defined as being just after the corner
        //Since the grid of roads is always in line with the x and y axis we can simply use minimum and maximum values 
        //We only care about x,z coordinates 
        float minX = Mathf.Min(point1.x, point2.x, point3.x, point4.x);
        float maxX = Mathf.Max(point1.x, point2.x, point3.x, point4.x);
        float minZ = Mathf.Min(point1.z, point2.z, point3.z, point4.z);
        float maxZ = Mathf.Max(point1.z, point2.z, point3.z, point4.z);

        if(transform.position.x > minX && transform.position.x < maxX && transform.position.z > minZ && transform.position.z < maxZ) { return true; }
        else { return false; }*/
    }
}

public enum Side
{
    Right,
    Left,
    Undetermined,
}
