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
public partial class OsmMapGenerator
{
    public sealed class Road
    {
        public List<Vector3> Line;
        public float Width;
        public string Type;
        public bool OneWay;
        /// <summary>On a bridge above other roads or the ground (layer 1 and up): lifted straight between its ends.</summary>
        public bool Bridge;
        /// <summary>That bridge's layer (0: not on such a bridge).</summary>
        public int BridgeLayer;
        /// <summary>Cars parked along the left / right kerb (as seen along the way), on the carriageway.</summary>
        public bool ParkLeft, ParkRight;
        /// <summary>Not for the built-in traffic and the autopilot: a service road with no or private access, or (on maps
        /// with the national topography's surfaces) a road that lies mostly on paving, grass or gravel there (pedestrian
        /// streets and squares, yards).</summary>
        public bool NoThroughTraffic;
        /// <summary>Left out of the traffic's routes only as it lies on the topography's paving, grass or gravel (a pedestrian
        /// street, a square, a yard, a campus boulevard): riders still find their way along it.</summary>
        public bool PavedOver;
        /// <summary>Speed limit (km/h): OpenStreetMap's maxspeed (mph converted), else a default by road type.</summary>
        public float SpeedLimitKmh;
        /// <summary>Lanes (OpenStreetMap's lanes; 0: not given).</summary>
        public int Lanes;
    }
    /// <summary>Cycle paths and pavements on such bridges.</summary>
    public readonly Dictionary<List<Vector3>, int> BridgeLines = new Dictionary<List<Vector3>, int>();

    public readonly List<Road> Roads = new List<Road>();
    /// <summary>The trees placed (they sway in the wind: TreeSway).</summary>
    public readonly List<Transform> TreeTransforms = new List<Transform>();
    public readonly List<List<Vector3>> CyclePaths = new List<List<Vector3>>();
    public readonly List<List<Vector3>> Pavements = new List<List<Vector3>>();
    /// <summary>The pavement lines that cross a road (footway=crossing): walked along, but no place to start on.</summary>
    public readonly HashSet<List<Vector3>> CrossingLines = new HashSet<List<Vector3>>();
    public readonly List<PedestrianCrossing> Crossings = new List<PedestrianCrossing>();
    public float HalfSize;
    /// <summary>The map's area beyond its square: the centre and corridors along a highway or ring road
    /// (StreamingAssets/Maps/{map}.area.txt, Tools/map_area.py); null: the square of its data.</summary>
    public MapArea Area { get; private set; }
    public int BuildingCount;

    readonly MapInfo _map;
    readonly OsmData _data;
    readonly GeoProjection _proj;
    /// <summary>True when the buildings are real 3D models (3D BAG) instead of extruded footprints.</summary>
    public bool Real3DBuildings { get; private set; }
    readonly MapAssets _assets;
    Func<int, Material> _facadeMat;
    Func<int, Material> _groundMat;
    /// <summary><c>-noFacadeRelief</c>: flat façades (no normal or parallax maps), e.g. for comparisons or slow PCs.</summary>
    static readonly bool _noRelief = Array.IndexOf(CommandLine.Args, "-noFacadeRelief") >= 0;
    /// <summary>Photo textures of brick, plaster and concrete on the walls (MapAssets.WallDetail); <c>-noPhotoTextures</c>: the drawn bricks of earlier versions.</summary>
    static bool _photoWalls;
    static readonly bool _noPhotoWalls = Array.IndexOf(CommandLine.Args, "-noPhotoTextures") >= 0;
    /// <summary>Façade materials whose windows light up at night (OsmMap sets their emission from the sun).</summary>
    public static readonly List<Material> NightMaterials = new List<Material>();
    static readonly int Styles = Enum.GetValues(typeof(WindowStyle)).Length;
    readonly Dictionary<long, V2> _pos = new Dictionary<long, V2>();
    readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    /// <summary>Height of the pavements (m): raised behind a kerb, or flat (0.012 m) on maps drawn from a national large-scale
    /// topography (the Dutch BGT), whose surfaces have no heights.</summary>
    public static float PavementHeight = 0.14f;
    /// <summary>The map's ground is drawn from the national topography's surfaces (the Dutch BGT): the roads, cycle paths and
    /// footpaths made from OpenStreetMap's lines are not drawn (they would lie over the real outlines, a little off); the
    /// roads stay as invisible driving surfaces, and the generated pavements, cycle paths and kerbs are left out.</summary>
    public bool TopographyGround { get; private set; }

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
        try
        {
            var areaFile = System.IO.Path.Combine(Application.streamingAssetsPath, "Maps", (map.Name ?? "") + ".area.txt");
            if (!string.IsNullOrEmpty(map.Name) && System.IO.File.Exists(areaFile) && Array.IndexOf(CommandLine.Args, "-noCorridors") < 0)
            {
                Area = MapArea.Parse(System.IO.File.ReadAllText(areaFile), (la, lo) => { proj.ToLocal(la, lo, out var ax, out var az); return new V2((float)ax, (float)az); });
                Debug.Log($"OsmMap: {map.Name} has {Area.CorridorCount} corridors ({Area.CorridorLength / 1000f:F1} km) beyond its centre");
            }
        }
        catch (Exception e) { Debug.LogWarning("OsmMap: area file: " + e.Message); Area = null; }
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

    /// <summary>A detail texture for flat roofs (multiplied by two over the photo: 0.5 is no change): gravel grains
    /// of a few millimetres to a few centimetres, and faint blotches of bitumen.</summary>
    static Texture2D RoofGrain() => Tex("roof_grain_detail", 256, (x, y, r) =>
    {
        float g = 0.5f + ((float)r.NextDouble() - 0.5f) * 0.22f;
        // (blotches: a smooth pattern over the tile, seamless)
        float a = 2f * Mathf.PI * x / 256f, b = 2f * Mathf.PI * y / 256f;
        g += 0.035f * (Mathf.Sin(a * 3f + 1.3f) * Mathf.Cos(b * 2f + 0.4f) + 0.6f * Mathf.Sin(a * 7f + b * 5f));
        return new Color(g, g, g, 1f);
    });

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
        Church,   // masonry with tall pointed windows on one floor in four, in every other bay (churches and their towers)
    }

    /// <summary>Bays per side of a façade texture: it holds 4 x 4 window bays (3 m by one floor of 3 m each),
    /// so lights can differ from window to window at night (the material's tiling is 1/4).</summary>
    public const int FacadeBays = 4;
    const int BayPx = 160;

    static Texture2D Facade(string name, Color wall, Color frame, bool brick, WindowStyle style = WindowStyle.Classic) =>
        Tex(name, BayPx * FacadeBays, (x, y, r) => FacadeTile(x % BayPx * 256 / BayPx, y % BayPx * 256 / BayPx, r, wall, frame, brick, style, x / BayPx, y / BayPx));

    /// <summary>Lit windows of a façade texture at night: warm light in about a third of the windows (random per bay), black elsewhere.</summary>
    static Texture2D FacadeLights(string name, WindowStyle style)
    {
        var rng = new System.Random(name.GetHashCode() ^ 0x5bd1e995);
        var lit = new Color[FacadeBays * FacadeBays];
        for (int i = 0; i < lit.Length; i++)
        {
            double p = rng.NextDouble();
            lit[i] = p < 0.38 ? Color.Lerp(new Color(1f, 0.78f, 0.5f), new Color(0.85f, 0.9f, 1f), (float)rng.NextDouble() * (p < 0.08 ? 1f : 0.3f)) * (0.55f + 0.45f * (float)rng.NextDouble()) : Color.black;
        }
        return Tex(name, BayPx * FacadeBays, (x, y, r) =>
        {
            var c = lit[y / BayPx * FacadeBays + x / BayPx];
            if (c.maxColorComponent <= 0f) return Color.black;
            float u = x % BayPx / (float)BayPx, v = y % BayPx / (float)BayPx;
            return InWindow(u, v, style, x / BayPx, y / BayPx) ? c : Color.black;
        });
    }

    // the glass of a window bay (as drawn by FacadeTile)
    /// <summary>A church's tall pointed (lancet) window: only on one floor in four and in every other bay.</summary>
    static bool Lancet(float u, float v, int bay, int floor, float grow = 0f) =>
        floor % FacadeBays == 1 && bay % 2 == 0 && Mathf.Abs(u - 0.5f) < 0.09f + grow && v > 0.06f - grow &&
        v < 0.78f + 0.16f * (1f - Mathf.Abs(u - 0.5f) / (0.09f + grow)) + grow;

    static bool InWindow(float u, float v, WindowStyle style, int bay = 0, int floor = 1)
    {
        switch (style)
        {
            case WindowStyle.Church: return Lancet(u, v, bay, floor);
            case WindowStyle.Tall: return u > 0.34f && u < 0.66f && v > 0.18f && v < 0.88f;
            case WindowStyle.Ribbon: return v > 0.4f && v < 0.8f && u > 0.03f && u < 0.97f;
            case WindowStyle.Pair: return (Mathf.Abs(u - 0.28f) < 0.15f || Mathf.Abs(u - 0.72f) < 0.15f) && v > 0.32f && v < 0.84f;
            case WindowStyle.Glass: return u > 0.02f && u < 0.98f && v > 0.2f;
            default: return u > 0.3f && u < 0.7f && v > 0.3f && v < 0.85f;
        }
    }

    /// <summary>Relief of a window bay in metres (wall = 0): glass set back in its reveal, frames a little back,
    /// sills and lintels standing out, mortar joints of brick. Same layout as <see cref="FacadeTile"/>.</summary>
    static float FacadeHeight(int x, int y, WindowStyle style, bool brick, int bay = 0, int floor = 1)
    {
        float u = x / 256f, v = y / 256f;
        const float f = 0.012f, glass = -0.14f, frameD = -0.06f, bar = -0.08f, sill = 0.04f;
        switch (style)
        {
            case WindowStyle.Church:
                if (Lancet(u, v, bay, floor)) return Mathf.Abs(u - 0.5f) < f || (v * 10f % 1f) < 0.06f ? bar : glass * 1.5f;   // leaded lights, deep in the wall
                if (Lancet(u, v, bay, floor, 0.025f)) return frameD;
                if (floor % FacadeBays == 0 && v < 0.035f) return sill;   // a stone string course
                break;
            case WindowStyle.Tall:
                if (u > 0.34f && u < 0.66f && v > 0.18f && v < 0.88f) return Mathf.Abs(u - 0.5f) < f || Mathf.Abs(v - 0.42f) < f || Mathf.Abs(v - 0.65f) < f ? bar : glass;
                if (u > 0.31f && u < 0.69f && v > 0.15f && v < 0.91f) return frameD;
                if (u > 0.29f && u < 0.71f && v > 0.91f && v < 0.96f) return sill * 0.7f;
                if (u > 0.3f && u < 0.7f && v > 0.12f && v < 0.15f) return sill;
                break;
            case WindowStyle.Ribbon:
                if (v > 0.4f && v < 0.8f) return u < f * 2 || u > 1f - f * 2 || Mathf.Abs(u - 0.5f) < f || Mathf.Abs(v - 0.7f) < f ? bar : glass;
                if (v > 0.37f && v < 0.83f) return frameD;
                if (v > 0.32f && v < 0.37f) return sill;
                break;
            case WindowStyle.Pair:
                foreach (var c in new[] { 0.28f, 0.72f })
                {
                    if (Mathf.Abs(u - c) < 0.15f && v > 0.32f && v < 0.84f) return Mathf.Abs(v - 0.66f) < f ? bar : glass;
                    if (Mathf.Abs(u - c) < 0.17f && v > 0.3f && v < 0.86f) return frameD;
                }
                if (v > 0.26f && v < 0.3f && (Mathf.Abs(u - 0.28f) < 0.18f || Mathf.Abs(u - 0.72f) < 0.18f)) return sill;
                break;
            case WindowStyle.Glass:
                if (u < 0.02f || u > 0.98f || Mathf.Abs(u - 0.5f) < 0.008f) return 0.03f;   // mullions stand out of the glass
                if (v < 0.2f) return v < 0.02f || v > 0.18f ? 0.03f : 0f;
                return -0.02f;
            default:
                if (u > 0.3f && u < 0.7f && v > 0.3f && v < 0.85f) return Mathf.Abs(u - 0.5f) < f || Mathf.Abs(v - 0.62f) < f ? bar : glass;
                if (u > 0.27f && u < 0.73f && v > 0.27f && v < 0.88f) return frameD;
                if (u > 0.25f && u < 0.75f && v > 0.24f && v < 0.27f) return sill;
                break;
        }
        if (brick)
        {
            int course = y / 6, offset = course % 2 == 0 ? 0 : 12;
            if (y % 6 == 0 || (x + offset) % 24 == 0) return -0.008f;
        }
        return 0f;
    }

    static readonly Dictionary<string, (Texture2D normal, Texture2D height, Texture2D occlusion)> _facadeRelief = new Dictionary<string, (Texture2D, Texture2D, Texture2D)>();

    /// <summary>Normal map, height map (for parallax) and ambient occlusion of a façade style: windows look set into the wall.</summary>
    static (Texture2D normal, Texture2D height, Texture2D occlusion) FacadeRelief(WindowStyle style, bool brick) =>
        Relief(style + (brick ? " brick" : ""), BayPx * FacadeBays, BayPx * FacadeBays, 3f / BayPx,
            (x, y) => FacadeHeight(x % BayPx * 256 / BayPx, y % BayPx * 256 / BayPx, style, brick, x / BayPx, y / BayPx));

    /// <summary>Relief maps from a height field in metres (width x height pixels, pixelM metres per pixel).</summary>
    static (Texture2D normal, Texture2D height, Texture2D occlusion) Relief(string key, int sizeX, int sizeY, float pixelM, Func<int, int, float> heightAt)
    {
        if (_facadeRelief.TryGetValue(key, out var cached) && cached.normal != null) return cached;
        int size = sizeX;   // row length
        var h = new float[sizeX * sizeY];
        for (int y = 0; y < sizeY; y++)
            for (int x = 0; x < sizeX; x++)
                h[y * size + x] = heightAt(x, y);
        // a little softening, so edges look bevelled rather than aliased
        var sm = new float[h.Length];
        for (int y = 0; y < sizeY; y++)
            for (int x = 0; x < size; x++)
            {
                float acc = 0f;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) acc += h[((y + dy + sizeY) % sizeY) * size + (x + dx + size) % size];
                sm[y * size + x] = acc / 9f;
            }
        // occlusion: how far a point lies below its surroundings (about 12 cm around): reveals and glass corners get less sky
        var wide = new float[h.Length]; var tmp = new float[h.Length]; const int rad = 6;
        for (int y = 0; y < sizeY; y++)
            for (int x = 0; x < size; x++)
            {
                float acc = 0f;
                for (int d = -rad; d <= rad; d++) acc += sm[y * size + (x + d + size) % size];
                tmp[y * size + x] = acc / (2 * rad + 1);
            }
        for (int y = 0; y < sizeY; y++)
            for (int x = 0; x < size; x++)
            {
                float acc = 0f;
                for (int d = -rad; d <= rad; d++) acc += tmp[((y + d + sizeY) % sizeY) * size + x];
                wide[y * size + x] = acc / (2 * rad + 1);
            }
        float min = sm.Min(), max = sm.Max(), range = Mathf.Max(0.001f, max - min);
        var normal = new Texture2D(sizeX, sizeY, TextureFormat.RGBA32, true, true) { name = "facade_normal_" + key, wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        var height = new Texture2D(sizeX, sizeY, TextureFormat.RGBA32, true, true) { name = "facade_height_" + key, wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        var occl = new Texture2D(sizeX, sizeY, TextureFormat.RGBA32, true, true) { name = "facade_occlusion_" + key, wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        var np = new Color32[h.Length]; var hp = new Color32[h.Length]; var op = new Color32[h.Length];
        for (int y = 0; y < sizeY; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (sm[y * size + (x + 1) % size] - sm[y * size + (x - 1 + size) % size]) / (2f * pixelM);
                float dy = (sm[((y + 1) % sizeY) * size + x] - sm[((y - 1 + sizeY) % sizeY) * size + x]) / (2f * pixelM);
                var n = new Vector3(-dx, -dy, 1f).normalized;
                np[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
                byte g = (byte)((sm[y * size + x] - min) / range * 255);
                hp[y * size + x] = new Color32(g, g, g, 255);
                float cavity = Mathf.Max(0f, wide[y * size + x] - sm[y * size + x]) / 0.1f;
                float depth = Mathf.Clamp01(-sm[y * size + x] / 0.14f);
                byte o = (byte)(Mathf.Clamp01(1f - 0.55f * Mathf.Clamp01(cavity) - 0.2f * depth) * 255);
                op[y * size + x] = new Color32(o, o, o, 255);
            }
        normal.SetPixels32(np); normal.Apply(true, false);
        height.SetPixels32(hp); height.Apply(true, false);
        occl.SetPixels32(op); occl.Apply(true, false);
        normal.Compress(true); height.Compress(false); occl.Compress(false);
        normal.Apply(false, true); height.Apply(false, true); occl.Apply(false, true);
        var r = (normal, height, occl);
        _facadeRelief[key] = r;
        return r;
    }

    // ------------------------------------------------------------------ ground floors

    /// <summary>Ground floor kinds: shops and cafés (shop windows, a fascia and a glass door), office lobbies
    /// (floor-to-ceiling glass), houses and flats (a front door between windows).</summary>
    public enum GroundStyle { Shop, Lobby, Door }
    public const float GroundFloorHeight = 3.2f;
    const int GroundBays = 4;   // a ground floor texture: 4 bays of 3 m, one storey of 3.2 m

    // classification of a ground floor pixel: 0 wall, 1 glass, 2 frame, 3 door, 4 fascia, 5 step/plinth
    static int GroundPart(float u, int bay, float v, GroundStyle style)
    {
        const float f = 0.02f;
        switch (style)
        {
            case GroundStyle.Shop:
                if (v > 0.84f && v < 0.97f) return 4;                                   // fascia above the windows
                if (v < 0.05f) return 5;
                if (bay == 1)                                                            // the shop door
                {
                    if (u > 0.32f && u < 0.68f && v < 0.78f) return Mathf.Abs(u - 0.5f) < f * 0.6f ? 2 : (v > 0.05f && u > 0.35f && u < 0.65f ? 3 : 2);
                }
                if (u > 0.04f && u < 0.96f && v > 0.08f && v < 0.8f)
                    return u < 0.07f || u > 0.93f || v < 0.11f || v > 0.77f ? 2 : 1;
                return 0;
            case GroundStyle.Lobby:
                if (v < 0.03f) return 5;
                if (u < 0.025f || u > 0.975f || Mathf.Abs(v - 0.88f) < 0.012f) return 2;
                if (bay == 2 && u > 0.3f && u < 0.7f && v < 0.8f) return Mathf.Abs(u - 0.5f) < 0.008f ? 2 : 3;
                return 1;
            default:
                if (v < 0.04f) return 5;
                if (bay % 2 == 0)                                                        // front door with a transom light
                {
                    if (u > 0.36f && u < 0.64f && v < 0.72f) return v > 0.6f ? (Mathf.Abs(v - 0.6f) < 0.012f ? 2 : 1) : (u < 0.385f || u > 0.615f ? 2 : 3);
                    if (u > 0.34f && u < 0.66f && v < 0.75f) return 2;
                }
                else if (u > 0.25f && u < 0.75f && v > 0.25f && v < 0.8f)
                    return u < 0.28f || u > 0.72f || v < 0.28f || v > 0.77f || Mathf.Abs(u - 0.5f) < f ? 2 : 1;
                return 0;
        }
    }

    static Texture2D GroundTexture(string name, Color wall, Color frame, bool brick, GroundStyle style, int seed)
    {
        var rng = new System.Random(seed);
        // shop fascias and doors in a few typical colours
        var fascias = new[] { new Color(0.12f, 0.16f, 0.22f), new Color(0.45f, 0.1f, 0.1f), new Color(0.1f, 0.25f, 0.18f), new Color(0.2f, 0.2f, 0.2f) };
        var doors = new[] { new Color(0.15f, 0.22f, 0.32f), new Color(0.32f, 0.12f, 0.1f), new Color(0.12f, 0.24f, 0.15f), new Color(0.3f, 0.22f, 0.14f) };
        var fascia = fascias[rng.Next(fascias.Length)]; var door = doors[rng.Next(doors.Length)];
        return TexRect(name, BayPx * GroundBays, BayPx, (x, y, r) =>
        {
            int bay = x / BayPx; float u = x % BayPx / (float)BayPx, v = y / (float)BayPx;
            switch (GroundPart(u, bay, v, style))
            {
                case 1: { float sky = 0.16f + 0.2f * v; return Noise(new Color(sky * 0.8f, sky * 0.9f, sky), r, 0.05f); }
                case 2: return style == GroundStyle.Door ? frame : new Color(0.18f, 0.19f, 0.2f);
                case 3: return style == GroundStyle.Door ? Noise(door, r, 0.08f) : Noise(new Color(0.16f, 0.2f, 0.24f), r, 0.05f);
                case 4: return Noise(fascia, r, 0.05f);
                case 5: return Noise(new Color(0.5f, 0.5f, 0.48f), r, 0.08f);
            }
            if (_photoWalls) return Noise(wall, r, 0.02f);
            if (brick)
            {
                int course = y / 6, offset = course % 2 == 0 ? 0 : 12;
                if (y % 6 == 0 || (x + offset) % 24 == 0) return Noise(new Color(0.72f, 0.7f, 0.66f), r, 0.1f);
                return Noise(wall, r, 0.18f) * (0.9f + 0.2f * ((course * 7 + (x + offset) / 24 * 13) % 10) / 10f);
            }
            return Noise(wall, r, 0.05f);
        });
    }

    static float GroundHeight(int x, int y, GroundStyle style, bool brick)
    {
        int bay = x / BayPx; float u = x % BayPx / (float)BayPx, v = y / (float)BayPx;
        switch (GroundPart(u, bay, v, style))
        {
            case 1: return style == GroundStyle.Lobby ? -0.03f : -0.12f;
            case 2: return style == GroundStyle.Lobby ? 0.02f : -0.05f;
            case 3: return -0.1f;
            case 4: return 0.06f;
            case 5: return 0.03f;
        }
        if (brick) { int course = y / 6, offset = course % 2 == 0 ? 0 : 12; if (y % 6 == 0 || (x + offset) % 24 == 0) return -0.008f; }
        return 0f;
    }

    /// <summary>Shop windows lit warm at night (lobbies cool white); doors and houses dark.</summary>
    static Texture2D GroundLights(string name, GroundStyle style) =>
        TexRect(name, BayPx * GroundBays, BayPx, (x, y, r) =>
        {
            int bay = x / BayPx; float u = x % BayPx / (float)BayPx, v = y / (float)BayPx;
            int part = GroundPart(u, bay, v, style);
            if (part != 1 && part != 3) return Color.black;
            // shops lit softly at night, one bay in four dark (closed, shutters down)
            if (style == GroundStyle.Shop)
            {
                // a lit shop seen through its window: warm light, brighter under the ceiling lights, darker rows of
                // shelves with goods of many colours; a third of the bays closed and dark
                if ((bay * 7 + 3) % 3 == 0) return Color.black;
                var light = new Color(1f, 0.86f, 0.66f) * (0.13f + 0.09f * v);
                float lamp = Mathf.Exp(-Mathf.Pow((u * 5f % 1f - 0.5f) * 3.2f, 2f)) * Mathf.Clamp01((v - 0.78f) * 6f);
                light += new Color(1f, 0.95f, 0.85f) * 0.3f * lamp;
                foreach (var (lo, hi) in new[] { (0.12f, 0.3f), (0.36f, 0.52f) })
                    if (v > lo && v < hi)
                    {
                        float k = r.NextDouble() < 0.5 ? 0.25f : 0.5f;
                        var goods = Color.HSVToRGB((float)((x / 6 * 0.137 + bay * 0.31) % 1.0), 0.45f, 1f);
                        light = Color.Lerp(light * k, goods * 0.12f, 0.45f);
                    }
                return light;
            }
            if (style == GroundStyle.Lobby) return bay == 1 ? Color.black : new Color(0.85f, 0.9f, 1f) * 0.3f;
            return part == 1 && bay % 2 == 0 ? new Color(1f, 0.8f, 0.55f) * 0.5f : Color.black;   // a light in the transom
        });

    static Texture2D TexRect(string name, int w, int h, Func<int, int, System.Random, Color> pixel)
    {
        if (_textures.TryGetValue(name, out var t) && t != null) return t;
        var rng = new System.Random(name.GetHashCode());
        t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = pixel(x, y, rng);
        t.SetPixels(px);
        t.Apply(true, true);
        _textures[name] = t;
        return t;
    }

    /// <summary>One window bay (3 m) by one floor (3 m), in 256 x 256 coordinates.</summary>
    static Color FacadeTile(int x, int y, System.Random r, Color wall, Color frame, bool brick, WindowStyle style, int bay = 0, int floor = 1)
    {
        float u = x / 256f, v = y / 256f;
        Color Glass(float vv) { float sky = 0.18f + 0.25f * vv; return Noise(new Color(sky * 0.8f, sky * 0.9f, sky), r, 0.05f); }
        const float f = 0.012f; // frame half-width
        switch (style)
        {
            case WindowStyle.Church:
                if (Lancet(u, v, bay, floor)) return Mathf.Abs(u - 0.5f) < f || (v * 10f % 1f) < 0.06f ? new Color(0.12f, 0.12f, 0.12f) : Glass(v) * 0.75f;
                if (Lancet(u, v, bay, floor, 0.025f)) return Noise(new Color(0.62f, 0.6f, 0.55f), r, 0.06f);   // stone surround
                if (floor % FacadeBays == 0 && v < 0.035f) return Noise(new Color(0.66f, 0.64f, 0.58f), r, 0.06f);
                break;
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
        if (_photoWalls) return Noise(wall, r, 0.02f);   // the photo detail map draws the bricks
        if (brick)
        {
            int course = y / 6, offset = course % 2 == 0 ? 0 : 12;
            if (y % 6 == 0 || (x + offset) % 24 == 0) return Noise(new Color(0.72f, 0.7f, 0.66f), r, 0.1f);
            return Noise(wall, r, 0.18f) * (0.9f + 0.2f * ((course * 7 + (x + offset) / 24 * 13) % 10) / 10f);
        }
        return Noise(wall, r, 0.05f);
    }

    /// <summary>A photo of the wall kind (MapAssets.WallDetail) as the material's detail map, where <paramref name="mask"/>'s alpha is 1;
    /// <paramref name="metresPerUv"/>: metres of wall per unit of the material's (tiled) texture coordinates.</summary>
    void WallPhoto(Material m, int kind, Texture2D mask, Vector2 metresPerUv)
    {
        if (kind >= _assets.WallDetail.Length || _assets.WallDetail[kind] == null) return;
        var size = _assets.WallDetailSize != null && kind < _assets.WallDetailSize.Length ? _assets.WallDetailSize[kind] : new Vector2(1.5f, 1.5f);
        m.SetTexture("_DetailMask", mask);
        m.SetTexture("_DetailAlbedoMap", _assets.WallDetail[kind]);
        m.SetFloat("_DetailAlbedoMapScale", 1f);
        m.SetTextureScale("_DetailAlbedoMap", new Vector2(metresPerUv.x / size.x, metresPerUv.y / size.y));
        if (_assets.WallNormal != null && kind < _assets.WallNormal.Length && _assets.WallNormal[kind] != null)
        {
            m.SetTexture("_DetailNormalMap", _assets.WallNormal[kind]);
            m.SetFloat("_DetailNormalMapScale", 1f);
        }
        m.EnableKeyword("_DETAIL_MULX2");
    }

    /// <summary>Where a façade texture is wall (alpha 1), not window, frame, sill or lintel.</summary>
    static Texture2D FacadeMask(WindowStyle style) =>
        Tex("facade_mask_" + style, BayPx * FacadeBays, (x, y, r) =>
            FacadeHeight(x % BayPx * 256 / BayPx, y % BayPx * 256 / BayPx, style, false, x / BayPx, y / BayPx) == 0f ? Color.white : new Color(1f, 1f, 1f, 0f));

    static Texture2D GroundMask(GroundStyle style) =>
        TexRect("ground_mask_" + style, BayPx * GroundBays, BayPx, (x, y, r) =>
            GroundPart(x % BayPx / (float)BayPx, x / BayPx, y / (float)BayPx, style) == 0 ? Color.white : new Color(1f, 1f, 1f, 0f));

    /// <summary>The window style of a building: from its construction year (3D BAG), height and OSM type.</summary>
    public static WindowStyle StyleFor(int year, float height, string type, uint seed)
    {
        bool office = type == "office" || type == "commercial" || type == "retail" || type == "hotel" || type == "university";
        // old buildings and churches keep traditional windows, however tall (church towers)
        if (type == "church" || type == "cathedral" || type == "chapel") return WindowStyle.Church;
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

        public void Append(MeshBuilder o)
        {
            int k = V.Count;
            V.AddRange(o.V); UV.AddRange(o.UV);
            foreach (var t in o.T) T.Add(t + k);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            UV.Add(ua); UV.Add(ub); UV.Add(uc);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        public void Strip(List<V2> left, List<V2> right, float y, float uvScale = 1f)
        {
            float along = 0;
            for (int i = 0; i < left.Count - 1; i++)
            {
                float seg = V2.Distance((left[i] + right[i]) / 2, (left[i + 1] + right[i + 1]) / 2);
                // (across in metres too: a texture keeps its proportions on wide and narrow strips)
                float w0 = V2.Distance(left[i], right[i]) / uvScale, w1 = V2.Distance(left[i + 1], right[i + 1]) / uvScale;
                Quad(P(left[i], y), P(left[i + 1], y), P(right[i + 1], y), P(right[i], y),
                    new Vector2(0, along / uvScale), new Vector2(0, (along + seg) / uvScale),
                    new Vector2(w1, (along + seg) / uvScale), new Vector2(w0, along / uvScale));
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

        /// <summary>A flat polygon. maxEdge &gt; 0: its triangles split until no edge is longer (on terrain, so the lift
        /// follows the ground inside large polygons, not only at their outline).</summary>
        public void Polygon(List<V2> poly, float y, float uvScale, float maxEdge = 0f)
        {
            if (maxEdge > 0f) poly = Densify(poly, maxEdge);
            var tris = Geometry2D.Triangulate(poly);
            var pts = new List<V2>(poly);
            if (maxEdge > 0f) Refine(pts, tris, maxEdge);
            int b = V.Count;
            foreach (var p in pts) { V.Add(P(p, y)); UV.Add(new Vector2(p.X / uvScale, p.Y / uvScale)); }
            for (int i = 0; i < tris.Count; i += 3) { T.Add(b + tris[i]); T.Add(b + tris[i + 2]); T.Add(b + tris[i + 1]); }
        }

        /// <summary>The outline with each edge in equal pieces of at most maxEdge: neighbours that share an edge (the
        /// topography's surfaces) then get the same points on it, so no cracks open between them on the terrain.</summary>
        public static List<V2> Densify(List<V2> poly, float maxEdge)
        {
            var dense = new List<V2>(poly.Count * 2);
            for (int i = 0; i < poly.Count; i++)
            {
                V2 a = poly[i], c = poly[(i + 1) % poly.Count];
                int k = Math.Max(1, (int)Math.Ceiling(V2.Distance(a, c) / maxEdge - 1e-3f));
                // from the lower end of the edge, so both neighbours compute the same points
                bool fwd = a.X < c.X || (a.X == c.X && a.Y <= c.Y);
                for (int j = 0; j < k; j++)
                    dense.Add(j == 0 ? a : fwd ? V2.Lerp(a, c, j / (float)k) : V2.Lerp(c, a, (k - j) / (float)k));
            }
            return dense;
        }

        /// <summary>A band along a closed outline (centred on it), with the texture in world space like
        /// <see cref="Polygon"/>: under the topography's surfaces it fills the slivers between neighbours that do not quite
        /// meet (their curves are stroked apart), where the ground would show through.</summary>
        public void OutlineBand(List<V2> poly, float width, float y, float uvScale, float maxEdge = 0f)
        {
            var ring = maxEdge > 0f ? Densify(poly, maxEdge) : new List<V2>(poly);
            ring.Add(ring[0]);
            var l = new List<V2>(); var r = new List<V2>();
            Geometry2D.Strip(ring, width, 0f, l, r);
            Vector2 U(V2 q) => new Vector2(q.X / uvScale, q.Y / uvScale);
            for (int i = 0; i < l.Count - 1; i++)
            {
                Quad(P(l[i], y), P(l[i + 1], y), P(r[i + 1], y), P(r[i], y), U(l[i]), U(l[i + 1]), U(r[i + 1]), U(r[i]));
                // both sides: the outline runs either way round
                Quad(P(r[i], y), P(r[i + 1], y), P(l[i + 1], y), P(l[i], y), U(r[i]), U(r[i + 1]), U(l[i + 1]), U(l[i]));
            }
        }

        /// <summary>Conforming refinement: in each pass, every triangle's longest edge longer than maxEdge is marked, and
        /// every triangle is split by the marked edges it has (into 2, 3 or 4), so neighbours share the new points (no cracks).</summary>
        static void Refine(List<V2> pts, List<int> tris, float maxEdge)
        {
            float max2 = maxEdge * maxEdge;
            for (int pass = 0; pass < 40; pass++)   // (a motorway's surface can be kilometres long: each pass halves the longest edges)
            {
                var marked = new Dictionary<long, int>();
                long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    float ab = V2.DistanceSquared(pts[a], pts[b]), bc = V2.DistanceSquared(pts[b], pts[c]), ca = V2.DistanceSquared(pts[c], pts[a]);
                    float m = Math.Max(ab, Math.Max(bc, ca));
                    if (m <= max2) continue;
                    long k = m == ab ? Key(a, b) : m == bc ? Key(b, c) : Key(c, a);
                    marked[k] = -1;
                }
                if (marked.Count == 0) return;
                int Mid(int a, int b)
                {
                    long k = Key(a, b);
                    if (!marked.TryGetValue(k, out var m)) return -1;
                    if (m < 0) { m = pts.Count; pts.Add((pts[a] + pts[b]) / 2f); marked[k] = m; }
                    return m;
                }
                var next = new List<int>(tris.Count * 2);
                void Add(int a, int b, int c) { next.Add(a); next.Add(b); next.Add(c); }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int mab = Mid(a, b), mbc = Mid(b, c), mca = Mid(c, a);
                    int n = (mab >= 0 ? 1 : 0) + (mbc >= 0 ? 1 : 0) + (mca >= 0 ? 1 : 0);
                    if (n == 0) { Add(a, b, c); continue; }
                    if (n == 3) { Add(a, mab, mca); Add(mab, b, mbc); Add(mca, mbc, c); Add(mab, mbc, mca); continue; }
                    // rotate so that the first split edge is a-b (and for two, the second b-c)
                    for (int r = 0; r < 3 && !(mab >= 0 && (n == 1 || mbc >= 0)); r++)
                    {
                        int t = a; a = b; b = c; c = t;
                        int tm = mab; mab = mbc; mbc = mca; mca = tm;
                    }
                    if (n == 1) { Add(a, mab, c); Add(mab, b, c); }
                    else { Add(mab, b, mbc); Add(a, mab, mbc); Add(a, mbc, c); }
                }
                tris.Clear(); tris.AddRange(next);
            }
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
            // (a vertex that is not a number would make a collider's raycasts loop for ever: set to the origin, and said)
            int bad = 0;
            for (int i = 0; i < V.Count; i++)
                if (!float.IsFinite(V[i].x) || !float.IsFinite(V[i].y) || !float.IsFinite(V[i].z)) { V[i] = Vector3.zero; bad++; }
            if (bad > 0) Debug.LogWarning($"OsmMap: {bad} vertices of '{name}' were not numbers");
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

    /// <summary>Junction corners rounded: each road is drawn as a strip along its line, so where two meet at an angle
    /// the corner between their edges was square, and a car turning from one into the other (keeping to its side, a
    /// narrower street) crossed bare ground there, as though over the pavement. Between each two neighbouring arms of a
    /// junction on the ground (meeting at 20-160°) the corner is filled with road out to an arc of 3 m radius tangent to
    /// both edges (a kerb's radius in town), so the carriageway's own surface carries the turn; where a road goes on
    /// narrower (160-200°), the step between their edges is tapered.</summary>
    void JunctionCorners(MeshBuilder roads, Dictionary<long, int> nodeUse)
    {
        var arms = new Dictionary<long, List<(V2 dir, float half)>>();
        foreach (var w in _data.Ways)
        {
            if (!CarRoads.Contains(w.Tag("highway") ?? "") || w.Tag("area") == "yes" || Layer(w) != 0 || w.Tag("tunnel") == "yes" || (w.Tag("bridge") ?? "no") != "no" || SurfaceOf(w) != RoadSurface.Asphalt) continue;
            var ids = w.NodeIds.Where(_pos.ContainsKey).ToList();
            float half = RoadWidth(w) / 2f;
            for (int k = 0; k < ids.Count; k++)
            {
                if (!nodeUse.TryGetValue(ids[k], out var uses) || uses < 2) continue;
                var at = _pos[ids[k]];
                if (!Inside(at)) continue;
                foreach (int n in new[] { k - 1, k + 1 })
                {
                    if (n < 0 || n >= ids.Count) continue;
                    var d = _pos[ids[n]] - at; if (d.LengthSquared() < 0.25f) continue;
                    if (!arms.TryGetValue(ids[k], out var l)) arms[ids[k]] = l = new List<(V2, float)>();
                    l.Add((V2.Normalize(d), half));
                }
            }
        }
        const float radius = 3f;
        int corners = 0;
        foreach (var (id, l) in arms)
        {
            if (l.Count < 2) continue;
            var node = _pos[id];
            var sorted = l.OrderBy(a => Math.Atan2(a.dir.Y, a.dir.X)).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var (da, ha) = sorted[i]; var (db, hb) = sorted[(i + 1) % sorted.Count];
                var poly = Geometry2D.CornerFillet(node, da, ha, db, hb, radius) ?? Geometry2D.WidthTaper(node, da, ha, db, hb);
                if (poly == null) continue;
                // (a fan from the corner, wound like the strips: facing up)
                for (int k = 1; k + 1 < poly.Count; k++)
                {
                    V2 p0 = poly[0], p1 = poly[k], p2 = poly[k + 1];
                    float cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
                    if (cross > 0) { var tmp = p1; p1 = p2; p2 = tmp; }
                    roads.Quad(P(p0, 0.02f), P(p1, 0.02f), P(p2, 0.02f), P(p2, 0.02f),
                        new Vector2(p0.X / 4f, p0.Y / 4f), new Vector2(p1.X / 4f, p1.Y / 4f), new Vector2(p2.X / 4f, p2.Y / 4f), new Vector2(p2.X / 4f, p2.Y / 4f));
                }
                corners++;
            }
        }
        if (corners > 0) Debug.Log($"OsmMap: {corners} junction corners rounded (3 m) or tapered (a road going on narrower)");
    }

    /// <summary>Triangles above which a mesh is split into pieces (by 400 m square): PhysX's mesh colliders of over 2
    /// million triangles can make a raycast loop for ever (Eindhoven's corridor made such meshes of its grass and
    /// pavements).</summary>
    const int MaxTrianglesPerMesh = 1000000;

    static GameObject Emit(Transform parent, string name, MeshBuilder mb, Material mat, bool collider)
    {
        // (buildings open where a street, cycle path or footway runs through them: OsmMapGenerator.Passages)
        if (_passages.Count > 0 && (name.StartsWith("Buildings ") || name == "Cornices")) mb = CutPassages(mb);
        return EmitMesh(parent, name, mb, mat, collider);
    }

    static GameObject EmitMesh(Transform parent, string name, MeshBuilder mb, Material mat, bool collider)
    {
        if (mb.V.Count == 0) return null;
        if (mb.T.Count / 3 > MaxTrianglesPerMesh)
        {
            // pieces of the same name, by the square their triangles' middles lie in
            var pieces = new Dictionary<long, MeshBuilder>();
            var map = new Dictionary<long, Dictionary<int, int>>();
            for (int t = 0; t + 2 < mb.T.Count; t += 3)
            {
                var c = (mb.V[mb.T[t]] + mb.V[mb.T[t + 1]] + mb.V[mb.T[t + 2]]) / 3f;
                long k = ((long)Mathf.FloorToInt(c.x / 400f) << 32) ^ (uint)Mathf.FloorToInt(c.z / 400f);
                if (!pieces.TryGetValue(k, out var piece)) { pieces[k] = piece = new MeshBuilder(); map[k] = new Dictionary<int, int>(); }
                var ix = map[k];
                for (int j = 0; j < 3; j++)
                {
                    int vi = mb.T[t + j];
                    if (!ix.TryGetValue(vi, out var ni)) { ni = piece.V.Count; piece.V.Add(mb.V[vi]); piece.UV.Add(mb.UV[vi]); ix[vi] = ni; }
                    piece.T.Add(ni);
                }
            }
            GameObject first = null;
            foreach (var piece in pieces.Values) { var pieceGo = EmitMesh(parent, name, piece, mat, collider); if (first == null) first = pieceGo; }
            return first;
        }
        var mesh = mb.Build(name);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.isStatic = true;
        _emitted.Add(go);
        return go;
    }

    // ------------------------------------------------------------------ OSM classification

    static readonly HashSet<string> CarRoads = new HashSet<string>
    {
        "motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link", "secondary", "secondary_link",
        "tertiary", "tertiary_link", "unclassified", "residential", "living_street", "service", "road",
    };

    static readonly HashSet<string> BarrierKinds = new HashSet<string> { "bollard", "block", "jersey_barrier", "planter", "chain", "bus_trap", "sump_buster", "full-height_turnstile" };

    /// <summary>A road with a barrier on it that stops cars (bollards, blocks, planters: OpenStreetMap's barrier nodes on
    /// the way, not open to motor vehicles): no through traffic (cars drove into a row of bollards across a street).</summary>
    bool Barred(OsmWay w)
    {
        foreach (var id in w.NodeIds)
        {
            if (!_data.Nodes.TryGetValue(id, out var n) || n.Tags.Count == 0) continue;
            var b = n.Tag("barrier");
            if (b == null || !BarrierKinds.Contains(b)) continue;
            var mv = n.Tag("motor_vehicle") ?? n.Tag("motorcar") ?? n.Tag("vehicle") ?? n.Tag("access");
            if (mv == "yes" || mv == "designated" || mv == "permissive") continue;
            return true;
        }
        return false;
    }

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
        // (a two-way street tagged as one lane is a single-track street that two cars pass on, not one lane's width: drawn
        // 3.1 m wide, a car keeping to its side of it hung 0.3 m over the pavement, as in a narrow London street)
        if (lanes == 1 && OneWayDir(w) == 0) return 4f;
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

    /// <summary>Cars park along this side of the road (-1 left, 1 right, as seen along the way) on the carriageway.</summary>
    static bool StreetParking(OsmWay w, int side)
    {
        var tag = w.Tag("parking:both") ?? w.Tag(side < 0 ? "parking:left" : "parking:right") ?? w.Tag("parking:lane:both") ?? w.Tag(side < 0 ? "parking:lane:left" : "parking:lane:right");
        return !(tag == null || tag == "no" || tag == "separate" || tag == "no_parking" || tag == "no_stopping" || tag == "fire_lane");
    }

    static bool IsOneWay(OsmWay w) => OneWayDir(w) != 0;
    static bool IsCycleway(OsmWay w) => w.Tag("highway") == "cycleway" || (w.Tag("highway") == "path" && w.Tag("bicycle") == "designated");
    static bool IsFootway(OsmWay w)
    {
        var hw = w.Tag("highway");
        return hw == "footway" || hw == "pedestrian" || hw == "path" || hw == "corridor";
    }
    // (a building passage is at street level: its layer, often -1, only says it runs under the building)
    static int Layer(OsmWay w) => w.Tag("tunnel") == "building_passage" ? 0 : int.TryParse(w.Tag("layer"), out var l) ? l : 0;

    List<V2> Line(OsmWay w)
    {
        var line = w.NodeIds.Where(_pos.ContainsKey).Select(id => _pos[id]).ToList();
        if (Terrain == null || line.Count < 2) return line;
        // on terrain: a point every 8 m at most, so streets and walls follow the slope between OSM's nodes
        var dense = new List<V2>(line.Count * 2) { line[0] };
        for (int i = 1; i < line.Count; i++)
        {
            float d = V2.Distance(line[i - 1], line[i]);
            int n = (int)(d / 8f);
            for (int k = 1; k <= n; k++) dense.Add(V2.Lerp(line[i - 1], line[i], k / (n + 1f)));
            dense.Add(line[i]);
        }
        return dense;
    }
    bool Inside(V2 p) => Area != null ? Area.Contains(p, 30f) : Math.Abs(p.X) < HalfSize + 30 && Math.Abs(p.Y) < HalfSize + 30;

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

    readonly List<MeshBuilder> _grassMeshes = new List<MeshBuilder>();

    public void Build(Transform root)
    {
        var world = new GameObject("World").transform;
        world.SetParent(root, false);
        _emitted.Clear();
        NightMaterials.Clear();
        _photoWalls = !_noPhotoWalls && !_noRelief && _assets != null && _assets.FacadeTemplate != null && _assets.WallDetail != null && _assets.WallDetail.Length >= 5;
        try { Terrain = TerrainHeight.Load(_map.Name, _proj); }
        catch (Exception e) { Debug.LogWarning("TerrainHeight: " + e.Message); Terrain = null; }

        var asphalt = Mat("Asphalt", Color.white, 0.2f, Tex("asphalt", 256, (x, y, r) => Noise(new Color(0.23f, 0.23f, 0.24f), r, 0.35f)));
        // road surfaces from OpenStreetMap's surface tag: brick paving (Dutch "klinkers"), cobblestones, gravel
        var bricks = Mat("Brick paving", Color.white, 0.12f, Tex("bricks", 256, (x, y, r) =>
        {
            int row = y / 16, off = row % 2 == 0 ? 0 : 16;
            if (y % 16 < 1 || (x + off) % 32 < 1) return Noise(new Color(0.3f, 0.29f, 0.27f), r, 0.2f);
            float tone = 0.85f + 0.3f * (((x + off) / 32 * 7 + row * 13) % 10) / 10f;
            return Noise(new Color(0.42f, 0.22f, 0.17f) * tone, r, 0.18f);
        }));
        var cobbles = Mat("Cobblestones", Color.white, 0.18f, Tex("cobbles", 256, (x, y, r) =>
        {
            int cx = x / 21, cy = y / 18;
            float u = (x % 21) / 21f - 0.5f, v = (y % 18) / 18f - 0.5f;
            if (u * u + v * v > 0.2f) return Noise(new Color(0.2f, 0.19f, 0.18f), r, 0.3f);
            float tone = 0.75f + 0.5f * ((cx * 31 + cy * 17) % 10) / 10f;
            return Noise(new Color(0.45f, 0.44f, 0.42f) * tone * (1f - (u * u + v * v)), r, 0.15f);
        }));
        var gravel = Mat("Gravel", Color.white, 0.05f, Tex("gravel", 256, (x, y, r) => Noise(new Color(0.55f, 0.5f, 0.42f), r, 0.6f)));
        var red = Mat("Cycle path", Color.white, 0.2f, Tex("red_asphalt", 256, (x, y, r) => Noise(new Color(0.55f, 0.22f, 0.19f), r, 0.3f)));
        var pavingTex = Tex("paving", 256, (x, y, r) => (x % 64 < 2 || y % 64 < 2) ? new Color(0.45f, 0.44f, 0.42f) : Noise(new Color(0.68f, 0.66f, 0.62f), r, 0.12f));
        var paving = Mat("Pavement", Color.white, 0.15f, pavingTex);
        var kerb = Mat("Kerb", new Color(0.62f, 0.61f, 0.58f), 0.2f);
        var marking = Mat("Road marking", new Color(0.92f, 0.92f, 0.9f), 0.35f);
        var grass = Mat("Grass", Color.white, 0.05f, Tex("grass", 256, (x, y, r) => Noise(new Color(0.28f, 0.42f, 0.2f), r, 0.4f)));
        var water = Mat("Water", new Color(0.12f, 0.2f, 0.22f), 0.95f);
        var roof = Mat("Roof", new Color(0.27f, 0.27f, 0.28f), 0.2f);
        // the topography's parking bays and asphalt footways: their own surfaces
        var parking = Mat("Parking paving", Color.white, 0.12f, bricks.GetTexture("_BaseMap") as Texture2D);
        var footAsphalt = Mat("Asphalt footway", Color.white, 0.2f, asphalt.GetTexture("_BaseMap") as Texture2D);
        // photo textures of the real surfaces (Poly Haven, CC0) and the lawn, each at its real size (the meshes' UVs: 4 m
        // for asphalt, cycle paths and grass, 1.5 m for paving, bricks, cobbles and gravel); -noPhotoTextures: the drawn ones
        if (!_noPhotoWalls && !_noRelief && _assets != null && _assets.FacadeTemplate != null && _assets.GroundNames != null)
        {
            // (tints, in linear light: each photo's mean colour brought to that of the real surface in daylight photos)
            GroundPhoto(asphalt, "asphalt", 4f, new Color(0.78f, 0.78f, 0.85f)); GroundPhoto(footAsphalt, "asphalt_light", 4f, new Color(0.55f, 0.55f, 0.56f));
            GroundPhoto(red, "cycle_red", 4f, Color.white);
            GroundPhoto(bricks, "bricks", 1.5f, new Color(1.45f, 1.0f, 1.0f)); GroundPhoto(parking, "parking", 1.5f, new Color(1.75f, 1.8f, 1.9f));
            GroundPhoto(paving, "paving", 1.5f, new Color(1.0f, 1.1f, 1.25f));
            MacroVariation(paving, 1.8f); MacroVariation(footAsphalt, 4f); MacroVariation(grass, 2f, 17f); MacroVariation(gravel, 2f, 15f);
            GroundPhoto(cobbles, "cobbles", 1.5f, new Color(0.56f, 0.56f, 0.58f)); GroundPhoto(gravel, "gravel", 1.5f, new Color(0.68f, 0.66f, 0.62f));
            GroundPhoto(grass, "lawn", 4f, new Color(1.05f, 0.95f, 1.1f));
        }
        // façades: wall kinds (red, dark and yellow brick, plaster, concrete) × window styles, made when first used
        var wallKinds = new (string name, Color wall, Color frame, bool brick)[]
        {
            ("red brick", _photoWalls ? new Color(0.6f, 0.38f, 0.29f) : new Color(0.52f, 0.23f, 0.17f), new Color(0.93f, 0.93f, 0.9f), true),
            ("dark brick", _photoWalls ? new Color(0.5f, 0.32f, 0.26f) : new Color(0.33f, 0.18f, 0.14f), new Color(0.9f, 0.9f, 0.88f), true),
            ("yellow brick", _photoWalls ? new Color(0.72f, 0.58f, 0.42f) : new Color(0.7f, 0.58f, 0.38f), new Color(0.15f, 0.25f, 0.2f), true),
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
            var m = Mat($"Facade {name} {style}", Color.white, style == WindowStyle.Glass ? 0.55f : 0.1f,
                Facade($"facade_{name.Replace(' ', '_')}_{style}", wall, frame, brick, style));
            m.mainTextureScale = Vector2.one / FacadeBays;
            // depth: windows set into the wall, sills and mortar (normal map, parallax); the template keeps the shader variant in builds
            if (_assets != null && _assets.FacadeTemplate != null && !_noRelief)
            {
                var (nrm, hgt, occ) = FacadeRelief(style, brick && !_photoWalls);
                m.shader = _assets.FacadeTemplate.shader;
                m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_ParallaxMap", hgt); m.SetFloat("_Parallax", 0.03f); m.EnableKeyword("_PARALLAXMAP");
                m.SetTexture("_OcclusionMap", occ); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
                // the photo of the wall up close, not on windows, frames and sills; 12 m of wall per façade texture
                if (_photoWalls && style != WindowStyle.Glass) WallPhoto(m, k / styles, FacadeMask(style), new Vector2(12f, 12f));
            }
            m.SetTexture("_EmissionMap", FacadeLights($"facade_lights_{name.Replace(' ', '_')}_{style}", style));
            m.SetColor("_EmissionColor", Color.black);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            NightMaterials.Add(m);
            return facades[k] = m;
        };
        // ground floors: wall kind × ground style (shop, lobby, door)
        int groundStyles = Enum.GetValues(typeof(GroundStyle)).Length;
        var grounds = new Material[wallKinds.Length * groundStyles];
        _groundMat = k =>
        {
            if (grounds[k] != null) return grounds[k];
            var (name, wall, frame, brick) = wallKinds[k / groundStyles];
            var gs = (GroundStyle)(k % groundStyles);
            var tag = $"{name.Replace(' ', '_')}_{gs}";
            var m = Mat($"Ground floor {name} {gs}", Color.white, gs == GroundStyle.Door ? 0.1f : 0.4f, GroundTexture("ground_" + tag, wall, frame, brick, gs, k));
            m.mainTextureScale = new Vector2(1f / GroundBays, 1f);
            if (_assets != null && _assets.FacadeTemplate != null && !_noRelief)
            {
                bool mortar = brick && !_photoWalls;
                var (nrm, hgt, occ) = Relief("ground " + gs + (mortar ? " brick" : ""), BayPx * GroundBays, BayPx, 3f / BayPx, (x, y) => GroundHeight(x, y, gs, mortar));
                m.shader = _assets.FacadeTemplate.shader;
                m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_ParallaxMap", hgt); m.SetFloat("_Parallax", 0.02f); m.EnableKeyword("_PARALLAXMAP");
                m.SetTexture("_OcclusionMap", occ); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
                if (_photoWalls) WallPhoto(m, k / groundStyles, GroundMask(gs), new Vector2(3f * GroundBays, GroundFloorHeight));
            }
            m.SetTexture("_EmissionMap", GroundLights("ground_lights_" + gs, gs));
            m.SetColor("_EmissionColor", Color.black);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            NightMaterials.Add(m);
            return grounds[k] = m;
        };
        // brick-heavy palettes for the Netherlands, Belgium and the UK; more plaster/concrete elsewhere
        bool brickCountry = _map.Country == "Netherlands" || _map.Country == "Belgium" || _map.Country == "United Kingdom";
        int[] palette = brickCountry ? new[] { 0, 0, 1, 1, 2, 3 } : new[] { 3, 3, 4, 4, 2, 0 };

        // the national large-scale topography's surfaces (the Netherlands: BGT), drawn further down; known first, as with
        // them the roads' lines are not drawn and the ground between the surfaces is not the aerial photo
        var surfaces = MapExtras.LoadSurfaces(_map.Name, _proj, out var surfaceCredit);
        TopographyGround = surfaces != null && surfaces.Count > 100;
        PavementHeight = TopographyGround ? 0.012f : 0.14f;
        // the roads' profiles on the terrain: without humps (the elevation model's gaps under viaducts are filled from the
        // embankments beside them) or steps
        if (Terrain != null)
        {
            var profiles = new List<(List<Vector2>, float)>();
            var bridges = new List<(List<Vector2>, float)>();
            foreach (var w in _data.Ways)
            {
                if (w.Tag("highway") == null || w.Tag("area") == "yes") continue;
                var rl = Line(w);
                if (rl.Count < 2) continue;
                if ((w.Tag("bridge") ?? "no") != "no") { bridges.Add((rl.Select(q => new Vector2(q.X, q.Y)).ToList(), RoadWidth(w))); continue; }
                if (!CarRoads.Contains(w.Tag("highway")) || Layer(w) != 0 || w.Tag("tunnel") == "yes") continue;
                profiles.Add((rl.Select(q => new Vector2(q.X, q.Y)).ToList(), RoadWidth(w)));
            }
            // (not at bridges: their ramps rise on purpose, and the decks run between their ends' ground)
            Terrain.SmoothRoads(profiles, bridges);
            // (on a flat map, its roads' profiles smoothed, the ground's 2.5 m cells do not rise through the streets: the
            // base ground 3 cm under them, not 15 cm; in Ghent the open paving between the streets lay 20 cm under them,
            // and a pavement's piece stood over it like a slab with a shadow under it)
            if (Area == null && Terrain.Max - Terrain.Min < 12f) Terrain.GroundDrop = 0.03f;
            // (a map with corridors: TerrainHeight.Apply keeps the ground under the nearest road's lower edge, so its 10 m
            // tiles do not cut through a motorway's surface on an embankment or in a cutting)
        }

        // water (canals, rivers, ponds, harbours): its surface lies WaterDepth below the streets, between quay walls; the
        // ground has holes for it
        var waterPolys = new List<List<V2>>();
        foreach (var w in _data.Ways.Where(w => w.IsClosed))
        {
            var lu = w.Tag("landuse"); var nat = w.Tag("natural");
            if (!(nat == "water" || lu == "basin" || w.Tag("waterway") == "riverbank")) continue;
            var poly = Line(w);
            if (poly.Count < 4 || !poly.Any(Inside)) continue;
            poly.RemoveAt(poly.Count - 1);
            if (Math.Abs(Geometry2D.SignedArea(poly)) < 4f) continue;
            waterPolys.Add(poly);
        }
        var waterIndex = new WaterIndex(waterPolys);

        float h = HalfSize + 40;
        var ground = new MeshBuilder();
        // in tiles of at most 100 m: the physics engine warns about (and handles badly) triangles over 500 m
        // (2.5 m on terrain, so the ground follows it as closely as the roads do, and 3 cm lower: with 10 m cells the
        // ground's straight edges stood up through the asphalt in triangles where the slope curved, as in Montreal)
        int tiles = Mathf.CeilToInt(2 * h / (Terrain != null || waterPolys.Count > 0 ? 10f : 100f));
        float step = 2 * h / tiles, gy = Terrain != null ? -0.03f : 0f;
        Vector2 Uv(float x, float z) => new Vector2((x + h) / 4f, (z + h) / 4f);
        void GroundQuad(float x0, float z0, float x1, float z1)
        {
            // (5 m on a map with corridors: kilometres of them in 2.5 m cells ran out of memory)
            int n = Terrain != null ? Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(x1 - x0, z1 - z0) / (Area != null ? 5f : 2.5f))) : 1;
            float dx = (x1 - x0) / n, dz = (z1 - z0) / n;
            for (int a = 0; a < n; a++)
                for (int b = 0; b < n; b++)
                {
                    float qx0 = x0 + a * dx, qx1 = qx0 + dx, qz0 = z0 + b * dz, qz1 = qz0 + dz;
                    ground.Quad(new Vector3(qx0, gy, qz0), new Vector3(qx0, gy, qz1), new Vector3(qx1, gy, qz1), new Vector3(qx1, gy, qz0), Uv(qx0, qz0), Uv(qx0, qz1), Uv(qx1, qz1), Uv(qx1, qz0));
                }
        }
        for (int i = 0; i < tiles; i++)
            for (int j = 0; j < tiles; j++)
            {
                float x0 = -h + i * step, x1 = x0 + step, z0 = -h + j * step, z1 = z0 + step;
                // (a map with corridors: only the tiles in its area, and 40 m around it)
                if (Area != null && !Area.Touches(new V2(x0 + step / 2, z0 + step / 2), step / 2 + 40f)) continue;
                if (waterPolys.Count == 0) { GroundQuad(x0, z0, x1, z1); continue; }
                var local = waterIndex.EdgesIn(x0, z0, x1, z1);
                var centre = new V2((x0 + x1) / 2, (z0 + z1) / 2);
                bool centreWet = waterIndex.Inside(centre);
                if (local.Count == 0) { if (!centreWet) GroundQuad(x0, z0, x1, z1); continue; }
                // a shore runs through the tile: 1 m cells, each left out where a corner is in the water (the quay's
                // coping covers the jagged edge)
                int k = Mathf.CeilToInt(step);
                float cs = step / k;
                var wet = new bool[(k + 1) * (k + 1)];
                for (int a = 0; a <= k; a++)
                    for (int b = 0; b <= k; b++)
                        wet[a * (k + 1) + b] = waterIndex.Inside(new V2(x0 + a * cs, z0 + b * cs));
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++)
                    {
                        if (wet[a * (k + 1) + b] || wet[(a + 1) * (k + 1) + b] || wet[a * (k + 1) + b + 1] || wet[(a + 1) * (k + 1) + b + 1]) continue;
                        GroundQuad(x0 + a * cs, z0 + b * cs, x0 + (a + 1) * cs, z0 + (b + 1) * cs);
                    }
            }
        // a map with corridors: fields beyond its area (3 km around it, a little lower, without colliders), so the view
        // from a highway ends in land rather than in the void
        if (Area != null)
        {
            var beyond = new MeshBuilder();
            const float tile = 200f, reach = 3000f;
            float bx0 = Area.MinX - reach, bz0 = Area.MinZ - reach, bx1 = Area.MaxX + reach, bz1 = Area.MaxZ + reach;
            for (float x = bx0; x < bx1; x += tile)
                for (float z = bz0; z < bz1; z += tile)
                {
                    // not in the area (the map's own ground): whole tiles well away from it, else in cells of 20 m along its
                    // edge (with the terrain, a big tile's flat face would rise through the roads)
                    float d = Area.Depth(new V2(x + tile / 2, z + tile / 2));
                    if (d >= tile * 0.71f) continue;
                    float cs = d < -tile * 0.71f ? tile : 20f;
                    for (float cx = x; cx < x + tile - 0.1f; cx += cs)
                        for (float cz = z; cz < z + tile - 0.1f; cz += cs)
                        {
                            if (cs < tile && Area.Depth(new V2(cx + cs / 2, cz + cs / 2)) >= cs * 0.71f) continue;
                            beyond.Quad(new Vector3(cx, -0.35f, cz), new Vector3(cx, -0.35f, cz + cs), new Vector3(cx + cs, -0.35f, cz + cs), new Vector3(cx + cs, -0.35f, cz),
                                new Vector2(cx, cz) / 4f, new Vector2(cx, cz + cs) / 4f, new Vector2(cx + cs, cz + cs) / 4f, new Vector2(cx + cs, cz) / 4f);
                        }
                }
            Emit(world, "Ground beyond the map", beyond, grass, false);
        }
        // the real aerial photo where the map has one (Tools/aerial_photo.py): car parks, yards and
        // squares as they are; else paving
        Aerial = AerialPhoto.Load(_map.Name ?? "");
        if (Aerial != null && TopographyGround)
        {
            // the topography's surfaces cover streets, yards and squares: what is left between them (embankments, verges,
            // under buildings) is grass, not the aerial photo (its parked cars and shadows look smeared at eye level)
            Emit(world, "Ground grass", ground, grass, true);
            EmitSurroundings(world);
        }
        else if (Aerial != null)
        {
            // a map with corridors reaches beyond the photo: the tiles there are grass (the photo's edge pixels would
            // smear over kilometres)
            if (Area != null)
            {
                var photo = new MeshBuilder(); var beyondPhoto = new MeshBuilder();
                for (int i = 0; i + 3 < ground.V.Count; i += 4)
                {
                    var c = (ground.V[i] + ground.V[i + 2]) / 2f;
                    var to = Aerial.Covers(c) ? photo : beyondPhoto;
                    to.Quad(ground.V[i], ground.V[i + 1], ground.V[i + 2], ground.V[i + 3], ground.UV[i], ground.UV[i + 1], ground.UV[i + 2], ground.UV[i + 3]);
                }
                ground = photo;
                if (beyondPhoto.V.Count > 0) Emit(world, "Ground grass", beyondPhoto, grass, true);
            }
            if (Array.IndexOf(CommandLine.Args, "-photoGround") >= 0)
            {
                // (the photo itself on the ground, as before: -photoGround)
                for (int i = 0; i < ground.V.Count; i++) ground.UV[i] = Aerial.Uv(ground.V[i]);
                var groundMat = Mat("Ground aerial photo", Color.white, 0.08f, Aerial.Texture);
                if (_assets != null && _assets.DetailTemplate != null)
                {
                    // fine grain every 2 m over the photo (0.2-0.3 m per pixel), so it is not blurry up close
                    groundMat.shader = _assets.DetailTemplate.shader;
                    groundMat.CopyPropertiesFromMaterial(_assets.DetailTemplate);
                    groundMat.SetTexture("_BaseMap", Aerial.GroundTexture != null ? Aerial.GroundTexture : Aerial.Texture);
                    groundMat.SetColor("_BaseColor", Color.white);
                    groundMat.SetFloat("_Smoothness", 0.08f);
                    groundMat.EnableKeyword("_DETAIL_MULX2");
                    // up close: real paving (the photo of concrete tiles in grey, Resources/paving_detail), 1.8 m a tile; the
                    // aerial photo gives the colour
                    var pavingDetail = _noPhotoWalls ? null : Resources.Load<Texture2D>("paving_detail");
                    groundMat.SetTexture("_DetailAlbedoMap", pavingDetail != null ? pavingDetail : Tex("ground_detail", 256, (x, y, r) =>
                    {
                        float v = 0.5f + ((float)r.NextDouble() - 0.5f) * 0.16f;
                        if (r.NextDouble() < 0.004) v -= 0.12f;   // grit
                        return new Color(v, v, v);
                    }));
                    groundMat.SetTextureScale("_DetailAlbedoMap", Vector2.one * (pavingDetail != null ? 2f * Aerial.HalfSize / 1.8f : Aerial.HalfSize));
                    groundMat.SetFloat("_DetailAlbedoMapScale", 1f);
                }
                Emit(world, "Ground", ground, groundMat, true);
            }
            else
            {
                // the ground as what it is, read from the photo (AerialPhoto.Classes, about half a metre a cell): paving
                // everywhere, the lawns and verges as lawn and bare earth as gravelly soil over it, edges along the
                // classes' borders (the photo itself, blurred, with its cars and shadows, looked smeared at eye level)
                var paved = Mat("Ground", Color.white, 0.12f, pavingTex);
                if (!_noPhotoWalls && !_noRelief && _assets != null && _assets.FacadeTemplate != null && _assets.GroundNames != null)
                { GroundPhoto(paved, "paving", 4f, new Color(0.95f, 1.03f, 1.15f)); MacroVariation(paved, 1.8f); }
                Emit(world, "Ground", ground, paved, true);
                if (Aerial.Classes != null)
                {
                    TreeCrownsOffLawns();
                    var lawn = ClassOverlay(AerialPhoto.Green, 0.004f, 4f);
                    var soil = ClassOverlay(AerialPhoto.Soil, 0.003f, 1.5f);
                    if (lawn.V.Count > 0) { Emit(world, "Ground lawn", lawn, grass, true); _grassMeshes.Add(lawn); }
                    if (soil.V.Count > 0) Emit(world, "Ground soil", soil, gravel, true);
                    Debug.Log($"OsmMap: ground from the aerial photo: {lawn.T.Count / 3} lawn and {soil.T.Count / 3} soil triangles over the paving");
                }
            }
            EmitSurroundings(world);
        }
        else if (!_data.Ways.Any(w => w.Tag("building") != null))
            // a road network without buildings (OpenDRIVE, SUMO): fields between the roads, not paving
            Emit(world, "Ground grass", ground, grass, true);
        else
        {
            var paved = Mat("Ground", Color.white, 0.12f, pavingTex);
            if (!_noPhotoWalls && !_noRelief && _assets != null && _assets.FacadeTemplate != null && _assets.GroundNames != null)
                GroundPhoto(paved, "paving", 4f, new Color(0.95f, 1.03f, 1.15f));
            Emit(world, "Ground", ground, paved, true);
        }

        var green = new MeshBuilder();
        var waterMb = new MeshBuilder();
        foreach (var w in _data.Ways.Where(w => w.IsClosed))
        {
            var lu = w.Tag("landuse"); var le = w.Tag("leisure"); var nat = w.Tag("natural");
            bool isGreen = lu == "grass" || lu == "meadow" || lu == "village_green" || lu == "recreation_ground" || le == "park" || le == "garden" || nat == "grassland" || nat == "scrub" || nat == "wood";
            if (!isGreen) continue;
            var poly = Line(w);
            if (poly.Count < 4 || !poly.Any(Inside)) continue;
            poly.RemoveAt(poly.Count - 1);
            green.Polygon(poly, 0.006f, 4f, Terrain != null ? 8f : 0f);   // just above the ground, under the paths and roads through parks
        }
        Emit(world, "Green areas", green, grass, true);
        _grassMeshes.Add(green);
        var quays = new MeshBuilder(); var copings = new MeshBuilder(); var edges = new MeshBuilder();
        // where a way (road, path, bridge) crosses the water's edge (onto a bridge or a ford), by 4 m cell: the quay's
        // invisible kerb has a gap there
        var crossings = new Dictionary<(int, int), List<V2>>();
        if (waterPolys.Count > 0)
            foreach (var w in _data.Ways)
            {
                if (w.Tag("highway") == null) continue;
                var wl = Line(w);
                for (int i = 1; i < wl.Count; i++)
                {
                    V2 a = wl[i - 1], b = wl[i];
                    foreach (var (c, d) in waterIndex.EdgesIn(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)))
                    {
                        float d1 = Geometry2D.Cross(d - c, a - c), d2 = Geometry2D.Cross(d - c, b - c);
                        if ((d1 > 0) == (d2 > 0)) continue;
                        float d3 = Geometry2D.Cross(b - a, c - a), d4 = Geometry2D.Cross(b - a, d - a);
                        if ((d3 > 0) == (d4 > 0)) continue;
                        var x = a + (b - a) * (d1 / (d1 - d2));
                        var key = ((int)Math.Floor(x.X / 4f), (int)Math.Floor(x.Y / 4f));
                        if (!crossings.TryGetValue(key, out var l)) crossings[key] = l = new List<V2>();
                        l.Add(x);
                    }
                }
            }
        foreach (var poly in waterPolys)
        {
            waterMb.Polygon(poly, -WaterDepth, 10f, Terrain != null ? 8f : 0f);
            Quay(poly, quays, copings, edges, crossings, waterIndex);
        }
        var waterGo = Emit(world, "Water", waterMb, WaterMaterial(water), true);
        if (waterGo != null && waterGo.GetComponent<MeshRenderer>().sharedMaterial.shader.name == "CoupledSim/Water") waterGo.AddComponent<PlanarWaterReflection>();
        if (waterGo != null) waterGo.AddComponent<WaterRipples>();
        Emit(world, "Quay walls", quays, Mat("Quay wall", Color.white, 0.1f, Tex("quay_bricks", 256, (x, y, r) =>
        {
            int row = y / 12, off = row % 2 == 0 ? 0 : 16;
            if (y % 12 < 1 || (x + off) % 32 < 1) return Noise(new Color(0.28f, 0.27f, 0.25f), r, 0.2f);
            float tone = 0.8f + 0.3f * (((x + off) / 32 * 7 + row * 13) % 10) / 10f;
            // darker and greener towards the water line (the bottom of the texture)
            float damp = Mathf.Clamp01(1f - y / 90f);
            return Noise(Color.Lerp(new Color(0.42f, 0.24f, 0.18f) * tone, new Color(0.16f, 0.17f, 0.12f), damp * 0.8f), r, 0.18f);
        })), true);
        Emit(world, "Quay copings", copings, kerb, true);
        // (an invisible kerb along the quays: vehicles and people that stray do not fall into the water)
        var edgeGo = Emit(world, "Quay edges", edges, kerb, true);
        if (edgeGo != null) edgeGo.GetComponent<MeshRenderer>().enabled = false;
        // the national large-scale topography (the Netherlands: BGT) as the ground's surfaces: every carriageway,
        // footpath, cycle path, parking bay, verge and green area at its exact outline, just under the generated
        // streets (which stay on top); the names give the tyres their grip (bricks, paving, grass, gravel)
        if (surfaces != null)
        {
            var kinds = new Dictionary<string, (string name, Material mat)>
            {
                ["road"] = ("BGT roads", asphalt), ["road_bricks"] = ("BGT roads bricks", bricks), ["footway"] = ("BGT pavements", paving),
                ["footway_asphalt"] = ("BGT pavements asphalt", footAsphalt), ["parking"] = ("BGT parking bricks", parking), ["cycleway"] = ("BGT cycle paths", red),
                ["grass"] = ("BGT green areas", grass), ["gravel"] = ("BGT gravel", gravel),
            };
            var builders = kinds.Keys.ToDictionary(k => k, _ => new MeshBuilder());
            // the ground-level polygons by 20 m cell (their bounds): a deck over a canal in a culvert, or over nothing
            // the topography draws, has no surface under it (the Waterslootsebrug in Delft: a strip of the ground's grass
            // across the street)
            var groundCells = new Dictionary<(int, int), List<(List<Vector2> ring, Vector2 min, Vector2 max)>>();
            foreach (var (tagged, ring) in surfaces)
            {
                if (tagged.IndexOf('@') >= 0 || ring.Count < 3) continue;
                Vector2 mn = ring[0], mx = ring[0];
                foreach (var q in ring) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
                for (int cx = Mathf.FloorToInt(mn.x / 20f); cx <= Mathf.FloorToInt(mx.x / 20f); cx++)
                    for (int cz = Mathf.FloorToInt(mn.y / 20f); cz <= Mathf.FloorToInt(mx.y / 20f); cz++)
                    {
                        if (!groundCells.TryGetValue((cx, cz), out var l)) groundCells[(cx, cz)] = l = new List<(List<Vector2>, Vector2, Vector2)>();
                        l.Add((ring, mn, mx));
                    }
            }
            bool GroundSurfaceAt(Vector2 q)
            {
                if (!groundCells.TryGetValue((Mathf.FloorToInt(q.x / 20f), Mathf.FloorToInt(q.y / 20f)), out var l)) return false;
                foreach (var (ring, mn, mx) in l)
                {
                    if (q.x < mn.x || q.y < mn.y || q.x > mx.x || q.y > mx.y) continue;
                    bool inside = false;
                    for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                        if ((ring[i].y > q.y) != (ring[j].y > q.y) && q.x < (ring[j].x - ring[i].x) * (q.y - ring[i].y) / (ring[j].y - ring[i].y) + ring[i].x) inside = !inside;
                    if (inside) return true;
                }
                return false;
            }
            int decksOverNothing = 0;
            foreach (var (tagged, ring) in surfaces)
            {
                // "kind@n": n levels above the ground (bridge decks): kept over water only (a canal bridge's deck; decks over
                // roads and railways are the generated bridges'), a little above the ground's surfaces
                int at = tagged.IndexOf('@');
                var kind = at < 0 ? tagged : tagged.Substring(0, at);
                if (!builders.TryGetValue(kind, out var mb) || ring.Count < 3) continue;
                var poly = ring.Select(q => new V2(q.x, q.y)).ToList();
                if (!poly.Any(Inside)) continue;
                if (at >= 0)
                {
                    var c = new V2(poly.Average(q => q.X), poly.Average(q => q.Y));
                    if (!waterIndex.Inside(c) && !poly.Any(waterIndex.Inside))
                    {
                        // (over a drawn road or railway: the generated bridge's deck; over no surface: this deck)
                        if (GroundSurfaceAt(new Vector2(c.X, c.Y))) continue;
                        decksOverNothing++;
                    }
                }
                float uvs = kind == "grass" ? 4f : kind == "road" || kind == "footway_asphalt" ? 4f : kind == "cycleway" ? 4f : 1.5f;
                // green and gravel at the ground's level never over a canal or pond (the topography's verges and banks
                // reach over the water's edge; at the street's height they hung over the water as slabs)
                bool offWater = at < 0 && (kind == "grass" || kind == "gravel") && poly.Any(waterIndex.Inside);
                var into = offWater ? new MeshBuilder() : mb;
                into.Polygon(poly, at >= 0 ? 0.016f : 0.012f, uvs, Terrain != null ? 8f : 0f);
                // a 16 cm band under its outline: neighbours' curves are stroked separately, and the slivers between
                // them (a few cm) showed the grass underneath as cracks across the road
                if (at < 0) into.OutlineBand(poly, 0.16f, 0.009f, uvs, Terrain != null ? 8f : 0f);
                if (offWater) AppendOffWater(mb, into, waterIndex);
            }
            foreach (var kv in kinds) if (builders[kv.Key].V.Count > 0) Emit(world, kv.Value.name, builders[kv.Key], kv.Value.mat, true);
            _grassMeshes.Add(builders["grass"]);
            RealDataCredits.Add($"{surfaces.Count} surfaces: {surfaceCredit}");
            if (decksOverNothing > 0) Debug.Log($"OsmMap: {decksOverNothing} topography decks kept over no ground surface (culverts)");
        }

        // [0]: on the ground, [1]: on bridges above other roads (their own meshes: the terrain lifts them, and only them, straight between the bridge's ends)
        var roadsAll = new Dictionary<int, MeshBuilder>();
        var brickRoadsAll = new Dictionary<int, MeshBuilder>();
        var cobbleRoadsAll = new Dictionary<int, MeshBuilder>();
        var gravelRoadsAll = new Dictionary<int, MeshBuilder>();
        var cyclesAll = new Dictionary<int, MeshBuilder>();
        var cycleLanesAll = new Dictionary<int, MeshBuilder>();   // painted on the road: drawn on every map
        var walksAll = new Dictionary<int, MeshBuilder>();
        var kerbsAll = new Dictionary<int, MeshBuilder>();
        var marksAll = new Dictionary<int, MeshBuilder>();
        MeshBuilder roads, brickRoads, cobbleRoads, gravelRoads, cycles, walks, kerbs, marks;
        var lamps = new List<(Vector3 p, float yaw)>();
        var left = new List<V2>(); var right = new List<V2>();
        var roadWays = new List<(OsmWay way, List<V2> line, float width)>();
        var guardRails = new MeshBuilder();   // steel barriers in the central reservation of motorways
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
        // the car roads on the ground, by 20 m cell: footpaths that OSM draws straight across a road (without
        // footway=crossing) get no paving where they lie on it
        var roadCells = _roadCells = new Dictionary<(int, int), List<(V2 a, V2 b, float half)>>();
        foreach (var w in _data.Ways)
        {
            if (!CarRoads.Contains(w.Tag("highway") ?? "") || w.Tag("area") == "yes" || Layer(w) != 0 || w.Tag("tunnel") == "yes" || (w.Tag("bridge") ?? "no") != "no") continue;
            var rl = Line(w); float half = RoadWidth(w) / 2f;
            for (int i = 1; i < rl.Count; i++)
            {
                var a = rl[i - 1]; var b = rl[i];
                int x0 = (int)Math.Floor(Math.Min(a.X, b.X) / 20f) - 1, x1 = (int)Math.Floor(Math.Max(a.X, b.X) / 20f) + 1;
                int z0 = (int)Math.Floor(Math.Min(a.Y, b.Y) / 20f) - 1, z1 = (int)Math.Floor(Math.Max(a.Y, b.Y) / 20f) + 1;
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                {
                    if (!roadCells.TryGetValue((x, z), out var l)) roadCells[(x, z)] = l = new List<(V2, V2, float)>();
                    l.Add((a, b, half));
                }
            }
        }
        // a footpath point on a road it crosses (well inside the carriageway, at more than 40 degrees to it; for a
        // pedestrian street, 4 m wide, anywhere on the carriageway: its paving stops at the road's edge)
        bool AcrossRoad(V2 p, V2 dir, bool wide = false)
        {
            if (!roadCells.TryGetValue(((int)Math.Floor(p.X / 20f), (int)Math.Floor(p.Y / 20f)), out var l)) return false;
            foreach (var (a, b, half) in l)
            {
                var ab = b - a; float len2 = ab.LengthSquared(); if (len2 < 1e-4f) continue;
                float u = Math.Max(0f, Math.Min(1f, V2.Dot(p - a, ab) / len2));
                if (V2.Distance(p, a + ab * u) > (wide ? half : half * 0.8f)) continue;
                float cos = Math.Abs(V2.Dot(V2.Normalize(ab), dir));
                if (cos < 0.766f) return true;
            }
            return false;
        }
        // the parts of a footpath off the roads it crosses (points every metre)
        List<List<V2>> OffRoads(List<V2> l, bool wide = false)
        {
            var parts = new List<List<V2>>(); var cur = new List<V2>();
            for (int i = 1; i < l.Count; i++)
            {
                var a = l[i - 1]; var b = l[i]; float d = V2.Distance(a, b); if (d < 1e-3f) continue;
                var dir = (b - a) / d;
                int n = Math.Max(1, (int)Math.Ceiling(d));
                for (int k = (i == 1 ? 0 : 1); k <= n; k++)
                {
                    var q = V2.Lerp(a, b, k / (float)n);
                    if (AcrossRoad(q, dir, wide)) { if (cur.Count >= 2) parts.Add(cur); cur = new List<V2>(); }
                    else cur.Add(q);
                }
            }
            if (cur.Count >= 2) parts.Add(cur);
            return parts;
        }
        foreach (var w in _data.Ways)
        {
            var hw = w.Tag("highway");
            if (hw == null || w.Tag("area") == "yes" || Layer(w) < 0 || w.Tag("tunnel") == "yes") continue;
            var line = Line(w);
            if (line.Count < 2 || !line.Any(Inside)) continue;
            float lift = Math.Max(0, Layer(w)) * 0.02f;
            var bridgeTag = w.Tag("bridge");
            bool onBridge = bridgeTag != null && bridgeTag != "no";
            bool elevated = onBridge && Layer(w) >= 1;
            if (Terrain != null && onBridge)
                Terrain.AddBridge(line.Select(q => new Vector2(q.X, q.Y)).ToList(), RoadWidth(w), elevated ? Math.Min(9, Math.Max(1, Layer(w))) : 0);
            // 0: on the ground; else the bridge's layer: each layer its own meshes, lifted only by its own bridges
            int bi = elevated && Terrain != null ? Math.Min(9, Layer(w)) : 0;
            MeshBuilder Get(Dictionary<int, MeshBuilder> d) => d.TryGetValue(bi, out var m) ? m : d[bi] = new MeshBuilder();
            roads = Get(roadsAll); brickRoads = Get(brickRoadsAll); cobbleRoads = Get(cobbleRoadsAll); gravelRoads = Get(gravelRoadsAll);
            cycles = Get(cyclesAll); var cycleLanes = Get(cycleLanesAll); walks = Get(walksAll); kerbs = Get(kerbsAll); marks = Get(marksAll);
            int pavementsBefore = Pavements.Count, cyclesBefore = CyclePaths.Count;
            if (CarRoads.Contains(hw))
            {
                float width = RoadWidth(w);
                roadWays.Add((w, line, width));
                var access = w.Tag("motor_vehicle") ?? w.Tag("vehicle") ?? w.Tag("access");
                // oneway=-1: the line runs the way the traffic goes (its parking sides swap with it)
                int owDir = OneWayDir(w);
                var roadLine = line.Select(p => P(p, 0.02f + lift)).ToList();
                if (owDir < 0) roadLine.Reverse();
                bool parkL = StreetParking(w, -1), parkR = StreetParking(w, 1);
                var ms = w.Tag("maxspeed");
                float limit = SpeedNumber(ms) * (ms != null && ms.Contains("mph") ? 1.609f : 1f);
                Roads.Add(new Road { Line = roadLine, Width = width, Type = hw, OneWay = owDir != 0, Bridge = bi > 0, BridgeLayer = bi,
                    SpeedLimitKmh = limit > 0f ? limit : RoadSpeedLimits.Default(hw), Lanes = int.TryParse(w.Tag("lanes"), out var nLanes) ? nLanes : 0,
                    ParkLeft = owDir < 0 ? parkR : parkL, ParkRight = owDir < 0 ? parkL : parkR,
                    NoThroughTraffic = access == "no" || access == "private" || (hw == "service" && (access == "customers" || access == "delivery" || w.Tag("service") == "driveway" || w.Tag("service") == "parking_aisle" || w.Tag("service") == "alley")) || Barred(w) });
                // a dual carriageway's motorway: a steel guard rail along its inner edge (the central reservation), 0.45-0.8 m
                // high, no collider (the lanes keep traffic off it)
                if (hw == "motorway" && owDir != 0 && bi == 0 && roadLine.Count >= 2)
                {
                    var travel = roadLine.Select(q => new V2(q.x, q.z)).ToList();
                    var rl = new List<V2>(); var rr = new List<V2>();
                    float inner = (width / 2f + 0.6f) * (_map.LeftHandTraffic ? 1f : -1f);
                    Geometry2D.Strip(travel, 0.06f, inner, rl, rr);
                    for (int k = 0; k + 1 < rl.Count; k++)
                        foreach (var e in new[] { rl, rr })
                        {
                            V2 a = e[k], b = e[k + 1];
                            guardRails.Quad(P(a, 0.45f), P(a, 0.8f), P(b, 0.8f), P(b, 0.45f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                            guardRails.Quad(P(b, 0.45f), P(b, 0.8f), P(a, 0.8f), P(a, 0.45f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                        }
                    // posts every 4 m
                    float railAt = 0f, nextPost = 0f;
                    for (int k = 0; k + 1 < rl.Count; k++)
                    {
                        V2 c0 = (rl[k] + rr[k]) / 2f, c1 = (rl[k + 1] + rr[k + 1]) / 2f; float seg = V2.Distance(c0, c1);
                        while (nextPost <= railAt + seg && seg > 1e-3f)
                        {
                            var c = V2.Lerp(c0, c1, (nextPost - railAt) / seg);
                            guardRails.Box(P(c, 0f), new Vector3(0.1f, 0.75f, 0.1f), 0f);
                            nextPost += 4f;
                        }
                        railAt += seg;
                    }
                }
                Geometry2D.Strip(line, width, 0, left, right);
                var surface = SurfaceOf(w);
                (surface == RoadSurface.Bricks ? brickRoads : surface == RoadSurface.Cobblestones ? cobbleRoads : surface == RoadSurface.Gravel ? gravelRoads : roads)
                    .Strip(left, right, 0.02f + lift, surface == RoadSurface.Asphalt ? 4f : 1.5f);
                RoadDetails(line, width, 0.02f + lift, w.Id, surface);
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
                        LaneMarkings(w, piece, width, 0.035f + lift, marks, cycleLanes, to < total - 0.01f, from > 0.01f);
                    var sw = w.Tag("sidewalk") ?? w.Tag("sidewalk:both") ?? (hw == "residential" || hw == "tertiary" || hw == "secondary" || hw == "primary" || hw == "unclassified" ? "both" : "no");
                    // street lamps every 25 m at the kerb (also where the pavements are mapped as ways of their own, as in
                    // New York and Sydney: those streets were dark at night)
                    if (sw != "no" && sw != "none")
                    {
                        float len = Geometry2D.Length(piece);
                        // (on streets wider than 10 m on both sides in turn, every 20 m; else one side, every 25 m)
                        bool both = width > 10f; int k = 0;
                        for (float d = 6f; d < len - 4f; d += both ? 20f : 25f, k++)
                        {
                            var (pt, dir) = Geometry2D.PointAt(piece, d);
                            var n = new V2(dir.Y, -dir.X) * (both && k % 2 == 1 ? -1f : 1f);
                            var lp = pt + n * (width / 2 + 0.4f);
                            if (Inside(lp)) lamps.Add((P(lp, PavementHeight), Mathf.Atan2(-n.X, -n.Y) * Mathf.Rad2Deg));
                        }
                    }
                    if (sw == "no" || sw == "none" || sw == "separate") continue;
                    // (not a leftover under 5 m between two junctions' cut-outs: a 3 m piece stood raised in the middle of the
                    // open paving at a junction in Ghent, like a slab dropped on the street)
                    if (Geometry2D.Length(piece) < 5f) continue;
                    bool untagged = w.Tag("sidewalk") == null && w.Tag("sidewalk:both") == null;
                    var (mid, midDir) = Geometry2D.PointAt(piece, Geometry2D.Length(piece) / 2f);
                    foreach (var side in new[] { -1, 1 })
                    {
                        if ((sw == "left" && side > 0) || (sw == "right" && side < 0)) continue;
                        // (one carriageway of a dual carriageway, untagged: no pavement on the side where the other one lies
                        // within 4 m beyond it, the central reservation; people walked there between the two streams)
                        if (untagged && OneWayDir(w) != 0 && InCarriageway(mid + new V2(midDir.Y, -midDir.X) * side * (width / 2 + 4f), 0f, 0f, out _)) continue;
                        Geometry2D.Strip(piece, 2.0f, side * (width / 2 + 1.0f), left, right);
                        if (bi == 0) OffCarriageways(left, right);
                        walks.Strip(left, right, PavementHeight, 1.5f);
                        kerbs.Wall(side < 0 ? right : left, 0.02f, PavementHeight, side < 0);
                        // kerb at both ends of the piece too, and its back edge down into the ground (which lies under the
                        // streets: without it the pavement looked like a slab floating over the ground beside it)
                        kerbs.Wall(new List<V2> { left[0], right[0] }, -0.45f, PavementHeight, side > 0);
                        kerbs.Wall(new List<V2> { left[left.Count - 1], right[right.Count - 1] }, -0.45f, PavementHeight, side < 0);
                        kerbs.Wall(side < 0 ? left : right, -0.45f, PavementHeight, side > 0);
                        var centre = new List<V2>(); var dummy = new List<V2>();
                        Geometry2D.Strip(piece, 0f, side * (width / 2 + 1.0f), centre, dummy);
                        // (the walking line off the other carriageways, as the paving is: on a dual carriageway the inner
                        // pavement's line ran down the other carriageway, and people walked on it in Ho Chi Minh City)
                        foreach (var part in bi == 0 ? WalkingLineOffCarriageways(centre, false) : new List<List<V2>> { centre })
                            Pavements.Add(part.Select(p => P(p, PavementHeight)).ToList());
                    }
                }
            }
            else if (IsCycleway(w))
            {
                Geometry2D.Strip(line, ParseMetres(w.Tag("width"), 2.2f), 0, left, right);
                if (bi == 0 && lift == 0f) OffCarriageways(left, right);
                cycles.Strip(left, right, 0.04f + lift, 4f);
                CyclePaths.Add(line.Select(p => P(p, 0.04f + lift)).ToList());
            }
            else if (IsFootway(w))
            {
                // a crossing (footway=crossing: the footpath over a road, drawn by OSM between the pavements) is the
                // road's zebra or unmarked crossing, not a raised strip of paving across the carriageway: the AI
                // pedestrians still walk along it
                bool crossing = w.Tag("footway") == "crossing" || w.Tag("path") == "crossing" || w.Tag("crossing") != null && hw != "pedestrian";
                if (!crossing)
                    // (a pedestrian street too: Maiden Lane in London began on Bedford Street's middle, its paving over half
                    // of that street's carriageway, and a car entering it drove over the paving)
                    foreach (var part in bi == 0 && lift == 0f ? OffRoads(line, hw == "pedestrian") : new List<List<V2>> { line })
                    {
                        Geometry2D.Strip(part, hw == "pedestrian" ? 4f : 1.8f, 0, left, right);
                        if (bi == 0 && lift == 0f) OffCarriageways(left, right);
                        walks.Strip(left, right, 0.05f + lift, 1.5f);
                    }
                if (crossing) { Pavements.Add(line.Select(p => P(p, 0.05f + lift)).ToList()); CrossingLines.Add(Pavements[Pavements.Count - 1]); }
                else
                    // (a footpath mapped beside a road, overlapping it: its walking line off the carriageway where it runs
                    // along it; where it crosses a road, people still walk over)
                    foreach (var part in bi == 0 && lift == 0f ? WalkingLineOffCarriageways(line, true) : new List<List<V2>> { line })
                        Pavements.Add(part.Select(p => P(p, 0.05f + lift)).ToList());
            }
            if (bi > 0)
            {
                for (int k = pavementsBefore; k < Pavements.Count; k++) BridgeLines[Pavements[k]] = bi;
                for (int k = cyclesBefore; k < CyclePaths.Count; k++) BridgeLines[CyclePaths[k]] = bi;
            }
        }
        if (PavementPointsMoved > 0) Debug.Log($"OsmMap: {PavementPointsMoved} points of pavements, footpaths and cycle paths moved off the carriageways to their edge");
        JunctionCorners(roadsAll.TryGetValue(0, out var groundRoads) ? groundRoads : roadsAll[0] = new MeshBuilder(), nodeUse);
        foreach (var bi in roadsAll.Keys.Union(cyclesAll.Keys).Union(walksAll.Keys).Union(cycleLanesAll.Keys).OrderBy(k => k).ToList())
        {
            var sfx = bi > 0 ? " bridge L" + bi : "";
            GameObject E(string n, Dictionary<int, MeshBuilder> d, Material m, bool c) => d.TryGetValue(bi, out var mb) ? Emit(world, n + sfx, mb, m, c) : null;
            // on the ground of a map with the national topography's surfaces (TopographyGround): the roads only as invisible
            // driving surfaces, no generated cycle paths, pavements and kerbs (bridges above other roads keep theirs: the
            // topography's surfaces lie on the ground)
            bool topo = TopographyGround && bi == 0;
            void Hidden(GameObject go) { if (go != null && topo) go.GetComponent<MeshRenderer>().enabled = false; }
            Hidden(E("Roads", roadsAll, asphalt, true));
            // the collider names tell the car physics the surface (CoupledSim.Vehicles.RoadSurfaces)
            Hidden(E("Roads bricks", brickRoadsAll, bricks, true));
            Hidden(E("Roads cobblestones", cobbleRoadsAll, cobbles, true));
            Hidden(E("Roads gravel", gravelRoadsAll, gravel, true));
            if (!topo)
            {
                // (the drawn road is often wider than OpenStreetMap's width, which OffCarriageways kept the strips off:
                // what still lies on it moves off it now that it is built)
                if (bi == 0)
                {
                    Physics.SyncTransforms();
                    if (walksAll.TryGetValue(0, out var w0)) OffDrawnRoads(w0);
                    if (cyclesAll.TryGetValue(0, out var c0)) OffDrawnRoads(c0);
                }
                E("Cycle paths", cyclesAll, red, true);
                E("Pavements", walksAll, paving, true);
                E("Kerbs", kerbsAll, kerb, true);
            }
            E("Cycle lanes", cycleLanesAll, red, true);
            E("Road markings", marksAll, marking, false);
        }
        EmitRoadDetails(world);
        if (guardRails.V.Count > 0) Emit(world, "Guard rails", guardRails, Mat("Guard rail steel", new Color(0.62f, 0.64f, 0.66f), 0.55f), false);

        // zebra crossings at OSM crossing nodes on car roads
        var zebras = new MeshBuilder();
        var drawnCrossings = new List<V2>();
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
                // one set of markings per place: crossings mapped on both carriageways or on several ways of one junction
                // (a scramble crossing) would otherwise lay stripes over each other at different angles
                if (drawnCrossings.Any(q => V2.DistanceSquared(q, p) < 5f * 5f)) marked = false;
                else if (marked) drawnCrossings.Add(p);
                // signalised crossings without zebra stripes (as in the Netherlands): two dashed lines
                bool signalDashes = marked && (c == "traffic_signals" || node.Tag("crossing:signals") == "yes") && cr != "zebra" && c != "zebra";
                if (signalDashes)
                {
                    foreach (var along in new[] { -2f, 2f })
                        for (float o = -width / 2 + 0.25f; o < width / 2 - 0.3f; o += 1.0f)
                        {
                            var c0 = p + nrm * o + dir * (along - 0.15f); var c1 = p + nrm * o + dir * (along + 0.15f);
                            var c2 = p + nrm * (o + 0.5f) + dir * (along + 0.15f); var c3 = p + nrm * (o + 0.5f) + dir * (along - 0.15f);
                            zebras.Quad(P(c0, 0.04f), P(c1, 0.04f), P(c2, 0.04f), P(c3, 0.04f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                        }
                }
                else if (marked)
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
        PlaceTrafficSignals(root, roadWays, marking);

        LoadLandmarks();   // (before the passages: a landmark line can give a passage its real width and clearance)
        FindPassages();
        // buildings: real 3D models where available (3D BAG, the Netherlands), else OSM footprints extruded
        var buildingMbs = facades.Select(_ => new MeshBuilder()).ToArray();
        var groundMbs = grounds.Select(_ => new MeshBuilder()).ToArray();
        var cornices = new MeshBuilder();
        // shops, cafés and other businesses (OSM points): the buildings they are in get shop fronts
        var shopPoints = _data.Nodes.Values.Where(n => (n.Tag("shop") != null && n.Tag("shop") != "vacant") || ShopAmenities.Contains(n.Tag("amenity") ?? ""))
            .Select(n => _pos.TryGetValue(n.Id, out var q) ? q : new V2(float.NaN, 0)).Where(q => !float.IsNaN(q.X)).ToList();
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
            AddRealBuildings(bag, buildingMbs, palette, roofs, tileRoofs, groundMbs, shopPoints, cornices);
        }
        // OpenStreetMap's buildings where the file of real 3D buildings has none (a map's corridors reach beyond it)
        HashSet<long> realCells = null;
        if (bag != null)
        {
            realCells = new HashSet<long>();
            foreach (var b in bag) foreach (var v in b.V) realCells.Add(((long)Mathf.FloorToInt(v.x / 30f) << 32) ^ (uint)Mathf.FloorToInt(v.z / 30f));
        }
        bool RealHere(V2 q) => realCells.Contains(((long)Mathf.FloorToInt(q.X / 30f) << 32) ^ (uint)Mathf.FloorToInt(q.Y / 30f));
        foreach (var w in bag != null && Area == null ? Enumerable.Empty<OsmWay>() : _data.Ways.Where(w => w.IsClosed && w.Tag("building") != null))
        {
            var poly = Line(w);
            if (poly.Count < 4 || !poly.Any(Inside)) continue;
            if (realCells != null && poly.Any(RealHere)) continue;
            poly.RemoveAt(poly.Count - 1);
            float height = BuildingHeight(w);
            float minHeight = ParseMetres(w.Tag("min_height"), 0f);
            if (Geometry2D.SignedArea(poly) < 0) poly.Reverse();
            var style = StyleFor(0, height, w.Tag("building"), (uint)(Math.Abs(w.Id) * 2654435761L));
            int kindO = palette[(int)(Math.Abs(w.Id) % palette.Length)];
            var mb = buildingMbs[kindO * Styles + (int)style];
            var gsBase = GroundStyleFor(style, w.Tag("building"));
            float split = minHeight < 0.5f && height > GroundFloorHeight + 1.5f ? GroundFloorHeight : minHeight;
            float u = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                float len = V2.Distance(a, b);
                var gmb = groundMbs[kindO * GroundStyleCount + (int)(NearShop((a + b) / 2f, shopPoints) ? GroundStyle.Shop : gsBase)];
                // facing out of the building (the polygon is counter-clockwise: the outside is to the right of a->b)
                if (split > minHeight)
                    gmb.Quad(P(a, minHeight), P(a, split), P(b, split), P(b, minHeight),
                        new Vector2(u / 3f, 0f), new Vector2(u / 3f, 1f), new Vector2((u + len) / 3f, 1f), new Vector2((u + len) / 3f, 0f));
                mb.Quad(P(a, split), P(a, height), P(b, height), P(b, split),
                    new Vector2(u / 3f, split / 3f), new Vector2(u / 3f, height / 3f),
                    new Vector2((u + len) / 3f, height / 3f), new Vector2((u + len) / 3f, split / 3f));
                u += len;
            }
            if (height > 6f) Cornice(cornices, poly, height);
            roofs.Polygon(poly, height, 5f);
            BuildingCount++;
        }
        var buildings = new GameObject("Buildings").transform;
        buildings.SetParent(world, false);
        for (int i = 0; i < facades.Length; i++)
            if (buildingMbs[i].V.Count > 0) { var m = _facadeMat(i); Emit(buildings, "Buildings " + m.name, buildingMbs[i], m, true); }
        for (int i = 0; i < grounds.Length; i++)
            if (groundMbs[i].V.Count > 0) { var m = _groundMat(i); Emit(buildings, "Buildings " + m.name, groundMbs[i], m, true); }
        Emit(buildings, "Cornices", cornices, Mat("Cornice", new Color(0.72f, 0.7f, 0.66f), 0.15f), false);
        if (_realColumns.V.Count > 0)
        {
            Emit(buildings, "Building columns (3D BAG)", _realColumns, Mat("Building columns", new Color(0.78f, 0.77f, 0.74f), 0.1f), true);
            Debug.Log($"OsmMap: {RealColumns} real buildings are columns: plain concrete");
        }
        EmitLandmarkFacades(buildings);
        EmitPassages(buildings, bag);
        EmitLetters(buildings, bag);
        EmitLandmarkDetails(buildings);
        if (Aerial != null)
        {
            // roofs in the photo's colours (tiles, gravel, solar panels, dormers); roofs over 35 m keep
            // their materials, as the photo shows tall buildings leaning away from the camera
            var photoRoofs = new MeshBuilder();
            var steep = new[] { new MeshBuilder(), new MeshBuilder() };
            roofs = AerialSplit(roofs, photoRoofs, steep);
            for (int i = 0; i < tileRoofs.Length; i++) tileRoofs[i] = AerialSplit(tileRoofs[i], photoRoofs, steep);
            for (int i = 0; i < tileRoofs.Length; i++) { tileRoofs[i].Append(steep[i]); tileRoofs[i].Append(_realTileRoofs[i]); }
            // (with a fine grain of gravel and bitumen over the photo, 1.2 m across: seen close from a drone, the photo's
            // pixels were a blur)
            var photoMat = Mat("Roof aerial photo", Color.white, 0.15f, Aerial.Texture);
            photoMat.EnableKeyword("_DETAIL_MULX2");
            photoMat.SetTexture("_DetailAlbedoMap", RoofGrain());
            photoMat.SetFloat("_DetailAlbedoMapScale", 1f);
            float reps = 2f * Aerial.HalfSize / 1.2f;
            photoMat.SetTextureScale("_DetailAlbedoMap", new Vector2(reps, reps));
            Emit(buildings, "Roofs aerial photo", photoRoofs, photoMat, true);
        }
        // real buildings' flat roofs, by their roofing
        if (_realFlatRoofs.Any(m => m.V.Count > 0))
        {
            var grainTex = Tex("roof_bitumen", 256, (x, y, r) => Noise(new Color(0.22f, 0.22f, 0.23f), r, 0.25f));
            var flatMats = new[]
            {
                Mat("Roof bitumen", Color.white, 0.25f, grainTex),
                Mat("Roof gravel", Color.white, 0.05f, Tex("roof_gravel", 256, (x, y, r) => Noise(new Color(0.55f, 0.54f, 0.5f), r, 0.5f))),
                Mat("Roof membrane", new Color(0.66f, 0.66f, 0.64f), 0.3f, Tex("roof_membrane", 256, (x, y, r) => Noise(new Color(0.95f, 0.95f, 0.94f), r, 0.06f))),
                Mat("Roof sedum", Color.white, 0.05f, Tex("roof_sedum", 256, (x, y, r) => Noise(r.NextDouble() < 0.15 ? new Color(0.45f, 0.3f, 0.2f) : new Color(0.33f, 0.42f, 0.2f), r, 0.5f))),
            };
            if (!_noPhotoWalls && !_noRelief && _assets != null && _assets.FacadeTemplate != null && _assets.GroundNames != null)
            {
                GroundPhoto(flatMats[1], "gravel", 2.5f, new Color(0.75f, 0.75f, 0.75f));
                GroundPhoto(flatMats[3], "lawn", 2.5f, new Color(0.8f, 0.9f, 0.6f));
            }
            for (int k = 0; k < 4; k++) Emit(buildings, "Roofs " + flatMats[k].name, _realFlatRoofs[k], flatMats[k], true);
            Debug.Log($"OsmMap: real buildings' flat roofs: {RealFlatRoofs[0]} bitumen, {RealFlatRoofs[1]} gravel, {RealFlatRoofs[2]} light membrane, {RealFlatRoofs[3]} green");
        }
        // roofs collide too: drones land on them and measure their height above them, planners see the buildings' tops
        Emit(buildings, "Roofs", roofs, roof, true);
        EmitSkyline(buildings);
        EmitFarCity(world);
        for (int i = 0; i < tileRoofs.Length; i++) Emit(buildings, "Roofs " + tileMats[i].name, tileRoofs[i], tileMats[i], true);
        // buildings cast shadows from both sides of their faces: the seams between walls, roofs and cornices let no
        // sunlight through into the shade of the street (it showed as bright slivers on the pavement)
        foreach (var r in buildings.GetComponentsInChildren<MeshRenderer>())
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

        PlaceStreetFurniture(root);

        // street lamps (EnvironmentController lights them at dusk) and mapped trees
        var props = new GameObject("Street furniture").transform;
        props.SetParent(root, false);
        // the city's own register of street lights, where there is one: every real pole, at its real height,
        // turned to the nearest road; else one every 25 m along streets with pavements
        var realLamps = MapExtras.Load(_map.Name, "lamps", _proj, out var lampCredit);
        // a pole that the road's width (estimated from OpenStreetMap) puts in the carriageway stands at its edge, as
        // trees do (traffic cars held by poles stopped and the cars behind them ran into them)
        int lampsMoved = 0;
        V2 PoleOffCarriageway(V2 q)
        {
            if (!InCarriageway(q, 0.3f, 0.6f, out var edge)) return q;
            lampsMoved++;
            return edge;
        }
        if (realLamps != null && _assets != null && _assets.StreetLamp != null)
        {
            float lampHeight = PrefabHeight(_assets.StreetLamp, 6f);
            int n = 0;
            foreach (var r in realLamps)
            {
                var p = new V2(r.X, r.Z);
                if (!Inside(p)) continue;
                p = PoleOffCarriageway(p);
                var (near, dist) = NearestRoad(p, roadWays);
                float yaw = dist < 40f ? Mathf.Atan2(near.X - p.X, near.Y - p.Y) * Mathf.Rad2Deg : 0f;
                var go = UnityEngine.Object.Instantiate(_assets.StreetLamp, P(p, dist < 12f ? PavementHeight : 0.05f), Quaternion.Euler(0, yaw, 0), props);
                go.name = "StreetLamp (register)";
                CullWhenSmall(go, 0.008f);
                go.transform.localScale *= Mathf.Clamp(r.Float("height", lampHeight) / lampHeight, 0.4f, 2.2f);
                n++;
            }
            RealDataCredits.Add($"{n} street lights: {lampCredit}");
            lamps.Clear();
        }
        if (_assets != null && _assets.StreetLamp != null)
            foreach (var (p0, yaw) in lamps)
            {
                var q = PoleOffCarriageway(new V2(p0.x, p0.z));
                var p = new Vector3(q.X, p0.y, q.Y);
                var go = UnityEngine.Object.Instantiate(_assets.StreetLamp, p, Quaternion.Euler(0, yaw, 0), props);
                go.name = "StreetLamp";
                CullWhenSmall(go, 0.008f);
            }
        if (lampsMoved > 0) Debug.Log($"OsmMap: {lampsMoved} street lamps moved out of the carriageway to its edge");
        Debug.Log($"OsmMap: {lamps.Count} street lamps along the streets (every 25 m), {lamps.Count(l => new Vector2(l.p.x, l.p.z).magnitude < 150f)} within 150 m of the centre, {realLamps?.Count ?? -1} from a register");
        if (_assets != null && _assets.Trees != null && _assets.Trees.Length > 0)
        {
            var rng = new System.Random(_map.Name.GetHashCode());
            // a trunk that the road's width (estimated from OpenStreetMap) puts in the carriageway stands just beside it,
            // on the verge or pavement, as street trees do
            int treesMoved = 0;
            V2 OffCarriageway(V2 q)
            {
                if (!InCarriageway(q, 0.4f, 0.9f, out var edge)) return q;
                treesMoved++;
                return edge;
            }
            // the city's tree register, where there is one: every street and park tree it manages, at its height
            var realTrees = MapExtras.Load(_map.Name, "trees", _proj, out var treeCredit);
            if (realTrees != null)
            {
                float treeHeight = PrefabHeight(_assets.Trees[0], 12f);
                int n = 0, treesWithCrown = 0;
                // (the nearest the map's centre first, when a register has more than MaxRealTrees)
                foreach (var r in realTrees.OrderBy(t => t.X * t.X + t.Z * t.Z))
                {
                    var p = OffCarriageway(new V2(r.X, r.Z));
                    if (!Inside(p) || n >= MaxRealTrees) continue;
                    int pick = rng.Next(_assets.Trees.Length); float yaw = (float)rng.NextDouble() * 360f, vary = 0.9f + 0.2f * (float)rng.NextDouble();
                    var kind = r["kind"];
                    if (kind != "conifer" && kind != "palm") kind = TreeKind(r["species"]);
                    GameObject go;
                    if (kind == "conifer" || kind == "palm")
                        go = MakeTree(kind, P(p, 0.1f), Mathf.Clamp(r.Float("height", kind == "palm" ? 9f : 12f), 3f, 35f) * vary, yaw, props, pick + n);
                    else
                    {
                        go = UnityEngine.Object.Instantiate(_assets.Trees[pick], P(p, 0.1f), Quaternion.Euler(0, yaw, 0), props);
                        float s = Mathf.Clamp(r.Float("height", treeHeight) / treeHeight, 0.25f, 2.2f) * vary;
                        // the crown as wide as the register measured it (a young or pruned street tree is narrower than
                        // the model, an old plane tree wider), within half to one and a half times the model's shape
                        float crown = r.Float("crown", 0f), sx = s;
                        if (crown > 0f) sx = Mathf.Clamp(crown / PrefabCrownWidth(_assets.Trees[pick], treeHeight * 0.75f) * vary, s * 0.5f, s * 1.5f);
                        go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(sx, s, sx));
                        if (crown > 0f) treesWithCrown++;
                    }
                    CullWhenSmall(go, 0.02f);
                    TreeTransforms.Add(go.transform);
                    n++;
                }
                RealDataCredits.Add($"{n} trees: {treeCredit}");
                if (treesWithCrown > 0) Debug.Log($"OsmMap: {treesWithCrown} register trees with their measured crown width");
            }
            // OpenStreetMap's trees (all of them, or those a partial register leaves out: not within 4 m of a registered tree)
            var registered = new HashSet<(int, int)>();
            if (realTrees != null) foreach (var r in realTrees) registered.Add(((int)Math.Floor(r.X / 4f), (int)Math.Floor(r.Z / 4f)));
            bool NearRegistered(V2 q)
            {
                int cx = (int)Math.Floor(q.X / 4f), cz = (int)Math.Floor(q.Y / 4f);
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) if (registered.Contains((cx + dx, cz + dz))) return true;
                return false;
            }
            foreach (var n in _data.Nodes.Values.Where(n => n.Tag("natural") == "tree"))
            {
                if (!_pos.TryGetValue(n.Id, out var p) || !Inside(p)) continue;
                if (registered.Count > 0 && NearRegistered(p)) continue;
                p = OffCarriageway(p);
                int pick = rng.Next(_assets.Trees.Length); float yaw = (float)rng.NextDouble() * 360f;
                // needle-leaved trees and palms (OSM: leaf_type, genus, species, taxon) get their own shapes
                var okind = TreeKind(n.Tag("genus") ?? n.Tag("species") ?? n.Tag("taxon") ?? n.Tag("species:en"), n.Tag("leaf_type"));
                float oh = ParseMetres(n.Tag("height"), okind == "palm" ? 9f : 12f);
                var tree = okind == "deciduous" ? UnityEngine.Object.Instantiate(_assets.Trees[pick], P(p, 0.1f), Quaternion.Euler(0, yaw, 0), props)
                                                : MakeTree(okind, P(p, 0.1f), Mathf.Clamp(oh, 3f, 35f), yaw, props, (int)(n.Id % 3));
                CullWhenSmall(tree, 0.02f);
                TreeTransforms.Add(tree.transform);
            }
            if (treesMoved > 0) Debug.Log($"OsmMap: {treesMoved} trees moved out of the carriageway to its edge");
        }
        PostsOutOfCarriageway(root);
        ObjectsOutOfWater(root, waterIndex);
        MarkPavedOverRoads();
        PavementsOntoTopography();
        RemoveBlockedParkedCars();
        // grass blades where grass is on top (not where a road, path, building or tree stands on the green area)
        Physics.SyncTransforms();
        EmitGrassBlades(world, _grassMeshes, q =>
        {
            if (!Physics.Raycast(new Vector3(q.X, 20f, q.Y), Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore)) return true;
            var n = hit.collider.name;
            return !(n == "Green areas" || n == "BGT green areas");
        });
        GroundShoulders(world, waterIndex);
    }

    /// <summary>
    /// On a map with terrain the base ground lies <see cref="TerrainHeight.GroundDrop"/> and 3 cm under the streets (so
    /// its cells do not rise through them where the slope curves), which left a ledge of 15-25 cm at a road's edge
    /// wherever nothing else (pavement, grass) covers the ground beside it: a cyclist that had strayed a metre off its
    /// line stood in such a ditch between a road and a lawn in São Paulo. Each car road gets a shoulder on both sides,
    /// the ground's own surface: from 0.3 m under the road's edge, just below its asphalt, down to the ground 1.5 m out,
    /// under the pavements and lawns where there are some. Not on maps with corridors (there the ground stays under the
    /// roads' lower edges anyway), not on bridges, not over water.
    /// </summary>
    void GroundShoulders(Transform world, WaterIndex water)
    {
        if (Terrain == null || Area != null || world == null) return;
        var groundGo = world.Find("Ground");
        if (groundGo == null || !groundGo.TryGetComponent<MeshRenderer>(out var mr)) return;
        var mb = new MeshBuilder();
        float h = HalfSize + 40;
        Vector2 Uv(V2 q) => new Vector2((q.X + h) / 4f, (q.Y + h) / 4f);
        float inY = Terrain.GroundDrop - 0.01f, outY = -0.03f;   // (lifted with the ground: 1 cm under the terrain, and the ground's own level)
        void Quad(V2 a, float ya, V2 b, float yb, V2 c, float yc, V2 d, float yd)
        {
            // (clockwise seen from above: facing up)
            float area = a.X * b.Y - b.X * a.Y + b.X * c.Y - c.X * b.Y + c.X * d.Y - d.X * c.Y + d.X * a.Y - a.X * d.Y;
            if (area > 0f) { (b, d) = (d, b); (yb, yd) = (yd, yb); }
            mb.Quad(new Vector3(a.X, ya, a.Y), new Vector3(b.X, yb, b.Y), new Vector3(c.X, yc, c.Y), new Vector3(d.X, yd, d.Y), Uv(a), Uv(b), Uv(c), Uv(d));
        }
        int quads = 0;
        foreach (var r in Roads)
        {
            if (r.Bridge || r.Line == null || r.Line.Count < 2 || !CarRoads.Contains(r.Type)) continue;
            // the centre line every 2.5 m at most (the ground's own cells), with each point's sideways direction
            var pts = new List<V2>();
            for (int i = 0; i + 1 < r.Line.Count; i++)
            {
                var a = new V2(r.Line[i].x, r.Line[i].z); var b = new V2(r.Line[i + 1].x, r.Line[i + 1].z);
                int n = Math.Max(1, (int)Math.Ceiling(V2.Distance(a, b) / 2.5f));
                for (int k = 0; k < n; k++) pts.Add(V2.Lerp(a, b, k / (float)n));
            }
            pts.Add(new V2(r.Line[r.Line.Count - 1].x, r.Line[r.Line.Count - 1].z));
            if (pts.Count < 2) continue;
            var side = new V2[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                var d = pts[Math.Min(i + 1, pts.Count - 1)] - pts[Math.Max(i - 1, 0)];
                side[i] = d.LengthSquared() > 1e-6f ? V2.Normalize(new V2(d.Y, -d.X)) : V2.Zero;
            }
            float inner = Math.Max(0.5f, r.Width / 2f - 0.3f), outer = r.Width / 2f + 1.5f;
            foreach (float sgn in new[] { -1f, 1f })
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    if (side[i] == V2.Zero || side[i + 1] == V2.Zero) continue;
                    V2 ai = pts[i] + side[i] * (sgn * inner), ao = pts[i] + side[i] * (sgn * outer);
                    V2 bi = pts[i + 1] + side[i + 1] * (sgn * inner), bo = pts[i + 1] + side[i + 1] * (sgn * outer);
                    if (!Inside(ao) && !Inside(bo)) continue;
                    if (water != null && water.Any && (water.Inside(ao) || water.Inside(bo))) continue;
                    Quad(ai, inY, ao, outY, bo, outY, bi, inY);
                    quads++;
                }
        }
        if (quads == 0) return;
        Emit(world, "Ground shoulders", mb, mr.sharedMaterial, true);
        Debug.Log($"OsmMap: {quads} ground shoulder quads along the car roads (no ledge at their edges)");
    }

    /// <summary>
    /// Placed objects (signs, lamps, trees, benches, parked bicycles and cars) whose foot is in the water, now that the
    /// water lies below the streets (registers and OpenStreetMap put some on a quay's edge, a few decimetres off), stand
    /// on the quay instead: 0.6 m from the water's edge.
    /// </summary>
    void ObjectsOutOfWater(Transform root, WaterIndex water)
    {
        if (water == null || !water.Any) return;
        int moved = 0;
        bool Container(Transform t) => t != null && t != root && !_emitted.Contains(t.gameObject) && t.GetComponents<Component>().Length == 1;
        foreach (var c in root.GetComponentsInChildren<Transform>(true).Where(Container).ToList())
            for (int i = 0; i < c.childCount; i++)
            {
                var o = c.GetChild(i);
                if (_emitted.Contains(o.gameObject) || Container(o)) continue;
                var q = new V2(o.position.x, o.position.z);
                if (!water.Inside(q) || !water.NearestShore(q, 30f, out var shore)) continue;
                var away = shore - q; float len = away.Length();
                var to = shore + (len > 1e-3f ? away / len : V2.UnitX) * 0.6f;
                o.position += new Vector3(to.X - q.X, 0f, to.Y - q.Y);
                moved++;
            }
        if (moved > 0) Debug.Log($"OsmMap: {moved} objects moved out of the water onto the quay");
    }

    /// <summary>
    /// On maps with the national topography's surfaces: the pedestrians' lines along the roads (placed beside each road at
    /// half its estimated width) moved sideways, point by point, onto the nearest of the topography's pavements within 4 m
    /// where they lie on a carriageway, cycle path or parking bay, so pedestrians walk on the pavement, not in the street.
    /// </summary>
    void PavementsOntoTopography()
    {
        if (!TopographyGround) return;
        Physics.SyncTransforms();
        string KindAt(float x, float z)
        {
            foreach (var h in Physics.RaycastAll(new Vector3(x, 5f, z), Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.name.StartsWith("BGT")) return h.collider.name;
            return null;
        }
        bool Walkable(string k) => k != null && (k.StartsWith("BGT pavements") || k.StartsWith("BGT green"));
        int moved = 0;
        foreach (var line in Pavements)
        {
            if (CrossingLines.Contains(line) || BridgeLines.ContainsKey(line) || line.Count < 2) continue;
            for (int i = 0; i < line.Count; i++)
            {
                var p = line[i];
                var k = KindAt(p.x, p.z);
                if (k == null || Walkable(k)) continue;
                var d = (line[Math.Min(i + 1, line.Count - 1)] - line[Math.Max(i - 1, 0)]); d.y = 0f;
                if (d.sqrMagnitude < 1e-4f) continue;
                var side = Vector3.Cross(Vector3.up, d.normalized);
                for (float o = 0.5f; o <= 4f; o += 0.5f)
                {
                    Vector3? hit = null;
                    foreach (var sgn in new[] { 1f, -1f })
                    {
                        var q = p + side * (o * sgn);
                        if (KindAt(q.x, q.z) is string kq && kq.StartsWith("BGT pavements")) { hit = q + side * (0.6f * sgn); break; }
                    }
                    if (hit.HasValue) { line[i] = new Vector3(hit.Value.x, p.y, hit.Value.z); moved++; break; }
                }
            }
        }
        if (moved > 0) Debug.Log($"OsmMap: {moved} points of the pedestrians' lines moved onto the topography's pavements");
    }

    /// <summary>On maps with the national topography's surfaces: roads that lie mostly (over half, at least 30 m) on its
    /// paving, grass or gravel are pedestrian streets, squares and yards, not for through traffic.</summary>
    void MarkPavedOverRoads()
    {
        if (!TopographyGround) return;
        Physics.SyncTransforms();
        int marked = 0;
        foreach (var r in Roads)
        {
            if (r.Bridge || r.NoThroughTraffic || r.Line.Count < 2) continue;
            int on = 0, off = 0;
            for (int i = 1; i < r.Line.Count; i++)
            {
                var a = r.Line[i - 1]; var b = r.Line[i]; float len = Vector3.Distance(a, b);
                for (float d = 1f; d < len; d += 2f)
                {
                    var q = Vector3.Lerp(a, b, d / len);
                    string kind = null;
                    foreach (var h in Physics.RaycastAll(new Vector3(q.x, 5f, q.z), Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider.name.StartsWith("BGT")) { kind = h.collider.name; break; }
                    if (kind == null) continue;
                    if (kind.StartsWith("BGT pavements") || kind.StartsWith("BGT green") || kind.StartsWith("BGT gravel")) off++; else on++;
                }
            }
            if (off * 2 > on + off && off * 2f >= 30f) { r.NoThroughTraffic = true; r.PavedOver = true; marked++; }
        }
        if (marked > 0) Debug.Log($"OsmMap: {marked} roads on paving, grass or gravel (pedestrian streets, squares, yards) left out of the traffic's routes");
    }

    /// <summary>The ground-level car roads by 20 m cell (segment ends and half the road's width).</summary>
    Dictionary<(int, int), List<(V2 a, V2 b, float half)>> _roadCells;

    /// <summary>Whether a point lies in a car road's carriageway or within <paramref name="margin"/> m of its edge; then
    /// <paramref name="edge"/> is the point <paramref name="outside"/> m beyond that edge, square to the road.</summary>
    /// <summary>
    /// A pavement's, footpath's or cycle path's strip (its two edges, point by point) kept off the carriageways: the
    /// edges get a point at least every metre, and each point more than 5 cm inside a car road (its estimated width) moves
    /// out to that road's edge. A pavement drawn beside one road reached over the next road at a junction, and a footpath
    /// mapped beside a road overlapped it by its width: the paving lay on the road in wedges.
    /// </summary>
    void OffCarriageways(List<V2> left, List<V2> right)
    {
        if (_roadCells == null || left.Count < 2 || left.Count != right.Count) return;
        var nl = new List<V2>(); var nr = new List<V2>();
        for (int i = 0; i < left.Count; i++)
        {
            if (i > 0)
            {
                float d = Math.Max(V2.Distance(left[i - 1], left[i]), V2.Distance(right[i - 1], right[i]));
                int steps = (int)Math.Ceiling(d / 1f);
                for (int k = 1; k < steps; k++) { nl.Add(V2.Lerp(left[i - 1], left[i], k / (float)steps)); nr.Add(V2.Lerp(right[i - 1], right[i], k / (float)steps)); }
            }
            nl.Add(left[i]); nr.Add(right[i]);
        }
        for (int i = 0; i < nl.Count; i++)
        {
            if (InCarriageway(nl[i], -0.05f, 0f, out var e1)) { nl[i] = e1; PavementPointsMoved++; }
            if (InCarriageway(nr[i], -0.05f, 0f, out var e2)) { nr[i] = e2; PavementPointsMoved++; }
        }
        left.Clear(); left.AddRange(nl); right.Clear(); right.AddRange(nr);
    }
    /// <summary>
    /// The points of a pavement's or cycle path's mesh that lie on a drawn road (its collider, built before them) move to
    /// the nearest point off it within 3 m (eight directions, then halved down to 5 cm), so the strips end at the road's
    /// edge: in Montreal a pavement's corner lay over the road in a slab.
    /// </summary>
    void OffDrawnRoads(MeshBuilder mb)
    {
        var done = new Dictionary<(int, int), Vector3>();
        int moved = 0;
        for (int i = 0; i < mb.V.Count; i++)
        {
            var v = mb.V[i];
            var key = (Mathf.RoundToInt(v.x * 100f), Mathf.RoundToInt(v.z * 100f));
            if (done.TryGetValue(key, out var to)) { if (to != v) { mb.V[i] = new Vector3(to.x, v.y, to.z); } continue; }
            var q = new V2(v.x, v.z);
            // (5 cm inside: a strip's edge lying on the road's edge stays)
            if (!OnDrawnRoad(q))
            { done[key] = v; continue; }
            float best = float.MaxValue; V2 bestQ = q;
            for (int k = 0; k < 8; k++)
            {
                var d = new V2(Mathf.Cos(k * Mathf.PI / 4f), Mathf.Sin(k * Mathf.PI / 4f));
                float lo = 0f, hi = -1f;
                for (float t = 0.25f; t <= 3f; t += 0.25f) if (!OnDrawnRoad(q + d * t)) { hi = t; break; }
                if (hi < 0f || hi >= best) continue;
                lo = hi - 0.25f;
                while (hi - lo > 0.05f) { float m = (lo + hi) / 2f; if (OnDrawnRoad(q + d * m)) lo = m; else hi = m; }
                if (hi < best) { best = hi; bestQ = q + d * hi; }
            }
            var nv = best < float.MaxValue ? new Vector3(bestQ.X, v.y, bestQ.Y) : v;
            done[key] = nv;
            if (best < float.MaxValue && best > 0.05f) { mb.V[i] = nv; moved++; }
        }
        if (moved > 0) Debug.Log($"OsmMap: {moved} points of pavements and cycle paths moved off the drawn roads");
    }

    /// <summary>Bicycle parks mapped as areas filled with rows of bicycles.</summary>
    public int BikeParkAreas;
    /// <summary>Bicycle racks moved off the carriageway, or left out where they could not be.</summary>
    public int RacksMoved, RacksLeftOut;
    /// <summary>Points of pavements, footpaths and cycle paths moved off a carriageway to its edge.</summary>
    public int PavementPointsMoved;

    /// <summary>
    /// The parts of a walking line (the middle of a pavement, a footpath) not on a carriageway: points every metre, the line
    /// cut where one lies more than 0.3 m inside a car road (its estimated width); with <paramref name="alongOnly"/> only
    /// where the line runs along that road (within 40 degrees of it), so a footpath over a road still crosses it. Parts
    /// under 4 m are left out. The AI pedestrians walk these lines.
    /// </summary>
    List<List<V2>> WalkingLineOffCarriageways(List<V2> line, bool alongOnly)
    {
        var parts = new List<List<V2>>();
        if (_roadCells == null || line.Count < 2) { parts.Add(line); return parts; }
        var cur = new List<V2>();
        void Close() { if (cur.Count >= 2 && Geometry2D.Length(cur) >= 4f) parts.Add(cur); cur = new List<V2>(); }
        for (int i = 1; i < line.Count; i++)
        {
            var a = line[i - 1]; var b = line[i]; float d = V2.Distance(a, b); if (d < 1e-3f) continue;
            var dir = (b - a) / d;
            int n = Math.Max(1, (int)Math.Ceiling(d));
            for (int k = i == 1 ? 0 : 1; k <= n; k++)
            {
                var q = V2.Lerp(a, b, k / (float)n);
                if (OnCarriageway(q, dir, alongOnly)) Close();
                else cur.Add(q);
            }
        }
        Close();
        return parts;
    }

    bool OnCarriageway(V2 q, V2 dir, bool alongOnly)
    {
        if (!_roadCells.TryGetValue(((int)Math.Floor(q.X / 20f), (int)Math.Floor(q.Y / 20f)), out var segs)) return false;
        foreach (var (a, b, half) in segs)
        {
            var ab = b - a; float len2 = ab.LengthSquared(); if (len2 < 1e-4f) continue;
            float u = Math.Max(0f, Math.Min(1f, V2.Dot(q - a, ab) / len2));
            if (V2.Distance(q, a + ab * u) > half - 0.3f) continue;
            if (alongOnly && Math.Abs(V2.Dot(V2.Normalize(ab), dir)) < 0.766f) continue;
            return true;
        }
        return false;
    }

    bool InCarriageway(V2 q, float margin, float outside, out V2 edge)
    {
        edge = q;
        if (_roadCells == null || !_roadCells.TryGetValue(((int)Math.Floor(q.X / 20f), (int)Math.Floor(q.Y / 20f)), out var segs)) return false;
        float bestD = float.MaxValue; V2 bestQ = q, bestN = default; float bestHalf = 0f;
        foreach (var (a, b, half) in segs)
        {
            var ab = b - a; float len2 = ab.LengthSquared(); if (len2 < 1e-4f) continue;
            float u = Math.Max(0f, Math.Min(1f, V2.Dot(q - a, ab) / len2));
            var c = a + ab * u; float d = V2.Distance(q, c);
            if (d - half < bestD) { bestD = d - half; bestQ = c; bestHalf = half; bestN = d > 1e-3f ? (q - c) / d : V2.Normalize(new V2(ab.Y, -ab.X)); }
        }
        if (bestD > margin) return false;
        edge = bestQ + bestN * (bestHalf + outside);
        return true;
    }

    /// <summary>
    /// Posts that ended up in a carriageway (lamp and sign posts, bollards, beacons, signal posts without an arm over the
    /// road: placed from registers or OpenStreetMap's nodes, against a road width that OpenStreetMap only estimates) stand
    /// at its edge instead, where traffic does not run into them.
    /// </summary>
    void PostsOutOfCarriageway(Transform root)
    {
        int moved = 0, movedOffDrawn = 0, keptBack = 0;
        Physics.SyncTransforms();
        var containers = new HashSet<string> { "Street furniture", "Street furniture (OSM)", "Traffic signs", "Traffic signals", "Pedestrian crossings" };
        foreach (var c in root.GetComponentsInChildren<Transform>(true).Where(t => containers.Contains(t.name)).ToList())
            for (int i = 0; i < c.childCount; i++)
            {
                var o = c.GetChild(i);
                var rs = o.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                // (signals with an arm over the road stay; benches, up to 2 m long, are kept a little further from the edge;
                // a street lamp's arm reaches over the road, but its pole is a post)
                bool bench = o.name.Contains("Bench") || o.name.Contains("bench");
                bool lamp = o.name.StartsWith("StreetLamp", StringComparison.Ordinal);
                if ((!bench && !lamp && Mathf.Max(b.size.x, b.size.z) > 1.3f) || (!bench && b.size.y < 0.8f)) continue;
                var q = new V2(o.position.x, o.position.z);
                float reach = bench ? Mathf.Max(b.size.x, b.size.z) / 2f + 0.3f : 0.3f;
                if (InCarriageway(q, reach, reach + 0.3f, out var edge))
                {
                    o.position += new Vector3(edge.X - q.X, PavementHeight - Mathf.Min(o.position.y, PavementHeight), edge.Y - q.Y);
                    moved++;
                    q = edge;
                    // (and on to the drawn road's check: the drawn road is often wider than OpenStreetMap's estimate, with
                    // its lanes and rounded corners, and a sign moved to the estimate's edge still stood in the road)
                }
                // still on the drawn road or a cycle path or cycle lane (the drawn road is wider than OpenStreetMap's width at
                // junctions and crossings, where a rider keeping to the kerb rode through a crossing sign's post; signs stood
                // in red cycle paths): stepped away from the road until the ground under it is neither, then half a metre further
                if (bench || !NearestRoad(q, out _, out var away)) continue;
                if (!OnDrawnRoad(q, true))
                {
                    // (beside the drawn road, at least half a metre back from its edge, as posts stand behind a kerb: a
                    // lamp every 25 m stood on the kerb line where the drawn road is wider than OpenStreetMap's estimate,
                    // and cars passing it brushed its pole; moved back up to a metre, not into a building)
                    if (!OnDrawnRoad(q - away * 0.5f, true)) continue;
                    var back = q;
                    for (int k = 0; k < 4 && (OnDrawnRoad(back - away * 0.5f, true) || OnDrawnRoad(back, true)); k++) back += away * 0.25f;
                    if (OnDrawnRoad(back - away * 0.5f, true) || InBuilding(back)) continue;
                    o.position += new Vector3(back.X - q.X, 0f, back.Y - q.Y);
                    keptBack++;
                    continue;
                }
                var p2 = q;
                for (int k = 0; k < 24 && OnDrawnRoad(p2, true); k++) p2 += away * 0.25f;
                if (OnDrawnRoad(p2, true)) continue;
                p2 += away * 0.5f;
                if (OnDrawnRoad(p2, true)) p2 -= away * 0.3f;
                o.position += new Vector3(p2.X - q.X, PavementHeight - Mathf.Min(o.position.y, PavementHeight), p2.Y - q.Y);
                movedOffDrawn++;
            }
        if (moved > 0) Debug.Log($"OsmMap: {moved} posts moved out of the carriageway to its edge");
        if (movedOffDrawn > 0) Debug.Log($"OsmMap: {movedOffDrawn} posts moved off the drawn road at junctions");
        if (keptBack > 0) Debug.Log($"OsmMap: {keptBack} posts moved back to half a metre from the drawn road's edge");
        // (a building's walls or roof at the post's foot, its 30 cm round)
        bool InBuilding(V2 q)
        {
            foreach (var c in Physics.OverlapSphere(new Vector3(q.X, PavementHeight + 1f, q.Y), 0.3f, ~0, QueryTriggerInteraction.Ignore))
                if (c.name.StartsWith("Buildings", StringComparison.Ordinal) || c.name.StartsWith("Roofs", StringComparison.Ordinal)) return true;
            return false;
        }
    }

    /// <summary>Whether the ground drawn at <paramref name="q"/> is a road (a car road's surface, not its markings; on maps
    /// with the national topography its carriageways, not OpenStreetMap's invisible driving surfaces).</summary>
    bool OnDrawnRoad(V2 q, bool cycleToo = false)
    {
        // (the surface seen there: the highest one, within 5 cm; the topography's cycle paths lie on its carriageways)
        float top = float.NegativeInfinity; bool road = false, cycle = false;
        foreach (var h in Physics.RaycastAll(new Vector3(q.X, 200f, q.Y), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
        {
            var n = h.collider.name;
            bool isRoad = TopographyGround ? n.StartsWith("BGT roads") : n.StartsWith("Roads");
            bool isCycle = n.IndexOf("ycle", StringComparison.Ordinal) >= 0;
            bool isGround = isRoad || isCycle || n.StartsWith("BGT") || n.StartsWith("Pavements") || n.StartsWith("Ground") || n.StartsWith("Green") || n.StartsWith("Kerbs");
            if (!isGround) continue;
            if (h.point.y > top + 0.05f) { top = h.point.y; road = isRoad; cycle = isCycle; }
            else if (h.point.y > top - 0.05f) { road |= isRoad; cycle |= isCycle; }
        }
        return road || (cycleToo && cycle);
    }

    /// <summary>The nearest car road's centre line within 30 m: the point on it and the direction from it towards <paramref name="q"/>.</summary>
    bool NearestRoad(V2 q, out V2 centre, out V2 away)
    {
        centre = q; away = default;
        if (_roadCells == null || !_roadCells.TryGetValue(((int)Math.Floor(q.X / 20f), (int)Math.Floor(q.Y / 20f)), out var segs)) return false;
        float bestD = 30f; bool found = false;
        foreach (var (a, b, half) in segs)
        {
            var ab = b - a; float len2 = ab.LengthSquared(); if (len2 < 1e-4f) continue;
            float u = Math.Max(0f, Math.Min(1f, V2.Dot(q - a, ab) / len2));
            var c = a + ab * u; float d = V2.Distance(q, c);
            if (d >= bestD || d < 0.05f) continue;
            bestD = d; centre = c; away = (q - c) / d; found = true;
        }
        return found;
    }

    /// <summary>Whether a point lies on a car road on the ground or within <paramref name="margin"/> of its edge, by the
    /// roads' 20 m cells (every road for each of a car park's bays took minutes on a big map).</summary>
    bool OnCarRoad(V2 q, float margin)
    {
        if (_roadCells == null) return NearestRoad(q, out _, out var rd, out var rw) && rd < rw / 2f + margin;
        if (!_roadCells.TryGetValue(((int)Math.Floor(q.X / 20f), (int)Math.Floor(q.Y / 20f)), out var segs)) return false;
        foreach (var (a, b, half) in segs)
        {
            var ab = b - a; float len2 = ab.LengthSquared(); if (len2 < 1e-4f) continue;
            float u = Math.Max(0f, Math.Min(1f, V2.Dot(q - a, ab) / len2));
            if (V2.Distance(q, a + ab * u) < half + margin) return true;
        }
        return false;
    }

    /// <summary>Parked cars, kept to take away those that stand in a tree, a post or a wall (RemoveBlockedParkedCars).</summary>
    readonly List<(GameObject car, float yaw)> _parkedCars = new List<(GameObject, float)>();

    /// <summary>
    /// Parked cars are placed before the trees and street lamps (and partly on the kerb in narrow streets): those that stand
    /// in a tree's trunk or in anything solid that is not the ground (a lamp or sign post, a bollard, a wall) are taken away.
    /// </summary>
    void RemoveBlockedParkedCars()
    {
        Physics.SyncTransforms();
        int removed = 0;
        foreach (var (car, yaw) in _parkedCars)
        {
            if (car == null) continue;
            var t = car.transform; var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; var right = Vector3.Cross(Vector3.up, fwd);
            bool blocked = false;
            foreach (var tree in TreeTransforms)
            {
                if (tree == null) continue;
                var d = tree.position - t.position; d.y = 0f;
                if (Mathf.Abs(Vector3.Dot(d, fwd)) < 2.6f && Mathf.Abs(Vector3.Dot(d, right)) < 1.4f) { blocked = true; break; }
            }
            if (!blocked)
                foreach (var c in Physics.OverlapBox(t.position + Vector3.up * 0.9f, new Vector3(0.85f, 0.5f, 2.1f), Quaternion.LookRotation(fwd, Vector3.up), ~0, QueryTriggerInteraction.Ignore))
                {
                    if (c.transform.IsChildOf(t)) continue;
                    var n = c.name;
                    if (n.StartsWith("Roads") || n.StartsWith("Pavements") || n.StartsWith("Kerbs") || n.StartsWith("Cycle") || n.StartsWith("BGT") || n.StartsWith("Ground") || n.StartsWith("Terrain") || n.StartsWith("Road")) continue;
                    blocked = true; break;
                }
            if (blocked) { if (Application.isPlaying) UnityEngine.Object.Destroy(car); else UnityEngine.Object.DestroyImmediate(car); removed++; ParkedCarCount--; }
        }
        if (removed > 0) Debug.Log($"OsmMap: {removed} parked cars taken away (in a tree, a post or a wall)");
    }

    /// <summary>Not drawn once smaller than this fraction of the screen's height (thousands of register trees and lamps).</summary>
    /// <summary>How far the skyline reaches from the centre (m; 0: none): cameras see that far.</summary>
    public float SkylineReach { get; private set; }

    /// <summary>
    /// The skyline around the map ({map}.skyline.txt.gz from Tools/skyline.py: the tall buildings within a few km, from
    /// OpenStreetMap): plain towers with rows of windows (lit at night), without collisions or shadows, so views along
    /// the streets end in the city's towers instead of empty sky.
    /// </summary>
    /// <summary>Far city blocks built (-debug: how many, in how many chunks).</summary>
    public int FarCityParts;

    /// <summary>
    /// The far city ({map}.far.bin.gz, Tools/bag3d_far.py): every building around the map's area as a block, its 3D BAG
    /// LoD 1.3 footprint raised to its roof's height, so the city goes on to the horizon from a drone or the motorway (on
    /// the TU/e campus it ended at the campus' edge). Walls in one of four plain façades with rows of windows a storey
    /// apart; roofs in the colours of the surroundings photo over them (tiles, gravel, green roofs). In 500 m chunks
    /// (culled out of view); no colliders (nobody drives there).
    /// </summary>
    /// <summary>No far city (the tests that build every map: it has no colliders and doubles Eindhoven's build time; on
    /// the command line -noFarCity).</summary>
    public static bool NoFarCity = Array.IndexOf(CommandLine.Args, "-noFarCity") >= 0;

    void EmitFarCity(Transform world)
    {
        if (MapExtras.Disabled || NoFarCity || string.IsNullOrEmpty(_map.Name)) return;
        List<Bag3dBuildings.FarPart> parts;
        try { parts = Bag3dBuildings.LoadFar(_map.Name, _proj); }
        catch (Exception e) { Debug.LogWarning("Far city: " + e.Message); return; }
        if (parts == null || parts.Count == 0) return;
        var wallTones = new[] { new Color(0.47f, 0.26f, 0.19f), new Color(0.36f, 0.28f, 0.24f), new Color(0.74f, 0.71f, 0.65f), new Color(0.56f, 0.56f, 0.55f) };
        var wallMats = new Material[wallTones.Length];
        for (int k = 0; k < wallTones.Length; k++)
        {
            var tone = wallTones[k];
            // (one bay 2.7 m wide and one storey 3 m high: a window 1.3 by 1.5 m in the wall's colour)
            var tex = Tex("far_facade_" + k, 64, (x, y, r) =>
            {
                bool window = x >= 16 && x < 47 && y >= 22 && y < 54;
                var c = window ? new Color(0.16f, 0.19f, 0.23f) : tone;
                return Noise(c, r, window ? 0.15f : 0.08f);
            });
            wallMats[k] = Mat("Far city facade " + k, Color.white, 0.1f, tex);
        }
        var roofMat = Aerial != null && Aerial.FarTexture != null ? Mat("Far city roofs", Color.white, 0.1f, Aerial.FarTexture) : Mat("Far city roofs plain", new Color(0.33f, 0.32f, 0.31f), 0.1f);
        float fh = Aerial != null ? Aerial.FarHalfSize : 1f;
        var chunks = new Dictionary<long, MeshBuilder[]>();
        const float Chunk = 500f, Base = -0.4f;
        int built = 0;
        foreach (var part in parts)
        {
            if (part.Ring.Length < 3) continue;
            var poly = part.Ring.Select(v => new V2(v.x, v.y)).ToList();
            var c = poly.Aggregate(V2.Zero, (acc, q) => acc + q) / poly.Count;
            long key = ((long)Mathf.FloorToInt(c.X / Chunk) << 32) ^ (uint)Mathf.FloorToInt(c.Y / Chunk);
            if (!chunks.TryGetValue(key, out var mbs)) chunks[key] = mbs = Enumerable.Range(0, wallTones.Length + 1).Select(_ => new MeshBuilder()).ToArray();
            if (Geometry2D.SignedArea(poly) < 0) poly.Reverse();
            uint seed = (uint)(Mathf.RoundToInt(c.X * 7.3f) * 73856093 ^ Mathf.RoundToInt(c.Y * 3.1f) * 19349663);
            // (taller blocks: concrete and light plaster; low ones mostly brick)
            int tone = part.Height > 14f ? 2 + (int)(seed % 2) : (int)(seed % 6 < 4 ? seed % 2 : 2 + seed % 2);
            var wall = mbs[tone];
            float top = Base + part.Height, u = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                float len = V2.Distance(a, b);
                if (len < 0.05f) continue;
                // (outward: the ring is counter-clockwise, the outside to the right of a->b; windows from 0.4 m up)
                wall.Quad(P(a, Base), P(a, top), P(b, top), P(b, Base),
                    new Vector2(u / 2.7f, (Base - 0.6f) / 3f), new Vector2(u / 2.7f, (top - 0.6f) / 3f), new Vector2((u + len) / 2.7f, (top - 0.6f) / 3f), new Vector2((u + len) / 2.7f, (Base - 0.6f) / 3f));
                u += len;
            }
            var roofMb = mbs[wallTones.Length];
            int v0 = roofMb.V.Count;
            try { roofMb.Polygon(poly, top, 1f); } catch { continue; }
            for (int i = v0; i < roofMb.V.Count; i++) { var q = roofMb.V[i]; roofMb.UV[i] = new Vector2((q.x + fh) / (2f * fh), (q.z + fh) / (2f * fh)); }
            built++;
        }
        var farRoot = new GameObject("Far city").transform;
        farRoot.SetParent(world, false);
        foreach (var kv in chunks)
        {
            var chunkRoot = new GameObject($"Far city {(int)(kv.Key >> 32)},{(int)(kv.Key & 0xffffffff)}").transform;
            chunkRoot.SetParent(farRoot, false);
            for (int k = 0; k < kv.Value.Length; k++)
                if (kv.Value[k].V.Count > 0) Emit(chunkRoot, k < wallTones.Length ? "Far city walls" : "Far city roofs", kv.Value[k], k < wallTones.Length ? wallMats[k] : roofMat, false);
        }
        FarCityParts = built;
        RealDataCredits.Add($"{built} far buildings: 3D BAG LoD 1.3 (tudelft3d and 3DGI, CC BY 4.0)");
        Debug.Log($"OsmMap: far city: {built} buildings around the area in {chunks.Count} chunks of {Chunk:F0} m");
    }

    void EmitSkyline(Transform parent)
    {
        if (MapExtras.Disabled || string.IsNullOrEmpty(_map.Name)) return;
        var path = System.IO.Path.Combine(Application.streamingAssetsPath, "Maps", _map.Name + ".skyline.txt.gz");
        if (!System.IO.File.Exists(path)) return;
        var walls = new MeshBuilder(); var tops = new MeshBuilder();
        int count = 0; float reach = 0f;
        var ci = CultureInfo.InvariantCulture;
        using (var gz = new System.IO.Compression.GZipStream(System.IO.File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress))
        using (var r = new System.IO.StreamReader(gz))
        {
            var head = r.ReadLine()?.Split(' ');
            if (head == null || head.Length < 3 || head[0] != "SKY1") return;
            var local = new GeoProjection(double.Parse(head[1], ci), double.Parse(head[2], ci));
            bool same = Math.Abs(local.Lat0 - _proj.Lat0) < 1e-9 && Math.Abs(local.Lon0 - _proj.Lon0) < 1e-9;
            r.ReadLine();   // credit
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var f = line.Split(' ');
                if (f.Length < 9) continue;
                float height = int.Parse(f[0], ci) / 10f;
                var poly = new List<V2>();
                for (int i = 1; i + 1 < f.Length; i += 2)
                {
                    double x = int.Parse(f[i], ci) / 100.0, z = int.Parse(f[i + 1], ci) / 100.0;
                    if (!same) { local.ToGeo(x, z, out var la, out var lo); _proj.ToLocal(la, lo, out x, out z); }
                    poly.Add(new V2((float)x, (float)z));
                }
                if (poly.Count < 3 || poly.Any(q => Area != null ? Area.Contains(q) : Math.Abs(q.X) < HalfSize && Math.Abs(q.Y) < HalfSize)) continue;
                if (Geometry2D.SignedArea(poly) < 0) poly.Reverse();   // counter-clockwise: walls face out
                float along = 0f;
                for (int i = 0; i < poly.Count; i++)
                {
                    var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                    float len = V2.Distance(a, b); if (len < 0.05f) continue;
                    // (from 5 m below the ground: the terrain beyond the map is not known; UVs: 6 m wide, 7 m high windows pattern)
                    walls.Quad(P(b, -5f), P(b, height), P(a, height), P(a, -5f),
                        new Vector2((along + len) / 6f, -5f / 7f), new Vector2((along + len) / 6f, height / 7f), new Vector2(along / 6f, height / 7f), new Vector2(along / 6f, -5f / 7f));
                    along += len;
                    reach = Math.Max(reach, a.Length());
                }
                tops.Polygon(poly, height, 10f);
                count++;
            }
        }
        if (count == 0) return;
        // glass and concrete in bands of windows: two storeys and four bays per texture repeat
        var tex = Tex("skyline_windows", 64, (x, y, rr) =>
        {
            bool window = (y % 32) >= 9 && (y % 32) <= 28 && (x % 16) >= 2 && (x % 16) <= 14;
            return window ? Noise(new Color(0.2f, 0.25f, 0.3f), rr, 0.15f) : Noise(new Color(0.55f, 0.55f, 0.53f), rr, 0.08f);
        });
        var m = Mat("Skyline", Color.white, 0.35f, tex);
        m.SetTexture("_EmissionMap", Tex("skyline_lights", 64, (x, y, rr) =>
        {
            bool window = (y % 32) >= 9 && (y % 32) <= 28 && (x % 16) >= 2 && (x % 16) <= 14;
            return window && rr.NextDouble() < 0.5 ? new Color(0.9f, 0.75f, 0.5f) : Color.black;
        }));
        m.SetColor("_EmissionColor", Color.black);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        NightMaterials.Add(m);
        foreach (var (mb, name) in new[] { (walls, "Skyline walls"), (tops, "Skyline roofs") })
        {
            var go = Emit(parent, name, mb, name.EndsWith("roofs") ? Mat("Roof", new Color(0.27f, 0.27f, 0.28f), 0.2f) : m, false);
            if (go == null) continue;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }
        SkylineReach = reach + 200f;
        RealDataCredits.Add($"skyline of {count} tall buildings around the map (OpenStreetMap)");
    }

    /// <summary>How far the water lies below the streets (m): Dutch canals about 1 m, harbours and rivers more (not modelled).</summary>
    public const float WaterDepth = 0.9f;

    /// <summary>A quay along a water outline: brick walls from below the water line up to the street (both faces), and a
    /// stone coping 0.8 m wide on the land side, just above the ground, over the ground's 1 m cells at the shore.</summary>
    void Quay(List<V2> poly, MeshBuilder walls, MeshBuilder copings, MeshBuilder edges, Dictionary<(int, int), List<V2>> crossings, WaterIndex water)
    {
        // only along shores: where this area touches or overlaps another water area there is no wall, coping or kerb
        // the invisible kerb, 0.6 m high, in 1 m pieces, left out within 7 m of a way crossing the edge (onto a bridge)
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            float len = V2.Distance(a, b); if (len < 0.01f) continue;
            int pieces = Math.Max(1, (int)Math.Ceiling(len));
            for (int k = 0; k < pieces; k++)
            {
                var p0 = V2.Lerp(a, b, k / (float)pieces); var p1 = V2.Lerp(a, b, (k + 1) / (float)pieces);
                var mid = (p0 + p1) / 2f;
                if (!water.IsShore(p0, p1)) continue;
                int cx = (int)Math.Floor(mid.X / 4f), cz = (int)Math.Floor(mid.Y / 4f);
                bool way = false;
                for (int dx = -2; dx <= 2 && !way; dx++)
                    for (int dz = -2; dz <= 2 && !way; dz++)
                        if (crossings.TryGetValue((cx + dx, cz + dz), out var near)) foreach (var x in near) if (V2.DistanceSquared(x, mid) < 49f) { way = true; break; }
                if (way) continue;
                edges.Quad(P(p0, 0f), P(p0, 0.6f), P(p1, 0.6f), P(p1, 0f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                edges.Quad(P(p1, 0f), P(p1, 0.6f), P(p0, 0.6f), P(p0, 0f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            }
        }
        int n = poly.Count;
        float along = 0f;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % n];
            float len = V2.Distance(a, b); if (len < 0.01f) continue;
            float y0 = -WaterDepth - 0.3f, y1 = 0f;
            if (!water.IsShore(a, b)) { along += len; continue; }
            Vector2 U(float s, float y) => new Vector2(s / 2.5f, (y - y0) / 2.5f);
            walls.Quad(P(a, y0), P(a, y1), P(b, y1), P(b, y0), U(along, y0), U(along, y1), U(along + len, y1), U(along + len, y0));
            walls.Quad(P(b, y0), P(b, y1), P(a, y1), P(a, y0), U(along + len, y0), U(along + len, y1), U(along, y1), U(along, y0));
            along += len;
        }
        // the land side: right of the outline when it runs counter-clockwise
        // in runs of shore edges (a closed ring when the whole outline is shore)
        float side = Geometry2D.SignedArea(poly) > 0 ? 0.4f : -0.4f;
        int start = -1;
        for (int i = 0; i < n; i++) if (!water.IsShore(poly[i], poly[(i + 1) % n])) { start = i; break; }
        var runs = new List<List<V2>>();
        if (start < 0) runs.Add(new List<V2>(poly) { poly[0] });
        else
        {
            List<V2> run = null;
            for (int k = 1; k <= n; k++)
            {
                int i = (start + k) % n;
                if (water.IsShore(poly[i], poly[(i + 1) % n]))
                {
                    if (run == null) { run = new List<V2> { poly[i] }; runs.Add(run); }
                    run.Add(poly[(i + 1) % n]);
                }
                else run = null;
            }
        }
        foreach (var run in runs)
        {
            if (run.Count < 2) continue;
            var l = new List<V2>(); var r = new List<V2>();
            Geometry2D.Strip(run, 0.8f, side, l, r);
            copings.Strip(l, r, 0.006f, 1f);
        }
    }

    /// <summary>The posters in bus shelters' advertising panels: bright pictures with a headline, lit from behind at night.</summary>
    Material AdvertMaterial()
    {
        if (_mats.TryGetValue("Advert", out var made)) return made;
        var tex = TexRect("advert_posters", 256, 384, (x, y, r) =>
        {
            float u = x / 256f, v = y / 384f;
            if (u < 0.04f || u > 0.96f || v < 0.03f || v > 0.97f) return new Color(0.85f, 0.85f, 0.83f);   // the panel's frame
            // a sky-to-sea picture with a big round product and a headline band
            var bg = Color.Lerp(new Color(0.12f, 0.45f, 0.75f), new Color(0.95f, 0.75f, 0.35f), v);
            float d = new Vector2(u - 0.5f, v - 0.42f).magnitude;
            if (d < 0.22f) bg = Color.Lerp(new Color(0.9f, 0.2f, 0.25f), new Color(1f, 0.55f, 0.5f), Mathf.Clamp01(0.2f - (u - 0.45f) - (v - 0.5f)));
            if (v > 0.74f && v < 0.9f) bg = (x / 9 + y / 13) % 3 == 0 && u > 0.12f && u < 0.88f ? Color.white : new Color(0.08f, 0.1f, 0.18f);
            if (v > 0.1f && v < 0.16f && u > 0.2f && u < 0.8f) bg = new Color(0.95f, 0.95f, 0.95f);
            return bg;
        });
        var m = Mat("Advert", Color.white, 0.6f, tex);
        m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.black); m.EnableKeyword("_EMISSION");
        NightMaterials.Add(m);
        return m;
    }

    /// <summary>A photo texture of a surface (MapAssets.GroundColour/GroundNormal) on a ground material whose meshes have
    /// <paramref name="uvMetres"/> metres per UV unit.</summary>
    void GroundPhoto(Material m, string name, float uvMetres, Color tint)
    {
        int i = Array.IndexOf(_assets.GroundNames, name);
        if (i < 0 || _assets.GroundColour == null || i >= _assets.GroundColour.Length || _assets.GroundColour[i] == null) return;
        m.shader = _assets.FacadeTemplate.shader;
        m.SetTexture("_BaseMap", _assets.GroundColour[i]);
        m.SetColor("_BaseColor", tint);
        if (_assets.GroundNormal != null && i < _assets.GroundNormal.Length && _assets.GroundNormal[i] != null)
        {
            m.SetTexture("_BumpMap", _assets.GroundNormal[i]); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
        }
        float size = _assets.GroundSize != null && i < _assets.GroundSize.Length && _assets.GroundSize[i] > 0f ? _assets.GroundSize[i] : 2f;
        m.SetTextureScale("_BaseMap", Vector2.one * (uvMetres / size));
    }

    /// <summary>
    /// The aerial photo's green cells under the crown of a mapped tree (the city's register or OpenStreetMap; the crown's
    /// diameter where known, else 7 m): a street tree's crown seen from above made a lawn under it on the pavement. The
    /// crown keeps its green only where the ring just outside it is mostly green too (a tree in a lawn).
    /// </summary>
    void TreeCrownsOffLawns()
    {
        var a = Aerial; int n = a.ClassN; float cell = 2f * a.HalfSize / n;
        var trees = new List<(V2 p, float r)>();
        var reg = MapExtras.Load(_map.Name, "trees", _proj, out _);
        if (reg != null) foreach (var t in reg) trees.Add((new V2(t.X, t.Z), Mathf.Clamp(t.Float("crown", 7f) / 2f, 2f, 9f)));
        foreach (var nd in _data.Nodes.Values)
            if (nd.Tag("natural") == "tree" && _pos.TryGetValue(nd.Id, out var q)) trees.Add((q, 3.5f));
        var orig = (byte[])a.Classes.Clone();
        int cleared = 0;
        foreach (var (p, r) in trees)
        {
            int ci = (int)((p.X + a.HalfSize) / cell), cj = (int)((p.Y + a.HalfSize) / cell);
            if (ci < 0 || cj < 0 || ci >= n || cj >= n) continue;
            int green = 0, all = 0;
            for (int k = 0; k < 16; k++)
            {
                float ang = k * Mathf.PI / 8f;
                int ii = (int)((p.X + Mathf.Cos(ang) * (r + 1.5f) + a.HalfSize) / cell), jj = (int)((p.Y + Mathf.Sin(ang) * (r + 1.5f) + a.HalfSize) / cell);
                if (ii < 0 || jj < 0 || ii >= n || jj >= n) continue;
                all++; if (orig[jj * n + ii] == AerialPhoto.Green) green++;
            }
            if (all > 0 && green >= all * 0.6f) continue;
            int rc = Mathf.CeilToInt(r / cell);
            for (int dj = -rc; dj <= rc; dj++)
                for (int di = -rc; di <= rc; di++)
                {
                    int ii = ci + di, jj = cj + dj;
                    if (ii < 0 || jj < 0 || ii >= n || jj >= n || (di * di + dj * dj) * cell * cell > r * r) continue;
                    if (a.Classes[jj * n + ii] == AerialPhoto.Green) { a.Classes[jj * n + ii] = AerialPhoto.Paved; cleared++; }
                }
        }
        Debug.Log($"OsmMap: {cleared} ground cells under {trees.Count} trees' crowns taken off the lawns");
    }

    /// <summary>
    /// The cells of one ground class as a mesh just above the ground (<paramref name="y"/>), by marching squares over
    /// the cells' centres: the edges run along the class's border (cut at half a cell), not in steps. Squares wholly of
    /// the class merge along a row, up to 8 m (the terrain lifts the mesh by its vertices). UVs in metres /
    /// <paramref name="uvMetres"/>.
    /// </summary>
    MeshBuilder ClassOverlay(byte cls, float y, float uvMetres)
    {
        var mb = new MeshBuilder();
        var a = Aerial; int n = a.ClassN; float cell = 2f * a.HalfSize / n;
        bool In(int i, int j) => i >= 0 && j >= 0 && i < n && j < n && a.Classes[j * n + i] == cls;
        Vector3 At(float i, float j) { float x = -a.HalfSize + (i + 0.5f) * cell, z = -a.HalfSize + (j + 0.5f) * cell; return new Vector3(x, y, z); }
        void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            int k = mb.V.Count;
            foreach (var q in new[] { p0, p1, p2 }) { mb.V.Add(q); mb.UV.Add(new Vector2(q.x, q.z) / uvMetres); }
            // (clockwise seen from above: Unity's front face points up)
            mb.T.Add(k); mb.T.Add(k + 2); mb.T.Add(k + 1);
        }
        int maxRun = Math.Max(1, Mathf.RoundToInt(8f / cell));
        var poly = new List<Vector3>(8);
        for (int j = 0; j + 1 < n; j++)
        {
            int runStart = -1;
            for (int i = 0; i + 1 <= n; i++)
            {
                bool full = i + 1 < n && In(i, j) && In(i + 1, j) && In(i + 1, j + 1) && In(i, j + 1);
                if (full && runStart < 0) runStart = i;
                if (runStart >= 0 && (!full || i - runStart + 1 >= maxRun))
                {
                    int end = full ? i + 1 : i;   // (exclusive: squares runStart .. end - 1)
                    if (end > runStart)
                    {
                        Vector3 p00 = At(runStart, j), p10 = At(end, j), p11 = At(end, j + 1), p01 = At(runStart, j + 1);
                        Tri(p00, p10, p11); Tri(p00, p11, p01);
                    }
                    runStart = full ? -1 : -1;
                    if (full) { continue; }
                }
                if (full || i + 1 >= n) continue;
                // a border square: its corners (counter-clockwise from the south-west) in or out
                bool c0 = In(i, j), c1 = In(i + 1, j), c2 = In(i + 1, j + 1), c3 = In(i, j + 1);
                if (!(c0 || c1 || c2 || c3)) continue;
                var corners = new[] { At(i, j), At(i + 1, j), At(i + 1, j + 1), At(i, j + 1) };
                var ins = new[] { c0, c1, c2, c3 };
                poly.Clear();
                for (int k = 0; k < 4; k++)
                {
                    if (ins[k]) poly.Add(corners[k]);
                    if (ins[k] != ins[(k + 1) % 4]) poly.Add((corners[k] + corners[(k + 1) % 4]) * 0.5f);
                }
                for (int k = 1; k + 1 < poly.Count; k++) Tri(poly[0], poly[k], poly[k + 1]);
            }
        }
        return mb;
    }

    /// <summary>A seamless 256 x 256 field of large-scale wear: tone that drifts by a few percent, darker stains and
    /// lighter worn patches (multiplied by two over a surface: 0.5 is no change).</summary>
    static Texture2D MacroTex() => Tex("surface_macro_variation", 256, (x, y, r) =>
    {
        float u = 2f * Mathf.PI * x / 256f, v = 2f * Mathf.PI * y / 256f, g = 0.5f;
        // (integer frequencies on the torus: it repeats without a seam)
        int[] fu = { 1, 2, 3, 5, 7, 11 }, fv = { 2, 1, 4, 3, 6, 9 };
        float[] amp = { 0.035f, 0.03f, 0.022f, 0.016f, 0.012f, 0.008f }, ph = { 0.3f, 1.7f, 2.9f, 4.1f, 5.3f, 0.9f };
        for (int k = 0; k < fu.Length; k++) g += amp[k] * Mathf.Sin(fu[k] * u + fv[k] * v + ph[k]) * Mathf.Cos(fv[k] * u - fu[k] * v + 2f * ph[k]);
        // stains: a few soft dark blots
        float s = Mathf.Sin(3 * u + 1.1f) * Mathf.Sin(2 * v + 0.6f) + Mathf.Sin(5 * u - 3 * v + 2.2f) * 0.6f;
        if (s > 0.9f) g -= (s - 0.9f) * 0.12f;
        return new Color(g, g, g, 1f);
    });

    /// <summary>
    /// Large-scale variation over a tiled surface (the detail map at a far larger scale than the texture: wear and stains
    /// over about <paramref name="period"/> metres): a scanned paving of 1.8 m repeated over a whole street showed as
    /// the same flat squares everywhere. <paramref name="tileMetres"/>: the metres one repeat of the base texture covers.
    /// </summary>
    void MacroVariation(Material m, float tileMetres, float period = 23f)
    {
        if (_noRelief) return;
        m.EnableKeyword("_DETAIL_MULX2");
        m.SetTexture("_DetailAlbedoMap", MacroTex());
        m.SetFloat("_DetailAlbedoMapScale", 1f);
        m.SetTextureScale("_DetailAlbedoMap", Vector2.one * (tileMetres / period));
    }

    Material WaterMaterial(Material plain)
    {
        if (_assets == null || _assets.FacadeTemplate == null || _noRelief) return plain;
        if (_mats.TryGetValue("Water rippled", out var made)) return made;
        // small wind ripples (a sum of waves that repeat every 2.5 m)
        var (nrm, hgt, occ) = Relief("water ripples", 256, 256, 2.5f / 256f, (x, y) =>
        {
            float u = x / 256f * 2f * Mathf.PI, v = y / 256f * 2f * Mathf.PI;
            return 0.008f * (Mathf.Sin(3 * u + 1 * v) + 0.7f * Mathf.Sin(5 * u - 4 * v + 1.3f) + 0.5f * Mathf.Sin(-2 * u + 7 * v + 2.1f) + 0.35f * Mathf.Sin(11 * u + 6 * v + 0.4f) + 0.25f * Mathf.Sin(-9 * u + 13 * v + 2.7f));
        });
        // the water shader (CoupledSim/Water): a murky body, the ripples moving with the wind, the scene's mirror image
        // (PlanarWaterReflection) by Fresnel, the sun's glint
        if (_assets.WaterTemplate != null)
        {
            var w = new Material(_assets.WaterTemplate) { name = "Water rippled" };
            w.SetTexture("_BumpMap", nrm);
            _mats["Water rippled"] = w;
            return w;
        }
        var m = Mat("Water rippled", new Color(0.07f, 0.11f, 0.1f), 0.93f);
        m.shader = _assets.FacadeTemplate.shader;
        m.SetColor("_BaseColor", new Color(0.07f, 0.11f, 0.1f));
        m.SetFloat("_Smoothness", 0.93f);
        m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 0.6f); m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_ParallaxMap", hgt); m.SetFloat("_Parallax", 0f); m.EnableKeyword("_PARALLAXMAP");
        m.SetTexture("_OcclusionMap", occ); m.SetFloat("_OcclusionStrength", 0f); m.EnableKeyword("_OCCLUSIONMAP");
        m.SetTextureScale("_BaseMap", Vector2.one * 4f);   // (URP's maps share the base map's tiling: 2.5 m on the water's 10 m UVs)
        m.SetColor("_EmissionColor", Color.black); m.EnableKeyword("_EMISSION");
        return m;
    }

    /// <summary>Adds <paramref name="src"/>'s triangles to <paramref name="dst"/>, without the parts over water (split
    /// down to 1.5 m, each piece kept where its centre is dry).</summary>
    static void AppendOffWater(MeshBuilder dst, MeshBuilder src, WaterIndex water)
    {
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, int depth)
        {
            float longest = Math.Max(Vector3.Distance(a, b), Math.Max(Vector3.Distance(b, c), Vector3.Distance(c, a)));
            var centre = (a + b + c) / 3f;
            if (longest > 1.5f && depth < 10)
            {
                // the whole triangle dry or wet (its corners, edges' middles and centre all alike): kept whole or left out,
                // however big (splitting dry lawns made Eindhoven's grass a mesh of millions of vertices)
                bool W(Vector3 q) => water.Inside(new V2(q.x, q.z));
                bool wm = W(centre);
                if (W(a) == wm && W(b) == wm && W(c) == wm && W((a + b) / 2f) == wm && W((b + c) / 2f) == wm && W((c + a) / 2f) == wm)
                { if (!wm) Add(a, b, c, ua, ub, uc); return; }
                // split the longest edge
                if (Vector3.Distance(a, b) >= longest - 1e-4f) { var m = (a + b) / 2f; var um = (ua + ub) / 2f; Tri(a, m, c, ua, um, uc, depth + 1); Tri(m, b, c, um, ub, uc, depth + 1); }
                else if (Vector3.Distance(b, c) >= longest - 1e-4f) { var m = (b + c) / 2f; var um = (ub + uc) / 2f; Tri(a, b, m, ua, ub, um, depth + 1); Tri(a, m, c, ua, um, uc, depth + 1); }
                else { var m = (c + a) / 2f; var um = (uc + ua) / 2f; Tri(a, b, m, ua, ub, um, depth + 1); Tri(m, b, c, um, ub, uc, depth + 1); }
                return;
            }
            if (!water.Inside(new V2(centre.x, centre.z))) Add(a, b, c, ua, ub, uc);
        }
        void Add(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int i = dst.V.Count;
            dst.V.Add(a); dst.V.Add(b); dst.V.Add(c); dst.UV.Add(ua); dst.UV.Add(ub); dst.UV.Add(uc);
            dst.T.Add(i); dst.T.Add(i + 1); dst.T.Add(i + 2);
        }
        for (int t = 0; t + 2 < src.T.Count; t += 3)
        {
            int i0 = src.T[t], i1 = src.T[t + 1], i2 = src.T[t + 2];
            Tri(src.V[i0], src.V[i1], src.V[i2], src.UV[i0], src.UV[i1], src.UV[i2], 0);
        }
    }

    /// <summary>The water outlines' edges by 10 m cell, for the ground's holes.</summary>
    sealed class WaterIndex
    {
        readonly List<List<V2>> _polys;
        readonly Dictionary<(int, int), List<(V2 a, V2 b)>> _cells = new Dictionary<(int, int), List<(V2, V2)>>();
        readonly List<(float x0, float z0, float x1, float z1)> _boxes = new List<(float, float, float, float)>();
        const float Cell = 10f;

        public WaterIndex(List<List<V2>> polys)
        {
            _polys = polys;
            foreach (var poly in polys)
                _boxes.Add((poly.Min(q => q.X), poly.Min(q => q.Y), poly.Max(q => q.X), poly.Max(q => q.Y)));
            foreach (var poly in polys)
            {
                for (int i = 0; i < poly.Count; i++)
                {
                    var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                    if (!IsShore(a, b)) continue;   // between two water areas (a canal mapped in pieces): not a shore
                    int cx0 = (int)Math.Floor(Math.Min(a.X, b.X) / Cell), cx1 = (int)Math.Floor(Math.Max(a.X, b.X) / Cell);
                    int cz0 = (int)Math.Floor(Math.Min(a.Y, b.Y) / Cell), cz1 = (int)Math.Floor(Math.Max(a.Y, b.Y) / Cell);
                    for (int x = cx0; x <= cx1; x++) for (int z = cz0; z <= cz1; z++)
                    {
                        if (!_cells.TryGetValue((x, z), out var l)) _cells[(x, z)] = l = new List<(V2, V2)>();
                        l.Add((a, b));
                    }
                }
            }
        }

        public bool Any => _polys.Count > 0;

        /// <summary>Whether a piece of a water outline has land on one side (not water on both: canals are often
        /// mapped as several areas that touch or overlap).</summary>
        public bool IsShore(V2 a, V2 b)
        {
            var d = b - a; float l = d.Length(); if (l < 1e-4f) return false;
            var n = new V2(-d.Y, d.X) * (0.3f / l); var m = (a + b) / 2f;
            return !(Inside(m + n) && Inside(m - n));
        }

        /// <summary>The nearest point of a water outline within <paramref name="radius"/> m.</summary>
        public bool NearestShore(V2 p, float radius, out V2 shore)
        {
            shore = p; float best = float.MaxValue;
            foreach (var (a, b) in EdgesIn(p.X - radius, p.Y - radius, p.X + radius, p.Y + radius))
            {
                var ab = b - a; float l2 = ab.LengthSquared(); if (l2 < 1e-6f) continue;
                float u = Math.Max(0f, Math.Min(1f, V2.Dot(p - a, ab) / l2));
                var c = a + ab * u; float d = V2.DistanceSquared(p, c);
                if (d < best) { best = d; shore = c; }
            }
            return best <= radius * radius;
        }

        /// <summary>The edges that may lie in a rectangle.</summary>
        public List<(V2 a, V2 b)> EdgesIn(float x0, float z0, float x1, float z1)
        {
            var res = new List<(V2, V2)>();
            for (int x = (int)Math.Floor(x0 / Cell); x <= (int)Math.Floor(x1 / Cell); x++)
                for (int z = (int)Math.Floor(z0 / Cell); z <= (int)Math.Floor(z1 / Cell); z++)
                    if (_cells.TryGetValue((x, z), out var l)) res.AddRange(l);
            return res;
        }

        public bool Inside(V2 p)
        {
            for (int i = 0; i < _polys.Count; i++)
            {
                var bx = _boxes[i];
                if (p.X < bx.x0 || p.X > bx.x1 || p.Y < bx.z0 || p.Y > bx.z1) continue;
                if (Geometry2D.PointInPolygon(p, _polys[i])) return true;
            }
            return false;
        }
    }

    /// <summary>The surroundings (6 km) from the far aerial photo, just under the map's ground, for distant and high views.</summary>
    void EmitSurroundings(Transform world)
    {
        if (Aerial == null || Aerial.FarTexture == null) return;
        var far = new MeshBuilder();
        float fh = Aerial.FarHalfSize; int n = 12; float fs = 2 * fh / n;
        Vector2 FarUv(float x, float z) => new Vector2((x + fh) / (2 * fh), (z + fh) / (2 * fh));
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float x0 = -fh + i * fs, x1 = x0 + fs, z0 = -fh + j * fs, z1 = z0 + fs;
                far.Quad(new Vector3(x0, -0.1f, z0), new Vector3(x0, -0.1f, z1), new Vector3(x1, -0.1f, z1), new Vector3(x1, -0.1f, z0),
                    FarUv(x0, z0), FarUv(x0, z1), FarUv(x1, z1), FarUv(x1, z0));
            }
        var go = Emit(world, "Surroundings aerial photo", far, Mat("Surroundings aerial photo", Color.white, 0.05f, Aerial.FarTexture), false);
        if (go != null) go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    static void CullWhenSmall(GameObject go, float screenFraction)
    {
        if (!go.TryGetComponent<LODGroup>(out var lod)) lod = go.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(screenFraction, go.GetComponentsInChildren<Renderer>(true)) });
        lod.RecalculateBounds();
    }


    /// <summary>The RVV code of a Dutch sign (with its number or arrow) as one of the signs drawn here; false if none.</summary>
    public static bool RvvKind(string code, string text, out TrafficSignKind kind, out int limit)
    {
        limit = 0; kind = TrafficSignKind.Stop;
        int.TryParse(new string((text ?? "").TakeWhile(char.IsDigit).ToArray()), out var number);
        switch ((code ?? "").ToUpperInvariant())
        {
            case "A1": kind = TrafficSignKind.SpeedLimit; limit = number > 0 ? number : 50; return true;
            case "A2": kind = TrafficSignKind.EndSpeedLimit; limit = number > 0 ? number : 50; return true;
            case "B1": kind = TrafficSignKind.PriorityRoad; return true;
            case "B6": kind = TrafficSignKind.GiveWay; return true;
            case "B7": kind = TrafficSignKind.Stop; return true;
            case "C1": kind = TrafficSignKind.ClosedToVehicles; return true;
            case "C2": kind = TrafficSignKind.NoEntry; return true;
            case "C3": kind = TrafficSignKind.NoEntry; return true;
            case "D2": kind = (text ?? "").ToUpperInvariant().StartsWith("L") ? TrafficSignKind.KeepLeftDown : TrafficSignKind.KeepRightDown; return true;
            case "D4": kind = TrafficSignKind.AheadOnly; return true;
            case "D5": kind = (text ?? "").ToUpperInvariant().StartsWith("L") ? TrafficSignKind.TurnLeft : TrafficSignKind.TurnRight; return true;
            case "D6": kind = TrafficSignKind.AheadOrRight; return true;
            case "E1": kind = TrafficSignKind.NoParking; return true;
            case "E2": kind = TrafficSignKind.NoStopping; return true;
            case "E4": kind = TrafficSignKind.Parking; return true;
            case "E6": kind = TrafficSignKind.DisabledParking; return true;
            case "E9": kind = TrafficSignKind.PermitParking; return true;
            case "G5": kind = TrafficSignKind.ResidentialZone; return true;
            case "G7": kind = TrafficSignKind.FootPath; return true;
            case "G11": kind = TrafficSignKind.CyclePath; return true;
            case "G12A": kind = TrafficSignKind.MopedCyclePath; return true;
            case "H1": return false;
            case "J21": kind = TrafficSignKind.Children; return true;
            case "L2": kind = TrafficSignKind.PedestrianCrossing; return true;
            case "L8": kind = TrafficSignKind.DeadEnd; return true;
            case "F1": return false;
        }
        return false;
    }

    /// <summary>Signs from the map's register ({map}.signs.csv: lat, lon, bearing = the direction of the traffic they are
    /// for, RVV code, number/arrow): at their real place (moved to the road edge if they stood on the carriageway),
    /// facing that traffic along the nearest road; signs on one pole stacked. False when the map has no register.</summary>
    bool PlaceRegisteredSigns(Transform signs, List<(OsmWay way, List<V2> line, float width)> roadWays)
    {
        var reg = MapExtras.Load(_map.Name, "signs", _proj, out var credit);
        if (reg == null) return false;
        var poles = new Dictionary<(int, int), int>();
        int skipped = 0;
        foreach (var r in reg)
        {
            if (SignCount >= MaxRegisteredSigns) break;
            if (!RvvKind(r["code"], r["text"], out var kind, out var limit)) { skipped++; continue; }
            var p = new V2(r.X, r.Z);
            if (!Inside(p)) continue;
            // traffic direction from the bearing (compass: 0 north = +z, 90 east = +x); the face looks back at it
            float brg = r.Float("bearing", 0f) * Mathf.Deg2Rad;
            var travel = new V2(Mathf.Sin(brg), Mathf.Cos(brg));
            var (near, dist) = NearestRoad(p, roadWays, out var along, out float width);
            if (dist < 30f && along.LengthSquared() > 1e-6f)
            {
                along = V2.Normalize(along);
                travel = V2.Dot(along, travel) >= 0f ? along : -along;
                // on the carriageway (data a little off): to the edge on the side it is nearer
                if (dist < width / 2f + 0.3f)
                {
                    var off = p - near;
                    var side = off.LengthSquared() > 1e-6f ? V2.Normalize(off) : new V2(travel.Y, -travel.X);
                    p = near + side * (width / 2f + 0.7f);
                }
                // in the first 2.5 m beside it (on the pavement, a few metres off in the data): just behind the kerb,
                // where such signs stand, not in the middle of the footway (a pedestrian walked into one on De Zaale)
                else if (dist < width / 2f + 2.5f)
                {
                    var off = p - near;
                    if (off.LengthSquared() > 1e-6f) p = near + V2.Normalize(off) * (width / 2f + 0.5f);
                }
            }
            var cell = ((int)Math.Round(p.X / 0.6f), (int)Math.Round(p.Y / 0.6f));
            poles.TryGetValue(cell, out var stacked);
            poles[cell] = stacked + 1;
            var go = new GameObject($"Sign {kind}{(limit > 0 ? " " + limit : "")} ({r["code"]})");
            go.transform.SetParent(signs, false);
            go.transform.SetPositionAndRotation(P(p, PavementHeight), Quaternion.LookRotation(new Vector3(-travel.X, 0f, -travel.Y), Vector3.up));
            go.SetActive(false);
            var s = go.AddComponent<TrafficSign>();
            s.Kind = kind; s.SpeedLimit = limit; s.Size = 0.6f;
            s.Height = 2.2f + stacked * 0.68f;          // the next sign on the same pole above the first
            go.SetActive(true);
            SignCount++;
        }
        RealDataCredits.Add($"{SignCount} traffic signs ({skipped} of kinds not drawn): {credit}");
        return true;
    }
    const int MaxRegisteredSigns = 1500;

    /// <summary>The nearest point on a road centre line, its distance, the road's direction there and width.</summary>
    static (V2 point, float dist) NearestRoad(V2 p, List<(OsmWay way, List<V2> line, float width)> roads, out V2 along, out float width)
    {
        V2 best = p; float bestD = float.MaxValue; along = default; width = 6f;
        foreach (var (_, line, wd) in roads)
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i]; var ab = line[i + 1] - a;
                float t = ab.LengthSquared() > 1e-6f ? Mathf.Clamp01(V2.Dot(p - a, ab) / ab.LengthSquared()) : 0f;
                var q = a + ab * t;
                float d = V2.DistanceSquared(p, q);
                if (d < bestD) { bestD = d; best = q; along = ab; width = wd; }
            }
        return (best, Mathf.Sqrt(bestD));
    }

    /// <summary>Sources of real-world data used for this map (logged and shown in the build report).</summary>
    public readonly List<string> RealDataCredits = new List<string>();

    /// <summary>The surfaces built by this generator (each with its own mesh), as opposed to placed prefabs.</summary>
    public HashSet<GameObject> EmittedMeshes => _emitted;
    static readonly HashSet<GameObject> _emitted = new HashSet<GameObject>();

    /// <summary>The map's terrain (null: flat). Ground and ways get finer vertices so the lift follows it.</summary>
    public TerrainHeight Terrain { get; private set; }
    // (8000: a register with the canopy map's crowns added (Tools/canopy.py) has some 27,000 trees in Eindhoven's corridors;
    // the nearest the centre first, so the TU/e campus, 1.1 km out, has its trees)
    const int MaxRealTrees = 8000;

    static readonly Dictionary<GameObject, float> _prefabHeights = new Dictionary<GameObject, float>();

    /// <summary>Height of a prefab's drawn parts (measured once).</summary>
    static readonly Dictionary<GameObject, float> _prefabCrowns = new Dictionary<GameObject, float>();

    /// <summary>The width of a tree model's crown in metres (the mean of its extent along x and z), at its own scale.</summary>
    static float PrefabCrownWidth(GameObject prefab, float fallback)
    {
        if (_prefabCrowns.TryGetValue(prefab, out var w)) return w;
        var go = UnityEngine.Object.Instantiate(prefab);
        go.SetActive(true);
        w = fallback;
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length > 0)
        {
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            w = Mathf.Max(0.5f, (b.size.x + b.size.z) * 0.5f);
        }
        UnityEngine.Object.DestroyImmediate(go);
        return _prefabCrowns[prefab] = w;
    }

    static float PrefabHeight(GameObject prefab, float fallback)
    {
        if (_prefabHeights.TryGetValue(prefab, out var h)) return h;
        var go = UnityEngine.Object.Instantiate(prefab);
        go.SetActive(true);
        var rs = go.GetComponentsInChildren<Renderer>(true);
        h = fallback;
        if (rs.Length > 0)
        {
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            h = Mathf.Max(0.5f, b.max.y - go.transform.position.y);
        }
        UnityEngine.Object.DestroyImmediate(go);
        return _prefabHeights[prefab] = h;
    }

    /// <summary>The nearest point on a road centre line and its distance.</summary>
    static (V2 point, float dist) NearestRoad(V2 p, List<(OsmWay way, List<V2> line, float width)> roads)
    {
        V2 best = p; float bestD = float.MaxValue;
        foreach (var (_, line, _) in roads)
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i]; var ab = line[i + 1] - a;
                float t = ab.LengthSquared() > 1e-6f ? Mathf.Clamp01(V2.Dot(p - a, ab) / ab.LengthSquared()) : 0f;
                var q = a + ab * t;
                float d = V2.DistanceSquared(p, q);
                if (d < bestD) { bestD = d; best = q; }
            }
        return (best, Mathf.Sqrt(bestD));
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
        // the national register of traffic signs, where there is one (the Netherlands: NDW): every real sign
        if (PlaceRegisteredSigns(signs, roadWays)) return;
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
            int owDir = OneWayDir(w);
            bool oneWay = owDir != 0;
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
                    bool forward = owDir > 0 || (owDir == 0 && (dirTag == "forward" || (dirTag != "backward" && i >= ids.Count / 2)));
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
                    if (owDir >= 0) Sign(TrafficSignKind.PedestrianCrossing, p - V2.Normalize(dir) * 2.5f, dir, width);
                    if (owDir <= 0) Sign(TrafficSignKind.PedestrianCrossing, p + V2.Normalize(dir) * 2.5f, -dir, width);
                }
            }
            int limit = SpeedNumber(w.Tag("maxspeed"));
            if (limit > 0 && total > 120f)
            {
                if (owDir >= 0) { var (a, da) = Geometry2D.PointAt(line, 12f); Sign(TrafficSignKind.SpeedLimit, a, da, width, limit); }
                if (owDir <= 0) { var (b, db) = Geometry2D.PointAt(line, total - 12f); Sign(TrafficSignKind.SpeedLimit, b, -db, width, limit); }
            }
            if (oneWay && total > 30f && w.Tag("junction") != "roundabout" && !(w.Tag("highway") ?? "").StartsWith("motorway"))
            {
                if (owDir > 0)
                {
                    var (a, da) = Geometry2D.PointAt(line, 6f);
                    Sign(TrafficSignKind.OneWay, a, da, width);
                    var (b, db) = Geometry2D.PointAt(line, total - 3f);
                    Sign(TrafficSignKind.NoEntry, b, -db, width);
                }
                else
                {
                    var (b, db) = Geometry2D.PointAt(line, total - 6f);
                    Sign(TrafficSignKind.OneWay, b, -db, width);
                    var (a, da) = Geometry2D.PointAt(line, 3f);
                    Sign(TrafficSignKind.NoEntry, a, da, width);
                }
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
        var glass = Mat("Shelter glass", new Color(0.62f, 0.7f, 0.72f, 0.22f), 0.95f);
        if (glass.GetFloat("_Surface") < 0.5f)
        {
            // see-through glass (URP Lit, transparent)
            glass.SetFloat("_Surface", 1f); glass.SetFloat("_Blend", 0f);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        var wood = Mat("Terrace furniture", new Color(0.42f, 0.3f, 0.2f), 0.3f);
        var brickWall = Mat("Garden wall", Color.white, 0.1f, Tex("garden_wall", 128, (x, y, r) => (y % 16 < 2 || (x + (y / 16 % 2) * 16) % 32 < 2) ? new Color(0.5f, 0.47f, 0.43f) : Noise(new Color(0.5f, 0.25f, 0.18f), r, 0.25f)));
        MeshBuilder bollards = new MeshBuilder(), racks = new MeshBuilder(), hedges = new MeshBuilder(), walls = new MeshBuilder(),
            fences = new MeshBuilder(), containers = new MeshBuilder(), containersPaper = new MeshBuilder(), containersPlastic = new MeshBuilder(), shelters = new MeshBuilder(), shelterFrames = new MeshBuilder(), shelterAds = new MeshBuilder(), furniture = new MeshBuilder(), cabinets = new MeshBuilder(), busSigns = new MeshBuilder();
        var assets = _assets;

        GameObject Place(GameObject prefab, V2 p, float yaw, string name)
        {
            if (prefab == null) return null;
            // keep the prefab's own rotation (models imported lying on their back are turned upright there)
            var go = UnityEngine.Object.Instantiate(prefab, new Vector3(p.X, GroundAt(p, PavementHeight), p.Y), Quaternion.Euler(0, yaw, 0) * prefab.transform.rotation, props);
            go.name = name;
            go.isStatic = true;
            PropCount++;
            return go;
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
                // (the whole row, bicycles 1.9 m long across it, at least 0.3 m off the carriageway: moved away from the road
                // by the least that clears it, else left out; at a kerb the bicycles stood 1 m into the road)
                bool Clear(V2 o)
                {
                    for (int i = 0; i < capacity; i++)
                    {
                        var q = o + dir * ((i - capacity / 2f) * 0.75f);
                        if (InCarriageway(q + right * 1.25f, 0.3f, 0f, out _) || InCarriageway(q - right * 0.7f, 0.3f, 0f, out _) || InCarriageway(q, 0.3f, 0f, out _)) return false;
                    }
                    return true;
                }
                if (!Clear(p))
                {
                    InCarriageway(p, 50f, 0f, out var edgeAt);
                    // (the edge point lies on p's side of the road: out from the road is from the edge to p, or from p to the
                    // edge when p is on the carriageway)
                    var away = InCarriageway(p, 0f, 0f, out _) ? edgeAt - p : p - edgeAt;
                    away = away.LengthSquared() > 1e-4f ? V2.Normalize(away) : right;
                    bool found = false;
                    for (float m = 0.25f; m <= 3f && !found; m += 0.25f)
                        foreach (var dd in new[] { away, right, -right })
                            if (!found && Clear(p + dd * m)) { p += dd * m; found = true; }
                    if (!found) { RacksLeftOut++; continue; }
                    RacksMoved++;
                }
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
                    // a steel frame (roof and four posts), glass panes at the back and one end, an advertising panel at the
                    // other end (lit at night)
                    shelterFrames.Box(c3 + Vector3.up * 2.3f, new Vector3(1.6f, 0.08f, 3.2f), along);                       // roof
                    foreach (var (px, pz) in new[] { (0.72f, 1.52f), (0.72f, -1.52f), (-0.72f, 1.52f), (-0.72f, -1.52f) })
                        shelterFrames.Box(c3 + fwd * new Vector3(px, 0, pz), new Vector3(0.06f, 2.3f, 0.06f), along);
                    shelters.Box(c3 + fwd * new Vector3(0.75f, 0.1f, 0), new Vector3(0.02f, 2.1f, 3.0f), along);              // back pane
                    shelters.Box(c3 + fwd * new Vector3(0.05f, 0.1f, 1.55f), new Vector3(1.3f, 2.1f, 0.02f), along);          // end pane
                    shelterAds.Box(c3 + fwd * new Vector3(0.05f, 0.3f, -1.55f), new Vector3(1.25f, 1.85f, 0.08f), along);    // advert
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
                // a row of three, one per kind of waste (glass, paper, packaging), each in its own colour
                for (int k = 0; k < 3; k++) (k == 0 ? containers : k == 1 ? containersPaper : containersPlastic).Box(p3 + Quaternion.Euler(0, along, 0) * new Vector3(0, 0, (k - 1) * 1.7f), new Vector3(1.4f, 1.6f, 1.5f), along);
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
                // as real car parks are laid out: a 6 m aisle, then two rows of 5 m bays back to back facing the aisles on
                // either side, and so on (5.2 m and 6.5 m between rows left aisles of 1.5 m: the cars at the back could not
                // get out); bays 2.5 m wide; a car only where its whole footprint lies in the lot, nose to its aisle
                bool Fits(V2 q, V2 facing)
                {
                    var side = new V2(-facing.Y, facing.X);
                    foreach (var (f, sd) in new[] { (2.3f, 1.0f), (2.3f, -1.0f), (-2.3f, 1.0f), (-2.3f, -1.0f) })
                        if (!Geometry2D.PointInPolygon(q + facing * f + side * sd, poly)) return false;
                    return true;
                }
                const float Aisle = 6f, Bay = 5f, BayWidth = 2.5f;
                for (float v0 = minB + Aisle; v0 + Bay <= maxB; v0 += 2f * Bay + Aisle)
                {
                    // (the second row only with an aisle behind it, else the lot's far edge closes it in)
                    bool second = v0 + 2f * Bay + Aisle <= maxB + 0.5f;
                    for (int r2 = 0; r2 < (second ? 2 : 1); r2++)
                    {
                        float v = v0 + Bay * (r2 + 0.5f);
                        var facing = r2 == 0 ? -ay : ay;   // (towards the aisle on its side)
                        for (float u = minA + BayWidth * 0.6f; u < maxA - BayWidth * 0.5f; u += BayWidth)
                        {
                            var q = ax * u + ay * v;
                            if (!Fits(q, facing) || rng.NextDouble() >= 0.75) continue;
                            // (not on a road through or along the lot: cars stood on an alley through a car park in
                            // Montreal, and the traffic on it stood behind them)
                            if (OnCarRoad(q, 1.3f)) continue;
                            // (most drive in nose first; some reverse in)
                            Car(q, Yaw(rng.NextDouble() < 0.8 ? -facing : facing));
                        }
                    }
                }
            }
            else if (amenity == "bicycle_parking" && w.IsClosed && w.Tag("building") == null
                && w.Tag("bicycle_parking") != "building" && w.Tag("bicycle_parking") != "underground" && w.Tag("location") != "underground")
            {
                // a bicycle park mapped as an area (a campus or a station has them by the hundred): rows of bicycles 1.9 m
                // long facing aisles of 1.6 m, 0.7 m apart, a low rail along each row; each bicycle wholly in the area,
                // about three quarters of the places taken (of its capacity where mapped), at most 300
                var poly = Line(w); if (poly.Count < 4 || !poly.Any(Inside)) continue;
                poly.RemoveAt(poly.Count - 1);
                int longest = 0; float best = 0;
                for (int i = 0; i < poly.Count; i++) { float l = V2.Distance(poly[i], poly[(i + 1) % poly.Count]); if (l > best) { best = l; longest = i; } }
                var ax = V2.Normalize(poly[(longest + 1) % poly.Count] - poly[longest]); var ay = new V2(-ax.Y, ax.X);
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                foreach (var q in poly) { float u = V2.Dot(q, ax), v = V2.Dot(q, ay); minA = Math.Min(minA, u); maxA = Math.Max(maxA, u); minB = Math.Min(minB, v); maxB = Math.Max(maxB, v); }
                int cap = int.TryParse(w.Tag("capacity"), out var cc) && cc > 0 ? cc : int.MaxValue, placed = 0;
                for (float v = minB + 1.0f; v + 0.95f <= maxB && placed < Math.Min(300, cap); v += 3.5f)
                {
                    float rowStart = float.NaN, rowEnd = float.NaN;
                    for (float u = minA + 0.4f; u < maxA - 0.3f && placed < Math.Min(300, cap); u += 0.7f)
                    {
                        var q = ax * u + ay * v;
                        if (!Geometry2D.PointInPolygon(q + ay * 0.95f, poly) || !Geometry2D.PointInPolygon(q - ay * 0.95f, poly)) continue;
                        if (float.IsNaN(rowStart)) rowStart = u; rowEnd = u;
                        if (rng.NextDouble() < 0.75) { Bike(q, Yaw(ay) + (rng.NextDouble() < 0.5 ? 0 : 180)); placed++; }
                    }
                    if (!float.IsNaN(rowStart) && rowEnd - rowStart > 0.5f)
                    {
                        var a3 = ax * ((rowStart + rowEnd) / 2f) + ay * (v - 0.45f);
                        var p3r = new Vector3(a3.X, GroundAt(a3, 0f), a3.Y);
                        racks.Box(p3r, new Vector3(0.05f, 0.35f, rowEnd - rowStart + 0.5f), Yaw(ax));
                    }
                }
                if (placed > 0) BikeParkAreas++;
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
                    if (!StreetParking(w, side)) continue;
                    var line = Line(w); if (line.Count < 2 || !line.Any(Inside)) continue;
                    float width = RoadWidth(w), total = Geometry2D.Length(line);
                    // on the road where the lanes still fit beside the cars, else half on or on the kerb (MapRoutes.ParkDepth)
                    float depth = CoupledSim.Maps.MapRoutes.ParkDepth(width, IsOneWay(w), (StreetParking(w, -1) ? 1 : 0) + (StreetParking(w, 1) ? 1 : 0));
                    for (float d = 8f; d < total - 8f; d += 6.5f)
                    {
                        if (rng.NextDouble() > 0.6) continue;
                        var (pt, dir) = Geometry2D.PointAt(line, d);
                        var n = new V2(dir.Y, -dir.X) * side;
                        Car(pt + n * (width / 2f + 1.1f - depth), Yaw(dir));
                    }
                }
            }
        }
        if (assets != null && assets.ParkedCars != null && assets.ParkedCars.Length > 0)
            foreach (var c in cars.OrderBy(c => c.p.Length()).Take(MaxParkedCars))
            {
                var prefab = assets.ParkedCars[rng.Next(assets.ParkedCars.Length)];
                var car = Place(prefab, c.p, c.yaw, "Parked car");
                _parkedCars.Add((car, c.yaw)); ParkedCarCount++;
                NumberPlates.Fit(car, prefab.name, NumberPlates.For(_map.Country), new System.Random(ParkedCarCount * 7919 + 17), Quaternion.Euler(0f, c.yaw, 0f));
            }
        if (assets != null && assets.ParkedBicycles != null && assets.ParkedBicycles.Length > 0)
            foreach (var b in bikes.OrderBy(b => b.p.Length()).Take(MaxParkedBicycles))
            { Place(assets.ParkedBicycles[rng.Next(assets.ParkedBicycles.Length)], b.p, b.yaw, "Parked bicycle"); ParkedBicycleCount++; }
        if (BikeParkAreas > 0) Debug.Log($"OsmMap: {BikeParkAreas} bicycle parks mapped as areas filled with rows of bicycles");
        if (RacksMoved + RacksLeftOut > 0) Debug.Log($"OsmMap: {RacksMoved} bicycle racks moved off the carriageway, {RacksLeftOut} left out (no room beside it)");
        Emit(props, "Bollards", bollards, metal, true);
        Emit(props, "Bicycle racks", racks, metal, false);
        Emit(props, "Hedges", hedges, green, true);
        Emit(props, "Walls", walls, brickWall, true);
        Emit(props, "Fences", fences, metal, true);
        Emit(props, "Recycling containers", containers, containerMat, true);
        Emit(props, "Recycling containers paper", containersPaper, Mat("Containers paper", new Color(0.12f, 0.24f, 0.5f), 0.3f), true);
        Emit(props, "Recycling containers packaging", containersPlastic, Mat("Containers packaging", new Color(0.82f, 0.66f, 0.12f), 0.3f), true);
        var shelterGo = Emit(props, "Bus shelters", shelters, glass, true);
        if (shelterGo != null) shelterGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Emit(props, "Bus shelter frames", shelterFrames, Mat("Shelter frame", new Color(0.2f, 0.21f, 0.22f), 0.45f), true);
        Emit(props, "Bus shelter adverts", shelterAds, AdvertMaterial(), false);
        Emit(props, "Terrace furniture", furniture, wood, true);
        Emit(props, "Street cabinets", cabinets, metal, true);
        Emit(props, "Bus stop signs", busSigns, Mat("Bus stop sign", new Color(0.05f, 0.25f, 0.6f), 0.5f), false);
    }

    // ------------------------------------------------------------------ real 3D buildings (3D BAG)

    static Texture2D RoofTiles(string name, Color baseColor) => Tex(name, 256, (x, y, r) =>
    {
        // pantiles (the wavy tiles of most Dutch and Flemish roofs): 8 rows and 8 tiles a row, each tile a wave across
        // with its lit crest and shaded trough, a dark shadow line under the row above, rows offset by half a tile,
        // and every tile a little lighter or darker (weathering, replaced tiles)
        int row = y / 32, ty = y % 32, col = (x + (row % 2) * 16) / 32, tx = (x + (row % 2) * 16) % 32;
        var c = Noise(baseColor, r, 0.1f);
        uint hsh = (uint)(row * 73856093 ^ (col & 7) * 19349663);
        c *= 0.86f + 0.28f * ((hsh % 1000) / 1000f);
        float wave = Mathf.Sin(tx / 32f * Mathf.PI * 2f);
        c *= 0.86f + 0.16f * wave;
        if (ty < 3) c *= 0.5f + 0.12f * ty;          // the shadow of the row above
        else c *= 0.92f + 0.1f * ty / 32f;           // a tile is lighter towards its lower edge
        if (tx == 31 || tx == 0) c *= 0.8f;           // the overlap between tiles
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
                    // (not a mapped passage through the building, cut through it by itself: De Zaale under Atlas lifted
                    // the whole of Atlas onto a 5 m open ground floor)
                    if (InPassage(p.x, p.z)) continue;
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

    void AddRealBuildings(List<Bag3dBuildings.Building> bag, MeshBuilder[] facadeMbs, int[] palette, MeshBuilder flatRoofs, MeshBuilder[] tileRoofs,
        MeshBuilder[] groundMbs, List<V2> shopPoints, MeshBuilder cornices)
    {
        // churches (OpenStreetMap's building or amenity tags, around a real building's middle): church windows, no shop fronts
        var churches = _data.Ways.Where(w => w.IsClosed && (w.Tag("building") == "church" || w.Tag("building") == "cathedral" || w.Tag("building") == "chapel" || w.Tag("amenity") == "place_of_worship"))
            .Select(w => Line(w).Select(q => new System.Numerics.Vector2(q.X, q.Y)).ToList()).Where(l => l.Count >= 4).ToList();
        int churchCount = 0, downtownLow = 0;
        // the buildings' heights by 40 m cell: a low building among towers (midtown New York) is a shop or an office's
        // podium, not a brick house with sash windows
        var cellTops = new Dictionary<long, List<float>>();
        long CellOf(float x, float z) => ((long)Mathf.FloorToInt(x / 40f) << 32) ^ (uint)Mathf.FloorToInt(z / 40f);
        var tops = new float[bag.Count]; var mids = new Vector2[bag.Count];
        for (int bi = 0; bi < bag.Count; bi++)
        {
            float t = 0f, sx = 0f, sz = 0f;
            foreach (var v in bag[bi].V) { t = Math.Max(t, v.y); sx += v.x; sz += v.z; }
            tops[bi] = t; mids[bi] = bag[bi].V.Length > 0 ? new Vector2(sx, sz) / bag[bi].V.Length : Vector2.zero;
            var key = CellOf(mids[bi].x, mids[bi].y);
            if (!cellTops.TryGetValue(key, out var l)) cellTops[key] = l = new List<float>();
            l.Add(t);
        }
        float NeighbourMedian(Vector2 m)
        {
            var hs = new List<float>();
            int cx = Mathf.FloorToInt(m.x / 40f), cz = Mathf.FloorToInt(m.y / 40f);
            for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++)
                if (cellTops.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out var l)) hs.AddRange(l);
            if (hs.Count < 6) return 0f;
            hs.Sort(); return hs[hs.Count / 2];
        }
        for (int bi = 0; bi < bag.Count; bi++)
        {
            var b = bag[bi];
            bool inside = false;
            foreach (var v in b.V) if (Inside(new V2(v.x, v.z))) { inside = true; break; }
            if (!inside) continue;
            // older buildings are mostly brick; newer ones get the whole palette
            int pi = (int)((uint)(bi * 2654435761u) % (uint)palette.Length);
            float top = 0f; float mx = 0f, mz = 0f; int nb = 0;
            foreach (var v in b.V) { top = Math.Max(top, v.y); if (v.y < 0.5f) { mx += v.x; mz += v.z; nb++; } }
            // a column (a footprint under 3.5 m across, standing taller than a person: Atlas's tower stands on 2 m square
            // columns 8 m high, each a building of its own in the 3D BAG): plain concrete, no windows, door or cornice (they
            // came out as narrow towers with windows and a front door)
            float cx0 = float.MaxValue, cx1 = float.MinValue, cz0 = float.MaxValue, cz1 = float.MinValue;
            foreach (var v in b.V) if (v.y < 0.5f) { cx0 = Math.Min(cx0, v.x); cx1 = Math.Max(cx1, v.x); cz0 = Math.Min(cz0, v.z); cz1 = Math.Max(cz1, v.z); }
            if (nb > 0 && cx1 - cx0 < 3.5f && cz1 - cz0 < 3.5f && top > 3f)
            {
                AddTriangles(_realColumns, b.V, b.Walls, true);
                AddTriangles(flatRoofs, b.V, b.Roofs, false);
                RealColumns++;
                continue;
            }
            bool church = nb > 0 && churches.Any(c => CoupledSim.Maps.Geometry2D.PointInPolygon(new System.Numerics.Vector2(mx / nb, mz / nb), c));
            if (church) churchCount++;
            // the mapped building here (OpenStreetMap): its use, and a landmark's own façade (Atlas' curtain wall)
            var osmB = OsmBuildingFor(b.V);
            var osmType = osmB?.Tag("building");
            var landmark = LandmarkFacade(osmB);
            NoteLandmarkBuilding(osmB, b);   // (a landmark's crown, balcony or roof: its walls' line and top; a roof it replaces taken out)
            var style = StyleFor(b.Year, top, church ? "church" : osmType, (uint)(bi * 2246822519u));
            int kind = b.Year > 0 && b.Year < 1940 ? palette[pi % 3] : palette[pi];
            if (style == WindowStyle.Glass && kind < 3) kind = 3 + bi % 2;   // glass walls: plaster or concrete frames
            // (universities, offices and hospitals after the war: concrete and plaster with glass, not a brick house's
            // façade: the TU/e's campus came out in red brick with sash windows)
            bool institutional = osmType == "university" || osmType == "office" || osmType == "college" || osmType == "hospital" || osmType == "commercial" || osmType == "school";
            if (institutional && !church && (b.Year == 0 || b.Year >= 1945)) { kind = 3 + bi % 2; if (style != WindowStyle.Glass) style = bi % 3 == 0 ? WindowStyle.Glass : WindowStyle.Ribbon; }
            bool lowAmongTowers = !church && top < 15f && NeighbourMedian(mids[bi]) > 35f;
            if (lowAmongTowers) { style = bi % 2 == 0 ? WindowStyle.Ribbon : WindowStyle.Glass; kind = 3 + bi % 2; downtownLow++; }
            var wall = landmark ?? facadeMbs[kind * Styles + (int)style];
            var verts = b.V;
            // the walls of columns it stands on, where OpenStreetMap maps them as small building parts (Atlas's tower on
            // two rows of 2 m columns at each end, which the 3D BAG models with the building): plain concrete, not the
            // building's façade (a column came out as a narrow tower with curtain-wall windows and a front door)
            var walls = ColumnWalls(b, out var columnWalls);
            if (columnWalls != null) { AddTriangles(_realColumns, b.V, columnWalls, true); RealColumns++; }
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
            // ground floor below GroundFloorHeight (shop fronts where OSM has businesses), the rest as before
            var gsBase = GroundStyleFor(style, null);
            if (landmark == null && top > GroundFloorHeight + 1.5f && verts.Any(v => v.y < 0.5f))
            {
                SplitWalls(verts, walls, GroundFloorHeight, out var below, out var above);
                foreach (var poly in below)
                {
                    var c = poly.Aggregate(Vector3.zero, (acc, q) => acc + q) / poly.Length;
                    var gs = !church && (lowAmongTowers || NearShop(new V2(c.x, c.z), shopPoints)) ? GroundStyle.Shop : gsBase;
                    AddPolys(groundMbs[kind * GroundStyleCount + (int)gs], new List<Vector3[]> { poly }, true, GroundFloorHeight);
                }
                AddPolys(wall, above, true, 0f);
            }
            else AddTriangles(wall, verts, walls, true);
            if (b.RoofKind != 1 && top > 6f && landmark == null) CorniceFromWalls(cornices, verts, walls, top);
            if (Aerial != null) AddRealRoof(verts, b.Roofs, wall, flatRoofs, bi);
            else AddTriangles(b.RoofKind == 1 ? tileRoofs[bi % 3 == 0 ? 1 : 0] : flatRoofs, verts, b.Roofs, false);
            BuildingCount++;
        }
        if (churchCount > 0) Debug.Log($"OsmMap: {churchCount} real buildings are churches (OpenStreetMap): church windows");
        if (downtownLow > 0) Debug.Log($"OsmMap: {downtownLow} low buildings among towers get shopfronts and modern windows");
    }


    List<List<V2>> _columnParts;

    /// <summary>A real building's walls without those of the columns OpenStreetMap maps under it (building:parts under
    /// 3.5 m across, standing on the ground): those walls (wall triangles whose middle is within 0.6 m of such a part) in
    /// columnWalls, null if there are none.</summary>
    int[] ColumnWalls(Bag3dBuildings.Building b, out int[] columnWalls)
    {
        columnWalls = null;
        if (_columnParts == null)
        {
            _columnParts = new List<List<V2>>();
            foreach (var w in _data.Ways)
            {
                if (!w.IsClosed || w.Tag("building:part") == null || w.Tag("building") != null) continue;
                if (ParseMetres(w.Tag("min_height"), 0f) > 0.5f || (w.Tag("building:min_level") ?? "0") != "0") continue;
                var poly = Line(w);
                if (poly.Count < 4) continue;
                float sx = poly.Max(q => q.X) - poly.Min(q => q.X), sz = poly.Max(q => q.Y) - poly.Min(q => q.Y);
                if (sx < 3.5f && sz < 3.5f) _columnParts.Add(poly);
            }
        }
        if (_columnParts.Count == 0) return b.Walls;
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        foreach (var v in b.V) { x0 = Math.Min(x0, v.x); x1 = Math.Max(x1, v.x); z0 = Math.Min(z0, v.z); z1 = Math.Max(z1, v.z); }
        var here = _columnParts.Where(c => c.Any(q => q.X > x0 - 1f && q.X < x1 + 1f && q.Y > z0 - 1f && q.Y < z1 + 1f)).ToList();
        if (here.Count == 0) return b.Walls;
        var keep = new List<int>(); var cols = new List<int>();
        for (int i = 0; i + 2 < b.Walls.Length; i += 3)
        {
            var m = (b.V[b.Walls[i]] + b.V[b.Walls[i + 1]] + b.V[b.Walls[i + 2]]) / 3f;
            var q = new V2(m.x, m.z);
            bool col = here.Any(c => Geometry2D.PointInPolygon(q, c) || DistToRing(q, c) < 0.6f);
            (col ? cols : keep).AddRange(new[] { b.Walls[i], b.Walls[i + 1], b.Walls[i + 2] });
        }
        if (cols.Count == 0) return b.Walls;
        columnWalls = cols.ToArray();
        return keep.ToArray();
    }

    static float DistToRing(V2 q, List<V2> c)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < c.Count; i++)
        {
            var a = c[i]; var ab = c[i + 1] - a; float l2 = ab.LengthSquared();
            float t = l2 > 1e-6f ? Math.Clamp(V2.Dot(q - a, ab) / l2, 0f, 1f) : 0f;
            best = Math.Min(best, V2.Distance(q, a + ab * t));
        }
        return best;
    }

    /// <summary>Real buildings that are columns (see AddRealBuildings): plain concrete.</summary>
    readonly MeshBuilder _realColumns = new MeshBuilder();
    public int RealColumns;

    /// <summary>The pitched roofs of real buildings, one tile colour per building ([0] red, [1] anthracite): added to
    /// the tile roofs after the photo split, which would choose each triangle's colour on its own.</summary>
    readonly MeshBuilder[] _realTileRoofs = { new MeshBuilder(), new MeshBuilder() };
    /// <summary>Real buildings' flat roofs by roofing: [0] dark bitumen, [1] grey gravel, [2] light membrane, [3] green
    /// (sedum); UVs in metres / 2.5.</summary>
    readonly MeshBuilder[] _realFlatRoofs = { new MeshBuilder(), new MeshBuilder(), new MeshBuilder(), new MeshBuilder() };
    public readonly int[] RealFlatRoofs = new int[4];

    /// <summary>
    /// A real building's roof on a map with an aerial photo: its faces by slope. Near-flat faces (under 25°) go to the
    /// flat roofs (the photo's colours: gravel, solar panels, roof lights); pitched faces (25-72°) get tiles, one colour
    /// for the whole building (red or anthracite by the photo's colour over all its pitched faces, weighted by their
    /// area: chosen per triangle, a hip end came out grey on a red roof and looked like a loose slab); steeper faces
    /// (a mansard's lower slope, a dormer's cheek that the model calls roof) are wall (the photo from above smeared down
    /// them as white flaps).
    /// </summary>
    void AddRealRoof(Vector3[] v, int[] tris, MeshBuilder wall, MeshBuilder flat, int bi)
    {
        var flatT = new List<int>(); var pitched = new List<int>(); var steepT = new List<int>();
        float redArea = 0f, greyArea = 0f;
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            var n = Vector3.Cross(b - a, c - a);
            float area = n.magnitude * 0.5f;
            if (area < 1e-6f) continue;
            float up = Mathf.Abs(n.y) / (2f * area);
            var dst = up > 0.906f ? flatT : up > 0.309f ? pitched : steepT;   // (cos 25°, cos 72°)
            dst.Add(tris[i]); dst.Add(tris[i + 1]); dst.Add(tris[i + 2]);
            if (dst == pitched && Aerial.Covers(a))
            {
                Color.RGBToHSV(Aerial.Sample((a + b + c) / 3f), out float h, out float sat, out float val);
                if (sat > 0.18f && (h < 0.09f || h > 0.94f) && val > 0.22f) redArea += area; else greyArea += area;
            }
        }
        // (flat faces: a roofing by the photo's mean colour over them, not the photo itself, which is not a true
        // orthophoto: bits of walls, streets and shadows smeared over the roofs, and blurred seen close from a drone)
        if (flatT.Count > 0)
        {
            float fa = 0f; Color sum = Color.black;
            for (int i = 0; i + 2 < flatT.Count; i += 3)
            {
                Vector3 a = v[flatT[i]], b = v[flatT[i + 1]], c = v[flatT[i + 2]];
                if (!Aerial.Covers(a)) continue;
                float ar = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                sum += Aerial.Sample((a + b + c) / 3f) * ar; fa += ar;
            }
            int kind = 1;
            if (fa > 0f)
            {
                var m = sum / fa; float sm = m.r + m.g + m.b + 1e-3f, exg = (2f * m.g - m.r - m.b) / sm, lum = 0.3f * m.r + 0.59f * m.g + 0.11f * m.b;
                kind = exg > 0.06f ? 3 : lum > 0.55f ? 2 : lum < 0.26f ? 0 : 1;
            }
            var dstFlat = _realFlatRoofs[kind];
            int k0 = dstFlat.V.Count;
            AddTriangles(dstFlat, v, flatT.ToArray(), false);
            for (int i = k0; i < dstFlat.V.Count; i++) dstFlat.UV[i] = new Vector2(dstFlat.V[i].x, dstFlat.V[i].z) / 2.5f;
            RealFlatRoofs[kind]++;
        }
        AddTriangles(wall, v, steepT.ToArray(), true);
        if (pitched.Count == 0) return;
        int colour = redArea + greyArea > 0f ? (redArea >= greyArea * 0.6f ? 0 : 1) : (bi % 3 == 0 ? 1 : 0);
        var dstMb = _realTileRoofs[colour];
        for (int i = 0; i + 2 < pitched.Count; i += 3)
        {
            // (turned over as AddTriangles does: the model's faces point inwards in Unity's axes)
            Vector3 a = v[pitched[i]], b = v[pitched[i + 2]], c = v[pitched[i + 1]];
            var n = Vector3.Cross(b - a, c - a).normalized; if (n.y < 0) n = -n;
            var strike = Vector3.Cross(Vector3.up, n).normalized; var dip = Vector3.Cross(n, strike);
            int k0 = dstMb.V.Count;
            foreach (var q in new[] { a, b, c }) { dstMb.V.Add(q); dstMb.UV.Add(new Vector2(Vector3.Dot(q, strike), Vector3.Dot(q, dip)) / 2.6f); }
            dstMb.T.Add(k0); dstMb.T.Add(k0 + 1); dstMb.T.Add(k0 + 2);
        }
    }

    static readonly HashSet<string> ShopAmenities = new HashSet<string> { "restaurant", "cafe", "bar", "pub", "fast_food", "bank", "pharmacy", "ice_cream" };
    static readonly int GroundStyleCount = Enum.GetValues(typeof(GroundStyle)).Length;

    /// <summary>The ground floor of a building without a business at it: whole shops by type, lobbies under glass façades, else front doors.</summary>
    static GroundStyle GroundStyleFor(WindowStyle style, string type)
    {
        if (type == "retail" || type == "kiosk" || type == "supermarket") return GroundStyle.Shop;
        return style == WindowStyle.Glass ? GroundStyle.Lobby : GroundStyle.Door;
    }

    /// <summary>A shop front for the stretch of wall at p: an OSM business (shop, café, bank, ...) within 6 m.</summary>
    static bool NearShop(V2 p, List<V2> shops)
    {
        foreach (var q in shops) if (V2.DistanceSquared(p, q) < 36f) return true;
        return false;
    }

    /// <summary>A stone cornice along the top of a flat-roofed wall: 25 cm out, 40 cm tall.</summary>
    static void Cornice(MeshBuilder mb, List<V2> poly, float top)
    {
        // poly is counter-clockwise (positive area): the outside of an edge a->b is to its right
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            CorniceEdge(mb, a, b, top);
        }
    }

    static void CorniceEdge(MeshBuilder mb, V2 a, V2 b, float top)
    {
        var d = b - a; float len = d.Length();
        if (len < 0.5f) return;
        d /= len;
        var outN = new V2(d.Y, -d.X) * 0.25f;
        Vector3 A = P(a, top - 0.4f), B = P(b, top - 0.4f), A2 = P(a + outN, top - 0.4f), B2 = P(b + outN, top - 0.4f);
        Vector3 At = P(a + outN, top + 0.05f), Bt = P(b + outN, top + 0.05f), Ai = P(a, top + 0.05f), Bi = P(b, top + 0.05f);
        mb.Quad(A2, At, Bt, B2, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);     // front
        mb.Quad(A, A2, B2, B, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);       // underside
        mb.Quad(At, Ai, Bi, Bt, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);     // top
    }

    /// <summary>Cornices along the top edges of a 3D BAG building's walls (edges whose ends are both at the roof).</summary>
    static void CorniceFromWalls(MeshBuilder mb, Vector3[] v, int[] walls, float top)
    {
        var done = new HashSet<long>();
        for (int i = 0; i + 2 < walls.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int ia = walls[i + e], ib = walls[i + (e + 1) % 3];
                Vector3 a = v[ia], b = v[ib];
                if (a.y < top - 0.3f || b.y < top - 0.3f) continue;
                long key = Math.Min(ia, ib) * 1000003L + Math.Max(ia, ib);
                if (!done.Add(key)) continue;
                // outward: the wall triangle's normal (after the RD flip in AddTriangles: the reversed order)
                var c = v[walls[i + (e + 2) % 3]];
                var n = -Vector3.Cross(b - a, c - a); n.y = 0f;
                if (n.sqrMagnitude < 1e-6f) continue;
                n.Normalize();
                var a2 = new V2(a.x, a.z); var b2 = new V2(b.x, b.z);
                // CorniceEdge puts the outside to the right of a->b
                var right = new V2((b2 - a2).Y, -(b2 - a2).X);
                if (right.X * n.x + right.Y * n.z < 0f) { var t = a2; a2 = b2; b2 = t; }
                CorniceEdge(mb, a2, b2, Mathf.Min(a.y, b.y));
            }
    }

    /// <summary>Wall triangles cut at height y into the polygons below and above it.</summary>
    static void SplitWalls(Vector3[] v, int[] tris, float y, out List<Vector3[]> below, out List<Vector3[]> above)
    {
        below = new List<Vector3[]>(); above = new List<Vector3[]>();
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            var t = new[] { v[tris[i]], v[tris[i + 1]], v[tris[i + 2]] };
            var lo = new List<Vector3>(); var hi = new List<Vector3>();
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = t[k], q = t[(k + 1) % 3];
                bool pIn = p.y <= y, qIn = q.y <= y;
                if (pIn) lo.Add(p); else hi.Add(p);
                if (pIn != qIn)
                {
                    var x = Vector3.Lerp(p, q, (y - p.y) / (q.y - p.y));
                    lo.Add(x); hi.Add(x);
                }
            }
            if (lo.Count >= 3) below.Add(lo.ToArray());
            if (hi.Count >= 3) above.Add(hi.ToArray());
        }
    }

    /// <summary>Convex polygons (from SplitWalls) as wall triangles; groundTop &gt; 0: ground floor UVs (v 0..1 over that height).</summary>
    static void AddPolys(MeshBuilder mb, List<Vector3[]> polys, bool wall, float groundTop)
    {
        foreach (var poly in polys)
        {
            var verts = poly;
            var idx = new List<int>();
            for (int k = 1; k + 1 < verts.Length; k++) { idx.Add(0); idx.Add(k); idx.Add(k + 1); }
            if (groundTop > 0f) AddTrianglesGround(mb, verts, idx.ToArray(), groundTop);
            else AddTriangles(mb, verts, idx.ToArray(), wall);
        }
    }

    // like AddTriangles for walls, with the ground floor texture's UVs (3 m per bay, one storey tall)
    static void AddTrianglesGround(MeshBuilder mb, Vector3[] v, int[] tris, float storey)
    {
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-8f) continue;
            { var t = b; b = c; c = t; n = -n; }
            n.Normalize();
            var tan = Vector3.Cross(Vector3.up, n);
            if (tan.sqrMagnitude < 1e-4f) tan = Vector3.right;
            tan.Normalize();
            int k = mb.V.Count;
            foreach (var p in new[] { a, b, c }) { mb.V.Add(p); mb.UV.Add(new Vector2(Vector3.Dot(p, tan) / 3f, p.y / storey)); }
            mb.T.Add(k); mb.T.Add(k + 1); mb.T.Add(k + 2);
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

    /// <summary>The map's aerial photo (null when it has none).</summary>
    public AerialPhoto Aerial { get; private set; }
    const float AerialRoofMaxHeight = 35f;

    // moves the triangles of src that lie below AerialRoofMaxHeight into photo (with photo UVs); returns the rest.
    // Steep faces (over 30°, pitched roofs) go to the tile roofs instead (steep[0] red, steep[1] anthracite, by the
    // photo's colour there), with the rows of tiles along their eaves: a photo from above smears on a steep slope.
    MeshBuilder AerialSplit(MeshBuilder src, MeshBuilder photo, MeshBuilder[] steep)
    {
        var rest = new MeshBuilder();
        for (int i = 0; i + 2 < src.T.Count; i += 3)
        {
            int a = src.T[i], b = src.T[i + 1], c = src.T[i + 2];
            Vector3 va = src.V[a], vb = src.V[b], vc = src.V[c];
            // (beyond the photo, on a map with corridors: the roof's own material)
            bool low = va.y < AerialRoofMaxHeight && vb.y < AerialRoofMaxHeight && vc.y < AerialRoofMaxHeight && Aerial.Covers(va) && Aerial.Covers(vb) && Aerial.Covers(vc);
            var n = Vector3.Cross(vb - va, vc - va);
            bool isSteep = low && n.sqrMagnitude > 1e-6f && Mathf.Abs(n.normalized.y) < 0.866f && Mathf.Abs(n.normalized.y) > 0.17f;
            if (isSteep)
            {
                n = n.normalized; if (n.y < 0) n = -n;
                var col = Aerial.Sample((va + vb + vc) / 3f);
                Color.RGBToHSV(col, out float h, out float sat, out float val);
                bool red = sat > 0.18f && (h < 0.09f || h > 0.94f) && val > 0.22f;
                var dst = steep[red ? 0 : 1];
                var strike = Vector3.Cross(Vector3.up, n).normalized; var dip = Vector3.Cross(n, strike);
                int k0 = dst.V.Count;
                foreach (var v in new[] { va, vb, vc }) { dst.V.Add(v); dst.UV.Add(new Vector2(Vector3.Dot(v, strike), Vector3.Dot(v, dip)) / 2.6f); }
                dst.T.Add(k0); dst.T.Add(k0 + 1); dst.T.Add(k0 + 2);
                continue;
            }
            var to = low ? photo : rest;
            int k = to.V.Count;
            foreach (var j in new[] { a, b, c }) { to.V.Add(src.V[j]); to.UV.Add(low ? Aerial.Uv(src.V[j]) : src.UV[j]); }
            to.T.Add(k); to.T.Add(k + 1); to.T.Add(k + 2);
        }
        return rest;
    }

    public enum RoadSurface { Asphalt, Bricks, Cobblestones, Gravel }

    static RoadSurface SurfaceOf(OsmWay w)
    {
        switch (w.Tag("surface") ?? "")
        {
            case "paving_stones": case "bricks": case "brick": case "paving_stones:30": return RoadSurface.Bricks;
            case "sett": case "cobblestone": case "unhewn_cobblestone": case "cobblestone:flattened": return RoadSurface.Cobblestones;
            case "gravel": case "fine_gravel": case "unpaved": case "compacted": case "dirt": case "ground": case "pebblestone": return RoadSurface.Gravel;
            default: return RoadSurface.Asphalt;
        }
    }

    // repair patches and manhole covers on asphalt roads, and puddles that show in the rain
    readonly MeshBuilder _patches = new MeshBuilder(), _manholes = new MeshBuilder(), _puddles = new MeshBuilder();

    void RoadDetails(List<V2> line, float width, float y, long seed, RoadSurface surface)
    {
        var rng = new System.Random((int)(seed * 2654435761L % int.MaxValue));
        float len = Geometry2D.Length(line);
        for (float d = 8f + (float)rng.NextDouble() * 20f; d < len - 4f; d += 18f + (float)rng.NextDouble() * 40f)
        {
            var (pt, dir) = Geometry2D.PointAt(line, d);
            if (!Inside(pt)) continue;
            var n = new V2(dir.Y, -dir.X);
            float side = ((float)rng.NextDouble() - 0.5f) * (width - 2f);
            var c = pt + n * side;
            double kind = rng.NextDouble();
            if (surface == RoadSurface.Gravel || surface == RoadSurface.Cobblestones) kind = 1.0;   // no patches or covers
            else if (surface == RoadSurface.Bricks && kind < 0.35) kind = 0.5;                     // covers, no asphalt patches
            if (kind < 0.35)
            {
                // a rectangular repair patch, along the road
                float pl = 1.5f + (float)rng.NextDouble() * 4f, pw = 0.8f + (float)rng.NextDouble() * 1.6f;
                Quad2(_patches, c, dir, n, pl, pw, y + 0.004f, 3f);
            }
            else if (kind < 0.6)
            {
                Disc(_manholes, c, 0.33f, y + 0.006f);
            }
            if (rng.NextDouble() < 0.45)
            {
                // puddles near the kerb, where water stays
                var pc = pt + n * (width / 2 - 0.6f) * (rng.NextDouble() < 0.5 ? -1f : 1f) + dir * (float)rng.NextDouble() * 3f;
                Blob(_puddles, pc, 0.6f + (float)rng.NextDouble() * 1.4f, y + 0.008f, rng);
            }
        }
    }

    void Quad2(MeshBuilder mb, V2 c, V2 dir, V2 n, float length, float width, float y, float uvScale)
    {
        var a = c - dir * (length / 2) - n * (width / 2); var b = c + dir * (length / 2) - n * (width / 2);
        var e = c + dir * (length / 2) + n * (width / 2); var f = c - dir * (length / 2) + n * (width / 2);
        mb.Quad(P(a, y), P(f, y), P(e, y), P(b, y), new Vector2(a.X, a.Y) / uvScale, new Vector2(f.X, f.Y) / uvScale, new Vector2(e.X, e.Y) / uvScale, new Vector2(b.X, b.Y) / uvScale);
    }

    static void Disc(MeshBuilder mb, V2 c, float radius, float y)
    {
        int i0 = mb.V.Count;
        mb.V.Add(P(c, y)); mb.UV.Add(new Vector2(0.5f, 0.5f));
        const int n = 16;
        for (int k = 0; k <= n; k++)
        {
            float a = k * Mathf.PI * 2f / n;
            mb.V.Add(P(c + new V2(Mathf.Cos(a), Mathf.Sin(a)) * radius, y));
            mb.UV.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
        }
        for (int k = 1; k <= n; k++) { mb.T.Add(i0); mb.T.Add(i0 + k + 1); mb.T.Add(i0 + k); }
    }

    static void Blob(MeshBuilder mb, V2 c, float size, float y, System.Random rng)
    {
        int i0 = mb.V.Count;
        mb.V.Add(P(c, y)); mb.UV.Add(Vector2.zero);
        const int n = 14;
        var radii = new float[n];
        for (int k = 0; k < n; k++) radii[k] = size * (0.55f + 0.45f * (float)rng.NextDouble());
        float stretch = 1.2f + (float)rng.NextDouble();
        for (int k = 0; k <= n; k++)
        {
            float a = k * Mathf.PI * 2f / n, r = radii[k % n];
            mb.V.Add(P(c + new V2(Mathf.Cos(a) * r * stretch, Mathf.Sin(a) * r), y));
            mb.UV.Add(Vector2.right);   // (u: 0 in the middle, 1 at the edge: the puddle's material fades out towards it)
        }
        for (int k = 1; k <= n; k++) { mb.T.Add(i0); mb.T.Add(i0 + k + 1); mb.T.Add(i0 + k); }
    }

    public int PatchCount => _patches.T.Count / 6;
    public int ManholeCount => _manholes.T.Count / 48;
    public int PuddleCount => _puddles.T.Count / 42;

    /// <summary>The puddles of the map (shown only while it rains: OsmMap).</summary>
    public GameObject Puddles { get; private set; }

    void EmitRoadDetails(Transform world)
    {
        var patch = Mat("Asphalt patch", Color.white, 0.25f, Tex("asphalt_patch", 128, (x, y, r) => Noise(new Color(0.16f, 0.16f, 0.17f), r, 0.3f)));
        // (with the photo textures: the same asphalt, newer and a little darker, so a repair reads as one and not as a slab)
        if (!_noPhotoWalls && !_noRelief && _assets != null && _assets.FacadeTemplate != null && _assets.GroundNames != null)
            GroundPhoto(patch, "asphalt", 3f, new Color(0.6f, 0.6f, 0.65f));
        var manhole = Mat("Manhole cover", Color.white, 0.45f, Tex("manhole", 128, (x, y, r) =>
        {
            float u = x / 128f - 0.5f, v = y / 128f - 0.5f, d = Mathf.Sqrt(u * u + v * v);
            if (d > 0.46f) return new Color(0.18f, 0.18f, 0.18f);
            bool rib = (x / 10 + y / 10) % 2 == 0;
            return Noise(rib ? new Color(0.3f, 0.29f, 0.28f) : new Color(0.22f, 0.21f, 0.2f), r, 0.15f);
        }));
        var puddle = Mat("Puddle", new Color(0.06f, 0.07f, 0.075f, 0.85f), 0.97f, TexRect("puddle_edge", 64, 4, (x, y, r) =>
            new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Pow(x / 63f, 2.2f)))));
        if (puddle.GetFloat("_Surface") < 0.5f)
        {
            // a film of water: see-through towards its soft edge, a mirror of the sky and the street in the middle
            puddle.SetFloat("_Surface", 1f); puddle.SetFloat("_Blend", 0f);
            puddle.SetOverrideTag("RenderType", "Transparent");
            puddle.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); puddle.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            puddle.SetFloat("_ZWrite", 0f); puddle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            puddle.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
            puddle.mainTexture.wrapMode = TextureWrapMode.Clamp;
        }
        puddle.SetFloat("_Metallic", 0.3f);
        // flat shapes on the road: drawn from both sides (their winding differs with the road's direction)
        foreach (var m in new[] { patch, manhole, puddle }) m.SetFloat("_Cull", 0f);
        Emit(world, "Road patches", _patches, patch, false);
        Emit(world, "Manhole covers", _manholes, manhole, false);
        Puddles = Emit(world, "Puddles", _puddles, puddle, false);
        if (Puddles != null) Puddles.SetActive(false);
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
