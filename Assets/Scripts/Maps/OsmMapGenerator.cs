using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoupledSim.Maps;
using UnityEngine;
using UnityEngine.Rendering;
using V2 = System.Numerics.Vector2;

/// <summary>
/// Builds a city from OpenStreetMap data at runtime: roads (widths from OSM, centre and edge
/// markings), red cycle paths, raised pavements with kerbs, zebra crossings with
/// PedestrianCrossing triggers, buildings extruded to their OSM height with brick/plaster façades,
/// green areas, water, mapped trees and street lamps; with colliders. Deterministic: every agent
/// builds the same map from the same data.
/// </summary>
public class OsmMapGenerator
{
    public sealed class Road
    {
        public List<Vector3> Line;
        public float Width;
        public string Type;
        public bool OneWay;
    }

    public readonly List<Road> Roads = new List<Road>();
    public readonly List<List<Vector3>> CyclePaths = new List<List<Vector3>>();
    public readonly List<List<Vector3>> Pavements = new List<List<Vector3>>();
    public readonly List<PedestrianCrossing> Crossings = new List<PedestrianCrossing>();
    public float HalfSize;
    public int BuildingCount;

    readonly MapInfo _map;
    readonly OsmData _data;
    readonly MapAssets _assets;
    readonly Dictionary<long, V2> _pos = new Dictionary<long, V2>();
    readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    public const float PavementHeight = 0.14f;

    public OsmMapGenerator(MapInfo map, OsmData data, MapAssets assets)
    {
        _map = map;
        _data = data;
        _assets = assets;
        var proj = new GeoProjection(map.Latitude, map.Longitude);
        foreach (var n in data.Nodes.Values)
        {
            proj.ToLocal(n.Lat, n.Lon, out var x, out var z);
            _pos[n.Id] = new V2((float)x, (float)z);
        }
        proj.ToLocal(data.MinLat, data.MinLon, out var x0, out var z0);
        proj.ToLocal(data.MaxLat, data.MaxLon, out var x1, out var z1);
        HalfSize = (float)Math.Max(Math.Max(Math.Abs(x0), Math.Abs(x1)), Math.Max(Math.Abs(z0), Math.Abs(z1)));
    }

    // ------------------------------------------------------------------ materials and textures

    Material Mat(string name, Color color, float smooth, Texture2D tex = null)
    {
        if (_mats.TryGetValue(name, out var m)) return m;
        m = _assets != null && _assets.LitTemplate != null ? new Material(_assets.LitTemplate) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = name;
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.enableInstancing = true;
        _mats[name] = m;
        return m;
    }

    static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();

    static Texture2D Tex(string name, int size, Func<int, int, System.Random, Color> pixel)
    {
        if (_textures.TryGetValue(name, out var t) && t != null) return t;
        var rng = new System.Random(name.GetHashCode());
        t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = pixel(x, y, rng);
        t.SetPixels(px);
        t.Apply(true, true);
        _textures[name] = t;
        return t;
    }

    static Color Noise(Color c, System.Random r, float amount) => c * (1f + ((float)r.NextDouble() - 0.5f) * amount);

    /// <summary>One façade tile: one window bay (3 m) by one floor (3 m).</summary>
    static Texture2D Facade(string name, Color wall, Color frame, bool brick) => Tex(name, 256, (x, y, r) =>
    {
        float u = x / 256f, v = y / 256f;
        if (u > 0.3f && u < 0.7f && v > 0.3f && v < 0.85f)
        {
            if (Mathf.Abs(u - 0.5f) < 0.012f || Mathf.Abs(v - 0.62f) < 0.012f) return frame;
            float sky = 0.18f + 0.25f * v;
            return Noise(new Color(sky * 0.8f, sky * 0.9f, sky), r, 0.05f);
        }
        if (u > 0.27f && u < 0.73f && v > 0.27f && v < 0.88f) return frame;
        if (u > 0.25f && u < 0.75f && v > 0.24f && v < 0.27f) return new Color(0.6f, 0.6f, 0.58f);
        if (brick)
        {
            int course = y / 6, offset = course % 2 == 0 ? 0 : 12;
            if (y % 6 == 0 || (x + offset) % 24 == 0) return Noise(new Color(0.72f, 0.7f, 0.66f), r, 0.1f);
            return Noise(wall, r, 0.18f) * (0.9f + 0.2f * ((course * 7 + (x + offset) / 24 * 13) % 10) / 10f);
        }
        return Noise(wall, r, 0.05f);
    });

    // ------------------------------------------------------------------ meshes

    sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<int> T = new List<int>();

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        public void Strip(List<V2> left, List<V2> right, float y, float uvScale = 1f)
        {
            float along = 0;
            for (int i = 0; i < left.Count - 1; i++)
            {
                float seg = V2.Distance((left[i] + right[i]) / 2, (left[i + 1] + right[i + 1]) / 2);
                Quad(P(left[i], y), P(left[i + 1], y), P(right[i + 1], y), P(right[i], y),
                    new Vector2(0, along / uvScale), new Vector2(0, (along + seg) / uvScale),
                    new Vector2(1, (along + seg) / uvScale), new Vector2(1, along / uvScale));
                along += seg;
            }
        }

        public void Wall(List<V2> edge, float y0, float y1, bool flip)
        {
            for (int i = 0; i < edge.Count - 1; i++)
            {
                var a = edge[flip ? i + 1 : i];
                var b = edge[flip ? i : i + 1];
                Quad(P(a, y0), P(a, y1), P(b, y1), P(b, y0), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            }
        }

        public void Polygon(List<V2> poly, float y, float uvScale)
        {
            var tris = Geometry2D.Triangulate(poly);
            int b = V.Count;
            foreach (var p in poly) { V.Add(P(p, y)); UV.Add(new Vector2(p.X / uvScale, p.Y / uvScale)); }
            for (int i = 0; i < tris.Count; i += 3) { T.Add(b + tris[i]); T.Add(b + tris[i + 2]); T.Add(b + tris[i + 1]); }
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(V);
            m.SetUVs(0, UV);
            m.SetTriangles(T, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }

    static Vector3 P(V2 p, float y) => new Vector3(p.X, y, p.Y);

    static GameObject Emit(Transform parent, string name, MeshBuilder mb, Material mat, bool collider)
    {
        if (mb.V.Count == 0) return null;
        var mesh = mb.Build(name);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.isStatic = true;
        return go;
    }

    // ------------------------------------------------------------------ OSM classification

    static readonly HashSet<string> CarRoads = new HashSet<string>
    {
        "motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link", "secondary", "secondary_link",
        "tertiary", "tertiary_link", "unclassified", "residential", "living_street", "service", "road",
    };

    static float ParseMetres(string s, float fallback)
    {
        if (string.IsNullOrEmpty(s)) return fallback;
        return float.TryParse(s.Replace("m", "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    static float RoadWidth(OsmWay w)
    {
        float width = ParseMetres(w.Tag("width"), 0);
        if (width > 2) return width;
        int lanes = int.TryParse(w.Tag("lanes"), out var l) ? l : 0;
        var hw = w.Tag("highway");
        float laneWidth = hw.StartsWith("motorway") || hw.StartsWith("trunk") ? 3.5f : 3.1f;
        if (lanes > 0) return lanes * laneWidth;
        switch (hw)
        {
            case "motorway": case "trunk": return 14f;
            case "primary": return 7.5f;
            case "secondary": return 7f;
            case "tertiary": return 6.5f;
            case "residential": case "unclassified": return 5.5f;
            case "living_street": return 4.5f;
            case "service": return w.Tag("service") == "parking_aisle" ? 5f : 3.5f;
            default: return 5f;
        }
    }

    static bool IsOneWay(OsmWay w) => w.Tag("oneway") == "yes" || (w.Tag("highway") ?? "").StartsWith("motorway") || w.Tag("junction") == "roundabout";
    static bool IsCycleway(OsmWay w) => w.Tag("highway") == "cycleway" || (w.Tag("highway") == "path" && w.Tag("bicycle") == "designated");
    static bool IsFootway(OsmWay w)
    {
        var hw = w.Tag("highway");
        return hw == "footway" || hw == "pedestrian" || hw == "path" || hw == "corridor";
    }
    static int Layer(OsmWay w) => int.TryParse(w.Tag("layer"), out var l) ? l : 0;

    List<V2> Line(OsmWay w) => w.NodeIds.Where(_pos.ContainsKey).Select(id => _pos[id]).ToList();
    bool Inside(V2 p) => Math.Abs(p.X) < HalfSize + 30 && Math.Abs(p.Y) < HalfSize + 30;

    static float BuildingHeight(OsmWay w)
    {
        float h = ParseMetres(w.Tag("height"), 0);
        if (h > 2) return h;
        if (float.TryParse(w.Tag("building:levels"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lv) && lv > 0) return lv * 3f + 1f;
        var b = w.Tag("building");
        if (b == "garage" || b == "garages" || b == "shed" || b == "kiosk" || b == "roof" || b == "carport") return 3f;
        if (b == "church" || b == "cathedral") return 25f;
        return 11.5f; // 3.5 floors
    }

    // ------------------------------------------------------------------ build

    public void Build(Transform root)
    {
        var world = new GameObject("World").transform;
        world.SetParent(root, false);

        var asphalt = Mat("Asphalt", Color.white, 0.2f, Tex("asphalt", 256, (x, y, r) => Noise(new Color(0.23f, 0.23f, 0.24f), r, 0.35f)));
        var red = Mat("Cycle path", Color.white, 0.2f, Tex("red_asphalt", 256, (x, y, r) => Noise(new Color(0.55f, 0.22f, 0.19f), r, 0.3f)));
        var pavingTex = Tex("paving", 256, (x, y, r) => (x % 64 < 2 || y % 64 < 2) ? new Color(0.45f, 0.44f, 0.42f) : Noise(new Color(0.68f, 0.66f, 0.62f), r, 0.12f));
        var paving = Mat("Pavement", Color.white, 0.15f, pavingTex);
        var kerb = Mat("Kerb", new Color(0.62f, 0.61f, 0.58f), 0.2f);
        var marking = Mat("Road marking", new Color(0.92f, 0.92f, 0.9f), 0.35f);
        var grass = Mat("Grass", Color.white, 0.05f, Tex("grass", 256, (x, y, r) => Noise(new Color(0.28f, 0.42f, 0.2f), r, 0.4f)));
        var water = Mat("Water", new Color(0.12f, 0.2f, 0.22f), 0.95f);
        var roof = Mat("Roof", new Color(0.27f, 0.27f, 0.28f), 0.2f);
        var facades = new[]
        {
            Mat("Facade red brick", Color.white, 0.1f, Facade("facade_red", new Color(0.52f, 0.23f, 0.17f), new Color(0.93f, 0.93f, 0.9f), true)),
            Mat("Facade dark brick", Color.white, 0.1f, Facade("facade_dark", new Color(0.33f, 0.18f, 0.14f), new Color(0.9f, 0.9f, 0.88f), true)),
            Mat("Facade yellow brick", Color.white, 0.1f, Facade("facade_yellow", new Color(0.7f, 0.58f, 0.38f), new Color(0.15f, 0.25f, 0.2f), true)),
            Mat("Facade plaster", Color.white, 0.1f, Facade("facade_plaster", new Color(0.86f, 0.84f, 0.79f), new Color(0.2f, 0.22f, 0.25f), false)),
            Mat("Facade concrete", Color.white, 0.1f, Facade("facade_concrete", new Color(0.6f, 0.6f, 0.58f), new Color(0.25f, 0.27f, 0.3f), false)),
        };
        // brick-heavy palettes for the Netherlands, Belgium and the UK; more plaster/concrete elsewhere
        bool brickCountry = _map.Country == "Netherlands" || _map.Country == "Belgium" || _map.Country == "United Kingdom";
        int[] palette = brickCountry ? new[] { 0, 0, 1, 1, 2, 3 } : new[] { 3, 3, 4, 4, 2, 0 };

        float h = HalfSize + 40;
        var ground = new MeshBuilder();
        ground.Quad(new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h),
            new Vector2(0, 0), new Vector2(0, h / 2), new Vector2(h / 2, h / 2), new Vector2(h / 2, 0));
        Emit(world, "Ground", ground, Mat("Ground", Color.white, 0.12f, pavingTex), true);

        var green = new MeshBuilder();
        var waterMb = new MeshBuilder();
        foreach (var w in _data.Ways.Where(w => w.IsClosed))
        {
            var lu = w.Tag("landuse"); var le = w.Tag("leisure"); var nat = w.Tag("natural");
            bool isGreen = lu == "grass" || lu == "meadow" || lu == "village_green" || lu == "recreation_ground" || le == "park" || le == "garden" || nat == "grassland" || nat == "scrub" || nat == "wood";
            bool isWater = nat == "water" || lu == "basin" || w.Tag("waterway") == "riverbank";
            if (!isGreen && !isWater) continue;
            var poly = Line(w);
            if (poly.Count < 4 || !poly.Any(Inside)) continue;
            poly.RemoveAt(poly.Count - 1);
            if (isGreen) green.Polygon(poly, 0.03f, 4f); else waterMb.Polygon(poly, -0.4f, 10f);
        }
        Emit(world, "Green areas", green, grass, true);
        Emit(world, "Water", waterMb, water, false);

        var roads = new MeshBuilder();
        var cycles = new MeshBuilder();
        var walks = new MeshBuilder();
        var kerbs = new MeshBuilder();
        var marks = new MeshBuilder();
        var lamps = new List<(Vector3 p, float yaw)>();
        var left = new List<V2>(); var right = new List<V2>();
        var roadWays = new List<(OsmWay way, List<V2> line, float width)>();
        // junctions: nodes shared by car roads, with the widest road meeting there
        var junctionWidth = new Dictionary<long, float>();
        var nodeUse = new Dictionary<long, int>();
        foreach (var w in _data.Ways)
        {
            if (!CarRoads.Contains(w.Tag("highway") ?? "") || w.Tag("area") == "yes") continue;
            float wd = RoadWidth(w);
            foreach (var id in w.NodeIds.Distinct())
            {
                nodeUse[id] = nodeUse.TryGetValue(id, out var c) ? c + 1 : 1;
                junctionWidth[id] = Math.Max(junctionWidth.TryGetValue(id, out var jw) ? jw : 0, wd);
            }
        }
        foreach (var w in _data.Ways)
        {
            var hw = w.Tag("highway");
            if (hw == null || w.Tag("area") == "yes" || Layer(w) < 0 || w.Tag("tunnel") == "yes") continue;
            var line = Line(w);
            if (line.Count < 2 || !line.Any(Inside)) continue;
            float lift = Math.Max(0, Layer(w)) * 0.02f;
            if (CarRoads.Contains(hw))
            {
                float width = RoadWidth(w);
                roadWays.Add((w, line, width));
                Roads.Add(new Road { Line = line.Select(p => P(p, 0.02f + lift)).ToList(), Width = width, Type = hw, OneWay = IsOneWay(w) });
                Geometry2D.Strip(line, width, 0, left, right);
                roads.Strip(left, right, 0.02f + lift, 4f);
                // markings, pavements, kerbs and lamps stop short of junctions (otherwise a
                // pavement would cross the other road)
                var cuts = new List<(float, float)>();
                float along = 0;
                var lineNodes = w.NodeIds.Where(_pos.ContainsKey).ToList();
                for (int k = 0; k < lineNodes.Count; k++)
                {
                    if (k > 0) along += V2.Distance(_pos[lineNodes[k - 1]], _pos[lineNodes[k]]);
                    var id = lineNodes[k];
                    if (nodeUse.TryGetValue(id, out var uses) && uses >= 2)
                        cuts.Add((along, junctionWidth[id] / 2 + 2.5f));
                }
                float total = Geometry2D.Length(line);
                foreach (var (from, to) in Geometry2D.Remaining(total, cuts))
                {
                    var piece = Geometry2D.SubLine(line, from, to);
                    if (piece.Count < 2) continue;
                    if (width >= 5.5f && hw != "service" && hw != "living_street")
                    {
                        var l2 = new List<V2>(); var r2 = new List<V2>();
                        Geometry2D.Strip(piece, 0.12f, -width / 2 + 0.25f, l2, r2); marks.Strip(l2, r2, 0.035f + lift);
                        Geometry2D.Strip(piece, 0.12f, width / 2 - 0.25f, l2, r2); marks.Strip(l2, r2, 0.035f + lift);
                        if (!IsOneWay(w)) Dashes(piece, marks, 0.12f, 3f, 6f, 0.035f + lift);
                    }
                    var sw = w.Tag("sidewalk") ?? w.Tag("sidewalk:both") ?? (hw == "residential" || hw == "tertiary" || hw == "secondary" || hw == "primary" || hw == "unclassified" ? "both" : "no");
                    if (sw == "no" || sw == "none" || sw == "separate") continue;
                    foreach (var side in new[] { -1, 1 })
                    {
                        if ((sw == "left" && side > 0) || (sw == "right" && side < 0)) continue;
                        Geometry2D.Strip(piece, 2.0f, side * (width / 2 + 1.0f), left, right);
                        walks.Strip(left, right, PavementHeight, 2f);
                        kerbs.Wall(side < 0 ? right : left, 0.02f, PavementHeight, side < 0);
                        // kerb at both ends of the piece too
                        kerbs.Wall(new List<V2> { left[0], right[0] }, 0.02f, PavementHeight, side > 0);
                        kerbs.Wall(new List<V2> { left[left.Count - 1], right[right.Count - 1] }, 0.02f, PavementHeight, side < 0);
                        var centre = new List<V2>(); var dummy = new List<V2>();
                        Geometry2D.Strip(piece, 0f, side * (width / 2 + 1.0f), centre, dummy);
                        Pavements.Add(centre.Select(p => P(p, PavementHeight)).ToList());
                    }
                    float len = Geometry2D.Length(piece);
                    for (float d = 6f; d < len - 4f; d += 25f)
                    {
                        var (pt, dir) = Geometry2D.PointAt(piece, d);
                        var n = new V2(dir.Y, -dir.X);
                        var lp = pt + n * (width / 2 + 0.4f);
                        if (Inside(lp)) lamps.Add((P(lp, PavementHeight), Mathf.Atan2(-n.X, -n.Y) * Mathf.Rad2Deg));
                    }
                }
            }
            else if (IsCycleway(w))
            {
                Geometry2D.Strip(line, ParseMetres(w.Tag("width"), 2.2f), 0, left, right);
                cycles.Strip(left, right, 0.04f + lift, 4f);
                CyclePaths.Add(line.Select(p => P(p, 0.04f + lift)).ToList());
            }
            else if (IsFootway(w))
            {
                Geometry2D.Strip(line, hw == "pedestrian" ? 4f : 1.8f, 0, left, right);
                walks.Strip(left, right, 0.05f + lift, 2f);
                Pavements.Add(line.Select(p => P(p, 0.05f + lift)).ToList());
            }
        }
        Emit(world, "Roads", roads, asphalt, true);
        Emit(world, "Cycle paths", cycles, red, true);
        Emit(world, "Pavements", walks, paving, true);
        Emit(world, "Kerbs", kerbs, kerb, true);
        Emit(world, "Road markings", marks, marking, false);

        // zebra crossings at OSM crossing nodes on car roads
        var zebras = new MeshBuilder();
        var crossingRoot = new GameObject("Pedestrian crossings").transform;
        crossingRoot.SetParent(root, false);
        foreach (var (w, line, width) in roadWays)
        {
            for (int i = 0; i < w.NodeIds.Count; i++)
            {
                if (!_data.Nodes.TryGetValue(w.NodeIds[i], out var node) || node.Tag("highway") != "crossing" || !_pos.ContainsKey(node.Id)) continue;
                var c = node.Tag("crossing"); var cr = node.Tag("crossing_ref") ?? node.Tag("crossing:markings");
                bool marked = cr == "zebra" || cr == "yes" || c == "zebra" || c == "marked" || c == "uncontrolled" || c == "traffic_signals";
                var p = _pos[node.Id];
                if (!Inside(p)) continue;
                var a = _pos[w.NodeIds[Math.Max(0, i - 1)]]; var b = _pos[w.NodeIds[Math.Min(w.NodeIds.Count - 1, i + 1)]];
                if (V2.DistanceSquared(a, b) < 1e-4f) continue;
                var dir = V2.Normalize(b - a);
                var nrm = new V2(dir.Y, -dir.X);
                if (marked)
                {
                    for (float o = -width / 2 + 0.5f; o < width / 2 - 0.3f; o += 1.0f)
                    {
                        var c0 = p + nrm * o - dir * 2f; var c1 = p + nrm * o + dir * 2f;
                        var c2 = p + nrm * (o + 0.5f) + dir * 2f; var c3 = p + nrm * (o + 0.5f) - dir * 2f;
                        zebras.Quad(P(c0, 0.04f), P(c1, 0.04f), P(c2, 0.04f), P(c3, 0.04f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                    }
                }
                // a crossing in each direction, for AI pedestrians with behaviour models
                foreach (var side in new[] { -1, 1 })
                {
                    var near = P(p + nrm * side * (width / 2 + 0.6f), PavementHeight);
                    var far = P(p - nrm * side * (width / 2 + 0.6f), PavementHeight);
                    var go = new GameObject($"Crossing {node.Id} {(side < 0 ? "A" : "B")}");
                    go.transform.SetParent(crossingRoot, false);
                    go.transform.SetPositionAndRotation(near, Quaternion.LookRotation(far - near, Vector3.up));
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(4f, 2f, 1f);
                    box.center = new Vector3(0, 1f, -0.5f);
                    var pc = go.AddComponent<PedestrianCrossing>();
                    var fk = new GameObject("FarKerb").transform;
                    fk.SetParent(go.transform, false);
                    fk.position = far;
                    pc.FarKerb = fk;
                    pc.Zebra = marked;
                    Crossings.Add(pc);
                }
            }
        }
        Emit(world, "Zebra crossings", zebras, marking, false);

        // buildings
        var buildingMbs = facades.Select(_ => new MeshBuilder()).ToArray();
        var roofs = new MeshBuilder();
        foreach (var w in _data.Ways.Where(w => w.IsClosed && w.Tag("building") != null))
        {
            var poly = Line(w);
            if (poly.Count < 4 || !poly.Any(Inside)) continue;
            poly.RemoveAt(poly.Count - 1);
            float height = BuildingHeight(w);
            float minHeight = ParseMetres(w.Tag("min_height"), 0f);
            if (Geometry2D.SignedArea(poly) < 0) poly.Reverse();
            var mb = buildingMbs[palette[(int)(Math.Abs(w.Id) % palette.Length)]];
            float u = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                float len = V2.Distance(a, b);
                mb.Quad(P(b, minHeight), P(b, height), P(a, height), P(a, minHeight),
                    new Vector2((u + len) / 3f, minHeight / 3f), new Vector2((u + len) / 3f, height / 3f),
                    new Vector2(u / 3f, height / 3f), new Vector2(u / 3f, minHeight / 3f));
                u += len;
            }
            roofs.Polygon(poly, height, 5f);
            BuildingCount++;
        }
        var buildings = new GameObject("Buildings").transform;
        buildings.SetParent(world, false);
        for (int i = 0; i < facades.Length; i++) Emit(buildings, "Buildings " + facades[i].name, buildingMbs[i], facades[i], true);
        Emit(buildings, "Roofs", roofs, roof, false);

        // street lamps (EnvironmentController lights them at dusk) and mapped trees
        var props = new GameObject("Street furniture").transform;
        props.SetParent(root, false);
        if (_assets != null && _assets.StreetLamp != null)
            foreach (var (p, yaw) in lamps)
                UnityEngine.Object.Instantiate(_assets.StreetLamp, p, Quaternion.Euler(0, yaw, 0), props).name = "StreetLamp";
        if (_assets != null && _assets.Trees != null && _assets.Trees.Length > 0)
        {
            var rng = new System.Random(_map.Name.GetHashCode());
            foreach (var n in _data.Nodes.Values.Where(n => n.Tag("natural") == "tree"))
            {
                if (!_pos.TryGetValue(n.Id, out var p) || !Inside(p)) continue;
                UnityEngine.Object.Instantiate(_assets.Trees[rng.Next(_assets.Trees.Length)], P(p, 0.1f), Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), props);
            }
        }
    }

    static void Dashes(List<V2> line, MeshBuilder mb, float width, float dash, float gap, float y)
    {
        float len = Geometry2D.Length(line);
        for (float d = 0; d < len - dash; d += dash + gap)
        {
            var (a, dir) = Geometry2D.PointAt(line, d);
            var (b, _) = Geometry2D.PointAt(line, d + dash);
            var n = new V2(dir.Y, -dir.X) * (width / 2);
            mb.Quad(P(a - n, y), P(b - n, y), P(b + n, y), P(a + n, y), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
        }
    }
}
