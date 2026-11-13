using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CoupledSim.Maps;
using UnityEngine;

/// <summary>
/// The map scene (Assets/_Scenes/OsmMap.unity): builds the requested city from
/// StreamingAssets/Maps/{name}.osm.gz while the scene loads, before the experiment is placed in it.
/// LevelManager sets <see cref="Requested"/> from the experiment's Map, the trial parameter "map"
/// or the host's choice; clients get the same map from the host.
/// </summary>
public class OsmMap : MonoBehaviour
{
    /// <summary>Map to build when the scene loads (set before loading the scene).</summary>
    public static string Requested;
    public static OsmMap Current { get; private set; }

    [Tooltip("Map built when the scene is opened directly (e.g. in the editor).")]
    public string DefaultMap = "Eindhoven";

    public MapInfo Info { get; private set; }
    public OsmMapGenerator Generator { get; private set; }

    void Awake()
    {
        Current = this;
        Load(string.IsNullOrEmpty(Requested) ? DefaultMap : Requested);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    /// <summary>"lat,lon" or "lat,lon,radius" (degrees, metres; default radius 400 m).</summary>
    public static bool TryParseLocation(string s, out double lat, out double lon, out float radius)
    {
        lat = lon = 0; radius = 400f;
        if (string.IsNullOrEmpty(s)) return false;
        var parts = s.Split(',');
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (parts.Length < 2 || !double.TryParse(parts[0], System.Globalization.NumberStyles.Float, ci, out lat) ||
            !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, ci, out lon)) return false;
        if (parts.Length > 2 && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, ci, out var r)) radius = Mathf.Clamp(r, 100f, 1000f);
        return Math.Abs(lat) <= 85 && Math.Abs(lon) <= 180;
    }

    /// <summary>OSM data of any place (cached in the persistent data folder), downloaded from the OpenStreetMap API.</summary>
    static string LocationData(double lat, double lon, float radius, out string cachePath)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var dir = Path.Combine(Application.persistentDataPath, "Maps");
        Directory.CreateDirectory(dir);
        cachePath = Path.Combine(dir, string.Format(ci, "{0:F5}_{1:F5}_{2:F0}.osm.gz", lat, lon, radius));
        if (!File.Exists(cachePath))
        {
            double dLat = radius / 111320.0, dLon = radius / (111320.0 * Math.Cos(lat * Math.PI / 180));
            var url = string.Format(ci, "https://api.openstreetmap.org/api/0.6/map?bbox={0},{1},{2},{3}", lon - dLon, lat - dLat, lon + dLon, lat + dLat);
            using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(120) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("coupled-sim/1.0");
                var xml = http.GetStringAsync(url).Result;
                using (var gz = new GZipStream(File.Create(cachePath), System.IO.Compression.CompressionLevel.Optimal))
                using (var w = new StreamWriter(gz, Encoding.UTF8)) w.Write(xml);
            }
        }
        return cachePath;
    }

    // ------------------------------------------------------------------ road networks (OpenDRIVE, SUMO) (#40)

    /// <summary>An OpenDRIVE (.xodr) or SUMO network (.net.xml, .net.xml.gz) file as a map: its name in StreamingAssets/Maps, or a full path.</summary>
    public static bool IsRoadNetworkFile(string s) => s != null &&
        (s.EndsWith(".xodr", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".net.xml", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".net.xml.gz", StringComparison.OrdinalIgnoreCase));

    public static string ResolveRoadNetwork(string name) => Path.IsPathRooted(name) ? name : Path.Combine(Application.streamingAssetsPath, "Maps", name);

    /// <summary>SUMO's netconvert: SUMO_HOME/bin, else on the PATH.</summary>
    static string Netconvert()
    {
        var exe = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor ? "netconvert.exe" : "netconvert";
        var home = Environment.GetEnvironmentVariable("SUMO_HOME");
        if (!string.IsNullOrEmpty(home) && File.Exists(Path.Combine(home, "bin", exe))) return Path.Combine(home, "bin", exe);
        return exe;
    }

    /// <summary>An OpenDRIVE file converted to a SUMO network by netconvert (cached in the simulator's data folder).</summary>
    static string ConvertOpenDrive(string xodr)
    {
        var dir = Path.Combine(Application.persistentDataPath, "Maps");
        Directory.CreateDirectory(dir);
        var stamp = File.GetLastWriteTimeUtc(xodr).Ticks ^ new FileInfo(xodr).Length;
        var net = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(xodr)}-{stamp & 0xffffffff:x8}.net.xml");
        if (File.Exists(net)) return net;
        // SUMO's lane-type maps: driving lanes, and also pavements (sidewalk) and cycle lanes (biking)
        var types = "";
        var home = Environment.GetEnvironmentVariable("SUMO_HOME");
        if (!string.IsNullOrEmpty(home))
        {
            var maps = new[] { "opendriveNetconvert.typ.xml", "opendriveNetconvertPedestrians.typ.xml", "opendriveNetconvertBicycle.typ.xml" }
                .Select(f => Path.Combine(home, "data", "typemap", f)).Where(File.Exists).ToArray();
            if (maps.Length > 0) types = $" --type-files \"{string.Join(",", maps)}\"";
        }
        var psi = new System.Diagnostics.ProcessStartInfo(Netconvert(),
            $"--opendrive-files \"{xodr}\" -o \"{net}\" --no-turnarounds true --no-warnings true{types}")
        { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
        using (var p = System.Diagnostics.Process.Start(psi))
        {
            var err = p.StandardError.ReadToEnd();
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0 || !File.Exists(net)) throw new Exception($"netconvert failed ({p.ExitCode}): {err}");
        }
        return net;
    }

    void LoadRoadNetwork(string name)
    {
        var path = ResolveRoadNetwork(name);
        if (!File.Exists(path)) { Debug.LogError("OsmMap: road network not found: " + path); return; }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string netPath = path;
        if (path.EndsWith(".xodr", StringComparison.OrdinalIgnoreCase))
        {
            try { netPath = ConvertOpenDrive(path); }
            catch (Exception e)
            {
                Debug.LogError($"OsmMap: could not convert the OpenDRIVE file {path} with SUMO's netconvert (install SUMO and set SUMO_HOME): {e.Message}");
                return;
            }
        }
        string xml;
        if (netPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            using (var gz = new GZipStream(File.OpenRead(netPath), CompressionMode.Decompress))
            using (var reader = new StreamReader(gz, Encoding.UTF8)) xml = reader.ReadToEnd();
        else xml = File.ReadAllText(netPath);
        // a network without geographic reference is placed in Eindhoven's latitude (sun and time zone)
        if (!SumoNetwork.TryGetCentre(xml, out var lat, out var lon)) { lat = 51.44; lon = 5.48; }
        var mapName = Path.GetFileName(path);
        foreach (var ext in new[] { ".gz", ".xml", ".net", ".xodr" })
            if (mapName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) mapName = mapName.Substring(0, mapName.Length - ext.Length);
        var info = new MapInfo
        {
            Name = mapName, City = mapName, Country = "", Continent = "", Latitude = lat, Longitude = lon,
            UtcOffsetJune = Mathf.Round((float)lon / 15f), Area = "road network " + Path.GetFileName(path),
        };
        var net = SumoNetwork.Convert(xml, new GeoProjection(lat, lon));
        Info = info;
        Generator = new OsmMapGenerator(info, net.Data, MapAssets.Instance);
        var root = new GameObject(info.City).transform;
        root.SetParent(transform, false);
        Generator.Build(root);
        Debug.Log($"OsmMap: built road network {Path.GetFileName(path)} in {sw.ElapsedMilliseconds} ms: {net.Roads} roads, {net.Junctions} junctions, {Generator.Crossings.Count / 2} crossings, {Generator.SignCount} traffic signs");
    }

    public void Load(string name)
    {
        if (IsRoadNetworkFile(name)) { LoadRoadNetwork(name); return; }
        string path;
        MapInfo info;
        if (TryParseLocation(name, out var lat, out var lon, out var radius))
        {
            info = new MapInfo
            {
                Name = "Location", City = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4}, {1:F4}", lat, lon),
                Country = "", Continent = "", Latitude = lat, Longitude = lon,
                UtcOffsetJune = Mathf.Round((float)lon / 15f), Area = $"{radius:F0} m around the point",
            };
            try
            {
                path = LocationData(lat, lon, radius, out _);
            }
            catch (Exception e)
            {
                Debug.LogError($"OsmMap: could not download OpenStreetMap data for {name} (internet needed the first time): {e.Message}");
                return;
            }
        }
        else if (MapCatalog.TryGet(name, out info))
        {
            path = Path.Combine(Application.streamingAssetsPath, "Maps", info.Name + ".osm.gz");
        }
        else
        {
            Debug.LogError($"OsmMap: unknown map '{name}'. Known maps: {string.Join(", ", MapCatalog.Names())}, or a location 'lat,lon[,radius]'");
            return;
        }
        Info = info;
        if (!File.Exists(path))
        {
            Debug.LogError("OsmMap: map data not found: " + path);
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string xml;
        using (var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
        using (var reader = new StreamReader(gz, Encoding.UTF8))
            xml = reader.ReadToEnd();
        var data = OsmData.Parse(xml);
        Generator = new OsmMapGenerator(info, data, MapAssets.Instance);
        var root = new GameObject(info.City).transform;
        root.SetParent(transform, false);
        Generator.Build(root);
        new GameObject("Map data © OpenStreetMap contributors (ODbL)").transform.SetParent(transform, false);
        Debug.Log($"OsmMap: built {info} in {sw.ElapsedMilliseconds} ms: {Generator.Roads.Count} roads, {Generator.BuildingCount} buildings, {Generator.Crossings.Count / 2} crossings, {Generator.SignCount} traffic signs, {Generator.PropCount} street props, {Generator.ParkedCarCount} parked cars, {Generator.ParkedBicycleCount} parked bicycles");
        // the road network for analysis, once per map (ExperimentLogs/RoadNetwork-{map}.geojson)
        try
        {
            if (!System.IO.File.Exists($"ExperimentLogs/RoadNetwork-{info.Name.Replace(' ', '_')}.geojson")) RoadNetworkExport.Export("ExperimentLogs", info.Name);
        }
        catch (System.Exception e) { Debug.LogWarning("RoadNetworkExport: " + e.Message); }
    }

    public enum SpawnKind
    {
        /// <summary>In the driving lane of a road (right lane, or left in left-hand traffic).</summary>
        RoadLane,
        /// <summary>At the edge of the road, where cyclists ride without a cycle path.</summary>
        RoadEdge,
        /// <summary>On a cycle path (falls back to RoadEdge).</summary>
        CyclePath,
        /// <summary>On a pavement or footway.</summary>
        Pavement,
    }

    static readonly string[] SpawnRoadTypes = { "residential", "tertiary", "secondary", "unclassified", "primary", "living_street" };

    /// <summary>
    /// A place to spawn of the given kind: the index-th nearest such place to the map centre,
    /// in the middle of a road/path, facing along it.
    /// </summary>
    public bool FindSpawn(SpawnKind kind, int index, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (Generator == null) return false;
        if (kind == SpawnKind.CyclePath && Generator.CyclePaths.Count == 0) kind = SpawnKind.RoadEdge;

        if (kind == SpawnKind.CyclePath || kind == SpawnKind.Pavement)
        {
            var lines = (kind == SpawnKind.CyclePath ? Generator.CyclePaths : Generator.Pavements)
                .Where(l => Length(l) > 15f).OrderBy(l => Mid(l).sqrMagnitude).ToList();
            if (lines.Count == 0) return false;
            var line = lines[index % lines.Count];
            PointAt(line, Length(line) / 2, out position, out var dir);
            rotation = Quaternion.LookRotation(dir, Vector3.up);
            return true;
        }
        var roads = Generator.Roads.Where(r => SpawnRoadTypes.Contains(r.Type) && Length(r.Line) > 40f)
            .OrderBy(r => Mid(r.Line).sqrMagnitude).ToList();
        if (roads.Count == 0) roads = Generator.Roads.Where(r => Length(r.Line) > 20f).OrderBy(r => Mid(r.Line).sqrMagnitude).ToList();
        if (roads.Count == 0) return false;
        var road = roads[index % roads.Count];
        PointAt(road.Line, Length(road.Line) / 2, out var centre, out var forward);
        var rightVec = Vector3.Cross(Vector3.up, forward).normalized;
        float side = Info.LeftHandTraffic ? -1f : 1f;
        float offset = kind == SpawnKind.RoadEdge ? road.Width / 2 - 0.8f : (road.OneWay ? 0f : road.Width / 4);
        position = centre + rightVec * side * offset + Vector3.up * 0.05f;
        rotation = Quaternion.LookRotation(forward, Vector3.up);
        return true;
    }

    static float Length(System.Collections.Generic.List<Vector3> l)
    {
        float s = 0;
        for (int i = 1; i < l.Count; i++) s += Vector3.Distance(l[i - 1], l[i]);
        return s;
    }

    static Vector3 Mid(System.Collections.Generic.List<Vector3> l)
    {
        PointAt(l, Length(l) / 2, out var p, out _);
        return p;
    }

    static void PointAt(System.Collections.Generic.List<Vector3> l, float d, out Vector3 p, out Vector3 dir)
    {
        for (int i = 1; i < l.Count; i++)
        {
            float seg = Vector3.Distance(l[i - 1], l[i]);
            if (d <= seg || i == l.Count - 1)
            {
                dir = seg > 1e-4f ? (l[i] - l[i - 1]) / seg : Vector3.forward;
                dir.y = 0;
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
                p = l[i - 1] + (l[i] - l[i - 1]) * Mathf.Clamp01(seg > 1e-4f ? d / seg : 0);
                return;
            }
            d -= seg;
        }
        p = l.Count > 0 ? l[0] : Vector3.zero;
        dir = Vector3.forward;
    }
}
