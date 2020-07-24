
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

//Interpolation between points with a Catmull-Rom spline
[RequireComponent(typeof(MeshFilter))]
public class CatmullRomSpline : MonoBehaviour
{
	//Has to be at least 4 points
	public Transform[] pointList;
	private Vector3[] previousPointListPosition;
	public bool showGizmos = true;
	public bool standAlone = true;
	public float curveRadius = 2;


	private Mesh mesh;
	private Vector3[] vertices;
	private int[] triangles;


	public float navWidth;
	[Range(0.01f, 0.2f)]    //Make sure it's is adding up to 1, so 0.3 will give a gap, but 0.2 will work
	public float resolution = 0.2f;
	private float my_resolution = 0.2f;


	public delegate void OnVariableChangeDelegate(float newVal);
	public event OnVariableChangeDelegate OnVariableChange;

	public bool isLooping = false;

	private int currentChildCount = 0;


	private void Awake()
	{
		UpdateMesh();
		UpdateControlPoints();
		CopyPointListArray();

	}

	private void Update()
	{
		if (resolution != my_resolution)
		{
			my_resolution = resolution;
			Debug.Log("Should update mesh....");
			UpdateMesh();
		}

		if (CheckPointPositions())
		{
			UpdateMesh();
			CopyPointListArray();

		}
	}

	void UpdateMesh()
	{
		GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		mesh.name = "Pipe";
		SetVertices();
		SetTriangles();
		mesh.RecalculateNormals();
	}
	//Display without having to press play
	void OnDrawGizmos()
	{

		CheckChildren();


		Gizmos.color = Color.white;

		if (pointList.Length > 4)
		{


			//Draw the Catmull-Rom spline between the points
			for (int i = 0; i < pointList.Length; i++)
			{
				//Cant draw between the endpoints
				//Neither do we need to draw from the second to the last endpoint
				//...if we are not making a looping line
				if ((i == 0 || i == pointList.Length - 2 || i == pointList.Length - 1) && !isLooping)
				{
					continue;
				}

				DisplayCatmullRomSpline(i);

			}
		}
	}
	void CopyPointListArray()
	{
		previousPointListPosition = new Vector3[pointList.Length];
		for (int i = 0; i < pointList.Length; i++)
		{
			if (pointList[i] != null)
			{
				previousPointListPosition[i] = new Vector3(pointList[i].position.x, pointList[i].position.y, pointList[i].position.z);
			}
		}

	}
	private bool CheckPointPositions()
	{
		bool changed = false;
		for (int i = 0; i < pointList.Length; i++)
		{
			if (i >= previousPointListPosition.Length)
			{
				Debug.Log("Length of pointLists changed!");
				changed = true;
				break;
			}
			else if (pointList[i].position != previousPointListPosition[i])
			{
				Debug.Log("Position of point changed!");
				changed = true;
				break;
			}
			else
			{
				Vector3 p1 = pointList[i].position;
				Vector3 p2 = previousPointListPosition[i];
				//Debug.Log("Point " + i + "'s outputs are the same: [" + p1.x + ", " + p1.y + ", " + p1.z + "] == [" + p2.x + ", " + p2.y + ", " + p2.z + "] ??");
			}
		}

		return changed;
	}

	void CheckChildren()
	{

		if (transform.childCount != currentChildCount)
		{
			UpdateControlPoints();
			currentChildCount = transform.childCount;
		}
	}

	//Populate controlpoint list with children form waypointROot
	void UpdateControlPoints()

	{
		int childCount = transform.childCount;
		//Debug.Log("Updating pointList");


		pointList = new Transform[childCount];

		List<NavWaypoint> waypointOrderList = new List<NavWaypoint>();
		foreach (Transform child in transform)
		{
			NavWaypoint waypoint = child.gameObject.GetComponent<NavWaypoint>();
			waypointOrderList.Add(waypoint);

		}
		List<NavWaypoint> sortedWaypointOrderList = waypointOrderList.OrderBy(d => d.orderId).ToList();

		int i = 0;
		foreach (NavWaypoint waypoint in sortedWaypointOrderList)
		{
			pointList[i] = waypoint.transform;
			i++;
		}

	}
	void SetVertices()
	{

		int loops = Mathf.FloorToInt(1f / resolution);

		vertices = new Vector3[loops * (pointList.Length - 3) * 2];
		Vector3 verticeA, verticeB, yDirection, sideDirection, basePosition;
		yDirection = new Vector3(0, 1, 0);
		int vertice_cnt = 0;

		basePosition = -transform.position;

		//Draw the Catmull-Rom spline between the points
		for (int i = 0; i < pointList.Length; i++)
		{
			//Cant draw between the endpoints
			//Neither do we need to draw from the second to the last endpoint
			//...if we are not making a looping line
			if ((i == 0 || i == pointList.Length - 2 || i == pointList.Length - 1) && !isLooping)
			{
				continue;
			}

			//The 4 points we need to form a spline between p1 and p2
			Vector3 p0 = pointList[ClampListPos(i - 1)].position;
			Vector3 p1 = pointList[i].position;
			Vector3 p2 = pointList[ClampListPos(i + 1)].position;
			Vector3 p3 = pointList[ClampListPos(i + 2)].position;

			//The start position of the line
			Vector3 lastPos = p1;

			for (int k = 1; k <= loops; k++)
			{
				//Which t position are we at?
				float t = k * resolution;

				//Find the coordinate between the end points with a Catmull-Rom spline
				Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);


				sideDirection = Vector3.Normalize(Vector3.Cross(newPos - lastPos, yDirection));
				verticeA = lastPos + sideDirection * navWidth;
				verticeB = lastPos - sideDirection * navWidth;

				vertices[vertice_cnt] = verticeA + basePosition;
				vertices[vertice_cnt + 1] = verticeB + basePosition;
				vertice_cnt += 2;


				//Save this pos so we can draw the next line segment
				lastPos = newPos;
			}
		}

		Debug.Log("NUmber of vertices = " + vertices.Length);
		
		mesh.vertices = vertices;
	}

	void SetTriangles()
	{
		int loops = Mathf.FloorToInt(1f / resolution);

		triangles = new int[(vertices.Length - 2) / 2 * 6];

		for (int t = 0, i = 0; t < triangles.Length; t += 6, i += 2)
		{
			triangles[t] = i;
			triangles[t + 1] = triangles[t + 4] = i + 1;
			triangles[t + 2] = triangles[t + 3] = i + 2;
			triangles[t + 5] = i + 3;

		}
		Debug.Log("NUmber of triangles indices = " + triangles.Length);

		mesh.triangles = triangles;

	}

	//Display a spline between 2 points derived with the Catmull-Rom spline algorithm
	void DisplayCatmullRomSpline(int pos)
	{
		if (showGizmos)
		{
			Vector3 verticeA, verticeB, yDirection, sideDirection;
			yDirection = new Vector3(0, 1, 0);

			//The 4 points we need to form a spline between p1 and p2
			Vector3 p0 = pointList[ClampListPos(pos - 1)].position;
			Vector3 p1 = pointList[pos].position;
			Vector3 p2 = pointList[ClampListPos(pos + 1)].position;
			Vector3 p3 = pointList[ClampListPos(pos + 2)].position;

			//The start position of the line
			Vector3 lastPos = p1;

			//The spline's resolution


			//How many times should we loop?
			int loops = Mathf.FloorToInt(1f / resolution);

			for (int i = 1; i <= loops; i++)
			{
				//Which t position are we at?
				float t = i * resolution;

				//Find the coordinate between the end points with a Catmull-Rom spline
				Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);

				sideDirection = Vector3.Normalize(Vector3.Cross(newPos - lastPos, yDirection));
				verticeA = lastPos + sideDirection * navWidth;
				verticeB = lastPos - sideDirection * navWidth;


				{
					Gizmos.color = Color.red;
					Gizmos.DrawSphere(verticeA, 0.1f);
				}
				{
					Gizmos.color = Color.green;
					Gizmos.DrawSphere(verticeB, 0.1f);
				}

				//Draw this line segment
				Gizmos.DrawLine(lastPos, newPos);

				//Save this pos so we can draw the next line segment
				lastPos = newPos;
			}
		}
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


//using UnityEngine;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System;

////Interpolation between points with a Catmull-Rom spline
//[RequireComponent(typeof(MeshFilter))]
//public class CatmullRomSpline : MonoBehaviour
//{
//	//Has to be at least 4 points
//	public Transform[] pointList;
//	public Vector3[] previousPointListPosition;


//	private Mesh mesh;
//	private Vector3[] vertices;
//	private int[] triangles;


//	public float navWidth;
//	[Range(0.01f, 0.2f)]    //Make sure it's is adding up to 1, so 0.3 will give a gap, but 0.2 will work
//	public float resolution = 0.2f;
//	public bool isLooping = false;


//	public GameObject navPrefab;
//	public GameObject navParent;
//	private List<List<Transform>> planeList;
//	private float errorMargin = 0.1f;
//	private int planeNumber = 0;

//	private int currentChildCount = 0;

//void CopyPointListArray()
//{
//	previousPointListPosition = new Vector3[pointList.Length];
//	for (int i = 0; i < pointList.Length; i++)
//	{
//		if (pointList[i] != null)
//		{
//			previousPointListPosition[i] = new Vector3(pointList[i].position.x, pointList[i].position.y, pointList[i].position.z);
//		}
//	}


//}
//	void CheckChildren()
//	{

//		if (transform.childCount != currentChildCount)
//		{

//			Debug.Log("Number of points changed!");
//			CopyPointListArray();
//			UpdateControlPoints();
//			currentChildCount = transform.childCount;
//			UpdateNavObjects();

//		}
//	}

//	private bool CheckPointPositions()
//	{
//		bool changed = false;
//		for (int i = 0; i<pointList.Length; i++)
//		{
//			if( i>= previousPointListPosition.Length)
//			{
//				Debug.Log("Length of pointLists changed!");
//				changed = true;
//				break;
//			}
//			else if(pointList[i].position != previousPointListPosition[i])
//			{
//				Debug.Log("Position of point changed!");
//				changed = true;
//				break;
//			}
//			else
//			{
//				Vector3 p1 = pointList[i].position;
//				Vector3 p2 = previousPointListPosition[i];
//				//Debug.Log("Point " + i + "'s outputs are the same: [" + p1.x + ", " + p1.y + ", " + p1.z + "] == [" + p2.x + ", " + p2.y + ", " + p2.z + "] ??");
//			}
//		}

//		return changed;
//	}
//	//Populate controlpoint list with children form waypointROot
//	void UpdateControlPoints()

//	{
//		int childCount = transform.childCount;
//		//Debug.Log("Updating pointList");


//		pointList = new Transform[childCount];

//		List<NavWaypoint> waypointOrderList = new List<NavWaypoint>();
//		foreach (Transform child in transform)
//		{
//			NavWaypoint waypoint = child.gameObject.GetComponent<NavWaypoint>();
//			waypointOrderList.Add(waypoint);

//		}
//		List<NavWaypoint> sortedWaypointOrderList = waypointOrderList.OrderBy(d => d.orderId).ToList();

//		int i = 0 ;
//		foreach( NavWaypoint waypoint in sortedWaypointOrderList)
//		{
//			pointList[i] = waypoint.transform;
//			i++;
//		}

//	}

//	private void Awake()
//	{
//		SetVertices();
//		SetTriangles();
//	}
//	//Display without having to press play
//	void OnDrawGizmos()
//	{

//		CheckChildren();

//		if (CheckPointPositions())
//		{
//			UpdateNavObjects();
//			CopyPointListArray();
//		}



//		Gizmos.color = Color.white;

//		if (pointList.Length > 4)
//		{


//			//Draw the Catmull-Rom spline between the points
//			for (int i = 0; i < pointList.Length; i++)
//			{
//				//Cant draw between the endpoints
//				//Neither do we need to draw from the second to the last endpoint
//				//...if we are not making a looping line
//				if ((i == 0 || i == pointList.Length - 2 || i == pointList.Length - 1) && !isLooping)
//				{
//					continue;
//				}

//				DisplayCatmullRomSpline(i);

//			}
//		}


//		void SetVertices()
//		{

//			int loops = Mathf.FloorToInt(1f / resolution);

//			vertices = new Vector3[loops * pointList.Length *2];
//			Vector3 verticeA, verticeB, yDirection, sideDirection;
//			yDirection = new Vector3(0, 1, 0);
//			int vertice_cnt = 0;

//			//Draw the Catmull-Rom spline between the points
//			for (int i = 0; i < pointList.Length; i++)
//			{
//				//Cant draw between the endpoints
//				//Neither do we need to draw from the second to the last endpoint
//				//...if we are not making a looping line
//				if ((i == 0 || i == pointList.Length - 2 || i == pointList.Length - 1) && !isLooping)
//				{
//					continue;
//				}

//				//The 4 points we need to form a spline between p1 and p2
//				Vector3 p0 = pointList[ClampListPos(i - 1)].position;
//				Vector3 p1 = pointList[i].position;
//				Vector3 p2 = pointList[ClampListPos(i + 1)].position;
//				Vector3 p3 = pointList[ClampListPos(i + 2)].position;

//				//The start position of the line
//				Vector3 lastPos = p1;

//				for (int k = 1; k <= loops; k++)
//				{
//					//Which t position are we at?
//					float t = k * resolution;

//					//Find the coordinate between the end points with a Catmull-Rom spline
//					Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);


//					sideDirection = Vector3.Normalize(Vector3.Cross(newPos - lastPos, yDirection));
//					verticeA = lastPos + sideDirection * navWidth;
//					verticeB = lastPos - sideDirection * navWidth;

//					vertices[vertice_cnt] = verticeA;
//					vertices[vertice_cnt + 1] = verticeB;
//					vertice_cnt += 2;
//					{
//						Gizmos.color = Color.red;
//						Gizmos.DrawSphere(verticeA, 0.1f);
//					}
//					{
//						Gizmos.color = Color.green;
//						Gizmos.DrawSphere(verticeB, 0.1f);
//					}

//					//Save this pos so we can draw the next line segment
//					lastPos = newPos;
//				}
//			}
//		}

//		mesh.vertices = vertices;
//	}

//	void SetTriangles() {
//		int loops = Mathf.FloorToInt(1f / resolution);

//		triangles = new int[loops * pointList.Length * 6];

//		for( int i =0; i < loops * pointList.Length; i+=6)
//		{
//			triangles[i] = triangles[i+3] = i;
//			triangles[i+1] = i+2;
//			triangles[i + 2] = triangles[i + 4] = i + 3;
//			triangles[i + 5] = i + 2;

//		}
//		mesh.triangles = triangles;

//	}

//	//Display a spline between 2 points derived with the Catmull-Rom spline algorithm
//	void DisplayCatmullRomSpline(int pos)
//	{


//		//The 4 points we need to form a spline between p1 and p2
//		Vector3 p0 = pointList[ClampListPos(pos - 1)].position;
//		Vector3 p1 = pointList[pos].position;
//		Vector3 p2 = pointList[ClampListPos(pos + 1)].position;
//		Vector3 p3 = pointList[ClampListPos(pos + 2)].position;

//		//The start position of the line
//		Vector3 lastPos = p1;

//		//The spline's resolution


//		//How many times should we loop?
//		int loops = Mathf.FloorToInt(1f / resolution);

//		for (int i = 1; i <= loops; i++)
//		{
//			//Which t position are we at?
//			float t = i * resolution;

//			//Find the coordinate between the end points with a Catmull-Rom spline
//			Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);


//			//Draw this line segment
//			Gizmos.DrawLine(lastPos, newPos);

//			//Save this pos so we can draw the next line segment
//			lastPos = newPos;
//		}
//	}

//	//Clamp the list positions to allow looping
//	int ClampListPos(int pos)
//	{
//		if (pos < 0)
//		{
//			pos = pointList.Length - 1;
//		}

//		if (pos > pointList.Length)
//		{
//			pos = 1;
//		}
//		else if (pos > pointList.Length - 1)
//		{
//			pos = 0;
//		}

//		return pos;
//	}

//	//Returns a position between 4 Vector3 with Catmull-Rom spline algorithm
//	//http://www.iquilezles.org/www/articles/minispline/minispline.htm
//	Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
//	{
//		//The coefficients of the cubic polynomial (except the 0.5f * which I added later for performance)
//		Vector3 a = 2f * p1;
//		Vector3 b = p2 - p0;
//		Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
//		Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

//		//The cubic polynomial: a + b * t + c * t^2 + d * t^3
//		Vector3 pos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));

//		return pos;
//	}


//	void UpdateNavObjects()
//	{
//		//if (pointList.Length > 4)
//		//{
//		//	bool waypointChanged = false;

//		//	//Draw the Catmull-Rom spline between the points
//		//	for (int i = 0; i < pointList.Length; i++)
//		//	{
//		//		//Cant draw between the endpoints
//		//		//Neither do we need to draw from the second to the last endpoint
//		//		//...if we are not making a looping line
//		//		if ((i == 0 || i == pointList.Length - 2 || i == pointList.Length - 1) && !isLooping)
//		//		{
//		//			continue;
//		//		}

//		//		//If have been added to the list
//		//		//If the waypoints changed then, from this point in the line,  we redo all planes
//		//		if (WayPointChanged(i))
//		//		{
//		//			//Debug.Log("Found change from waypoint" + i + " onwards!");
//		//			waypointChanged = true;
//		//		}
//		//		if (waypointChanged)
//		//		{
//		//			//Debug.Log("Creating new planes for waypoint " + i );
//		//			CreateNavObject(i);
//		//		}


//		//	}
//		//}
//	}

//	//private bool WayPointChanged( int i)
//	//{
//	//	//Gives a true if this, or the coming two waypoints have changed
//	//	bool waypointChanged = false;

//	//	if( i+2 >= previousPointListPosition.Length)
//	//	{
//	//		waypointChanged = true;
//	//	}
//	//	else if(pointList[i].position != previousPointListPosition[i] || pointList[i+1].position != previousPointListPosition[i+1] || pointList[i+2].position != previousPointListPosition[i + 2])
//	//	{
//	//		waypointChanged = true;
//	//	}
//	//	return waypointChanged;
//	//}
//	//private bool IsStraight(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
//	//{
//	//	//Checks if differences in z fall within error margin or differences in x fal within error margin

//	//	bool IsStraight = false;
//	//	float delta_x_1 = p1.x - p0.x; float delta_z_1 = p1.z - p0.z;
//	//	float delta_x_2 = p2.x - p1.x; float delta_z_2 = p2.z - p1.z;
//	//	float delta_x_3 = p3.x - p2.x; float delta_z_3 = p3.z - p2.z;


//	//	if ( Mathf.Abs(delta_x_1) < errorMargin && Mathf.Abs(delta_x_2) < errorMargin && Mathf.Abs(delta_x_3) < errorMargin)
//	//	{
//	//		IsStraight = true;
//	//	}
//	//	if (Mathf.Abs(delta_z_1) < errorMargin && Mathf.Abs(delta_z_2) < errorMargin && Mathf.Abs(delta_z_3) < errorMargin)
//	//	{
//	//		IsStraight = true;
//	//	}

//	//	return IsStraight;
//	//}
//	//void CreateNavObject(int pos)
//	//{

//	//	//The 4 points we need to form a spline between p1 and p2
//	//	Vector3 p0 = pointList[ClampListPos(pos - 1)].position;
//	//	Vector3 p1 = pointList[pos].position;
//	//	Vector3 p2 = pointList[ClampListPos(pos + 1)].position;
//	//	Vector3 p3 = pointList[ClampListPos(pos + 2)].position;

//	//	//The start position of the line
//	//	Vector3 lastPos = p1;

//	//	//The spline's resolution


//	//	//How many times should we loop?
//	//	if (IsStraight(p0, p1, p2, p3))
//	//	{
//	//		CreateStraight(pos, p1, p2);
//	//	}
//	//	else {
//	//		CreateBend(pos, p0, p1, p2, p3);	
//	//	}	
//	//}

//	//void CreateBend(int index, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
//	//{
//	//	Debug.Log("Creating Bend for point " + index);
//	//	/*int loops = Mathf.FloorToInt(1f / resolution);
//	//	Vector3 lastPos = p1;
//	//	for (int i = 1; i <= loops; i++)
//	//	{
//	//		//Which t position are we at?
//	//		float t = i * resolution;

//	//		//Find the coordinate between the end points with a Catmull-Rom spline
//	//		Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);

//	//		//Draw this line segment


//	//		GameObject newPlane = Instantiate(navPrefab, navParent.transform);
//	//		newPlane.transform.position = newPos;
//	//		//newPlane.transform.rotation = newPos;
//	//		Vector3 scale = new Vector3(0.1f, 0.1f, 1f);
//	//		newPlane.transform.localScale = scale;

//	//		planeNumber++;

//	//		//Save this pos so we can draw the next line segment
//	//		lastPos = newPos;
//	//	}*/
//	//}

//	//void CreateStraight(int index, Vector3 p1, Vector3 p2)
//	//{
//	//	Debug.Log("Creating straight for point " + index);

//	//	//GameObject newPlane = Instantiate(navPrefab, navParent.transform);

//	//	////find the vector pointing from our position to the target
//	//	//Vector3 direction = (p2 - p1).normalized;
//	//	//float length = (p2 - p1).magnitude;
//	//	////create the rotation we need to be in to look at the target
//	//	//Quaternion lookRotation = Quaternion.LookRotation(direction);

//	//	//newPlane.transform.position = p1;
//	//	//newPlane.transform.rotation = lookRotation;


//	//	//newPlane.transform.localScale.z = length
//	//	//rotate us over time according to speed until we are in the required rotation

//	//}



//}

