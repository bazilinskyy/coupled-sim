using System;
using System.Collections.Generic;
using System.Numerics;

namespace CoupledSim.Maps
{
    /// <summary>2D geometry for map building (x east, y = Unity z north).</summary>
    public static class Geometry2D
    {
        /// <summary>Whether <paramref name="p"/> lies inside the polygon (even-odd rule).</summary>
        public static bool PointInPolygon(Vector2 p, IList<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) &&
                    p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    inside = !inside;
            return inside;
        }

        public static float SignedArea(IList<Vector2> poly)
        {
            double a = 0;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                a += (double)poly[j].X * poly[i].Y - (double)poly[i].X * poly[j].Y;
            return (float)(a / 2);
        }

        /// <summary>Triangulation of a polygon (a ring; holes bridged into it are fine) by <see cref="Earcut"/>. Returns
        /// index triples, counter-clockwise.</summary>
        public static List<int> Triangulate(IList<Vector2> poly)
        {
            var result = new List<int>();
            int n = poly.Count;
            if (n < 3) return result;
            var data = new double[2 * n];
            for (int i = 0; i < n; i++) { data[2 * i] = poly[i].X; data[2 * i + 1] = poly[i].Y; }
            var tris = Earcut.Triangulate(data);
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                float cr = Cross(poly[b] - poly[a], poly[c] - poly[a]);
                if (Math.Abs(cr) < 1e-9f) continue;   // a sliver along a bridge
                if (cr > 0) { result.Add(a); result.Add(b); result.Add(c); }
                else { result.Add(a); result.Add(c); result.Add(b); }
            }
            return result;
        }

        public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        /// <summary>
        /// Left and right edges of a strip of the given width along a polyline (mitred joins, limited
        /// at sharp angles). offset shifts the strip sideways (positive = to the right).
        /// </summary>
        public static void Strip(IList<Vector2> line, float width, float offset, List<Vector2> left, List<Vector2> right)
        {
            left.Clear(); right.Clear();
            int n = line.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 dirIn = i > 0 ? Vector2.Normalize(line[i] - line[i - 1]) : Vector2.Normalize(line[1] - line[0]);
                Vector2 dirOut = i < n - 1 ? Vector2.Normalize(line[i + 1] - line[i]) : dirIn;
                var tangent = dirIn + dirOut;
                tangent = tangent.LengthSquared() < 1e-6f ? dirOut : Vector2.Normalize(tangent);
                var normal = new Vector2(tangent.Y, -tangent.X); // right of the direction
                var nOut = new Vector2(dirOut.Y, -dirOut.X);
                float miter = 1f / Math.Max(0.3f, Vector2.Dot(normal, nOut));
                left.Add(line[i] + normal * (offset - width / 2) * miter);
                right.Add(line[i] + normal * (offset + width / 2) * miter);
            }
        }

        public static float Length(IList<Vector2> line)
        {
            float l = 0;
            for (int i = 1; i < line.Count; i++) l += Vector2.Distance(line[i - 1], line[i]);
            return l;
        }

        /// <summary>Point and direction at distance d along a polyline.</summary>
        public static (Vector2 point, Vector2 dir) PointAt(IList<Vector2> line, float d)
        {
            for (int i = 1; i < line.Count; i++)
            {
                float seg = Vector2.Distance(line[i - 1], line[i]);
                if (d <= seg || i == line.Count - 1)
                {
                    var dir = seg > 1e-6f ? (line[i] - line[i - 1]) / seg : Vector2.UnitY;
                    return (line[i - 1] + dir * Math.Min(d, seg), dir);
                }
                d -= seg;
            }
            return (line[0], Vector2.UnitY);
        }
    
        /// <summary>The part of a polyline between distances from and to (m) along it.</summary>
        public static List<Vector2> SubLine(IList<Vector2> line, float from, float to)
        {
            var result = new List<Vector2>();
            if (to <= from) return result;
            float acc = 0;
            result.Add(PointAt(line, from).point);
            for (int i = 1; i < line.Count; i++)
            {
                acc += Vector2.Distance(line[i - 1], line[i]);
                if (acc > from && acc < to) result.Add(line[i]);
            }
            result.Add(PointAt(line, to).point);
            return result;
        }

        /// <summary>
        /// Intervals [start, end] of a line of the given length that remain after cutting out
        /// [c - r, c + r] around each cut (centre c, half-length r); pieces shorter than minLength are dropped.
        /// </summary>
        public static List<(float start, float end)> Remaining(float length, IEnumerable<(float centre, float radius)> cuts, float minLength = 1f)
        {
            var removed = new List<(float a, float b)>();
            foreach (var (c, r) in cuts) removed.Add((Math.Max(0, c - r), Math.Min(length, c + r)));
            removed.Sort((x, y) => x.a.CompareTo(y.a));
            var result = new List<(float, float)>();
            float pos = 0;
            foreach (var (a, b) in removed)
            {
                if (a - pos >= minLength) result.Add((pos, a));
                pos = Math.Max(pos, b);
            }
            if (length - pos >= minLength) result.Add((pos, length));
            return result;
        }
        /// <summary>The kerb's rounding between two roads meeting at a node: arm a and arm b leave the node along the unit
        /// directions <paramref name="da"/> and <paramref name="db"/> (b counter-clockwise from a, 20-160° apart), with
        /// half widths <paramref name="ha"/> and <paramref name="hb"/>. Returns the patch that fills the square corner
        /// between their facing edges out to an arc of <paramref name="radius"/> tangent to both: the corner point first,
        /// then the arc from arm a's edge to arm b's (null where the arms are too close to straight or too sharp for it,
        /// or the arc would run more than <paramref name="maxTangent"/> along an edge).</summary>
        public static List<Vector2> CornerFillet(Vector2 node, Vector2 da, float ha, Vector2 db, float hb, float radius, float maxTangent = 8f, int arcPieces = 8)
        {
            double ang = Math.Atan2(db.Y, db.X) - Math.Atan2(da.Y, da.X);
            if (ang <= 0) ang += 2 * Math.PI;
            float theta = (float)ang;
            if (theta < 20f * MathF.PI / 180f || theta > 160f * MathF.PI / 180f) return null;
            // the facing edges: arm a's on its left (towards b), arm b's on its right (towards a)
            Vector2 pa = node + new Vector2(-da.Y, da.X) * ha, pb = node + new Vector2(db.Y, -db.X) * hb;
            float det = Cross(db, da);
            if (Math.Abs(det) < 1e-4f) return null;
            // where they cross (the square corner): pa + s da = pb + u db
            float s = Cross(db, pb - pa) / det;
            if (s < 0f) return null;
            var c = pa + da * s;
            float t = radius / MathF.Tan(theta / 2f);
            if (t > maxTangent) return null;
            var centre = c + Vector2.Normalize(da + db) * (radius / MathF.Sin(theta / 2f));
            Vector2 a = c + da * t, b = c + db * t;
            float a0 = MathF.Atan2(a.Y - centre.Y, a.X - centre.X), a1 = MathF.Atan2(b.Y - centre.Y, b.X - centre.X);
            float sweep = a1 - a0;
            while (sweep > MathF.PI) sweep -= 2f * MathF.PI;
            while (sweep < -MathF.PI) sweep += 2f * MathF.PI;
            var poly = new List<Vector2> { c, a };
            for (int k = 1; k < arcPieces; k++)
            {
                float q = a0 + sweep * k / arcPieces;
                poly.Add(centre + new Vector2(MathF.Cos(q), MathF.Sin(q)) * radius);
            }
            poly.Add(b);
            return poly;
        }
        /// <summary>Where a road goes on narrower (arm b counter-clockwise from arm a by 160-200°, their edges on that side
        /// <paramref name="ha"/> and <paramref name="hb"/> from the line), the step between their square ends filled
        /// with a taper along the narrower one, 4 m per metre of step and at least 3 m long (a lane moving over from the
        /// wider road's middle keeps its wheels on the road); null where they differ by less than 0.2 m.</summary>
        public static List<Vector2> WidthTaper(Vector2 node, Vector2 da, float ha, Vector2 db, float hb)
        {
            double ang = Math.Atan2(db.Y, db.X) - Math.Atan2(da.Y, da.X);
            if (ang <= 0) ang += 2 * Math.PI;
            if (ang < 160.0 * Math.PI / 180.0 || ang > 200.0 * Math.PI / 180.0 || Math.Abs(ha - hb) < 0.2f) return null;
            Vector2 na = new Vector2(-da.Y, da.X), nb = new Vector2(db.Y, -db.X);
            Vector2 pa = node + na * ha, pb = node + nb * hb;
            float len = Math.Max(3f, 4f * Math.Abs(ha - hb));
            return ha > hb ? new List<Vector2> { pa, pb, pb + db * len } : new List<Vector2> { pb, pa, pa + da * len };
        }
    }
}
