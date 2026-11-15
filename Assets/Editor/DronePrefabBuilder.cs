using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The participant's drone: Assets/Prefabs/Drones/Drone.prefab (a <see cref="DroneController"/> that builds its body in
/// code, a camera rig, a PlayerAvatar of type Drone), registered in StartScene's PlayerSystem; and a "Drone pilot" role
/// and AI drones (<see cref="DroneSpawner"/>) in the CityMap experiment. Menu: Coupled Sim ➡️ Drones ➡️ Set up drones;
/// batch: -executeMethod DronePrefabBuilder.SetUp.
/// </summary>
public static class DronePrefabBuilder
{
    const string Dir = "Assets/Prefabs/Drones";
    const string PrefabPath = Dir + "/Drone.prefab";

    [MenuItem("Coupled Sim/Drones/Set up drones")]
    public static void SetUp()
    {
        BuildPrefab();
        Register();
        AddToCityMap();
        AssetDatabase.SaveAssets();
    }

    static void BuildPrefab()
    {
        System.IO.Directory.CreateDirectory(Dir);
        var root = new GameObject("Drone");
        try
        {
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.46f, 0.15f, 0.46f);
            col.center = new Vector3(0, 0.06f, 0);
            col.isTrigger = true;
            var rig = new GameObject("Camera rig").transform;
            rig.SetParent(root.transform, false);
            rig.localPosition = new Vector3(0, 0.4f, -1.3f);
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(rig, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            camGo.SetActive(false);   // switched on for the local player
            var ctrl = root.AddComponent<DroneController>();
            ctrl.Preset = "Consumer";
            ctrl.CameraRig = rig;
            ctrl.ReadInput = true;
            ctrl.enabled = false;     // enabled for the local player by PlayerAvatar (PlayerAsDriver)
            var avatar = root.AddComponent<PlayerAvatar>();
            avatar.Type = AvatarType.Drone;
            avatar.cameras = new[] { cam };
            avatar.SyncTransforms = new Transform[0];
            avatar.PlayerAsDriver = new PlayerAvatar.ModeElements { gameObjects = new GameObject[0], monoBehaviours = new MonoBehaviour[] { ctrl } };
            avatar.HostDrivenAIElements = new PlayerAvatar.ModeElements { gameObjects = new GameObject[0], monoBehaviours = new MonoBehaviour[0] };
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("DronePrefabBuilder: built " + PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void Register()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Scenes/StartScene.unity");
        var drone = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<PlayerAvatar>();
        var ps = Object.FindAnyObjectByType<PlayerSystem>();
        var so = new SerializedObject(ps);
        var list = so.FindProperty("_AvatarPrefabDrone");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = drone;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("DronePrefabBuilder: registered in PlayerSystem");
    }

    static void AddToCityMap()
    {
        const string path = "Assets/ExperimentDefinitions/CityMap.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var def = root.GetComponent<ExperimentDefinition>();
            var roles = def.Roles.ToList();
            if (!roles.Any(r => r.SpawnPoint.Type == SpawnPointType.PlayerFlyingDrone))
            {
                var spawns = root.transform.Find("SpawnPoints") ?? root.transform;
                var t = new GameObject("Drone pilot spawn").transform;
                t.SetParent(spawns, false);
                var sp = t.gameObject.AddComponent<OsmSpawnPoint>();
                sp.Kind = OsmMap.SpawnKind.Pavement;
                sp.Index = 2;
                roles.Add(new ExperimentRoleDefinition
                {
                    Name = "Drone pilot",
                    droneIdx = 0,
                    SpawnPoint = new SpawnPoint { Point = t, Type = SpawnPointType.PlayerFlyingDrone, CameraIndex = 0, VehicleType = PlayerSystem.VehicleType.MDV },
                });
                def.Roles = roles.ToArray();
            }
            if (root.GetComponentInChildren<DroneSpawner>(true) == null)
            {
                var go = new GameObject("Drones (AI)");
                go.transform.SetParent(root.transform, false);
                var s = go.AddComponent<DroneSpawner>();
                s.Deliveries = 2; s.Patrols = 1; s.Swarm = 0;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("DronePrefabBuilder: drone pilot role and AI drones in " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
