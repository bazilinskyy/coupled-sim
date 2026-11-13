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
    readonly GeoProjection _proj;
    /// <summary>True when the buildings are real 3D models (3D BAG) instead of extruded footprints.</summary>
    public bool Real3DBuildings { get; private set; }
    readonly MapAssets _assets;
    Func<int, Material> _facadeMat;
    static readonly int Styles = Enum.GetValues(typeof(WindowStyle)).Length;
    readonly Dictionary<long, V2> _pos = new Dictionary<long, V2>();
    readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    public const float PavementHeight = 0.14f;

    public OsmMapGenerator(MapInfo map, OsmData data, MapAssets assets)
    {
        _map = map;
        _data = data;
        _assets = assets;
        var proj = new GeoProjection(map.Latitude, map.Longitude);
        _proj = proj;
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

    /// <summary>Window layouts of the façades, roughly by building age and type.</summary>
    public enum WindowStyle
    {
        Classic,  // one window with a cross, per bay (the default of earlier versions)
        Tall,     // tall narrow sash windows with small panes and a stone lintel (before ~1920)
        Ribbon,   // horizontal window bands with a concrete sill (post-war blocks)
        Pair,     // two windows per bay (later housing)
        Glass,    // glass curtain wall with mullions and dark spandrels (offices, towers)
    }

    /// <summary>One façade tile: one window bay (3 m) by one floor (3 m).</summary>
    static Texture2D Facade(string name, Color wall, Color frame, bool brick, WindowStyle style = WindowStyle.Classic) => Tex(name, 256, (x, y, r) =>
    {
        float u = x / 256f, v = y / 256f;
        Color Glass(float vv) { float sky = 0.18f + 0.25f * vv; return Noise(new Color(sky * 0.8f, sky * 0.9f, sky), r, 0.05f); }
        const float f = 0.012f; // frame half-width
        switch (style)
        {
            case WindowStyle.Tall:
                if (u > 0.34f && u < 0.66f && v > 0.18f && v < 0.88f)
                {
                    if (Mathf.Abs(u - 0.5f) < f || Mathf.Abs(v - 0.42f) < f || Mathf.Abs(v - 0.65f) < f) return frame;
                    return Glass(v);
                }
                if (u > 0.31f && u < 0.69f && v > 0.15f && v < 0.91f) return frame;
                if (u > 0.29f && u < 0.71f && v > 0.91f && v < 0.96f) return Noise(new Color(0.78f, 0.76f, 0.7f), r, 0.06f);   // lintel
                if (u > 0.3f && u < 0.7f && v > 0.12f && v < 0.15f) return new Color(0.62f, 0.61f, 0.58f);                       // sill
                break;
            case WindowStyle.Ribbon:
                if (v > 0.4f && v < 0.8f)
                {
                    if (u < f * 2 || u > 1f - f * 2 || Mathf.Abs(u - 0.5f) < f || Mathf.Abs(v - 0.7f) < f) return frame;
                    return Glass(v);
                }
                if (v > 0.37f && v < 0.83f) return frame;
                if (v > 0.32f && v < 0.37f) return Noise(new Color(0.66f, 0.66f, 0.63f), r, 0.05f);
                break;
            case WindowStyle.Pair:
                foreach (var c in new[] { 0.28f, 0.72f })
                {
                    if (Mathf.Abs(u - c) < 0.15f && v > 0.32f && v < 0.84f) return Mathf.Abs(v - 0.66f) < f ? frame : Glass(v);
                    if (Mathf.Abs(u - c) < 0.17f && v > 0.3f && v < 0.86f) return frame;
                }
                if (v > 0.26f && v < 0.3f && (Mathf.Abs(u - 0.28f) < 0.18f || Mathf.Abs(u - 0.72f) < 0.18f)) return new Color(0.6f, 0.6f, 0.58f);
                break;
            case WindowStyle.Glass:
                if (u < 0.02f || u > 0.98f || Mathf.Abs(u - 0.5f) < 0.008f) return frame;     // mullions
                if (v < 0.2f) return v < 0.02f || v > 0.18f ? frame : Noise(Color.Lerp(wall, new Color(0.15f, 0.17f, 0.2f), 0.7f), r, 0.04f); // spandrel
                { var g = Glass(v); return new Color(g.r * 0.85f, g.g, g.b * 1.08f); }
            default:
                if (u > 0.3f && u < 0.7f && v > 0.3f && v < 0.85f)
                {
                    if (Mathf.Abs(u - 0.5f) < f || Mathf.Abs(v - 0.62f) < f) return frame;
                    return Glass(v);
                }
                if (u > 0.27f && u < 0.73f && v > 0.27f && v < 0.88f) return frame;
                if (u > 0.25f && u < 0.75f && v > 0.24f && v < 0.27f) return new Color(0.6f, 0.6f, 0.58f);
                break;
        }
        if (brick)
        {
            int course = y / 6, offset = course % 2 == 0 ? 0 : 12;
            if (y % 6 == 0 || (x + offset) % 24 == 0) return Noise(new Color(0.72f, 0.7f, 0.66f), r, 0.1f);
            return Noise(wall, r, 0.18f) * (0.9f + 0.2f * ((course * 7 + (x + offset) / 24 * 13) % 10) / 10f);
        }
        return Noise(wall, r, 0.05f);
    });

    /// <summary>The window style of a building: from its construction year (3D BAG), height and OSM type.</summary>
    public static WindowStyle StyleFor(int year, float height, string type, uint seed)
    {
        bool office = type == "office" || type == "commercial" || type == "retail" || type == "hotel" || type == "university";
        // old buildings and churches keep traditional windows, however tall (church towers)
        if (type == "church" || type == "cathedral" || type == "chapel") return WindowStyle.Tall;
        if (year > 0 && year < 1920) return seed % 4 == 0 ? WindowStyle.Classic : WindowStyle.Tall;
        if (year > 0 && year < 1945) return seed % 3 == 0 ? WindowStyle.Tall : WindowStyle.Classic;
        if (height > 30f) return seed % 5 < 3 ? WindowStyle.Glass : WindowStyle.Ribbon;
        if (year > 0)
        {
            if (year < 1975) return seed % 3 == 0 ? WindowStyle.Pair : WindowStyle.Ribbon;
            if (year < 2000) return office ? WindowStyle.Ribbon : seed % 2 == 0 ? WindowStyle.Pair : WindowStyle.Classic;
            return office || seed % 3 == 0 ? WindowStyle.Glass : WindowStyle.Pair;
        }
        if (office) return seed % 2 == 0 ? WindowStyle.Glass : WindowStyle.Ribbon;
        if (type == "house" || type == "terrace" || type == "detached" || type == "semidetached_house") return seed % 2 == 0 ? WindowStyle.Tall : WindowStyle.Classic;
        if (type == "apartments") return seed % 2 == 0 ? WindowStyle.Ribbon : WindowStyle.Pair;
        return (WindowStyle)(seed % 4);   // Classic, Tall, Ribbon or Pair
    }

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

        /// <summary>A box with its bottom centre at <paramref name="p"/>, turned by <paramref name="yaw"/> degrees.</summary>
        public void Box(Vector3 p, Vector3 size, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 hx = q * new Vector3(size.x / 2, 0, 0), hz = q * new Vector3(0, 0, size.z / 2), h = new Vector3(0, size.y, 0);
            Vector3 a = p - hx - hz, b = p + hx - hz, c = p + hx + hz, d = p - hx + hz;
            Vector2 u0 = Vector2.zero, u1 = new Vector2(0, size.y), u2 = new Vector2(1, size.y), u3 = Vector2.right;
            Quad(a, a + h, b + h, b, u0, u1, u2, u3);
            Quad(b, b + h, c + h, c, u0, u1, u2, u3);
            Quad(c, c + h, d + h, d, u0, u1, u2, u3);
            Quad(d, d + h, a + h, a, u0, u1, u2, u3);
            Quad(a + h, d + h, c + h, b + h, u0, u1, u2, u3);
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
        // façades: wall kinds (red, dark and yellow brick, plaster, concrete) × window styles, made when first used
        var wallKinds = new (string name, Color wall, Color frame, bool brick)[]
        {
            ("red brick", new Color(0.52f, 0.23f, 0.17f), new Color(0.93f, 0.93f, 0.9f), true),
            ("dark brick", new Color(0.33f, 0.18f, 0.14f), new Color(0.9f, 0.9f, 0.88f), true),
            ("yellow brick", new Color(0.7f, 0.58f, 0.38f), new Color(0.15f, 0.25f, 0.2f), true),
            ("plaster", new Color(0.86f, 0.84f, 0.79f), new Color(0.2f, 0.22f, 0.25f), false),
            ("concrete", new Color(0.6f, 0.6f, 0.58f), new Color(0.25f, 0.27f, 0.3f), false),
        };
        int styles = Styles;
        var facades = new Material[wallKinds.Length * styles];
        _facadeMat = k =>
        {
            if (facades[k] != null) return facades[k];
            var (name, wall, frame, brick) = wallKinds[k / styles];
            var style = (WindowStyle)(k % styles);
            return facades[k] = Mat($"Facade {name} {style}", Color.white, style == WindowStyle.Glass ? 0.55f : 0.1f,
                Facade($"facade_{name.Replace(' ', '_')}_{style}", wall, frame, brick, style));
        };
        // brick-heavy palettes for the Netherlands, Belgium and the UK; more plaster/concrete elsewhere
        bool brickCountry = _map.Country == "Netherlands" || _map.Country == "Belgium" || _map.Country == "United Kingdom";
        int[] palette = brickCountry ? new[] { 0, 0, 1, 1, 2, 3 } : new[] { 3, 3, 4, 4, 2, 0 };

        float h = HalfSize + 40;
        var ground = new MeshBuilder();
        // in tiles of at most 100 m: the physics engine warns about (and handles badly) triangles over 500 m
        int tiles = Mathf.CeilToInt(2 * h / 100f);
        float step = 2 * h / tiles;
        for (int i = 0; i < tiles; i++)
            for (int j = 0; j < tiles; j++)
            {
                float x0 = -h + i * step, x1 = x0 + step, z0 = -h + j * step, z1 = z0 + step;
                Vector2 Uv(float x, float z) => new Vector2((x + h) / 4f, (z + h) / 4f);
                ground.Quad(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0),
                    Uv(x0, z0), Uv(x0, z1), Uv(x1, z1), Uv(x1, z0));
            }
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

        PlaceSigns(root, roadWays);

        // buildings: real 3D models where available (3D BAG, the Netherlands), else OSM footprints extruded
        var buildingMbs = facades.Select(_ => new MeshBuilder()).ToArray();
        var roofs = new MeshBuilder();
        var tileRoofs = new[] { new MeshBuilder(), new MeshBuilder() };
        var tileMats = new[]
        {
            Mat("Roof tiles red", Color.white, 0.25f, RoofTiles("roof_tiles_red", new Color(0.46f, 0.2f, 0.13f))),
            Mat("Roof tiles anthracite", Color.white, 0.3f, RoofTiles("roof_tiles_grey", new Color(0.2f, 0.21f, 0.22f))),
        };
        List<Bag3dBuildings.Building> bag = null;
        try { bag = Bag3dBuildings.Load(_map.Name ?? "", _proj); }
        catch (Exception e) { Debug.LogWarning("Bag3dBuildings: " + e.Message); }
        if (bag != null)
        {
            Real3DBuildings = true;
            AddRealBuildings(bag, buildingMbs, palette, roofs, tileRoofs);
        }
        foreach (var w in bag != null ? Enumerable.Empty<OsmWay>() : _data.Ways.Where(w => w.IsClosed && w.Tag("building") != null))
        {
            var poly = Line(w);
            if (poly.Count < 4 || !poly.Any(Inside)) continue;
            poly.RemoveAt(poly.Count - 1);
            float height = BuildingHeight(w);
            float minHeight = ParseMetres(w.Tag("min_height"), 0f);
            if (Geometry2D.SignedArea(poly) < 0) poly.Reverse();
            var style = StyleFor(0, height, w.Tag("building"), (uint)(Math.Abs(w.Id) * 2654435761L));
            var mb = buildingMbs[palette[(int)(Math.Abs(w.Id) % palette.Length)] * Styles + (int)style];
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
        for (int i = 0; i < facades.Length; i++)
            if (buildingMbs[i].V.Count > 0) { var m = _facadeMat(i); Emit(buildings, "Buildings " + m.name, buildingMbs[i], m, true); }
        Emit(buildings, "Roofs", roofs, roof, false);
        for (int i = 0; i < tileRoofs.Length; i++) Emit(buildings, "Roofs " + tileMats[i].name, tileRoofs[i], tileMats[i], false);

        PlaceStreetFurniture(root);

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

    // ------------------------------------------------------------------ traffic signs

    public int SignCount;
    const int MaxSigns = 600;

    static int SpeedNumber(string maxspeed)
    {
        if (string.IsNullOrEmpty(maxspeed)) return 0;
        var digits = new string(maxspeed.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var v) ? v : 0;
    }

    /// <summary>
    /// Signs from OSM: stop and give-way nodes (for the traffic approaching them, or the
    /// direction tag), pedestrian crossing signs at marked crossings, speed limits (maxspeed) at
    /// the start of longer roads, one-way and no-entry signs at the ends of one-way roads. On the
    /// right side of the road (left where traffic drives on the left), facing the traffic.
    /// </summary>
    void PlaceSigns(Transform root, List<(OsmWay way, List<V2> line, float width)> roadWays)
    {
        var signs = new GameObject("Traffic signs").transform;
        signs.SetParent(root, false);
        float sideSign = _map.LeftHandTraffic ? -1f : 1f;
        var used = new HashSet<string>();
        void Sign(TrafficSignKind kind, V2 at, V2 travel, float width, int limit = 0)
        {
            if (SignCount >= MaxSigns || travel.LengthSquared() < 1e-6f) return;
            travel = V2.Normalize(travel);
            var right = new V2(travel.Y, -travel.X) * sideSign;
            var p = at + right * (width / 2f + 0.7f);
            if (!Inside(p)) return;
            // one sign of a kind per 3 m cell
            var key = $"{kind}{limit}:{Math.Round(p.X / 3f)}:{Math.Round(p.Y / 3f)}";
            if (!used.Add(key)) return;
            var go = new GameObject($"Sign {kind}{(limit > 0 ? " " + limit : "")}");
            go.transform.SetParent(signs, false);
            // the picture faces the oncoming traffic
            go.transform.SetPositionAndRotation(P(p, PavementHeight), Quaternion.LookRotation(new Vector3(-travel.X, 0f, -travel.Y), Vector3.up));
            go.SetActive(false);
            var s = go.AddComponent<TrafficSign>();
            s.Kind = kind; s.SpeedLimit = limit;
            s.Size = width >= 7f ? 0.75f : 0.6f;
            go.SetActive(true);
            SignCount++;
        }
        foreach (var (w, line, width) in roadWays)
        {
            var ids = w.NodeIds.Where(_pos.ContainsKey).ToList();
            if (ids.Count < 2) continue;
            bool oneWay = IsOneWay(w);
            float total = Geometry2D.Length(line);
            for (int i = 0; i < ids.Count; i++)
            {
                if (!_data.Nodes.TryGetValue(ids[i], out var node)) continue;
                var hw = node.Tag("highway");
                var p = _pos[ids[i]];
                V2 prev = _pos[ids[Math.Max(0, i - 1)]], next = _pos[ids[Math.Min(ids.Count - 1, i + 1)]];
                if (hw == "stop" || hw == "give_way")
                {
                    var kind = hw == "stop" ? TrafficSignKind.Stop : TrafficSignKind.GiveWay;
                    var dirTag = node.Tag("direction");
                    // traffic towards the node: along the way when the node is in its second half
                    bool forward = dirTag == "forward" || (dirTag != "backward" && i >= ids.Count / 2);
                    var travel = forward ? p - prev : p - next;
                    Sign(kind, p - V2.Normalize(travel) * 1.5f, travel, width);
                }
                else if (hw == "crossing")
                {
                    var c = node.Tag("crossing"); var cr = node.Tag("crossing_ref") ?? node.Tag("crossing:markings");
                    bool zebra = cr == "zebra" || c == "zebra" || c == "marked" || c == "uncontrolled";
                    if (!zebra) continue;
                    var dir = next - prev;
                    if (dir.LengthSquared() < 1e-6f) continue;
                    Sign(TrafficSignKind.PedestrianCrossing, p - V2.Normalize(dir) * 2.5f, dir, width);
                    if (!oneWay) Sign(TrafficSignKind.PedestrianCrossing, p + V2.Normalize(dir) * 2.5f, -dir, width);
                }
            }
            int limit = SpeedNumber(w.Tag("maxspeed"));
            if (limit > 0 && total > 120f)
            {
                var (a, da) = Geometry2D.PointAt(line, 12f);
                Sign(TrafficSignKind.SpeedLimit, a, da, width, limit);
                if (!oneWay) { var (b, db) = Geometry2D.PointAt(line, total - 12f); Sign(TrafficSignKind.SpeedLimit, b, -db, width, limit); }
            }
            if (oneWay && total > 30f && w.Tag("junction") != "roundabout" && !(w.Tag("highway") ?? "").StartsWith("motorway"))
            {
                var (a, da) = Geometry2D.PointAt(line, 6f);
                Sign(TrafficSignKind.OneWay, a, da, width);
                var (b, db) = Geometry2D.PointAt(line, total - 3f);
                Sign(TrafficSignKind.NoEntry, b, -db, width);
            }
        }
    }

    // ------------------------------------------------------------------ street furniture and parked vehicles

    public int PropCount, ParkedCarCount, ParkedBicycleCount;
    const int MaxParkedCars = 250, MaxParkedBicycles = 400;

    // the nearest car road: its direction there and the distance from its centre line
    bool NearestRoad(V2 p, out V2 dir, out float distance, out float width)
    {
        dir = new V2(1, 0); distance = float.MaxValue; width = 6f;
        foreach (var r in Roads)
            for (int i = 0; i + 1 < r.Line.Count; i++)
            {
                var a = new V2(r.Line[i].x, r.Line[i].z); var b = new V2(r.Line[i + 1].x, r.Line[i + 1].z);
                var ab = b - a; float len2 = ab.LengthSquared(); if (len2 < 1e-6f) continue;
                float t = Math.Clamp(V2.Dot(p - a, ab) / len2, 0f, 1f);
                float d = V2.Distance(p, a + ab * t);
                if (d < distance) { distance = d; dir = V2.Normalize(ab); width = r.Width; }
            }
        return distance < 60f;
    }

    // ground height under a point (pavement, square or road), ignoring roofs
    static float GroundAt(V2 p, float fallback)
    {
        var o = new Vector3(p.X, 30f, p.Y);
        foreach (var hit in Physics.RaycastAll(o, Vector3.down, 40f).OrderBy(h => h.distance))
            if (hit.point.y < 0.6f) return hit.point.y;
        return fallback;
    }

    static float Yaw(V2 dir) => Mathf.Atan2(dir.X, dir.Y) * Mathf.Rad2Deg;

    /// <summary>
    /// From OpenStreetMap: benches, waste bins, bicycle racks with bicycles, bollards, bus stops
    /// (sign, shelter, bench), café terraces, recycling containers, street cabinets, walls, fences
    /// and hedges, cars in parking lots and spaces and along streets with street-side parking.
    /// Deterministic (seeded with the map name), so every agent has the same.
    /// </summary>
    void PlaceStreetFurniture(Transform root)
    {
        Physics.SyncTransforms();
        var props = new GameObject("Street furniture (OSM)").transform;
        props.SetParent(root, false);
        var rng = new System.Random((_map.Name ?? "").Length * 7919 + 17);
        var metal = Mat("Street metal", new Color(0.28f, 0.29f, 0.3f), 0.45f);
        var green = Mat("Hedge", Color.white, 0.05f, Tex("hedge", 128, (x, y, r) => Noise(new Color(0.16f, 0.3f, 0.12f), r, 0.6f)));
        var containerMat = Mat("Containers", new Color(0.18f, 0.32f, 0.22f), 0.3f);
        var glass = Mat("Shelter glass", new Color(0.55f, 0.65f, 0.7f), 0.9f);
        var wood = Mat("Terrace furniture", new Color(0.42f, 0.3f, 0.2f), 0.3f);
        var brickWall = Mat("Garden wall", Color.white, 0.1f, Tex("garden_wall", 128, (x, y, r) => (y % 16 < 2 || (x + (y / 16 % 2) * 16) % 32 < 2) ? new Color(0.5f, 0.47f, 0.43f) : Noise(new Color(0.5f, 0.25f, 0.18f), r, 0.25f)));
        MeshBuilder bollards = new MeshBuilder(), racks = new MeshBuilder(), hedges = new MeshBuilder(), walls = new MeshBuilder(),
            fences = new MeshBuilder(), containers = new MeshBuilder(), shelters = new MeshBuilder(), furniture = new MeshBuilder(), cabinets = new MeshBuilder(), busSigns = new MeshBuilder();
        var assets = _assets;

        void Place(GameObject prefab, V2 p, float yaw, string name)
        {
            if (prefab == null) return;
            // keep the prefab's own rotation (models imported lying on their back are turned upright there)
            var go = UnityEngine.Object.Instantiate(prefab, new Vector3(p.X, GroundAt(p, PavementHeight), p.Y), Quaternion.Euler(0, yaw, 0) * prefab.transform.rotation, props);
            go.name = name;
            go.isStatic = true;
            PropCount++;
        }
        // parked vehicles are collected first and the ones nearest the centre placed (the caps keep the frame rate)
        var cars = new List<(V2 p, float yaw)>(); var bikes = new List<(V2 p, float yaw)>();
        void Car(V2 p, float yaw) { if (Inside(p)) cars.Add((p, yaw + (rng.Next(2) == 0 ? 0 : 180))); }
        void Bike(V2 p, float yaw) { if (Inside(p)) bikes.Add((p, yaw + (float)(rng.NextDouble() * 10 - 5))); }

        foreach (var node in _data.Nodes.Values)
        {
            if (node.Tags.Count == 0 || !_pos.TryGetValue(node.Id, out var p) || !Inside(p)) continue;
            var amenity = node.Tag("amenity"); var barrier = node.Tag("barrier"); var hw = node.Tag("highway");
            NearestRoad(p, out var dir, out var dist, out var width);
            float along = Yaw(dir);
            float y = GroundAt(p, PavementHeight);
            var p3 = new Vector3(p.X, y, p.Y);
            if (amenity == "bench") Place(assets?.Bench, p, along + 180, "Bench");
            else if (amenity == "waste_basket") Place(assets?.WasteBin, p, along, "Waste bin");
            else if (amenity == "bicycle_parking")
            {
                int capacity = Math.Clamp(int.TryParse(node.Tag("capacity"), out var c) ? c : 8, 2, 20);
                var right = new V2(dir.Y, -dir.X);
                for (int i = 0; i < capacity; i++)
                {
                    var q = p + dir * ((i - capacity / 2f) * 0.75f);
                    var q3 = new Vector3(q.X, y, q.Y);
                    racks.Box(q3, new Vector3(0.05f, 0.75f, 0.05f), along);              // a staple: two posts and a bar
                    racks.Box(q3 + new Vector3(right.X, 0, right.Y) * 0.5f, new Vector3(0.05f, 0.75f, 0.05f), along);
                    if (rng.NextDouble() < 0.7) Bike(q + right * 0.25f, Yaw(right));
                }
            }
            else if (barrier == "bollard") { bollards.Box(p3, new Vector3(0.14f, 0.9f, 0.14f), along); PropCount++; }
            else if (hw == "bus_stop")
            {
                var right = new V2(dir.Y, -dir.X);
                // a pole with a blue sign
                bollards.Box(p3, new Vector3(0.07f, 2.6f, 0.07f), along);
                busSigns.Box(p3 + Vector3.up * 2.1f, new Vector3(0.05f, 0.6f, 0.45f), along);
                if (node.Tag("shelter") == "yes" || node.Tag("bench") == "yes")
                {
                    var c3 = p3 + new Vector3(right.X, 0, right.Y) * 1.5f;
                    var fwd = Quaternion.Euler(0, along, 0);
                    shelters.Box(c3 + fwd * new Vector3(0, 0, 0) + Vector3.up * 2.3f, new Vector3(1.6f, 0.08f, 3.2f), along);   // roof
                    shelters.Box(c3 + fwd * new Vector3(0.75f, 0, 0), new Vector3(0.04f, 2.3f, 3.2f), along);                  // back wall
                    shelters.Box(c3 + fwd * new Vector3(0, 0, 1.55f), new Vector3(1.5f, 2.3f, 0.04f), along);                  // side
                    Place(assets?.Bench, new V2(c3.x, c3.z), along - 90, "Bus stop bench");
                }
                PropCount++;
            }
            else if (node.Tag("leisure") == "outdoor_seating")
            {
                for (int t = 0; t < 4; t++)
                {
                    var q = p + dir * ((t - 1.5f) * 1.6f);
                    var q3 = new Vector3(q.X, y, q.Y);
                    furniture.Box(q3, new Vector3(0.7f, 0.74f, 0.7f) * 1f, along);
                    furniture.Box(q3 + Quaternion.Euler(0, along, 0) * new Vector3(0.55f, 0, 0), new Vector3(0.4f, 0.45f, 0.4f), along);
                    furniture.Box(q3 + Quaternion.Euler(0, along, 0) * new Vector3(-0.55f, 0, 0), new Vector3(0.4f, 0.45f, 0.4f), along);
                }
                PropCount++;
            }
            else if (amenity == "recycling")
            {
                for (int k = 0; k < 3; k++) containers.Box(p3 + Quaternion.Euler(0, along, 0) * new Vector3(0, 0, (k - 1) * 1.7f), new Vector3(1.4f, 1.6f, 1.5f), along);
                PropCount++;
            }
            else if (node.Tag("man_made") == "street_cabinet") { cabinets.Box(p3, new Vector3(0.7f, 1.2f, 0.35f), along); PropCount++; }
            else if (amenity == "parking_space") Car(p, along);
        }

        foreach (var w in _data.Ways)
        {
            var barrier = w.Tag("barrier"); var amenity = w.Tag("amenity");
            if (barrier == "wall" || barrier == "fence" || barrier == "hedge" || barrier == "retaining_wall")
            {
                var line = Line(w);
                if (line.Count < 2 || !line.Any(Inside)) continue;
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    var a = line[i]; var b = line[i + 1]; float len = V2.Distance(a, b); if (len < 0.2f) continue;
                    var mid = (a + b) / 2; float yaw = Yaw(V2.Normalize(b - a));
                    var m3 = new Vector3(mid.X, GroundAt(mid, 0f), mid.Y);
                    if (barrier == "hedge") hedges.Box(m3, new Vector3(0.9f, 1.3f, len), yaw);
                    else if (barrier == "fence") fences.Box(m3, new Vector3(0.05f, 1.3f, len), yaw);
                    else walls.Box(m3, new Vector3(0.3f, barrier == "retaining_wall" ? 0.8f : 1.8f, len), yaw);
                }
                PropCount++;
            }
            else if (amenity == "parking" && w.IsClosed && w.Tag("parking") != "underground" && w.Tag("parking") != "multi-storey" && w.Tag("parking") != "rooftop" && w.Tag("building") == null)
            {
                // rows of cars across the lot, along its longest side, with aisles
                var poly = Line(w); if (poly.Count < 4 || !poly.Any(Inside)) continue;
                poly.RemoveAt(poly.Count - 1);
                int longest = 0; float best = 0;
                for (int i = 0; i < poly.Count; i++) { float l = V2.Distance(poly[i], poly[(i + 1) % poly.Count]); if (l > best) { best = l; longest = i; } }
                var ax = V2.Normalize(poly[(longest + 1) % poly.Count] - poly[longest]); var ay = new V2(-ax.Y, ax.X);
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                foreach (var q in poly) { float u = V2.Dot(q, ax), v = V2.Dot(q, ay); minA = Math.Min(minA, u); maxA = Math.Max(maxA, u); minB = Math.Min(minB, v); maxB = Math.Max(maxB, v); }
                int row = 0;
                for (float v = minB + 3f; v < maxB - 2.5f; v += (row++ % 2 == 0) ? 5.2f : 6.5f)
                    for (float u = minA + 1.5f; u < maxA - 1.2f; u += 2.6f)
                    {
                        var q = ax * u + ay * v;
                        if (Geometry2D.PointInPolygon(q, poly) && rng.NextDouble() < 0.75) Car(q, Yaw(ay));
                    }
            }
            else if (amenity == "parking_space" && w.IsClosed)
            {
                var poly = Line(w); if (poly.Count < 4) continue;
                var c = poly.Aggregate(V2.Zero, (s, q) => s + q) / poly.Count;
                var e = V2.Distance(poly[0], poly[1]) > V2.Distance(poly[1], poly[2]) ? poly[1] - poly[0] : poly[2] - poly[1];
                Car(c, Yaw(V2.Normalize(e)));
            }
            else if (CarRoads.Contains(w.Tag("highway") ?? ""))
            {
                // street-side parking: along the kerb, a car every ~6.5 m, not all bays taken
                foreach (var side in new[] { -1, 1 })
                {
                    var tag = w.Tag("parking:both") ?? w.Tag(side < 0 ? "parking:left" : "parking:right") ?? w.Tag("parking:lane:both") ?? w.Tag(side < 0 ? "parking:lane:left" : "parking:lane:right");
                    if (tag == null || tag == "no" || tag == "separate" || tag == "no_parking" || tag == "no_stopping" || tag == "fire_lane") continue;
                    var line = Line(w); if (line.Count < 2 || !line.Any(Inside)) continue;
                    float width = RoadWidth(w), total = Geometry2D.Length(line);
                    for (float d = 8f; d < total - 8f; d += 6.5f)
                    {
                        if (rng.NextDouble() > 0.6) continue;
                        var (pt, dir) = Geometry2D.PointAt(line, d);
                        var n = new V2(dir.Y, -dir.X) * side;
                        Car(pt + n * (width / 2f - 1.0f), Yaw(dir));
                    }
                }
            }
        }
        if (assets != null && assets.ParkedCars != null && assets.ParkedCars.Length > 0)
            foreach (var c in cars.OrderBy(c => c.p.Length()).Take(MaxParkedCars))
            { Place(assets.ParkedCars[rng.Next(assets.ParkedCars.Length)], c.p, c.yaw, "Parked car"); ParkedCarCount++; }
        if (assets != null && assets.ParkedBicycles != null && assets.ParkedBicycles.Length > 0)
            foreach (var b in bikes.OrderBy(b => b.p.Length()).Take(MaxParkedBicycles))
            { Place(assets.ParkedBicycles[rng.Next(assets.ParkedBicycles.Length)], b.p, b.yaw, "Parked bicycle"); ParkedBicycleCount++; }
        Emit(props, "Bollards", bollards, metal, true);
        Emit(props, "Bicycle racks", racks, metal, false);
        Emit(props, "Hedges", hedges, green, true);
        Emit(props, "Walls", walls, brickWall, true);
        Emit(props, "Fences", fences, metal, true);
        Emit(props, "Recycling containers", containers, containerMat, true);
        Emit(props, "Bus shelters", shelters, glass, true);
        Emit(props, "Terrace furniture", furniture, wood, true);
        Emit(props, "Street cabinets", cabinets, metal, true);
        Emit(props, "Bus stop signs", busSigns, Mat("Bus stop sign", new Color(0.05f, 0.25f, 0.6f), 0.5f), false);
    }

    // ------------------------------------------------------------------ real 3D buildings (3D BAG)

    static Texture2D RoofTiles(string name, Color baseColor) => Tex(name, 256, (x, y, r) =>
    {
        // rows of overlapping tiles: a dark shadow line under each row, rows offset by half a tile
        int row = y / 32, ty = y % 32, tx = (x + (row % 2) * 16) % 32;
        var c = Noise(baseColor, r, 0.18f);
        if (ty < 3) c *= 0.55f;
        else if (tx < 2) c *= 0.75f;
        else c *= 0.9f + 0.1f * ty / 32f;
        c.a = 1f;
        return c;
    });

    public int BuildingsOverRoads;
    const float ClearanceUnderBuildings = 5f;

    /// <summary>Whether a car road runs through the building's footprint (6 m or more of it).</summary>
    bool OverRoad(Bag3dBuildings.Building b)
    {
        float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
        foreach (var v in b.V) { x0 = Math.Min(x0, v.x); x1 = Math.Max(x1, v.x); z0 = Math.Min(z0, v.z); z1 = Math.Max(z1, v.z); }
        int hits = 0;
        foreach (var r in Roads)
            for (int i = 0; i + 1 < r.Line.Count; i++)
            {
                Vector3 a = r.Line[i], c = r.Line[i + 1];
                if (Math.Max(a.x, c.x) < x0 || Math.Min(a.x, c.x) > x1 || Math.Max(a.z, c.z) < z0 || Math.Min(a.z, c.z) > z1) continue;
                float len = Vector3.Distance(a, c);
                for (float d = 0; d <= len; d += 2f)
                {
                    var p = Vector3.Lerp(a, c, len > 0 ? d / len : 0);
                    if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue;
                    for (int t = 0; t + 2 < b.Roofs.Length; t += 3)
                        if (InTriangle(p, b.V[b.Roofs[t]], b.V[b.Roofs[t + 1]], b.V[b.Roofs[t + 2]])) { if (++hits >= 3) return true; break; }
                }
            }
        return false;
    }

    static bool InTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        float d1 = (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.z - b.z);
        float d2 = (p.x - c.x) * (b.z - c.z) - (b.x - c.x) * (p.z - c.z);
        float d3 = (p.x - a.x) * (c.z - a.z) - (c.x - a.x) * (p.z - a.z);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    void AddRealBuildings(List<Bag3dBuildings.Building> bag, MeshBuilder[] facadeMbs, int[] palette, MeshBuilder flatRoofs, MeshBuilder[] tileRoofs)
    {
        for (int bi = 0; bi < bag.Count; bi++)
        {
            var b = bag[bi];
            bool inside = false;
            foreach (var v in b.V) if (Inside(new V2(v.x, v.z))) { inside = true; break; }
            if (!inside) continue;
            // older buildings are mostly brick; newer ones get the whole palette
            int pi = (int)((uint)(bi * 2654435761u) % (uint)palette.Length);
            float top = 0f;
            foreach (var v in b.V) top = Math.Max(top, v.y);
            var style = StyleFor(b.Year, top, null, (uint)(bi * 2246822519u));
            int kind = b.Year > 0 && b.Year < 1940 ? palette[pi % 3] : palette[pi];
            if (style == WindowStyle.Glass && kind < 3) kind = 3 + bi % 2;   // glass walls: plaster or concrete frames
            var wall = facadeMbs[kind * Styles + (int)style];
            var verts = b.V;
            if (OverRoad(b))
            {
                // over a car road (a station building, a passage): the road stays open under it, at
                // least ClearanceUnderBuildings high, with the building's underside as a ceiling
                if (top < ClearanceUnderBuildings + 2f) continue;
                verts = b.V.Select(v => v.y < ClearanceUnderBuildings ? new Vector3(v.x, ClearanceUnderBuildings, v.z) : v).ToArray();
                var under = new int[b.Roofs.Length];
                for (int i = 0; i + 2 < under.Length; i += 3) { under[i] = b.Roofs[i]; under[i + 1] = b.Roofs[i + 2]; under[i + 2] = b.Roofs[i + 1]; }
                var flat = b.V.Select(v => new Vector3(v.x, ClearanceUnderBuildings, v.z)).ToArray();
                AddTriangles(flatRoofs, flat, under, false);
                BuildingsOverRoads++;
            }
            AddTriangles(wall, verts, b.Walls, true);
            AddTriangles(b.RoofKind == 1 ? tileRoofs[bi % 3 == 0 ? 1 : 0] : flatRoofs, verts, b.Roofs, false);
            BuildingCount++;
        }
    }

    // each triangle on its own vertices (flat shading), turned to face outwards (walls) or up (roofs),
    // with UVs in metres along the surface (facades 3 m per texture tile, roof tiles 2.5 m)
    static void AddTriangles(MeshBuilder mb, Vector3[] v, int[] tris, bool wall)
    {
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-8f) continue;
            // the 3D BAG's surfaces face outwards in RD (x east, y north, z up, right-handed); in Unity's
            // left-handed axes (y up, z north) the same order faces inwards, so every triangle is turned
            // over (not by the direction to the building's centre: courtyard walls face the centre)
            { var t = b; b = c; c = t; n = -n; }
            n.Normalize();
            var tan = Vector3.Cross(Vector3.up, n);
            if (tan.sqrMagnitude < 1e-4f) tan = Vector3.right;
            tan.Normalize();
            var bit = Vector3.Cross(n, tan);
            int k = mb.V.Count;
            foreach (var p in new[] { a, b, c })
            {
                mb.V.Add(p);
                mb.UV.Add(wall ? new Vector2(Vector3.Dot(p, tan) / 3f, p.y / 3f) : new Vector2(Vector3.Dot(p, tan) / 2.5f, Vector3.Dot(p, bit) / 2.5f));
            }
            mb.T.Add(k); mb.T.Add(k + 1); mb.T.Add(k + 2);
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
