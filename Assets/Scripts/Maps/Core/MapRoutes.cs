using System;
using System.Collections.Generic;
using System.Numerics;

namespace CoupledSim.Maps
{

/// <summary>
/// Lane routes across a city map, made from its roads: each route enters at the map's edge, drives straight on at
/// junctions as far as the roads allow (turning where the road it is on bends or ends), keeps to its side of the road
/// (right, or left in left-hand traffic) and leaves at the edge again, so cars that reach its end can start again out
/// of sight. Used by <see cref="MapTraffic"/> for background traffic without SUMO and for the participant's autopilot.
/// </summary>
public static class MapRoutes
{
    static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    static int Round(float v) => (int)Math.Round(v);
    static float Angle(Vector3 a, Vector3 b) { float d = a.Length() * b.Length(); return d < 1e-9f ? 0f : (float)(Math.Acos(Clamp(Vector3.Dot(a, b) / d, -1f, 1f)) * 180.0 / Math.PI); }
    static Vector3 Flat(Vector3 v) => new Vector3(v.X, 0f, v.Z);

    public struct RoadLine
    {
        public List<Vector3> Line;
        public float Width;
        public bool OneWay;
    }

    struct Edge { public int To, Road; }

    static long Key(Vector3 p) => ((long)Round(p.X * 2f) << 32) ^ (uint)Round(p.Z * 2f);

    /// <summary>Routes (lane centre lines, in driving order), longest first.</summary>
    /// <param name="halfSize">Half the map's size (m); the map is centred on the origin.</param>
    public static string LastStats = "";

    public static List<List<Vector3>> Build(IList<RoadLine> roads, float halfSize, bool leftHandTraffic, int max = 60, float minLength = 150f)
    {
        // the road network as a directed graph of the roads' points (shared points are junctions)
        var ids = new Dictionary<long, int>();
        var pos = new List<Vector3>();
        var outs = new List<List<Edge>>();
        int Node(Vector3 p)
        {
            var k = Key(p);
            if (!ids.TryGetValue(k, out var i)) { i = pos.Count; ids[k] = i; pos.Add(p); outs.Add(new List<Edge>()); }
            return i;
        }
        for (int r = 0; r < roads.Count; r++)
        {
            var l = roads[r].Line;
            if (l == null || l.Count < 2) continue;
            int prev = Node(l[0]);
            for (int i = 1; i < l.Count; i++)
            {
                int cur = Node(l[i]);
                if (cur == prev) continue;
                outs[prev].Add(new Edge { To = cur, Road = r });
                if (!roads[r].OneWay) outs[cur].Add(new Edge { To = prev, Road = r });
                prev = cur;
            }
        }
        bool AtEdge(Vector3 p) => Math.Max(Math.Abs(p.X), Math.Abs(p.Z)) > halfSize * 0.85f;

        var found = new List<(List<int> nodes, List<int> roadsOf, float len)>();
        for (int s = 0; s < pos.Count; s++)
        {
            if (!AtEdge(pos[s])) continue;
            foreach (var first in outs[s])
            {
                var dir0 = Flat(pos[first.To] - pos[s]);
                if (Vector3.Dot(dir0, -pos[s]) <= 0f) continue;   // into the map
                var nodes = new List<int> { s, first.To };
                var rs = new List<int> { first.Road };
                var seen = new HashSet<int> { s, first.To };
                float len = dir0.Length();
                int cur = first.To, from = s, road = first.Road;
                while (len < halfSize * 4f)
                {
                    if (len > 60f && AtEdge(pos[cur])) break;
                    var inDir = Vector3.Normalize(Flat(pos[cur] - pos[from]));
                    Edge best = default; float bestScore = float.MaxValue; bool any = false;
                    foreach (var e in outs[cur])
                    {
                        if (e.To == from || seen.Contains(e.To)) continue;
                        var d = Flat(pos[e.To] - pos[cur]);
                        if (d.LengthSquared() < 1e-4f) continue;
                        float angle = Angle(inDir, d);
                        if (angle > 135f) continue;   // turns at T-junctions too, but not back
                        float score = angle + (e.Road == road ? 0f : 15f);   // straight on, and on the same road
                        if (score < bestScore) { bestScore = score; best = e; any = true; }
                    }
                    if (!any) break;
                    len += Vector3.Distance(pos[cur], pos[best.To]);
                    from = cur; cur = best.To; road = best.Road;
                    nodes.Add(cur); rs.Add(road); seen.Add(cur);
                }
                if (len >= minLength && AtEdge(pos[cur])) found.Add((nodes, rs, len));
            }
        }
        found.Sort((a, b) => b.len.CompareTo(a.len));
        int junctions = 0, edgeStarts = 0;
        for (int i = 0; i < pos.Count; i++) { if (outs[i].Count >= 3) junctions++; if (AtEdge(pos[i])) edgeStarts++; }
        LastStats = $"{roads.Count} roads, {pos.Count} points, {junctions} junction points, {edgeStarts} points at the edge, {found.Count} candidate routes";

        // keep routes that mostly run where no kept route (in the same direction) runs yet
        var routes = new List<List<Vector3>>();
        var used = new Dictionary<long, Vector3>();   // 5 m cells -> direction
        long Cell(Vector3 p) => ((long)Math.Floor(p.X / 5f) << 32) ^ (uint)(int)Math.Floor(p.Z / 5f);
        foreach (var (nodes, rs, len) in found)
        {
            var lane = Offset(nodes, rs, pos, roads, leftHandTraffic);
            int overlap = 0, total = 0;
            for (int i = 1; i < lane.Count; i++)
            {
                var d = Vector3.Normalize(Flat(lane[i] - lane[i - 1]));
                total++;
                if (used.TryGetValue(Cell(lane[i]), out var u) && Vector3.Dot(u, d) > 0.7f) overlap++;
            }
            if (total == 0 || overlap > total * 0.6f) continue;
            for (int i = 1; i < lane.Count; i++) used[Cell(lane[i])] = Vector3.Normalize(Flat(lane[i] - lane[i - 1]));
            routes.Add(lane);
            if (routes.Count >= max) break;
        }
        return routes;
    }

    // the lane's centre: a quarter of the road's width to the driver's side of two-way roads (the middle of a
    // one-way road), points every few metres
    static List<Vector3> Offset(List<int> nodes, List<int> rs, List<Vector3> pos, IList<RoadLine> roads, bool lht)
    {
        var lane = new List<Vector3>();
        float side = lht ? -1f : 1f;
        for (int i = 0; i < nodes.Count; i++)
        {
            var p = pos[nodes[i]];
            var a = pos[nodes[Math.Max(0, i - 1)]]; var b = pos[nodes[Math.Min(nodes.Count - 1, i + 1)]];
            var d = Flat(b - a);
            if (d.LengthSquared() < 1e-6f) continue;
            d = Vector3.Normalize(d);
            var right = Vector3.Cross(Vector3.UnitY, d);
            var road = roads[rs[Math.Min(i, rs.Count - 1)]];
            float off = road.OneWay ? (road.Width >= 6.6f ? road.Width / 4f : 0f) : Clamp(road.Width / 4f, 1.5f, 3.5f);
            var q = p + right * side * off;
            if (lane.Count > 0)
            {
                // fill long segments, so the route follows the road and not a spline between far points
                var last = lane[lane.Count - 1];
                float seg = Vector3.Distance(last, q);
                int steps = (int)Math.Floor(seg / 8f);
                for (int k = 1; k < steps; k++) lane.Add(Vector3.Lerp(last, q, k / (float)steps));
                if (seg < 0.5f) continue;
            }
            lane.Add(q);
        }
        return lane;
    }

    public static float Length(List<Vector3> l)
    {
        float s = 0f;
        for (int i = 1; i < l.Count; i++) s += Vector3.Distance(l[i - 1], l[i]);
        return s;
    }
}
}
