using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// How busy a city's streets are, from CROWD (City Road Observations With Dashcams, crowd-dataset/crowd; CC BY 4.0):
/// the pedestrians and cars a dashcam sees per minute of urban driving there, against the world's (18.1 and 50.9),
/// as coupled-carla scales its walkers (coupledcarla/crowdpeds.py). The figures of the nearest CROWD locality within
/// 25 km of the map with 5 h of footage, else its country's (10 h), else its continent's, else the world's
/// (StreamingAssets/Crowd/crowd_density.csv). CROWD counts no cyclists: their factor is the country's cycling, by its
/// share of trips by bicycle (the Netherlands about 27 %, Denmark 16 %, Belgium and Germany about 10 %, Japan about
/// 13 %, most other countries 1-3 %).
/// </summary>
public static class TrafficDensity
{
    public struct Factors
    {
        /// <summary>Cars, pedestrians and cyclists against an average city (1: as the world's average).</summary>
        public float Cars, Pedestrians, Cyclists;
        /// <summary>At night: the pedestrians' share of the day's (CROWD's night and day footage of the same level).</summary>
        public float PedestriansAtNight;
        /// <summary>Where the figures come from ("locality New York", "country Netherlands", ...).</summary>
        public string From;
        public float PersonsPerMin, CarsPerMin;
    }

    struct Row { public string Level, Key, Name, Continent; public double Lat, Lon; public float Hours, Ppm, Cpm, PpmDay, PpmNight; }
    static List<Row> _rows;

    /// <summary>Cycling against an average country (1), by the country's share of trips by bicycle.</summary>
    static readonly Dictionary<string, float> CyclingByCountry = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { "Netherlands", 3f }, { "Denmark", 2.4f }, { "Belgium", 1.7f }, { "Germany", 1.5f }, { "Japan", 1.6f },
        { "Finland", 1.3f }, { "Sweden", 1.3f }, { "China", 1.5f }, { "Switzerland", 1.1f }, { "Austria", 1.1f },
        { "United States", 0.5f }, { "Canada", 0.6f }, { "Australia", 0.5f }, { "New Zealand", 0.5f },
        { "United Kingdom", 0.7f }, { "Spain", 0.6f }, { "Egypt", 0.4f }, { "Kenya", 0.5f }, { "Brazil", 0.6f },
    };

    static List<Row> Rows()
    {
        if (_rows != null) return _rows;
        _rows = new List<Row>();
        try
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Crowd", "crowd_density.csv");
            if (!File.Exists(path)) { Debug.LogWarning("TrafficDensity: " + path + " missing: every city as the world's average"); return _rows; }
            var ci = CultureInfo.InvariantCulture;
            float F(string s) => float.TryParse(s, NumberStyles.Float, ci, out var v) ? v : float.NaN;
            double D(string s) => double.TryParse(s, NumberStyles.Float, ci, out var v) ? v : double.NaN;
            bool header = true;
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("#")) continue;
                if (header) { header = false; continue; }
                var c = SplitCsv(line);
                if (c.Count < 11) continue;
                _rows.Add(new Row { Level = c[0], Key = c[1], Name = c[2], Continent = c[3], Lat = D(c[4]), Lon = D(c[5]), Hours = F(c[6]), Ppm = F(c[7]), Cpm = F(c[8]), PpmDay = F(c[9]), PpmNight = F(c[10]) });
            }
        }
        catch (Exception e) { Debug.LogWarning("TrafficDensity: " + e.Message); }
        return _rows;
    }

    static List<string> SplitCsv(string line)
    {
        var r = new List<string>(); var sb = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = !q; }
            else if (ch == ',' && !q) { r.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        r.Add(sb.ToString());
        return r;
    }

    /// <summary>The factors for a map in <paramref name="country"/> at lat, lon (degrees).</summary>
    public static Factors For(string country, double lat, double lon)
    {
        var rows = Rows();
        Row? world = null, cont = null, ctry = null, loc = null;
        double best = 25.0;
        foreach (var r in rows)
        {
            if (r.Level == "world") world = r;
            else if (r.Level == "country" && string.Equals(r.Name, country, StringComparison.OrdinalIgnoreCase)) ctry = r;
        }
        if (ctry.HasValue)
            foreach (var r in rows)
            {
                if (r.Level == "continent" && r.Name == ctry.Value.Continent) cont = r;
                if (r.Level != "locality" || r.Key != ctry.Value.Key || double.IsNaN(r.Lat)) continue;
                double km = Km(lat, lon, r.Lat, r.Lon);
                if (km < best && r.Hours >= 5f) { best = km; loc = r; }
            }
        var f = new Factors { Cars = 1f, Pedestrians = 1f, PedestriansAtNight = 0.6f, From = "no CROWD data (as the world's average)",
            Cyclists = CyclingByCountry.TryGetValue(country ?? "", out var cyc) ? cyc : 1f };
        if (!world.HasValue) return f;
        Row? use = loc ?? (ctry.HasValue && ctry.Value.Hours >= 10f ? ctry : null) ?? cont ?? world;
        var u = use.Value;
        string level = loc.HasValue ? "locality" : ctry.HasValue && use.Value.Level == "country" ? "country" : u.Level;
        f.From = $"{level} {u.Name}";
        f.PersonsPerMin = u.Ppm; f.CarsPerMin = u.Cpm;
        if (!float.IsNaN(u.Ppm) && world.Value.Ppm > 0f) f.Pedestrians = Mathf.Clamp(u.Ppm / world.Value.Ppm, 0.25f, 4f);
        if (!float.IsNaN(u.Cpm) && world.Value.Cpm > 0f) f.Cars = Mathf.Clamp(u.Cpm / world.Value.Cpm, 0.5f, 2.5f);
        // (night against day: from the first level that has both)
        foreach (var r in new[] { loc, ctry, cont, world })
            if (r.HasValue && !float.IsNaN(r.Value.PpmDay) && !float.IsNaN(r.Value.PpmNight) && r.Value.PpmDay > 0f)
            { f.PedestriansAtNight = Mathf.Clamp(r.Value.PpmNight / r.Value.PpmDay, 0.2f, 1f); break; }
        return f;
    }

    static double Km(double la1, double lo1, double la2, double lo2)
    {
        double r = 6371.0, p = Math.PI / 180.0;
        double a = Math.Sin((la2 - la1) * p / 2) * Math.Sin((la2 - la1) * p / 2) + Math.Cos(la1 * p) * Math.Cos(la2 * p) * Math.Sin((lo2 - lo1) * p / 2) * Math.Sin((lo2 - lo1) * p / 2);
        return 2 * r * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }
}
