using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityStandardAssets.Utility;

/// <summary>
/// Editor helpers to populate experiments with AI pedestrians: walking paths, pedestrian crossings
/// and groups of (randomly chosen) pedestrian prefabs registered in the experiment definition.
/// Also builds the PedestrianCrossings example experiment.
/// </summary>
public static class ExperimentPedestrianTools
{
    public const string RocketboxPrefabDir = "Assets/Prefabs/AIPedestrians/Rocketbox";

    /// <summary>Creates a looping walking path (WaypointCircuit) under `parent`.</summary>
    public static WaypointCircuit CreatePath(Transform parent, string name, IList<Vector3> points)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var circuit = go.AddComponent<WaypointCircuit>();
        var items = new Transform[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            var wp = new GameObject($"Waypoint {i:000}").transform;
            wp.SetParent(go.transform, false);
            wp.position = points[i];
            items[i] = wp;
        }
        circuit.waypointList.circuit = circuit;
        circuit.waypointList.items = items;
        circuit.smoothRoute = true;
        return circuit;
    }

    /// <summary>
    /// Creates a crossing from the near kerb `near` to the far kerb `far`: a trigger at the near kerb
    /// (where pedestrians stop and decide) and the far-kerb target.
    /// </summary>
    public static PedestrianCrossing CreateCrossing(Transform parent, string name, Vector3 near, Vector3 far, bool zebra)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = near;
        var dir = far - near; dir.y = 0;
        go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(2.5f, 2f, 1.0f);
        box.center = new Vector3(0, 1f, -0.5f); // on the pavement, just before the kerb
        var crossing = go.AddComponent<PedestrianCrossing>();
        var farKerb = new GameObject("FarKerb").transform;
        farKerb.SetParent(go.transform, false);
        farKerb.position = far;
        crossing.FarKerb = farKerb;
        crossing.Zebra = zebra;
        return crossing;
    }

    /// <summary>
    /// Adds `count` pedestrians (prefabs picked with a seeded random generator) spread along `path`,
    /// using the behaviour models, and registers them in the experiment's AI pedestrian list.
    /// </summary>
    public static List<AIPedestrian> AddPedestrians(ExperimentDefinition experiment, WaypointCircuit path, IList<GameObject> prefabs, int count, int seed)
    {
        var rng = new System.Random(seed);
        var added = new List<AIPedestrian>();
        var points = path.Waypoints;
        // total length of the loop, to spread the pedestrians evenly
        var lengths = new List<float>();
        float total = 0;
        for (int i = 0; i < points.Length; i++)
        {
            float l = Vector3.Distance(points[i].position, points[(i + 1) % points.Length].position);
            lengths.Add(l);
            total += l;
        }
        for (int k = 0; k < count; k++)
        {
            var prefab = prefabs[rng.Next(prefabs.Count)];
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, experiment.transform);
            instance.name = $"{prefab.name} ({k})";
            // position k/count along the loop, with a small sideways offset
            float d = total * k / count, acc = 0;
            int seg = 0;
            while (seg < lengths.Count - 1 && acc + lengths[seg] < d) acc += lengths[seg++];
            var a = points[seg].position;
            var b = points[(seg + 1) % points.Length].position;
            var pos = Vector3.Lerp(a, b, (d - acc) / Mathf.Max(0.01f, lengths[seg]));
            var fwd = (b - a).normalized;
            pos += Vector3.Cross(Vector3.up, fwd) * (float)(rng.NextDouble() - 0.5);
            instance.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
            var ai = instance.GetComponent<AIPedestrian>();
            ai.Behaviour = AIPedestrian.BehaviourMode.BehaviourModels;
            ai.Seed = seed * 1000 + k;
            added.Add(ai);
        }
        Register(experiment, added, path);
        return added;
    }

    static void Register(ExperimentDefinition experiment, List<AIPedestrian> pedestrians, WaypointCircuit path)
    {
        var so = new SerializedObject(experiment);
        var list = so.FindProperty("AIPedestrians").FindPropertyRelative("AIPedestrians");
        foreach (var p in pedestrians)
        {
            int i = list.arraySize;
            list.InsertArrayElementAtIndex(i);
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Pedestrian").objectReferenceValue = p;
            e.FindPropertyRelative("Path").objectReferenceValue = path;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static List<GameObject> RocketboxPrefabs() =>
        AssetDatabase.FindAssets("t:Prefab", new[] { RocketboxPrefabDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null && p.GetComponent<AIPedestrian>() != null)
            .ToList();

    const string ExamplePath = "Assets/ExperimentDefinitions/PedestrianCrossings.prefab";

    /// <summary>
    /// Example experiment: Base plus a group of Rocketbox pedestrians on a loop that crosses the
    /// road south of the central block twice (unmarked, mid-block), where the AV of the
    /// "Automated - Passenger" role passes. Registered in StartScene's experiment list.
    /// </summary>
    [MenuItem("Coupled Sim/Examples/Create PedestrianCrossings experiment")]
    public static void CreateExample()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ExamplePath) == null)
            AssetDatabase.CopyAsset("Assets/ExperimentDefinitions/Base.prefab", ExamplePath);
        var root = PrefabUtility.LoadPrefabContents(ExamplePath);
        try
        {
            var exp = root.GetComponent<ExperimentDefinition>();
            exp.Name = "Pedestrian crossings (behaviour models)";
            exp.ShortName = "PedCross";
            var old = root.transform.Find("CrossingDemo");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var demo = new GameObject("CrossingDemo").transform;
            demo.SetParent(root.transform, false);
            demo.position = Vector3.zero;

            // MainScene: road from z = 2.5 to 12.5 (kerbs 0.25 m high), pavements at y = 0.22
            const float y = 0.22f;
            var path = CreatePath(demo, "CrossingPath", new[]
            {
                new Vector3(-112, y, 14.5f), new Vector3(-101, y, 14.5f),          // north pavement, east
                new Vector3(-100, y, 13.3f), new Vector3(-100, y, 1.8f),           // cross southwards
                new Vector3(-92, y, 1.2f), new Vector3(-86, y, 1.2f),              // south pavement, east
                new Vector3(-85, y, 1.8f), new Vector3(-85, y, 13.3f),             // cross northwards
                new Vector3(-86, y, 14.8f), new Vector3(-111, y, 15.2f),           // back west
            });
            CreateCrossing(demo, "Crossing West", new Vector3(-100, y, 13.3f), new Vector3(-100, y, 1.8f), false);
            CreateCrossing(demo, "Crossing East", new Vector3(-85, y, 1.8f), new Vector3(-85, y, 13.3f), false);

            // drop previously added demo pedestrians from the list (re-running the menu item)
            var so = new SerializedObject(exp);
            var list = so.FindProperty("AIPedestrians").FindPropertyRelative("AIPedestrians");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var ped = list.GetArrayElementAtIndex(i).FindPropertyRelative("Pedestrian").objectReferenceValue as AIPedestrian;
                if (ped == null) list.DeleteArrayElementAtIndex(i);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var p in root.GetComponentsInChildren<AIPedestrian>(true).Where(p => p.name.StartsWith("Pedestrian_") && p.name.Contains("(")))
                Object.DestroyImmediate(p.gameObject);

            var prefabs = RocketboxPrefabs();
            var peds = AddPedestrians(exp, path, prefabs, 8, 7);
            foreach (var p in peds) p.transform.SetParent(demo, true);
            PrefabUtility.SaveAsPrefabAsset(root, ExamplePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        RegisterInStartScene(ExamplePath);
        Debug.Log("Created " + ExamplePath);
    }

    /// <summary>Adds an experiment prefab to NetworkingManager.Experiments in StartScene (if missing).</summary>
    public static void RegisterInStartScene(string experimentPath)
    {
        var exp = AssetDatabase.LoadAssetAtPath<ExperimentDefinition>(experimentPath);
        var scene = EditorSceneManager.OpenScene("Assets/_Scenes/StartScene.unity", OpenSceneMode.Single);
        var nm = Object.FindAnyObjectByType<NetworkingManager>();
        if (nm.Experiments.Contains(exp)) return;
        nm.Experiments = nm.Experiments.Append(exp).ToArray();
        EditorUtility.SetDirty(nm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
