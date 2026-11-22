using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace CoupledSim.Maps
{
    /// <summary>
    /// The area of a city map when it is more than the square of its data: boxes (the centre) and corridors (a strip
    /// along a route, e.g. a highway stretch), from StreamingAssets/Maps/{map}.area.txt (Tools/map_area.py). The map is
    /// built only inside it: the ground, buildings, surfaces and trees, and the traffic's routes start and end at its
    /// edge. In local metres (x east, z north as Vector2.X, Y) of the map's projection.
    /// </summary>
    public sealed class MapArea
    {
        struct Box { public float X0, Z0, X1, Z1; }
        struct Corridor { public List<Vector2> Line; public float HalfWidth; }

        readonly List<Box> _boxes = new List<Box>();
        readonly List<Corridor> _corridors = new List<Corridor>();
        // corridor segments by 100 m cell, for quick distance queries
        readonly Dictionary<long, List<(Vector2 a, Vector2 b, float hw)>> _cells = new Dictionary<long, List<(Vector2, Vector2, float)>>();
        const float Cell = 100f;
        float _maxHalfWidth;

        public float MinX = float.MaxValue, MinZ = float.MaxValue, MaxX = float.MinValue, MaxZ = float.MinValue;
        /// <summary>Total length of the corridors (m).</summary>
        public float CorridorLength { get; private set; }
        public int CorridorCount => _corridors.Count;

        /// <summary>Reads an area file; lat/lon go through <paramref name="toLocal"/> (the map's projection).</summary>
        public static MapArea Parse(string text, Func<double, double, Vector2> toLocal)
        {
            var area = new MapArea();
            var ci = CultureInfo.InvariantCulture;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw;
                int hash = line.IndexOf('#'); if (hash >= 0) line = line.Substring(0, hash);
                var f = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (f.Length == 0) continue;
                // ("far ..." lines: the far city around the area, for Tools/bag3d_far.py; not the area itself)
                if (f[0] == "far") continue;
                if (f[0] == "box" && f.Length >= 5)
                {
                    var a = toLocal(double.Parse(f[1], ci), double.Parse(f[2], ci)); var b = toLocal(double.Parse(f[3], ci), double.Parse(f[4], ci));
                    area.AddBox(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
                }
                else if (f[0] == "corridor" && f.Length >= 6)
                {
                    float width = float.Parse(f[1], ci);
                    var pts = new List<Vector2>();
                    for (int i = 2; i + 1 < f.Length; i += 2) pts.Add(toLocal(double.Parse(f[i], ci), double.Parse(f[i + 1], ci)));
                    area.AddCorridor(pts, width);
                }
                else throw new FormatException($"area: cannot read '{raw.Trim()}' (box south west north east | corridor width lat lon ...)");
            }
            return area;
        }

        public void AddBox(float x0, float z0, float x1, float z1)
        {
            _boxes.Add(new Box { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1 });
            Grow(x0, z0); Grow(x1, z1);
        }

        public void AddCorridor(List<Vector2> line, float width)
        {
            float hw = width / 2f;
            _corridors.Add(new Corridor { Line = line, HalfWidth = hw });
            _maxHalfWidth = Math.Max(_maxHalfWidth, hw);
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                CorridorLength += Vector2.Distance(a, b);
                Grow(Math.Min(a.X, b.X) - hw, Math.Min(a.Y, b.Y) - hw); Grow(Math.Max(a.X, b.X) + hw, Math.Max(a.Y, b.Y) + hw);
                int cx0 = (int)Math.Floor((Math.Min(a.X, b.X) - hw) / Cell), cx1 = (int)Math.Floor((Math.Max(a.X, b.X) + hw) / Cell);
                int cz0 = (int)Math.Floor((Math.Min(a.Y, b.Y) - hw) / Cell), cz1 = (int)Math.Floor((Math.Max(a.Y, b.Y) + hw) / Cell);
                for (int x = cx0; x <= cx1; x++) for (int z = cz0; z <= cz1; z++)
                {
                    long k = ((long)x << 32) ^ (uint)z;
                    if (!_cells.TryGetValue(k, out var l)) _cells[k] = l = new List<(Vector2, Vector2, float)>();
                    l.Add((a, b, hw));
                }
            }
        }

        void Grow(float x, float z) { MinX = Math.Min(MinX, x); MinZ = Math.Min(MinZ, z); MaxX = Math.Max(MaxX, x); MaxZ = Math.Max(MaxZ, z); }

        /// <summary>How far (m) a point lies inside the area: positive inside, negative outside (by about that much;
        /// far outside the corridors' cells: a large negative number).</summary>
        public float Depth(Vector2 p)
        {
            float best = -1e6f;
            foreach (var b in _boxes) best = Math.Max(best, Math.Min(Math.Min(p.X - b.X0, b.X1 - p.X), Math.Min(p.Y - b.Z0, b.Z1 - p.Y)));
            long k = ((long)(int)Math.Floor(p.X / Cell) << 32) ^ (uint)(int)Math.Floor(p.Y / Cell);
            if (_cells.TryGetValue(k, out var segs))
                foreach (var (a, b, hw) in segs) best = Math.Max(best, hw - SegmentDistance(p, a, b));
            return best;
        }

        public bool Contains(Vector2 p, float margin = 0f) => Depth(p) >= -margin;

        /// <summary>Whether any part of a square (centre, half side) may be in the area.</summary>
        public bool Touches(Vector2 centre, float half) => Depth(centre) > -half * 1.4143f;

        /// <summary>Whether a point is near the area's edge (within <paramref name="band"/> m inside it) or outside it:
        /// where traffic routes start and end.</summary>
        public bool AtEdge(Vector2 p, float band) => Depth(p) < band;

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float l2 = ab.LengthSquared();
            float t = l2 < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, Vector2.Dot(p - a, ab) / l2));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
