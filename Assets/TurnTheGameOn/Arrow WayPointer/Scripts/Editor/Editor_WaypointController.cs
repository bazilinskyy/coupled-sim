namespace TurnTheGameOn.ArrowWaypointer
{
    using UnityEngine;
    using UnityEditor;
    [CustomEditor(typeof(WaypointController))]
    public class Editor_WaypointController : Editor
    {
        [MenuItem("Tools/TurnTheGameOn/Waypoint Controller")]
        static void Create()
        {
            GameObject instance = Instantiate(Resources.Load("Waypoint Controller", typeof(GameObject))) as GameObject;
            instance.name = "Waypoint Controller";
        }

        public override void OnInspectorGUI()
        {
            WaypointController waypointController = (WaypointController)target;

            if (!waypointController.player) EditorGUILayout.HelpBox("Assign the Player transform to use the waypoints radius value as a waypoint trigger.", MessageType.Info);
            if (waypointController.configureMode == WaypointController.Switch.On) EditorGUILayout.HelpBox("Configure Mode is turned on, you must turn it off to complete configuration.", MessageType.Warning);

            EditorGUILayout.Space();

            if (GUILayout.Button("Cleanup Old Wayponts"))
            {
                waypointController.CleanUpWaypoints();
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            DrawDefaultInspector();

            EditorUtility.SetDirty(waypointController);
        }

    }
}