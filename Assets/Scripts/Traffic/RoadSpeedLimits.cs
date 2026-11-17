using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The speed limit (km/h) of the city map's roads at a point (OpenStreetMap's maxspeed, else a default by road type), for
/// the built-in traffic and the autopilot: on a motorway they drive at about its limit, in town as before.
/// </summary>
public static class RoadSpeedLimits
{
    static readonly Dictionary<long, List<(Vector3 a, Vector3 b, float kmh)>> _cells = new Dictionary<long, List<(Vector3, Vector3, float)>>();
    const float Cell = 20f;
    public static bool Any => _cells.Count > 0;

    public static void Build(IEnumerable<OsmMapGenerator.Road> roads)
    {
        _cells.Clear();
        foreach (var r in roads)
        {
            if (r.SpeedLimitKmh <= 0f || r.Line == null) continue;
            for (int i = 0; i + 1 < r.Line.Count; i++)
            {
                var a = r.Line[i]; var b = r.Line[i + 1];
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / Cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / Cell);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.z, b.z) / Cell), z1 = Mathf.FloorToInt(Mathf.Max(a.z, b.z) / Cell);
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                {
                    long k = ((long)x << 32) ^ (uint)z;
                    if (!_cells.TryGetValue(k, out var l)) _cells[k] = l = new List<(Vector3, Vector3, float)>();
                    l.Add((a, b, r.SpeedLimitKmh));
                }
            }
        }
    }

    /// <summary>The limit of the nearest road within 8 m (and 2 m in height, for roads over roads), or 0 when none.</summary>
    public static float At(Vector3 p)
    {
        if (!_cells.TryGetValue(((long)Mathf.FloorToInt(p.x / Cell) << 32) ^ (uint)Mathf.FloorToInt(p.z / Cell), out var segs)) return 0f;
        float best = 64f, kmh = 0f;
        foreach (var (a, b, v) in segs)
        {
            var ab = b - a; ab.y = 0f; var ap = p - a; ap.y = 0f;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude);
            var q = a + (b - a) * t;
            if (Mathf.Abs(q.y - p.y) > 2.5f) continue;
            var d = q - p; d.y = 0f;
            if (d.sqrMagnitude < best) { best = d.sqrMagnitude; kmh = v; }
        }
        return kmh;
    }

    /// <summary>The default limit (km/h) of a road type, where OpenStreetMap gives none (Dutch and most European values).</summary>
    public static float Default(string highway)
    {
        switch (highway ?? "")
        {
            case "motorway": return 100f;
            case "trunk": return 80f;
            case "motorway_link": case "trunk_link": return 60f;
            case "residential": case "unclassified": return 30f;
            case "living_street": return 15f;
            case "service": return 20f;
            default: return 50f;
        }
    }
}
