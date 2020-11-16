using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

//Interpolation between points with a Catmull-Rom spline
[RequireComponent(typeof(NavigationHelper))]
public class SplineCreator : MonoBehaviour
{

	public RoadParameters roadParameters;

	public bool showGizmos = true;

	public Material navigationPartMaterial;
	
	private NavigationHelper navigationHelper;

	public string symbologyTag = "ConformalSymbology";
	//Holds the rendered navbigation parts;
	private GameObject navigationPartsParent;

	//Amount of points used per spline
	private int pointsPerSpline = 10;

	private Vector3[] pointsNavigationLine;
	private void Awake()
	{
		
		navigationHelper = gameObject.GetComponent<NavigationHelper>();
		MakeNavigation();
	}
	void OnDrawGizmos()
	{
		if (navigationHelper == null) {navigationHelper = gameObject.GetComponent<NavigationHelper>(); }
		Gizmos.color = Color.cyan;
		if (showGizmos)
		{
			pointsNavigationLine = MakeNavigationLine();
			//Set forward direction of splinepoints based on acquired spline
			SetForwardDirectionSplinePoints(pointsNavigationLine);

			DrawGizmoLines(pointsNavigationLine);
			//GetVerticesVirtualCable(pointsNavigationLine);
		}
	}
	public Vector3 [] GetNavigationLine()
	{
		return MakeNavigationLine();
	}
	public void MakeNavigation()
	{
		if (navigationHelper == null) { navigationHelper = gameObject.GetComponent<NavigationHelper>(); }

		//Destoy current navigation if existent
		foreach (Transform child in transform)
		{
			if(child.name == "NavigationParts")
			{
				DestroyImmediate(child.gameObject);
				break;
			}
		}

		navigationHelper.RemoveNavigationPartsFromWaypoints();
	
		//Make it again
		SetNavigationPartParents();
		CreateNavigationParts();
	}
	private void SetNavigationPartParents()
	{		
		navigationPartsParent = new GameObject("NavigationParts");
		navigationPartsParent.transform.SetParent(transform);

		var navigationTypeOptions = EnumUtil.GetValues<NavigationType>();

		foreach (NavigationType navigationType in navigationTypeOptions)
		{
			GameObject navigationTypeParent = new GameObject(navigationType.ToString());
			navigationTypeParent.transform.SetParent(navigationPartsParent.transform);
		}		
	}
	private void CreateNavigationParts()
	{
		//Get waypoint list
		
		List<Waypoint> waypointList = navigationHelper.GetOrderedWaypointList();

		//List of potetnial spline points
		List<Waypoint> currentWaypointList = new List<Waypoint>();
		bool addedSplineWaypoints = false;

		Vector3[] points;

		//loop trough points and draw gizmos based on operation of waypoints and roadParameters
		foreach (Waypoint waypoint in waypointList)
		{
			//Stop if its the endpoint or last point in the list (except when its the ending of a spline)
			if((waypoint.operation == Operation.EndPoint || waypoint == waypointList.Last()) && !addedSplineWaypoints) { break; }

			
			//If we encounter spline points we will add these to splineList untill we reach a non-spline point
			if (waypoint.operation == Operation.SplinePoint)
			{
				currentWaypointList.Add(waypoint);
				addedSplineWaypoints = true;
				continue;
			}

			if (addedSplineWaypoints) {
				currentWaypointList.Add(waypoint);
				Vector3[] points_spline = GetSplinePoints(currentWaypointList);
				Vector3[] points_current_waypoint = GetPointsWaypoint(waypoint);
				points = points_spline.Concat(points_current_waypoint).ToArray();
				currentWaypointList.Clear();
				addedSplineWaypoints = false; 
			}
			else { points = GetPointsWaypoint(waypoint); }			
			
			CreateNavigationPart(points, waypoint);
			
		}

	}
	private void CreateNavigationPart(Vector3[] points, Waypoint waypoint)
	{
		var navigationTypeOptions = EnumUtil.GetValues<NavigationType>();
		Mesh mesh;
		
		foreach (NavigationType navigationType in navigationTypeOptions)
		{
			if(navigationType == NavigationType.HUD_high || navigationType == NavigationType.HUD_low) { continue; }
			//Make gameobejct with mesh and navigationpart component
			GameObject navigationPartGameObject = new GameObject(waypoint.operation.ToString());
			navigationPartGameObject.AddComponent(typeof(MeshRenderer));
			navigationPartGameObject.AddComponent(typeof(MeshFilter));
			navigationPartGameObject.AddComponent(typeof(NavigationPart));
			navigationPartGameObject.AddComponent(typeof(MeshCollider));

			//Sets parent to appropriate gameobject
			navigationPartGameObject.transform.SetParent(navigationPartsParent.transform.Find(navigationType.ToString()));

			//Set attributes of NavigationPart Class
			NavigationPart navigationPart = navigationPartGameObject.GetComponent<NavigationPart>();
			navigationPart.waypoint = waypoint;
			navigationPart.navigationType = navigationType;

			//Set navigationPart on the waypoint to this navigationPart
			waypoint.AddNavigationPart(navigationPart);

			//Set the mesh
			navigationPartGameObject.GetComponent<MeshFilter>().mesh = mesh = new Mesh();
			navigationPartGameObject.GetComponent<MeshRenderer>().material = navigationPartMaterial;
			navigationPartGameObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			navigationPartGameObject.GetComponent<MeshRenderer>().receiveShadows = false;
			navigationPartGameObject.GetComponent<MeshCollider>().sharedMesh = mesh;

			navigationPartGameObject.gameObject.tag = symbologyTag;

			mesh.name = waypoint.operation.ToString();
			mesh.vertices = GetVertices( points, navigationType);
			mesh.triangles = GetTriangles(mesh.vertices, navigationType);
		}

	}
	private Vector3[] GetVertices(Vector3[] points, NavigationType navigationType)
	{

		if (navigationType == NavigationType.VirtualCable) { return GetVerticesVirtualCable(points); }
		else if (navigationType == NavigationType.HighlightedRoad) { return GetVerticesHighlightedRoad(points); }
		else { return new Vector3[0]; }
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
			Vector3[] circle = getCircle(roadParameters.pipeSegmentCount, roadParameters.radiusPipe);

			circle = transformCircle(circle, perpendicularToPipe, worldY, directionOfPipe, newPos);

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
	private Vector3[] GetVerticesHighlightedRoad(Vector3[] points)
	{
		Vector3[] vertices = new Vector3[points.Length * 2];

		Vector3 worldY = new Vector3(0, 1, 0);
		Vector3 directionFirstLine = Vector3.Normalize(points[1] - points[0]);
		Vector3 lastPos = points[0] - directionFirstLine;

		int vertice_cnt = 0;
		//Loop through navigation points
		for (int i = 0; i < points.Length; i++)
		{
			Vector3 newPos = points[i];

			Vector3 sideDirection = Vector3.Normalize(Vector3.Cross(newPos - lastPos, worldY));
			Vector3 verticeA = newPos + sideDirection * roadParameters.widthRoad;
			Vector3 verticeB = newPos - sideDirection * roadParameters.widthRoad;


			vertices[vertice_cnt] = verticeA;
			vertices[vertice_cnt + 1] = verticeB;
			vertice_cnt += 2;

			//Save this pos so we can draw the next line segment
			lastPos = newPos;
		}

		return vertices;
	}
	private Vector3[] GetPointsWaypoint(Waypoint waypoint)
	{
		//Never adds the next waypoints to the point list
		//So we add up to the current waypoint
		Vector3[] points = new Vector3[0];
		
		if (waypoint.operation.IsTurn()){points = GetPointsCorner(waypoint);}
        else {if (waypoint.nextWaypoint != null){	points = new Vector3[] { waypoint.transform.position, waypoint.nextWaypoint.transform.position };}	}

		return points;
	}
	private Vector3[] GetPointsCorner(Waypoint waypoint)
	{
		//get radius based on waypoint oepration and raodParameter scriptable variable
		float radius;
		/*if (waypoint.operation == Operation.TurnRightShort) { radius = roadParameters.radiusShort; }
		else { radius = roadParameters.radiusLong; }
*/
		radius = roadParameters.radiusLong;
		//Get half circle in x,y frame
		Vector2[] quarterCircle = GetQuarterCircle(radius, pointsPerSpline, waypoint.operation);
		
		//Transpose circle to the current waypoint position and rotation
		Vector3[] points = TransformVector2ToVector3(quarterCircle);
		points = TransposePoints(waypoint, points);

		points = AddNextWaypoint(waypoint, points);

		return points;

	}
	private Vector3[] AddNextWaypoint(Waypoint waypoint ,Vector3[] points)
	{
		if(waypoint.nextWaypoint == null)
		{
			throw new Exception($"Error in SplineCreator -> Add next waypoint. Next waypoint is missing......Trying again");	
		}

		//Make new array with icreased length
		Vector3[] newPoints = new Vector3[points.Length + 1];

		for(int i =0; i<points.Length; i++)
		{
			newPoints[i] = points[i];
		}
		//Add waypoint
		newPoints[points.Length] = waypoint.nextWaypoint.transform.position;
		
		return newPoints;
	}
	private int[] GetTriangles(Vector3[] vertices, NavigationType navigationType)
	{

		if (navigationType == NavigationType.VirtualCable) { return GetTrianglesVirtualCable(vertices); }
		else if (navigationType == NavigationType.HighlightedRoad) { return GetTrianglesHighlightedRoad(vertices); }
		else { return new int[0]; }
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
	private int[] GetTrianglesHighlightedRoad(Vector3[] vertices)
	{
		int[] triangles = new int[(vertices.Length - 2) * 3];

		for (int t = 0, i = 0; t < triangles.Length; t += 6, i += 2)
		{

			triangles[t] = i;
			triangles[t + 1] = triangles[t + 4] = i + 2;
			triangles[t + 2] = triangles[t + 3] = i + 1;
			triangles[t + 5] = i + 3;
		}
		return triangles;
	}
	void DrawGizmoLines(Vector3[] points)
	{
		for(int i =0; i<(points.Length-1); i++)
		{
			Gizmos.DrawLine(points[i], points[i + 1]);
		}
	}
	private Vector3[] MakeNavigationLine()
	{
		//Get waypoint list
		List<Waypoint> waypointList = GetComponent<NavigationHelper>().GetOrderedWaypointList();

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
	private Vector3[] RemoveLastElementVector3(Vector3[] array)
	{
		Vector3[] arrayOut = new Vector3[array.Length - 1];
		Array.Copy(array, 0, arrayOut, 0, arrayOut.Length);
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

		float radStep = Mathf.PI / 2 / (numberOfPoints-1);

		int sign = 1;
		if(operation.IsLeftTurn()) { sign = -1; }

		
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
	void SetForwardDirectionSplinePoints(Vector3[] splinePoints)
	{
		//Turns the forward direction of waypoints based on spline
		//Get waypoint list
		List<Waypoint> waypoints = navigationHelper.GetOrderedWaypointList();
		
		bool lastPointWasSpline = false;
		Waypoint lastWaypoint = null;
		foreach ( Waypoint waypoint in waypoints)
		{
			if (waypoint.operation == Operation.SplinePoint)
			{
				lastWaypoint = waypoint;
				//Set forward direction of waypoint
				int index = Array.FindIndex(splinePoints, element => (element == waypoint.transform.position));
				//If non valid index found we skip
				if (!(index >= 0)) { continue;}
				

				Vector3 forward = splinePoints[index + 2] - splinePoints[index];
				waypoint.transform.forward = forward.normalized;

				//First spline point we dont change forward direction
				if (!lastPointWasSpline) { lastPointWasSpline = true; continue; }

			}
			else if (lastPointWasSpline && waypoint.operation == Operation.None)
			{
				//This is the waypoint which the last spline point is connected to
				//Set forward direction of waypoint
				waypoint.transform.forward = lastWaypoint.transform.forward;
				lastPointWasSpline = false;
			}
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
	private Vector3[] getCircle(int SegmentCount, float radius)
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
	private Vector3[] CatmullRomSpline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
	{ 
		//Initiate spline
		Vector3[] catmullRomSpline = new Vector3[pointsPerSpline];
		//Add startpoint
		catmullRomSpline[0] = p1;	
		//Get spline points
		for (int i = 1; i < pointsPerSpline; i++)
		{
			//Which t position are we at?
			float t = (float)i / (float)pointsPerSpline;

			//Find the coordinate between the end points with a Catmull-Rom spline
			Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);

			//Add to spline list
			catmullRomSpline[i] = newPos;
		}
		return catmullRomSpline;
	}
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