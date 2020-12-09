using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class CrossComponents : MonoBehaviour
{
	public RoadParameters roadParameters;
	private int pointsPerCorner = 30;

	public Transform variableBlocks;
	public GameObject blockFirstCrossing;

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

	public WaypointsObjects waypointObjects = new WaypointsObjects();
	public List<Target> targetList = new List<Target>();

	public WaypointStruct[] waypoints;

	public TurnType turn1;
	public TurnType turn2;

	private newExperimentManager experimentManager;
	public bool isCurrentCrossing = true;
    private void Awake()
    {
		experimentManager = MyUtils.GetExperimentManager();
    }
	public void SetCurrentCrossing (bool _isCurrentCrossing)
    {
		isCurrentCrossing = _isCurrentCrossing;

		StartCoroutine(SetBuildingBlocks());
	}
	public void SetUpCrossing(WaypointStruct[] _waypoints, MainExperimentSetting settings, bool _isCurrentCrossing, bool _isFirstCrossing = false)
    {
		if(experimentManager == null) { experimentManager = MyUtils.GetExperimentManager(); }
		
		waypoints = _waypoints;
		turn1 = waypoints[0].turn; turn2 = waypoints[1].turn;

		isCurrentCrossing = _isCurrentCrossing; isFirstCrossing = _isFirstCrossing;

		//Removes/adds buildings for first crossing
		blockFirstCrossing.gameObject.SetActive(isFirstCrossing);

		SetTriggers();

        SetWaypoints();

		StartCoroutine(SetBuildingBlocks());

		//?Remove old targets
		foreach ( Transform child in TargetParent) { Destroy(child.gameObject); }
		targetList = new List<Target>();

		SpawnTargets(settings);
	}

	IEnumerator SetBuildingBlocks()
    {
        //Deactivates the building block which corresponds to correct path
		//Also the next crossing on the map will have all these varibles blocks turned off as they may coincide (spacially) with the first (current) crossing
        if (!isCurrentCrossing)
        {
			foreach (Transform child in variableBlocks) { child.gameObject.SetActive(false); yield return new WaitForEndOfFrame(); }
		}
        else if (waypoints[0].turn.IsOperation() && waypoints[1].turn.IsOperation()) 
		{
			string blockNameToDeactivate = waypoints[0].turn.ToString() + waypoints[1].turn.ToString();

			foreach (Transform child in variableBlocks) { child.gameObject.SetActive(child.name != blockNameToDeactivate); yield return new WaitForEndOfFrame(); }
		}
		
    }
	void SpawnTargets(MainExperimentSetting settings) 
	{
		if(settings.maxTargets == 0) { return; }
		List<Transform> spawnPoints;
		Transform parentSpawPoints = null;

		if (!isFirstCrossing && waypoints[0].turn.IsOperation()) 
		{
			parentSpawPoints = FirstTurnTargetSpawnPoints;
			spawnPoints = SelectRandomSpawnPoints(parentSpawPoints, settings.minTargets, settings.maxTargets);

			InstantiateTargets(spawnPoints, waypoints[0], settings);
		}

		if(waypoints[1].turn.IsOperation() && waypoints[0].turn.IsOperation())
        {
			if (waypoints[0].turn == TurnType.Right) { parentSpawPoints = RightTargetSpawnPoints; }
			if (waypoints[0].turn == TurnType.Left) { parentSpawPoints = LeftTargetSpawnPoints; }
			if (waypoints[0].turn == TurnType.Straight) { parentSpawPoints = StraightTargetSpawnPoints; }

			spawnPoints = SelectRandomSpawnPoints(parentSpawPoints, settings.minTargets, settings.maxTargets);

			InstantiateTargets(spawnPoints, waypoints[1], settings);
		}
	}
	private void InstantiateTargets(List<Transform> spawnPoints, WaypointStruct waypoint, MainExperimentSetting settings)
    {
		GameObject target;
		List<Transform> orderedSpawnPointList = spawnPoints.OrderByDescending(s => Vector3.Magnitude(s.position - waypoint.waypoint.position)).ToList();
	
		foreach (Transform point in orderedSpawnPointList)
		{

			int ID = experimentManager.GetNextTargetID();
			//Varies position of target
			Vector3 sideVariation = point.name == "Right" ? waypoint.waypoint.right*Random.Range(0, 2f) : -waypoint.waypoint.right * Random.Range(0, 2f);
			Vector3 forwardVariation = waypoint.waypoint.forward * Random.Range(-4f, 4f);
			Vector3 positionTarget = point.position + forwardVariation + sideVariation;

			TargetDifficulty targetDifficulty;
			//During the practise drive we have easy targets for the first half of the track and hard targets for the second half.
			if(settings.targetDifficulty == TargetDifficulty.EasyAndMedium) 
			{
				
				int NTurns = experimentManager.experimentSettings.turns.Count();
				if (waypoint.waypointID < (int)Mathf.Floor(NTurns / 2)) { targetDifficulty = TargetDifficulty.easy; }
				else { targetDifficulty = TargetDifficulty.hard; }
			}
            else { targetDifficulty = settings.targetDifficulty; }

			target = Instantiate(TargetPrefab);
			target.transform.position = positionTarget;
			target.transform.parent = TargetParent;
			target.transform.name = "Target " + ID.ToString();
			target.GetComponent<Target>().SetDifficulty(targetDifficulty);
			target.GetComponent<Target>().waypoint = waypoint;
			target.GetComponent<Target>().ID = ID;
			target.GetComponent<Target>().side = point.name == "Left" ? Side.Left : Side.Right;
			targetList.Add(target.GetComponent<Target>());
		}
	}
	private List<Transform> SelectRandomSpawnPoints(Transform parent, int minTargets, int maxTargets)
    {

		List<Transform> spawnPoints = new List<Transform>();
		List<Transform> selectedSpawnPoints = new List<Transform>();
		bool selectionFinished = false;
		
		int numberOfTargets = Random.Range(minTargets, maxTargets + 1);

		foreach (Transform child in parent) { spawnPoints.Add(child); }

		while (!selectionFinished)
		{
			int randomIndex = (int)Mathf.Round(Random.Range(0, spawnPoints.Count() - 1));
            if (!selectedSpawnPoints.Contains(spawnPoints[randomIndex])){ selectedSpawnPoints.Add(spawnPoints[randomIndex]); }
			
			if(selectedSpawnPoints.Count() >= numberOfTargets) { selectionFinished = true; }
			if(selectedSpawnPoints.Count() == spawnPoints.Count()) { selectionFinished = true; }
		}

		return selectedSpawnPoints;
	}
    public void SetTriggers()
    {
		rightTrigger.tag = waypoints[0].turn == TurnType.Right ? "CorrectTurn" : "WrongTurn";
		leftTrigger.tag = waypoints[0].turn == TurnType.Left ? "CorrectTurn" : "WrongTurn";
		straightTrigger.tag = waypoints[0].turn == TurnType.Straight ? "CorrectTurn" : "WrongTurn";

		if (waypoints[0].turn == TurnType.Left) { 
			leftRightTrigger.tag = waypoints[1].turn == TurnType.Right ? "CorrectTurn" : "WrongTurn";
			leftLeftTrigger.tag = waypoints[1].turn == TurnType.Left ? "CorrectTurn" : "WrongTurn";

			if (waypoints[1].turn == TurnType.EndPoint) { leftEndTrigger.SetActive(true); }
		}
        else if (waypoints[0].turn == TurnType.Right) {

			rightRightTrigger.tag = waypoints[1].turn == TurnType.Right ? "CorrectTurn" : "WrongTurn";
			rightLeftTrigger.tag = waypoints[1].turn == TurnType.Left ? "CorrectTurn" : "WrongTurn";

			if (waypoints[1].turn == TurnType.EndPoint) { rightEndTrigger.SetActive(true); }
		}
        else if (waypoints[0].turn == TurnType.Straight) {
			
			straightRightTrigger.tag = waypoints[1].turn == TurnType.Right ? "CorrectTurn" : "WrongTurn";
			straightLeftTrigger.tag = waypoints[1].turn == TurnType.Left ? "CorrectTurn" : "WrongTurn";
			
			if (waypoints[1].turn == TurnType.EndPoint) { straightEndTrigger.SetActive(true); }
		}
		else if (waypoints[0].turn == TurnType.EndPoint)
        {
			triggerEnd.SetActive(true);
        }
    }
    void SetWaypoints()
    {
		waypoints[0].waypoint = waypointObjects.waypoint1;

		if (waypoints[0].turn == TurnType.None){ return; } 
        if (waypoints[0].turn == TurnType.EndPoint){ waypointObjects.waypoint1.position -= waypointObjects.waypoint1.forward * 20f;  return; }
        
        if (waypoints[0].turn == TurnType.Left) { waypointObjects.waypoint2.SetPositionAndRotation(waypointObjects.left.position, waypointObjects.left.rotation); }
        if (waypoints[0].turn == TurnType.Right) { waypointObjects.waypoint2.SetPositionAndRotation(waypointObjects.right.position, waypointObjects.right.rotation); }
        if (waypoints[0].turn == TurnType.Straight) { waypointObjects.waypoint2.SetPositionAndRotation(waypointObjects.straight.position, waypointObjects.straight.rotation); }
		if(waypoints[1].turn == TurnType.EndPoint) { waypointObjects.waypoint2.position -= waypointObjects.waypoint2.forward * 20f; }

		waypoints[1].waypoint = waypointObjects.waypoint2;
    }
    public List<Vector3> GetPointsNavigationLine()
    {
        List<Vector3> points = new List<Vector3>();

        points.Add(waypointObjects.startPoint.position);

        if (waypoints[0].turn == TurnType.None) { Debug.Log("Got None turn type..."); return null; }
        if (waypoints[0].turn == TurnType.EndPoint) { points.Add(waypointObjects.waypoint1.position - 30f * waypointObjects.waypoint1.forward); return points; }
        else if (waypoints[0].turn != TurnType.Straight){ foreach (Vector3 point in GetPointsCorner(waypointObjects.waypoint1, waypoints[0].turn)) { points.Add(point); }}

        if (waypoints[1].turn == TurnType.None) { Debug.Log("Got None turn type..."); return null; }
        if (waypoints[1].turn == TurnType.EndPoint) { points.Add(waypointObjects.waypoint2.position - 30f * waypointObjects.waypoint2.forward); return points; }
        else if (waypoints[1].turn != TurnType.Straight){ foreach (Vector3 point in GetPointsCorner(waypointObjects.waypoint2, waypoints[1].turn)) { points.Add(point); }}

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
public struct WaypointsObjects
{
    public Transform startPoint;
    public Transform centre;
    public Transform straight;
    public Transform right;
    public Transform left;

    public Transform waypoint1;
    public Transform waypoint2;
}


