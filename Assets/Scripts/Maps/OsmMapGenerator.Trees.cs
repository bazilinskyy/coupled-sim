using System;
using System.Collections.Generic;
using CoupledSim.Maps;
using UnityEngine;
using V2 = System.Numerics.Vector2;

/// <summary>
/// Conifers and palms, made from code (no third-party models): the trees of the cities' registers and OpenStreetMap
/// that are pines, spruces, firs, cedars, cypresses, ... (needle-leaved) or palms get their own shapes instead of the
/// broadleaf models. A conifer: a tapering trunk and tiers of drooping needle branches, narrowing to the top; a palm:
/// a slender, slightly bent, ringed trunk and a crown of pinnate fronds arching out and down. Solid geometry (both
/// sides of the fronds), so no alpha-tested shader variant is needed. Three variants of each; one unit tall, scaled
/// to the tree's height.
/// </summary>
public partial class OsmMapGenerator
{
    static Mesh[] _coniferMeshes, _palmMeshes;
    Material _barkMat, _needleMat, _palmBarkMat, _frondMat;

    static readonly string[] Conifers = { "pinus", "picea", "abies", "taxus", "thuja", "larix", "pseudotsuga", "metasequoia", "taxodium",
        "chamaecyparis", "cupressus", "juniperus", "cedrus", "sequoia", "sequoiadendron", "tsuga", "cryptomeria", "araucaria", "cupressocyparis", "xcupressocyparis" };
    static readonly string[] Palms = { "phoenix", "washingtonia", "trachycarpus", "chamaerops", "livistona", "syagrus", "butia", "archontophoenix", "howea", "cocos", "roystonea", "sabal", "arecaceae" };

    /// <summary>"conifer", "palm" or "deciduous" from a species name or OSM tags.</summary>
    public static string TreeKind(string species, string leafType = null)
    {
        if (leafType == "needleleaved") return "conifer";
        if (string.IsNullOrEmpty(species)) return "deciduous";
        var s = species.ToLowerInvariant();
        foreach (var g in Palms) if (s.StartsWith(g) || s.Contains(" " + g) || s.Contains("palm")) return "palm";
        foreach (var g in Conifers) if (s.StartsWith(g) || s.Contains("(" + g)) return "conifer";
        return "deciduous";
    }

    void TreeMaterials()
    {
        if (_barkMat != null) return;
        _barkMat = Mat("Tree bark", Color.white, 0.05f, Tex("bark", 128, (x, y, r) =>
        {
            float v = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.35f, y * 0.04f) + ((float)r.NextDouble() - 0.5f) * 0.1f;
            return new Color(0.33f, 0.24f, 0.18f) * v;
        }));
        _needleMat = Mat("Conifer needles", Color.white, 0.08f, Tex("needles", 128, (x, y, r) =>
        {
            float v = 0.7f + 0.3f * (float)r.NextDouble();
            return new Color(0.11f, 0.24f, 0.13f) * v;
        }));
        _palmBarkMat = Mat("Palm trunk", Color.white, 0.05f, Tex("palm_trunk", 128, (x, y, r) =>
        {
            float ring = (y % 16) < 3 ? 0.7f : 1f;   // the leaf scars, ring after ring
            return new Color(0.48f, 0.42f, 0.34f) * ring * (0.85f + 0.15f * (float)r.NextDouble());
        }));
        _frondMat = Mat("Palm fronds", Color.white, 0.15f, Tex("fronds", 64, (x, y, r) =>
            new Color(0.25f, 0.4f, 0.16f) * (0.8f + 0.25f * (float)r.NextDouble())));
    }

    /// <summary>A conifer or palm standing at <paramref name="pos"/>, <paramref name="height"/> metres tall.</summary>
    GameObject MakeTree(string kind, Vector3 pos, float height, float yaw, Transform parent, int variant)
    {
        TreeMaterials();
        bool palm = kind == "palm";
        if (palm && _palmMeshes == null) _palmMeshes = new[] { PalmMesh(1), PalmMesh(2), PalmMesh(3) };
        if (!palm && _coniferMeshes == null) _coniferMeshes = new[] { ConiferMesh(1), ConiferMesh(2), ConiferMesh(3) };
        var go = new GameObject(palm ? "Palm" : "Conifer");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = Vector3.one * height;
        go.AddComponent<MeshFilter>().sharedMesh = (palm ? _palmMeshes : _coniferMeshes)[Math.Abs(variant) % 3];
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = palm ? new[] { _palmBarkMat, _frondMat } : new[] { _barkMat, _needleMat };
        return go;
    }

    // ---- meshes (one unit tall; sub-mesh 0 the trunk, 1 the foliage)

    sealed class TwoPart
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<int> A = new List<int>(), B = new List<int>();
        public int Add(Vector3 v, Vector2 uv) { V.Add(v); UV.Add(uv); return V.Count - 1; }
        public void Tri(List<int> t, int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); }
        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(V); m.SetUVs(0, UV);
            m.subMeshCount = 2; m.SetTriangles(A, 0); m.SetTriangles(B, 1);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }

    // a tapering tube along a path (rings of n vertices), outward faces
    static void Tube(TwoPart mb, List<int> tris, IList<Vector3> path, IList<float> radii, int n, float uvV)
    {
        int start = mb.V.Count;
        for (int i = 0; i < path.Count; i++)
        {
            var fwd = (i + 1 < path.Count ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
            var side = Vector3.Cross(fwd, Vector3.forward).sqrMagnitude > 1e-4f ? Vector3.Cross(fwd, Vector3.forward).normalized : Vector3.right;
            var up2 = Vector3.Cross(side, fwd);
            for (int k = 0; k <= n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                mb.Add(path[i] + (side * Mathf.Cos(a) + up2 * Mathf.Sin(a)) * radii[i], new Vector2(k / (float)n, i * uvV));
            }
        }
        for (int i = 0; i + 1 < path.Count; i++)
            for (int k = 0; k < n; k++)
            {
                int a = start + i * (n + 1) + k, b = a + 1, c = a + n + 1, d = c + 1;
                mb.Tri(tris, a, c, b); mb.Tri(tris, b, c, d);
            }
    }

    static Mesh ConiferMesh(int seed)
    {
        var rng = new System.Random(seed * 7919);
        var mb = new TwoPart();
        Tube(mb, mb.A, new[] { Vector3.zero, Vector3.up * 0.6f, Vector3.up * 0.95f }, new[] { 0.025f, 0.015f, 0.004f }, 7, 4f);
        // tiers of drooping branches: a skirt (cone) per tier, wider and lower below, with a ragged edge
        int tiers = 7 + rng.Next(3);
        float bottom = 0.12f + 0.05f * (float)rng.NextDouble();
        for (int t = 0; t < tiers; t++)
        {
            float f = t / (float)(tiers - 1);                         // 0 at the bottom tier, 1 at the top
            float top = Mathf.Lerp(bottom + 0.22f, 1.0f, f);
            float baseY = top - Mathf.Lerp(0.26f, 0.12f, f);
            float radius = Mathf.Lerp(0.24f, 0.05f, f) * (0.9f + 0.2f * (float)rng.NextDouble());
            int n = 14;
            int apex = mb.Add(new Vector3(0, top, 0), new Vector2(0.5f, 1f));
            int ring = mb.V.Count;
            for (int k = 0; k <= n; k++)
            {
                float a = k * Mathf.PI * 2f / n + t * 0.37f;
                float rr = radius * (k % 2 == 0 ? 1f : 0.78f) * (0.9f + 0.2f * (float)rng.NextDouble());   // branch tips and gaps
                float droop = (k % 2 == 0 ? 0.03f : 0f);
                mb.Add(new Vector3(Mathf.Cos(a) * rr, baseY - droop, Mathf.Sin(a) * rr), new Vector2(k / (float)n, 0f));
            }
            for (int k = 0; k < n; k++) { mb.Tri(mb.B, apex, ring + k + 1, ring + k); }
            // the underside, a little higher in the middle (seen from below)
            int under = mb.Add(new Vector3(0, baseY + 0.05f, 0), new Vector2(0.5f, 0.5f));
            for (int k = 0; k < n; k++) { mb.Tri(mb.B, under, ring + k, ring + k + 1); }
        }
        return mb.Build("Conifer " + seed);
    }

    static Mesh PalmMesh(int seed)
    {
        var rng = new System.Random(seed * 104729);
        var mb = new TwoPart();
        // the trunk, slightly bent, a little thicker at the foot
        var bend = new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f).normalized * (0.03f + 0.04f * (float)rng.NextDouble());
        var path = new List<Vector3>(); var radii = new List<float>();
        for (int i = 0; i <= 8; i++)
        {
            float f = i / 8f;
            path.Add(new Vector3(0, f * 0.86f, 0) + bend * f * f);
            radii.Add(Mathf.Lerp(0.03f, 0.022f, f) * (i == 0 ? 1.3f : 1f));
        }
        Tube(mb, mb.A, path, radii, 8, 14f);
        var crown = path[path.Count - 1];
        // fronds: a rachis arching out and down, about half the tree's height long, with dense narrow leaflets on both
        // sides (each leaflet a thin triangle, both faces)
        int fronds = 14 + rng.Next(6);
        for (int fi = 0; fi < fronds; fi++)
        {
            float az = fi * Mathf.PI * 2f / fronds + (float)rng.NextDouble() * 0.3f;
            float rise = Mathf.Lerp(1.1f, -0.2f, (float)rng.NextDouble());   // young fronds up, old ones hanging
            float len = 0.42f + 0.12f * (float)rng.NextDouble();
            var outward = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
            var sideDir = Vector3.Cross(Vector3.up, outward).normalized;
            const int segs = 14;
            var pts = new Vector3[segs + 1];
            for (int s = 0; s <= segs; s++)
            {
                float f = s / (float)segs;
                // a parabola: out along the frond, up at first, then down under its weight
                pts[s] = crown + outward * (len * f) + Vector3.up * (len * (rise * f - 1.0f * f * f));
            }
            for (int s = 1; s <= segs; s++)
            {
                float f = s / (float)segs;
                float leaf = 0.11f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(f * 1.05f)) + 0.012f;   // leaflets longest mid-frond
                for (int half = 0; half < 2; half++)
                {
                    var b0 = Vector3.Lerp(pts[s - 1], pts[s], half * 0.5f);
                    var b1 = Vector3.Lerp(pts[s - 1], pts[s], half * 0.5f + 0.5f);
                    var dir = (b1 - b0).normalized;
                    foreach (var sd in new[] { -1f, 1f })
                    {
                        var tip = b0 + sideDir * sd * leaf + dir * leaf * 0.6f + Vector3.down * leaf * 0.45f;
                        var b0w = b0 - dir * 0.004f; var b1w = b0 + dir * 0.012f;   // a narrow base
                        // both faces, each with its own vertices (shared ones would average the normals away)
                        int a = mb.Add(b0w, new Vector2(0, 0)), b = mb.Add(b1w, new Vector2(1, 0)), c = mb.Add(tip, new Vector2(0.5f, 1));
                        mb.Tri(mb.B, a, b, c);
                        int a2 = mb.Add(b0w, new Vector2(0, 0)), b2 = mb.Add(b1w, new Vector2(1, 0)), c2 = mb.Add(tip, new Vector2(0.5f, 1));
                        mb.Tri(mb.B, a2, c2, b2);
                    }
                }
                // the rachis itself
                {
                    var w = sideDir * 0.006f;
                    int a = mb.Add(pts[s - 1] - w, Vector2.zero), b = mb.Add(pts[s] - w, Vector2.right), c = mb.Add(pts[s] + w, Vector2.one), d = mb.Add(pts[s - 1] + w, Vector2.up);
                    mb.Tri(mb.B, a, b, c); mb.Tri(mb.B, a, c, d);
                    int a2 = mb.Add(pts[s - 1] - w, Vector2.zero), b2 = mb.Add(pts[s] - w, Vector2.right), c2 = mb.Add(pts[s] + w, Vector2.one), d2 = mb.Add(pts[s - 1] + w, Vector2.up);
                    mb.Tri(mb.B, a2, c2, b2); mb.Tri(mb.B, a2, d2, c2);
                }
            }
        }
        return mb.Build("Palm " + seed);
    }
}
