using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class CrossComponents : MonoBehaviour
{
	public RoadParameters roadParameters;
	private int pointsPerCorner = 30;

    public GameObject forwardCross;
    public GameObject backwardCross;
    public GameObject leftCross;
    public GameObject rightCross;

	public GameObject startPoint;

    public GameObject triggerStart;
	public GameObject triggerEnd;

	public GameObject rightTrigger;
	public GameObject rightRightTrigger;
	public GameObject rightLeftTrigger;
	public GameObject rightEndTrigger;

	public GameObject leftTrigger;
	public GameObject leftRightTrigger;
	public GameObject leftLeftTrigger;
	public GameObject leftEndTrigger;

	public GameObject straightTrigger;
	public GameObject straightRightTrigger;
	public GameObject straightLeftTrigger;
	public GameObject straightEndTrigger;

	public Transform FirstTurnTargetSpawnPoints;
	public Transform RightTargetSpawnPoints;
	public Transform LeftTargetSpawnPoints;
	public Transform StraightTargetSpawnPoints;

	public Transform TargetParent;
	public GameObject TargetPrefab;

	public bool isFirstCrossing;

	public Waypoints waypoints = new Waypoints();
	public List<Target> targetList = new List<Target>(); 

    public TurnType turn1 = TurnType.None;

    public TurnType turn2 = TurnType.None;
	private newExperimentManager experimentManager;
    private void Awake()
    {
		experimentManager = MyUtils.GetExperimentManager();
    }
    public void SetUpCrossing(TurnType _turn1, TurnType _turn2, MainExperimentSetting settings, bool _isFirstCrossing = false)
    {
        turn1 = _turn1; turn2 = _turn2; isFirstCrossing = _isFirstCrossing;

		SetTriggers();
        SetWaypointTurn2();
		
		//?Remove old targets
		foreach( Transform child in TargetParent) { Destroy(child.gameObject); }
		targetList = new List<Target>();

		SpawnTargets(settings);
	}
	void SpawnTargets(MainExperimentSetting settings) 
	{
		if(settings.targetsPerTurn == 0) { return; }
		List<Transform> spawnPoints;
		Transform parentSpawPoints = null;
		WaypointStruct waypoint = new WaypointStruct();

		if (!isFirstCrossing) 
		{
			parentSpawPoints = FirstTurnTargetSpawnPoints;
			spawnPoints = SelectRandomSpawnPoints(parentSpawPoints, settings.targetsPerTurn);
			
			waypoint.turn = turn1; 
			waypoint.waypoint = waypoints.waypoint1;

			InstantiateTargets(spawnPoints, waypoint, settings);
		}

		if(turn2 != TurnType.EndPoint || turn2 == TurnType.None)
        {
			if (turn2 == TurnType.Right) { parentSpawPoints = RightTargetSpawnPoints; }
			if (turn2 == TurnType.Left) { parentSpawPoints = LeftTargetSpawnPoints; }
			if (turn2 == TurnType.Straight) { parentSpawPoints = StraightTargetSpawnPoints; }

			spawnPoints = SelectRandomSpawnPoints(parentSpawPoints, settings.targetsPerTurn);

			waypoint.turn = turn2; 
			waypoint.waypoint = waypoints.waypoint2;

			InstantiateTargets(spawnPoints, waypoint, settings);
		}
	}

	private void InstantiateTargets(List<Transform> spawnPoints, WaypointStruct waypoint, MainExperimentSetting settings)
    {
		GameObject target;
		foreach (Transform point in spawnPoints)
		{
			
			target = Instantiate(TargetPrefab);
			target.transform.position = point.position;
			target.transform.parent = TargetParent;
			target.transform.name = "Target " + experimentManager.GetNextTargetID().ToString();
			target.GetComponent<Target>().SetDifficulty(settings.targetDifficulty);
			target.GetComponent<Target>().waypoint = waypoint;
			target.GetComponent<Target>().ID = experimentManager.GetNextTargetID();
			target.GetComponent<Target>().side = point.name == "Left" ? Side.Left : Side.Right;
			targetList.Add(target.GetComponent<Target>());
		}
	}
	private List<Transform> SelectRandomSpawnPoints(Transform parent, int numberOfTargetSpawnPoints)
    {

		List<Transform> spawnPoints = new List<Transform>();
		List<Transform> selectedSpawnPoints = new List<Transform>();
		bool selectionFinished = false;
		
		foreach (Transform child in parent) { spawnPoints.Add(child); }

		while (!selectionFinished)
		{
			int randomIndex = (int)Mathf.Round(Random.Range(0, spawnPoints.Count() - 1));
            if (!selectedSpawnPoints.Contains(spawnPoints[randomIndex])){ selectedSpawnPoints.Add(spawnPoints[randomIndex]); }
			
			if(selectedSpawnPoints.Count() >= numberOfTargetSpawnPoints) { selectionFinished = true; }
			if(selectedSpawnPoints.Count() == spawnPoints.Count()) { selectionFinished = true; }
		}

		return selectedSpawnPoints;
	}
    public void SetTriggers()
    {
		rightTrigger.tag = turn1 == TurnType.Right ? "CorrectTurn" : "WrongTurn";
		leftTrigger.tag = turn1 == TurnType.Left ? "CorrectTurn" : "WrongTurn";
		straightTrigger.tag = turn1 == TurnType.Straight ? "CorrectTurn" : "WrongTurn";

		if (turn1 == TurnType.Left) { 
			leftRightTrigger.tag = turn2 == TurnType.Right ? "CorrectTurn" : "WrongTurn";
			leftLeftTrigger.tag = turn2 == TurnType.Left ? "CorrectTurn" : "WrongTurn";

			if (turn2 == TurnType.EndPoint) { leftEndTrigger.SetActive(true); }
		}
        else if (turn1 == TurnType.Right) {

			rightRightTrigger.tag = turn2 == TurnType.Right ? "CorrectTurn" : "WrongTurn";
			rightLeftTrigger.tag = turn2 == TurnType.Left ? "CorrectTurn" : "WrongTurn";

			if (turn2 == TurnType.EndPoint) { rightEndTrigger.SetActive(true); }
		}
        else if (turn1 == TurnType.Straight) {
			
			straightRightTrigger.tag = turn2 == TurnType.Right ? "CorrectTurn" : "WrongTurn";
			straightLeftTrigger.tag = turn2 == TurnType.Left ? "CorrectTurn" : "WrongTurn";
			
			if (turn2 == TurnType.EndPoint) { straightEndTrigger.SetActive(true); }
		}
		else if (turn1 == TurnType.EndPoint)
        {
			triggerEnd.SetActive(true);
        }
    }
    void SetWaypointTurn2()
    {
        if (turn1 == TurnType.None){ return; } 
        if (turn1 == TurnType.EndPoint){ return; }
        
        if (turn1 == TurnType.Left) { waypoints.waypoint2.SetPositionAndRotation(waypoints.left.position, waypoints.left.rotation); }
        if (turn1 == TurnType.Right) { waypoints.waypoint2.SetPositionAndRotation(waypoints.right.position, waypoints.right.rotation); }
        if (turn1 == TurnType.Straight) { waypoints.waypoint2.SetPositionAndRotation(waypoints.straight.position, waypoints.straight.rotation); }
    }
    public List<Vector3> GetPointsNavigationLine()
    {
        List<Vector3> points = new List<Vector3>();

        points.Add(waypoints.startPoint.position);

        if (turn1 == TurnType.None) { Debug.Log("Got None turn type..."); return null; }
        if (turn1 == TurnType.EndPoint) { points.Add(waypoints.waypoint1.position - 30f * waypoints.waypoint1.forward); return points; }
        else if (turn1 != TurnType.Straight){ foreach (Vector3 point in GetPointsCorner(waypoints.waypoint1, turn1)) { points.Add(point); }}

        if (turn2 == TurnType.None) { Debug.Log("Got None turn type..."); return null; }
        if (turn2 == TurnType.EndPoint) { points.Add(waypoints.waypoint2.position - 30f * waypoints.waypoint2.forward); return points; }
        else if (turn2 != TurnType.Straight){ foreach (Vector3 point in GetPointsCorner(waypoints.waypoint2, turn2)) { points.Add(point); }}

		return points;
    }
	private Vector3[] GetPointsCorner(Transform waypoint, TurnType turn)
	{
		//get radius based on waypoint oepration and raodParameter scriptable variable
		float radius;
		/*if (waypoint.operation == Operation.TurnRightShort) { radius = roadParameters.radiusShort; }
		else { radius = roadParameters.radiusLong; }
*/
		radius = roadParameters.radiusLong;
		//Get half circle in x,y frame
		Vector2[] quarterCircle = GetQuarterCircle(radius, pointsPerCorner, turn);

		//Transpose circle to the current waypoint position and rotation
		Vector3[] points = TransformVector2ToVector3(quarterCircle);
		points = TransposePoints(waypoint, points);

		return points;

	}
	private Vector3[] TransposePoints(Transform waypoint, Vector3[] circle)
	{
		//Transposes points to be in a frame (forward,perpendicular2forward, worldY)  
		//Input circle is vector3[] in the x,y,z frame

		//z = worldy
		//y = forward
		//x = perp2forward
		Vector3 worldY = new Vector3(0, 1f, 0);
		Vector3 forward = waypoint.transform.forward;
		Vector3 perp2forward = -Vector3.Cross(forward, worldY); //Has two possibilities, has to be the minus one

		Vector3 waypointPosition = waypoint.position;


		//Make homogeneous transformation matrix
		Vector4 x = new Vector4(perp2forward.x, perp2forward.y, perp2forward.z, 0);
		Vector4 y = new Vector4(forward.x, forward.y, forward.z, 0);
		Vector4 z = new Vector4(worldY.x, worldY.y, worldY.z, 0);
		Vector4 t = new Vector4(waypointPosition.x, waypointPosition.y, waypointPosition.z, 1);

		Matrix4x4 rotationMatrix = new Matrix4x4(x, y, z, t);

		//Transpose points
		for (int v = 0; v < circle.Length; v++)
		{
			circle[v] = rotationMatrix.MultiplyPoint3x4(circle[v]);
		}

		return circle;
	}
	private Vector3[] TransformVector2ToVector3(Vector2[] points, float z = 0)
	{
		//Transform array of vector2 to vector3 with z
		Vector3[] newPoints = new Vector3[points.Length];
		for (int i = 0; i < points.Length; i++)
		{
			newPoints[i] = new Vector3(points[i].x, points[i].y, z);
		}
		return newPoints;
	}
	private Vector2[] GetQuarterCircle(float radius, int numberOfPoints, TurnType turn)
	{
		//returns quarter circle in the x,y frame 
		//operation -> right --> (0,0) to (radius,radius)
		//operation -> left --> (0,0) to (-radius, radius)
		Vector2[] pointsCircle = new Vector2[numberOfPoints];

		float radStep = Mathf.PI / 2 / (numberOfPoints - 1);

		int sign = 1;
		if (turn == TurnType.Left) { sign = -1; }


		//- PI to 0 gets a half circle to the right
		for (int i = 0; i < numberOfPoints; i++)
		{
			float radians = -Mathf.PI / 2 + i * radStep;
			float x = sign * (radius * Mathf.Sin(radians) + radius);
			float y = radius * Mathf.Cos(radians);

			pointsCircle[i] = new Vector2(x, y);
		}
		return pointsCircle;
	}

}

[System.Serializable]
public struct Waypoints
{
    public Transform startPoint;
    public Transform centre;
    public Transform straight;
    public Transform right;
    public Transform left;

    public Transform waypoint1;
    public Transform waypoint2;
}


