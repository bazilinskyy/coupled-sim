using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class CreateVirtualCable : MonoBehaviour
{
	public newExperimentManager experimentManager;
	public RoadParameters roadParameters;
	public Material navigationPartMaterial;
	private GameObject navigationSymbology;
	private Vector3[] navigationLine;
	private  Vector3[] vertices;
	private int[] triangles;
	int pointsPerCorner = 30;
    WaypointsObjects waypoints;

	Crossings crossings;
    private void Awake()
    {

		MakeNavigationObject();
	}

    private void Update()
    {
        if (experimentManager.MakeVirtualCable()) { navigationSymbology.SetActive(true); }
        else { navigationSymbology.SetActive(false); }
    }

    private void MakeNavigationObject()
    {
		navigationSymbology = new GameObject("VirtualCable");
		//Make gameobejct with mesh and navigationpart component		
		navigationSymbology.AddComponent(typeof(MeshRenderer));
		navigationSymbology.AddComponent(typeof(MeshFilter));
		//navigationSymbology.AddComponent(typeof(MeshCollider));

		MeshRenderer meshRenderer = navigationSymbology.GetComponent<MeshRenderer>();
		meshRenderer.material = navigationPartMaterial;
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;

        /*navigationSymbology.GetComponent<MeshCollider>().convex = true;
        navigationSymbology.GetComponent<MeshCollider>().isTrigger = true;*/

    }
	List<Vector3> GetPointsCrossing(Crossing crossing)
    {
		CrossComponents components= crossing.components;
		waypoints = components.waypointObjects;
		List<Vector3> points = new List<Vector3>();
		Transform waypoint = waypoints.waypoint1;
		//Turn 1
		if (crossing.isCurrentCrossing) { points.Add(waypoints.startPoint.position - waypoints.startPoint.forward * 30f ); }
        else { points.Add(waypoints.startPoint.position); }
		
		if (components.turn1 == TurnType.None) { Debug.Log("GOT NONE ON FIRST TURN should not get here... CreateVirutalCable -> GetPointcomponents()"); return points;  }
		if (components.turn1.IsEndPoint()){ points.Add(waypoint.position); return points; }
		else if (components.turn1.IsTurn()){ foreach (Vector3 point in GetPointsCorner(waypoint, components.turn1)) { points.Add(point); }}
        else { points.Add(waypoint.position); }

		//Turn 2
		waypoint = waypoints.waypoint2;
		if (components.turn2 == TurnType.None) { return points; }
		if (components.turn2.IsEndPoint()) { points.Add(waypoint.position); }
		else if (components.turn2.IsTurn()) { foreach (Vector3 point in GetPointsCorner(waypoint, components.turn2)) { points.Add(point); } }
		else { points.Add(waypoint.position); }
		return points;
	}
	void CreateColliders()
    {
        if (navigationLine.Count() < 2) { return; }

		int count = 0;
		foreach(Vector3 point in navigationLine)
        {
			//Stop at point second to last
			if((count+1) == navigationLine.Count() ) { return; }

			Vector3 nextPoint = navigationLine[count+1];
			
			Vector3 direction = Vector3.Normalize(nextPoint - point);

			float distance = Vector3.Magnitude(nextPoint - point);

			GameObject colliderObj = new GameObject();
			colliderObj.transform.parent = navigationSymbology.transform;
			colliderObj.name = "Collider-" + count.ToString();
			colliderObj.transform.position = point + Vector3.up * roadParameters.heightVirtualCable;
			colliderObj.transform.LookAt(nextPoint + Vector3.up * roadParameters.heightVirtualCable);
			colliderObj.tag = LoggedTags.ConformalSymbology.ToString();

			BoxCollider collider = colliderObj.AddComponent<BoxCollider>();
		
			float x; float y; float z;

			
			//At small distance add 5 % size (means we are in a corner
			z = distance < 10f ? distance * 1.05f: distance;
			x = y = roadParameters.radiusPipe * 2;

			
			collider.size = new Vector3(x, y, z);
			collider.center = new Vector3(0, 0, z / 2);

			count++;
        }
    }
	public void MakeVirtualCable()
    {
		if (crossings != null) { MakeVirtualCable(crossings); }
        else { Debug.Log("Could not make virtual cable yet, crossings not set..."); }

	}
    public void MakeVirtualCable(Crossings _crossings)
    {
		crossings = _crossings;

		StartCoroutine(MakeVirtualCalbleIENumerator());
		
    }

	IEnumerator MakeVirtualCalbleIENumerator()
    {
		List<Vector3> points = GetPointsCrossing(crossings.CurrentCrossing());

		while (true)
		{
			if (crossings.crossing1.components.finishedMoving && crossings.crossing2.components.finishedMoving) { break; }
			else { yield return new WaitForEndOfFrame(); }
		}

		//Debug.Log($"Making vritual cable since: {crossings.crossing1.components.finishedMoving } and {crossings.crossing2.components.finishedMoving }.... ");

		experimentManager.dataManager.AddCurrentNavigationLine(points.ToArray());

		if (crossings.NextCrossing() != null) { points = points.Concat(GetPointsCrossing(crossings.NextCrossing())).ToList(); }

		navigationLine = points.ToArray();


		if (experimentManager.MakeVirtualCable()) { CreateNavigationPart(navigationLine); CreateColliders(); }
		

	}
    /*private void OnDrawGizmos()
    {
        if(navigationLine.Count() != 0)
        {
			int i = 0;
			Gizmos.color = Color.cyan;
			foreach(Vector3 point in navigationLine)
            {

				Gizmos.DrawSphere(point, 0.02f);
				UnityEditor.Handles.Label(point, $"{i}");
				i++;
			}
        }
    }*/
    private void CreateNavigationPart(Vector3[] points)
	{
		Mesh mesh;
		//Set the mesh
		navigationSymbology.GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		

		navigationSymbology.tag = "ConformalSymbology";

		mesh.name = "VirtualCable";
		mesh.vertices = vertices = GetVerticesVirtualCable(points);
		mesh.triangles = triangles = GetTrianglesVirtualCable(mesh.vertices);

		navigationSymbology.isStatic = true;

		//navigationSymbology.GetComponent<MeshCollider>().sharedMesh = mesh;
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
	
	private int[] GetTrianglesVirtualCable(Vector3[] vertices)
	{
		int pipeSegmentCount = roadParameters.pipeSegmentCount;
		int[] triangles = new int[(vertices.Length - pipeSegmentCount) * 2 * 3];

		for (int t = 0, i = 0; t < triangles.Length; i += pipeSegmentCount)
		{
			for (int v = 0; v < pipeSegmentCount; v++)
			{
				int index = v + i;
				if (v == (pipeSegmentCount - 1))
				{
					triangles[t] = triangles[t + 3] = index;
					triangles[t + 1] = triangles[t + 5] = i + pipeSegmentCount;
					triangles[t + 2] = i;
					triangles[t + 4] = index + pipeSegmentCount;
				}
				else
				{
					triangles[t] = triangles[t + 3] = index;
					triangles[t + 1] = triangles[t + 5] = index + 1 + pipeSegmentCount;
					triangles[t + 2] = index + 1;
					triangles[t + 4] = index + pipeSegmentCount;

				}
				t += 6;
			}
		}
		return triangles;
	}
	private Vector3[] GetVerticesVirtualCable(Vector3[] points)
	{
		//Set legnth of vertices arrays	
		Vector3[] vertices = new Vector3[points.Length * roadParameters.pipeSegmentCount];

		Vector3 worldY = new Vector3(0, 1, 0);
		Vector3 directionFirstLine = Vector3.Normalize(points[1] - points[0]);
		Vector3 lastPos = points[0] - directionFirstLine;

		int vertice_cnt = 0;
		//Loop through navigation points
		for (int i = 0; i < points.Length; i++)
		{
			Vector3 newPos = points[i];

			Vector3 directionOfPipe = Vector3.Normalize(newPos - lastPos);
			Vector3 perpendicularToPipe = Vector3.Normalize(Vector3.Cross(newPos - lastPos, worldY));
			Vector3[] circle = GetCircle(roadParameters.pipeSegmentCount, roadParameters.radiusPipe);

			circle = TransformCircle(circle, perpendicularToPipe, worldY, directionOfPipe, newPos);

			for (int v = 0; v < roadParameters.pipeSegmentCount; v++)
			{
				vertices[vertice_cnt] = circle[v] + worldY * roadParameters.heightVirtualCable;
				vertice_cnt += 1;

				//GIZMOS VIRTUAL CABLE
				/*Gizmos.DrawSphere(circle[v] + worldY * roadParameters.heightVirtualCable, 0.05f);
				UnityEditor.Handles.Label(circle[v] + worldY * roadParameters.heightVirtualCable, $"{i}");*/
			}
			//Save this pos so we can draw the next line segment
			lastPos = newPos;
		}

		return vertices;
	}
	private Vector3[] TransformCircle(Vector3[] points, Vector3 x, Vector3 y, Vector3 z, Vector3 t)
	{
		x = new Vector4(x.x, x.y, x.z, 0);
		y = new Vector4(y.x, y.y, y.z, 0);
		z = new Vector4(z.x, z.y, z.z, 0);
		t = new Vector4(t.x, t.y, t.z, 1);

		Matrix4x4 rotationMatrix = new Matrix4x4(x, y, z, t);

		for (int v = 0; v < points.Length; v++)
		{
			points[v] = rotationMatrix.MultiplyPoint3x4(points[v]);
		}
		return points;
	}
	private Vector3[] GetCircle(int SegmentCount, float radius)
	{
		Vector3[] points = new Vector3[SegmentCount];
		float vStep = (2f * Mathf.PI) / SegmentCount;
		for (int v = 0; v < SegmentCount; v++)
		{
			Vector3 p;

			p.x = radius * Mathf.Cos(v * vStep);
			p.y = radius * Mathf.Sin(v * vStep);
			p.z = 0;

			points[v] = p;
		}
		return points;
	}
}
