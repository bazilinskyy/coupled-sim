using System.Collections.Generic;
using System.Linq;
using System.IO;
using CoupledSim.TwoWheelers;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the bicycle, e-scooter and moped avatar prefabs: a vehicle model made from meshes in code
/// (torus tyres with spokes, frame tubes, steering assembly on a tilted head tube, saddle, cranks,
/// lights) with URP materials, a Rocketbox rider posed with IK, a head camera, TwoWheelerController
/// and PlayerAvatar. Menu: Coupled Sim ➡️ Two-wheelers ➡️ Build avatar prefabs.
/// </summary>
public static class TwoWheelerPrefabBuilder
{
    const string Dir = "Assets/Prefabs/TwoWheelers";
    const string MeshDir = Dir + "/Meshes";
    const string MatDir = Dir + "/Materials";

    static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
    static Mesh _cylinder;

    [MenuItem("Coupled Sim/Two-wheelers/Build avatar prefabs")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(MeshDir);
        Directory.CreateDirectory(MatDir);
        _materials.Clear();
        _cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        Build(TwoWheelerType.Bicycle, "Bicycle", new Color(0.08f, 0.16f, 0.3f), "Female_Adult_04");
        Build(TwoWheelerType.EScooter, "EScooter", new Color(0.12f, 0.12f, 0.13f), "Male_Adult_05");
        Build(TwoWheelerType.Moped25, "Moped", new Color(0.62f, 0.75f, 0.7f), "Male_Adult_09");
        AssetDatabase.SaveAssets();
        BuildAIBicycle();
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ materials and meshes

    static Material Mat(string name, Color color, float smooth, float metal = 0f, bool emissive = false)
    {
        if (_materials.TryGetValue(name, out var m)) return m;
        var path = $"{MatDir}/{name}.mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2f);
        }
        EditorUtility.SetDirty(m);
        _materials[name] = m;
        return m;
    }

    static Mesh Torus(string name, float radius, float tube, int seg = 48, int side = 12)
    {
        var path = $"{MeshDir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * 2f * Mathf.PI / seg;
            // wheel plane: y-z, axle along x
            var centre = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a)) * radius;
            for (int j = 0; j <= side; j++)
            {
                float b = j * 2f * Mathf.PI / side;
                var n = centre.normalized * Mathf.Cos(b) + Vector3.right * Mathf.Sin(b);
                verts.Add(centre + n * tube);
                normals.Add(n);
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < side; j++)
            {
                int a0 = i * (side + 1) + j, a1 = a0 + side + 1;
                tris.AddRange(new[] { a0, a1, a0 + 1, a0 + 1, a1, a1 + 1 });
            }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static GameObject Part(string name, Transform parent, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return go;
    }

    /// <summary>A tube (cylinder) between two local points.</summary>
    static GameObject Tube(string name, Transform parent, Vector3 a, Vector3 b, float diameter, Material mat)
    {
        var go = Part(name, parent, _cylinder, mat);
        go.transform.localPosition = (a + b) / 2f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        go.transform.localScale = new Vector3(diameter, (b - a).magnitude / 2f, diameter);
        return go;
    }

    static GameObject Box(string name, Transform parent, Vector3 centre, Vector3 size, Material mat, Vector3 euler = default)
    {
        var go = Part(name, parent, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), mat);
        go.transform.localPosition = centre;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        return go;
    }

    static GameObject Ball(string name, Transform parent, Vector3 centre, Vector3 size, Material mat)
    {
        var go = Part(name, parent, Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), mat);
        go.transform.localPosition = centre;
        go.transform.localScale = size;
        return go;
    }

    static Transform Empty(string name, Transform parent, Vector3 pos, Quaternion rot = default)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = rot == default ? Quaternion.identity : rot;
        return t;
    }

    /// <summary>Wheel with tyre, rim, hub and spokes, spinning around local x.</summary>
    static Transform Wheel(string name, Transform parent, Vector3 axle, float radius, float tyre, int spokes)
    {
        var w = Empty(name, parent, axle);
        Part("Tyre", w, Torus($"Tyre_{radius:0.00}_{tyre:0.000}", radius - tyre, tyre), Mat("Rubber", new Color(0.05f, 0.05f, 0.05f), 0.25f));
        Part("Rim", w, Torus($"Rim_{radius:0.00}", radius - tyre * 2.1f, tyre * 0.35f, 48, 8), Mat("Aluminium", new Color(0.75f, 0.76f, 0.78f), 0.8f, 1f));
        var hub = Tube("Hub", w, new Vector3(-0.05f, 0, 0), new Vector3(0.05f, 0, 0), 0.05f, Mat("Aluminium", Color.gray, 0.8f, 1f));
        for (int i = 0; i < spokes; i++)
        {
            float a = i * 2f * Mathf.PI / spokes;
            var rim = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a)) * (radius - tyre * 2.1f);
            Tube($"Spoke {i}", w, new Vector3((i % 2 == 0 ? 0.03f : -0.03f), 0, 0), rim, 0.004f, Mat("Steel", new Color(0.8f, 0.8f, 0.82f), 0.85f, 1f));
        }
        return w;
    }

    // ------------------------------------------------------------------ vehicles

    class Rig
    {
        public Transform Root, Lean, Steering, FrontWheel, RearWheel, Crank;
        public Transform LeftGrip, RightGrip, LeftFoot, RightFoot, Seat, Head;
        public float WheelRadius;
        public bool Seated = true;
    }

    static Rig BuildBicycle(Transform root, Color paint)
    {
        var r = new Rig { Root = root, WheelRadius = 0.35f };
        var frame = Mat("BikePaint", paint, 0.6f, 0.3f);
        var steel = Mat("Steel", new Color(0.8f, 0.8f, 0.82f), 0.85f, 1f);
        var black = Mat("Rubber", new Color(0.05f, 0.05f, 0.05f), 0.25f);
        r.Lean = Empty("Lean", root, Vector3.zero);
        var rear = new Vector3(0, 0.35f, -0.55f);
        var bb = new Vector3(0, 0.3f, -0.05f);
        var seatTop = new Vector3(0, 0.85f, -0.22f);
        var headTop = new Vector3(0, 0.98f, 0.36f);
        var headBottom = new Vector3(0, 0.74f, 0.44f);
        r.RearWheel = Wheel("Rear wheel", r.Lean, rear, 0.35f, 0.022f, 32);
        Tube("Seat tube", r.Lean, bb, seatTop, 0.035f, frame);
        Tube("Down tube", r.Lean, bb, headBottom, 0.04f, frame);
        Tube("Top tube (step-through)", r.Lean, new Vector3(0, 0.55f, -0.12f), new Vector3(0, 0.85f, 0.39f), 0.035f, frame);
        Tube("Head tube", r.Lean, headBottom, headTop, 0.045f, frame);
        Tube("Chain stay L", r.Lean, bb + new Vector3(-0.04f, 0, 0), rear + new Vector3(-0.06f, 0, 0), 0.02f, frame);
        Tube("Chain stay R", r.Lean, bb + new Vector3(0.04f, 0, 0), rear + new Vector3(0.06f, 0, 0), 0.02f, frame);
        Tube("Seat stay L", r.Lean, seatTop + new Vector3(-0.02f, -0.06f, 0), rear + new Vector3(-0.06f, 0, 0), 0.018f, frame);
        Tube("Seat stay R", r.Lean, seatTop + new Vector3(0.02f, -0.06f, 0), rear + new Vector3(0.06f, 0, 0), 0.018f, frame);
        Tube("Seat post", r.Lean, seatTop, seatTop + new Vector3(0, 0.12f, -0.03f), 0.027f, steel);
        r.Seat = Empty("Seat", r.Lean, seatTop + new Vector3(0, 0.15f, -0.05f));
        Box("Saddle", r.Lean, seatTop + new Vector3(0, 0.15f, -0.03f), new Vector3(0.17f, 0.06f, 0.26f), Mat("Saddle", new Color(0.12f, 0.08f, 0.05f), 0.35f));
        Box("Rear rack", r.Lean, rear + new Vector3(0, 0.38f, 0.02f), new Vector3(0.14f, 0.015f, 0.38f), steel);
        Box("Rear light", r.Lean, rear + new Vector3(0, 0.4f, -0.2f), new Vector3(0.06f, 0.03f, 0.02f), Mat("RearLight", new Color(0.8f, 0.05f, 0.05f), 0.6f, 0, true));
        Box("Chain guard", r.Lean, (bb + rear) / 2f + new Vector3(0.07f, 0.03f, 0), new Vector3(0.01f, 0.12f, 0.6f), frame);

        // steering assembly rotates about the head tube axis
        var axis = (headTop - headBottom).normalized;
        r.Steering = Empty("Steering", r.Lean, headBottom, Quaternion.FromToRotation(Vector3.up, axis));
        var front = r.Steering.InverseTransformPoint(r.Lean.TransformPoint(new Vector3(0, 0.35f, 0.55f)));
        Tube("Fork L", r.Steering, new Vector3(-0.05f, 0.02f, 0), front + new Vector3(-0.05f, 0, 0), 0.025f, steel);
        Tube("Fork R", r.Steering, new Vector3(0.05f, 0.02f, 0), front + new Vector3(0.05f, 0, 0), 0.025f, steel);
        r.FrontWheel = Wheel("Front wheel", r.Steering, front, 0.35f, 0.022f, 32);
        var stemTop = new Vector3(0, (headTop - headBottom).magnitude + 0.14f, 0);
        Tube("Stem", r.Steering, new Vector3(0, (headTop - headBottom).magnitude, 0), stemTop, 0.025f, steel);
        var barL = stemTop + new Vector3(-0.3f, 0.02f, -0.14f);
        var barR = stemTop + new Vector3(0.3f, 0.02f, -0.14f);
        Tube("Handlebar L", r.Steering, stemTop, barL, 0.022f, steel);
        Tube("Handlebar R", r.Steering, stemTop, barR, 0.022f, steel);
        Tube("Grip L", r.Steering, barL, barL + new Vector3(0.1f, 0, 0.02f), 0.032f, black);
        Tube("Grip R", r.Steering, barR, barR + new Vector3(-0.1f, 0, 0.02f), 0.032f, black);
        Box("Head light", r.Steering, stemTop + new Vector3(0, -0.12f, 0.08f), new Vector3(0.07f, 0.06f, 0.05f), Mat("HeadLight", new Color(1f, 0.95f, 0.85f), 0.8f, 0, true));
        r.LeftGrip = Empty("Left grip", r.Steering, barL + new Vector3(0.05f, 0, 0.01f));
        r.RightGrip = Empty("Right grip", r.Steering, barR + new Vector3(-0.05f, 0, 0.01f));

        // crank with pedals (they stay level when the crank turns)
        r.Crank = Empty("Crank", r.Lean, bb);
        Tube("Crank arm R", r.Crank, new Vector3(0.08f, 0, 0), new Vector3(0.08f, 0.17f, 0), 0.02f, steel);
        Tube("Crank arm L", r.Crank, new Vector3(-0.08f, 0, 0), new Vector3(-0.08f, -0.17f, 0), 0.02f, steel);
        Part("Chainring", r.Crank, Torus("Chainring", 0.09f, 0.006f, 32, 6), steel).transform.localPosition = new Vector3(0.06f, 0, 0);
        r.RightFoot = Empty("Right pedal", r.Crank, new Vector3(0.13f, 0.17f, 0));
        r.LeftFoot = Empty("Left pedal", r.Crank, new Vector3(-0.13f, -0.17f, 0));
        Box("Pedal R", r.RightFoot, Vector3.zero, new Vector3(0.09f, 0.02f, 0.06f), black);
        Box("Pedal L", r.LeftFoot, Vector3.zero, new Vector3(0.09f, 0.02f, 0.06f), black);
        return r;
    }

    static Rig BuildEScooter(Transform root, Color paint)
    {
        var r = new Rig { Root = root, WheelRadius = 0.1f, Seated = false };
        var body = Mat("ScooterPaint", paint, 0.5f, 0.2f);
        var steel = Mat("Steel", new Color(0.8f, 0.8f, 0.82f), 0.85f, 1f);
        var black = Mat("Rubber", new Color(0.05f, 0.05f, 0.05f), 0.25f);
        r.Lean = Empty("Lean", root, Vector3.zero);
        r.RearWheel = Wheel("Rear wheel", r.Lean, new Vector3(0, 0.1f, -0.42f), 0.1f, 0.03f, 6);
        Box("Deck", r.Lean, new Vector3(0, 0.12f, -0.02f), new Vector3(0.17f, 0.05f, 0.58f), body);
        Box("Grip tape", r.Lean, new Vector3(0, 0.147f, -0.04f), new Vector3(0.15f, 0.005f, 0.5f), black);
        Box("Rear mudguard", r.Lean, new Vector3(0, 0.22f, -0.43f), new Vector3(0.06f, 0.01f, 0.2f), body, new Vector3(-12, 0, 0));
        Box("Rear light", r.Lean, new Vector3(0, 0.2f, -0.52f), new Vector3(0.05f, 0.02f, 0.01f), Mat("RearLight", new Color(0.8f, 0.05f, 0.05f), 0.6f, 0, true));
        var headBottom = new Vector3(0, 0.16f, 0.32f);
        var axis = Quaternion.Euler(-12, 0, 0) * Vector3.up;
        r.Steering = Empty("Steering", r.Lean, headBottom, Quaternion.FromToRotation(Vector3.up, axis));
        var front = r.Steering.InverseTransformPoint(r.Lean.TransformPoint(new Vector3(0, 0.1f, 0.42f)));
        Tube("Fork", r.Steering, Vector3.zero, front + new Vector3(0.03f, 0, 0), 0.03f, steel);
        r.FrontWheel = Wheel("Front wheel", r.Steering, front, 0.1f, 0.03f, 6);
        Tube("Stem", r.Steering, Vector3.zero, new Vector3(0, 0.95f, 0), 0.04f, body);
        Tube("Handlebar", r.Steering, new Vector3(-0.27f, 0.95f, 0), new Vector3(0.27f, 0.95f, 0), 0.025f, black);
        Box("Display", r.Steering, new Vector3(0, 0.96f, 0.02f), new Vector3(0.07f, 0.03f, 0.05f), black);
        Box("Head light", r.Steering, new Vector3(0, 0.8f, 0.035f), new Vector3(0.05f, 0.03f, 0.02f), Mat("HeadLight", new Color(1f, 0.95f, 0.85f), 0.8f, 0, true));
        r.LeftGrip = Empty("Left grip", r.Steering, new Vector3(-0.22f, 0.95f, 0));
        r.RightGrip = Empty("Right grip", r.Steering, new Vector3(0.22f, 0.95f, 0));
        // standing: feet on the deck, one behind the other
        r.LeftFoot = Empty("Left foot", r.Lean, new Vector3(-0.04f, 0.16f, 0.12f));
        r.RightFoot = Empty("Right foot", r.Lean, new Vector3(0.04f, 0.16f, -0.18f));
        r.Seat = Empty("Stand", r.Lean, new Vector3(0, 0.15f, -0.03f));
        return r;
    }

    static Rig BuildMoped(Transform root, Color paint)
    {
        var r = new Rig { Root = root, WheelRadius = 0.24f };
        var body = Mat("MopedPaint", paint, 0.75f, 0.1f);
        var steel = Mat("Steel", new Color(0.8f, 0.8f, 0.82f), 0.85f, 1f);
        var black = Mat("Rubber", new Color(0.05f, 0.05f, 0.05f), 0.25f);
        var seat = Mat("Saddle", new Color(0.12f, 0.08f, 0.05f), 0.35f);
        r.Lean = Empty("Lean", root, Vector3.zero);
        r.RearWheel = Wheel("Rear wheel", r.Lean, new Vector3(0, 0.24f, -0.62f), 0.24f, 0.05f, 5);
        Ball("Engine cover", r.Lean, new Vector3(0, 0.48f, -0.5f), new Vector3(0.42f, 0.42f, 0.7f), body);
        Box("Floorboard", r.Lean, new Vector3(0, 0.27f, 0.05f), new Vector3(0.3f, 0.06f, 0.55f), black);
        Box("Seat", r.Lean, new Vector3(0, 0.78f, -0.42f), new Vector3(0.28f, 0.1f, 0.62f), seat);
        Box("Rear light", r.Lean, new Vector3(0, 0.62f, -0.86f), new Vector3(0.12f, 0.05f, 0.02f), Mat("RearLight", new Color(0.8f, 0.05f, 0.05f), 0.6f, 0, true));
        Box("Leg shield", r.Lean, new Vector3(0, 0.6f, 0.36f), new Vector3(0.38f, 0.65f, 0.05f), body, new Vector3(-15, 0, 0));
        Box("Number plate", r.Lean, new Vector3(0, 0.55f, -0.88f), new Vector3(0.18f, 0.08f, 0.01f), Mat("PlateBlue", new Color(0.2f, 0.45f, 0.75f), 0.4f));
        r.Seat = Empty("Seat", r.Lean, new Vector3(0, 0.85f, -0.36f));
        var headBottom = new Vector3(0, 0.55f, 0.42f);
        var axis = Quaternion.Euler(-25, 0, 0) * Vector3.up;
        r.Steering = Empty("Steering", r.Lean, headBottom, Quaternion.FromToRotation(Vector3.up, axis));
        var front = r.Steering.InverseTransformPoint(r.Lean.TransformPoint(new Vector3(0, 0.24f, 0.66f)));
        Tube("Fork", r.Steering, Vector3.zero, front + new Vector3(0.06f, 0, 0), 0.05f, steel);
        Box("Front mudguard", r.Steering, front + new Vector3(0, 0.27f, 0), new Vector3(0.14f, 0.04f, 0.4f), body);
        r.FrontWheel = Wheel("Front wheel", r.Steering, front, 0.24f, 0.05f, 5);
        Tube("Steering column", r.Steering, Vector3.zero, new Vector3(0, 0.5f, 0), 0.06f, body);
        Box("Handlebar cover", r.Steering, new Vector3(0, 0.52f, -0.04f), new Vector3(0.5f, 0.08f, 0.14f), body);
        Ball("Head light", r.Steering, new Vector3(0, 0.52f, 0.04f), new Vector3(0.14f, 0.12f, 0.06f), Mat("HeadLight", new Color(1f, 0.95f, 0.85f), 0.8f, 0, true));
        Tube("Grip L", r.Steering, new Vector3(-0.25f, 0.52f, -0.06f), new Vector3(-0.36f, 0.52f, -0.08f), 0.035f, black);
        Tube("Grip R", r.Steering, new Vector3(0.25f, 0.52f, -0.06f), new Vector3(0.36f, 0.52f, -0.08f), 0.035f, black);
        Ball("Mirror L", r.Steering, new Vector3(-0.3f, 0.75f, -0.02f), new Vector3(0.09f, 0.06f, 0.02f), steel);
        Ball("Mirror R", r.Steering, new Vector3(0.3f, 0.75f, -0.02f), new Vector3(0.09f, 0.06f, 0.02f), steel);
        r.LeftGrip = Empty("Left grip", r.Steering, new Vector3(-0.31f, 0.52f, -0.07f));
        r.RightGrip = Empty("Right grip", r.Steering, new Vector3(0.31f, 0.52f, -0.07f));
        r.LeftFoot = Empty("Left foot", r.Lean, new Vector3(-0.11f, 0.31f, 0.12f));
        r.RightFoot = Empty("Right foot", r.Lean, new Vector3(0.11f, 0.31f, 0.12f));
        return r;
    }

    // ------------------------------------------------------------------ prefab

    static void Build(TwoWheelerType type, string name, Color paint, string riderModel)
    {
        var root = new GameObject(name);
        try
        {
            var rig = type == TwoWheelerType.Bicycle ? BuildBicycle(root.transform, paint)
                : type == TwoWheelerType.EScooter ? BuildEScooter(root.transform, paint)
                : BuildMoped(root.transform, paint);

            // rider: a Rocketbox avatar, hips at the seat (or standing on the deck), limbs by IK
            var riderAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{RocketboxImporter.Root}{riderModel}/{riderModel}.fbx");
            Transform head = null;
            if (riderAsset != null)
            {
                var rider = (GameObject)PrefabUtility.InstantiatePrefab(riderAsset);
                PrefabUtility.UnpackPrefabInstance(rider, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                rider.name = "Rider";
                rider.transform.SetParent(rig.Lean, false);
                var animator = rider.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Prefabs/AIPedestrians/Pedestrian.controller");
                animator.applyRootMotion = false;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                float hipHeight = hips != null ? hips.position.y - rider.transform.position.y : 0.95f;
                // seated: hips just above the saddle; standing: feet on the deck
                rider.transform.localPosition = rig.Seated
                    ? rig.Seat.localPosition - new Vector3(0, hipHeight - 0.08f, type == TwoWheelerType.Bicycle ? 0.02f : 0f)
                    : rig.Seat.localPosition;
                rider.transform.localRotation = Quaternion.identity;
                var ik = rider.AddComponent<RiderIK>();
                ik.LeftHand = rig.LeftGrip;
                ik.RightHand = rig.RightGrip;
                ik.LeftFoot = rig.LeftFoot;
                ik.RightFoot = rig.RightFoot;
                ik.LeftKnee = Empty("Left knee hint", rig.Lean, rig.Seat.localPosition + new Vector3(-0.15f, -0.25f, 0.6f));
                ik.RightKnee = Empty("Right knee hint", rig.Lean, rig.Seat.localPosition + new Vector3(0.15f, -0.25f, 0.6f));
                ik.LookTarget = Empty("Look target", rig.Root, new Vector3(0, 1.2f, 15f));
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            // camera at the rider's eyes
            float eyeHeight = head != null ? head.position.y - root.transform.position.y + 0.08f : (rig.Seated ? 1.55f : 1.75f);
            float eyeForward = head != null ? head.position.z - root.transform.position.z + 0.1f : 0f;
            var cameraRig = Empty("Camera rig", rig.Root, Vector3.zero);
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(cameraRig, false);
            camGo.transform.localPosition = new Vector3(0, eyeHeight, eyeForward);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            camGo.SetActive(false); // switched on for the local player

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.mass = TwoWheelerParameters.Preset(type).Mass;
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.3f;
            col.height = type == TwoWheelerType.Moped25 ? 1.8f : 1.6f;
            col.center = new Vector3(0, 0.7f, 0);

            var ctrl = root.AddComponent<TwoWheelerController>();
            ctrl.Type = type;
            ctrl.Parameters = TwoWheelerParameters.Preset(type);
            ctrl.LeanPivot = rig.Lean;
            ctrl.SteeringPivot = rig.Steering;
            ctrl.FrontWheel = rig.FrontWheel;
            ctrl.RearWheel = rig.RearWheel;
            ctrl.Crank = rig.Crank;
            ctrl.WheelRadius = rig.WheelRadius;
            ctrl.CameraRig = cameraRig;
            ctrl.ReadInput = true;
            ctrl.enabled = false; // enabled for the local player by PlayerAvatar (PlayerAsDriver)

            // PlayerAvatar: logged and synced like a vehicle; the moving parts are synced too
            var avatar = root.AddComponent<PlayerAvatar>();
            avatar.Type = AvatarType.Driver;
            avatar.cameras = new[] { cam };
            var sync = new List<Transform> { rig.Lean, rig.Steering, rig.FrontWheel, rig.RearWheel };
            if (rig.Crank != null) sync.Add(rig.Crank);
            avatar.SyncTransforms = sync.ToArray();
            avatar.PlayerAsDriver = new PlayerAvatar.ModeElements { gameObjects = new GameObject[0], monoBehaviours = new MonoBehaviour[] { ctrl } };
            avatar.HostDrivenAIElements = new PlayerAvatar.ModeElements { gameObjects = new GameObject[0], monoBehaviours = new MonoBehaviour[0] };

            var path = $"{Dir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("TwoWheelerPrefabBuilder: built " + path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Registers the two-wheeler prefabs in StartScene's PlayerSystem and appends Cyclist, E-scooter
    /// rider and Moped rider roles to the given experiments (existing role indices stay the same).
    /// </summary>
    [MenuItem("Coupled Sim/Two-wheelers/Add rider roles to Base and the examples")]
    public static void AddRoles()
    {
        // open the scene first: opening a scene unloads assets that nothing references yet
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Scenes/StartScene.unity");
        var prefabs = new[] { "Bicycle", "EScooter", "Moped" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{n}.prefab").GetComponent<PlayerAvatar>()).ToArray();
        var ps = Object.FindAnyObjectByType<PlayerSystem>();
        var so = new SerializedObject(ps);
        var list = so.FindProperty("_AvatarPrefabTwoWheeler");
        list.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        foreach (var exp in new[] { "Base", "Traffic", "SumoTraffic", "PedestrianCrossings" })
        {
            var path = $"Assets/ExperimentDefinitions/{exp}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var def = root.GetComponent<ExperimentDefinition>();
                var roles = def.Roles.ToList();
                if (roles.Any(r => r.SpawnPoint.Type == SpawnPointType.PlayerRidingTwoWheeler)) continue;
                var spawnParent = root.transform.Find("SpawnPoints") ?? root.transform;
                var names = new[] { "Cyclist", "E-scooter rider", "Moped rider" };
                for (int i = 0; i < names.Length; i++)
                {
                    // right edge of the westbound lane south of the central block (MainScene)
                    var point = new GameObject(names[i] + " spawn").transform;
                    point.SetParent(spawnParent, true);
                    point.position = new Vector3(-50f + 6f * i, 0f, 11.6f);
                    point.rotation = Quaternion.Euler(0, 270f, 0);
                    roles.Add(new ExperimentRoleDefinition
                    {
                        Name = names[i],
                        twoWheelerIdx = i,
                        SpawnPoint = new SpawnPoint { Point = point, Type = SpawnPointType.PlayerRidingTwoWheeler, CameraIndex = 0, VehicleType = PlayerSystem.VehicleType.MDV },
                    });
                }
                def.Roles = roles.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("TwoWheelerPrefabBuilder: rider roles added to " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    /// <summary>
    /// AIBicycle: the bicycle with a different rider, as an externally driven AI vehicle (for SUMO's
    /// bicycles): an AICar component (disabled) so it can be spawned and synced like AI cars.
    /// </summary>
    /// <summary>Static bicycles in several colours for city maps (no rider, no scripts), leaning slightly as on a kickstand.</summary>
    public static GameObject[] BuildParkedBicycles(string folder)
    {
        var colours = new[] { new Color(0.05f, 0.05f, 0.06f), new Color(0.1f, 0.2f, 0.45f), new Color(0.45f, 0.08f, 0.08f), new Color(0.75f, 0.75f, 0.72f), new Color(0.15f, 0.3f, 0.18f) };
        // the shared paint (and lights) of the rideable bicycle must keep their settings
        var keep = new[] { "BikePaint", "HeadLight", "RearLight" }
            .Select(n => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{n}.mat")).Where(m => m != null)
            .ToDictionary(m => m, m => new Material(m));
        var shared = Mat("BikePaint", Color.black, 0.6f, 0.3f);
        var result = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < colours.Length; i++)
        {
            var root = new GameObject("ParkedBicycle " + i);
            try
            {
                var rig = BuildBicycle(root.transform, colours[i]);
                var paintPath = $"{folder}/ParkedBikePaint_{i}.mat";
                var paint = AssetDatabase.LoadAssetAtPath<Material>(paintPath);
                if (paint == null) { paint = new Material(shared); AssetDatabase.CreateAsset(paint, paintPath); }
                paint.CopyPropertiesFromMaterial(shared);
                paint.SetColor("_BaseColor", colours[i]);
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                    r.sharedMaterials = r.sharedMaterials.Select(m => m == shared ? paint : m).ToArray();
                rig.Lean.localRotation = Quaternion.Euler(0, 0, 7f);
                foreach (var c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                var path = $"{folder}/ParkedBicycle_{i}.prefab";
                result.Add(PrefabUtility.SaveAsPrefabAsset(root, path));
            }
            finally { Object.DestroyImmediate(root); }
        }
        foreach (var kv in keep) { kv.Key.CopyPropertiesFromMaterial(kv.Value); EditorUtility.SetDirty(kv.Key); }
        AssetDatabase.SaveAssets();
        return result.ToArray();
    }

    public static void BuildAIBicycle()
    {
        var root = PrefabUtility.LoadPrefabContents($"{Dir}/Bicycle.prefab");
        try
        {
            root.name = "AIBicycle";
            var ai = root.AddComponent<AICar>();
            ai.enabled = false;
            ai.playerAvatar = root.GetComponent<PlayerAvatar>();
            ai.modelElements = new Transform[0];
            var tracker = root.AddComponent<UnityStandardAssets.Utility.WaypointProgressTracker>();
            tracker.enabled = false;
            foreach (var cam in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(cam.gameObject);
            root.GetComponent<PlayerAvatar>().cameras = new Camera[0];
            PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/AIBicycle.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
