using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CoupledSim.Maps;
using UnityEngine;

/// <summary>
/// Real street trees, street lights and traffic signs of a map from open government registers:
/// StreamingAssets/Maps/{map}.{layer}.csv, made by Tools/city_data.py (first line "# credit", then a
/// header with lat and lon among the columns). The map builder uses them instead of what it would
/// otherwise derive from OpenStreetMap.
/// </summary>
public static class MapExtras
{
    public sealed class Record
    {
        public float X, Z;                                 // scene coordinates
        public Dictionary<string, string> Fields;
        public string this[string key] => Fields.TryGetValue(key, out var v) ? v : null;
        public float Float(string key, float fallback) =>
            float.TryParse(this[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
    }

    public static string PathFor(string map, string layer) => Path.Combine(Application.streamingAssetsPath, "Maps", $"{map}.{layer}.csv");

    /// <summary><c>-noRealData</c>: build maps from OpenStreetMap alone (comparisons).</summary>
    public static bool Disabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-noRealData") >= 0;

    /// <summary>The records of a layer, or null when the map has none. Credit: the file's first line.</summary>
    public static List<Record> Load(string map, string layer, GeoProjection proj, out string credit)
    {
        credit = null;
        if (Disabled || string.IsNullOrEmpty(map)) return null;
        var path = PathFor(map, layer);
        if (!File.Exists(path)) return null;
        var lines = File.ReadAllLines(path);
        int start = 0;
        if (lines.Length > 0 && lines[0].StartsWith("#")) { credit = lines[0].TrimStart('#', ' '); start = 1; }
        if (lines.Length <= start) return new List<Record>();
        var header = Split(lines[start]);
        int iLat = Array.IndexOf(header, "lat"), iLon = Array.IndexOf(header, "lon");
        if (iLat < 0 || iLon < 0) { Debug.LogWarning("MapExtras: no lat/lon columns in " + path); return null; }
        var list = new List<Record>();
        for (int i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            var f = Split(lines[i]);
            if (f.Length <= Math.Max(iLat, iLon)) continue;
            if (!double.TryParse(f[iLat], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(f[iLon], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;
            proj.ToLocal(lat, lon, out var x, out var z);
            var d = new Dictionary<string, string>();
            for (int k = 0; k < header.Length && k < f.Length; k++) d[header[k]] = f[k];
            list.Add(new Record { X = (float)x, Z = (float)z, Fields = d });
        }
        return list;
    }

    /// <summary>Surface polygons ({map}.surfaces.txt.gz from Tools/city_data.py: the Dutch BGT): kind and outline in
    /// scene coordinates. Null when the map has none.</summary>
    public static List<(string kind, List<Vector2> ring)> LoadSurfaces(string map, GeoProjection proj, out string credit)
    {
        credit = null;
        if (Disabled || string.IsNullOrEmpty(map)) return null;
        var path = Path.Combine(Application.streamingAssetsPath, "Maps", map + ".surfaces.txt.gz");
        if (!File.Exists(path)) return null;
        var list = new List<(string, List<Vector2>)>();
        using (var gz = new System.IO.Compression.GZipStream(File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            var head = r.ReadLine()?.Split(' ');
            if (head == null || head.Length < 3 || head[0] != "BGS1") { Debug.LogWarning("MapExtras: unknown surfaces file " + path); return null; }
            double lat0 = double.Parse(head[1], CultureInfo.InvariantCulture), lon0 = double.Parse(head[2], CultureInfo.InvariantCulture);
            var local = new GeoProjection(lat0, lon0);
            bool same = Math.Abs(lat0 - proj.Lat0) < 1e-9 && Math.Abs(lon0 - proj.Lon0) < 1e-9;
            credit = r.ReadLine();
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var f = line.Split(' ');
                if (f.Length < 7) continue;
                var ring = new List<Vector2>((f.Length - 1) / 2);
                for (int i = 1; i + 1 < f.Length; i += 2)
                {
                    double x = int.Parse(f[i], CultureInfo.InvariantCulture) / 100.0, z = int.Parse(f[i + 1], CultureInfo.InvariantCulture) / 100.0;
                    if (!same) { local.ToGeo(x, z, out var la, out var lo); proj.ToLocal(la, lo, out x, out z); }
                    ring.Add(new Vector2((float)x, (float)z));
                }
                list.Add((f[0], ring));
            }
        }
        return list;
    }

    // CSV with optional double quotes
    static string[] Split(string line)
    {
        var parts = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { parts.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        parts.Add(sb.ToString());
        return parts.ToArray();
    }
}
