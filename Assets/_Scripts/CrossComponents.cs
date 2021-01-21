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
	public GameObject startPointCalibration;


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
	
	public Transform FirstTurnNoStraightTargetSpawnPoints;
	public Transform FirstTurnTargetSpawnPoints;
	public Transform RightTargetSpawnPoints;
	public Transform LeftTargetSpawnPoints;
	public Transform StraightTargetSpawnPoints;

	public Transform TargetParent;
	public GameObject TargetPrefab;

	public bool isFirstCrossing;

	public bool finishedMoving = false;
	public WaypointsObjects waypointObjects = new WaypointsObjects();
	public List<Target> targetList = new List<Target>();

	public WaypointStruct[] waypoints;

	public TurnType turn1;
	public TurnType turn2;

	private bool SetAllStatic = false;
	private newExperimentManager experimentManager;
	public bool isCurrentCrossing = true;

	private int totalObjects = 0;
    private void Awake()
    {
		experimentManager = MyUtils.GetExperimentManager();
    }
	public void SetCurrentCrossing (bool _isCurrentCrossing, Transform otherCrossing)
    {
		isCurrentCrossing = _isCurrentCrossing;

		if (isCurrentCrossing) { StartCoroutine(SetBuildingBlocksCurrentCrossing()); }
	}

	public void SetUpCrossing(WaypointStruct[] _waypoints, MainExperimentSetting settings, bool _isCurrentCrossing,
							bool _isFirstCrossing = false, Vector3 nextPosition = new Vector3(), Transform otherCrossing = null,
							float rotationAngleY = 0f)
	{
		if (experimentManager == null) { experimentManager = MyUtils.GetExperimentManager(); }

		waypoints = _waypoints;
		turn1 = waypoints[0].turn; turn2 = waypoints[1].turn;

		isCurrentCrossing = _isCurrentCrossing; isFirstCrossing = _isFirstCrossing;

		//This boolean is used by the CreateVirtualCablke script to ensure it only makes the virtual cable when the block is movedd
		finishedMoving = false;
		//Debug.Log($"{transform.name}.finished moving = false...");
		//Removes/adds buildings for first crossing
		blockFirstCrossing.gameObject.SetActive(isFirstCrossing);

		SetTriggers();

		SetWaypoints();

		if (isCurrentCrossing) { StartCoroutine(SetBuildingBlocksCurrentCrossing()); }
		else { StartCoroutine(SetBuildingBlocksNextCrossing(otherCrossing, nextPosition, rotationAngleY)); }

		RemoveTargets();

		SpawnTargets(settings);
	}
	public void RemoveTargets()
    {
		//?Remove old targets
		foreach (Transform child in TargetParent) { Destroy(child.gameObject); }
		targetList = new List<Target>();
	}

	IEnumerator SetBuildingBlocksCurrentCrossing()
    {
		finishedMoving = false;

		StartCoroutine(SetStaticAllChildren(transform, true, true));
		
		while (SetAllStatic == false) { yield return new WaitForEndOfFrame(); }

		if (waypoints[0].turn.IsOperation() && waypoints[1].turn.IsOperation())
		{

			string blockNameToDeactivate = waypoints[0].turn.ToString() + waypoints[1].turn.ToString();

			string otherTurn = waypoints[1].turn.IsRightTurn() ? "Left" : "Right"; //if right turn -> ;left, otherise wright

			string[] blockNamesToActivate = { waypoints[0].turn.ToString() + otherTurn, "Left", "Right" };
			//Debug.Log($"{transform.name} should active {blockNamesToActivate[0]}, {blockNamesToActivate[1]}, {blockNamesToActivate[2]} and  deactivate {blockNameToDeactivate}...");

			foreach (Transform child in variableBlocks)
			{
				//Continue to next child if this is not one of the childs we are looking for
				if (child.name == blockNameToDeactivate && blockNamesToActivate.Contains(child.name)) { continue; }

				bool activateBlock = (child.name != blockNameToDeactivate || blockNamesToActivate.Contains(child.name));

				child.gameObject.SetActive(activateBlock); yield return new WaitForEndOfFrame();
			}
		}

		finishedMoving = true;
		//Debug.Log($"{transform.name}.finished moving = true...");

	}
	IEnumerator SetBuildingBlocksNextCrossing(Transform otherCrossing = null, Vector3 nextPosition = new Vector3(), float rotationAngleY= 0)
    {
		//Deactivates the building block which corresponds to correct path
		//Also the next crossing on the map will have all these varibles blocks turned off as they may coincide (spacially) with the first (current) crossing
		//Done in corourtine as these activation operations are expensive
		/*    if (!isCurrentCrossing)
			{
				//We need to deactivate leftleft and right right as they can interfere with the current corssing
				string[] blocksToDeactivate = { "LeftLeft", "LeftRight", "RightLeft", "RightRight", "Left", "Right" };
				foreach (Transform child in variableBlocks) {

					bool deactivateBlock = blocksToDeactivate.Contains(child.name);
					if (deactivateBlock)
					{
						//Debug.Log($"{transform.name} should deactivate {child.name}...");
						//foreach (Transform childsChild in child) { childsChild.gameObject.SetActive(false); yield return new WaitForSeconds(0.05f); }
						child.gameObject.SetActive(false); yield return new WaitForEndOfFrame();
					}

				}
			}

			//Put crossing in place
			if (!isFirstCrossing && otherCrossing != null)
			{
	*/
		finishedMoving = false;
		//We need to deactivate leftleft and right right as they can interfere with the current corssing
		string[] blocksToDeactivate = { "LeftLeft", "LeftRight", "RightLeft", "RightRight", "Left", "Right" };
		foreach (Transform child in variableBlocks)
		{

			bool deactivateBlock = blocksToDeactivate.Contains(child.name);
			if (deactivateBlock)
			{
				//Debug.Log($"{transform.name} should deactivate {child.name}...");
				//foreach (Transform childsChild in child) { childsChild.gameObject.SetActive(false); yield return new WaitForSeconds(0.05f); }
				child.gameObject.SetActive(false); yield return new WaitForEndOfFrame();
			}
		}

		StartCoroutine(SetStaticAllChildren(transform, false, true));

		//Wait till all children are set to nonstatic
		while (SetAllStatic == false) { yield return new WaitForEndOfFrame(); }

		transform.position = nextPosition; yield return new WaitForEndOfFrame();

		transform.rotation = otherCrossing.rotation; yield return new WaitForEndOfFrame();
		transform.Rotate(transform.up, rotationAngleY); yield return new WaitForEndOfFrame();

		//Debug.Log($"{transform.name} has been moved...");
		StartCoroutine(SetStaticAllChildren(transform, true, true));
		//Wait till all children are set to static
		while (SetAllStatic == false) { yield return new WaitForEndOfFrame(); }

		finishedMoving = true;
		//Debug.Log($"{transform.name}.finished moving = true...");


		/*
				//If this is the next crossing and we will get there (e.g., both next operations are turns we have to deactivate/activate the needed block as well
				if (isCurrentCrossing && waypoints[0].turn.IsOperation() && waypoints[1].turn.IsOperation()) 
				{

					string blockNameToDeactivate = waypoints[0].turn.ToString() + waypoints[1].turn.ToString();

					string otherTurn = waypoints[1].turn.IsRightTurn() ? "Left" : "Right"; //if right turn -> ;left, otherise wright

					string[] blockNamesToActivate = { waypoints[0].turn.ToString() + otherTurn, "Left", "Right" };
					//Debug.Log($"{transform.name} should active {blockNamesToActivate[0]}, {blockNamesToActivate[1]}, {blockNamesToActivate[2]} and  deactivate {blockNameToDeactivate}...");

					foreach (Transform child in variableBlocks)
					{
						//Continue to next child if this is not one of the childs we are looking for
						if(child.name == blockNameToDeactivate && blockNamesToActivate.Contains(child.name)) { continue; }

						bool activateBlock = (child.name != blockNameToDeactivate || blockNamesToActivate.Contains(child.name));

						child.gameObject.SetActive(activateBlock); yield return new WaitForEndOfFrame();
					}	
				}*/


	}
	IEnumerator SetStaticAllChildren(Transform parent, bool makeStatic, bool firstCall = false)
    {
        if (firstCall)
        {
			totalObjects = 0;
			SetAllStatic = false;
		}

		
		parent.gameObject.isStatic = true;

		foreach ( Transform child in parent) 
		{
			//Skip targets
			if(child.name == "Targets") { continue; }

			//We do 1000 objects per frame ~taking around 6 frames to complete
			if (totalObjects % 1000 == 0) { yield return new WaitForEndOfFrame(); }

			child.gameObject.isStatic = makeStatic;
			totalObjects++;

			if (child.childCount != 0) { StartCoroutine(SetStaticAllChildren(child, makeStatic)); }
		}

		if (firstCall) { SetAllStatic = true; }
	}
	public void DisableVariableBlocks()
    {
		StartCoroutine(DisableVariableBlocksSlowly());
    }
	private IEnumerator DisableVariableBlocksSlowly()
	{
		foreach (Transform child in variableBlocks)
		{
			foreach (Transform childsChild in child) { childsChild.gameObject.SetActive(false); yield return new WaitForEndOfFrame(); }

			child.gameObject.SetActive(false); yield return new WaitForEndOfFrame();
		}
	}
	public void SpawnTargets(MainExperimentSetting settings) 
	{
		if(settings.targetsPerCrossing == 0) { return; }
		List<Transform> spawnPoints;
		Transform parentSpawPoints = null;

		int numberOfTargetsTurn1 = Random.Range(settings.minTargetsPerTurn, settings.maxTargetsPerTurn+1);
		int numberOfTargetsTurn2 = settings.targetsPerCrossing - numberOfTargetsTurn1;

		if ((!isFirstCrossing || settings.experimentType.IsTargetCalibration()) && waypoints[0].turn.IsOperation()) 
		{
            if (!waypoints[0].turn.IsStraight()) { parentSpawPoints = FirstTurnNoStraightTargetSpawnPoints; }
            else { parentSpawPoints = FirstTurnTargetSpawnPoints; }
			
			spawnPoints = SelectRandomSpawnPoints(parentSpawPoints, numberOfTargetsTurn1);

			InstantiateTargets(spawnPoints, waypoints[0], settings);
		}

		if(waypoints[1].turn.IsOperation() && waypoints[0].turn.IsOperation())
        {
			if (waypoints[0].turn == TurnType.Right) { parentSpawPoints = RightTargetSpawnPoints; }
			if (waypoints[0].turn == TurnType.Left) { parentSpawPoints = LeftTargetSpawnPoints; }
			if (waypoints[0].turn == TurnType.Straight) { parentSpawPoints = StraightTargetSpawnPoints; }

			spawnPoints = SelectRandomSpawnPoints(parentSpawPoints, numberOfTargetsTurn2);

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
			//Debug.Log($"Instantiating target: {ID}...");
			//Varies position of target number: odd = right side, even -> left side...
			Vector3 sideVariation = waypoint.waypoint.right*Random.Range(-2.5f, 2.5f);
			Vector3 forwardVariation = waypoint.waypoint.forward * Random.Range(-7f, 7f);
			Vector3 positionTarget = point.position + forwardVariation + sideVariation;

			TargetDifficulty targetDifficulty = settings.targetDifficulty;
			
			//Side is determined by spawnpoint index i.e., name EXCEPT when it is 10th spawnpoint for odd indexed waypoints
			Side side = int.Parse(point.name) % 2 == 0 ? Side.Left : Side.Right;

			if(waypoint.waypointID % 2 != 0 && int.Parse(point.name) == 10) { side = Side.Undetermined; }

			//Add the targets to the experiment manager 
			experimentManager.leftTargets += side == Side.Left ? 1 : 0;
			experimentManager.rightTargets += side == Side.Right ? 1 : 0;

			target = Instantiate(TargetPrefab);
			target.transform.position = positionTarget;
			target.transform.parent = TargetParent;
			target.transform.name = "Target " + ID.ToString();
			target.GetComponent<Target>().SetDifficulty(targetDifficulty);
			target.GetComponent<Target>().waypoint = waypoint;
			target.GetComponent<Target>().ID = ID;
			target.GetComponent<Target>().side = side;
			target.GetComponent<Target>().positionNumber = int.Parse(point.name);
			target.GetComponent<Target>().transparency = target.GetComponent<MeshRenderer>().material.color.a;
			target.transform.localScale = new Vector3(settings.targetSize, settings.targetSize, settings.targetSize);
			target.GetComponent<SphereCollider>().radius = .75f / settings.targetSize;

			targetList.Add(target.GetComponent<Target>());
		}
	}
	private List<Transform> SelectRandomSpawnPoints(Transform parent, int numberOfTargets)
    {
		//Debug.Log("Selecting spawn points...");
		List<Transform> spawnPoints = new List<Transform>();
		List<Transform> selectedSpawnPoints = new List<Transform>();
		bool selectionFinished = false;

		List<int> leftTargetsIndices = new List<int>(); 
		List<int> rightTargetsIndices = new List<int>();

		foreach(Transform spawnPoint in spawnPoints) 
		{
			int index = int.Parse(spawnPoint.name);
			if ( index % 2 == 0) { leftTargetsIndices.Add(index); }
            else { rightTargetsIndices.Add(index); }
		}

		foreach (Transform child in parent) { spawnPoints.Add(child);  }

		while (!selectionFinished)
		{
			Debug.Log("While loop SelectRandomSpawnPoints()...");
			//Choose left or right target -> higher probability with less targets chosen on that side
			// NEGATIVE = LEFT (= EVEN INDEXED SPAWN POINT), POSITIVE = RIGHT (= ODD INDEXED SPAWN POINT)
			float randomNegativeOrPositive = Random.Range(-1f / (2f*experimentManager.leftTargets + 1f), 1f / (2f*experimentManager.rightTargets + 1f));
			float sign = Mathf.Sign(randomNegativeOrPositive);


			int spawnPointIndex = ChooseTargetSpawnPoint((int)sign, spawnPoints, selectedSpawnPoints, leftTargetsIndices, rightTargetsIndices);

			Transform spawnPointToAdd = spawnPoints.Where(t => t.name == spawnPointIndex.ToString()).First();

			//Debug.Log($"Sign: {sign}, spawnPointToAdd: {spawnPointToAdd.name}, {spawnPointIndex}...");

			selectedSpawnPoints.Add(spawnPointToAdd);
		
			if(selectedSpawnPoints.Count() >= numberOfTargets) { selectionFinished = true; }
			if(selectedSpawnPoints.Count() == spawnPoints.Count()) { selectionFinished = true; }
		}

		return selectedSpawnPoints;
	}
	private int ChooseTargetSpawnPoint(int sign, List<Transform> spawnPoints, List<Transform> selectedSpawnPoints, List<int> leftTargetsIndices, List<int> rightTargetsIndices, bool tryingOtherSide=false)
	{
		//Chooses a target on either the left or right side
		List<int> indicesToChooseFrom;
		int spawnPointIndex = 0;
		//choose from right array
		if (sign > 0) { indicesToChooseFrom = rightTargetsIndices; }
        else { indicesToChooseFrom = leftTargetsIndices; }

		while (indicesToChooseFrom.Count > 0)
        {
			Debug.Log("While loop ChooseTargetSpawnPoint()...");
			int randomListIndex = (int)Random.Range(0, indicesToChooseFrom.Count);
			spawnPointIndex = indicesToChooseFrom[randomListIndex];

			Transform spawnPointToAdd = spawnPoints.Where(t => t.name == spawnPointIndex.ToString()).First();
			if (!selectedSpawnPoints.Contains(spawnPointToAdd)){ break; }
            else { indicesToChooseFrom.Remove(spawnPointIndex); }

		}

		//If somehow we are unable to choose any on this side (if we have more than 5 targets...) we choose the other side
		if(indicesToChooseFrom.Count == 0) 
		{ 
			Debug.Log("CrossComponents.cs -> ChooseTargetSpawnPoint: Could not choose spawnPointIndex for this side!!\nTrying other side"); 
			spawnPointIndex = ChooseTargetSpawnPoint(-sign, spawnPoints, selectedSpawnPoints, leftTargetsIndices, rightTargetsIndices, true); 
		}
		//If second try also failed something is definetely wrong (or we have more than 10 targets)
		if(tryingOtherSide && indicesToChooseFrom.Count == 0) { Debug.LogError("Error in CrossComponents.cs -> ChooseTargetSpawnPoint, could not choose spawnPointIndex, MAXIMUM targets = 10!!\nSet targetspawnpoint to 1"); spawnPointIndex = 1; }

		return spawnPointIndex;
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


