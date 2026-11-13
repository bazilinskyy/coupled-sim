using System;
using System.Collections.Generic;
using UnityEngine;

public enum TrafficSignKind { Stop, GiveWay, SpeedLimit, PedestrianCrossing, NoEntry, OneWay, PriorityRoad, Children }

/// <summary>
/// A traffic sign on a pole (#24), drawn from code in the style of the Vienna Convention used in
/// Europe (stop, give way, speed limit, pedestrian crossing, no entry, one way, priority road,
/// children): no third-party assets. Place it anywhere (GameObject ➡️ Coupled Sim ➡️ Traffic
/// sign) with its front facing the traffic it is for; city maps get them from OpenStreetMap
/// (stop and give-way nodes, crossings, speed limits). The pole stands at the object's position.
/// </summary>
[ExecuteAlways]
public class TrafficSign : MonoBehaviour
{
    public TrafficSignKind Kind = TrafficSignKind.Stop;
    [Tooltip("SpeedLimit: km/h (or mph where that is the custom: the number is shown as is).")]
    public int SpeedLimit = 50;
    [Tooltip("Size of the sign (m): 0.6 in towns, 0.9 on main roads.")]
    public float Size = 0.65f;
    [Tooltip("Height of the sign's lower edge above the ground (m).")]
    public float Height = 2.2f;

    const string VisualName = "Sign (generated)";
    TrafficSignKind _builtKind;
    int _builtLimit;
    float _builtSize, _builtHeight;

    void OnEnable() => Rebuild();

    void OnValidate()
    {
#if UNITY_EDITOR
        // rebuilding destroys objects: not allowed in OnValidate itself
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };
#endif
    }

    public void Rebuild()
    {
        var existing = transform.Find(VisualName);
        if (existing != null && _builtKind == Kind && _builtLimit == SpeedLimit && Mathf.Approximately(_builtSize, Size) && Mathf.Approximately(_builtHeight, Height)) return;
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
        }
        _builtKind = Kind; _builtLimit = SpeedLimit; _builtSize = Size; _builtHeight = Height;
        var root = new GameObject(VisualName) { hideFlags = HideFlags.DontSave }.transform;
        root.SetParent(transform, false);

        float plateH = Size;
        float top = Height + plateH;
        // pole: a thin box (cheaper than a cylinder and looks the same at a distance)
        var pole = Quad(root, "Pole", PoleMesh(0.03f, top + 0.05f), PoleMaterial);
        pole.transform.localPosition = new Vector3(0f, 0f, -0.04f);

        var plate = new GameObject("Plate").transform;
        plate.SetParent(root, false);
        plate.localPosition = new Vector3(0f, Height + plateH / 2f, 0f);
        float w = Size;
        // the picture faces the object's forward (+z) axis: point that at the oncoming traffic
        Quad(plate, "Front", PlateMesh(w, plateH, false), SignMaterial(Kind, SpeedLimit));
        Quad(plate, "Back", PlateMesh(w, plateH, true), BackMaterial(Kind, SpeedLimit));
    }

    static GameObject Quad(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        return go;
    }

    // ---- meshes and materials (shared by all signs)

    static readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

    static Mesh PlateMesh(float w, float h, bool back)
    {
        var key = $"plate {w:F2} {h:F2} {back}";
        if (_meshes.TryGetValue(key, out var m) && m != null) return m;
        m = new Mesh { name = key, hideFlags = HideFlags.DontSave };
        float z = back ? -0.006f : 0.006f;
        m.vertices = new[] { new Vector3(-w / 2, -h / 2, z), new Vector3(w / 2, -h / 2, z), new Vector3(w / 2, h / 2, z), new Vector3(-w / 2, h / 2, z) };
        // the front looks towards +z (towards the traffic): the back's picture is mirrored
        m.uv = back ? new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }
                    : new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = back ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        _meshes[key] = m;
        return m;
    }

    static Mesh PoleMesh(float r, float h)
    {
        var key = $"pole {r:F3} {h:F2}";
        if (_meshes.TryGetValue(key, out var m) && m != null) return m;
        m = new Mesh { name = key, hideFlags = HideFlags.DontSave };
        var v = new List<Vector3>(); var t = new List<int>();
        int seg = 8;
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            v.Add(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r));
        }
        for (int i = 0; i < seg; i++) { int b = i * 2; t.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 }); }
        m.SetVertices(v); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        _meshes[key] = m;
        return m;
    }

    static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

    static Material PoleMaterial
    {
        get
        {
            if (_materials.TryGetValue("pole", out var m) && m != null) return m;
            m = new Material(LitShader) { name = "Sign pole", hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", new Color(0.55f, 0.56f, 0.57f));
            m.SetFloat("_Metallic", 0.6f); m.SetFloat("_Smoothness", 0.45f);
            return _materials["pole"] = m;
        }
    }

    static Material Cutout(string name, Texture2D tex, float smooth)
    {
        var m = new Material(LitShader) { name = name, hideFlags = HideFlags.DontSave };
        m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Cull", 2f);
        return m;
    }

    static Material SignMaterial(TrafficSignKind kind, int limit)
    {
        var key = $"{kind} {limit}";
        if (_materials.TryGetValue(key, out var m) && m != null) return m;
        return _materials[key] = Cutout("Sign " + key, SignPainter.Paint(kind, limit, false), 0.55f);
    }

    static Material BackMaterial(TrafficSignKind kind, int limit)
    {
        var key = $"back {kind}";
        if (_materials.TryGetValue(key, out var m) && m != null) return m;
        return _materials[key] = Cutout("Sign back " + kind, SignPainter.Paint(kind, limit, true), 0.3f);
    }
}
