using System.Collections.Generic;
using System.Linq;
using CoupledSim.Traffic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityStandardAssets.Utility;

/// <summary>Editor helpers for model-driven background traffic, and the Traffic example experiment.</summary>
public static class TrafficTools
{
    const string ExamplePath = "Assets/ExperimentDefinitions/Traffic.prefab";

    /// <summary>
    /// Creates a lane for the opposite direction next to a looping lane: the points are moved
    /// `offset` metres outwards from the loop's centre and the order is reversed.
    /// </summary>
    public static WaypointCircuit CreateOppositeLane(WaypointCircuit lane, Transform parent, string name, float offset)
    {
        var pts = lane.Waypoints.Select(w => w.position).ToList();
        var centre = pts.Aggregate(Vector3.zero, (a, b) => a + b) / pts.Count;
        float halfX = pts.Max(p => Mathf.Abs(p.x - centre.x)) * 0.85f;
        float halfZ = pts.Max(p => Mathf.Abs(p.z - centre.z)) * 0.85f;
        var moved = pts.Select(p => new Vector3(
            p.x + (Mathf.Abs(p.x - centre.x) > halfX ? Mathf.Sign(p.x - centre.x) * offset : 0f),
            p.y,
            p.z + (Mathf.Abs(p.z - centre.z) > halfZ ? Mathf.Sign(p.z - centre.z) * offset : 0f))).ToList();
        moved.Reverse();
        return ExperimentPedestrianTools.CreatePath(parent, name, moved);
    }

    /// <summary>Adds car prefabs to NetworkingManager ➡️ AI Car System ➡️ Prefabs in StartScene.</summary>
    public static void RegisterAICarPrefabs(IEnumerable<AICar> prefabs)
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Scenes/StartScene.unity", OpenSceneMode.Single);
        var nm = Object.FindAnyObjectByType<NetworkingManager>();
        var so = new SerializedObject(nm);
        var list = so.FindProperty("_aiCarSystem").FindPropertyRelative("Prefabs");
        var existing = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
        foreach (var p in prefabs.Where(p => p != null && !existing.Contains(p)))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = p;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static AICar LoadCar(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<AICar>();

    /// <summary>
    /// Example experiment: Base plus IDM-driven traffic in both directions on the roads around the
    /// central block (the AV's route and the opposite lane), with Smart and hatchback cars.
    /// </summary>
    [MenuItem("Coupled Sim/Examples/Create Traffic experiment")]
    public static void CreateExample()
    {
        var cars = new[]
        {
            LoadCar("Assets/Prefabs/Cars/DrivableSmartCommon.prefab"),
            LoadCar("Assets/Prefabs/Cars/DrivableHatchbackCommon.prefab"),
        }.Where(c => c != null).ToArray();
        RegisterAICarPrefabs(cars);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ExamplePath) == null)
            AssetDatabase.CopyAsset("Assets/ExperimentDefinitions/Base.prefab", ExamplePath);
        var root = PrefabUtility.LoadPrefabContents(ExamplePath);
        try
        {
            var exp = root.GetComponent<ExperimentDefinition>();
            exp.Name = "Traffic (car-following models)";
            exp.ShortName = "Traffic";
            var old = root.transform.Find("Traffic");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var traffic = new GameObject("Traffic").transform;
            traffic.SetParent(root.transform, false);

            var inner = root.GetComponentsInChildren<WaypointCircuit>(true).First(c => c.name == "Passenger Traffic Circuit");
            var outer = CreateOppositeLane(inner, traffic, "Opposite lane", 5f);

            var spawner = traffic.gameObject.AddComponent<TrafficSpawner>();
            spawner.Lanes = new[]
            {
                new TrafficSpawner.Lane { Path = inner, Cars = 6 },
                new TrafficSpawner.Lane { Path = outer, Cars = 6 },
            };
            spawner.Model = CarFollowingModelType.IDM;
            spawner.CarPrefabs = cars;
            spawner.spawnParams = new CarSpawnParams { Car = cars[0], SpawnDriver = true, VehicleType = PlayerSystem.VehicleType.MDV };

            exp.CarSpawners = exp.CarSpawners.Where(s => s != null).Append(spawner).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, ExamplePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        ExperimentPedestrianTools.RegisterInStartScene(ExamplePath);
        Debug.Log("Created " + ExamplePath);
    }
}
