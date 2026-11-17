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
        /// <summary>Cars parked along the left / right kerb (as seen along the line) on the carriageway.</summary>
        public bool ParkLeft, ParkRight;
    }

    struct Edge { public int To, Road; }

    static long Key(Vector3 p) => ((long)Round(p.X * 2f) << 32) ^ (uint)Round(p.Z * 2f);

    /// <summary>Routes (lane centre lines, in driving order), longest first.</summary>
    /// <param name="halfSize">Half the map's size (m); the map is centred on the origin.</param>
    public static string LastStats = "";

    /// <param name="variants">Walks from each entry: the first goes straight on where it can, the others turn at a junction now and then (seeded, the same on every agent).</param>
    public static List<List<Vector3>> Build(IList<RoadLine> roads, float halfSize, bool leftHandTraffic, int max = 60, float minLength = 150f, int variants = 1, int seed = 7)
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
        var rnd = new Random(seed);
        for (int s = 0; s < pos.Count; s++)
        {
            if (!AtEdge(pos[s])) continue;
            foreach (var first in outs[s])
            for (int variant = 0; variant < variants; variant++)
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
                    var options = new List<Edge>();
                    foreach (var e in outs[cur])
                    {
                        if (e.To == from || seen.Contains(e.To)) continue;
                        var d = Flat(pos[e.To] - pos[cur]);
                        if (d.LengthSquared() < 1e-4f) continue;
                        float angle = Angle(inDir, d);
                        if (angle > 120f) continue;   // turns at T-junctions too, but no hairpins (cars cut them into poles and walls)
                        float score = angle + (e.Road == road ? 0f : 15f);   // straight on, and on the same road
                        if (score < bestScore) { bestScore = score; best = e; any = true; }
                        if (angle < 110f) options.Add(e);
                    }
                    if (!any) break;
                    // the variants turn off at a junction now and then
                    if (variant > 0 && options.Count > 1 && rnd.NextDouble() < 0.35) best = options[rnd.Next(options.Count)];
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
    /// <summary>
    /// How much of the carriageway a row of parked cars takes (m), as Dutch narrow streets do: fully on the road (2.1 m)
    /// where the traffic still has its lanes beside them (1.9 m from the centre line to the parked cars per direction,
    /// 2.4 m on a one-way street), else half on the kerb (1.0 m), else on the kerb (0.2 m). A parked car's centre is
    /// then <c>width / 2 + 1.1 - depth</c> from the road's centre line.
    /// </summary>
    public static float ParkDepth(float width, bool oneWay, int sides)
    {
        if (sides <= 0) return 0f;
        foreach (var depth in new[] { 2.1f, 1.0f })
            if (oneWay ? width - depth * sides >= 2.4f : width / 2f - depth >= 1.9f) return depth;
        return 0.2f;
    }

    /// <summary>The route at p (heading d) runs along the line's direction (not against it).</summary>
    static bool Aligned(List<Vector3> line, Vector3 p, Vector3 d)
    {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++) { float q = Vector3.DistanceSquared((line[i] + line[i + 1]) / 2f, p); if (q < bd) { bd = q; best = i; } }
        return line.Count < 2 || Vector3.Dot(Flat(line[Math.Min(best + 1, line.Count - 1)] - line[best]), d) >= 0f;
    }

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
            if (road.ParkLeft || road.ParkRight)
            {
                // parked cars take ParkDepth of the carriageway on their side: the lane runs between them and the centre
                // line (or in the middle of a one-way street's free part)
                bool along = Aligned(road.Line, p, d);
                int sides = (road.ParkLeft ? 1 : 0) + (road.ParkRight ? 1 : 0);
                float depth = ParkDepth(road.Width, road.OneWay, sides);
                float parkR = (along ? road.ParkRight : road.ParkLeft) ? depth : 0f, parkL = (along ? road.ParkLeft : road.ParkRight) ? depth : 0f;
                float half = road.Width / 2f;
                if (road.OneWay)
                {
                    float free = road.Width - parkL - parkR, x = (parkL - parkR) / 2f + (free >= 6.6f ? free / 4f : 0f);   // to the route's right
                    off = x * side;
                }
                else
                {
                    float free = half - (lht ? parkL : parkR);   // from the centre line to the parked cars on the driver's side
                    // (at least 0.95 m from the middle, so oncoming cars pass each other even on a very narrow street)
                    off = free >= 1.9f ? Clamp(Math.Min(free / 2f + 0.2f, free - 1.0f), 0.95f, 3.5f) : 0.95f;
                }
            }
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
