using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using CoupledSim.Maps;
using UnityEngine;

/// <summary>
/// The terrain of a city map (StreamingAssets/Maps/{map}.height.bin.gz from Tools/terrain_height.py: the Dutch AHN, or
/// open terrain tiles elsewhere): heights in metres relative to the ground at the map's centre. After the map is built
/// flat, <see cref="Apply"/> lifts it onto the terrain: the generated surfaces vertex by vertex (walls stay vertical,
/// as their top and bottom share a position), every placed object (trees, lamps, signs, props, parked cars) by the
/// height at its foot, the paths of traffic and pedestrians, and bridges straight between their ends instead of
/// down into the valley. <c>-flatTerrain</c> keeps maps flat.
/// </summary>
public sealed class TerrainHeight
{
    float _x0, _z0, _step;
    int _nx, _nz;
    float[] _h;
    public string Credit;
    public float Min, Max;

    public static bool Disabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-flatTerrain") >= 0;

    public static TerrainHeight Load(string map, GeoProjection proj)
    {
        if (Disabled || string.IsNullOrEmpty(map)) return null;
        var path = Path.Combine(Application.streamingAssetsPath, "Maps", map + ".height.bin.gz");
        if (!File.Exists(path)) return null;
        var t = new TerrainHeight();
        using (var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
        using (var r = new BinaryReader(new BufferedStream(gz)))
        {
            if (new string(r.ReadChars(4)) != "HGT1") { Debug.LogWarning("TerrainHeight: unknown file " + path); return null; }
            double lat0 = r.ReadDouble(), lon0 = r.ReadDouble();
            t._x0 = r.ReadSingle(); t._z0 = r.ReadSingle(); t._step = r.ReadSingle();
            t._nx = r.ReadInt32(); t._nz = r.ReadInt32();
            t.Credit = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
            t._h = new float[t._nx * t._nz];
            t.Min = float.MaxValue; t.Max = float.MinValue;
            for (int i = 0; i < t._h.Length; i++) { var v = r.ReadSingle(); t._h[i] = v; t.Min = Mathf.Min(t.Min, v); t.Max = Mathf.Max(t.Max, v); }
            // made for another centre: shift the grid (the maps' centres rarely change; a shift is enough over a map)
            if (Math.Abs(lat0 - proj.Lat0) > 1e-9 || Math.Abs(lon0 - proj.Lon0) > 1e-9)
            {
                proj.ToLocal(lat0, lon0, out var dx, out var dz);
                t._x0 += (float)dx; t._z0 += (float)dz;
            }
        }
        return t;
    }

    /// <summary>Height (m) at a scene position; the grid's edge beyond it.</summary>
    public float At(float x, float z)
    {
        float fx = Mathf.Clamp((x - _x0) / _step, 0f, _nx - 1.001f), fz = Mathf.Clamp((z - _z0) / _step, 0f, _nz - 1.001f);
        int i = (int)fx, j = (int)fz;
        float tx = fx - i, tz = fz - j;
        float a = _h[j * _nx + i], b = _h[j * _nx + i + 1], c = _h[(j + 1) * _nx + i], d = _h[(j + 1) * _nx + i + 1];
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    // ---- bridges: their decks run straight from one end's ground to the other's

    struct Bridge { public Vector2 A, B; public float HalfWidth, HA, HB; public int Layer; }
    readonly List<Bridge> _bridges = new List<Bridge>();

    /// <summary>A bridge from its line; <paramref name="layer"/> 1 and up: above other roads or the ground: it lifts only the geometry
    /// of its own layer (the generator's "... bridge L{layer}" meshes and that layer's lines), not what passes under it (nor
    /// other decks stacked over or under it). Layer 0: a bridge over water or a small valley, lifting everything on it.</summary>
    public void AddBridge(List<Vector2> line, float width, int layer = 0)
    {
        if (line.Count < 2) return;
        float ha = At(line[0].x, line[0].y), hb = At(line[line.Count - 1].x, line[line.Count - 1].y);
        float total = 0f; for (int i = 0; i + 1 < line.Count; i++) total += Vector2.Distance(line[i], line[i + 1]);
        float run = 0f;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            float seg = Vector2.Distance(line[i], line[i + 1]);
            _bridges.Add(new Bridge { A = line[i], B = line[i + 1], HalfWidth = width / 2f + 1.5f,
                HA = Mathf.Lerp(ha, hb, total > 0 ? run / total : 0), HB = Mathf.Lerp(ha, hb, total > 0 ? (run + seg) / total : 1), Layer = Mathf.Max(0, layer) });
            run += seg;
        }
    }

    /// <summary>The height to lift a point by: the terrain, or a bridge deck's where one passes over it: bridges of layer 0 for
    /// every point, elevated ones only for points of their own <paramref name="layer"/> (their decks).</summary>
    public float Lift(float x, float z) => Lift(x, z, 0);

    public float Lift(float x, float z, int layer)
    {
        var p = new Vector2(x, z);
        float? own = null, any = null;
        foreach (var b in _bridges)
        {
            if (b.Layer != 0 && b.Layer != layer) continue;
            var ab = b.B - b.A; float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - b.A, ab) / l2) : 0f;
            if ((b.A + ab * t - p).sqrMagnitude >= b.HalfWidth * b.HalfWidth) continue;
            float h = Mathf.Lerp(b.HA, b.HB, t);
            if (b.Layer == layer && layer > 0) { own = h; break; }   // the point's own deck first
            if (any == null) any = h;
        }
        if (own.HasValue) return own.Value;
        if (any.HasValue) return any.Value;
        return At(x, z);
    }

    /// <summary>Lifts a map built flat onto the terrain.</summary>
    public void Apply(Transform root, OsmMapGenerator gen)
    {
        int meshes = 0, objects = 0, placed = 0;
        // the surfaces things stand on (not buildings), to set each placed object on the one under it afterwards
        var surfaces = new HashSet<Collider>();
        foreach (var go in gen.EmittedMeshes)
            if (go != null && go.TryGetComponent<Collider>(out var c) && !go.name.StartsWith("Buildings") && !go.name.StartsWith("Roofs") && go.name != "Cornices") surfaces.Add(c);
        float? SurfaceNear(Vector3 p, float expected)
        {
            float? best = null;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, expected + 3f, p.z), Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
                if (surfaces.Contains(h.collider) && (best == null || Mathf.Abs(h.point.y - expected) < Mathf.Abs(best.Value - expected))) best = h.point.y;
            return best;
        }
        var things = new List<(Transform t, float offset, bool onSurface)>();
        void Collect(Transform t)
        {
            if (gen.EmittedMeshes.Contains(t.gameObject)) return;
            if (t != root && t.GetComponents<Component>().Length > 1)
            {
                // how high above the surface under it the flat map placed it (on the pavement, the road, the ground)
                var p = t.position;
                var s0 = SurfaceNear(p, p.y - 0.05f);
                things.Add((t, s0.HasValue ? p.y - s0.Value : 0f, s0.HasValue && p.y - s0.Value < 1.5f));
                return;
            }
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i));
        }
        Physics.SyncTransforms();
        Collect(root);
        void Visit(Transform t)
        {
            if (gen.EmittedMeshes.Contains(t.gameObject))
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var m = mf.sharedMesh;
                    var v = m.vertices;
                    var l2w = t.localToWorldMatrix; var w2l = t.worldToLocalMatrix;
                    // the distant surroundings (a few big triangles): flat, under the lowest ground of the map
                    bool surroundings = t.name.StartsWith("Surroundings");
                    // the ground a little under the streets: its 10 m tiles cut corners on slopes the streets follow more closely
                    float extra = t.name == "Ground" ? -0.15f : 0f;
                    int layer = 0;
                    int bl = t.name.LastIndexOf(" bridge L", StringComparison.Ordinal);
                    if (bl >= 0) int.TryParse(t.name.Substring(bl + 9), out layer);
                    for (int i = 0; i < v.Length; i++)
                    {
                        var w = l2w.MultiplyPoint3x4(v[i]);
                        w.y += surroundings ? Min - 0.5f : Lift(w.x, w.z, layer) + extra;
                        v[i] = w2l.MultiplyPoint3x4(w);
                    }
                    m.vertices = v;
                    m.RecalculateNormals();
                    m.RecalculateBounds();
                    var mc = t.GetComponent<MeshCollider>();
                    if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = m; }
                    meshes++;
                }
                return;
            }
            for (int i = 0; i < t.childCount; i++) Visit(t.GetChild(i));
        }
        Visit(root);
        // placed objects (anything with more than a Transform: trees, lamps, signs, props, parked cars) moved as a whole:
        // onto the lifted surface that was under them (the ground follows the terrain in 10 m tiles, so the terrain's
        // own height at an object's foot can be off it by a fraction of a metre on slopes), else by the terrain there
        Physics.SyncTransforms();
        foreach (var (t, offset, onSurface) in things)
        {
            var p = t.position;
            float lifted = p.y + Lift(p.x, p.z, 0);
            var s1 = onSurface ? SurfaceNear(p, lifted - offset) : null;
            t.position = new Vector3(p.x, s1.HasValue ? s1.Value + offset : lifted, p.z);
            objects++;
            if (s1.HasValue) placed++;
        }
        void LiftLine(List<Vector3> line, int layer) { for (int i = 0; i < line.Count; i++) { var q = line[i]; line[i] = new Vector3(q.x, q.y + Lift(q.x, q.z, layer), q.z); } }
        foreach (var r in gen.Roads) LiftLine(r.Line, r.BridgeLayer);
        foreach (var l in gen.CyclePaths) LiftLine(l, gen.BridgeLines.TryGetValue(l, out var ly) ? ly : 0);
        foreach (var l in gen.Pavements) LiftLine(l, gen.BridgeLines.TryGetValue(l, out var ly) ? ly : 0);
        Debug.Log($"TerrainHeight: map lifted onto the terrain ({Min:F1} .. {Max:F1} m around the centre; {meshes} surfaces, {objects} objects ({placed} set on the surface under them), {_bridges.Count} bridge segments): {Credit}");
    }
}
