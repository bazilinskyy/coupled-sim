using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoupledSim.Maps;
using UnityEngine;
using V2 = System.Numerics.Vector2;

/// <summary>
/// Lane-level road markings from OpenStreetMap's lane tags: the lanes of each direction (<c>lanes</c>, <c>lanes:forward</c>,
/// <c>lanes:backward</c>) with dashed lines between lanes of one direction and a centre line between the directions
/// (solid on roads of three lanes or more), turn arrows before junctions (<c>turn:lanes</c>, <c>turn:lanes:forward</c>,
/// <c>turn:lanes:backward</c>), cycle lanes (<c>cycleway=lane</c> and its sides: a line, red asphalt in the Netherlands
/// and Belgium) and bus lanes (<c>busway=lane</c>, <c>lanes:bus</c>, <c>bus:lanes</c>: a solid line and "BUS").
/// </summary>
public partial class OsmMapGenerator
{
    public int LaneArrowCount, CycleLaneCount, BusLaneCount;

    static int TagInt(OsmWay w, string k) => int.TryParse(w.Tag(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;

    /// <summary>The lanes of a road: forward (along the way) and backward.</summary>
    static void LaneCounts(OsmWay w, float width, out int forward, out int backward)
    {
        int ow = OneWayDir(w);
        int n = TagInt(w, "lanes");
        if (n <= 0) n = ow != 0 ? Math.Max(1, (int)(width / 3.3f + 0.01f)) : (width >= 5.5f ? 2 : 1);   // one-way: a second lane from 6.6 m
        int f = TagInt(w, "lanes:forward"), b = TagInt(w, "lanes:backward");
        if (ow > 0) { forward = n; backward = 0; return; }
        if (ow < 0) { forward = 0; backward = n; return; }
        if (f < 0 && b < 0) { f = (n + 1) / 2; b = n - f; }
        else if (f < 0) f = Math.Max(0, n - b);
        else if (b < 0) b = Math.Max(0, n - f);
        forward = Math.Max(0, f); backward = Math.Max(0, b);
        if (forward + backward == 0) forward = 1;
    }

    /// <summary>Whether the kerb of a one-way road's traffic is on the way's right (drivers keep to their right in
    /// right-hand traffic, to their left in left-hand traffic; oneway=-1 runs against the way).</summary>
    static bool KerbOnRight(OsmWay w, bool lht) => OneWayDir(w) >= 0 != lht;

    /// <summary>Cycle lanes on the road's right and left side (as seen along the way). On a one-way road, cycleway=lane
    /// is at the kerb of its traffic and cycleway=opposite_lane (contraflow) on the other side.</summary>
    static void CycleLanes(OsmWay w, bool lht, out bool right, out bool left)
    {
        var both = w.Tag("cycleway") ?? w.Tag("cycleway:both");
        right = IsLane(w.Tag("cycleway:right"));
        left = IsLane(w.Tag("cycleway:left"));
        if (OneWayDir(w) == 0) { if (IsLane(both)) right = left = true; return; }
        bool kerbRight = KerbOnRight(w, lht);
        if (both == "lane") { if (kerbRight) right = true; else left = true; }
        if (both == "opposite_lane") { if (kerbRight) left = true; else right = true; }
    }

    static bool IsLane(string v) => v == "lane" || v == "opposite_lane";

    /// <summary>A bus lane on the right and left side (along the way).</summary>
    static void BusLanes(OsmWay w, bool lht, out bool right, out bool left)
    {
        int ow = OneWayDir(w);
        bool kerbRight = KerbOnRight(w, lht);
        var both = w.Tag("busway") ?? w.Tag("busway:both");
        right = w.Tag("busway:right") == "lane";
        left = w.Tag("busway:left") == "lane";
        if (both == "lane") { if (ow == 0) right = left = true; else if (kerbRight) right = true; else left = true; }
        bool Bus(string v) => v == "designated" || v == "only";
        string Tag2(string a, string b) => w.Tag(a) ?? w.Tag(b);
        // per-lane values are listed from the driver's left in the direction of travel (all lanes along the way when
        // a two-way road has no :forward/:backward)
        var bl = Tag2("bus:lanes", "psv:lanes");
        if (bl != null)
        {
            var p = bl.Split('|');
            if (p.Length > 1)
            {
                bool first = Bus(p[0]), last = Bus(p[p.Length - 1]);
                if (ow >= 0) { left |= first; right |= last; }
                else { right |= first; left |= last; }
            }
        }
        // the forward lanes lie on the way's right in right-hand traffic (the kerb lane last), on its left in
        // left-hand traffic (the kerb lane first); the backward lanes the other way round
        var blf = Tag2("bus:lanes:forward", "psv:lanes:forward");
        if (blf != null) { var p = blf.Split('|'); if (p.Length > 1 && Bus(p[lht ? 0 : p.Length - 1])) { if (lht) left = true; else right = true; } }
        var blb = Tag2("bus:lanes:backward", "psv:lanes:backward");
        if (blb != null) { var p = blb.Split('|'); if (p.Length > 1 && Bus(p[lht ? 0 : p.Length - 1])) { if (lht) right = true; else left = true; } }
        if (TagInt(w, "lanes:bus") > 0 || TagInt(w, "lanes:psv") > 0) { if (ow != 0 ? kerbRight : !lht) right = true; else left = true; }
    }

    /// <summary>The markings of one piece of a road between junctions.</summary>
    void LaneMarkings(OsmWay w, List<V2> piece, float width, float y, MeshBuilder marks, MeshBuilder redLanes, bool endsAtJunction, bool startsAtJunction)
    {
        bool lht = _map.LeftHandTraffic;
        LaneCounts(w, width, out int fwd, out int bwd);
        CycleLanes(w, lht, out bool cycR, out bool cycL);
        BusLanes(w, lht, out bool busR, out bool busL);
        int oneWay = OneWayDir(w);
        bool redCountry = _map.Country == "Netherlands" || _map.Country == "Belgium";
        const float edge = 0.25f, line = 0.12f, cycleW = 1.5f;
        var l2 = new List<V2>(); var r2 = new List<V2>();
        void Solid(float offset, float wdt = line) { Geometry2D.Strip(piece, wdt, offset, l2, r2); marks.Strip(l2, r2, y); }
        void Dashed(float offset, float dash = 3f, float gap = 6f)
        {
            var c = new List<V2>(); var d = new List<V2>();
            Geometry2D.Strip(piece, 0f, offset, c, d);
            Dashes(c, marks, line, dash, gap, y);
        }
        // the road's edges, inside its cycle lanes
        float rightEdge = width / 2 - edge, leftEdge = -width / 2 + edge;
        Solid(leftEdge); Solid(rightEdge);
        if (cycR || cycL)
        {
            foreach (var (on, sign) in new[] { (cycR, 1f), (cycL, -1f) })
            {
                if (!on) continue;
                float inner = sign * (width / 2 - cycleW);
                if (redCountry) { Geometry2D.Strip(piece, cycleW - edge, sign * (width / 2 - edge - (cycleW - edge) / 2), l2, r2); redLanes.Strip(l2, r2, y - 0.004f, 4f); Dashed(inner, 1f, 1f); }
                else Solid(inner);
                if (sign > 0) rightEdge = width / 2 - cycleW; else leftEdge = -width / 2 + cycleW;
                CycleLaneCount++;
            }
        }
        // bus lanes: the outermost lane of a side, behind a solid line, with "BUS" painted in it
        int n = fwd + bwd;
        float lw = (rightEdge - leftEdge) / Math.Max(1, n);
        if (n >= 2)
        {
            foreach (var (on, sign) in new[] { (busR, 1f), (busL, -1f) })
            {
                if (!on) continue;
                float edgeOff = sign > 0 ? rightEdge : leftEdge;
                Solid(edgeOff - sign * lw, 0.25f);
                BusText(piece, edgeOff - sign * lw / 2, y, marks, oneWay != 0 ? oneWay > 0 : sign > 0 != lht);   // read by the lane's traffic
                BusLaneCount++;
            }
        }
        // the lanes, left to right across the road (along the way): in right-hand traffic the backward lanes first
        int leftCount = lht ? fwd : bwd;
        for (int k = 1; k < n; k++)
        {
            float off = leftEdge + k * lw;
            if (k == leftCount && fwd > 0 && bwd > 0)
            {
                if (n >= 3) { Solid(off - 0.1f); Solid(off + 0.1f); }   // a double line between the directions of a wide road
                else Dashed(off);
            }
            else Dashed(off);
        }
        // turn arrows a little before the junction a lane leads to
        float len = Geometry2D.Length(piece);
        if (len < 25f) return;
        var turnF = w.Tag("turn:lanes:forward") ?? (OneWayDir(w) > 0 ? w.Tag("turn:lanes") : null);
        var turnB = w.Tag("turn:lanes:backward") ?? (OneWayDir(w) < 0 ? w.Tag("turn:lanes") : null);
        if (turnF != null && endsAtJunction && fwd > 0)
        {
            var lanes = turnF.Split('|');
            for (int i = 0; i < lanes.Length && i < fwd; i++)
            {
                // lane i from the left of its direction: forward lanes run right of the centre in right-hand traffic
                int idx = lht ? i : bwd + i;
                Arrow(piece, len - 12f, leftEdge + (idx + 0.5f) * lw, lanes[i], false, y, marks);
            }
        }
        if (turnB != null && startsAtJunction && bwd > 0)
        {
            var lanes = turnB.Split('|');
            for (int i = 0; i < lanes.Length && i < bwd; i++)
            {
                // backward lanes counted from their own left (against the way): in right-hand traffic the innermost is next to the centre
                int idx = lht ? fwd + (bwd - 1 - i) : (bwd - 1 - i);
                Arrow(piece, 12f, leftEdge + (idx + 0.5f) * lw, lanes[i], true, y, marks);
            }
        }
    }

    /// <summary>A turn arrow (5 m) on a lane: through, left, right, slight turns, merges, or several at once ("left;through").</summary>
    void Arrow(List<V2> line, float along, float offset, string turns, bool reverse, float y, MeshBuilder mb)
    {
        if (string.IsNullOrEmpty(turns) || turns == "none") return;
        var (p, dir) = Geometry2D.PointAt(line, Mathf.Clamp(along, 0f, Geometry2D.Length(line)));
        if (reverse) dir = -dir;
        // the lane's centre: offset is across the way, positive to its right
        var c = p + RightOf(line, along) * offset;
        var start = c - dir * 2.5f;
        var stem = start + dir * 3f;
        void Bar(V2 a, V2 b, float wdt)
        {
            var d = V2.Normalize(b - a); var n2 = new V2(d.Y, -d.X) * (wdt / 2);
            mb.Quad(P(a - n2, y), P(b - n2, y), P(b + n2, y), P(a + n2, y), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
        }
        void Head(V2 tip, V2 d)
        {
            var n2 = new V2(d.Y, -d.X);
            var b0 = tip - d * 1.2f;
            mb.Quad(P(b0 - n2 * 0.45f, y), P(tip, y), P(tip, y), P(b0 + n2 * 0.45f, y), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
        }
        Bar(start, stem, 0.18f);
        LaneArrowCount++;
        var leftDir = new V2(-dir.Y, dir.X);
        foreach (var t in turns.Split(';'))
        {
            switch (t.Trim())
            {
                case "through": Bar(stem, stem + dir * 0.8f, 0.18f); Head(stem + dir * 2f, dir); break;
                case "left": case "sharp_left": { var d = V2.Normalize(dir + leftDir * 1.2f); Bar(stem, stem + d * 0.6f, 0.18f); Head(stem + d * 1.8f, d); break; }
                case "right": case "sharp_right": { var d = V2.Normalize(dir - leftDir * 1.2f); Bar(stem, stem + d * 0.6f, 0.18f); Head(stem + d * 1.8f, d); break; }
                case "slight_left": case "merge_to_left": { var d = V2.Normalize(dir + leftDir * 0.45f); Bar(stem, stem + d * 0.8f, 0.18f); Head(stem + d * 2f, d); break; }
                case "slight_right": case "merge_to_right": { var d = V2.Normalize(dir - leftDir * 0.45f); Bar(stem, stem + d * 0.8f, 0.18f); Head(stem + d * 2f, d); break; }
                case "reverse": Head(stem + leftDir * 1.2f - dir * 0.3f, -dir); Bar(stem, stem + leftDir * 1.1f, 0.18f); break;
            }
        }
    }

    // the unit vector to the right of a line at a distance along it
    static V2 RightOf(List<V2> line, float along)
    {
        var (_, dir) = Geometry2D.PointAt(line, Mathf.Clamp(along, 0f, Geometry2D.Length(line)));
        return new V2(dir.Y, -dir.X);
    }

    /// <summary>"BUS" painted in a lane, read by those driving in it (letters 1.6 m long), every 60 m.</summary>
    void BusText(List<V2> line, float offset, float y, MeshBuilder mb, bool alongWay)
    {
        float len = Geometry2D.Length(line);
        for (float d = 15f; d < len - 10f; d += 60f)
        {
            var (p, dir) = Geometry2D.PointAt(line, d);
            if (!alongWay) dir = -dir;
            var c = p + RightOf(line, d) * offset;
            var across = new V2(dir.Y, -dir.X);   // to the reader's right
            // letters from the reader's near side: B, U, S (seven-segment strokes, 0.8 m wide, 1.6 m long)
            var letters = new[] { "B", "U", "S" };
            for (int i = 0; i < 3; i++)
            {
                var o = c + across * ((i - 1) * 1.05f);
                foreach (var (a, b) in Strokes(letters[i]))
                {
                    var pa = o + across * (a.X - 0.4f) + dir * (a.Y * 1.6f);
                    var pb = o + across * (b.X - 0.4f) + dir * (b.Y * 1.6f);
                    var dd = pb - pa; if (dd.LengthSquared() < 1e-6f) continue;
                    var u = V2.Normalize(dd); var nn = new V2(u.Y, -u.X) * 0.07f;
                    mb.Quad(P(pa - nn, y), P(pb - nn, y), P(pb + nn, y), P(pa + nn, y), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            }
        }
    }

    // strokes of the letters in a 0.8 x 1 box (x across, y along: 0 nearest the reader)
    static IEnumerable<(V2, V2)> Strokes(string ch)
    {
        V2 v(float x, float yy) => new V2(x, yy);
        switch (ch)
        {
            case "B": return new[] { (v(0, 0), v(0, 1)), (v(0, 1), v(0.65f, 1)), (v(0.65f, 1), v(0.8f, 0.75f)), (v(0.8f, 0.75f), v(0.65f, 0.5f)), (v(0, 0.5f), v(0.65f, 0.5f)), (v(0.65f, 0.5f), v(0.8f, 0.25f)), (v(0.8f, 0.25f), v(0.65f, 0)), (v(0.65f, 0), v(0, 0)) };
            case "U": return new[] { (v(0, 1), v(0, 0.15f)), (v(0, 0.15f), v(0.15f, 0)), (v(0.15f, 0), v(0.65f, 0)), (v(0.65f, 0), v(0.8f, 0.15f)), (v(0.8f, 0.15f), v(0.8f, 1)) };
            default: return new[] { (v(0.8f, 1), v(0, 1)), (v(0, 1), v(0, 0.5f)), (v(0, 0.5f), v(0.8f, 0.5f)), (v(0.8f, 0.5f), v(0.8f, 0)), (v(0.8f, 0), v(0, 0)) };
        }
    }
}
