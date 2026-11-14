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
        // detailed models (Wheels = null): wheels found from the tyres' contact with the ground and cut out of the meshes
        public float WheelRadius = 0.32f;    // metres (real tyre size)
        public string[] Remove = new string[0];   // nodes to leave out (helpers, ground planes)
        public bool Combine;                 // merge the body into one mesh per material (models of many small parts)
        public float ArticulationFromFront;  // articulated vehicles: the joint's distance from the front (m); the rear section follows as a trailer
        public string ClusterNode;           // the model's instrument panel: the cluster display is put there
        public Vector3 HeadFromSteeringWheel = new Vector3(0f, 0.386f, -0.489f);   // driver's eyes from the steering wheel's centre (as in the hatchback)
        public string Credit;                // attribution (CC-BY models)
        public bool Passenger = true;        // a passenger figure next to the driver (not in a bus)
    }

    const string Kenney = "Assets/ThirdParty/Kenney/CarKit/";
    const string Detailed = "Assets/ThirdParty/Vehicles/";
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

        // detailed models with interiors (CC BY 4.0, credits in Assets/ThirdParty/Vehicles/CREDITS.md)
        new Def { Name = "ToyotaCorolla", Model = Detailed + "ToyotaCorolla/corolla.glb", Preset = RealisticVehicleDynamics.CarPreset.Hatchback, Length = 4.62f,
                  Wheels = null, WheelRadius = 0.316f, Interior = true, HeadFromSteeringWheel = new Vector3(0f, 0.33f, -0.56f), SteeringWheel = "corolla_e180_steering_wheel", ClusterNode = "corolla_e180_dashboard_M_dashboard.001_0",
                  Credit = "\"2014 Toyota Corolla E180 EU (with interior)\" by Armored Wave" },
        new Def { Name = "LandRoverDefender", Model = Detailed + "LandRoverDefender/scene.gltf", Preset = RealisticVehicleDynamics.CarPreset.Suv, Length = 4.64f,
                  Wheels = null, WheelRadius = 0.385f, Remove = new[] { "Object_669" }, Combine = true, Interior = true, HeadFromFront = 2.1f, HeadHeight = 1.62f, HeadSide = -0.42f,   // right-hand drive
                  Credit = "\"Land Rover Defender 110\" by meeww" },
        new Def { Name = "ArticulatedBus", Model = Detailed + "CityBus/scene.gltf", Preset = RealisticVehicleDynamics.CarPreset.CityBus, Length = 18.0f,
                  Wheels = null, WheelRadius = 0.48f, Combine = true, ArticulationFromFront = 10.6f, Interior = true, Passenger = false, HeadFromFront = 1.15f, HeadHeight = 1.76f, HeadSide = 0.67f,
                  Remove = new[] { "Object_69", "Object_70", "Object_71", "Object_72", "Object_73" },   // the model's own seated driver (our driver figure takes the seat)
                  Credit = "\"A long City Bus (FULL INTERIOR)\" by Mixmamo.studio" },
        new Def { Name = "DafXF95", Model = Detailed + "DafXF95/daf_xf_95.glb", Preset = RealisticVehicleDynamics.CarPreset.Truck, Length = 7.45f,
                  Wheels = null, WheelRadius = 0.53f, Combine = true, Interior = true, HeadFromFront = 1.25f, HeadHeight = 2.45f, HeadSide = 0.82f,
                  Credit = "\"DAF XF 95\" by Kabourek Roman" },
        new Def { Name = "BoxTruck", Model = Detailed + "BoxTruck/box_truck.glb", Preset = RealisticVehicleDynamics.CarPreset.Truck, Length = 7.4f,
                  Wheels = null, WheelRadius = 0.40f, HeadFromFront = 1.0f, HeadHeight = 2.0f, HeadSide = 0.5f,
                  Credit = "\"Box-truck\" by Cristian Rus" },
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
        // -vehicleFilter Name: only the vehicles whose name contains it
        var args = Environment.GetCommandLineArgs();
        int fi = Array.IndexOf(args, "-vehicleFilter");
        var filter = fi >= 0 && fi + 1 < args.Length ? args[fi + 1] : "";
        foreach (var d in Defs.Where(x => filter.Split('|').Any(f => x.Name.Contains(f))))
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(d.Model) == null) { Debug.LogWarning($"VehicleBuilder: no model {d.Model} for {d.Name}"); continue; }
            try { var p = Build(d); if (p != null) built.Add(p); }
            catch (Exception e) { Debug.LogError($"VehicleBuilder: {d.Name}: {e}"); }
        }
        Register(built.Where(p => Defs.First(d => p.name == "Drivable" + d.Name).Traffic).ToList(),
                 built.Where(p => Defs.First(d => p.name == "Drivable" + d.Name).Interior).ToList());
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
            bool auto = d.Wheels == null;

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
            model.transform.localRotation = Quaternion.Euler(0f, d.Yaw, 0f) * model.transform.localRotation;   // keeps the model's own up axis
            foreach (var n in d.Remove)
                foreach (var t in model.GetComponentsInChildren<Transform>(true).Where(t => t.name == n).ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
            Bounds Measure() => auto ? VehicleModelRig.DrawnBounds(root, model) : LocalBounds(root, model.GetComponentsInChildren<Renderer>(true));
            var b0 = Measure();
            float s = d.Length / Mathf.Max(0.01f, b0.size.z);
            var axisScale = d.Width > 0f && d.Height > 0f
                ? new Vector3(d.Width / Mathf.Max(0.01f, b0.size.x), d.Height / Mathf.Max(0.01f, b0.size.y), s)
                : Vector3.one * s;
            model.transform.localScale = Vector3.Scale(model.transform.localScale, axisScale);
            // wheels stay round: their own scale is uniform (the body's height scale), width as the body's
            float wheelScale = axisScale.y;
            var b = Measure();
            model.transform.position += root.TransformVector(new Vector3(-b.center.x, ground - b.min.y, -b.center.z));
            b = Measure();

            // the instrument panel and steering wheel of a model with an interior (found before parts are merged)
            var swNode = d.SteeringWheel != null ? Find(model.transform, d.SteeringWheel) : null;
            if (d.SteeringWheel != null && swNode == null) Debug.LogWarning($"VehicleBuilder: {d.Name}: no steering wheel {d.SteeringWheel}");
            (Vector3 centre, Vector3 normal, Vector2 size)? cluster = null;
            if (d.ClusterNode != null)
            {
                var cn = Find(model.transform, d.ClusterNode);
                var mf = cn != null ? cn.GetComponentInChildren<MeshFilter>(true) : null;
                if (mf != null) cluster = Panel(root, mf, root.forward);
                else Debug.LogWarning($"VehicleBuilder: {d.Name}: no instrument panel {d.ClusterNode}");
            }
            (Vector3 centre, Vector3 normal, Vector2 size)? swPanel = null;
            if (swNode != null)
            {
                var mf = swNode.GetComponentsInChildren<MeshFilter>(true).OrderByDescending(f => f.sharedMesh != null ? f.sharedMesh.bounds.size.sqrMagnitude : 0f).FirstOrDefault();
                if (mf != null) swPanel = Panel(root, mf, root.forward);
            }

            // wheels: a pivot at each wheel's centre (the suspension turns and moves it), colliders there
            var colliders = new[] { "FLWheel (1)", "FRWheel (1)", "BLWheel (1)", "BRWheel (1)" }.Select(n => Find(root, n)).ToArray();
            var wheelRenderers = new List<Renderer>();
            var pivots = new Transform[4];
            float radius = 0.3f;
            VehicleModelRig.Result rig = null;
            if (auto)
            {
                rig = VehicleModelRig.Rig(root, carModel, model, d, ground, OutFolder + "/Meshes");
                pivots = rig.PhysicsPivots;
                radius = d.WheelRadius;
                wheelRenderers.AddRange(rig.WheelRenderers);
            }
            else
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
                    pivots[i] = pivot;
                }
            for (int i = 0; i < 4; i++)
            {
                if (pivots[i] == null) continue;
                var wc = colliders[i].GetComponent<WheelCollider>();
                wc.radius = radius;
                // at rest (spring target 0.5) the wheel sits where the model has it
                colliders[i].position = pivots[i].position + root.up * wc.suspensionDistance * 0.5f;
                var susp = colliders[i].GetComponent<VehicleBehaviour.Suspension>();
                if (susp != null) { susp.wheelModel = pivots[i].gameObject; susp.localRotOffset = Vector3.zero; }
            }
            if (avatar != null && avatar.SyncTransforms != null)
            {
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

            // body collider above the wheels (an articulated vehicle's front section; the rear has its own)
            var body = auto ? b : LocalBounds(root, model.GetComponentsInChildren<Renderer>(true).Except(wheelRenderers));
            float jointZ = rig != null && rig.Trailer != null ? rig.JointZ : float.NegativeInfinity;
            var box = go.GetComponent<BoxCollider>();
            float bottom = ground + radius * 0.9f;
            if (box != null)
            {
                float back = Mathf.Max(body.min.z, jointZ);
                box.center = new Vector3(body.center.x, (bottom + body.max.y) / 2f, (back + body.max.z) / 2f);
                box.size = new Vector3(body.size.x, body.max.y - bottom, body.max.z - back);
            }

            // driver's and passenger's eyes, and what is placed relative to them
            var head = swPanel.HasValue
                ? swPanel.Value.centre + d.HeadFromSteeringWheel
                : new Vector3(-d.HeadSide, ground + d.HeadHeight, b.max.z - d.HeadFromFront);
            var delta = head - oldHead;
            var pDelta = new Vector3(-delta.x, delta.y, delta.z);   // passenger: mirrored across the centre
            foreach (var p in new[] { "DriverLogic/CameraPositionSet", "CarLogic/HeadPosition", "CarLogic/HeadTargetAnchor", "DriverModel&Rig/Driver",
                                      "CarLogic/ModelElements/Dashboard", "CarLogic/ModelElements/steering wheel" })
                MoveBy(root, p, delta);
            foreach (var p in new[] { "PassengerLogic/CameraPositionSet", "PassengerModel/BlueSuitFree01" }) MoveBy(root, p, pDelta);

            // the steering wheel: the model's own (turned by WheelVehicle about its column), else an empty stand-in
            var sw = go.GetComponentInChildren<SteeringWheel>(true);
            Transform wheelVisual;
            if (swNode != null && swPanel.HasValue && sw != null && sw.pivot != null)
            {
                // the pivot at the wheel's centre, its z along the column (towards the dashboard); WheelVehicle
                // sets the turning part's local rotation about z, so the model's wheel goes under a part at rest
                var c = root.TransformPoint(swPanel.Value.centre);
                var column = root.TransformDirection(swPanel.Value.normal);
                sw.transform.position = c;
                sw.pivot.SetPositionAndRotation(c, Quaternion.LookRotation(column, root.up));
                wheelVisual = new GameObject("Steering wheel (turns)").transform;
                wheelVisual.SetParent(sw.pivot, false);
                swNode.SetParent(wheelVisual, true);
            }
            else
            {
                wheelVisual = new GameObject("Steering wheel (none)").transform;
                wheelVisual.SetParent(sw != null ? sw.transform : root, false);
            }
            if (sw != null) sw.steeringWheel = wheelVisual;
            var wv = go.GetComponent<VehicleBehaviour.WheelVehicle>();
            if (wv != null) wv.steeringWheel = swNode != null ? wheelVisual : null;

            // the instrument cluster display: on the model's own instrument panel, else behind the steering wheel
            if (auto)
            {
                var dash = Find(root, "CarLogic/ModelElements/Dashboard") as RectTransform;
                if (dash != null)
                {
                    // no named panel: where the driver looks through the top of the steering wheel, on the first surface
                    var look = new Vector3(0f, -0.36f, 1f).normalized;
                    Vector3? hit = null;
                    foreach (var down in new[] { 0.36f, 0.5f, 0.7f, 0.9f })
                    {
                        var dir = new Vector3(0f, -down, 1f).normalized;
                        hit = rig.Hit(head, dir, 0.45f, 1.6f);
                        if (hit != null) { look = dir; break; }
                    }
                    var place = cluster ?? ((hit ?? head + look * 0.8f) - look * 0.01f, look, new Vector2(0.24f, 0.09f));
                    dash.SetPositionAndRotation(root.TransformPoint(place.centre), Quaternion.LookRotation(root.TransformDirection(place.normal), root.up));
                    dash.sizeDelta = place.size / Mathf.Max(1e-6f, dash.lossyScale.x);
                    // the hatchback's own speedometer (a mesh) is not part of this model
                    foreach (var mr in dash.GetComponentsInChildren<MeshRenderer>(true)) mr.enabled = false;
                }
            }

            // blinkers and lights at the corners of the body
            float hw = body.extents.x, front = body.max.z, backZ = body.min.z, h = body.max.y - ground;
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
            if (rig == null)
            {
                Corner("CarBlinkerFront", front + 0.01f, ground + (tall ? 0.75f : h * 0.5f), 0.12f);
                Corner("CarFrontLights", front + 0.01f, ground + (tall ? 0.75f : h * 0.5f), 0.25f);
                Corner("CarBlinkerBack", backZ - 0.01f, ground + (tall ? 0.9f : h * 0.62f), 0.12f);
                Corner("BreakLights", backZ - 0.015f, ground + (tall ? 0.9f : h * 0.62f), 0.25f);
                Corner("CarBlinkerMirrors", head.z + 0.55f, head.y - 0.25f, -0.06f);
            }
            else
            {
                // detailed models: small lamps on the body's own surface (in the model's light clusters)
                void OnSurface(string group, int end, float y, float inset)
                {
                    var g = Find(root, "CarLogic/ModelElements/" + group);
                    if (g == null) return;
                    foreach (Transform c in g)
                    {
                        int side = c.name.Contains("Left") ? -1 : c.name.Contains("Right") ? 1 : (int)Mathf.Sign(root.InverseTransformPoint(c.position).x);
                        var p = rig.Corner(end, side, y, inset);
                        if (p == null) continue;
                        if (c.name.Contains("Left") || c.name.Contains("Right")) c.localScale *= 0.35f;
                        PlaceAt(root, c, c.name.Contains("Flare") ? p.Value - Vector3.forward * end * 0.08f : p.Value);
                    }
                }
                float yFront = ground + (tall ? 0.75f : h * 0.48f), yBack = ground + (tall ? 0.9f : h * 0.66f);
                OnSurface("CarBlinkerFront", 1, yFront, 0.1f);
                OnSurface("CarFrontLights", 1, yFront, 0.3f);
                OnSurface("CarBlinkerBack", -1, yBack, 0.08f);
                OnSurface("BreakLights", -1, yBack, 0.25f);
                var mg = Find(root, "CarLogic/ModelElements/CarBlinkerMirrors");
                if (mg != null)
                    foreach (Transform c in mg)
                    {
                        int side = c.name.Contains("Left") ? -1 : 1;
                        var p = rig.Outermost(side, head.y - 0.45f, head.y - 0.02f, head.z + 0.15f, head.z + 1.1f);
                        if (p == null) continue;
                        c.localScale *= 0.35f;
                        PlaceAt(root, c, p.Value - new Vector3(side * 0.02f, 0f, 0f));
                    }
            }

            // eHMI slots: windshield, roof, hood
            PlaceAt(root, Find(root, "CarLogic/ModelElements/HMI/Windshield"), new Vector3(0f, head.y - 0.13f, head.z + 0.75f));
            PlaceAt(root, Find(root, "CarLogic/ModelElements/HMI/Top"), new Vector3(0f, body.max.y + 0.02f, Mathf.Min(head.z + 0.3f, body.max.z - 0.3f)));
            PlaceAt(root, Find(root, "CarLogic/ModelElements/HMI/Hood"), new Vector3(0f, tall ? ground + h * 0.45f : ground + h * 0.62f, front - (tall ? 0.05f : 0.6f)));
            // objects other scripts refer to (switched per driving mode): moved with the driver, not removed
            MoveBy(root, "CarLogic/ModelElements/RearMirrors", delta);
            foreach (Transform t in root) if (t.name == "LensFlareOccluder" || t.name == "GameObject") t.position = root.TransformPoint(root.InverseTransformPoint(t.position) + new Vector3(0f, delta.y, b.max.z - 2.0f));

            // articulated: the rear section follows as a trailer, with the rear lamps and its own collider
            if (rig != null && rig.Trailer != null)
            {
                var tr = rig.Trailer;
                foreach (var gname in new[] { "CarBlinkerBack", "BreakLights" }) { var g = Find(root, "CarLogic/ModelElements/" + gname); if (g != null) g.SetParent(tr, true); }
                var tb = tr.gameObject.AddComponent<BoxCollider>();
                var lo = tr.InverseTransformPoint(root.TransformPoint(new Vector3(body.min.x, bottom, body.min.z)));
                var hi = tr.InverseTransformPoint(root.TransformPoint(new Vector3(body.max.x, body.max.y, jointZ - 0.3f)));
                tb.center = (lo + hi) / 2f; tb.size = new Vector3(Mathf.Abs(hi.x - lo.x), Mathf.Abs(hi.y - lo.y), Mathf.Abs(hi.z - lo.z));
                var art = go.AddComponent<ArticulatedTrailer>();
                art.Trailer = tr;
                art.HitchLocal = root.InverseTransformPoint(tr.position);
                art.AxleDistance = jointZ - rig.TrailerAxleZ;
                art.Wheels = rig.TrailerWheels.ToArray();
                art.WheelRadius = radius;
                foreach (var t in tr.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = go.layer;
            }

            // no interior (opaque windows): the driver and passenger figures stay (scripts use them) but are not drawn
            if (!d.Interior || !d.Passenger)
                foreach (var n in d.Interior ? new[] { "PassengerModel" } : new[] { "DriverModel&Rig", "PassengerModel" })
                {
                    var t = root.Find(n);
                    if (t != null) foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }

            // physics of the vehicle type; no repainting (these models have their own colours)
            var dyn = go.GetComponent<RealisticVehicleDynamics>();
            if (dyn != null) dyn.Car = d.Preset;
            var conf = go.GetComponent<CarConfigurator>();
            if (conf != null) conf.paintRenderers = new MeshRenderer[0];
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.transform.IsChildOf(carModel) || (rig?.Trailer != null && r.transform.IsChildOf(rig.Trailer))) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var path = $"{OutFolder}/{go.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Debug.Log($"VehicleBuilder: {go.name}: {body.size.x:F2} x {body.max.y - ground:F2} x {body.size.z:F2} m, wheels r {radius:F2} m, wheelbase {colliders[0].localPosition.z - colliders[2].localPosition.z:F2} m, eyes {head}, {d.Preset}" + (rig?.Trailer != null ? $", articulated at {jointZ:F2}" : ""));
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// <summary>A flat part (instrument panel, steering wheel) in root space: its centre, the normal of its
    /// plane pointing along <paramref name="away"/> (away from the driver), and its width and height.</summary>
    static (Vector3 centre, Vector3 normal, Vector2 size) Panel(Transform root, MeshFilter mf, Vector3 away)
    {
        var mb = mf.sharedMesh.bounds;
        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        var sizes = new[] { mb.size.x * mf.transform.lossyScale.x, mb.size.y * mf.transform.lossyScale.y, mb.size.z * mf.transform.lossyScale.z }.Select(Mathf.Abs).ToArray();
        var order = Enumerable.Range(0, 3).OrderBy(i => sizes[i]).ToArray();
        var normal = root.InverseTransformDirection(mf.transform.TransformDirection(axes[order[0]])).normalized;
        if (Vector3.Dot(normal, root.InverseTransformDirection(away)) < 0f) normal = -normal;
        var centre = root.InverseTransformPoint(mf.transform.TransformPoint(mb.center));
        return (centre, normal, new Vector2(sizes[order[2]], sizes[order[1]]));
    }

    // the vehicles join the AI car list (network spawning), traffic, SUMO and the parked cars
    static void Register(List<GameObject> prefabs, List<GameObject> drivers)
    {
        var driverPaths = drivers.Select(AssetDatabase.GetAssetPath).ToArray();
        // loaded again by path (saving prefabs reloads the assets)
        var paths = prefabs.Select(AssetDatabase.GetAssetPath).ToArray();
        AssetDatabase.SaveAssets();
        AICar[] Load() => paths.Select(p => AssetDatabase.LoadAssetAtPath<AICar>(p)).Where(a => a != null).ToArray();
        var ai = Load();
        // (traffic lists below stay as they are when no traffic vehicle was built)
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
        // vehicles with an interior: participants can drive them (-driverCar Name, or the role's car index)
        foreach (var ps in UnityEngine.Object.FindObjectsByType<PlayerSystem>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(ps);
            var list = so.FindProperty("_AvatarPrefabDriver");
            var names = new HashSet<string>(driverPaths.Select(Path.GetFileNameWithoutExtension));
            var current = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as PlayerAvatar).Where(a => a != null && !names.Contains(a.name));
            var all = current.Concat(driverPaths.Select(p => AssetDatabase.LoadAssetAtPath<PlayerAvatar>(p)).Where(a => a != null)).ToArray();
            list.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"VehicleBuilder: driver cars now {all.Length}: {string.Join(", ", all.Select(a => a.name))}");
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
