using System;
using System.Collections.Generic;
using System.Linq;
using CoupledSim.Maps;
using UnityEngine;
using V2 = System.Numerics.Vector2;

/// <summary>
/// Building passages: roads, cycle paths and footways that OpenStreetMap tags tunnel=building_passage run through a
/// building at street level (De Zaale under the TU/e's Atlas). The buildings' walls (generic, real 3D and landmark
/// façades) are cut away along them up to the passage's height, and the passage is lined with walls and a ceiling where
/// it is inside a building. Without them, the façade stood across the street, and the traffic and the autopilot stopped
/// at it.
/// </summary>
public partial class OsmMapGenerator
{
    /// <summary>A straight piece of a passage: from A along Dir for Len (m), HalfWidth to each side, up to Top.</summary>
    struct PassageBox { public V2 A, Dir, Side; public float Len, HalfWidth, Top; public V2 Min, Max; }

    static readonly List<PassageBox> _passages = new List<PassageBox>();
    readonly List<(List<V2> line, float halfWidth, float top)> _passageLines = new List<(List<V2>, float, float)>();

    /// <summary>The building passages on the map (road, cycle and foot passages).</summary>
    public int PassageCount => _passageLines.Count;

    /// <summary>Where the walls end at the passage's ends: a little beyond the building's outline.</summary>
    const float PassageOverhang = 1f;

    void FindPassages()
    {
        _passages.Clear(); _passageLines.Clear();
        foreach (var w in _data.Ways)
        {
            var hw = w.Tag("highway");
            if (hw == null || w.Tag("tunnel") != "building_passage" || w.Tag("area") == "yes") continue;
            // (not one below ground: a ramp down into a car park under a tower in Ho Chi Minh City, layer -1, got walls at
            // street level standing in the road, and a dog and a cyclist were held against them)
            if (int.TryParse(w.Tag("layer"), out var layer) && layer < 0) continue;
            var line = Line(w);
            if (line.Count < 2 || !line.Any(Inside)) continue;
            bool car = CarRoads.Contains(hw);
            float width = car ? RoadWidth(w) : Math.Max(IsCycleway(w) ? 2.5f : 2f, ParseMetres(w.Tag("width"), 0f));
            // (a little room beside the carriageway, as a passage has a kerb or a walkway along it)
            float half = width / 2f + (car ? 0.6f : 0.3f);
            float maxHeight = ParseMetres(w.Tag("maxheight"), 0f);
            float top = maxHeight > 2f ? maxHeight + 0.3f : car ? 4.5f : 3.2f;
            _passageLines.Add((line, half, top));
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i]; var b = line[i + 1]; float len = V2.Distance(a, b);
                if (len < 0.05f) continue;
                var d = (b - a) / len; var s = new V2(d.Y, -d.X);
                var a0 = a - d * PassageOverhang; float l0 = len + 2f * PassageOverhang;
                var c = new[] { a0 + s * half, a0 - s * half, a0 + d * l0 + s * half, a0 + d * l0 - s * half };
                _passages.Add(new PassageBox
                {
                    A = a0, Dir = d, Side = s, Len = l0, HalfWidth = half, Top = top,
                    Min = new V2(c.Min(q => q.X), c.Min(q => q.Y)), Max = new V2(c.Max(q => q.X), c.Max(q => q.Y)),
                });
            }
        }
        if (_passageLines.Count > 0) Debug.Log($"OsmMap: {_passageLines.Count} building passages (streets, cycle paths and footways through buildings) opened");
    }

    /// <summary>The mesh without what lies inside the passages (triangles cut at their walls and ceiling).</summary>
    static MeshBuilder CutPassages(MeshBuilder mb)
    {
        if (_passages.Count == 0 || mb.T.Count == 0) return mb;
        float topMax = _passages.Max(p => p.Top);
        var res = new MeshBuilder();
        res.V.AddRange(mb.V); res.UV.AddRange(mb.UV);
        var pieces = new List<List<(Vector3 p, Vector2 uv)>>();
        int cut = 0;
        for (int t = 0; t + 2 < mb.T.Count; t += 3)
        {
            int i0 = mb.T[t], i1 = mb.T[t + 1], i2 = mb.T[t + 2];
            Vector3 v0 = mb.V[i0], v1 = mb.V[i1], v2 = mb.V[i2];
            // (most triangles are nowhere near a passage: kept as they are)
            bool near = false;
            if (Mathf.Min(v0.y, Mathf.Min(v1.y, v2.y)) < topMax)
            {
                float minX = Mathf.Min(v0.x, Mathf.Min(v1.x, v2.x)), maxX = Mathf.Max(v0.x, Mathf.Max(v1.x, v2.x));
                float minZ = Mathf.Min(v0.z, Mathf.Min(v1.z, v2.z)), maxZ = Mathf.Max(v0.z, Mathf.Max(v1.z, v2.z));
                foreach (var b in _passages)
                    if (maxX >= b.Min.X && minX <= b.Max.X && maxZ >= b.Min.Y && minZ <= b.Max.Y) { near = true; break; }
            }
            if (!near) { res.T.Add(i0); res.T.Add(i1); res.T.Add(i2); continue; }
            pieces.Clear();
            pieces.Add(new List<(Vector3, Vector2)> { (v0, mb.UV[i0]), (v1, mb.UV[i1]), (v2, mb.UV[i2]) });
            bool changed = false;
            foreach (var b in _passages)
            {
                var next = new List<List<(Vector3 p, Vector2 uv)>>();
                foreach (var poly in pieces)
                {
                    var outside = SubtractBox(poly, b, out bool touched);
                    if (touched) changed = true;
                    next.AddRange(outside);
                }
                pieces = next;
                if (pieces.Count == 0) break;
            }
            if (!changed) { res.T.Add(i0); res.T.Add(i1); res.T.Add(i2); continue; }
            cut++;
            foreach (var poly in pieces)
            {
                int k = res.V.Count;
                foreach (var (p, uv) in poly) { res.V.Add(p); res.UV.Add(uv); }
                for (int j = 1; j + 1 < poly.Count; j++) { res.T.Add(k); res.T.Add(k + j); res.T.Add(k + j + 1); }
            }
        }
        return cut > 0 ? res : mb;
    }

    /// <summary>The parts of a convex polygon outside a passage's box (each convex), by clipping it at the box's planes in
    /// turn: what lies beyond a plane is outside; what is inside every plane is cut away.</summary>
    static List<List<(Vector3 p, Vector2 uv)>> SubtractBox(List<(Vector3 p, Vector2 uv)> poly, PassageBox b, out bool touched)
    {
        // (the planes as signed distances, positive outside: along the passage before its start and after its end, beside
        // it to either side, above its ceiling, below the ground)
        float D(int k, Vector3 q)
        {
            var r = new V2(q.x, q.z) - b.A;
            switch (k)
            {
                case 0: return -V2.Dot(r, b.Dir);
                case 1: return V2.Dot(r, b.Dir) - b.Len;
                case 2: return V2.Dot(r, b.Side) - b.HalfWidth;
                case 3: return -V2.Dot(r, b.Side) - b.HalfWidth;
                case 4: return q.y - b.Top;
                default: return -4f - q.y;
            }
        }
        var outParts = new List<List<(Vector3 p, Vector2 uv)>>();
        touched = false;
        var cur = poly;
        for (int k = 0; k < 6; k++)
        {
            var inside = new List<(Vector3 p, Vector2 uv)>(); var outside = new List<(Vector3 p, Vector2 uv)>();
            for (int i = 0; i < cur.Count; i++)
            {
                var a = cur[i]; var c = cur[(i + 1) % cur.Count];
                float da = D(k, a.p), dc = D(k, c.p);
                if (da <= 0f) inside.Add(a);
                if (da >= 0f) outside.Add(a);
                if ((da < 0f && dc > 0f) || (da > 0f && dc < 0f))
                {
                    float f = da / (da - dc);
                    var m = (Vector3.Lerp(a.p, c.p, f), Vector2.Lerp(a.uv, c.uv, f));
                    inside.Add(m); outside.Add(m);
                }
            }
            if (inside.Count < 3) { outParts.Add(cur); return outParts; }   // (wholly outside this plane: kept)
            touched = true;
            if (outside.Count >= 3) outParts.Add(outside);
            cur = inside;
        }
        return outParts;   // (what is inside all six is cut away)
    }

    /// <summary>The passages' walls and ceilings where they run inside a building (OpenStreetMap's outlines, or the real
    /// buildings' where there are some), facing into the passage.</summary>
    void EmitPassages(Transform buildings, List<Bag3dBuildings.Building> bag)
    {
        if (_passageLines.Count == 0) return;
        var outlines = _data.Ways.Where(w => w.IsClosed && (w.Tag("building") != null || w.Tag("building:part") != null)).Select(Line).Where(p => p.Count >= 4).ToList();
        var boxes = outlines.Select(p => (min: new V2(p.Min(q => q.X), p.Min(q => q.Y)), max: new V2(p.Max(q => q.X), p.Max(q => q.Y)))).ToList();
        bool InBuilding(V2 q)
        {
            for (int i = 0; i < outlines.Count; i++)
                if (q.X >= boxes[i].min.X && q.X <= boxes[i].max.X && q.Y >= boxes[i].min.Y && q.Y <= boxes[i].max.Y && Geometry2D.PointInPolygon(q, outlines[i])) return true;
            return false;
        }
        var walls = new MeshBuilder(); var ceiling = new MeshBuilder();
        foreach (var (line, half, top) in _passageLines)
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i]; var b = line[i + 1]; float len = V2.Distance(a, b);
                if (len < 0.05f) continue;
                var d = (b - a) / len; var s = new V2(d.Y, -d.X);
                // (the stretches of this piece inside a building, every 0.5 m, to the façades' line and a little beyond)
                float? from = null;
                for (float x = -PassageOverhang; x <= len + PassageOverhang + 1e-3f; x += 0.5f)
                {
                    bool inB = InBuilding(a + d * Mathf.Clamp(x, 0f, len));
                    if (inB && from == null) from = x;
                    if ((!inB || x + 0.5f > len + PassageOverhang + 1e-3f) && from != null)
                    {
                        float x0 = Mathf.Max(-PassageOverhang, from.Value - 0.5f), x1 = Mathf.Min(len + PassageOverhang, x);
                        var p0 = a + d * x0; var p1 = a + d * x1;
                        var l0 = p0 + s * half; var l1 = p1 + s * half; var r0 = p0 - s * half; var r1 = p1 - s * half;
                        float u = x1 - x0;
                        // walls (the right one faces left into the passage, the left one right), and the ceiling facing down
                        walls.Quad(P(l0, -0.5f), P(l1, -0.5f), P(l1, top), P(l0, top), new Vector2(0, -0.5f / 3f), new Vector2(u / 3f, -0.5f / 3f), new Vector2(u / 3f, top / 3f), new Vector2(0, top / 3f));
                        walls.Quad(P(r1, -0.5f), P(r0, -0.5f), P(r0, top), P(r1, top), new Vector2(0, -0.5f / 3f), new Vector2(u / 3f, -0.5f / 3f), new Vector2(u / 3f, top / 3f), new Vector2(0, top / 3f));
                        ceiling.Quad(P(l0, top), P(l1, top), P(r1, top), P(r0, top), new Vector2(0, 0), new Vector2(u / 3f, 0), new Vector2(u / 3f, 2f * half / 3f), new Vector2(0, 2f * half / 3f));
                        from = null;
                    }
                }
            }
        Emit(buildings, "Building passage walls", walls, Mat("Building passage walls", new Color(0.62f, 0.61f, 0.58f), 0.1f), true);
        Emit(buildings, "Building passage ceilings", ceiling, Mat("Building passage ceilings", new Color(0.48f, 0.48f, 0.47f), 0.05f), true);
    }
}
