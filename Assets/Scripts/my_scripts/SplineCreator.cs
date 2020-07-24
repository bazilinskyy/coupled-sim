using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//Interpolation between points with a Catmull-Rom spline
[RequireComponent(typeof(MeshFilter))]
public class SplineCreator : MonoBehaviour
{
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
		UpdateControlPoints();
		CheckForChanges();
		//if number of children change --> update mesh
		if (currentChildCount != transform.childCount)
		{
			UpdateMesh();
			currentChildCount = transform.childCount;
		}

		Gizmos.color = Color.white;

		if (pointList.Length > 4)
		{
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

				DisplayCatmullRomSpline(i);
			}
		}		
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
	void DisplayCatmullRomSpline(int pos)
	{
		if (showGizmos)
		{
			Vector3 verticeA, verticeB, yDirection, sideDirection;
			yDirection = new Vector3(0, 1, 0);

			//The 4 points we need to form a spline between p1 and p2
			Vector3 p0 = pointList[ClampListPos(pos - 1)];
			Vector3 p1 = pointList[pos];
			Vector3 p2 = pointList[ClampListPos(pos + 1)];
			Vector3 p3 = pointList[ClampListPos(pos + 2)];

			//The start position of the line
			Vector3 lastPos = p1;
			Gizmos.color = Color.yellow;
			float noise_level = .5f;
			float noise = UnityEngine.Random.Range(-noise_level, noise_level);
			Vector3 p1_random = new Vector3(p1.x + noise, p1.y + noise, p1.z + noise);
			Gizmos.DrawSphere(p1, 0.35f);
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