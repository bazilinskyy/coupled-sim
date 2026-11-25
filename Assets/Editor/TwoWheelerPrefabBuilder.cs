using System.Collections.Generic;
using System.Linq;
using System.IO;
using CoupledSim.TwoWheelers;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the bicycle, e-scooter, moped and race bicycle avatar prefabs: a vehicle model made from meshes in code
/// (torus tyres with spokes, frame tubes, steering assembly on a tilted head tube, saddle, cranks,
/// lights; the road bike with deep rims, a drop bar and hoods) with URP materials, a Rocketbox rider posed with IK
/// (RiderIK; on a bicycle the saddle set to the rider's legs, the race cyclist in a painted kit with a helmet and
/// glasses), a head camera, TwoWheelerController and PlayerAvatar. Menu: Coupled Sim ➡️ Two-wheelers ➡️ Build avatar prefabs.
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
        Build(TwoWheelerType.RaceBike, "RaceBicycle", new Color(0.04f, 0.04f, 0.05f), RaceRider);
        AssetDatabase.SaveAssets();
        BuildAIBicycle();
        BuildAIBicycle("RaceBicycle", "AIRaceBicycle");
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// The bicycle and the race bicycle again (with their AI versions), the race bicycle in PlayerSystem's two-wheeler
    /// list (index 3) and in the AI car prefabs (for the traffic), and the Race cyclist role in the experiments.
    /// For batch mode: -executeMethod TwoWheelerPrefabBuilder.BuildBicyclesAndRoles.
    /// </summary>
    [MenuItem("Coupled Sim/Two-wheelers/Build the bicycles and add the race cyclist role")]
    public static void BuildBicyclesAndRoles()
    {
        Directory.CreateDirectory(MeshDir);
        Directory.CreateDirectory(MatDir);
        _materials.Clear();
        _cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        Build(TwoWheelerType.Bicycle, "Bicycle", new Color(0.08f, 0.16f, 0.3f), "Female_Adult_04");
        Build(TwoWheelerType.RaceBike, "RaceBicycle", new Color(0.04f, 0.04f, 0.05f), RaceRider);
        AssetDatabase.SaveAssets();
        BuildAIBicycle();
        BuildAIBicycle("RaceBicycle", "AIRaceBicycle");
        AssetDatabase.SaveAssets();
        AddRoles();
        TrafficTools.RegisterAICarPrefabs(new[] { AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/AIRaceBicycle.prefab").GetComponent<AICar>() });
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Coupled Sim/Two-wheelers/Build the moped prefab")]
    public static void BuildMopedOnly()
    {
        Directory.CreateDirectory(MeshDir);
        Directory.CreateDirectory(MatDir);
        _materials.Clear();
        _cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        Build(TwoWheelerType.Moped25, "Moped", new Color(0.62f, 0.75f, 0.7f), "Male_Adult_09");
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
        public Transform LeftDrop, RightDrop;
        public float WheelRadius;
        public bool Seated = true;
        /// <summary>The top of the saddle (bicycles), in the leaning frame.</summary>
        public Vector3 SaddleTop;
    }

    /// <summary>The rider's skeleton, measured in the bind pose (rider's frame: y up from the soles, z forward).</summary>
    struct RiderSize
    {
        public float Leg, HipsAboveJoint, ToeHeight, Torso, Arm, EyeAboveHips;
        public static RiderSize Of(Animator a)
        {
            var root = a.transform;
            Vector3 P(HumanBodyBones b) { var t = a.GetBoneTransform(b); return t != null ? root.InverseTransformPoint(t.position) : Vector3.zero; }
            var hips = P(HumanBodyBones.Hips); var ul = P(HumanBodyBones.LeftUpperLeg); var ll = P(HumanBodyBones.LeftLowerLeg); var foot = P(HumanBodyBones.LeftFoot);
            var toe = P(HumanBodyBones.LeftToes); var ua = P(HumanBodyBones.LeftUpperArm); var la = P(HumanBodyBones.LeftLowerArm); var hand = P(HumanBodyBones.LeftHand); var head = P(HumanBodyBones.Head);
            return new RiderSize
            {
                Leg = Vector3.Distance(ul, ll) + Vector3.Distance(ll, foot),
                HipsAboveJoint = hips.y - ul.y,
                ToeHeight = Mathf.Clamp(toe.y, 0.015f, 0.08f),
                Torso = Vector3.Distance(hips, ua),
                Arm = Vector3.Distance(ua, la) + Vector3.Distance(la, hand),
                EyeAboveHips = head.y - hips.y + 0.08f,
            };
        }

        /// <summary>
        /// The saddle height (top, above the ground) at which the leg is 95% straight with the pedal at the bottom (the
        /// usual fit: knee bent some 25-30°), for a bottom bracket at <paramref name="bb"/> and the saddle at <paramref name="saddleZ"/>.
        /// </summary>
        public float SaddleFor(Vector3 bb, float crank, float saddleZ)
        {
            // the ankle with the pedal at the bottom: the ball of the foot over the spindle, toes some 16° down
            var ankle = new Vector3(0f, bb.y - crank + ToeHeight + 0.012f + 0.07f, bb.z - 0.11f);
            float dz = saddleZ - 0.02f - ankle.z, d = 0.95f * Leg;
            float hipJoint = ankle.y + Mathf.Sqrt(Mathf.Max(0.01f, d * d - dz * dz));
            return hipJoint - 0.09f;   // (the sit bones some 9 cm below the hip joints)
        }
    }

    static Rig BuildBicycle(Transform root, Color paint, float saddleTop = 1.03f)
    {
        var r = new Rig { Root = root, WheelRadius = 0.35f };
        var frame = Mat("BikePaint" + _paintSuffix, paint, 0.6f, 0.3f);
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
        // the seat post as long as the rider needs (the saddle top 18 cm above the seat tube by default)
        float post = Mathf.Clamp(saddleTop - seatTop.y - 0.06f, 0.04f, 0.32f);
        var postTop = seatTop + new Vector3(0, post, -post * 0.25f);
        Tube("Seat post", r.Lean, seatTop, postTop, 0.027f, steel);
        r.Seat = Empty("Seat", r.Lean, postTop + new Vector3(0, 0.03f, -0.02f));
        Box("Saddle", r.Lean, postTop + new Vector3(0, 0.03f, 0f), new Vector3(0.17f, 0.06f, 0.26f), Mat("Saddle", new Color(0.12f, 0.08f, 0.05f), 0.35f));
        r.SaddleTop = postTop + new Vector3(0, 0.06f, -0.02f);
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
        // swept-back bars, as on a Dutch city bike: the grips well back towards the rider
        var barL = stemTop + new Vector3(-0.28f, 0.02f, -0.23f);
        var barR = stemTop + new Vector3(0.28f, 0.02f, -0.23f);
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

    /// <summary>A deep rim: a band round the axle (x) from rInner to rOuter, narrower at the inside (a V section).</summary>
    static Mesh Rim(string name, float rOuter, float rInner, float halfWidth, int seg = 64)
    {
        var path = $"{MeshDir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        // the section, round: outer left, outer right, inner right, inner left (x across, r out)
        var sec = new[] { new Vector2(-halfWidth, rOuter), new Vector2(halfWidth, rOuter), new Vector2(halfWidth * 0.55f, rInner), new Vector2(-halfWidth * 0.55f, rInner) };
        var verts = new List<Vector3>(); var normals = new List<Vector3>(); var tris = new List<int>();
        for (int f = 0; f < 4; f++)
        {
            var p0 = sec[f]; var p1 = sec[(f + 1) % 4];
            var edge = p1 - p0; var n2 = new Vector2(edge.y, -edge.x).normalized;   // (outward of the section)
            int b0 = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * 2f * Mathf.PI / seg;
                var radial = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a));
                foreach (var q in new[] { p0, p1 })
                {
                    verts.Add(Vector3.right * q.x + radial * q.y);
                    normals.Add((Vector3.right * n2.x + radial * n2.y).normalized);
                }
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = b0 + i * 2, a1 = a0 + 2;
                tris.AddRange(new[] { a0, a0 + 1, a1, a1, a0 + 1, a1 + 1 });
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetTriangles(tris, 0);
        // (both faces: the winding of each strip depends on its direction)
        var t = mesh.triangles; var both = new int[t.Length * 2];
        for (int i = 0; i < t.Length; i += 3) { both[i] = t[i]; both[i + 1] = t[i + 1]; both[i + 2] = t[i + 2]; both[t.Length + i] = t[i]; both[t.Length + i + 1] = t[i + 2]; both[t.Length + i + 2] = t[i + 1]; }
        mesh.triangles = both;
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>Tubes along a polyline (handlebar bends).</summary>
    static void Bend(string name, Transform parent, IList<Vector3> pts, float diameter, Material mat)
    {
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Tube($"{name} {i}", parent, pts[i], pts[i + 1], diameter, mat);
            if (i > 0) Ball($"{name} joint {i}", parent, pts[i], Vector3.one * diameter, mat);
        }
    }

    /// <summary>
    /// A road racing bicycle: a carbon diamond frame (a sloping top tube, a fat down tube, dropped seat stays), thin
    /// 25 mm tyres on deep rims, a drop handlebar with brake hoods on a long low stem, a narrow saddle high above the
    /// bars, a double chainset with 172.5 mm cranks and clipless pedals, a bottle on the down tube; no mudguards, rack
    /// or lights. About 1 m wheelbase, the bottom bracket 27 cm up.
    /// </summary>
    static Rig BuildRaceBicycle(Transform root, Color paint, Color accent, float saddleTop)
    {
        const float wr = 0.335f, tyre = 0.0125f, crankLen = 0.1725f;
        var r = new Rig { Root = root, WheelRadius = wr };
        var carbon = Mat("RacePaint", paint, 0.7f, 0.15f);
        var deco = Mat("RaceAccent", accent, 0.6f, 0.1f);
        var alu = Mat("RaceAlloy", new Color(0.18f, 0.18f, 0.2f), 0.75f, 0.9f);
        var steel = Mat("Steel", new Color(0.8f, 0.8f, 0.82f), 0.85f, 1f);
        var black = Mat("Rubber", new Color(0.05f, 0.05f, 0.05f), 0.25f);
        var tape = Mat("BarTape", new Color(0.06f, 0.06f, 0.07f), 0.35f);
        var bottle = Mat("Bottle", new Color(0.9f, 0.92f, 0.95f), 0.5f);
        r.Lean = Empty("Lean", root, Vector3.zero);
        float o = -0.05f;   // (the middle between the axles at the origin)
        var rear = new Vector3(0, wr, -0.45f + o);
        var bb = new Vector3(0, 0.27f, -0.04f + o);
        // seat tube at 73.5°, the saddle on its line at the rider's height
        var seatDir = new Vector3(0, Mathf.Sin(73.5f * Mathf.Deg2Rad), -Mathf.Cos(73.5f * Mathf.Deg2Rad));
        var cluster = bb + seatDir * 0.52f;
        float postLen = Mathf.Clamp((saddleTop - 0.045f - cluster.y) / seatDir.y, 0.08f, 0.35f);
        var clamp = cluster + seatDir * postLen;
        // head tube at 73°, 14.5 cm, its top at 0.82 m
        var axis = new Vector3(0, Mathf.Sin(73f * Mathf.Deg2Rad), -Mathf.Cos(73f * Mathf.Deg2Rad));
        var headTop = new Vector3(0, 0.82f, 0.335f + o);
        var headBottom = headTop - axis * 0.145f;
        r.RearWheel = Empty("Rear wheel", r.Lean, rear);
        RaceWheel(r.RearWheel, wr, tyre, alu, black, steel, deco);
        Tube("Seat tube", r.Lean, bb, cluster, 0.036f, carbon);
        Tube("Down tube", r.Lean, bb + new Vector3(0, 0.01f, 0.01f), headBottom + new Vector3(0, 0.01f, 0), 0.052f, carbon);
        Tube("Top tube", r.Lean, cluster + seatDir * -0.02f, headTop - axis * 0.025f, 0.036f, carbon);
        Tube("Head tube", r.Lean, headBottom - axis * 0.01f, headTop, 0.046f, carbon);
        Tube("Down tube band", r.Lean, Vector3.Lerp(bb, headBottom, 0.55f), Vector3.Lerp(bb, headBottom, 0.75f), 0.054f, deco);
        Tube("Chain stay L", r.Lean, bb + new Vector3(-0.04f, 0, 0), rear + new Vector3(-0.065f, 0, 0), 0.022f, carbon);
        Tube("Chain stay R", r.Lean, bb + new Vector3(0.04f, 0, 0), rear + new Vector3(0.065f, 0, 0), 0.022f, carbon);
        var stayTop = bb + seatDir * 0.43f;   // (dropped seat stays)
        Tube("Seat stay L", r.Lean, stayTop + new Vector3(-0.015f, 0, 0), rear + new Vector3(-0.065f, 0, 0), 0.016f, carbon);
        Tube("Seat stay R", r.Lean, stayTop + new Vector3(0.015f, 0, 0), rear + new Vector3(0.065f, 0, 0), 0.016f, carbon);
        Tube("Seat post", r.Lean, cluster - seatDir * 0.03f, clamp, 0.027f, black);
        // the saddle: narrow, its nose forward, slightly nose-down
        r.SaddleTop = clamp + new Vector3(0, 0.045f, 0.01f);
        Box("Saddle", r.Lean, clamp + new Vector3(0, 0.03f, -0.035f), new Vector3(0.135f, 0.03f, 0.13f), black, new Vector3(1f, 0, 0));
        Box("Saddle nose", r.Lean, clamp + new Vector3(0, 0.028f, 0.075f), new Vector3(0.05f, 0.026f, 0.12f), black, new Vector3(2f, 0, 0));
        Box("Saddle stripe", r.Lean, clamp + new Vector3(0, 0.046f, -0.04f), new Vector3(0.02f, 0.004f, 0.11f), deco);
        r.Seat = Empty("Seat", r.Lean, r.SaddleTop - new Vector3(0, 0.03f, 0.02f));
        // a bottle in its cage on the down tube, the rear brake, the cassette and the derailleur
        var downDir = (headBottom - bb).normalized;
        var onDown = Vector3.Lerp(bb, headBottom, 0.45f) + Vector3.Cross(downDir, Vector3.right) * 0.068f;   // (on top of the down tube)
        Tube("Bottle", r.Lean, onDown - downDir * 0.09f, onDown + downDir * 0.09f, 0.068f, bottle);
        Tube("Bottle cap", r.Lean, onDown + downDir * 0.09f, onDown + downDir * 0.11f, 0.03f, black);
        Box("Rear brake", r.Lean, stayTop + new Vector3(0, -0.01f, -0.03f), new Vector3(0.08f, 0.03f, 0.03f), alu);
        Part("Cassette", r.Lean, Torus("Cassette", 0.035f, 0.012f, 24, 6), alu).transform.localPosition = rear + new Vector3(0.05f, 0, 0);
        Box("Derailleur", r.Lean, rear + new Vector3(0.065f, -0.07f, 0.01f), new Vector3(0.015f, 0.07f, 0.03f), alu);

        // steering: fork, front wheel, stem, drop bar with the brake hoods
        r.Steering = Empty("Steering", r.Lean, headBottom, Quaternion.FromToRotation(Vector3.up, axis));
        var frontAxle = headBottom - axis * ((headBottom.y - wr) / axis.y) + new Vector3(0, 0, 0.045f);
        var front = r.Steering.InverseTransformPoint(r.Lean.TransformPoint(frontAxle));
        var crown = new Vector3(0, -0.02f, 0);
        Tube("Fork L", r.Steering, crown + new Vector3(-0.03f, 0, 0), front + new Vector3(-0.05f, 0, 0), 0.026f, carbon);
        Tube("Fork R", r.Steering, crown + new Vector3(0.03f, 0, 0), front + new Vector3(0.05f, 0, 0), 0.026f, carbon);
        Box("Front brake", r.Steering, crown + new Vector3(0, -0.03f, 0.03f), new Vector3(0.08f, 0.03f, 0.03f), alu);
        r.FrontWheel = Empty("Front wheel", r.Steering, front);
        RaceWheel(r.FrontWheel, wr, tyre, alu, black, steel, deco);
        Vector3 S(Vector3 lean) => r.Steering.InverseTransformPoint(r.Lean.TransformPoint(lean));
        var stemBase = headTop + axis * 0.035f;
        var barC = stemBase + new Vector3(0, 0.012f, 0.085f);
        Tube("Spacers", r.Steering, S(headTop), S(stemBase + axis * 0.01f), 0.034f, black);
        Tube("Stem", r.Steering, S(stemBase), S(barC), 0.032f, alu);
        foreach (var side in new[] { -1f, 1f })
        {
            string n = side < 0 ? "L" : "R";
            // tops, the forward bend (ramps) up to the hood, the drop down and back
            var pts = new[]
            {
                barC, barC + new Vector3(side * 0.14f, 0, 0), barC + new Vector3(side * 0.195f, 0, 0.012f), barC + new Vector3(side * 0.205f, -0.004f, 0.045f),
                barC + new Vector3(side * 0.21f, -0.015f, 0.075f), barC + new Vector3(side * 0.21f, -0.045f, 0.093f), barC + new Vector3(side * 0.21f, -0.085f, 0.09f),
                barC + new Vector3(side * 0.21f, -0.115f, 0.065f), barC + new Vector3(side * 0.21f, -0.13f, 0.03f), barC + new Vector3(side * 0.21f, -0.135f, -0.04f),
            };
            Bend("Bar " + n, r.Steering, pts.Select(S).ToList(), 0.026f, tape);
            // the hood (lever body) on top of the bend, the lever blade down in front of the drop
            var hood = barC + new Vector3(side * 0.21f, 0.005f, 0.085f);
            Box("Hood " + n, r.Steering, S(hood + new Vector3(0, 0.012f, 0.015f)), new Vector3(0.03f, 0.038f, 0.075f), black, new Vector3(-12f, 0, 0));
            Box("Lever " + n, r.Steering, S(barC + new Vector3(side * 0.21f, -0.06f, 0.115f)), new Vector3(0.012f, 0.1f, 0.012f), alu, new Vector3(12f, 0, 0));
            var g = Empty("Hood grip " + n, r.Steering, S(hood + new Vector3(0, 0.03f, 0.0f)));
            var d = Empty("Drop grip " + n, r.Steering, S(barC + new Vector3(side * 0.21f, -0.13f, 0.0f)));
            if (side < 0) { r.LeftGrip = g; r.LeftDrop = d; } else { r.RightGrip = g; r.RightDrop = d; }
        }

        // chainset: two rings on the right, 172.5 mm cranks, small clipless pedals
        r.Crank = Empty("Crank", r.Lean, bb);
        Tube("Crank arm R", r.Crank, new Vector3(0.085f, 0, 0), new Vector3(0.085f, crankLen, 0), 0.02f, alu);
        Tube("Crank arm L", r.Crank, new Vector3(-0.085f, 0, 0), new Vector3(-0.085f, -crankLen, 0), 0.02f, alu);
        Tube("Axle", r.Crank, new Vector3(-0.085f, 0, 0), new Vector3(0.085f, 0, 0), 0.03f, alu);
        Part("Big ring", r.Crank, Torus("Chainring_big", 0.1f, 0.005f, 40, 6), alu).transform.localPosition = new Vector3(0.068f, 0, 0);
        Part("Small ring", r.Crank, Torus("Chainring_small", 0.07f, 0.005f, 32, 6), alu).transform.localPosition = new Vector3(0.062f, 0, 0);
        r.RightFoot = Empty("Right pedal", r.Crank, new Vector3(0.125f, crankLen, 0));
        r.LeftFoot = Empty("Left pedal", r.Crank, new Vector3(-0.125f, -crankLen, 0));
        Box("Pedal R", r.RightFoot, Vector3.zero, new Vector3(0.07f, 0.016f, 0.06f), black);
        Box("Pedal L", r.LeftFoot, Vector3.zero, new Vector3(0.07f, 0.016f, 0.06f), black);
        // the chain from the big ring to the cassette (top and bottom runs)
        Tube("Chain top", r.Lean, bb + new Vector3(0.068f, 0.1f, 0), rear + new Vector3(0.05f, 0.035f, 0), 0.008f, steel);
        Tube("Chain bottom", r.Lean, bb + new Vector3(0.068f, -0.1f, 0), rear + new Vector3(0.06f, -0.08f, 0.02f), 0.008f, steel);
        Box("Rear light", r.Lean, clamp + new Vector3(0, -0.12f, -0.03f), new Vector3(0.025f, 0.05f, 0.015f), Mat("RearLight", new Color(0.8f, 0.05f, 0.05f), 0.6f, 0, true));
        return r;
    }

    /// <summary>A road wheel: a 25 mm tyre on a deep rim (4 cm), 20 thin spokes, a small hub.</summary>
    static void RaceWheel(Transform w, float radius, float tyre, Material rimMat, Material rubber, Material steel, Material deco)
    {
        Part("Tyre", w, Torus($"Tyre_{radius:0.000}_{tyre:0.0000}", radius - tyre, tyre, 64, 10), rubber);
        Part("Rim", w, Rim($"DeepRim_{radius:0.000}", radius - tyre * 1.6f, radius - tyre * 1.6f - 0.04f, 0.0115f), rimMat);
        Part("Rim decal", w, Rim($"DeepRimDecal_{radius:0.000}", radius - tyre * 1.6f - 0.012f, radius - tyre * 1.6f - 0.026f, 0.0122f), deco);
        Tube("Hub", w, new Vector3(-0.045f, 0, 0), new Vector3(0.045f, 0, 0), 0.035f, rimMat);
        float rimIn = radius - tyre * 1.6f - 0.04f;
        for (int i = 0; i < 20; i++)
        {
            float a = i * 2f * Mathf.PI / 20;
            var rim = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a)) * rimIn;
            Tube($"Spoke {i}", w, new Vector3(i % 2 == 0 ? 0.03f : -0.03f, 0, 0), rim, 0.0028f, steel);
        }
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

    // the moped: "Moto Vespa" by mohamed ouartassi (Sketchfab, CC BY 4.0), its parts named by the model's nodes
    const string MopedModel = "Assets/ThirdParty/TwoWheelers/MotoVespa/moto_vespa.glb";
    static readonly string[] MopedFrontWheel = { "Object_65", "Object_66", "Object_67", "Object_45" };
    static readonly string[] MopedRearWheel = { "Object_69", "Object_70", "Object_71" };
    static readonly string[] MopedSteering = { "Object_41", "Object_43", "Object_44", "Object_52", "Object_17", "Object_28", "Object_13", "Object_14", "Object_56", "Object_57", "Object_59", "Object_60", "Object_61" };

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

    static Bounds BoundsOf(Transform model, params string[] names)
    {
        var b = new Bounds(); bool any = false;
        foreach (var n in names)
        {
            var t = FindDeep(model, n); if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        }
        return b;
    }

    /// <summary>
    /// The moped from its 3D model: the body leans, the handlebar, headlight, fork and front mudguard turn with the
    /// steering about the axis from the front axle to the headset, the wheels spin; the rider's seat, grips and footrests
    /// where the model has them. Falls back to the simple shapes (BuildMoped) without the model.
    /// </summary>
    static Rig BuildMopedModel(Transform root, GameObject asset)
    {
        var r = new Rig { Root = root };
        r.Lean = Empty("Lean", root, Vector3.zero);
        var holder = Empty("Model", r.Lean, Vector3.zero, Quaternion.Euler(0, 180, 0));   // (the model's front is its -z)
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.SetParent(holder, false);
        // the wheels' middle at the origin, their bottoms on the ground
        var fw = BoundsOf(model.transform, "Object_65"); var rw = BoundsOf(model.transform, "Object_69");
        holder.position += new Vector3(-(fw.center.x + rw.center.x) / 2f, -Mathf.Min(fw.min.y, rw.min.y), -(fw.center.z + rw.center.z) / 2f);
        fw = BoundsOf(model.transform, "Object_65"); rw = BoundsOf(model.transform, "Object_69");
        var frontC = new Vector3(0, fw.center.y, fw.center.z); var rearC = new Vector3(0, rw.center.y, rw.center.z);
        r.WheelRadius = fw.extents.y;
        // steering axis: from the front axle up to the headset
        var head = BoundsOf(model.transform, "Object_13").center; head.x = 0f;
        var axis = (head - frontC).normalized;
        var pivot = frontC + axis * ((0.55f - frontC.y) / Mathf.Max(0.1f, axis.y));
        r.Steering = Empty("Steering", r.Lean, pivot, Quaternion.FromToRotation(Vector3.up, axis));
        r.FrontWheel = Empty("Front wheel", r.Steering, r.Steering.InverseTransformPoint(r.Lean.TransformPoint(frontC)));
        r.RearWheel = Empty("Rear wheel", r.Lean, rearC);
        foreach (var n in MopedSteering) FindDeep(model.transform, n)?.SetParent(r.Steering, true);
        foreach (var n in MopedFrontWheel) FindDeep(model.transform, n)?.SetParent(r.FrontWheel, true);
        foreach (var n in MopedRearWheel) FindDeep(model.transform, n)?.SetParent(r.RearWheel, true);
        // the rider: on the rear part of the seat, hands on the grips, feet on the floorboard
        var seat = BoundsOf(model.transform, "Object_50", "Object_49");
        r.Seat = Empty("Seat", r.Lean, new Vector3(0, seat.max.y + 0.02f, seat.center.z - 0.12f));
        var bar = BoundsOf(r.Steering, "Object_14");
        Transform Grip(string name, float side) => Empty(name, r.Steering, r.Steering.InverseTransformPoint(new Vector3(side * (bar.extents.x - 0.07f), bar.center.y + 0.01f, bar.center.z - 0.03f)));
        r.LeftGrip = Grip("Left grip", -1f); r.RightGrip = Grip("Right grip", 1f);
        var floor = BoundsOf(model.transform, "Object_36");
        r.LeftFoot = Empty("Left foot", r.Lean, new Vector3(-0.11f, floor.max.y + 0.03f, floor.center.z + 0.08f));
        r.RightFoot = Empty("Right foot", r.Lean, new Vector3(0.11f, floor.max.y + 0.03f, floor.center.z + 0.08f));
        Debug.Log($"TwoWheelerPrefabBuilder: moped model, wheels at {rearC} and {frontC} (radius {r.WheelRadius:F2}), steering pivot {pivot}, seat {r.Seat.localPosition}");
        return r;
    }

    static Rig BuildMoped(Transform root, Color paint)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MopedModel);
        if (asset != null) return BuildMopedModel(root, asset);
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

    /// <summary>The race cyclist: Rocketbox's athletic woman (her figure-hugging body takes a painted kit), in team colours.</summary>
    const string RaceRider = "Sports_Female_01";
    /// <summary>The race cyclist's kit: D.S.W.V. WTOS, Delft's student cycling club (its 2019 kit): a mid-blue jersey with
    /// two white bands across the chest, white cuffs and WTOS in white down the back; black bib shorts with blue leg
    /// bands. (TeamDark: the side panels and sleeves, here the jersey's blue too.)</summary>
    static readonly Color TeamColour = new Color(0.27f, 0.53f, 0.82f), TeamDark = new Color(0.27f, 0.53f, 0.82f);
    /// <summary>The lettering down the back of the jersey (white, read from top to bottom), or null.</summary>
    const string BackLettering = "WTOS";

    // a 5 x 7 block font for the back's lettering (rows top to bottom, '#' set)
    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
    };

    static void Build(TwoWheelerType type, string name, Color paint, string riderModel)
    {
        var root = new GameObject(name);
        try
        {
            bool bicycle = type == TwoWheelerType.Bicycle || type == TwoWheelerType.RaceBike;
            // the rider first: a bicycle's saddle is set to the rider's leg length
            var riderAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{RocketboxImporter.Root}{riderModel}/{riderModel}.fbx");
            GameObject rider = null; Animator animator = null; RiderSize size = default;
            if (riderAsset != null)
            {
                rider = (GameObject)PrefabUtility.InstantiatePrefab(riderAsset);
                PrefabUtility.UnpackPrefabInstance(rider, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                rider.name = "Rider";
                animator = rider.GetComponent<Animator>();
                size = RiderSize.Of(animator);
                Debug.Log($"TwoWheelerPrefabBuilder: {riderModel}: leg {size.Leg:F3} m, torso {size.Torso:F3}, arm {size.Arm:F3}, toes {size.ToeHeight:F3} up, hips {size.HipsAboveJoint:F3} above the hip joints");
            }
            float saddle = rider == null ? 1.03f
                : type == TwoWheelerType.Bicycle ? Mathf.Clamp(size.SaddleFor(new Vector3(0, 0.3f, -0.05f), 0.17f, -0.27f), 0.9f, 1.2f)
                : Mathf.Clamp(size.SaddleFor(new Vector3(0, 0.27f, -0.09f), 0.1725f, -0.3f), 0.85f, 1.15f);
            var rig = type == TwoWheelerType.Bicycle ? BuildBicycle(root.transform, paint, saddle)
                : type == TwoWheelerType.RaceBike ? BuildRaceBicycle(root.transform, paint, TeamColour, saddle)
                : type == TwoWheelerType.EScooter ? BuildEScooter(root.transform, paint)
                : BuildMoped(root.transform, paint);

            // rider: a Rocketbox avatar, hips at the seat (or standing on the deck), limbs by IK
            Transform head = null;
            Vector3 eye = Vector3.zero; bool eyeKnown = false;
            if (rider != null)
            {
                rider.transform.SetParent(rig.Lean, false);
                // (a rider's seated posture as the animation, on which the IK puts the hands and feet: on a road bike a long,
                // rounded back with the neck bent back to look ahead; leaning the body about the hips alone, with the
                // standing spine, tipped the pelvis and kept the chest upright, the bottom pushed out)
                animator.runtimeAnimatorController = bicycle ? RiderPostureController(type == TwoWheelerType.RaceBike)
                    : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Prefabs/AIPedestrians/Pedestrian.controller");
                animator.applyRootMotion = false;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                float hipHeight = hips != null ? hips.position.y - rider.transform.position.y : 0.95f;
                var ik = rider.AddComponent<RiderIK>();
                ik.LeftHand = rig.LeftGrip;
                ik.RightHand = rig.RightGrip;
                ik.LeftFoot = rig.LeftFoot;
                ik.RightFoot = rig.RightFoot;
                ik.LookTarget = Empty("Look target", rig.Root, new Vector3(0, 1.2f, 15f));
                if (bicycle)
                {
                    // the cycling pose: the hips on the saddle (the sit bones 9 cm below the hip joints), the torso leaning
                    // as far as the grips need with slightly bent arms (at least 15° on a city bike, 52° on a road bike on
                    // the hoods, 64° on the drops)
                    var hipTarget = Empty("Hip target", rig.Lean, rig.SaddleTop + new Vector3(0, 0.09f + size.HipsAboveJoint, -0.02f));
                    ik.Cycling = true;
                    ik.Frame = rig.Lean;
                    ik.Root = rig.Root;
                    ik.Crank = rig.Crank;
                    ik.HipTarget = hipTarget;
                    ik.Hoods = type == TwoWheelerType.RaceBike;
                    ik.LeftDrop = rig.LeftDrop;
                    ik.RightDrop = rig.RightDrop;
                    // (with the road posture's rounded back on top: the shoulders some 55° forward on the hoods, more on the
                    // drops, the elbows bent)
                    ik.TorsoPitch = type == TwoWheelerType.RaceBike ? 28f : 15f;
                    ik.DropsTorsoPitch = 44f;
                    rider.transform.localPosition = hipTarget.localPosition - new Vector3(0, hipHeight, 0);
                    var grip = (rig.LeftGrip.position + rig.RightGrip.position) / 2f;
                    float pitch = RiderIK.TorsoPitchFor(hipTarget.localPosition, rig.Lean.InverseTransformPoint(grip), size.Torso, size.Arm, ik.TorsoPitch);
                    eye = RiderIK.EyeFor(hipTarget.localPosition, pitch, size.EyeAboveHips);
                    eyeKnown = true;
                    Debug.Log($"TwoWheelerPrefabBuilder: {name}: saddle top {rig.SaddleTop.y:F3} m, torso {pitch:F0}° forward, eyes at {eye}");
                }
                else
                {
                    // seated: hips just above the saddle; standing: feet on the deck
                    rider.transform.localPosition = rig.Seated ? rig.Seat.localPosition - new Vector3(0, hipHeight - 0.08f, 0f) : rig.Seat.localPosition;
                    ik.LeftKnee = Empty("Left knee hint", rig.Lean, rig.Seat.localPosition + new Vector3(-0.15f, -0.25f, 0.6f));
                    ik.RightKnee = Empty("Right knee hint", rig.Lean, rig.Seat.localPosition + new Vector3(0.15f, -0.25f, 0.6f));
                }
                rider.transform.localRotation = Quaternion.identity;
                if (type == TwoWheelerType.RaceBike) DressRacer(rider, animator, name);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            // camera at the rider's eyes
            float eyeHeight = eyeKnown ? eye.y : head != null ? head.position.y - root.transform.position.y + 0.08f : (rig.Seated ? 1.55f : 1.75f);
            float eyeForward = eyeKnown ? eye.z : head != null ? head.position.z - root.transform.position.z + 0.1f : 0f;
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
            if (bicycle) ctrl.Gears = PedallingParameters.Preset(type);
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

    // ------------------------------------------------------------------ a rider's posture

    /// <summary>
    /// An animator controller with one state: a still pose given as humanoid muscle values (Mecanim's ranges, -1 to 1),
    /// the posture of a seated rider on which RiderIK's IK places the hands, feet and hips. Road: the spine, chest and
    /// upper chest bent forward (a long, rounded back), the neck and head bent back to look up the road; city: nearly
    /// upright.
    /// </summary>
    /// <summary>Make the posture controllers again (their muscle values changed): the bicycles must then be rebuilt.</summary>
    static bool _remakePostures;

    static RuntimeAnimatorController RiderPostureController(bool road)
    {
        string name = road ? "RiderPostureRoad" : "RiderPostureCity";
        string clipPath = $"{Dir}/{name}.anim", ctrlPath = $"{Dir}/{name}.controller", maskPath = $"{Dir}/RiderPostureBack.mask";
        // (an existing controller is kept: made again, it got a new GUID, and the prefabs built before lost their posture)
        var have = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
        if (have != null && !_remakePostures) return have;
        // the back's posture: only the spine, chest, neck and head (the limbs stay as the idle pose has them, for the IK)
        var clip = new AnimationClip { name = name };
        var muscles = road
            // (Mecanim's muscles run from the first word of their name to the second: Front-Back -1 is bent forward,
            // Nod Down-Up +1 is the head up)
            ? new (string m, float v)[] { ("Spine Front-Back", -0.3f), ("Chest Front-Back", -0.35f), ("UpperChest Front-Back", -0.3f), ("Neck Nod Down-Up", 0.45f), ("Head Nod Down-Up", 0.35f) }
            : new (string m, float v)[] { ("Spine Front-Back", -0.12f), ("Chest Front-Back", -0.1f), ("UpperChest Front-Back", -0.05f), ("Neck Nod Down-Up", 0.1f) };
        foreach (var (m, v) in muscles)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), m), AnimationCurve.Constant(0f, 1f, v));
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.DeleteAsset(clipPath); AssetDatabase.CreateAsset(clip, clipPath);
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head);
            AssetDatabase.CreateAsset(mask, maskPath);
        }
        // the pedestrian's controller (its idle pose, with the IK pass) and the posture's layer over it
        AssetDatabase.DeleteAsset(ctrlPath);
        AssetDatabase.CopyAsset("Assets/Prefabs/AIPedestrians/Pedestrian.controller", ctrlPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
        ctrl.AddLayer("Posture");
        var layers = ctrl.layers;
        var top = layers[layers.Length - 1];
        top.defaultWeight = 1f; top.avatarMask = mask; top.blendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode.Override; top.iKPass = false;
        ctrl.layers = layers;
        var state = ctrl.layers[layers.Length - 1].stateMachine.AddState("Posture");
        state.motion = clip;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // ------------------------------------------------------------------ the race cyclist's kit

    /// <summary>
    /// Dresses the race cyclist: a cycling kit painted onto the body's texture (by default WTOS's: a close-fitting blue
    /// jersey with two white chest bands, white cuffs and the club's name down the back, black shorts to above the knee
    /// with a blue gripper band, white socks and
    /// white cycling shoes with black soles), a road helmet and wrap-around glasses. The kit follows the body: each texel
    /// is classed by where its point of the body is (from the mesh in its bind pose and the bones that move it).
    /// </summary>
    static void DressRacer(GameObject rider, Animator animator, string name)
    {
        var smrs = rider.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var main = smrs.OrderByDescending(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0).FirstOrDefault();
        if (main == null) return;
        int slot = System.Array.FindIndex(main.sharedMaterials, m => m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null && m.GetTexture("_BaseMap").name.Contains("body"));
        if (slot < 0) { Debug.LogWarning("TwoWheelerPrefabBuilder: no body material on the race rider"); return; }
        var bodyMat = main.sharedMaterials[slot];
        var srcTex = (Texture2D)bodyMat.GetTexture("_BaseMap");
        var src = new Texture2D(2, 2);
        src.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(srcTex)));
        int W = src.width, H = src.height;
        var pix = src.GetPixels32();
        var outPix = (Color32[])pix.Clone();
        var covered = new bool[W * H];
        var painted = new bool[W * H];

        // the body in its bind pose, in the rider's frame
        var mesh = main.sharedMesh;
        var baked = new Mesh(); main.BakeMesh(baked, true);
        var bv = baked.vertices; var bnm = baked.normals;
        var vtx = new Vector3[bv.Length]; var nrm = new Vector3[bv.Length];
        for (int i = 0; i < bv.Length; i++)
        {
            vtx[i] = rider.transform.InverseTransformPoint(main.transform.position + main.transform.rotation * bv[i]);
            nrm[i] = i < bnm.Length ? rider.transform.InverseTransformDirection(main.transform.rotation * bnm[i]).normalized : Vector3.zero;
        }
        var uv = mesh.uv;
        // each vertex's main bone, as a humanoid bone
        var human = new Dictionary<Transform, HumanBodyBones>();
        for (var hb = HumanBodyBones.Hips; hb < HumanBodyBones.LastBone; hb++) { var t = animator.GetBoneTransform(hb); if (t != null && !human.ContainsKey(t)) human[t] = hb; }
        var bones = main.bones; var weights = mesh.boneWeights;
        HumanBodyBones BoneOf(int bi)
        {
            for (var t = bi >= 0 && bi < bones.Length ? bones[bi] : null; t != null; t = t.parent) if (human.TryGetValue(t, out var hb)) return hb;
            return HumanBodyBones.LastBone;
        }
        var vb = new HumanBodyBones[vtx.Length];
        for (int i = 0; i < vtx.Length; i++) vb[i] = weights.Length > i ? BoneOf(weights[i].boneIndex0) : HumanBodyBones.LastBone;
        Vector3 P(HumanBodyBones b) { var t = animator.GetBoneTransform(b); return t != null ? rider.transform.InverseTransformPoint(t.position) : Vector3.zero; }
        var hipsP = P(HumanBodyBones.Hips); var kneeP = P(HumanBodyBones.LeftLowerLeg); var footP = P(HumanBodyBones.LeftFoot); var neckP = P(HumanBodyBones.Neck);
        var shL = P(HumanBodyBones.LeftUpperArm); var elL = P(HumanBodyBones.LeftLowerArm); var shR = P(HumanBodyBones.RightUpperArm);
        float upperArm = Vector3.Distance(shL, elL);
        float chestY = shL.y - 0.15f;
        Debug.Log($"TwoWheelerPrefabBuilder: race rider bind pose: hips {hipsP}, knee {kneeP}, ankle {footP}, neck {neckP}, shoulders {shL} {shR}, hands {P(HumanBodyBones.LeftHand)}");

        Color32 C(Color c) => (Color32)c;
        var white = C(new Color(0.93f, 0.93f, 0.92f)); var black = C(new Color(0.05f, 0.05f, 0.06f));
        var team = C(TeamColour); var dark = C(TeamDark); var sole = C(new Color(0.08f, 0.08f, 0.08f));
        // the kit's colour at a point of the body (bone: the bone that moves it), or null for skin (the texture as it is)
        // the lettering down the back: each letter turned a quarter clockwise (its top to the rider's right), from
        // under the shoulder blades down, 12 cm across
        // (front and back by the surface's direction: the spine's curve puts the small of the back ahead of the neck)
        float letterTop = shL.y - 0.07f, letterPitch = 0.095f, letterLen = 0.08f, letterHalf = 0.07f;
        bool Lettering(Vector3 p, Vector3 n)
        {
            if (string.IsNullOrEmpty(BackLettering) || n.z > -0.45f || Mathf.Abs(p.x) > letterHalf) return false;
            float s = (letterTop - p.y) / letterPitch;
            int li = Mathf.FloorToInt(s);
            if (li < 0 || li >= BackLettering.Length || !Glyphs.TryGetValue(BackLettering[li], out var g)) return false;
            float along = (s - li) * letterPitch;
            if (along > letterLen) return false;
            int col = Mathf.Clamp(Mathf.FloorToInt(along / letterLen * 5f), 0, 4);
            int row = Mathf.Clamp(Mathf.FloorToInt((letterHalf - p.x) / (2f * letterHalf) * 7f), 0, 6);
            return g[row][col] == '#';
        }
        bool Front(Vector3 n) => n.z > 0.35f;
        Color32? Kit(Vector3 p, HumanBodyBones b, Vector3 n)
        {
            switch (b)
            {
                case HumanBodyBones.Spine: case HumanBodyBones.Chest: case HumanBodyBones.UpperChest:
                case HumanBodyBones.LeftShoulder: case HumanBodyBones.RightShoulder:
                    // two bands across the chest (on the front), the lettering down the back
                    if (Front(n) && (Mathf.Abs(p.y - chestY) < 0.018f || Mathf.Abs(p.y - (chestY - 0.055f)) < 0.018f)) return white;
                    if (Lettering(p, n)) return white;
                    return Mathf.Abs(p.x) > 0.105f ? dark : team;                                    // side panels
                case HumanBodyBones.Neck:
                    // (the jersey zipped up to a collar round the neck: not open down to the breastbone)
                    return p.y < neckP.y + 0.035f ? team : (Color32?)null;                           // collar
                case HumanBodyBones.Hips:
                    if (p.y > hipsP.y + 0.04f) return Lettering(p, n) ? white : Mathf.Abs(p.x) > 0.11f ? dark : team;   // the jersey's hem
                    return black;
                case HumanBodyBones.LeftUpperLeg: case HumanBodyBones.RightUpperLeg:
                    if (p.y > kneeP.y + 0.16f) return black;                                          // shorts
                    if (p.y > kneeP.y + 0.13f) return team;                                           // gripper band
                    return null;
                case HumanBodyBones.LeftUpperArm: case HumanBodyBones.RightUpperArm:
                {
                    var sh = b == HumanBodyBones.LeftUpperArm ? shL : shR;
                    float along = Mathf.Abs(p.x - sh.x) > Mathf.Abs(p.y - sh.y) ? Mathf.Abs(p.x - sh.x) : Mathf.Abs(p.y - sh.y);
                    if (along < 0.5f * upperArm) return dark;                                         // sleeve
                    if (along < 0.5f * upperArm + 0.03f) return white;                                // cuff
                    return null;
                }
                case HumanBodyBones.LeftLowerLeg: case HumanBodyBones.RightLowerLeg:
                    return p.y < footP.y + 0.13f ? white : (Color32?)null;                           // socks
                case HumanBodyBones.LeftFoot: case HumanBodyBones.RightFoot: case HumanBodyBones.LeftToes: case HumanBodyBones.RightToes:
                    return p.y < 0.025f ? sole : p.y > footP.y + 0.02f ? white : C(new Color(0.85f, 0.85f, 0.86f));   // shoes
                default:
                    return null;
            }
        }
        var rng = new System.Random(7);
        int sub = System.Array.IndexOf(main.sharedMaterials, bodyMat);
        var tris = mesh.GetTriangles(sub);
        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
            Vector2 a = new Vector2(uv[i0].x * W, uv[i0].y * H), b = new Vector2(uv[i1].x * W, uv[i1].y * H), c = new Vector2(uv[i2].x * W, uv[i2].y * H);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1);
            float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
            if (Mathf.Abs(area) < 1e-6f) continue;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var q = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = ((b.x - q.x) * (c.y - q.y) - (c.x - q.x) * (b.y - q.y)) / area;
                    float w1 = ((c.x - q.x) * (a.y - q.y) - (a.x - q.x) * (c.y - q.y)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -0.02f || w1 < -0.02f || w2 < -0.02f) continue;
                    var p = vtx[i0] * w0 + vtx[i1] * w1 + vtx[i2] * w2;
                    var pn = (nrm[i0] * w0 + nrm[i1] * w1 + nrm[i2] * w2).normalized;
                    var bone = w0 >= w1 && w0 >= w2 ? vb[i0] : w1 >= w2 ? vb[i1] : vb[i2];
                    int k = y * W + x;
                    covered[k] = true;
                    var kit = Kit(p, bone, pn);
                    if (kit == null) continue;
                    // (lycra: the colour with a faint weave and the body's light and shade kept a little)
                    var o = pix[k];
                    float shade = Mathf.Lerp(1f, (o.r + o.g + o.b) / (3f * 200f), 0.15f) * (1f + ((float)rng.NextDouble() - 0.5f) * 0.03f);
                    var col = kit.Value;
                    outPix[k] = new Color32((byte)Mathf.Clamp(col.r * shade, 0, 255), (byte)Mathf.Clamp(col.g * shade, 0, 255), (byte)Mathf.Clamp(col.b * shade, 0, 255), 255);
                    painted[k] = true;
                }
        }
        // the kit spread a few texels beyond its triangles (so no skin shows at the seams of the texture's islands)
        for (int pass = 0; pass < 4; pass++)
        {
            var next = (bool[])painted.Clone();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int k = y * W + x;
                    if (painted[k] || covered[k]) continue;
                    foreach (int n in new[] { k - 1, k + 1, k - W, k + W })
                        if (painted[n]) { outPix[k] = outPix[n]; next[k] = true; break; }
                }
            painted = next;
        }
        var tex = new Texture2D(W, H, TextureFormat.RGB24, true);
        tex.SetPixels32(outPix);
        tex.Apply();
        var texPath = $"{MatDir}/RaceKit_body_color.png";
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);
        var kitTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var matPath = $"{MatDir}/RaceKit_body.mat";
        var kitMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (kitMat == null) { kitMat = new Material(bodyMat); AssetDatabase.CreateAsset(kitMat, matPath); }
        kitMat.CopyPropertiesFromMaterial(bodyMat);
        kitMat.SetTexture("_BaseMap", kitTex);
        if (kitMat.HasProperty("_MainTex")) kitMat.SetTexture("_MainTex", kitTex);
        kitMat.SetFloat("_Smoothness", 0.32f);
        EditorUtility.SetDirty(kitMat);
        foreach (var r in smrs) r.sharedMaterials = r.sharedMaterials.Select(m => m == bodyMat ? kitMat : m).ToArray();
        // the collar on the head's texture too: Rocketbox's head mesh reaches down over the neck to the breastbone, so
        // the jersey looked open in a deep V
        int hSlot = System.Array.FindIndex(main.sharedMaterials, m => m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null && m.GetTexture("_BaseMap").name.Contains("head"));
        if (hSlot >= 0)
        {
            var headMat = main.sharedMaterials[hSlot];
            var hsrc = new Texture2D(2, 2);
            hsrc.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(headMat.GetTexture("_BaseMap"))));
            int HW = hsrc.width, HH = hsrc.height;
            var hpix = hsrc.GetPixels32(); var hout = (Color32[])hpix.Clone();
            var hcov = new bool[HW * HH]; var hpaint = new bool[HW * HH];
            var htris = mesh.GetTriangles(hSlot);
            float collarTop = neckP.y + 0.035f;
            for (int t = 0; t < htris.Length; t += 3)
            {
                int i0 = htris[t], i1 = htris[t + 1], i2 = htris[t + 2];
                if (Mathf.Min(vtx[i0].y, Mathf.Min(vtx[i1].y, vtx[i2].y)) > collarTop + 0.02f) continue;
                Vector2 a = new Vector2(uv[i0].x * HW, uv[i0].y * HH), b = new Vector2(uv[i1].x * HW, uv[i1].y * HH), c = new Vector2(uv[i2].x * HW, uv[i2].y * HH);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1), x1 = Mathf.Min(HW - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1), y1 = Mathf.Min(HH - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1);
                float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                if (Mathf.Abs(area) < 1e-6f) continue;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var q = new Vector2(x + 0.5f, y + 0.5f);
                        float w0 = ((b.x - q.x) * (c.y - q.y) - (c.x - q.x) * (b.y - q.y)) / area;
                        float w1 = ((c.x - q.x) * (a.y - q.y) - (a.x - q.x) * (c.y - q.y)) / area;
                        float w2 = 1f - w0 - w1;
                        if (w0 < -0.02f || w1 < -0.02f || w2 < -0.02f) continue;
                        int k = y * HW + x;
                        hcov[k] = true;
                        var pp = vtx[i0] * w0 + vtx[i1] * w1 + vtx[i2] * w2;
                        if (pp.y > collarTop) continue;
                        var o = hpix[k];
                        float shade = Mathf.Lerp(1f, (o.r + o.g + o.b) / (3f * 200f), 0.15f);
                        hout[k] = new Color32((byte)Mathf.Clamp(team.r * shade, 0, 255), (byte)Mathf.Clamp(team.g * shade, 0, 255), (byte)Mathf.Clamp(team.b * shade, 0, 255), 255);
                        hpaint[k] = true;
                    }
            }
            for (int pass = 0; pass < 3; pass++)
            {
                var next = (bool[])hpaint.Clone();
                for (int y = 1; y < HH - 1; y++)
                    for (int x = 1; x < HW - 1; x++)
                    {
                        int k = y * HW + x;
                        if (hpaint[k] || hcov[k]) continue;
                        foreach (int n2 in new[] { k - 1, k + 1, k - HW, k + HW })
                            if (hpaint[n2]) { hout[k] = hout[n2]; next[k] = true; break; }
                    }
                hpaint = next;
            }
            var htex = new Texture2D(HW, HH, TextureFormat.RGB24, true);
            htex.SetPixels32(hout); htex.Apply();
            var htexPath = $"{MatDir}/RaceKit_head_color.png";
            File.WriteAllBytes(htexPath, htex.EncodeToPNG());
            AssetDatabase.ImportAsset(htexPath);
            var hmatPath = $"{MatDir}/RaceKit_head.mat";
            var hkit = AssetDatabase.LoadAssetAtPath<Material>(hmatPath);
            if (hkit == null) { hkit = new Material(headMat); AssetDatabase.CreateAsset(hkit, hmatPath); }
            hkit.CopyPropertiesFromMaterial(headMat);
            var hkTex = AssetDatabase.LoadAssetAtPath<Texture2D>(htexPath);
            hkit.SetTexture("_BaseMap", hkTex);
            if (hkit.HasProperty("_MainTex")) hkit.SetTexture("_MainTex", hkTex);
            EditorUtility.SetDirty(hkit);
            foreach (var r in smrs) r.sharedMaterials = r.sharedMaterials.Select(m => m == headMat ? hkit : m).ToArray();
        }
        Object.DestroyImmediate(baked);

        // the helmet and the glasses, on the head (sized to the head: the head's own vertices above the jaw, not the hair)
        var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        var headPts = new List<Vector3>();
        int headSub = System.Array.FindIndex(main.sharedMaterials, m => m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null && m.GetTexture("_BaseMap").name.Contains("head"));
        if (headSub >= 0) foreach (int i in mesh.GetTriangles(headSub)) if (vtx[i].y > neckP.y + 0.06f) headPts.Add(vtx[i]);
        if (headBone == null || headPts.Count < 10) return;
        var hb2 = new Bounds(headPts[0], Vector3.zero);
        foreach (var q in headPts) hb2.Encapsulate(q);
        Debug.Log($"TwoWheelerPrefabBuilder: race rider's head {hb2.center} size {hb2.size}");
        var shell = Mat("HelmetShell", new Color(0.95f, 0.95f, 0.95f), 0.75f, 0.05f);
        shell.SetFloat("_Cull", 0f);
        var stripe = Mat("HelmetStripe", TeamColour, 0.7f, 0.05f);
        var vent = Mat("HelmetVent", new Color(0.03f, 0.03f, 0.03f), 0.3f);
        var lens = Mat("GlassesLens", new Color(0.05f, 0.06f, 0.08f), 0.95f, 0.7f);
        var frameMat = Mat("GlassesFrame", TeamColour, 0.6f, 0.1f);
        var helmet = new GameObject("Helmet").transform;
        helmet.SetParent(rider.transform, false);
        // (centred over the top of the skull, a little behind the face)
        var radii = new Vector3(hb2.extents.x + 0.022f, hb2.size.y * 0.5f + 0.02f, hb2.extents.z + 0.03f);
        helmet.localPosition = new Vector3(hb2.center.x, hb2.center.y + hb2.size.y * 0.08f, hb2.center.z - 0.012f);
        Part("Shell", helmet, HelmetMesh(), shell).transform.localScale = radii;
        // vents (dark) and stripes (team colour) from front to back, in short pieces along the shell
        void Meridian(string what, float side, float width, Material m, float from, float to)
        {
            for (float t = from; t < to - 1e-3f; t += 0.25f)
            {
                Vector3 At(float u) { float el = Mathf.PI / 2f - u; var n = new Vector3(Mathf.Sin(side) * Mathf.Sin(el), Mathf.Cos(side) * Mathf.Sin(el), Mathf.Cos(el)); return Vector3.Scale(n, radii) * 1.005f; }
                var p0 = At(t); var p1 = At(Mathf.Min(t + 0.25f, to));
                var piece = Box(what, helmet, (p0 + p1) / 2f, new Vector3(width, 0.008f, (p1 - p0).magnitude + 0.004f), m);
                piece.transform.localRotation = Quaternion.LookRotation(p1 - p0, (p0 + p1).normalized);
            }
        }
        foreach (var side in new[] { -0.5f, 0f, 0.5f }) Meridian("Vent", side, 0.016f, vent, -1.0f, 0.9f);
        foreach (var side in new[] { -0.85f, 0.85f }) Meridian("Stripe", side, 0.013f, stripe, -1.1f, 1.0f);
        // straps down to the chin
        foreach (var sx in new[] { -1f, 1f })
            Tube("Strap", helmet, new Vector3(sx * radii.x * 0.92f, -0.01f, 0.0f), new Vector3(sx * 0.035f, -hb2.size.y * 0.55f, hb2.extents.z * 0.55f), 0.006f, vent);
        helmet.SetParent(headBone, true);
        // glasses: across the eyes (half way up the head), wrapping round to the temples
        var glasses = new GameObject("Glasses").transform;
        glasses.SetParent(rider.transform, false);
        float eyeY = hb2.max.y - 0.115f, front = hb2.max.z + 0.004f;
        glasses.localPosition = new Vector3(hb2.center.x, eyeY, front);
        Box("Lens", glasses, new Vector3(0, 0, 0), new Vector3(hb2.size.x * 0.62f, 0.045f, 0.008f), lens);
        foreach (var sx in new[] { -1f, 1f })
        {
            var side = Box("Lens side", glasses, new Vector3(sx * hb2.size.x * 0.36f, 0, -0.02f), new Vector3(0.05f, 0.04f, 0.007f), lens);
            side.transform.localRotation = Quaternion.Euler(0, sx * 55f, 0);
            Tube("Arm", glasses, new Vector3(sx * hb2.extents.x * 0.92f, 0.01f, -0.035f), new Vector3(sx * hb2.extents.x * 0.98f, 0.0f, -hb2.size.z * 0.55f), 0.006f, frameMat);
        }
        Box("Frame top", glasses, new Vector3(0, 0.024f, 0), new Vector3(hb2.size.x * 0.64f, 0.007f, 0.01f), frameMat);
        glasses.SetParent(headBone, true);
        foreach (var c in helmet.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (var c in glasses.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (var r in helmet.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    /// <summary>A road helmet's shell on a unit ellipsoid: down to above the brow at the front, the ears at the sides and
    /// lower at the back, a little longer at the back; outside and inside.</summary>
    static Mesh HelmetMesh()
    {
        var path = $"{MeshDir}/RoadHelmet.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        const int seg = 48, rings = 14;
        var verts = new List<Vector3>(); var normals = new List<Vector3>(); var tris = new List<int>();
        for (int layer = 0; layer < 2; layer++)
        {
            float scale = layer == 0 ? 1f : 0.9f;
            int b0 = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float az = i * 2f * Mathf.PI / seg;   // 0: forward
                float rim = (6f + 16f * Mathf.Cos(az) - 4f * Mathf.Cos(2f * az)) * Mathf.Deg2Rad;
                float back = 1f + 0.14f * Mathf.Max(0f, -Mathf.Cos(az));
                for (int j = 0; j <= rings; j++)
                {
                    float el = Mathf.Lerp(rim, Mathf.PI / 2f, j / (float)rings);
                    var n = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az) * back);
                    verts.Add(n * scale);
                    normals.Add(layer == 0 ? n.normalized : -n.normalized);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < rings; j++)
                {
                    int a = b0 + i * (rings + 1) + j, b = a + rings + 1;
                    if (layer == 0) tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                    else tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
        }
        // the rim between them
        int inner = (seg + 1) * (rings + 1);
        for (int i = 0; i < seg; i++)
        {
            int a = i * (rings + 1), b = (i + 1) * (rings + 1);
            tris.AddRange(new[] { a, b, inner + a, inner + a, b, inner + b });
        }
        var mesh = new Mesh { name = "RoadHelmet" };
        mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>
    /// Registers the two-wheeler prefabs in StartScene's PlayerSystem (bicycle, e-scooter, moped, race bicycle: twoWheelerIdx
    /// 0-3) and appends the rider roles the experiments do not have yet: Cyclist, E-scooter rider and Moped rider to Base and
    /// the examples, and Race cyclist to those and to CityMap (after their other roles, so the existing role indices stay).
    /// </summary>
    [MenuItem("Coupled Sim/Two-wheelers/Add rider roles to Base and the examples")]
    public static void AddRoles()
    {
        // open the scene first: opening a scene unloads assets that nothing references yet
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Scenes/StartScene.unity");
        var prefabs = new[] { "Bicycle", "EScooter", "Moped", "RaceBicycle" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{n}.prefab")?.GetComponent<PlayerAvatar>()).ToArray();
        var ps = Object.FindAnyObjectByType<PlayerSystem>();
        var so = new SerializedObject(ps);
        var list = so.FindProperty("_AvatarPrefabTwoWheeler");
        list.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        var names = new[] { "Cyclist", "E-scooter rider", "Moped rider", "Race cyclist" };
        foreach (var exp in new[] { "Base", "Traffic", "SumoTraffic", "PedestrianCrossings", "CityMap" })
        {
            var path = $"Assets/ExperimentDefinitions/{exp}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var def = root.GetComponent<ExperimentDefinition>();
                var roles = def.Roles.ToList();
                bool city = root.GetComponentInChildren<OsmSpawnPoint>(true) != null || exp == "CityMap";
                var spawnParent = root.transform.Find("SpawnPoints") ?? root.transform;
                int added = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    if (roles.Any(r => r.Name == names[i]) || (city && i < 3)) continue;
                    var point = new GameObject(names[i] + " spawn").transform;
                    point.SetParent(spawnParent, true);
                    if (city)
                    {
                        // on the carriageway's edge (a road cyclist rides on the road), another place than the moped's
                        var sp = point.gameObject.AddComponent<OsmSpawnPoint>();
                        sp.Kind = OsmMap.SpawnKind.RoadEdge;
                        sp.Index = 2;
                    }
                    else
                    {
                        // right edge of the westbound lane south of the central block (MainScene)
                        point.position = new Vector3(-50f + 6f * i, 0f, 11.6f);
                        point.rotation = Quaternion.Euler(0, 270f, 0);
                    }
                    roles.Add(new ExperimentRoleDefinition
                    {
                        Name = names[i],
                        twoWheelerIdx = i,
                        SpawnPoint = new SpawnPoint { Point = point, Type = SpawnPointType.PlayerRidingTwoWheeler, CameraIndex = 0, VehicleType = PlayerSystem.VehicleType.MDV },
                    });
                    added++;
                }
                if (added == 0) continue;
                def.Roles = roles.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"TwoWheelerPrefabBuilder: {added} rider roles added to {path} (now {roles.Count} roles)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

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

    /// <summary>
    /// AIBicycle (AIRaceBicycle): the bicycle (race bicycle) as an externally driven AI vehicle, for the traffic and
    /// SUMO's bicycles: an AICar component (disabled) so it can be spawned and synced like AI cars; its rider's animation
    /// is skipped while it is out of sight.
    /// </summary>
    public static void BuildAIBicycle() => BuildAIBicycle("Bicycle", "AIBicycle");

    /// <summary>The frame paint's material name suffix of the bicycle being built (each rider variant its own colour).</summary>
    static string _paintSuffix = "";

    /// <summary>The traffic's cyclists: women and men of the Rocketbox avatars in their own clothes, each on a bicycle
    /// fitted to them (the saddle set to their legs) in a colour of its own. MapTraffic picks among every AI prefab
    /// with "Bicycle" (and not "Race") in its name. (Race riders stay one: the painted kit needs a figure-hugging body.)</summary>
    static readonly (string suffix, string rider, Color paint)[] RiderVariants =
    {
        ("F01", "Female_Adult_01", new Color(0.45f, 0.08f, 0.1f)),
        ("F08", "Female_Adult_08", new Color(0.42f, 0.62f, 0.55f)),
        ("F12", "Female_Adult_12", new Color(0.06f, 0.06f, 0.07f)),
        ("M02", "Male_Adult_02", new Color(0.35f, 0.36f, 0.38f)),
        ("M05", "Male_Adult_05", new Color(0.1f, 0.14f, 0.3f)),
        ("M09", "Male_Adult_09", new Color(0.12f, 0.25f, 0.14f)),
        ("M14", "Male_Adult_14", new Color(0.05f, 0.05f, 0.05f)),
        ("BM01", "Business_Male_01", new Color(0.6f, 0.6f, 0.62f)),
    };

    /// <summary>
    /// Builds an AI bicycle for each rider variant (AIBicycle_F01, AIBicycle_M05, ...) and registers them with the
    /// traffic's prefabs. For batch mode: -executeMethod TwoWheelerPrefabBuilder.BuildRiderVariants.
    /// </summary>
    [MenuItem("Coupled Sim/Two-wheelers/Build the traffic's cyclist variants")]
    public static void BuildRiderVariants()
    {
        Directory.CreateDirectory(MeshDir);
        Directory.CreateDirectory(MatDir);
        _materials.Clear();
        _cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var built = new List<string>();
        try
        {
            foreach (var (suffix, rider, paint) in RiderVariants)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{RocketboxImporter.Root}{rider}/{rider}.fbx") == null) { Debug.LogWarning($"TwoWheelerPrefabBuilder: no {rider}"); continue; }
                _paintSuffix = "_" + suffix;
                // (the participant's version is only the source of the AI one: removed after)
                Build(TwoWheelerType.Bicycle, "Bicycle_" + suffix, paint, rider);
                AssetDatabase.SaveAssets();
                BuildAIBicycle("Bicycle_" + suffix, "AIBicycle_" + suffix);
                AssetDatabase.DeleteAsset($"{Dir}/Bicycle_{suffix}.prefab");
                built.Add($"{Dir}/AIBicycle_{suffix}.prefab");
            }
        }
        finally { _paintSuffix = ""; }
        AssetDatabase.SaveAssets();
        TrafficTools.RegisterAICarPrefabs(built.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<AICar>()));
        AssetDatabase.SaveAssets();
        Debug.Log($"TwoWheelerPrefabBuilder: {built.Count} cyclist variants: {string.Join(", ", built.Select(Path.GetFileNameWithoutExtension))}");
    }

    public static void BuildAIBicycle(string from, string name)
    {
        var root = PrefabUtility.LoadPrefabContents($"{Dir}/{from}.prefab");
        try
        {
            root.name = name;
            var ai = root.AddComponent<AICar>();
            ai.enabled = false;
            ai.playerAvatar = root.GetComponent<PlayerAvatar>();
            ai.modelElements = new Transform[0];
            var tracker = root.AddComponent<UnityStandardAssets.Utility.WaypointProgressTracker>();
            tracker.enabled = false;
            foreach (var cam in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(cam.gameObject);
            root.GetComponent<PlayerAvatar>().cameras = new Camera[0];
            foreach (var an in root.GetComponentsInChildren<Animator>(true)) an.cullingMode = AnimatorCullingMode.CullCompletely;
            PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/{name}.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
