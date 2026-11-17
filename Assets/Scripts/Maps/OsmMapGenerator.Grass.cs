using System;
using System.Collections.Generic;
using UnityEngine;
using V2 = System.Numerics.Vector2;

/// <summary>
/// Grass blades on the map's lawns and verges: small tufts of crossed blades (alpha-cut, two-sided, without shadows of
/// their own) scattered over the grass areas' triangles, in chunks of 40 m drawn only within about 90 m of the view, so a
/// lawn up close is grass rather than a green plane. <c>-noGrassBlades</c> leaves them out.
/// </summary>
public partial class OsmMapGenerator
{
    static readonly bool _noGrassBlades = Array.IndexOf(CommandLine.Args, "-noGrassBlades") >= 0;
    /// <summary>Tufts per square metre.</summary>
    public static float GrassBladeDensity = 7f;
    const float GrassChunk = 20f, GrassNearWays = 12f;
    /// <summary>At most this many tufts on a map, within GrassRadius of its centre (where its scenes are): a big map's
    /// lawns would otherwise take millions.</summary>
    public static int GrassBladeBudget = 250000;
    public static float GrassRadius = 500f;

    /// <summary>Scatters tufts over the grass meshes' triangles (their y: the grass's height above the flat map; the
    /// terrain lifts the tufts with everything else).</summary>
    void EmitGrassBlades(Transform parent, IEnumerable<MeshBuilder> grassMeshes, Func<V2, bool> skip)
    {
        if (_noGrassBlades || MapExtras.Disabled) return;
        var chunks = new Dictionary<long, MeshBuilder>();
        // only within 12 m of a road, path or cycle path (verges, tree pits, the edges of lawns: where one sees grass up
        // close); further into a park the lawn's texture does
        var ways = new Dictionary<long, List<(V2 a, V2 b)>>();
        void AddLine(List<Vector3> l)
        {
            if (l == null) return;
            for (int i = 0; i + 1 < l.Count; i++)
            {
                var a = new V2(l[i].x, l[i].z); var b = new V2(l[i + 1].x, l[i + 1].z);
                int x0 = Mathf.FloorToInt((Mathf.Min(a.X, b.X) - GrassNearWays) / 10f), x1 = Mathf.FloorToInt((Mathf.Max(a.X, b.X) + GrassNearWays) / 10f);
                int z0 = Mathf.FloorToInt((Mathf.Min(a.Y, b.Y) - GrassNearWays) / 10f), z1 = Mathf.FloorToInt((Mathf.Max(a.Y, b.Y) + GrassNearWays) / 10f);
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                {
                    long k = ((long)x << 32) ^ (uint)z;
                    if (!ways.TryGetValue(k, out var list)) ways[k] = list = new List<(V2, V2)>();
                    list.Add((a, b));
                }
            }
        }
        foreach (var r in Roads) AddLine(r.Line);
        foreach (var l in Pavements) AddLine(l);
        foreach (var l in CyclePaths) AddLine(l);
        bool NearWay(V2 q)
        {
            if (!ways.TryGetValue(((long)Mathf.FloorToInt(q.X / 10f) << 32) ^ (uint)Mathf.FloorToInt(q.Y / 10f), out var list)) return false;
            foreach (var (a, b) in list)
            {
                var ab = b - a; float l2 = ab.LengthSquared();
                float t = l2 < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, V2.Dot(q - a, ab) / l2));
                if (V2.DistanceSquared(q, a + ab * t) < GrassNearWays * GrassNearWays) return true;
            }
            return false;
        }
        var rng = new System.Random(_map.Name != null ? _map.Name.GetHashCode() : 1);
        bool Near(Vector3 p) => p.x * p.x + p.z * p.z < GrassRadius * GrassRadius;
        // the lawn near ways within the radius (estimated by the triangles' corners and middle), for the density
        float eligible = 0f;
        foreach (var mb in grassMeshes)
        {
            if (mb == null) continue;
            for (int t = 0; t + 2 < mb.T.Count; t += 3)
            {
                Vector3 a = mb.V[mb.T[t]], b = mb.V[mb.T[t + 1]], c = mb.V[mb.T[t + 2]];
                var m = (a + b + c) / 3f;
                if (!Near(m)) continue;
                int near = (NearWay(new V2(a.x, a.z)) ? 1 : 0) + (NearWay(new V2(b.x, b.z)) ? 1 : 0) + (NearWay(new V2(c.x, c.z)) ? 1 : 0) + (NearWay(new V2(m.x, m.z)) ? 1 : 0);
                eligible += Vector3.Cross(b - a, c - a).magnitude / 2f * near / 4f;
            }
        }
        float density = Mathf.Min(GrassBladeDensity, eligible > 1f ? GrassBladeBudget / eligible : GrassBladeDensity);
        foreach (var mb in grassMeshes)
        {
            if (mb == null) continue;
            for (int t = 0; t + 2 < mb.T.Count; t += 3)
            {
                Vector3 a = mb.V[mb.T[t]], b = mb.V[mb.T[t + 1]], c = mb.V[mb.T[t + 2]];
                float area = Vector3.Cross(b - a, c - a).magnitude / 2f;
                if (area < 0.05f || !Near((a + b + c) / 3f)) continue;
                float expected = area * density;
                int n = (int)expected + (rng.NextDouble() < expected - (int)expected ? 1 : 0);
                for (int k = 0; k < n; k++)
                {
                    // a uniform point in the triangle
                    float r1 = (float)rng.NextDouble(), r2 = (float)rng.NextDouble();
                    if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                    var p = a + (b - a) * r1 + (c - a) * r2;
                    var q2 = new V2(p.x, p.z);
                    if (!NearWay(q2) || (skip != null && skip(q2))) continue;
                    long key = ((long)Mathf.FloorToInt(p.x / GrassChunk) << 32) ^ (uint)Mathf.FloorToInt(p.z / GrassChunk);
                    if (!chunks.TryGetValue(key, out var chunk)) chunks[key] = chunk = new MeshBuilder();
                    Tuft(chunk, p, rng);
                }
            }
        }
        if (chunks.Count == 0) return;
        var mat = GrassBladeMaterial();
        var root = new GameObject("Grass blades").transform;
        root.SetParent(parent, false);
        int count = 0;
        foreach (var kv in chunks)
        {
            var go = Emit(root, "Grass blades", kv.Value, mat, false);
            if (go == null) continue;
            // (normals up: the blades lit as the lawn under them, from every side)
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var up = new Vector3[mesh.vertexCount];
            for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.normals = up;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // drawn only near the view: a 20 m chunk fills less than 45% of the screen's height beyond about 40 m
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.45f, new Renderer[] { r }) });
            lod.RecalculateBounds();
            count += kv.Value.V.Count / 8;
        }
        Debug.Log($"OsmMap: {count} tufts of grass blades in {chunks.Count} chunks ({density:F1} per m² within {GrassRadius:F0} m of the centre)");
    }

    // two crossed quads, 0.3-0.5 m wide and 0.08-0.2 m high, each showing a random part of the blades' texture
    static void Tuft(MeshBuilder mb, Vector3 p, System.Random rng)
    {
        float yaw = (float)(rng.NextDouble() * Math.PI);
        float w = 0.2f + 0.12f * (float)rng.NextDouble(), h = 0.07f + 0.11f * (float)rng.NextDouble();
        float u0 = (float)rng.Next(4) / 4f;
        for (int q = 0; q < 2; q++)
        {
            float ang = yaw + q * Mathf.PI / 2f;
            var d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * w;
            var lean = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.06f;
            var lo = p + Vector3.up * 0.005f;
            mb.Quad(lo - d, lo - d + Vector3.up * h + lean, lo + d + Vector3.up * h + lean, lo + d,
                new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u0 + 0.25f, 1f), new Vector2(u0 + 0.25f, 0f));
        }
    }

    Material GrassBladeMaterial()
    {
        if (_mats.TryGetValue("Grass blades", out var m)) return m;
        // blades: thin tapering strokes in the lawn's greens, transparent between them
        var tex = new Texture2D(512, 128, TextureFormat.RGBA32, true) { name = "grass_blades", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        var px = new Color32[512 * 128];
        var rng = new System.Random(11);
        for (int b = 0; b < 420; b++)
        {
            float x0 = (float)rng.NextDouble() * 512f, bend = ((float)rng.NextDouble() - 0.5f) * 30f;
            float height = 60f + (float)rng.NextDouble() * 66f, width = 2.5f + (float)rng.NextDouble() * 2.5f;
            // (each quarter of the texture is one tuft's face: blades stay inside their quarter)
            float q0 = Mathf.Floor(x0 / 128f) * 128f + 6f, q1 = q0 + 116f;
            var g = new Color(0.20f + 0.12f * (float)rng.NextDouble(), 0.34f + 0.16f * (float)rng.NextDouble(), 0.10f + 0.06f * (float)rng.NextDouble());
            for (int y = 0; y < (int)height; y++)
            {
                float t = y / height;
                float cx = Mathf.Clamp(x0 + bend * t * t, q0, q1), hw = width * (1f - t) * 0.5f + 0.4f;
                var col = Color.Lerp(g * 0.55f, g * 1.15f, t);
                for (int x = Mathf.FloorToInt(cx - hw); x <= Mathf.CeilToInt(cx + hw); x++)
                {
                    if (x < 0 || x >= 512) continue;
                    float cover = Mathf.Clamp01(hw + 0.5f - Mathf.Abs(x + 0.5f - cx));
                    if (cover <= 0f) continue;
                    int i = y * 512 + x;
                    px[i] = new Color32((byte)(col.r * 255), (byte)(col.g * 255), (byte)(col.b * 255), (byte)Mathf.Max(px[i].a, (int)(cover * 255)));
                }
            }
        }
        tex.SetPixels32(px); tex.Apply(true);
        // the smaller mip levels average the thin blades' alpha away (they would vanish a few metres off): kept up
        for (int level = 1; level < tex.mipmapCount; level++)
        {
            var mp = tex.GetPixels32(level);
            float k = 1f + 0.9f * level;
            for (int i = 0; i < mp.Length; i++) mp[i].a = (byte)Mathf.Min(255, mp[i].a * k);
            tex.SetPixels32(mp, level);
        }
        tex.Apply(false);
        m = Mat("Grass blades", Color.white, 0.05f, tex);
        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.4f); m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", 0f);   // both sides
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        return m;
    }
}
