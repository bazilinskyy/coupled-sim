using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//Interpolation between points with a Catmull-Rom spline
[RequireComponent(typeof(MeshFilter))]
public class SplineCreator : MonoBehaviour
{

	public RoadParameters roadParameters;

	public int pointsPerSpline = 100;

	//Has to be at least 4 points
	public Vector3[] pointList;
	public bool showGizmos = true;

	[Range(0.01f, 0.2f)]    //Make sure it's is adding up to 1, so 0.3 will give a gap, but 0.2 will work
	public float resolution = 0.05f; private float my_resolution = 0.05f;

	//Up to how many waypoints do we render?
	public int numberOfSegmentsToRender = 4;


	public bool _renderConformal1 = true; private bool renderConformal1 = true;
	public float pipeRadius = 1f;
	public int pipeSegmentCount = 9;
	private Vector3[] verticesConformal1;
	private int[] trianglesConformal1;


	public bool _renderConformal2 = true; private bool renderConformal2 = true;
	public float widthRoad = 3f;
	public float heightSystem = 10f;
	private Vector3[] verticesConformal2;
	private int[] trianglesConformal2;


	public bool _renderConformal3 = true; private bool renderConformal3 = true;
	private Vector3[] verticesConformal3;
	private int[] trianglesConformal3;


	public bool _pressMeToRerender = true; private bool pressMeToRerender = true;
	private Mesh mesh;
	private int currentChildCount = 0;
	
	private float navWidth = 1;
	private void Awake()
	{
		renderConformal1 = _renderConformal1;
		renderConformal2 = _renderConformal2;
		renderConformal3 = _renderConformal3;

		UpdateControlPoints();
		UpdateMesh();
	}

	private void Update()
	{
		CheckForChanges();
	}

	void CheckForChanges()
	{
		//if number of children change --> update mesh
		if (currentChildCount != transform.childCount)
		{
			UpdateMesh();
			currentChildCount = transform.childCount;
		}

		//if resolution change --> update mesh
		if (resolution != my_resolution)
		{
			my_resolution = resolution;
			UpdateMesh();
		}

		//if render booleans change --> update mesh
		if (renderConformal1 != _renderConformal1 || renderConformal2 != _renderConformal2 || renderConformal3 != _renderConformal3)
		{
			renderConformal1 = _renderConformal1;
			renderConformal2 = _renderConformal2;
			renderConformal3 = _renderConformal3;
			UpdateMesh();
		}

		if(pressMeToRerender != _pressMeToRerender)
		{
			UpdateMesh();
		}
	}
	public void UpdateMesh()
	{
		//Debug.Log("UPDATING MESH");
		if (renderConformal1 || renderConformal2 || renderConformal3)
		{
			
			GetComponent<MeshFilter>().mesh = mesh = new Mesh();
			mesh.name = "Pipe";
			SetVertices();
			SetTriangles();
			mesh.RecalculateNormals();
		}
		else
		{
			GetComponent<MeshFilter>().mesh = mesh = new Mesh();
			mesh.name = "nothing!";
		}
	}
	//Display without having to press play
	void OnDrawGizmos()
	{

		Gizmos.color = Color.cyan;
		if (showGizmos)
		{
			List<Waypoint> waypointList = transform.GetComponent<NavigationManager>().GetOrderedWaypointList();

			Vector3[] points = GetNavigationLine(waypointList);
			//Set forward direction of splinepoints based on acquired spline
			SetForwardDirectionSplinePoints(waypointList, points);
			//Check if repearting elements in spline points
			printRepeating(points);

			DrawGizmoLines(points);
		}
	}

	void DrawGizmoLines(Vector3[] points)
	{
		for(int i =0; i<(points.Length-1); i++)
		{
			Gizmos.DrawLine(points[i], points[i + 1]);
		}
	}

	void printRepeating(Vector3[] arr)
	{
		int size = arr.Length;
		int i, j;
		
		for (i = 0; i < size; i++)
			for (j = i + 1; j < size; j++)
				if (arr[i] == arr[j])
					Debug.Log($"Index {i} == {j} = {arr[i].ToString()}....");
	}
	private Vector3[] GetNavigationLine(List<Waypoint> waypointList)
	{

		//List of potetnial spline points
		List<Waypoint> splineList = new List<Waypoint>();
		bool addedSplineWaypoints = false;

		Vector3[] points = new Vector3[0];//Array of navigation points
		Vector3[] pointSet; //Array of splinePoints

		//loop trough points and draw gizmos based on operation of waypoints and roadParameters
		foreach (Waypoint waypoint in waypointList)
		{
			//If we encounter spline points we will add these to splineList untill we reach a non-spline point
			if (waypoint.operation == Operation.SplinePoint) {
				splineList.Add(waypoint);
				addedSplineWaypoints = true;
				continue;
				}
			//Draw all the spline points and clear the list
			if (addedSplineWaypoints) {
				//Adds this non-spline point as spline point (As we have to finish here)
				splineList.Add(waypoint);
				pointSet = GetSplinePoints(splineList);
				points = AppendArrays(points, pointSet);
				// Clear list and set bool to false
				splineList.Clear();
				addedSplineWaypoints = false;
			}
			//Then continue as normal
			pointSet = GetPointsWaypoint(waypoint);
			points = AppendArrays(points, pointSet);
			
		}
		return points;
	}

	private Vector3[] GetPointsWaypoint(Waypoint waypoint)
	{
		//Never adds the next waypoints to the point list
		//So we add up to the current waypoint
		Vector3[] points = new Vector3[0];
		if (waypoint.operation == Operation.StartPoint || waypoint.operation == Operation.Straight || waypoint.operation == Operation.None || waypoint.operation == Operation.EndPoint) { 
			points = new Vector3[] { waypoint.transform.position }; }
		else if (waypoint.operation == Operation.TurnRightLong || waypoint.operation == Operation.TurnRightShort || waypoint.operation == Operation.TurnLeftLong) { 
			points = GetPointsCorner(waypoint); }
		
		return points;
		}


	private Vector3[] GetPointsCorner(Waypoint waypoint)
	{
		//get radius based on waypoint oepration and raodParameter scriptable variable
		float radius;
		if (waypoint.operation == Operation.TurnRightShort) { radius = roadParameters.radiusShort; }
		else { radius = roadParameters.radiusLong; }
		
		//Get half circle in x,y frame
		Vector2[] quarterCircle = GetQuarterCircle(radius, pointsPerSpline, waypoint.operation);

		//Transpose circle to the current waypoint position and rotation
		Vector3[] points = TransformVector2ToVector3(quarterCircle);
		points = TransposePoints(waypoint, points);

		return points;
		
	}

	private Vector3[] RemoveLastElementVector3(Vector3[] array)
	{
		Vector3[] arrayOut = new Vector3[array.Length - 1];
		System.Array.Copy(array, 0, arrayOut, 0, arrayOut.Length);
		return arrayOut;
	}
	private Vector3[] TransposePoints(Waypoint waypoint, Vector3[] circle)
	{
		//Transposes points to be in a frame (forward,perpendicular2forward, worldY)  
		//Input circle is vector3[] in the x,y,z frame

		//z = worldy
		//y = forward
		//x = perp2forward
		Vector3 worldY = new Vector3(0, 1f, 0);
		Vector3 forward = waypoint.transform.forward;
		Vector3 perp2forward = -Vector3.Cross(forward, worldY); //Has two possibilities, has to be the minus one

		Vector3 waypointPosition = waypoint.transform.position;
		

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
		for(int i = 0; i < points.Length; i++)
		{
			newPoints[i] = new Vector3(points[i].x, points[i].y, z);
		}
		return newPoints;
	}
	private Vector2[] GetQuarterCircle(float radius, int numberOfPoints, Operation operation)
	{
		//returns quarter circle in the x,y frame 
		//operation -> right --> (0,0) to (radius,radius)
		//operation -> left --> (0,0) to (-radius, radius)
		Vector2[] pointsCircle = new Vector2[numberOfPoints];

		float radStep = Mathf.PI / 2 / numberOfPoints;

		int sign = 1;
		if(operation == Operation.TurnLeftLong) { sign = -1; }
		//- PI to 0 gets a half circle to the right
		for (int i = 0; i < numberOfPoints; i++)
		{
			float radians = -Mathf.PI / 2 + i * radStep;
			float x = sign * (radius * Mathf.Sin(radians) + radius);
			float y = radius * Mathf.Cos(radians);

			pointsCircle[i] = new Vector2(x,y);
		}
		return pointsCircle;
	}

	private Vector3[] GetSplinePoints(List<Waypoint> splineList)
	{
		//Contains points which have to be used for the creation of the spline
		Vector3[] mainSplinePoints = GetMainSplinePoints(splineList);
		//WIll contain all points in the spline
		Vector3[] allSplinePoints = new Vector3[0];
		//Will contain points for each specific spline
		Vector3[] catmullRomSpline;
		for ( int i=1; i<(mainSplinePoints.Length-2); i++)
		{
			Vector3 p0 = mainSplinePoints[i - 1];
			Vector3 p1 = mainSplinePoints[i];
			Vector3 p2 = mainSplinePoints[i + 1];
			Vector3 p3 = mainSplinePoints[i + 2];

			catmullRomSpline = CatmullRomSpline(p0, p1, p2, p3);
			allSplinePoints = AppendArrays(allSplinePoints, catmullRomSpline);

			DrawCatmullSpline(catmullRomSpline);
		}

		allSplinePoints = RemoveLastElementVector3(allSplinePoints);

		return allSplinePoints;
	}

	private Vector3[] AppendArrays( Vector3[] arr1, Vector3[] arr2)
	{
		Vector3[] arrOut = new Vector3[arr1.Length + arr2.Length];
		System.Array.Copy(arr1, arrOut, arr1.Length);
		System.Array.Copy(arr2, 0, arrOut, arr1.Length, arr2.Length);
		return arrOut;
	}

	void SetForwardDirectionSplinePoints(List<Waypoint> waypoints, Vector3[] splinePoints)
	{

		bool lastPointWasSpline = false;
		foreach ( Waypoint waypoint in waypoints)
		{
			
			if (waypoint.operation == Operation.SplinePoint)
			{
				lastPointWasSpline = true;
				//Set forward direction of waypoint
				int index = System.Array.FindIndex(splinePoints, element => (element == waypoint.transform.position));
				Vector3 forward = splinePoints[index + 1] - splinePoints[index];
				if (!(forward == Vector3.zero))
				{
					waypoint.transform.forward = forward.normalized;
				}

			}
			else if (lastPointWasSpline)
			{
				//This is the waypoint which the last spline point is connected to
				//Set forward direction of waypoint
				int index = System.Array.FindIndex(splinePoints, element => (element == waypoint.transform.position));
				if (index >= 0)
				{
					Vector3 forward = splinePoints[index] - splinePoints[index - 1];
					if (!(forward == Vector3.zero))
					{
						waypoint.transform.forward = forward.normalized;
					}

				}
				lastPointWasSpline = false;

			}
		}

	}
	void DrawCatmullSpline(Vector3[] spline)
	{
		for(int i=0; i<(spline.Length-1); i++)
		{
			Gizmos.DrawLine(spline[i], spline[i + 1]);
		}
	}
	private Vector3[] GetMainSplinePoints(List<Waypoint> waypoints)
	{
		//We want a spline starting at waypoints [0] up to waypoint[waypoints.Count]
		//CatmullRomSpline doesnt render the first and last point
		//So we add a point to the beginning and to the end in proper directions

		Vector3[] mainSplinePoints = new Vector3[waypoints.Count + 2];
		//First and last waypoints
		Waypoint firstWaypoint = waypoints[0]; Waypoint lastWaypoint = waypoints.Last();
		//Extra splinepoints to beginning and end
		Vector3 firstSplinePoint = firstWaypoint.transform.position - firstWaypoint.transform.forward;
		Vector3 lastSplinePoint = lastWaypoint.transform.position + lastWaypoint.transform.forward;
		mainSplinePoints[0] = firstSplinePoint;
		mainSplinePoints[waypoints.Count + 1] = lastSplinePoint;

		//Add positions of all spline points
		int splineCnt = 1;
		foreach (Waypoint waypoint in waypoints)
		{
			mainSplinePoints[splineCnt] = waypoint.transform.position;
			splineCnt++;
		}
		
		return mainSplinePoints;
	}
	//Populate controlpoint list with children form waypointROot
	public void UpdateControlPoints()
	{
		List<Waypoint> orderedWaypointOrderList =  GetOrderedWaypointList();
		pointList = new Vector3[CalculateNumberOfSplinePoints(orderedWaypointOrderList)];
			
		int i = 0;
		foreach (Waypoint waypoint in orderedWaypointOrderList)
		{
			//If not rendering skip 
			if (!waypoint.renderMe)
			{
				continue;
			}
			if (waypoint.extraSplinePoint)
			{
				Vector3 directionLastPoint = Vector3.Normalize(orderedWaypointOrderList[waypoint.orderId-1].transform.position - waypoint.transform.position);
				pointList[i] = waypoint.transform.position + waypoint.firstPointDistance * directionLastPoint;
				pointList[i + 1] = waypoint.transform.position;
				pointList[i + 2] = waypoint.transform.position + waypoint.secondPointDistance * waypoint.transform.forward;
				i += 3;
			}
			else if (waypoint.operation == Operation.StartPoint)
			{
				pointList[i] = waypoint.transform.position;
				pointList[i + 1] = waypoint.transform.position - waypoint.transform.forward;
				i+= 2;
			}
			else if(waypoint.operation == Operation.EndPoint)
			{
				pointList[i] = waypoint.transform.position;
				pointList[i + 1] = waypoint.transform.position + waypoint.transform.forward;
				i += 2;
			}
			else
			{
				pointList[i] = waypoint.transform.position;
				i++;
			}
				
		}
	}
	private List<Waypoint> GetOrderedWaypointList()
	{
		List<Waypoint> waypointOrderList = new List<Waypoint>();
		foreach (Transform child in transform)
		{
			Waypoint waypoint = child.gameObject.GetComponent<Waypoint>();
			if (waypoint != null)
			{
				waypointOrderList.Add(waypoint);
			}
		}
		List<Waypoint> orderedWaypointOrderList = waypointOrderList.OrderBy(d => d.orderId).ToList();
		return orderedWaypointOrderList;
	}

	int CalculateNumberOfSplinePoints(List<Waypoint> waypointList)
	{
		int pointCount = 0;
		foreach( Waypoint waypoint in waypointList)
		{
			//skip if it shouldnt be rendered
			if (!waypoint.renderMe) { continue; }
			if (waypoint.extraSplinePoint)
			{
				pointCount += 3;
			}
			else if (waypoint.operation == Operation.StartPoint || waypoint.operation == Operation.EndPoint) { pointCount+=2; }
			else { pointCount++; }

		}

		return pointCount;
	}
	void SetVertices()
	{
		int loops = Mathf.FloorToInt(1f / resolution);
		
		if (renderConformal1) { verticesConformal1 = new Vector3[loops * (pointList.Length - 3) * pipeSegmentCount]; } else { verticesConformal1 = new Vector3[0]; }
		if (renderConformal2) { verticesConformal2 = new Vector3[loops * (pointList.Length - 3) * 2]; } else { verticesConformal2 = new Vector3[0]; }
		if (renderConformal3) { verticesConformal3 = new Vector3[0]; } else { verticesConformal3 = new Vector3[0];  }
		int vertice_cnt_conf1 = 0;  int vertice_cnt_conf2 = 0;  int vertice_cnt_conf3 = 0;

		Vector3 basePosition = -transform.position;
		Vector3 worldY = new Vector3(0, 1, 0);

		//Draw the Catmull-Rom spline between the points
		for (int i = 0; i < pointList.Length; i++)
		{
			//Cant draw between the endpoints
			//Neither do we need to draw from the second to the last endpoint
			//...if we are not making a looping line
			if ((i == 0 || i == pointList.Length - 2 || i == pointList.Length - 1))
			{
				continue;
			}

			//The 4 points we need to form a spline between p1 and p2
			Vector3 p0 = pointList[ClampListPos(i - 1)];
			Vector3 p1 = pointList[i];
			Vector3 p2 = pointList[ClampListPos(i + 1)];
			Vector3 p3 = pointList[ClampListPos(i + 2)];

			//The start position of the line
			Vector3 lastPos = p1 + basePosition;

			for (int k = 1; k <= loops; k++)
			{
				//Which t position are we at?
				float t = k * resolution;

				//Find the coordinate between the end points with a Catmull-Rom spline
				Vector3 newPos = basePosition + GetCatmullRomPosition(t, p0, p1, p2, p3);

				//If we are making a pipe construct the following vertices:
				if (renderConformal1)
				{
					Vector3 directionOfPipe = Vector3.Normalize(newPos - lastPos);
					Vector3 perpendicularToPipe = Vector3.Normalize(Vector3.Cross(newPos - lastPos, worldY));
					Vector3[] points = getCircle();

					points = transformCircle(points, perpendicularToPipe, worldY, directionOfPipe, newPos);

					for (int v = 0; v < pipeSegmentCount; v++)
					{
						verticesConformal1[vertice_cnt_conf1] = points[v];
						vertice_cnt_conf1 += 1;
					}
				}
				//constructing surface
				if (renderConformal2)
				{
					Vector3 sideDirection = Vector3.Normalize(Vector3.Cross(newPos - lastPos, worldY));
					Vector3 verticeA = lastPos + sideDirection * widthRoad - worldY * heightSystem;
					Vector3 verticeB = lastPos - sideDirection * widthRoad - worldY * heightSystem;

					verticesConformal2[vertice_cnt_conf2] = verticeA;
					verticesConformal2[vertice_cnt_conf2 + 1] = verticeB;
					vertice_cnt_conf2 += 2;
					
				}

				//Save this pos so we can draw the next line segment
				lastPos = newPos;
			}
		}

		//Concat arays and set to mesh
		Vector3[] vertices = new Vector3[verticesConformal1.Length + verticesConformal2.Length + verticesConformal3.Length];
		verticesConformal1.CopyTo(vertices, 0);
		verticesConformal2.CopyTo(vertices, verticesConformal1.Length);
		verticesConformal3.CopyTo(vertices, verticesConformal1.Length + verticesConformal2.Length);

		mesh.vertices = vertices;
	}

	private Vector3[] transformCircle(Vector3[] points, Vector3 x, Vector3 y, Vector3 z, Vector3 t)
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
	private Vector3[] getCircle()
	{
		Vector3[] points = new Vector3[pipeSegmentCount];
		float vStep = (2f * Mathf.PI) / pipeSegmentCount;
		for (int v = 0; v < pipeSegmentCount; v++)
		{
			Vector3 p;

			p.x = pipeRadius * Mathf.Cos(v * vStep);
			p.y = pipeRadius * Mathf.Sin(v * vStep);
			p.z = 0;

			points[v] = p;
		}
		return points;
	}
	void SetTriangles()
	{
		if (renderConformal1) { trianglesConformal1 = new int[(verticesConformal1.Length - pipeSegmentCount) * 2 * 3]; } else { trianglesConformal1 = new int[0]; }
		if (renderConformal2) { trianglesConformal2 = new int[(verticesConformal2.Length -2 ) * 3]; } else { trianglesConformal2 = new int[0]; }
		if (renderConformal3) { trianglesConformal3 = new int[0]; } else { trianglesConformal3 = new int[0]; }

		if (renderConformal1)
		{
			for (int t = 0, i = 0; t < trianglesConformal1.Length; i += pipeSegmentCount)
			{
				for (int v = 0; v < pipeSegmentCount; v++)
				{
					int index = v + i;
					if (v == (pipeSegmentCount - 1))
					{
						trianglesConformal1[t] = trianglesConformal1[t + 3] = index;
						trianglesConformal1[t + 1] = trianglesConformal1[t + 5] = i + 1 + pipeSegmentCount;
						trianglesConformal1[t + 2] = i + 1;
						trianglesConformal1[t + 4] = index + pipeSegmentCount;
					}
					else
					{
						trianglesConformal1[t] = trianglesConformal1[t + 3] = index;
						trianglesConformal1[t + 1] = trianglesConformal1[t + 5] = index + 1 + pipeSegmentCount;
						trianglesConformal1[t + 2] = index + 1;
						trianglesConformal1[t + 4] = index + pipeSegmentCount;

					}
					t += 6;
				}

			}
		}
		if (renderConformal2)
		{
			//As triangles point to indeces of vertices we have to start at the vertex index that corresponds to these elements
			int startPoint = verticesConformal1.Length;
			for (int t = 0, i = startPoint; t < trianglesConformal2.Length; t+=6, i += 2)
			{

				trianglesConformal2[t] = i;
				trianglesConformal2[t + 1] = trianglesConformal2[t + 4] = i + 2;
				trianglesConformal2[t + 2] = trianglesConformal2[t + 3] = i + 1;
				trianglesConformal2[t + 5] = i + 3;
			}
		}
		if (renderConformal3)
		{
			//As triangles point to indeces of vertices we have to start at the vertex index that corresponds to these elements
			int startPoint = verticesConformal1.Length + verticesConformal2.Length;
			for (int t = 0, i = startPoint; t < trianglesConformal2.Length; t += 6, i += 2)
			{

				
			}
		}

		//Concat arays and set to mesh
		int[] triangles = new int[trianglesConformal1.Length + trianglesConformal2.Length + trianglesConformal3.Length];
		trianglesConformal1.CopyTo(triangles, 0);
		trianglesConformal2.CopyTo(triangles, trianglesConformal1.Length);
		trianglesConformal3.CopyTo(triangles, trianglesConformal1.Length + trianglesConformal2.Length);

		mesh.triangles = triangles;
	}

	//Display a spline between 2 points derived with the Catmull-Rom spline algorithm
	private Vector3[] CatmullRomSpline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
	{ 
		//Initiate spline
		Vector3[] catmullRomSpline = new Vector3[pointsPerSpline ];
		//Add startpoint
		catmullRomSpline[0] = p1;

		//The start position of the line
		Vector3 lastPos = p1;		
		//Get spline points
		for (int i = 1; i < pointsPerSpline; i++)
		{
			//Which t position are we at?
			float t = (float)i / (float)pointsPerSpline;

			//Find the coordinate between the end points with a Catmull-Rom spline
			Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);

			//Add to spline list
			catmullRomSpline[i] = newPos;
			//Save this pos so we can draw the next line segment
			lastPos = newPos;
		}
		return catmullRomSpline;
	}

	//Clamp the list positions to allow looping
	int ClampListPos(int pos)
	{
		if (pos < 0)
		{
			pos = pointList.Length - 1;
		}

		if (pos > pointList.Length)
		{
			pos = 1;
		}
		else if (pos > pointList.Length - 1)
		{
			pos = 0;
		}

		return pos;
	}

	//Returns a position between 4 Vector3 with Catmull-Rom spline algorithm
	//http://www.iquilezles.org/www/articles/minispline/minispline.htm
	Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
	{
		//The coefficients of the cubic polynomial (except the 0.5f * which I added later for performance)
		Vector3 a = 2f * p1;
		Vector3 b = p2 - p0;
		Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
		Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

		//The cubic polynomial: a + b * t + c * t^2 + d * t^3
		Vector3 pos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));

		return pos;
	}

}