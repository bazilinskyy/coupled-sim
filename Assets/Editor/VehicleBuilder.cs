using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Makes drivable (and AI-traffic) vehicle prefabs from 3D models: the hatchback prefab is copied with
/// all its logic (physics, wheel colliders, AI driving, blinkers, lights, cameras, driver, dashboard,
/// eHMI slots), its body is replaced by the model, and wheels, driver seat, camera, dashboard,
/// blinkers and eHMI slots are moved to fit the model. The physics come from the vehicle type
/// (RealisticVehicleDynamics.CarPreset). Models: Kenney Car Kit (CC0) and CC-BY models listed in
/// Assets/ThirdParty/Vehicles/CREDITS.md. Coupled Sim > Vehicles > Build vehicle prefabs, or
/// -executeMethod VehicleBuilder.BuildAll. The prefabs go to Assets/Prefabs/Cars/Vehicles and are
/// added to the AI car list (StartScene), background traffic, SUMO and the parked cars of city maps.
/// </summary>
public static class VehicleBuilder
{
    public sealed class Def
    {
        public string Name, Model;
        public RealisticVehicleDynamics.CarPreset Preset;
        public float Length;                 // metres, front to back (the model is scaled to it)
        public float Width, Height;          // metres; when set (stylised models), each axis is scaled to them and the wheels stay round
        public float HeadFromFront;          // driver's eyes behind the front of the body (m)
        public float HeadHeight;             // driver's eyes above the ground (m)
        public float HeadSide = 0.36f;       // driver's eyes left of the centre (m)
        public float Yaw;                    // turn the model so its front is +z
        public string[] Wheels = { "wheel-front-left", "wheel-front-right", "wheel-back-left", "wheel-back-right" };
        public string SteeringWheel;         // node of the model's steering wheel (with an interior)
        public bool Interior;                // can be driven from inside (else: traffic and parked only)
        public bool Traffic = true;          // in AI traffic and parked cars
    }

    const string Kenney = "Assets/ThirdParty/Kenney/CarKit/";
    const string OutFolder = "Assets/Prefabs/Cars/Vehicles";
    const string Template = "Assets/Prefabs/Cars/DrivableHatchbackCommon.prefab";

    public static readonly List<Def> Defs = new List<Def>
    {
        // real sizes (length, width, height incl. roof signs and light bars) of the vehicle types
        K("Sedan", "sedan", RealisticVehicleDynamics.CarPreset.Estate, 4.7f, 1.83f, 1.47f),
        K("SedanSports", "sedan-sports", RealisticVehicleDynamics.CarPreset.Hatchback, 4.5f, 1.85f, 1.35f),
        K("HatchbackSports", "hatchback-sports", RealisticVehicleDynamics.CarPreset.Hatchback, 4.2f, 1.8f, 1.42f),
        K("Suv", "suv", RealisticVehicleDynamics.CarPreset.Suv, 4.7f, 1.86f, 1.68f, eyes: 1.45f),
        K("SuvLuxury", "suv-luxury", RealisticVehicleDynamics.CarPreset.Suv, 5.0f, 1.95f, 1.75f, eyes: 1.45f),
        K("Taxi", "taxi", RealisticVehicleDynamics.CarPreset.Estate, 4.7f, 1.83f, 1.75f),
        K("Police", "police", RealisticVehicleDynamics.CarPreset.Estate, 4.8f, 1.85f, 1.75f),
        K("Van", "van", RealisticVehicleDynamics.CarPreset.Van, 5.3f, 2.05f, 2.5f, cab: true),
        K("Ambulance", "ambulance", RealisticVehicleDynamics.CarPreset.Van, 6.0f, 2.1f, 2.8f, cab: true),
        K("Delivery", "delivery", RealisticVehicleDynamics.CarPreset.Van, 6.0f, 2.1f, 2.9f, cab: true),
        K("DeliveryFlat", "delivery-flat", RealisticVehicleDynamics.CarPreset.Van, 6.0f, 2.1f, 2.4f, cab: true),
        K("Truck", "truck", RealisticVehicleDynamics.CarPreset.Truck, 7.5f, 2.5f, 3.4f, cab: true, eyes: 2.4f, side: 0.55f),
        K("TruckFlat", "truck-flat", RealisticVehicleDynamics.CarPreset.Truck, 7.5f, 2.5f, 3.0f, cab: true, eyes: 2.4f, side: 0.55f),
        K("GarbageTruck", "garbage-truck", RealisticVehicleDynamics.CarPreset.Truck, 8.5f, 2.55f, 3.5f, cab: true, eyes: 2.4f, side: 0.55f),
        K("FireTruck", "firetruck", RealisticVehicleDynamics.CarPreset.Truck, 9.0f, 2.55f, 3.3f, cab: true, eyes: 2.4f, side: 0.55f),
    };

    static Def K(string name, string file, RealisticVehicleDynamics.CarPreset preset, float length, float width, float height, bool cab = false, float eyes = 1.22f, float side = 0.36f) => new Def
    {
        Name = "Kenney" + name, Model = Kenney + file + ".fbx", Preset = preset, Length = length, Width = width, Height = height,
        HeadFromFront = cab ? 1.15f : length * 0.55f, HeadHeight = cab ? Mathf.Max(eyes, 1.95f) : eyes, HeadSide = cab ? Mathf.Max(side, 0.45f) : side,
    };

    [MenuItem("Coupled Sim/Vehicles/Build vehicle prefabs")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(OutFolder);
        var built = new List<GameObject>();
        foreach (var d in Defs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(d.Model) == null) { Debug.LogWarning($"VehicleBuilder: no model {d.Model} for {d.Name}"); continue; }
            try { var p = Build(d); if (p != null) built.Add(p); }
            catch (Exception e) { Debug.LogError($"VehicleBuilder: {d.Name}: {e}"); }
        }
        Register(built.Where(p => Defs.First(d => p.name == "Drivable" + d.Name).Traffic).ToList());
        AssetDatabase.SaveAssets();
        Debug.Log($"VehicleBuilder: built {built.Count} vehicles: {string.Join(", ", built.Select(b => b.name))}");
    }

    static Bounds LocalBounds(Transform root, IEnumerable<Renderer> renderers)
    {
        bool first = true; var b = new Bounds();
        foreach (var r in renderers)
        {
            // the renderer's bounds corners in the root's space
            var wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = root.InverseTransformPoint(c);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    static Transform Find(Transform root, string path) => root.Find(path) ?? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == path);

    static void MoveBy(Transform root, string path, Vector3 delta)
    {
        var t = Find(root, path);
        if (t != null) t.position = root.TransformPoint(root.InverseTransformPoint(t.position) + delta);
    }

    static void PlaceAt(Transform root, Transform t, Vector3 local) { if (t != null) t.position = root.TransformPoint(local); }

    public static GameObject Build(Def d)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(Template);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(template);
        try
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Drivable" + d.Name;
            var root = go.transform;
            root.position = Vector3.zero; root.rotation = Quaternion.identity;

            // the template's ground (bottom of its wheels) and driver's eyes
            var carModel = Find(root, "CarModel");
            var oldWheels = new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" }.Select(n => Find(carModel, n)).Where(t => t != null).ToArray();
            float ground = oldWheels.Length > 0 ? LocalBounds(root, oldWheels.SelectMany(w => w.GetComponentsInChildren<Renderer>(true))).min.y : 0f;
            var oldHead = root.InverseTransformPoint(Find(root, "DriverLogic").Find("CameraPositionSet").position);
            // parts sent over the network (PlayerAvatar.SyncTransforms): remember which old wheel each was
            var avatar = go.GetComponent<PlayerAvatar>();
            var syncNames = avatar != null && avatar.SyncTransforms != null ? avatar.SyncTransforms.Select(t => t != null ? t.name : null).ToArray() : new string[0];
            for (int i = carModel.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(carModel.GetChild(i).gameObject);

            // the model: turned, scaled to its length, standing on the ground, centred
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(d.Model));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model " + d.Name;
            model.transform.SetParent(carModel, false);
            model.transform.localRotation = Quaternion.Euler(0f, d.Yaw, 0f);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var b0 = LocalBounds(root, renderers);
            float s = d.Length / Mathf.Max(0.01f, b0.size.z);
            var axisScale = d.Width > 0f && d.Height > 0f
                ? new Vector3(d.Width / Mathf.Max(0.01f, b0.size.x), d.Height / Mathf.Max(0.01f, b0.size.y), s)
                : Vector3.one * s;
            model.transform.localScale = Vector3.Scale(model.transform.localScale, axisScale);
            // wheels stay round: their own scale is uniform (the body's height scale), width as the body's
            float wheelScale = axisScale.y;
            var b = LocalBounds(root, renderers);
            model.transform.position += root.TransformVector(new Vector3(-b.center.x, ground - b.min.y, -b.center.z));
            b = LocalBounds(root, renderers);

            // wheels: a pivot at each wheel's centre (the suspension turns and moves it), colliders there
            var colliders = new[] { "FLWheel (1)", "FRWheel (1)", "BLWheel (1)", "BRWheel (1)" }.Select(n => Find(root, n)).ToArray();
            var wheelRenderers = new List<Renderer>();
            float radius = 0.3f;
            for (int i = 0; i < 4; i++)
            {
                var w = Find(model.transform, d.Wheels[i]);
                if (w == null) { Debug.LogWarning($"VehicleBuilder: {d.Name}: no wheel {d.Wheels[i]}"); continue; }
                if (axisScale.x != axisScale.z)
                {
                    // undo the body's stretch on the wheel: x keeps the width scale, y and z the height scale
                    var centre = LocalBounds(root, w.GetComponentsInChildren<Renderer>(true)).center;
                    var ls = w.localScale;
                    w.localScale = new Vector3(ls.x, ls.y, ls.z * wheelScale / axisScale.z);
                    var after = LocalBounds(root, w.GetComponentsInChildren<Renderer>(true)).center;
                    w.position += root.TransformVector(centre - after);
                }
                var wr = w.GetComponentsInChildren<Renderer>(true);
                wheelRenderers.AddRange(wr);
                var wb = LocalBounds(root, wr);
                radius = wb.extents.y;
                var pivot = new GameObject("Wheel pivot " + d.Wheels[i]).transform;
                pivot.SetParent(carModel, false);
                pivot.position = root.TransformPoint(wb.center);
                w.SetParent(pivot, true);
                var wc = colliders[i].GetComponent<WheelCollider>();
                wc.radius = radius;
                // at rest (spring target 0.5) the wheel sits where the model has it
                colliders[i].position = root.TransformPoint(wb.center + Vector3.up * wc.suspensionDistance * 0.5f);
                var susp = colliders[i].GetComponent<VehicleBehaviour.Suspension>();
                if (susp != null) { susp.wheelModel = pivot.gameObject; susp.localRotOffset = Vector3.zero; }
            }
            if (avatar != null && avatar.SyncTransforms != null)
            {
                var pivots = d.Wheels.Select(n => carModel.Find("Wheel pivot " + n)).ToArray();
                for (int i = 0; i < avatar.SyncTransforms.Length; i++)
                {
                    if (avatar.SyncTransforms[i] != null) continue;
                    var n = syncNames[i] ?? "";
                    int k = n.EndsWith("FL") ? 0 : n.EndsWith("FR") ? 1 : n.EndsWith("RL") || n.EndsWith("BL") ? 2 : n.EndsWith("RR") || n.EndsWith("BR") ? 3 : -1;
                    avatar.SyncTransforms[i] = k >= 0 && pivots[k] != null ? pivots[k] : root;
                }
            }
            var rot = go.GetComponent<WheelRotator>();
            if (rot != null) rot.WheelDiameter = radius * 2f;

            // body collider above the wheels
            var body = LocalBounds(root, renderers.Except(wheelRenderers));
            var box = go.GetComponent<BoxCollider>();
            if (box != null)
            {
                float bottom = ground + radius * 0.9f;
                box.center = new Vector3(body.center.x, (bottom + body.max.y) / 2f, body.center.z);
                box.size = new Vector3(body.size.x, body.max.y - bottom, body.size.z);
            }

            // driver's and passenger's eyes, and what is placed relative to them
            var head = new Vector3(-d.HeadSide, ground + d.HeadHeight, b.max.z - d.HeadFromFront);
            var delta = head - oldHead;
            var pDelta = new Vector3(-delta.x, delta.y, delta.z);   // passenger: mirrored across the centre
            foreach (var p in new[] { "DriverLogic/CameraPositionSet", "CarLogic/HeadPosition", "CarLogic/HeadTargetAnchor", "DriverModel&Rig/Driver",
                                      "CarLogic/ModelElements/Dashboard", "CarLogic/ModelElements/steering wheel" })
                MoveBy(root, p, delta);
            foreach (var p in new[] { "PassengerLogic/CameraPositionSet", "PassengerModel/BlueSuitFree01" }) MoveBy(root, p, pDelta);

            // the steering wheel: the model's own (turned by WheelVehicle), else an empty stand-in
            var sw = go.GetComponentInChildren<SteeringWheel>(true);
            Transform wheelVisual = d.SteeringWheel != null ? Find(model.transform, d.SteeringWheel) : null;
            if (wheelVisual == null) { wheelVisual = new GameObject("Steering wheel (none)").transform; wheelVisual.SetParent(sw != null ? sw.transform : root, false); }
            if (sw != null)
            {
                if (d.SteeringWheel != null && wheelVisual != null)
                {
                    // turn about the wheel's own centre
                    var c = LocalBounds(root, wheelVisual.GetComponentsInChildren<Renderer>(true)).center;
                    sw.transform.position = root.TransformPoint(c);
                    if (sw.pivot != null) sw.pivot.position = root.TransformPoint(c);
                }
                sw.steeringWheel = wheelVisual;
            }
            var wv = go.GetComponent<VehicleBehaviour.WheelVehicle>();
            if (wv != null) wv.steeringWheel = d.SteeringWheel != null ? wheelVisual : null;

            // blinkers and lights at the corners of the body
            float hw = body.extents.x, front = body.max.z, back = body.min.z, h = body.max.y - ground;
            void Corner(string group, float z, float y, float inset)
            {
                var g = Find(root, "CarLogic/ModelElements/" + group);
                if (g == null) return;
                foreach (Transform c in g)
                    if (c.name.Contains("Left") || c.name.Contains("Right"))
                    {
                        // smaller lamps, flush with the body's corner
                        c.localScale *= 0.55f;
                        PlaceAt(root, c, new Vector3((c.name.Contains("Left") ? -1 : 1) * (hw - inset), y, z - Mathf.Sign(z) * 0.03f));
                    }
                    else if (c.name.Contains("Flare")) { var l = root.InverseTransformPoint(c.position); PlaceAt(root, c, new Vector3(Mathf.Sign(l.x) * (hw - inset), y, z - 0.08f)); }
            }
            bool tall = h > 2f;
            Corner("CarBlinkerFront", front + 0.01f, ground + (tall ? 0.75f : h * 0.5f), 0.12f);
            Corner("CarFrontLights", front + 0.01f, ground + (tall ? 0.75f : h * 0.5f), 0.25f);
            Corner("CarBlinkerBack", back - 0.01f, ground + (tall ? 0.9f : h * 0.62f), 0.12f);
            Corner("BreakLights", back - 0.015f, ground + (tall ? 0.9f : h * 0.62f), 0.25f);
            Corner("CarBlinkerMirrors", head.z + 0.55f, head.y - 0.25f, -0.06f);

            // eHMI slots: windshield, roof, hood
            PlaceAt(root, Find(root, "CarLogic/ModelElements/HMI/Windshield"), new Vector3(0f, head.y - 0.13f, head.z + 0.75f));
            PlaceAt(root, Find(root, "CarLogic/ModelElements/HMI/Top"), new Vector3(0f, body.max.y + 0.02f, head.z + 0.3f));
            PlaceAt(root, Find(root, "CarLogic/ModelElements/HMI/Hood"), new Vector3(0f, tall ? ground + h * 0.45f : ground + h * 0.62f, front - (tall ? 0.05f : 0.6f)));
            // objects other scripts refer to (switched per driving mode): moved with the driver, not removed
            MoveBy(root, "CarLogic/ModelElements/RearMirrors", delta);
            foreach (Transform t in root) if (t.name == "LensFlareOccluder" || t.name == "GameObject") t.position = root.TransformPoint(root.InverseTransformPoint(t.position) + new Vector3(0f, delta.y, b.max.z - 2.0f));

            // no interior (opaque windows): the driver and passenger figures stay (scripts use them) but are not drawn
            if (!d.Interior)
                foreach (var n in new[] { "DriverModel&Rig", "PassengerModel" })
                {
                    var t = root.Find(n);
                    if (t != null) foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }

            // physics of the vehicle type; no repainting (these models have their own colours)
            var dyn = go.GetComponent<RealisticVehicleDynamics>();
            if (dyn != null) dyn.Car = d.Preset;
            var conf = go.GetComponent<CarConfigurator>();
            if (conf != null) conf.paintRenderers = new MeshRenderer[0];
            foreach (var r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var path = $"{OutFolder}/{go.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Debug.Log($"VehicleBuilder: {go.name}: {body.size.x:F2} x {body.size.y + (body.min.y - ground):F2} x {body.size.z:F2} m, wheels r {radius:F2} m, wheelbase {colliders[0].localPosition.z - colliders[2].localPosition.z:F2} m, eyes {head}, {d.Preset}");
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // the vehicles join the AI car list (network spawning), traffic, SUMO and the parked cars
    static void Register(List<GameObject> prefabs)
    {
        // loaded again by path (saving prefabs reloads the assets)
        var paths = prefabs.Select(AssetDatabase.GetAssetPath).ToArray();
        AssetDatabase.SaveAssets();
        AICar[] Load() => paths.Select(p => AssetDatabase.LoadAssetAtPath<AICar>(p)).Where(a => a != null).ToArray();
        var ai = Load();
        if (ai.Length == 0) return;
        AICar[] Append(AICar[] list) { ai = Load(); var names = new HashSet<string>(paths.Select(Path.GetFileNameWithoutExtension)); return (list ?? new AICar[0]).Where(a => a != null && !names.Contains(a.name)).Concat(ai).ToArray(); }

        var scene = EditorSceneManager.OpenScene("Assets/_Scenes/StartScene.unity", OpenSceneMode.Single);
        foreach (var nm in UnityEngine.Object.FindObjectsByType<NetworkingManager>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(nm);
            var list = so.FindProperty("_aiCarSystem.Prefabs");
            var current = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as AICar).ToArray();
            var all = Append(current);
            list.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"VehicleBuilder: AI car list now {all.Length} prefabs");
        }
        EditorSceneManager.SaveScene(scene);

        foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ExperimentDefinitions" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var t in root.GetComponentsInChildren<TrafficSpawner>(true)) { t.CarPrefabs = Append(t.CarPrefabs); changed = true; }
            foreach (var t in root.GetComponentsInChildren<SumoBridge>(true)) { t.CarPrefabs = Append(t.CarPrefabs); changed = true; }
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); Debug.Log("VehicleBuilder: traffic in " + path); }
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
