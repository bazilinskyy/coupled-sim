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
    struct PassageBox { public V2 A, Dir, Side; public float Len, HalfWidth, Top; public V2 Min, Max; public int Line; }

    static readonly List<PassageBox> _passages = new List<PassageBox>();
    readonly List<(List<V2> line, float halfWidth, float top)> _passageLines = new List<(List<V2>, float, float)>();
    /// <summary>Streets through a building mapped below ground (layer below 0), which <see cref="Roads"/> leaves out: no
    /// through traffic, but riders to a destination find their way along them (De Zaale under Atlas on the TU/e campus).</summary>
    public readonly List<Road> PassageStreets = new List<Road>();

    /// <summary>The building passages on the map (road, cycle and foot passages).</summary>
    public int PassageCount => _passageLines.Count;

    /// <summary>Where the walls end at the passage's ends: a little beyond the building's outline.</summary>
    const float PassageOverhang = 1f;

    void FindPassages()
    {
        _passages.Clear(); _passageLines.Clear(); PassageStreets.Clear();
        var passageEnds = new HashSet<long>();
        void AddPassageStreet(OsmWay w, List<V2> line, string hw)
        {
            int ow = OneWayDir(w);
            var street = line.Select(q => P(q, 0.02f)).ToList();
            if (ow < 0) street.Reverse();
            PassageStreets.Add(new Road { Line = street, Width = RoadWidth(w), Type = hw, OneWay = ow != 0, SpeedLimitKmh = RoadSpeedLimits.Default(hw), NoThroughTraffic = true, PavedOver = true });
        }
        foreach (var w in _data.Ways)
        {
            var hw = w.Tag("highway");
            if (hw == null || w.Tag("tunnel") != "building_passage" || w.Tag("area") == "yes") continue;
            // (not a service road below ground: a car park's ramp or drive-through under a tower in Ho Chi Minh City, layer -1,
            // got walls at street level standing in the road, and a dog and a cyclist were held against them; a street mapped
            // below ground stays, as De Zaale under Atlas in Eindhoven, layer -1 along its whole length)
            if (int.TryParse(w.Tag("layer"), out var layer) && layer < 0 && hw == "service") continue;
            var line = Line(w);
            if (line.Count < 2 || !line.Any(Inside)) continue;
            bool car = CarRoads.Contains(hw);
            if (car && layer < 0) { AddPassageStreet(w, line, hw); passageEnds.Add(w.NodeIds[0]); passageEnds.Add(w.NodeIds[w.NodeIds.Count - 1]); }
            float width = car ? RoadWidth(w) : Math.Max(IsCycleway(w) ? 2.5f : 2f, ParseMetres(w.Tag("width"), 0f));
            // (a little room beside the carriageway, as a passage has a kerb or a walkway along it)
            float half = width / 2f + (car ? 0.6f : 0.3f);
            float maxHeight = ParseMetres(w.Tag("maxheight"), 0f);
            float top = maxHeight > 2f ? maxHeight + 0.3f : car ? 4.5f : 3.2f;
            // (its real size where the landmarks give it: De Zaale under Atlas is 11 m wide, the carriageway and its pavements, and
            // 4.2 m high, as the sign there says; by OpenStreetMap's estimate it was a 5 m tunnel the street ran into)
            if (_passageSizes.TryGetValue(w.Id, out var size)) { half = size.width / 2f; top = size.clearance; }
            // (an end inside a building runs on to its façade: De Zaale's ways under Atlas end short of the far façade, and the
            // passage was closed there by a wall)
            V2 Out(V2 end, V2 dir)
            {
                for (float k = 0.5f; k <= 30f && OsmBuildingAt(end + dir * k) != null; k += 0.5f) if (OsmBuildingAt(end + dir * (k + 0.5f)) == null) return end + dir * (k + 0.5f);
                return end;
            }
            if (line.Count >= 2 && OsmBuildingAt(line[0]) != null) line[0] = Out(line[0], V2.Normalize(line[0] - line[1]));
            if (line.Count >= 2 && OsmBuildingAt(line[line.Count - 1]) != null) line[line.Count - 1] = Out(line[line.Count - 1], V2.Normalize(line[line.Count - 1] - line[line.Count - 2]));
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
                    A = a0, Dir = d, Side = s, Len = l0, HalfWidth = half, Top = top, Line = _passageLines.Count - 1,
                    Min = new V2(c.Min(q => q.X), c.Min(q => q.Y)), Max = new V2(c.Max(q => q.X), c.Max(q => q.Y)),
                });
            }
        }
        if (_passageLines.Count > 0) Debug.Log($"OsmMap: {_passageLines.Count} building passages (streets, cycle paths and footways through buildings) opened");
        // (the short pieces below ground at a passage's ends that are not tagged as one: De Zaale's 3-4 m at Atlas's façades)
        foreach (var w in _data.Ways)
        {
            var hw = w.Tag("highway");
            if (hw == null || !CarRoads.Contains(hw) || hw == "service" || w.Tag("tunnel") == "building_passage" || w.Tag("tunnel") == "yes" || w.NodeIds.Count < 2) continue;
            if (!int.TryParse(w.Tag("layer"), out var layer) || layer >= 0) continue;
            if (!passageEnds.Contains(w.NodeIds[0]) && !passageEnds.Contains(w.NodeIds[w.NodeIds.Count - 1])) continue;
            var line = Line(w);
            if (line.Count >= 2 && line.Any(Inside)) AddPassageStreet(w, line, hw);
        }
    }

    /// <summary>
    /// Where passages cross (a footway through Atlas crosses De Zaale's passage; the entrances' halls under its tower cross
    /// De Zaale and a cycle path): what is left of one passage's lining at a point inside others. Its walls stand only above
    /// the others' openings (from the highest of their ceilings up to its own: a lower passage's walls and ceiling stood
    /// across the street in a higher one, and a higher one's walls left a hole above a lower one's ceiling); its ceiling
    /// only where no other passage there is higher (or as high and wider, or as high and as wide and earlier: one ceiling).
    /// </summary>
    static (float floor, bool ceiling) PassageCover(V2 q, int own, float half, float top)
    {
        float floor = -0.5f; bool ceiling = true;
        foreach (var b in _passages)
        {
            if (b.Line == own || q.X < b.Min.X || q.X > b.Max.X || q.Y < b.Min.Y || q.Y > b.Max.Y) continue;
            var r = q - b.A;
            float along = V2.Dot(r, b.Dir), across = V2.Dot(r, b.Side);
            if (along < 0f || along > b.Len || Math.Abs(across) > b.HalfWidth) continue;
            floor = Math.Max(floor, Math.Min(b.Top, top));
            if (b.Top > top + 0.05f || (Math.Abs(b.Top - top) <= 0.05f && (b.HalfWidth > half + 0.05f || (Math.Abs(b.HalfWidth - half) <= 0.05f && b.Line < own)))) ceiling = false;
        }
        return (floor, ceiling);
    }

    /// <summary>Whether a point on the ground lies in one of the passages.</summary>
    static bool InPassage(float x, float z)
    {
        foreach (var b in _passages)
        {
            if (x < b.Min.X || x > b.Max.X || z < b.Min.Y || z > b.Max.Y) continue;
            var r = new V2(x, z) - b.A;
            float along = V2.Dot(r, b.Dir), across = V2.Dot(r, b.Side);
            if (along >= 0f && along <= b.Len && Math.Abs(across) <= b.HalfWidth) return true;
        }
        return false;
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
        var walls = new MeshBuilder(); var ceiling = new MeshBuilder(); var fronts = new MeshBuilder();
        var frontMats = new Dictionary<Material, MeshBuilder>();
        for (int li = 0; li < _passageLines.Count; li++)
        {
            var (line, half, top) = _passageLines[li];
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i]; var b = line[i + 1]; float len = V2.Distance(a, b);
                if (len < 0.05f) continue;
                var d = (b - a) / len; var s = new V2(d.Y, -d.X);
                // (along this piece every 0.5 m, to the façades' line and a little beyond: in a building or not, and how much
                // of its lining is left where other passages cross it; each run of the same, to halfway to its neighbours)
                var xs = new List<float>(); var state = new List<(bool inB, float floor, bool ceil)>();
                for (float x = -PassageOverhang; x <= len + PassageOverhang + 1e-3f; x += 0.5f)
                {
                    var pq = a + d * Mathf.Clamp(x, 0f, len);
                    var (floor, ceil) = PassageCover(pq, li, half, top);
                    xs.Add(x); state.Add((InBuilding(pq), floor, ceil));
                }
                for (int k0 = 0; k0 < xs.Count;)
                {
                    int k1 = k0;
                    while (k1 + 1 < xs.Count && state[k1 + 1] == state[k0]) k1++;
                    var (inB, floor, ceil) = state[k0];
                    if (inB)
                    {
                        float x0 = Mathf.Max(-PassageOverhang, xs[k0] - 0.25f), x1 = Mathf.Min(len + PassageOverhang, xs[k1] + 0.25f);
                        var p0 = a + d * x0; var p1 = a + d * x1;
                        var l0 = p0 + s * half; var l1 = p1 + s * half; var r0 = p0 - s * half; var r1 = p1 - s * half;
                        float u = x1 - x0;
                        // walls (the right one faces left into the passage, the left one right), and the ceiling facing down
                        if (floor < top - 0.05f)
                        {
                            walls.Quad(P(l0, floor), P(l1, floor), P(l1, top), P(l0, top), new Vector2(0, floor / 3f), new Vector2(u / 3f, floor / 3f), new Vector2(u / 3f, top / 3f), new Vector2(0, top / 3f));
                            walls.Quad(P(r1, floor), P(r0, floor), P(r0, top), P(r1, top), new Vector2(0, floor / 3f), new Vector2(u / 3f, floor / 3f), new Vector2(u / 3f, top / 3f), new Vector2(0, top / 3f));
                        }
                        if (ceil) ceiling.Quad(P(l0, top), P(l1, top), P(r1, top), P(r0, top), new Vector2(0, 0), new Vector2(u / 3f, 0), new Vector2(u / 3f, 2f * half / 3f), new Vector2(0, 2f * half / 3f));
                    }
                    k0 = k1 + 1;
                }
            }
            // an end inside a building (a footway mapped up to a door: Atlas's entrances under its tower) closed by a front
            // across the passage, facing into it, in the building's landmark façade where it has one: through the open end
            // one looked into the empty inside of the real building's block, and through its walls' backs out the far side
            for (int e = 0; e < 2 && line.Count >= 2; e++)
            {
                var end = e == 0 ? line[0] : line[line.Count - 1];
                var prev = e == 0 ? line[1] : line[line.Count - 2];
                if (!InBuilding(end) || PassageCover(end, li, half, top).floor > -0.5f) continue;
                var d = V2.Normalize(end - prev); var s = new V2(d.Y, -d.X);
                var bw = OsmBuildingAt(end);
                var mat = bw != null && _facadeStyles.TryGetValue(bw.Id, out var st) && st.Mat != null ? st.Mat : null;
                MeshBuilder mb = fronts;
                if (mat != null && !frontMats.TryGetValue(mat, out mb)) frontMats[mat] = mb = new MeshBuilder();
                var l = end + s * half; var r = end - s * half;
                float w = 2f * half;
                // (facing back along the passage: seen from it, l is to the right)
                mb.Quad(P(r, -0.5f), P(r, top), P(l, top), P(l, -0.5f), new Vector2(0, -0.5f / 3f), new Vector2(0, top / 3f), new Vector2(w / 3f, top / 3f), new Vector2(w / 3f, -0.5f / 3f));
            }
        }
        Emit(buildings, "Building passage walls", walls, Mat("Building passage walls", new Color(0.62f, 0.61f, 0.58f), 0.1f), true);
        Emit(buildings, "Building passage ceilings", ceiling, Mat("Building passage ceilings", new Color(0.48f, 0.48f, 0.47f), 0.05f), true);
        Emit(buildings, "Building passage ends", fronts, Mat("Building passage walls", new Color(0.62f, 0.61f, 0.58f), 0.1f), true);
        foreach (var (mat, mb) in frontMats) Emit(buildings, "Building passage ends " + mat.name, mb, mat, true);
    }
}
