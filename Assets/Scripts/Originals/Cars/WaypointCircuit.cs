using System;
using System.Collections;
using UnityEngine;
using System.Text;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityStandardAssets.Utility
{
    public class WaypointCircuit : MonoBehaviour
    {
        public WaypointList waypointList = new WaypointList();
        public bool smoothRoute = true;
        private int numPoints;
        private Vector3[] points;
        private float[] distances;

        public float editorVisualisationSubsteps = 100;
        public float Length { get; private set; }

        public Transform[] Waypoints
        {
            get { return waypointList.items; }
        }

        // Use this for initialization
        private void Awake()
        {
            if (Waypoints.Length > 1)
            {
                CachePositionsAndDistances();
            }
            numPoints = Waypoints.Length;
        }


        public RoutePoint GetRoutePoint(float dist)
        {
            // position and direction
            Vector3 p1 = GetRoutePosition(dist);
            Vector3 p2 = GetRoutePosition(dist + 0.1f);
            Vector3 delta = p2 - p1;
            return new RoutePoint(p1, delta.normalized);
        }


        public Vector3 GetRoutePosition(float dist)
        {
            int point = 0;

            if (Length == 0)
            {
                Length = distances[distances.Length - 1];
            }

            dist = Mathf.Repeat(dist, Length);

            while (distances[point] < dist)
            {
                ++point;
            }


            // get nearest two points, ensuring points wrap-around start & end of circuit
            var p1n = ((point - 1) + numPoints)%numPoints;

            // found point numbers, now find interpolation value between the two middle points

            var i = Mathf.InverseLerp(distances[p1n], distances[point], dist);

            if (smoothRoute)
            {
                // smooth catmull-rom calculation between the two relevant points


                // get indices for the surrounding 2 points, because
                // four points are required by the catmull-rom function
                var p0n = ((point - 2) + numPoints)%numPoints;

                // 2nd point may have been the 'last' point - a dupe of the first,
                // (to give a value of max track distance instead of zero)
                // but now it must be wrapped back to zero if that was the case.
                var p2n = point%numPoints;

                var p3n = (point + 1)%numPoints;

                var P0 = points[p0n];
                var P1 = points[p1n];
                var P2 = points[p2n];
                var P3 = points[p3n];

                return CatmullRom(P0, P1, P2, P3, i);
            }
            else
            {
                // simple linear lerp between the two points:
                return Vector3.Lerp(points[p1n], points[point], i);
            }
        }


        private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float i)
        {
            // comments are no use here... it's the catmull-rom equation.
            // Un-magic this, lord vector!
            return 0.5f *
                   ((2 * p1) + (-p0 + p2) * i + (2 * p0 - 5 * p1 + 4 * p2 - p3) * i * i +
                    (-p0 + 3 * p1 - 3 * p2 + p3) * i * i * i);
        }


        private void CachePositionsAndDistances()
        {
            // transfer the position of each point and distances between points to arrays for
            // speed of lookup at runtime
            points = new Vector3[Waypoints.Length + 1];
            distances = new float[Waypoints.Length + 1];

            float accumulateDistance = 0;
            for (int i = 0; i < points.Length; ++i)
            {
                var t1 = Waypoints[(i) % Waypoints.Length];
                var t2 = Waypoints[(i + 1) % Waypoints.Length];
                if (t1 != null && t2 != null)
                {
                    Vector3 p1 = t1.position;
                    Vector3 p2 = t2.position;
                    points[i] = Waypoints[i % Waypoints.Length].position;
                    distances[i] = accumulateDistance;
                    accumulateDistance += (p1 - p2).magnitude;
                }
            }
        }


        private void OnDrawGizmos()
        {
            DrawGizmos(false);
        }


        private void OnDrawGizmosSelected()
        {
            DrawGizmos(true);
        }


        private void DrawGizmos(bool selected)
        {
            return;
            // waypointList.circuit = this;
            // if (Waypoints.Length > 1)
            // {
            //     numPoints = Waypoints.Length;

            //     CachePositionsAndDistances();
            //     Length = distances[distances.Length - 1];

            //     Gizmos.color = selected ? Color.yellow : new Color(1, 1, 0, 0.5f);
            //     Vector3 prev = Waypoints[0].position;
            //     if (smoothRoute)
            //     {
            //         for (float dist = 0; dist < Length; dist += Length / editorVisualisationSubsteps)
            //         {
            //             Vector3 next = GetRoutePosition(dist + 1);
            //             Gizmos.DrawLine(prev, next);
            //             prev = next;
            //         }
            //         Gizmos.DrawLine(prev, Waypoints[0].position);
            //     }
            //     else
            //     {
            //         for (int n = 0; n < Waypoints.Length; ++n)
            //         {
            //             Vector3 next = Waypoints[(n + 1) % Waypoints.Length].position;
            //             Gizmos.DrawLine(prev, next);
            //             prev = next;
            //         }
            //     }
            // }
        }


        [Serializable]
        public class WaypointList
        {
            public WaypointCircuit circuit;
            public Transform[] items = new Transform[0];
        }

        public struct RoutePoint
        {
            public Vector3 position;
            public Vector3 direction;


            public RoutePoint(Vector3 position, Vector3 direction)
            {
                this.position = position;
                this.direction = direction;
            }
        }
    }
}

#if UNITY_EDITOR
namespace UnityStandardAssets.Utility.Inspector
{
    [CustomEditor(typeof(WaypointCircuit))]
    public class WaypointCircuitEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var waypointList = serializedObject.FindProperty("waypointList");
            var items = waypointList.FindPropertyRelative("items");
            if (GUILayout.Button("Import from file"))
            {
                ImportFromFile(items.serializedObject);
            }
            if (GUILayout.Button("Export to file"))
            {
                ExportToFile(items.serializedObject);
            }
        }

        void ExportToFile(SerializedObject circuitObject)
        {
            var path = EditorUtility.SaveFilePanel("Export to", "./", "waypoints", "csv");
            if (string.IsNullOrWhiteSpace(path)) return;
            var circuit = circuitObject.targetObject as WaypointCircuit;
            StringBuilder sb = new StringBuilder();
            sb.Append($"Smooth: {circuit.smoothRoute}\n");
            sb.Append($"name;tag;layer;x;y;z;rotX;rotY;rotZ;scaleX;scaleY;scaleZ;" +
                $"waypointType;speed;acceleration;blinkerState;jerk;causeToYield;lookAtPlayerWhileYielding;lookAtPlayerAfterYielding;yieldTime;brakingAcceleration;lookAtPedFromSeconds;lookAtPedToSeconds;customBehaviourDataString;" +
                $"collider_enabled;isTrigger;centerX;centerY;centerZ;sizeX;sizeY;sizeZ" +
                $"\n");
            foreach (var wp in circuit.Waypoints)
            {
                var go = wp.gameObject;
                sb.Append($"{go.name};{go.tag};{go.layer};{wp.position.x};{wp.position.y};{wp.position.z};{wp.rotation.eulerAngles.x};{wp.rotation.eulerAngles.y};{wp.rotation.eulerAngles.z};{wp.localScale.x};{wp.localScale.y};{wp.localScale.z}");
                var s = wp.GetComponent<SpeedSettings>();
                if (s != null)
                {
                    sb.Append($";{(int)s.Type};{s.speed};{s.acceleration};{(int)s.BlinkerState};{s.jerk};{s.causeToYield};{s.EyeContactWhileYielding};{s.EyeContactAfterYielding};{s.yieldTime};{s.brakingAcceleration};{s.YieldingEyeContactSince};{s.YieldingEyeContactUntil};{s.GetCustomBehaviourDataString()}");
                }
                else
                {
                    sb.Append($";;;;;;;;;;;;;");
                }

                var b = wp.GetComponent<BoxCollider>();
                if (b != null)
                {
                    sb.Append($";{b.enabled};{b.isTrigger};{b.center.x};{b.center.y};{b.center.z};{b.size.x};{b.size.y};{b.size.z}");
                }
                else
                {
                    sb.Append($";;;;;;;;");
                }

                sb.Append("\n");
            }
            File.WriteAllText(path, sb.ToString());
        }

        void ImportFromFile(SerializedObject circuitObject)
        {
            var path = EditorUtility.OpenFilePanel("Import from", "./", "csv");
            if (string.IsNullOrWhiteSpace(path)) return;
            var lines = File.ReadAllLines(path);
            circuitObject.FindProperty(nameof(WaypointCircuit.smoothRoute)).boolValue = lines[0].Contains("True");

            var wpList = circuitObject.FindProperty(nameof(WaypointCircuit.waypointList));
            var wpts = wpList.FindPropertyRelative(nameof(WaypointCircuit.WaypointList.items));
            for (int i = 0; i < wpts.arraySize; i++)
            {
                var obj = wpts.GetArrayElementAtIndex(i);
                GameObject.DestroyImmediate((obj.objectReferenceValue as Transform).gameObject);
            }
            wpts.ClearArray();

            for (int i = 2; i < lines.Length; i++)
            {
                var line = lines[i].Split(';');
                var idx = i - 2;
                wpts.InsertArrayElementAtIndex(idx);
                var wp = new GameObject($"Waypoint {idx}", typeof(SpeedSettings), typeof(BoxCollider));
                wpts.GetArrayElementAtIndex(idx).objectReferenceValue = wp.transform;
                wp.transform.SetParent((circuitObject.targetObject as WaypointCircuit).transform);
                int lineCursor = 0;

                bool DeserializeFloat(out float f, float defaultValue = 0)
                {
                    bool res = false;
                    f = defaultValue;
                    if (line.Length > lineCursor && !string.IsNullOrWhiteSpace(line[lineCursor]))
                    {
                        res = float.TryParse(line[lineCursor], out f);
                    }
                    lineCursor++;
                    return res;
                }

                bool DeserializeBool(out bool b, bool defaultValue = false)
                {
                    bool res = false;
                    b = defaultValue;
                    if (line.Length > lineCursor && !string.IsNullOrWhiteSpace(line[lineCursor]))
                    {
                        res = bool.TryParse(line[lineCursor], out b);
                    }
                    lineCursor++;
                    return res;
                }

                bool DeserializeInt(out int b, int defaultValue = 0)
                {
                    bool res = false;
                    b = defaultValue;
                    if (line.Length > lineCursor && !string.IsNullOrWhiteSpace(line[lineCursor]))
                    {
                        res = int.TryParse(line[lineCursor], out b);
                    }
                    lineCursor++;
                    return res;
                }

                bool DeserializeString(out string b, string defaultValue = "")
                {
                    bool res = false;
                    b = defaultValue;
                    if (line.Length > lineCursor && !string.IsNullOrWhiteSpace(line[lineCursor]))
                    {
                        b = line[lineCursor];
                        res = true;
                    }
                    lineCursor++;
                    return res;
                }

                // position
                DeserializeString(out var name);
                wp.gameObject.name = name;
                DeserializeString(out var tag);
                wp.gameObject.tag = tag;
                DeserializeInt(out var layer);
                wp.gameObject.layer = layer;

                Vector3 position = default;
                if (DeserializeFloat(out position.x) && DeserializeFloat(out position.y) && DeserializeFloat(out position.z))
                {
                    wp.transform.position = position;
                }
                Vector3 rotation = default;
                if (DeserializeFloat(out rotation.x) && DeserializeFloat(out rotation.y) && DeserializeFloat(out rotation.z))
                {
                    wp.transform.rotation = Quaternion.Euler(rotation);
                }
                Vector3 scale = default;
                if (DeserializeFloat(out scale.x) && DeserializeFloat(out scale.y) && DeserializeFloat(out scale.z))
                {
                    wp.transform.localScale = scale;
                }

                var speedSettings = wp.GetComponent<SpeedSettings>();

                DeserializeInt(out int type, SpeedSettings.Defaults.WaypointType);
                speedSettings.Type = (SpeedSettings.WaypointType)type;
                DeserializeFloat(out speedSettings.speed, SpeedSettings.Defaults.Speed);
                DeserializeFloat(out speedSettings.acceleration, SpeedSettings.Defaults.Acceleration);
                DeserializeInt(out int blinkerState, SpeedSettings.Defaults.BlinkerState);
                speedSettings.BlinkerState = (BlinkerState)blinkerState;
                DeserializeFloat(out speedSettings.jerk, SpeedSettings.Defaults.Jerk);
                DeserializeBool(out speedSettings.causeToYield);
                DeserializeBool(out speedSettings.EyeContactWhileYielding);
                DeserializeBool(out speedSettings.EyeContactAfterYielding);
                DeserializeFloat(out speedSettings.yieldTime);
                DeserializeFloat(out speedSettings.brakingAcceleration);
                DeserializeFloat(out speedSettings.YieldingEyeContactSince);
                DeserializeFloat(out speedSettings.YieldingEyeContactUntil);
                DeserializeString(out string customBehaviourDataString);
                if (!string.IsNullOrWhiteSpace(customBehaviourDataString))
                {
                    //Debug.LogWarning("Circuit could not be fully deserialized. Fill customBehaviourData with: " + customBehaviourDataString);
                    var ids = customBehaviourDataString.Split("%");
                    speedSettings.customBehaviourData = new CustomBehaviourData[ids.Length];
                    for (int j = 0; j < ids.Length; j++)
                    {
                        var id = ids[j].Split("#")[1];
                        speedSettings.customBehaviourData[j] = EditorUtility.InstanceIDToObject(int.Parse(id)) as CustomBehaviourData;
                    }
                }

                var boxCollider = wp.GetComponent<BoxCollider>();

                DeserializeBool(out var enabl);
                boxCollider.enabled = enabl;
                DeserializeBool(out var trigg);
                boxCollider.isTrigger = trigg;

                Vector3 center = default;
                if (DeserializeFloat(out center.x) && DeserializeFloat(out center.y) && DeserializeFloat(out center.z))
                {
                    boxCollider.center = center;
                }

                Vector3 size = default;
                if (DeserializeFloat(out size.x) && DeserializeFloat(out size.y) && DeserializeFloat(out size.z))
                {
                    boxCollider.size = size;
                }
            }

            circuitObject.ApplyModifiedProperties();
        }


        // comparer for check distances in ray cast hits
        public class TransformNameComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                return ((Transform)x).name.CompareTo(((Transform)y).name);
            }
        }
    }
}
#endif

