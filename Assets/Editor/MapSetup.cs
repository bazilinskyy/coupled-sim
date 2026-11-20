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
        assets.DetailTemplate = DetailTemplate();
        assets.FacadeTemplate = FacadeTemplate();
        assets.StreetLamp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StreetLights/StreetLamp.prefab");
        assets.Trees = new[] { "Tree9_2", "Tree9_3", "Tree9_4", "Tree9_5" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Tree/{n}.prefab")).Where(t => t != null).ToArray();
        EditorUtility.SetDirty(assets);
    }

    /// <summary>Assets/Settings/Rendering/MapLitDetail.mat: URP Lit with the detail map keyword on.</summary>
    static Material DetailTemplate()
    {
        const string path = "Assets/Settings/Rendering/MapLitDetail.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.EnableKeyword("_DETAIL_MULX2");
        m.SetFloat("_DetailAlbedoMapScale", 1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material FacadeTemplate()
    {
        const string path = "Assets/Settings/Rendering/MapLitFacade.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.EnableKeyword("_NORMALMAP");
        m.EnableKeyword("_PARALLAXMAP");
        m.EnableKeyword("_EMISSION");
        m.EnableKeyword("_OCCLUSIONMAP");
        m.EnableKeyword("_DETAIL_MULX2");   // the photo textures of walls (kept in builds)
        m.SetFloat("_Parallax", 0.03f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Adds the façade template to Resources/MapAssets (batch: -executeMethod MapSetup.AddFacadeTemplate).</summary>
    public static void AddFacadeTemplate()
    {
        var assets = AssetDatabase.LoadAssetAtPath<MapAssets>("Assets/Resources/MapAssets.asset");
        assets.FacadeTemplate = FacadeTemplate();
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        Debug.Log("MapSetup: façade template " + AssetDatabase.GetAssetPath(assets.FacadeTemplate));
    }

    /// <summary>The photo textures of walls (Assets/ThirdParty/Textures/Walls, CC0) into Resources/MapAssets, normal maps imported as such
    /// (batch: -executeMethod MapSetup.AddWallTextures).</summary>
    public static void AddWallTextures()
    {
        const string dir = "Assets/ThirdParty/Textures/Walls/";
        // red brick, dark brick, yellow brick, plaster, concrete; metres of wall per texture
        var names = new[] { "Bricks101", "Bricks059", "Bricks105", "Plaster001", "Concrete034" };
        var sizes = new[] { new Vector2(1.6f, 1.6f), new Vector2(1.05f, 1.05f), new Vector2(2.4f, 1.2f), new Vector2(2.5f, 2.5f), new Vector2(1.1f, 0.55f) };
        foreach (var n in names)
        {
            var ni = (TextureImporter)AssetImporter.GetAtPath(dir + n + "_normal.jpg");
            if (ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
        }
        var assets = AssetDatabase.LoadAssetAtPath<MapAssets>("Assets/Resources/MapAssets.asset");
        assets.WallDetail = names.Select(n => AssetDatabase.LoadAssetAtPath<Texture2D>(dir + n + "_detail.jpg")).ToArray();
        assets.WallNormal = names.Select(n => AssetDatabase.LoadAssetAtPath<Texture2D>(dir + n + "_normal.jpg")).ToArray();
        assets.WallDetailSize = sizes;
        assets.FacadeTemplate = FacadeTemplate();
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        Debug.Log($"MapSetup: wall textures {assets.WallDetail.Count(t => t != null)} detail, {assets.WallNormal.Count(t => t != null)} normal");
    }

    /// <summary>The photo textures of the streets' surfaces (Assets/ThirdParty/Textures/Ground, CC0 Poly Haven) and the lawn
    /// (Assets/Textures/Ground/Lawn, Tools/lawn_texture.py) into Resources/MapAssets, normal maps imported as such
    /// (batch: -executeMethod MapSetup.AddGroundTextures).</summary>
    public static void AddGroundTextures()
    {
        const string dir = "Assets/ThirdParty/Textures/Ground/";
        // (name, colour map, normal map, metres per texture: the scanned area)
        var set = new (string name, string col, string nrm, float size)[]
        {
            ("asphalt", dir + "asphalt_02_diff_2k.jpg", dir + "asphalt_02_nor_gl_2k.jpg", 3f),
            ("asphalt_light", dir + "asphalt_04_diff_2k.jpg", dir + "asphalt_04_nor_gl_2k.jpg", 4f),
            ("cycle_red", dir + "cycle_red_diff_2k.jpg", dir + "clean_asphalt_nor_gl_2k.jpg", 2.1f),
            ("bricks", dir + "herringbone_brick_diff_2k.jpg", dir + "herringbone_brick_nor_gl_2k.jpg", 1.8f),
            ("parking", dir + "brick_pavement_02_diff_2k.jpg", dir + "brick_pavement_02_nor_gl_2k.jpg", 2f),
            ("paving", dir + "concrete_pavement_diff_2k.jpg", dir + "concrete_pavement_nor_gl_2k.jpg", 1.8f),
            ("cobbles", dir + "cobblestone_floor_08_diff_2k.jpg", dir + "cobblestone_floor_08_nor_gl_2k.jpg", 2f),
            ("gravel", dir + "gravel_floor_02_diff_2k.jpg", dir + "gravel_floor_02_nor_gl_2k.jpg", 2f),
            ("kerb", dir + "concrete_floor_01_diff_2k.jpg", dir + "concrete_floor_01_nor_gl_2k.jpg", 2f),
            ("lawn", "Assets/Textures/Ground/Lawn/lawn_col.png", "Assets/Textures/Ground/Lawn/lawn_nrm.png", 2f),
        };
        foreach (var e in set)
        {
            var ci = (TextureImporter)AssetImporter.GetAtPath(e.col);
            if (ci.maxTextureSize != 2048 || ci.wrapMode != TextureWrapMode.Repeat || ci.anisoLevel < 8) { ci.maxTextureSize = 2048; ci.wrapMode = TextureWrapMode.Repeat; ci.anisoLevel = 8; ci.SaveAndReimport(); }
            var ni = (TextureImporter)AssetImporter.GetAtPath(e.nrm);
            if (ni.textureType != TextureImporterType.NormalMap || ni.anisoLevel < 8) { ni.textureType = TextureImporterType.NormalMap; ni.wrapMode = TextureWrapMode.Repeat; ni.anisoLevel = 8; ni.SaveAndReimport(); }
        }
        var assets = AssetDatabase.LoadAssetAtPath<MapAssets>("Assets/Resources/MapAssets.asset");
        assets.GroundNames = set.Select(e => e.name).ToArray();
        assets.GroundColour = set.Select(e => AssetDatabase.LoadAssetAtPath<Texture2D>(e.col)).ToArray();
        assets.GroundNormal = set.Select(e => AssetDatabase.LoadAssetAtPath<Texture2D>(e.nrm)).ToArray();
        assets.GroundSize = set.Select(e => e.size).ToArray();
        // the water's material (CoupledSim/Water)
        const string waterPath = "Assets/Materials/Map/Water.mat";
        var water = AssetDatabase.LoadAssetAtPath<Material>(waterPath);
        if (water == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Materials/Map");
            water = new Material(Shader.Find("CoupledSim/Water"));
            AssetDatabase.CreateAsset(water, waterPath);
        }
        assets.WaterTemplate = water;
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        Debug.Log($"MapSetup: ground textures {assets.GroundColour.Count(t => t != null)} colour, {assets.GroundNormal.Count(t => t != null)} normal");
    }

    /// <summary>Adds the detail template to Resources/MapAssets (batch: -executeMethod MapSetup.AddDetailTemplate).</summary>
    public static void AddDetailTemplate()
    {
        var assets = AssetDatabase.LoadAssetAtPath<MapAssets>("Assets/Resources/MapAssets.asset");
        assets.DetailTemplate = DetailTemplate();
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        Debug.Log("MapSetup: detail template " + AssetDatabase.GetAssetPath(assets.DetailTemplate));
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
                Role("Drone pilot", SpawnPointType.PlayerFlyingDrone, OsmMap.SpawnKind.Pavement, 2),
            };
            var drones = new GameObject("Drones (AI)").AddComponent<DroneSpawner>();
            drones.transform.SetParent(root.transform, false);
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
