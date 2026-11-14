using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prepares a detailed 3D vehicle model (e.g. from Sketchfab) whose parts are not named or split the
/// way a vehicle needs: it finds the axles from where the tyres touch the ground, cuts each wheel out
/// of the meshes (so it can turn), cuts off the rear section of an articulated vehicle, and merges
/// the rest of the body into one mesh per material (fewer draw calls). The new meshes are saved in
/// binary in Assets/Prefabs/Cars/Vehicles/Meshes. Used by <see cref="VehicleBuilder"/>.
/// </summary>
public static class VehicleModelRig
{
    public sealed class Wheel
    {
        public Vector3 Centre;      // root space
        public float HalfWidth;
        public bool Left;
        public int Axle;
        public Transform Pivot;
        public bool OnTrailer;
    }

    public sealed class Result
    {
        public Bounds Bounds;                       // root space, from the vertices that are drawn
        public List<float> AxleZ = new List<float>();
        public List<Wheel> Wheels = new List<Wheel>();
        public Transform[] PhysicsPivots = new Transform[4];   // FL, FR, BL, BR (wheel colliders)
        public Transform Trailer;
        public float JointZ = float.NegativeInfinity;
        public float TrailerAxleZ;
        public List<Renderer> WheelRenderers = new List<Renderer>();
        public List<Transform> TrailerWheels = new List<Transform>();
        public Vector3[] Points = new Vector3[0];           // drawn vertices (root space), for placing lamps on the surface

        /// <summary>A point on the body's outside at a corner: at height y, near the front (end = +1) or back (-1), on the
        /// left (side = -1) or right (+1), <paramref name="inset"/> in from the side. Null when there is no surface there.</summary>
        public Vector3? Corner(int end, int side, float y, float inset, float band = 0.08f)
        {
            float zEnd = end > 0 ? Bounds.max.z : Bounds.min.z;
            float xMax = 0f; bool any = false;
            foreach (var p in Points)
                if (Mathf.Abs(p.y - y) < band && Mathf.Abs(p.z - zEnd) < 0.7f && Mathf.Sign(p.x - Bounds.center.x) == side)
                { xMax = Mathf.Max(xMax, Mathf.Abs(p.x - Bounds.center.x)); any = true; }
            if (!any) return null;
            float x = Bounds.center.x + side * Mathf.Max(0f, xMax - inset);
            float z = end > 0 ? float.NegativeInfinity : float.PositiveInfinity;
            foreach (var p in Points)
                if (Mathf.Abs(p.y - y) < band && Mathf.Abs(p.x - x) < 0.06f && Mathf.Abs(p.z - zEnd) < 0.7f)
                    z = end > 0 ? Mathf.Max(z, p.z) : Mathf.Min(z, p.z);
            if (float.IsInfinity(z)) return null;
            return new Vector3(x, y, z + end * 0.008f);
        }

        /// <summary>The first surface along a ray (root space): the nearest drawn vertex within <paramref name="width"/> of it.</summary>
        public Vector3? Hit(Vector3 from, Vector3 dir, float minDist, float maxDist, float width = 0.025f)
        {
            dir.Normalize();
            float best = float.PositiveInfinity;
            foreach (var p in Points)
            {
                var d = p - from; float t = Vector3.Dot(d, dir);
                if (t < minDist || t > maxDist || t >= best) continue;
                if ((d - dir * t).sqrMagnitude < width * width) best = t;
            }
            return float.IsInfinity(best) ? (Vector3?)null : from + dir * best;
        }

        /// <summary>The outermost point on one side between two heights and lengths (a door mirror's tip).</summary>
        public Vector3? Outermost(int side, float yMin, float yMax, float zMin, float zMax)
        {
            Vector3? best = null;
            foreach (var p in Points)
                if (p.y > yMin && p.y < yMax && p.z > zMin && p.z < zMax && Mathf.Sign(p.x - Bounds.center.x) == side)
                    if (best == null || Mathf.Abs(p.x - Bounds.center.x) > Mathf.Abs(best.Value.x - Bounds.center.x)) best = p;
            return best;
        }
    }

    // ----- vertices of the drawn triangles, in root space

    sealed class Source
    {
        public Renderer R; public Mesh M; public Vector3[] V; public Vector3[] W; public int[][] Tris; public Material[] Mats;
        public bool Steering;
    }

    static List<Source> Sources(Transform root, GameObject model, Transform steering)
    {
        var list = new List<Source>();
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || mf.sharedMesh == null || !r.enabled) continue;
            var m = mf.sharedMesh;
            var s = new Source { R = r, M = m, V = m.vertices, Mats = r.sharedMaterials, Steering = steering != null && mf.transform.IsChildOf(steering) };
            s.Tris = new int[m.subMeshCount][];
            for (int k = 0; k < m.subMeshCount; k++) s.Tris[k] = m.GetTopology(k) == MeshTopology.Triangles ? m.GetTriangles(k) : new int[0];
            var mat = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            s.W = new Vector3[s.V.Length];
            for (int i = 0; i < s.V.Length; i++) s.W[i] = mat.MultiplyPoint3x4(s.V[i]);
            list.Add(s);
        }
        return list;
    }

    /// <summary>Bounds (root space) of the vertices used by triangles: models often carry stray unused vertices.</summary>
    public static Bounds DrawnBounds(Transform root, GameObject model)
    {
        bool first = true; var b = new Bounds();
        foreach (var s in Sources(root, model, null))
            foreach (var tris in s.Tris)
                foreach (var i in tris)
                {
                    if (first) { b = new Bounds(s.W[i], Vector3.zero); first = false; }
                    else b.Encapsulate(s.W[i]);
                }
        return b;
    }

    // ----- meshes made of triangles taken from the sources

    sealed class Builder
    {
        public readonly string Name; public readonly Transform Space;
        readonly List<Vector3> _p = new List<Vector3>(); readonly List<Vector3> _n = new List<Vector3>(); readonly List<Vector4> _t = new List<Vector4>();
        readonly List<Color32> _c = new List<Color32>(); readonly List<Vector2> _uv = new List<Vector2>(); readonly List<Vector2> _uv1 = new List<Vector2>();
        readonly List<Material> _mats = new List<Material>(); readonly List<List<int>> _subs = new List<List<int>>();
        readonly Dictionary<Source, int[]> _remap = new Dictionary<Source, int[]>();
        readonly Dictionary<Source, (Matrix4x4 m, Matrix4x4 n, bool flip)> _mx = new Dictionary<Source, (Matrix4x4, Matrix4x4, bool)>();
        readonly Dictionary<Mesh, (Vector3[] n, Vector4[] t, Color32[] c, Vector2[] uv, Vector2[] uv1)> _attr = new Dictionary<Mesh, (Vector3[], Vector4[], Color32[], Vector2[], Vector2[])>();
        public int Triangles;
        bool _hasColor, _hasUv1;
        public Builder(string name, Transform space) { Name = name; Space = space; }

        public void Add(Source s, int sub, int a, int b, int c, Material mat)
        {
            if (!_mx.TryGetValue(s, out var mx))
            {
                var m = Space.worldToLocalMatrix * s.R.transform.localToWorldMatrix;
                mx = (m, m.inverse.transpose, m.determinant < 0f);
                _mx[s] = mx;
            }
            if (!_remap.TryGetValue(s, out var map)) { map = Enumerable.Repeat(-1, s.V.Length).ToArray(); _remap[s] = map; }
            if (!_attr.TryGetValue(s.M, out var at)) { at = (s.M.normals, s.M.tangents, s.M.colors32, s.M.uv, s.M.uv2); _attr[s.M] = at; }
            if (at.c.Length > 0) _hasColor = true;
            if (at.uv1.Length > 0) _hasUv1 = true;
            int si = _mats.IndexOf(mat);
            if (si < 0) { si = _mats.Count; _mats.Add(mat); _subs.Add(new List<int>()); }
            int V(int i)
            {
                if (map[i] >= 0) return map[i];
                map[i] = _p.Count;
                _p.Add(mx.m.MultiplyPoint3x4(s.V[i]));
                _n.Add(at.n.Length > i ? mx.n.MultiplyVector(at.n[i]).normalized : Vector3.up);
                if (at.t.Length > i) { var t = mx.m.MultiplyVector(at.t[i]).normalized; _t.Add(new Vector4(t.x, t.y, t.z, at.t[i].w * (mx.flip ? -1f : 1f))); }
                else _t.Add(new Vector4(1f, 0f, 0f, 1f));
                _c.Add(at.c.Length > i ? at.c[i] : new Color32(255, 255, 255, 255));
                _uv.Add(at.uv.Length > i ? at.uv[i] : Vector2.zero);
                _uv1.Add(at.uv1.Length > i ? at.uv1[i] : Vector2.zero);
                return map[i];
            }
            var l = _subs[si];
            int va = V(a), vb = V(b), vc = V(c);
            if (mx.flip) { l.Add(va); l.Add(vc); l.Add(vb); } else { l.Add(va); l.Add(vb); l.Add(vc); }
            Triangles++;
        }

        static uint Snorm4(float x, float y, float z, float w)
        {
            uint B(float v) => (uint)(byte)(sbyte)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 127f);
            return B(x) | (B(y) << 8) | (B(z) << 16) | (B(w) << 24);
        }

        /// <summary>The mesh, in compact vertex formats: 8-bit normals and tangents; colours and second UVs only
        /// when a source part had them.</summary>
        public Mesh Make()
        {
            var mesh = new Mesh { name = Name, indexFormat = _p.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            bool colors = _hasColor, uv1 = _hasUv1;
            var layout = new List<UnityEngine.Rendering.VertexAttributeDescriptor>
            {
                new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Position, UnityEngine.Rendering.VertexAttributeFormat.Float32, 3, 0),
                new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Normal, UnityEngine.Rendering.VertexAttributeFormat.SNorm8, 4, 1),
                new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Tangent, UnityEngine.Rendering.VertexAttributeFormat.SNorm8, 4, 1),
            };
            if (colors) layout.Add(new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Color, UnityEngine.Rendering.VertexAttributeFormat.UNorm8, 4, 1));
            layout.Add(new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.TexCoord0, UnityEngine.Rendering.VertexAttributeFormat.Float32, 2, 2));
            if (uv1) layout.Add(new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.TexCoord1, UnityEngine.Rendering.VertexAttributeFormat.Float16, 2, 2));
            int n = _p.Count;
            mesh.SetVertexBufferParams(n, layout.ToArray());
            mesh.SetVertexBufferData(_p, 0, 0, n, 0);
            int s1 = colors ? 3 : 2, s2 = uv1 ? 3 : 2;
            var st1 = new uint[n * s1]; var st2 = new uint[n * s2];
            for (int i = 0; i < n; i++)
            {
                var nn = _n[i]; var t = _t[i];
                st1[i * s1] = Snorm4(nn.x, nn.y, nn.z, 0f);
                st1[i * s1 + 1] = Snorm4(t.x, t.y, t.z, t.w < 0f ? -1f : 1f);
                if (colors) { var c = _c[i]; st1[i * s1 + 2] = (uint)c.r | ((uint)c.g << 8) | ((uint)c.b << 16) | ((uint)c.a << 24); }
                st2[i * s2] = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(_uv[i].x), 0);
                st2[i * s2 + 1] = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(_uv[i].y), 0);
                if (uv1) st2[i * s2 + 2] = Mathf.FloatToHalf(_uv1[i].x) | ((uint)Mathf.FloatToHalf(_uv1[i].y) << 16);
            }
            mesh.SetVertexBufferData(st1, 0, 0, st1.Length, 1);
            mesh.SetVertexBufferData(st2, 0, 0, st2.Length, 2);
            mesh.subMeshCount = _subs.Count;
            for (int k = 0; k < _subs.Count; k++) mesh.SetTriangles(_subs[k], k, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        public Material[] Materials => _mats.ToArray();

        public GameObject Attach(Mesh mesh, Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = Materials;
            return go;
        }
    }

    // ----- transparent materials used for whole bodies (a texture atlas with the windows in it): merged into one
    // mesh they would be drawn out of order, so the triangles whose texture is fully opaque get an opaque copy

    static class Opaque
    {
        static readonly Dictionary<Material, (Color32[] px, int w, int h, Vector2 scale, Vector2 offset, float alpha, Material opaque)> _cache = new Dictionary<Material, (Color32[], int, int, Vector2, Vector2, float, Material)>();
        static readonly Dictionary<Mesh, Vector2[]> _uv = new Dictionary<Mesh, Vector2[]>();
        public static readonly List<Material> Made = new List<Material>();

        public static void Reset() { _cache.Clear(); _uv.Clear(); Made.Clear(); }

        static bool Transparent(Material m) => m != null && (m.renderQueue >= 2900 || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) && !m.IsKeywordEnabled("_TRANSMISSION");

        public static Material For(Material m, Mesh mesh, int a, int b, int c)
        {
            if (!Transparent(m)) return m;
            if (!_cache.TryGetValue(m, out var e))
            {
                var tex = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") as Texture2D : null;
                Color32[] px = null; int w = 0, h = 0;
                if (tex != null)
                {
                    // read through the GPU: imported textures are not readable
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);
                    copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); copy.Apply();
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    px = copy.GetPixels32(); w = copy.width; h = copy.height;
                    Object.DestroyImmediate(copy);
                }
                float alpha = m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor").a : 1f;
                Material opaque = null;
                if (alpha > 0.97f)
                {
                    opaque = new Material(m) { name = m.name + " (opaque)" };
                    opaque.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    opaque.DisableKeyword("_DISABLE_SSR_TRANSPARENT");
                    opaque.DisableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                    if (opaque.HasProperty("_Surface")) opaque.SetFloat("_Surface", 0f);
                    if (opaque.HasProperty("_SrcBlend")) opaque.SetFloat("_SrcBlend", 1f);
                    if (opaque.HasProperty("_DstBlend")) opaque.SetFloat("_DstBlend", 0f);
                    if (opaque.HasProperty("_ZWrite")) opaque.SetFloat("_ZWrite", 1f);
                    opaque.SetOverrideTag("RenderType", "Opaque");
                    opaque.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    opaque.SetShaderPassEnabled("ShadowCaster", true);
                    opaque.SetShaderPassEnabled("DepthOnly", true);
                    opaque.SetShaderPassEnabled("DepthNormals", true);
                    Made.Add(opaque);
                }
                var st = m.HasProperty("baseColorTexture") ? (m.GetTextureScale("baseColorTexture"), m.GetTextureOffset("baseColorTexture")) : (Vector2.one, Vector2.zero);
                e = (px, w, h, st.Item1, st.Item2, alpha, opaque);
                _cache[m] = e;
            }
            if (e.opaque == null) return m;
            if (e.px == null) return e.opaque;     // no texture: the colour's alpha decides
            if (!_uv.TryGetValue(mesh, out var uv)) { uv = mesh.uv; _uv[mesh] = uv; }
            if (uv.Length == 0) return e.opaque;
            float A(Vector2 t)
            {
                t = Vector2.Scale(t, e.scale) + e.offset;
                int x = (int)(Mathf.Repeat(t.x, 1f) * e.w) % e.w, y = (int)(Mathf.Repeat(t.y, 1f) * e.h) % e.h;
                return e.px[y * e.w + x].a / 255f;
            }
            var ua = uv[a]; var ub = uv[b]; var uc = uv[c];
            float min = Mathf.Min(Mathf.Min(A(ua), A(ub)), Mathf.Min(A(uc), A((ua + ub + uc) / 3f)));
            return min > 0.97f ? e.opaque : m;
        }
    }

    // ----- the whole job

    /// <param name="ground">height of the ground in root space (the model already stands on it)</param>
    public static Result Rig(Transform root, Transform carModel, GameObject model, VehicleBuilder.Def d, float ground, string meshFolder)
    {
        var res = new Result();
        var steering = d.SteeringWheel != null ? model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == d.SteeringWheel) : null;
        var sources = Sources(root, model, steering);
        bool first = true;
        foreach (var s in sources) foreach (var tris in s.Tris) foreach (var i in tris) { if (first) { res.Bounds = new Bounds(s.W[i], Vector3.zero); first = false; } else res.Bounds.Encapsulate(s.W[i]); }
        var b = res.Bounds;
        res.Points = sources.Where(x => !x.Steering).SelectMany(x => x.W).ToArray();

        // contact patches: drawn vertices within 6 cm of the ground (models often sit a little nose up or down), per side, grouped along the length
        var contacts = new List<Vector3>();
        var contactNames = new Dictionary<string, int>();
        var seen = new HashSet<(Source, int)>();
        foreach (var s in sources)
        {
            if (s.Steering) continue;
            foreach (var tris in s.Tris) foreach (var i in tris) if (s.W[i].y < ground + 0.06f && seen.Add((s, i))) { contacts.Add(s.W[i]); contactNames[s.R.name] = contactNames.TryGetValue(s.R.name, out var cn) ? cn + 1 : 1; }
        }
        var patches = new List<(bool left, float z, float xMin, float xMax, float yMin)>();
        foreach (var side in new[] { true, false })
        {
            var pts = contacts.Where(p => (p.x < b.center.x) == side).OrderBy(p => p.z).ToList();
            int start = 0;
            for (int i = 1; i <= pts.Count; i++)
                if (i == pts.Count || pts[i].z - pts[i - 1].z > 0.5f)
                {
                    var g = pts.GetRange(start, i - start);
                    if (g.Count >= 2) patches.Add((side, (g.Min(p => p.z) + g.Max(p => p.z)) / 2f, g.Min(p => p.x), g.Max(p => p.x), g.Min(p => p.y)));
                    start = i;
                }
        }
        // axles: patches on both sides at about the same distance from the front
        var axles = new List<float>();
        foreach (var p in patches.Where(p => p.left).OrderByDescending(p => p.z))
        {
            var other = patches.Where(q => !q.left && Mathf.Abs(q.z - p.z) < 0.5f).ToList();
            if (other.Count == 0) continue;
            axles.Add((p.z + other.Average(q => q.z)) / 2f);
        }
        if (axles.Count < 2)
            foreach (var src in sources.OrderByDescending(x => { var bb = new Bounds(x.W[0], Vector3.zero); foreach (var w in x.W) bb.Encapsulate(w); return bb.size.sqrMagnitude; }).Take(6))
            { var bb = new Bounds(src.W[0], Vector3.zero); foreach (var w in src.W) bb.Encapsulate(w); Debug.Log($"VehicleModelRig: part {src.R.name} {bb.min:F2}..{bb.max:F2} scale {src.R.transform.lossyScale:F3} parent {src.R.transform.parent?.name}"); }
        if (axles.Count < 2) throw new System.Exception($"found {axles.Count} axles (contact patches: {string.Join(" ", patches.Select(p => $"{(p.left ? "L" : "R")}{p.z:F2}"))}; parts {string.Join(", ", contactNames.Select(kv => kv.Key + " " + kv.Value))}; ground {ground:F2}, bounds {b.min:F2}..{b.max:F2})");
        res.AxleZ = axles;
        float r = d.WheelRadius;
        if (d.ArticulationFromFront > 0f) res.JointZ = b.max.z - d.ArticulationFromFront;

        // the wheels and their pivots (under the car model; a trailer's under the trailer)
        if (d.ArticulationFromFront > 0f)
        {
            res.Trailer = new GameObject("Rear section").transform;
            res.Trailer.SetParent(root, false);
            res.Trailer.position = root.TransformPoint(new Vector3(b.center.x, ground + r, res.JointZ));
            res.Trailer.gameObject.layer = root.gameObject.layer;
        }
        for (int a = 0; a < axles.Count; a++)
            foreach (var left in new[] { true, false })
            {
                var p = patches.Where(q => q.left == left).OrderBy(q => Mathf.Abs(q.z - axles[a])).First();
                var w = new Wheel { Left = left, Axle = a, Centre = new Vector3((p.xMin + p.xMax) / 2f, p.yMin + r, axles[a]), HalfWidth = (p.xMax - p.xMin) / 2f + 0.05f, OnTrailer = axles[a] < res.JointZ };
                w.Pivot = new GameObject($"Wheel A{a + 1}{(left ? "L" : "R")}").transform;
                w.Pivot.SetParent(w.OnTrailer ? res.Trailer : carModel, false);
                w.Pivot.position = root.TransformPoint(w.Centre);
                w.Pivot.rotation = root.rotation;
                res.Wheels.Add(w);
                if (w.OnTrailer) res.TrailerWheels.Add(w.Pivot);
            }
        var front = res.Wheels.Where(w => w.Axle == 0).ToArray();
        res.PhysicsPivots[0] = front.First(w => w.Left).Pivot; res.PhysicsPivots[1] = front.First(w => !w.Left).Pivot;
        var rearAxles = Enumerable.Range(1, axles.Count - 1).Where(a => !(axles[a] < res.JointZ)).ToList();
        if (rearAxles.Count == 0) throw new System.Exception("no rear axle in front of the articulation");
        res.TrailerAxleZ = axles.Where(z => z < res.JointZ).DefaultIfEmpty(res.JointZ - 5f).Average();
        foreach (var left in new[] { true, false })
        {
            var group = res.Wheels.Where(w => w.Left == left && rearAxles.Contains(w.Axle)).ToList();
            Transform phys;
            if (group.Count == 1) phys = group[0].Pivot;
            else
            {
                // a tandem: the wheel collider halfway between the axles, the real wheels follow it
                phys = new GameObject($"Wheel group {(left ? "L" : "R")}").transform;
                phys.SetParent(carModel, false);
                phys.position = root.TransformPoint(new Vector3(group.Average(w => w.Centre.x), group.Average(w => w.Centre.y), group.Average(w => w.Centre.z)));
                phys.rotation = root.rotation;
                foreach (var w in group) w.Pivot.gameObject.AddComponent<WheelFollower>().Source = phys;
            }
            res.PhysicsPivots[left ? 2 : 3] = phys;
        }

        // every triangle to: a wheel, the rear section, the body (merged) or left alone
        Opaque.Reset();
        var body = new Builder($"{d.Name} body", model.transform);
        var rear = res.Trailer != null ? new Builder($"{d.Name} rear section", res.Trailer) : null;
        var wheelB = res.Wheels.Select(w => new Builder($"{d.Name} wheel {w.Pivot.name}", w.Pivot)).ToArray();
        float r2 = (r * 1.04f) * (r * 1.04f);
        int WheelOf(Vector3 v)
        {
            for (int k = 0; k < res.Wheels.Count; k++)
            {
                var w = res.Wheels[k];
                if (Mathf.Abs(v.x - w.Centre.x) > w.HalfWidth) continue;
                float dy = v.y - w.Centre.y, dz = v.z - w.Centre.z;
                if (dy * dy + dz * dz <= r2) return k;
            }
            return -1;
        }
        var consumed = new List<Renderer>();
        foreach (var s in sources)
        {
            if (s.Steering) continue;
            var wheelOf = new int[s.V.Length];
            for (int i = 0; i < wheelOf.Length; i++) wheelOf[i] = -2;
            int W(int i) { if (wheelOf[i] == -2) wheelOf[i] = WheelOf(s.W[i]); return wheelOf[i]; }
            bool touched = d.Combine || res.Trailer != null;
            if (!touched)
                foreach (var tris in s.Tris) { for (int t = 0; t < tris.Length && !touched; t += 3) { int k = W(tris[t]); if (k >= 0 && W(tris[t + 1]) == k && W(tris[t + 2]) == k) touched = true; } if (touched) break; }
            if (!touched) continue;
            for (int sub = 0; sub < s.Tris.Length; sub++)
            {
                var tris = s.Tris[sub];
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], bb = tris[t + 1], c = tris[t + 2];
                    int k = W(a);
                    var mat = Opaque.For(s.Mats.Length == 0 ? null : s.Mats[Mathf.Min(sub, s.Mats.Length - 1)], s.M, a, bb, c);
                    if (k >= 0 && W(bb) == k && W(c) == k) { wheelB[k].Add(s, sub, a, bb, c, mat); continue; }
                    if (rear != null && (s.W[a].z + s.W[bb].z + s.W[c].z) / 3f < res.JointZ) { rear.Add(s, sub, a, bb, c, mat); continue; }
                    body.Add(s, sub, a, bb, c, mat);
                }
            }
            consumed.Add(s.R);
        }

        // save the meshes (binary) and put them in place of the parts they came from
        Directory.CreateDirectory(meshFolder);
        var assetPath = $"{meshFolder}/{d.Name}.asset";
        AssetDatabase.DeleteAsset(assetPath);
        var store = ScriptableObject.CreateInstance<VehicleMeshData>();
        store.Source = d.Model;
        AssetDatabase.CreateAsset(store, assetPath);
        foreach (var m in Opaque.Made) AssetDatabase.AddObjectToAsset(m, store);
        int meshes = 0;
        void Keep(Builder bl, Transform parent, string name, bool wheel)
        {
            if (bl == null || bl.Triangles == 0) return;
            var mesh = bl.Make();
            AssetDatabase.AddObjectToAsset(mesh, store);
            var go = bl.Attach(mesh, parent, name);
            if (wheel) res.WheelRenderers.Add(go.GetComponent<Renderer>());
            meshes++;
        }
        Keep(body, model.transform, "Body (merged)", false);
        Keep(rear, res.Trailer, "Rear section (merged)", false);
        for (int k = 0; k < wheelB.Length; k++) Keep(wheelB[k], res.Wheels[k].Pivot, "Wheel mesh", true);
        foreach (var rr in consumed)
        {
            var mf = rr.GetComponent<MeshFilter>();
            Object.DestroyImmediate(rr);
            if (mf != null) Object.DestroyImmediate(mf);
        }
        // empty leaves left behind
        bool removed = true;
        while (removed)
        {
            removed = false;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t != model.transform && t.childCount == 0 && t.GetComponents<Component>().Length == 1 && (steering == null || !t.IsChildOf(steering))) { Object.DestroyImmediate(t.gameObject); removed = true; }
        }
        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssets();
        Debug.Log($"VehicleModelRig: {d.Name}: {axles.Count} axles at z {string.Join(", ", axles.Select(z => z.ToString("F2")))}, {res.Wheels.Count} wheels ({string.Join(" ", wheelB.Select(x => x.Triangles))} triangles), body {body.Triangles}, rear {rear?.Triangles ?? 0}, {consumed.Count} parts merged into {meshes} meshes");
        return res;
    }
}
