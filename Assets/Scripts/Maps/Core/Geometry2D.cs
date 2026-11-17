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
    }
}
