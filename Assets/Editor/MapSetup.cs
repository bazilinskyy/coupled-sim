using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sets up the city maps: the MapAssets resource, the OsmMap scene and the "City map" experiment
/// (roles placed by OsmSpawnPoint, time of day for the city, SUMO traffic of the map).
/// Menu: Coupled Sim ➡️ Maps ➡️ Set up map scene and experiment.
/// </summary>
public static class MapSetup
{
    const string ScenePath = "Assets/_Scenes/OsmMap.unity";
    const string ExperimentPath = "Assets/ExperimentDefinitions/CityMap.prefab";

    [MenuItem("Coupled Sim/Maps/Set up map scene and experiment")]
    public static void SetUp()
    {
        CreateAssets();
        CreateScene();
        CreateExperiment();
        ExperimentPedestrianTools.RegisterInStartScene(ExperimentPath);
        AssetDatabase.SaveAssets();
        Debug.Log("MapSetup: done");
    }

    static void CreateAssets()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources");
        var lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Rendering/MapLit.mat");
        if (lit == null)
        {
            lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(lit, "Assets/Settings/Rendering/MapLit.mat");
        }
        var assets = AssetDatabase.LoadAssetAtPath<MapAssets>("Assets/Resources/MapAssets.asset");
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<MapAssets>();
            AssetDatabase.CreateAsset(assets, "Assets/Resources/MapAssets.asset");
        }
        assets.LitTemplate = lit;
        assets.StreetLamp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StreetLights/StreetLamp.prefab");
        assets.Trees = new[] { "Tree9_2", "Tree9_3", "Tree9_4", "Tree9_5" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Tree/{n}.prefab")).Where(t => t != null).ToArray();
        EditorUtility.SetDirty(assets);
    }

    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var map = new GameObject("OsmMap").AddComponent<OsmMap>();
        map.DefaultMap = "Eindhoven";
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(45, 200, 0);
        RenderSettings.sun = sun;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/Rendering/SimulatorVolumeProfile.asset");
        if (profile != null)
        {
            var vol = new GameObject("Post-processing (URP)").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    static Transform Spawn(Transform parent, string name, OsmMap.SpawnKind kind, int index)
    {
        var t = new GameObject(name + " spawn").transform;
        t.SetParent(parent, false);
        var sp = t.gameObject.AddComponent<OsmSpawnPoint>();
        sp.Kind = kind;
        sp.Index = index;
        return t;
    }

    static void CreateExperiment()
    {
        var root = new GameObject("CityMap");
        try
        {
            var exp = root.AddComponent<ExperimentDefinition>();
            exp.Name = "City map (OpenStreetMap)";
            exp.ShortName = "City";
            exp.Scene = "OsmMap";
            exp.Map = "Eindhoven";
            var spawns = new GameObject("SpawnPoints").transform;
            spawns.SetParent(root.transform, false);
            ExperimentRoleDefinition Role(string name, SpawnPointType type, OsmMap.SpawnKind kind, int index, int carIdx = 0, int twoWheelerIdx = 0) =>
                new ExperimentRoleDefinition
                {
                    Name = name,
                    carIdx = carIdx,
                    twoWheelerIdx = twoWheelerIdx,
                    SpawnPoint = new SpawnPoint { Point = Spawn(spawns, name, kind, index), Type = type, CameraIndex = 0, VehicleType = PlayerSystem.VehicleType.MDV },
                };
            exp.Roles = new[]
            {
                Role("Driver", SpawnPointType.PlayerControlingCar, OsmMap.SpawnKind.RoadLane, 0),
                Role("Pedestrian", SpawnPointType.PlayerControlledPedestrian, OsmMap.SpawnKind.Pavement, 0),
                Role("Cyclist", SpawnPointType.PlayerRidingTwoWheeler, OsmMap.SpawnKind.CyclePath, 0, twoWheelerIdx: 0),
                Role("E-scooter rider", SpawnPointType.PlayerRidingTwoWheeler, OsmMap.SpawnKind.CyclePath, 1, twoWheelerIdx: 1),
                Role("Moped rider", SpawnPointType.PlayerRidingTwoWheeler, OsmMap.SpawnKind.RoadEdge, 1, twoWheelerIdx: 2),
                Role("Second driver", SpawnPointType.PlayerControlingCar, OsmMap.SpawnKind.RoadLane, 1),
            };
            exp.PointsOfInterest = new Transform[0];

            var env = new GameObject("Environment (time of day, weather)").AddComponent<EnvironmentController>();
            env.transform.SetParent(root.transform, false);
            env.UseBakedLighting = false;
            env.TimeOfDay = 14f;
            env.Weather = Weather.PartlyCloudy;

            var sumo = new GameObject("SUMO").AddComponent<SumoBridge>();
            sumo.transform.SetParent(root.transform, false);
            sumo.UseMapScenario = true;
            var cars = new[] { "Assets/Prefabs/Cars/DrivableSmartCommon.prefab", "Assets/Prefabs/Cars/DrivableHatchbackCommon.prefab" }
                .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)?.GetComponent<AICar>()).Where(c => c != null).ToArray();
            sumo.CarPrefabs = cars;
            var bike = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TwoWheelers/AIBicycle.prefab")?.GetComponent<AICar>();
            if (bike != null) sumo.BicyclePrefabs = new[] { bike };
            prefabsToRegister = cars.Append(bike).Where(c => c != null).Select(c => AssetDatabase.GetAssetPath(c)).ToArray();
            sumo.spawnParams = new CarSpawnParams { Car = cars.FirstOrDefault(), SpawnDriver = true, VehicleType = PlayerSystem.VehicleType.MDV };
            exp.CarSpawners = new CarSpawnerBase[] { sumo };

            PrefabUtility.SaveAsPrefabAsset(root, ExperimentPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        // after saving: this opens StartScene, which would destroy objects of the current scene
        TrafficTools.RegisterAICarPrefabs(prefabsToRegister.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<AICar>()));
    }

    static string[] prefabsToRegister = new string[0];

    /// <summary>Builds every map of the catalogue once and reports its size (and any errors).</summary>
    [MenuItem("Coupled Sim/Maps/Check all maps")]
    public static void CheckAll()
    {
        int failed = 0;
        foreach (var info in MapCatalog.Maps)
        {
            var go = new GameObject("check " + info.Name);
            try
            {
                var map = go.AddComponent<OsmMap>();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                map.Load(info.Name);
                var g = map.Generator;
                bool sumo = System.IO.File.Exists($"Assets/StreamingAssets/Sumo/Maps/{info.Name}/{info.Name}.sumocfg");
                bool spawns = map.FindSpawn(OsmMap.SpawnKind.RoadLane, 0, out _, out _) && map.FindSpawn(OsmMap.SpawnKind.Pavement, 0, out _, out _);
                Debug.Log($"MAPCHECK {info.Name}: {sw.ElapsedMilliseconds} ms, {g.Roads.Count} roads, {g.CyclePaths.Count} cycle paths, {g.BuildingCount} buildings, {g.Crossings.Count / 2} crossings, spawns {(spawns ? "ok" : "MISSING")}, SUMO {(sumo ? "ok" : "MISSING")}");
                if (!spawns || !sumo || g.Roads.Count == 0) failed++;
            }
            catch (System.Exception e)
            {
                failed++;
                Debug.LogError($"MAPCHECK {info.Name}: {e}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
        Debug.Log($"MAPCHECK done: {MapCatalog.Maps.Length - failed}/{MapCatalog.Maps.Length} maps ok");
    }
}
