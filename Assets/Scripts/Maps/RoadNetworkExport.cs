using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CoupledSim.Maps;
using UnityEngine;
using UnityStandardAssets.Utility;

/// <summary>
/// Exports the road network of the loaded world for analysis (#46), in the scene's coordinates
/// (x, z in metres, the same as the logs) and, on city maps, also in longitude/latitude:
/// - RoadNetwork-{name}.geojson: roads (type, width, one-way), cycle paths, pavements, pedestrian
///   crossings, traffic lights and the waypoint paths of AI cars and pedestrians;
/// - RoadNetwork-{name}.obj: the road, pavement and crossing surfaces as meshes (top view).
/// City maps write it to ExperimentLogs when they are built; any scene with Coupled Sim ➡️
/// Maps ➡️ Export road network (in Play mode for city maps).
/// </summary>
public static class RoadNetworkExport
{
    static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    public static string Export(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        var safe = string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
        var geo = Path.Combine(folder, $"RoadNetwork-{safe}.geojson");
        File.WriteAllText(geo, GeoJson());
        File.WriteAllText(Path.Combine(folder, $"RoadNetwork-{safe}.obj"), Obj());
        Debug.Log($"RoadNetworkExport: {geo} (+ .obj)");
        return geo;
    }

    // ---- GeoJSON

    static GeoProjection? Projection()
    {
        var map = OsmMap.Current;
        if (map == null || map.Generator == null) return null;
        return new GeoProjection(map.Info.Latitude, map.Info.Longitude);
    }

    static string Coord(Vector3 p, GeoProjection? proj)
    {
        if (proj == null) return $"[{p.x.ToString("F2", Ci)},{p.z.ToString("F2", Ci)},{p.y.ToString("F2", Ci)}]";
        proj.Value.ToGeo(p.x, p.z, out var lat, out var lon);
        return $"[{lon.ToString("F7", Ci)},{lat.ToString("F7", Ci)},{p.y.ToString("F2", Ci)}]";
    }

    static string Props(Dictionary<string, object> props, Vector3[] local)
    {
        var sb = new StringBuilder("{");
        foreach (var kv in props)
        {
            sb.Append('"').Append(kv.Key).Append("\":");
            switch (kv.Value)
            {
                case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case float f: sb.Append(f.ToString("G6", Ci)); break;
                default: sb.Append(Convert.ToString(kv.Value, Ci)); break;
            }
            sb.Append(',');
        }
        // the scene coordinates are always included, for matching the logs
        sb.Append("\"scene_xz\":[").Append(string.Join(",", local.Select(p => $"[{p.x.ToString("F2", Ci)},{p.z.ToString("F2", Ci)}]"))).Append("]}");
        return sb.ToString();
    }

    static void Feature(List<string> features, string kind, Vector3[] pts, Dictionary<string, object> props, GeoProjection? proj)
    {
        if (pts.Length == 0) return;
        props["kind"] = kind;
        string geom = pts.Length == 1
            ? $"{{\"type\":\"Point\",\"coordinates\":{Coord(pts[0], proj)}}}"
            : $"{{\"type\":\"LineString\",\"coordinates\":[{string.Join(",", pts.Select(p => Coord(p, proj)))}]}}";
        features.Add($"{{\"type\":\"Feature\",\"geometry\":{geom},\"properties\":{Props(props, pts)}}}");
    }

    public static string GeoJson()
    {
        var proj = Projection();
        var features = new List<string>();
        var gen = OsmMap.Current != null ? OsmMap.Current.Generator : null;
        if (gen != null)
        {
            foreach (var r in gen.Roads)
                Feature(features, "road", r.Line.ToArray(), new Dictionary<string, object> { ["type"] = r.Type, ["width_m"] = r.Width, ["oneway"] = r.OneWay }, proj);
            foreach (var c in gen.CyclePaths) Feature(features, "cycle_path", c.ToArray(), new Dictionary<string, object>(), proj);
            foreach (var p in gen.Pavements) Feature(features, "pavement", p.ToArray(), new Dictionary<string, object>(), proj);
        }
        foreach (var c in UnityEngine.Object.FindObjectsByType<PedestrianCrossing>())
            Feature(features, "pedestrian_crossing", new[] { c.transform.position }, new Dictionary<string, object> { ["name"] = c.name, ["heading_deg"] = c.transform.eulerAngles.y }, proj);
        foreach (var l in UnityEngine.Object.FindObjectsByType<CarTrafficLight>())
            Feature(features, "traffic_light", new[] { l.transform.position }, new Dictionary<string, object> { ["name"] = l.name, ["heading_deg"] = l.transform.eulerAngles.y }, proj);
        foreach (var s in UnityEngine.Object.FindObjectsByType<TrafficSign>())
            Feature(features, "traffic_sign", new[] { s.transform.position }, new Dictionary<string, object> { ["sign"] = s.Kind.ToString(), ["speed_limit"] = s.SpeedLimit, ["heading_deg"] = s.transform.eulerAngles.y }, proj);
        foreach (var w in UnityEngine.Object.FindObjectsByType<WaypointCircuit>())
        {
            var items = w.Waypoints?.Where(t => t != null).Select(t => t.position).ToArray() ?? new Vector3[0];
            if (items.Length < 2) continue;
            bool pedestrian = w.name.ToLower().Contains("ped");
            Feature(features, pedestrian ? "pedestrian_path" : "car_path", items, new Dictionary<string, object> { ["name"] = w.name }, proj);
        }
        var crs = proj == null ? "scene coordinates: x east (m), z north (m), y up" : "WGS84 lon/lat; scene_xz in metres (transverse Mercator around the map centre)";
        var name = OsmMap.Current != null && OsmMap.Current.Generator != null ? OsmMap.Current.Info.Name : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return "{\"type\":\"FeatureCollection\",\"name\":\"" + name + "\",\"coordinates\":\"" + crs + "\",\"features\":[\n" + string.Join(",\n", features) + "\n]}\n";
    }

    // ---- OBJ of the road surfaces

    static readonly string[] SurfaceWords = { "road", "asphalt", "crossing", "zebra", "pavement", "sidewalk", "kerb", "curb", "cycle path", "junction", "intersection", "marking" };
    static readonly string[] NotSurface = { "sign", "light", "lamp", "bench", "bin", "tree", "building", "car", "facade", "roof" };

    // road surfaces by the names of the object, its parent and its material (city maps: Roads,
    // Pavements, Kerbs, Zebra crossings, ...; the town: the RoadSystem's road meshes)
    static bool IsSurface(Renderer r)
    {
        string n = r.name.ToLowerInvariant() + " " + (r.transform.parent != null ? r.transform.parent.name.ToLowerInvariant() : "");
        if (r.sharedMaterial != null) n += " " + r.sharedMaterial.name.ToLowerInvariant();
        return SurfaceWords.Any(n.Contains) && !NotSurface.Any(n.Contains);
    }

    public static string Obj()
    {
        var sb = new StringBuilder("# Coupled Sim road surfaces in scene coordinates (m, as in the logs: x east, y up, z north; left-handed like Unity). Groups: object names.\n");
        int offset = 1;
        foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>())
        {
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || mf.sharedMesh == null || !IsSurface(r)) continue;
            var mesh = mf.sharedMesh;
            if (!mesh.isReadable) continue;
            sb.Append("g ").Append(mf.name.Replace(' ', '_')).Append('\n');
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mesh.vertices)
            {
                var w = m.MultiplyPoint3x4(v);
                sb.Append("v ").Append(w.x.ToString("F3", Ci)).Append(' ').Append(w.y.ToString("F3", Ci)).Append(' ').Append(w.z.ToString("F3", Ci)).Append('\n');
            }
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
                sb.Append("f ").Append(t[i] + offset).Append(' ').Append(t[i + 1] + offset).Append(' ').Append(t[i + 2] + offset).Append('\n');
            offset += mesh.vertexCount;
        }
        return sb.ToString();
    }
}
