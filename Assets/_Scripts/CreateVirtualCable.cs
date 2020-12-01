using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class CreateVirtualCable : MonoBehaviour
{
	public RoadParameters roadParameters;
	public Material navigationPartMaterial;
	private GameObject navigationSymbology;
	int pointsPerCorner = 30;
    Waypoints waypoints;
    private void Awake()
    {

		MakeNavigationObject();
	}

	private void MakeNavigationObject()
    {
		navigationSymbology = new GameObject("VirtualCable");
		//Make gameobejct with mesh and navigationpart component		
		navigationSymbology.AddComponent(typeof(MeshRenderer));
		navigationSymbology.AddComponent(typeof(MeshFilter));
		navigationSymbology.AddComponent(typeof(MeshCollider));

		navigationSymbology.GetComponent<MeshRenderer>().material = navigationPartMaterial;
		navigationSymbology.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		navigationSymbology.GetComponent<MeshRenderer>().receiveShadows = false;

	}
	List<Vector3> GetPointsCrossing(CrossComponents crossing)
    {
		waypoints = crossing.waypoints;
		List<Vector3> points = new List<Vector3>();

		points.Add(waypoints.startPoint.position);
		
		if (crossing.turn1 == TurnType.None) { Debug.Log("Got None turn type..."); return points;  }
		if (crossing.turn1 == TurnType.EndPoint)
		{
			points.Add(waypoints.waypoint1.position - 30f * waypoints.waypoint1.forward);
			return points;
		}
		else if (crossing.turn1 != TurnType.Straight)
		{
			foreach (Vector3 point in GetPointsCorner(waypoints.waypoint1, crossing.turn1)) { points.Add(point); }
		}

		if (crossing.turn2 == TurnType.None) { Debug.Log("Got None turn type..."); return points; }
		if (crossing.turn2 == TurnType.EndPoint) { points.Add(waypoints.waypoint2.position - 30f * waypoints.waypoint2.forward); }
		else if (crossing.turn2 != TurnType.Straight)
		{
			foreach (Vector3 point in GetPointsCorner(waypoints.waypoint2, crossing.turn2)) { points.Add(point); }

		}
		return points;
	}
    public void MakeVirtualCable(Crossings crossings)
    {

		List<Vector3> points = GetPointsCrossing(crossings.currentCrossing.GetComponent<CrossComponents>());
		if (crossings.nextCrossing != null) { points = points.Concat(GetPointsCrossing(crossings.nextCrossing.GetComponent<CrossComponents>())).ToList(); }

		Debug.Log($"MAking navigation with {points.Count()} points...");
		CreateNavigationPart(points);
    }
	private void CreateNavigationPart(List<Vector3> points)
	{
		Vector3[] pointArray = points.ToArray();
		Mesh mesh;

		

		//Set the mesh
		navigationSymbology.GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		navigationSymbology.GetComponent<MeshCollider>().sharedMesh = mesh;

		navigationSymbology.tag = "ConformalSymbology";

		mesh.name = "VirtualCable";
		mesh.vertices = GetVerticesVirtualCable(pointArray);
		mesh.triangles = GetTrianglesVirtualCable(mesh.vertices);
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
