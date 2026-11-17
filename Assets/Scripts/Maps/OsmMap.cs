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
    /// <summary>"lat,lon": spawn points near this place instead of the map centre (trial parameter start, -start).</summary>
    public static string RequestedStart;

    /// <summary>The local point spawn points are nearest to: the requested start, else the map centre.</summary>
    Vector3 SpawnFocus()
    {
        if (string.IsNullOrEmpty(RequestedStart) || Generator == null) return Vector3.zero;
        var f = RequestedStart.Split(',');
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (f.Length < 2 || !double.TryParse(f[0], System.Globalization.NumberStyles.Float, ci, out var la) || !double.TryParse(f[1], System.Globalization.NumberStyles.Float, ci, out var lo)) return Vector3.zero;
        new CoupledSim.Maps.GeoProjection(Info.Latitude, Info.Longitude).ToLocal(la, lo, out var x, out var z);
        return new Vector3((float)x, 0f, (float)z);
    }

    static float Distance2(System.Collections.Generic.List<Vector3> line, Vector3 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            var a = line[i]; var b = line[i + 1]; a.y = b.y = p.y = 0f;
            var ab = b - a; float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, (a + ab * t - p).sqrMagnitude);
        }
        return best;
    }

    /// <summary>The distance along a line of its point nearest to p.</summary>
    static float NearestAlong(System.Collections.Generic.List<Vector3> line, Vector3 p)
    {
        float best = float.MaxValue, at = 0f, acc = 0f;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            var a = line[i]; var b = line[i + 1]; a.y = b.y = p.y = 0f;
            var ab = b - a; float l = ab.magnitude; float t = l < 1e-3f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / (l * l));
            float d = (a + ab * t - p).sqrMagnitude;
            if (d < best) { best = d; at = acc + t * l; }
            acc += l;
        }
        return at;
    }
    public static OsmMap Current { get; private set; }

    [Tooltip("Map built when the scene is opened directly (e.g. in the editor).")]
    public string DefaultMap = "Eindhoven";

    public MapInfo Info { get; private set; }
    /// <summary>For maps from a road network (OpenDRIVE/SUMO): the SUMO network file it was built from, and the network coordinates of the scene origin.</summary>
    public string RoadNetwork { get; private set; }
    public Vector2 RoadNetworkOffset { get; private set; }
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

    float _nextLights = -1f, _lights = -1f;

    // windows light up at dusk: full brightness from 4° below the horizon, off from 2° above it
    void Update()
    {
        if (Time.unscaledTime < _nextLights) return;
        _nextLights = Time.unscaledTime + 1f;
        var env = EnvironmentController.Instance;
        // puddles while it rains (and a while after: they stay while the roads are wet)
        if (Generator != null && Generator.Puddles != null) Generator.Puddles.SetActive(env != null && env.Rain > 0.2f);
        CarHeadlights.Update(env != null ? env.SunElevation : 45f);
        // with a surroundings photo: cameras see it to the horizon (it is one flat, cheap mesh)
        if (Generator != null && Generator.Aerial != null && Generator.Aerial.FarTexture != null)
            foreach (var cam in Camera.allCameras)
                if (cam.farClipPlane < Generator.Aerial.FarHalfSize) cam.farClipPlane = Generator.Aerial.FarHalfSize;
        // and to the skyline around the map
        if (Generator != null && Generator.SkylineReach > 0f)
            foreach (var cam in Camera.allCameras)
                if (cam.farClipPlane < Generator.SkylineReach && cam.farClipPlane > 100f) cam.farClipPlane = Generator.SkylineReach;
        float elevation = env != null ? env.SunElevation : 45f;
        float level = Mathf.Clamp01((2f - elevation) / 6f);
        if (Mathf.Abs(level - _lights) < 0.01f) return;
        _lights = level;
        var c = Color.white * (1.4f * level);
        foreach (var m in OsmMapGenerator.NightMaterials) if (m != null) m.SetColor("_EmissionColor", c);
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
        RoadNetwork = netPath;
        RoadNetworkOffset = new Vector2((float)net.OffsetX, (float)net.OffsetY);
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
        // onto the real terrain, where the map has heights (Tools/terrain_height.py)
        if (Generator.Terrain != null)
        {
            try { Generator.Terrain.Apply(root, Generator); Generator.RealDataCredits.Add($"terrain {Generator.Terrain.Min:F0} to {Generator.Terrain.Max:F0} m: {Generator.Terrain.Credit}"); }
            catch (Exception e) { Debug.LogWarning("TerrainHeight: " + e); }
        }
        if (Generator.TreeTransforms.Count > 0) root.gameObject.AddComponent<TreeSway>().Trees.AddRange(Generator.TreeTransforms);
        new GameObject("Map data © OpenStreetMap contributors (ODbL)").transform.SetParent(transform, false);
        Debug.Log($"OsmMap: built {info} in {sw.ElapsedMilliseconds} ms: {Generator.Roads.Count} roads, {Generator.BuildingCount} buildings ({Generator.BuildingsOverRoads} over roads), {Generator.Crossings.Count / 2} crossings, {Generator.SignCount} traffic signs, {Generator.SignalJunctions} signalised junctions ({Generator.SignalHeads} signal heads with {Generator.TurnArrowHeads} turn arrows, {Generator.PedestrianSignalHeads} pedestrian lights), {Generator.LaneArrowCount} lane arrows, {Generator.CycleLaneCount} cycle lanes, {Generator.BusLaneCount} bus lanes, {Generator.PropCount} street props, {Generator.ParkedCarCount} parked cars, {Generator.ParkedBicycleCount} parked bicycles, {Generator.PatchCount} road patches, {Generator.ManholeCount} manhole covers, {Generator.PuddleCount} puddles{(Generator.Aerial != null ? ", aerial photo (" + Generator.Aerial.Credit + ")" : "")}{(Generator.RealDataCredits.Count > 0 ? "; real data: " + string.Join("; ", Generator.RealDataCredits) : "")}");
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
            var near = SpawnFocus();
            var lines = (kind == SpawnKind.CyclePath ? Generator.CyclePaths : Generator.Pavements)
                .Where(l => Length(l) > 15f && !Generator.CrossingLines.Contains(l)).OrderBy(l => (Mid(l) - near).sqrMagnitude).ToList();
            if (lines.Count == 0) return false;
            // the index-th line, or a later one when a tree, a post or a wall stands on it
            float len = kind == SpawnKind.CyclePath ? 1.9f : 0.6f, width = kind == SpawnKind.CyclePath ? 0.7f : 0.6f;
            for (int k = 0; k < lines.Count; k++)
            {
                var line = lines[(index + k) % lines.Count];
                foreach (var d in Along(Length(line), 3f))
                {
                    PointAt(line, d, out position, out var dir);
                    rotation = Quaternion.LookRotation(dir, Vector3.up);
                    if (k == lines.Count - 1 || Clear(position, dir, len, width) && OffRoad(position)) return true;
                }
            }
            return true;
        }
        var focus = SpawnFocus();
        bool atStart = focus != Vector3.zero;
        // (near a requested start: any road with through traffic, motorways too, nearest first)
        var roads = (atStart
                ? Generator.Roads.Where(r => !r.NoThroughTraffic && r.Type != "service" && Length(r.Line) > 40f).OrderBy(r => Distance2(r.Line, focus))
                : Generator.Roads.Where(r => SpawnRoadTypes.Contains(r.Type) && !r.NoThroughTraffic && Length(r.Line) > 40f).OrderBy(r => Mid(r.Line).sqrMagnitude))
            .ToList();
        if (atStart && index == 0)
        {
            var near = Generator.Roads.OrderBy(r => Distance2(r.Line, focus)).Take(4)
                .Select(r => $"{r.Type} {Mathf.Sqrt(Distance2(r.Line, focus)):F0} m{(r.NoThroughTraffic ? " no through traffic" : "")}{(r.Bridge ? " bridge" : "")}, {Length(r.Line):F0} m long");
            Debug.Log($"OsmMap: spawn near {RequestedStart} ({focus.x:F0}, {focus.z:F0}): nearest roads {string.Join("; ", near)}");
        }
        if (roads.Count == 0) roads = Generator.Roads.Where(r => Length(r.Line) > 20f).OrderBy(r => Mid(r.Line).sqrMagnitude).ToList();
        if (roads.Count == 0) return false;
        float side = Info.LeftHandTraffic ? -1f : 1f;
        // (a one-way road of two lanes and more, a motorway's carriageway: in the lane on the driver's side, as the routes)
        // the middle of the index-th road, else further along it, else a later road: where no tree, parked car,
        // post or wall stands in the vehicle's place (register trees often stand at the edge of narrow streets)
        for (int k = 0; k < roads.Count; k++)
        {
            var road = roads[(index + k) % roads.Count];
            float offset = kind == SpawnKind.RoadEdge ? road.Width / 2 - 0.8f : (road.OneWay ? (road.Width >= 6.6f ? road.Width / 4 : 0f) : road.Width / 4);
            foreach (var d in atStart ? AlongFrom(Length(road.Line), NearestAlong(road.Line, focus), 5f) : Along(Length(road.Line), 5f))
            {
                PointAt(road.Line, d, out var centre, out var forward);
                var rightVec = Vector3.Cross(Vector3.up, forward).normalized;
                position = centre + rightVec * side * offset + Vector3.up * 0.05f;
                rotation = Quaternion.LookRotation(forward, Vector3.up);
                bool rider = kind == SpawnKind.RoadEdge;
                if (Clear(position, forward, rider ? 1.9f : 4.8f, rider ? 0.8f : 2.0f))
                {
                    if (atStart) Debug.Log($"OsmMap: spawn {kind} {index} on a {road.Type} at {position}");
                    return true;
                }
            }
        }
        // nothing clear anywhere: the middle of the index-th road, as before
        {
            var road = roads[index % roads.Count];
            PointAt(road.Line, Length(road.Line) / 2, out var centre, out var forward);
            float offset = kind == SpawnKind.RoadEdge ? road.Width / 2 - 0.8f : (road.OneWay ? (road.Width >= 6.6f ? road.Width / 4 : 0f) : road.Width / 4);
            position = centre + Vector3.Cross(Vector3.up, forward).normalized * side * offset + Vector3.up * 0.05f;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        return true;
    }

    /// <summary>Distances along a line to try: from a point outwards in steps, keeping a few metres from its ends.</summary>
    static System.Collections.Generic.IEnumerable<float> AlongFrom(float length, float from, float step)
    {
        from = Mathf.Clamp(from, Mathf.Min(6f, length / 2), Mathf.Max(length - 6f, length / 2));
        yield return from;
        for (float o = step; o < length; o += step)
        {
            if (from + o < length - 6f) yield return from + o;
            if (from - o > 6f) yield return from - o;
        }
    }

    /// <summary>Distances along a line to try: its middle, then outwards in steps, keeping a few metres from its ends.</summary>
    static System.Collections.Generic.IEnumerable<float> Along(float length, float step)
    {
        float mid = length / 2f;
        yield return mid;
        for (float o = step; o < mid - 6f; o += step) { yield return mid + o; yield return mid - o; }
    }

    /// <summary>The ground there is not a carriageway (a footpath drawn across a road has no paving there).</summary>
    static bool OffRoad(Vector3 position)
    {
        if (!Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return true;
        return !hit.collider.name.StartsWith("Roads");
    }

    /// <summary>No tree trunk within reach of the footprint (length along forward, width across) and nothing solid in it
    /// above the kerbs (parked cars, posts, walls).</summary>
    bool Clear(Vector3 position, Vector3 forward, float length, float width)
    {
        forward.y = 0f; forward.Normalize();
        var a = position - forward * (length / 2f); var b = position + forward * (length / 2f);
        if (Generator != null)
            foreach (var t in Generator.TreeTransforms)
            {
                if (t == null) continue;
                var p = t.position; var ab = b - a; ab.y = 0f;
                float u = Mathf.Clamp01(Vector3.Dot(new Vector3(p.x - a.x, 0f, p.z - a.z), ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                var q = a + ab * u;
                // trunk and the low branches of street trees (crowns are wider, but higher up)
                if (new Vector2(p.x - q.x, p.z - q.z).magnitude < width / 2f + 1.6f) return false;
            }
        Physics.SyncTransforms();   // (the map's objects were made this frame)
        var half = new Vector3(width / 2f, 0.6f, length / 2f);
        foreach (var c in Physics.OverlapBox(position + Vector3.up * 1.0f, half, Quaternion.LookRotation(forward, Vector3.up), ~0, QueryTriggerInteraction.Ignore))
            if (c != null) return false;
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
