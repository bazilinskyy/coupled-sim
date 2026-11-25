using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoupledSim.Maps;
using UnityEngine;
using V2 = System.Numerics.Vector2;

/// <summary>
/// A map's landmark details in three dimensions (lines of StreamingAssets/Maps/{map}.landmarks.txt, documented at the top
/// of the TU/e's Eindhoven.landmarks.txt): what makes a building recognisable that neither OpenStreetMap's tags nor 3D BAG's
/// block models carry, ported from carla-digitaltwins' campus twin (TwinModel/tools/landmarks.py: faceted_crown,
/// pyramid_roof, balcony, the roof letters' frame; tools/osm_detail.py: revolving doors and steps).
/// <code>
/// crown &lt;OSM way id&gt; &lt;panel width m&gt; &lt;panels per module&gt; &lt;crown height m&gt; &lt;relief m&gt; &lt;colour rrggbb&gt;
/// pyramidroof &lt;OSM way id&gt; &lt;centre lat&gt; &lt;centre lon&gt; &lt;side m&gt; &lt;cells&gt; &lt;roofing rrggbb&gt; &lt;glass rrggbb&gt;
/// balcony &lt;OSM way id&gt; &lt;bearing the front faces, degrees&gt; &lt;depth m&gt; &lt;height m&gt; &lt;module m&gt; &lt;railing rrggbb&gt;
/// entrance &lt;lat&gt; &lt;lon&gt; &lt;bearing the door faces, degrees&gt; revolving &lt;steps&gt; &lt;stairs width m&gt; &lt;landing depth m&gt;
/// letterframe &lt;OSM way id&gt; &lt;colour rrggbb&gt;
/// </code>
/// </summary>
public partial class OsmMapGenerator
{
    /// <summary>A band of precast elements with pyramid-shaped faces at the top of a building's outer walls.</summary>
    sealed class CrownSpec { public long Way; public float Panel, Height, Relief; public int PerModule; public Color Colour; }

    /// <summary>Square pyramids in a staggered diamond lattice over a raised square of a building's roof, which replaces the
    /// real (3D BAG) roof there; the heights (the platform, the apexes and the roof around) are measured from that roof.</summary>
    sealed class PyramidRoofSpec
    {
        public long Way; public V2 Centre; public float Side; public int Cells; public Color Roofing, Glass;
        public V2 U, W;   // (the square's axes: along the building's longest wall, and across it)
        public float Base = float.NaN, Apex = float.NaN, Around = float.NaN; public int Dropped;
        public bool Inside(V2 p, float grow) { var d = p - Centre; return Math.Abs(V2.Dot(d, U)) <= Side / 2f + grow && Math.Abs(V2.Dot(d, W)) <= Side / 2f + grow; }
    }

    sealed class BalconySpec { public long Way; public float Bearing, Depth, Height, Module; public Color Railing; }

    sealed class EntranceSpec { public V2 Door; public float Bearing; public string Kind; public int Steps; public float Width, Landing; }

    readonly List<CrownSpec> _crowns = new List<CrownSpec>();
    readonly List<PyramidRoofSpec> _pyramidRoofs = new List<PyramidRoofSpec>();
    readonly List<BalconySpec> _balconies = new List<BalconySpec>();
    readonly List<EntranceSpec> _entrances = new List<EntranceSpec>();
    /// <summary>Steel frames behind roof lettering, by the building's OSM way.</summary>
    readonly Dictionary<long, Color> _letterFrames = new Dictionary<long, Color>();
    /// <summary>The real (3D BAG) building that stands for a mapped building with landmark details: where its walls are and
    /// how high they reach.</summary>
    readonly Dictionary<long, Bag3dBuildings.Building> _detailBuildings = new Dictionary<long, Bag3dBuildings.Building>();
    public int LandmarkDetails;

    /// <summary>Stairs to a raised entrance: the rise of a step (16 cm, comfortable outdoor steps) and its tread.</summary>
    const float StepRise = 0.16f, StepTread = 0.3f;
    /// <summary>A revolving door's drum: radius and height (a three- or four-wing door for heavy use, 2.6-3 m across).</summary>
    const float DrumRadius = 1.4f, DrumHeight = 2.4f;

    /// <summary>Reads a landmark details line (see the summary); false if it is none.</summary>
    bool ParseLandmarkDetail(string[] f)
    {
        var ci = CultureInfo.InvariantCulture;
        float F(int i) => float.Parse(f[i], ci);
        V2 LatLon(int i) { _proj.ToLocal(double.Parse(f[i], ci), double.Parse(f[i + 1], ci), out var x, out var z); return new V2((float)x, (float)z); }
        switch (f[0])
        {
            case "crown" when f.Length >= 7:
                _crowns.Add(new CrownSpec { Way = long.Parse(f[1], ci), Panel = Mathf.Max(0.3f, F(2)), PerModule = Math.Max(1, int.Parse(f[3], ci)), Height = F(4), Relief = F(5), Colour = Hex(f[6]) });
                return true;
            case "pyramidroof" when f.Length >= 8:
                _pyramidRoofs.Add(new PyramidRoofSpec { Way = long.Parse(f[1], ci), Centre = LatLon(2), Side = F(4), Cells = Math.Max(2, int.Parse(f[5], ci)), Roofing = Hex(f[6]), Glass = Hex(f[7]) });
                return true;
            case "balcony" when f.Length >= 7:
                _balconies.Add(new BalconySpec { Way = long.Parse(f[1], ci), Bearing = F(2), Depth = F(3), Height = F(4), Module = Mathf.Max(1f, F(5)), Railing = Hex(f[6]) });
                return true;
            case "entrance" when f.Length >= 8:
                _entrances.Add(new EntranceSpec { Door = LatLon(1), Bearing = F(3), Kind = f[4], Steps = Math.Max(0, int.Parse(f[5], ci)), Width = F(6), Landing = F(7) });
                return true;
            case "letterframe" when f.Length >= 3:
                _letterFrames[long.Parse(f[1], ci)] = Hex(f[2]);
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ the real building behind a landmark

    /// <summary>
    /// While the real buildings are added: notes the one that stands for a mapped building with landmark details (its walls
    /// give the crown's and the balcony's line and height), and takes out of it what a pyramid roof replaces: the roof
    /// inside the square and the walls of its raised part. Neuron's raised atrium roof is in 3D BAG as a lumpy block (the
    /// laser points of twelve pyramids smoothed into a dome, 14-16.6 m over a 12.1 m roof), which the pyramids would cut
    /// through; its heights are what the pyramids are built to: the platform at the median of that roof, the apexes at its
    /// 99th percentile, the skirt down to the roof around it (as carla-digitaltwins' faceted_block does).
    /// </summary>
    void NoteLandmarkBuilding(OsmWay w, Bag3dBuildings.Building b)
    {
        if (w == null || !(_crowns.Any(c => c.Way == w.Id) || _balconies.Any(c => c.Way == w.Id) || _pyramidRoofs.Any(c => c.Way == w.Id))) return;
        // (a mapped building is one real one; should 3D BAG split it, the largest)
        if (!_detailBuildings.TryGetValue(w.Id, out var had) || had.V.Length < b.V.Length) _detailBuildings[w.Id] = b;
        foreach (var pr in _pyramidRoofs)
        {
            if (pr.Way != w.Id) continue;
            var poly = Outline(w);
            if (poly == null) continue;
            // the square's axes along the building's longest wall (Neuron's are north and east)
            V2 longest = V2.UnitX; float best = 0f;
            for (int i = 0; i < poly.Count; i++) { var e = poly[(i + 1) % poly.Count] - poly[i]; if (e.Length() > best) { best = e.Length(); longest = V2.Normalize(e); } }
            if (Math.Abs(longest.X) < Math.Abs(longest.Y)) longest = new V2(longest.Y, -longest.X);
            if (longest.X < 0) longest = -longest;
            pr.U = longest; pr.W = new V2(-longest.Y, longest.X);
            var inside = new List<float>(); var around = new List<float>();
            for (int t = 0; t + 2 < b.Roofs.Length; t += 3)
            {
                Vector3 a = b.V[b.Roofs[t]], c = b.V[b.Roofs[t + 1]], d = b.V[b.Roofs[t + 2]];
                var m = (a + c + d) / 3f; var q = new V2(m.x, m.z);
                if (pr.Inside(q, -1f)) { inside.Add(a.y); inside.Add(c.y); inside.Add(d.y); }
                else if (pr.Inside(q, 4f) && !pr.Inside(q, 0f)) around.Add(m.y);
            }
            if (inside.Count == 0) { Debug.LogWarning($"OsmMap: landmarks: no roof of building {w.Id} under its pyramid roof"); continue; }
            inside.Sort(); around.Sort();
            pr.Around = around.Count > 0 ? around[around.Count / 2] : inside[0];
            pr.Base = Mathf.Max(inside[inside.Count / 2], pr.Around);
            pr.Apex = inside[Mathf.Min(inside.Count - 1, (int)(inside.Count * 0.99f))];
            // (a roof flat under the square: pyramids of a third of a cell's diagonal)
            if (pr.Apex < pr.Base + 0.8f) pr.Apex = pr.Base + pr.Side / pr.Cells * 0.45f;
            // the roof inside the square, and the walls of its raised part (not the low roof around reaching in under it)
            var roofs = new List<int>(); var walls = new List<int>();
            for (int t = 0; t + 2 < b.Roofs.Length; t += 3)
            {
                Vector3 a = b.V[b.Roofs[t]], c = b.V[b.Roofs[t + 1]], d = b.V[b.Roofs[t + 2]];
                var m = (a + c + d) / 3f;
                bool all = pr.Inside(new V2(a.x, a.z), 1f) && pr.Inside(new V2(c.x, c.z), 1f) && pr.Inside(new V2(d.x, d.z), 1f);
                if (pr.Inside(new V2(m.x, m.z), 0.05f) && (m.y > pr.Around + 0.3f || all)) { pr.Dropped++; continue; }
                roofs.Add(b.Roofs[t]); roofs.Add(b.Roofs[t + 1]); roofs.Add(b.Roofs[t + 2]);
            }
            for (int t = 0; t + 2 < b.Walls.Length; t += 3)
            {
                Vector3 a = b.V[b.Walls[t]], c = b.V[b.Walls[t + 1]], d = b.V[b.Walls[t + 2]];
                var m = (a + c + d) / 3f;
                if (pr.Inside(new V2(m.x, m.z), 0.3f) && Mathf.Min(a.y, Mathf.Min(c.y, d.y)) > pr.Around - 0.5f) { pr.Dropped++; continue; }
                walls.Add(b.Walls[t]); walls.Add(b.Walls[t + 1]); walls.Add(b.Walls[t + 2]);
            }
            b.Roofs = roofs.ToArray(); b.Walls = walls.ToArray();
        }
    }

    /// <summary>A mapped building's outline, counter-clockwise (the outside to the right of each edge), without the closing
    /// point and with points along a straight wall merged (Neuron's north wall is mapped in three pieces).</summary>
    List<V2> Outline(OsmWay w)
    {
        var poly = Line(w);
        if (poly.Count < 4) return null;
        poly.RemoveAt(poly.Count - 1);
        if (Geometry2D.SignedArea(poly) < 0) poly.Reverse();
        for (int guard = 0; guard < 1000 && poly.Count > 3; guard++)
        {
            int drop = -1;
            for (int i = 0; i < poly.Count; i++)
            {
                V2 p = poly[(i + poly.Count - 1) % poly.Count], q = poly[i], r = poly[(i + 1) % poly.Count];
                var a = q - p; var c = r - q;
                if (a.Length() < 0.3f || (c.Length() > 1e-3f && Math.Abs(Geometry2D.Cross(V2.Normalize(a), V2.Normalize(c))) < 0.05f && V2.Dot(a, c) > 0f)) { drop = i; break; }
            }
            if (drop < 0) break;
            poly.RemoveAt(drop);
        }
        return poly;
    }

    /// <summary>A mapped building's walls where the real building has them: each edge of its outline moved onto the real
    /// walls along it (3D BAG's and OpenStreetMap's outlines, both from the BAG, differ by 10-30 cm), with the real walls'
    /// top along it (else the mapped height). (a, b, outward normal, top)</summary>
    List<(V2 a, V2 b, V2 n, float top)> RealWalls(OsmWay w)
    {
        var poly = Outline(w);
        var res = new List<(V2, V2, V2, float)>();
        if (poly == null) return res;
        _detailBuildings.TryGetValue(w.Id, out var bld);
        var wallVerts = bld == null ? null : bld.Walls.Distinct().Select(i => bld.V[i]).ToList();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            float len = V2.Distance(a, b); if (len < 0.5f) continue;
            var d = (b - a) / len; var n = new V2(d.Y, -d.X);
            float off = 0f, top = 0f; int k = 0;
            if (wallVerts != null)
                foreach (var v in wallVerts)
                {
                    var r = new V2(v.x, v.z) - a;
                    float along = V2.Dot(r, d), across = V2.Dot(r, n);
                    if (along < -0.5f || along > len + 0.5f || Math.Abs(across) > 1.5f) continue;   // (a long wall's vertices are its corners)
                    off += across; k++; top = Math.Max(top, v.y);
                }
            if (k > 0) off /= k; else top = BuildingHeight(w);
            res.Add((a + n * off, b + n * off, n, top));
        }
        return res;
    }

    // ------------------------------------------------------------------ geometry helpers

    /// <summary>A flat convex polygon facing <paramref name="facing"/> (its corners in either order), UVs in metres / 3
    /// across its plane.</summary>
    static void Face(MeshBuilder mb, Vector3 facing, params Vector3[] p)
    {
        if (p.Length < 3) return;
        var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        if (Vector3.Dot(n, facing) < 0f) Array.Reverse(p);
        var u = (p[1] - p[0]).normalized; if (u.sqrMagnitude < 1e-8f) u = Vector3.right;
        var v = Vector3.Cross(facing.normalized, u);
        Vector2 Uv(Vector3 q) => new Vector2(Vector3.Dot(q - p[0], u) / 3f, Vector3.Dot(q - p[0], v) / 3f);
        for (int i = 1; i + 1 < p.Length; i++) mb.Tri(p[0], p[i], p[i + 1], Uv(p[0]), Uv(p[i]), Uv(p[i + 1]));
    }

    /// <summary>A box: its centre, its three axes (unit vectors) and half sizes along them.</summary>
    static void OrientedBox(MeshBuilder mb, Vector3 c, Vector3 ax, Vector3 ay, Vector3 az, float hx, float hy, float hz)
    {
        Vector3 X = ax * hx, Y = ay * hy, Z = az * hz;
        Face(mb, ax, c + X - Y - Z, c + X + Y - Z, c + X + Y + Z, c + X - Y + Z);
        Face(mb, -ax, c - X - Y - Z, c - X + Y - Z, c - X + Y + Z, c - X - Y + Z);
        Face(mb, ay, c - X + Y - Z, c + X + Y - Z, c + X + Y + Z, c - X + Y + Z);
        Face(mb, -ay, c - X - Y - Z, c + X - Y - Z, c + X - Y + Z, c - X - Y + Z);
        Face(mb, az, c - X - Y + Z, c + X - Y + Z, c + X + Y + Z, c - X + Y + Z);
        Face(mb, -az, c - X - Y - Z, c + X - Y - Z, c + X + Y - Z, c - X + Y - Z);
    }

    /// <summary>A box standing on the ground: across <paramref name="r"/> (half width), along <paramref name="f"/> from
    /// <paramref name="f0"/> to <paramref name="f1"/> m from <paramref name="o"/>, from y0 up to y1.</summary>
    static void StandingBox(MeshBuilder mb, V2 o, V2 f, V2 r, float halfWidth, float f0, float f1, float y0, float y1)
    {
        var c = o + f * ((f0 + f1) / 2f);
        OrientedBox(mb, new Vector3(c.X, (y0 + y1) / 2f, c.Y), new Vector3(r.X, 0f, r.Y), Vector3.up, new Vector3(f.X, 0f, f.Y), halfWidth, (y1 - y0) / 2f, Math.Abs(f1 - f0) / 2f);
    }

    static Vector3 P3(V2 p, float y) => new Vector3(p.X, y, p.Y);

    /// <summary>See-through glass (URP Lit, transparent), as the bus shelters' panes.</summary>
    Material GlassMat(string name, Color c)
    {
        var m = Mat(name, c, 0.95f);
        if (m.GetFloat("_Surface") < 0.5f)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return m;
    }

    Material MetalMat(string name, Color c, float smooth, float metallic)
    {
        var m = Mat(name, c, smooth);
        m.SetFloat("_Metallic", metallic);
        return m;
    }

    /// <summary>
    /// On a map with terrain every vertex is lifted afterwards by the ground's height under it (TerrainHeight.Apply), so a
    /// real building's long wall, which has vertices only at its corners, rises straight between its ends, while a detail
    /// along it with vertices all the way followed the ground's dips: Neuron's crown sagged along its 87 m walls against
    /// their straight top. The vertices from <paramref name="from"/> on are moved so that, lifted, they lie as the wall
    /// from <paramref name="a"/> to <paramref name="b"/> does (its ends' lift, in between straight); with a = b, rigidly
    /// as the point a (a roof's middle).
    /// </summary>
    void LiftLikeWall(MeshBuilder mb, int from, V2 a, V2 b)
    {
        if (Terrain == null) return;
        float la = Terrain.Lift(a.X, a.Y), lb = Terrain.Lift(b.X, b.Y);
        var d = b - a; float l2 = d.LengthSquared();
        for (int i = from; i < mb.V.Count; i++)
        {
            var v = mb.V[i];
            float t = l2 > 1e-6f ? Mathf.Clamp01(V2.Dot(new V2(v.x, v.z) - a, d) / l2) : 0f;
            float lift = Terrain.Lift(v.x, v.z);
            if (float.IsFinite(lift) && float.IsFinite(la) && float.IsFinite(lb)) v.y += Mathf.Lerp(la, lb, t) - lift;
            mb.V[i] = v;
        }
    }

    // ------------------------------------------------------------------ the details

    /// <summary>Builds the landmark details (after the buildings, their passages and lettering).</summary>
    void EmitLandmarkDetails(Transform root)
    {
        if (_crowns.Count + _pyramidRoofs.Count + _balconies.Count + _entrances.Count == 0) return;
        var parent = new GameObject("Landmark details").transform;
        parent.SetParent(root, false);
        var ways = new Dictionary<long, OsmWay>();
        foreach (var w in _data.Ways) if (w.IsClosed && (_crowns.Any(c => c.Way == w.Id) || _pyramidRoofs.Any(c => c.Way == w.Id) || _balconies.Any(c => c.Way == w.Id))) ways[w.Id] = w;
        OsmWay Way(long id) { if (ways.TryGetValue(id, out var w)) return w; Debug.LogWarning($"OsmMap: landmarks: no building {id}"); return null; }
        foreach (var c in _crowns) { var w = Way(c.Way); if (w != null && Crown(parent, w, c)) LandmarkDetails++; }
        foreach (var p in _pyramidRoofs) { var w = Way(p.Way); if (w != null && PyramidRoof(parent, w, p)) LandmarkDetails++; }
        foreach (var b in _balconies) { var w = Way(b.Way); if (w != null && Balcony(parent, w, b)) LandmarkDetails++; }
        if (_entrances.Count > 0) LandmarkDetails += Entrances(parent);
        Debug.Log($"OsmMap: landmarks: {LandmarkDetails} details (crowns, pyramid roofs, balconies, entrances)");
    }

    /// <summary>
    /// A crown at the top of a building's outer walls (carla-digitaltwins' faceted_crown): a band <c>Height</c> high of
    /// precast elements <c>Panel</c> wide, each face a low pyramid standing <c>Relief</c> proud of the wall, one of every
    /// <c>PerModule</c> (on the structural module's line) deeper (1.8 times), on a backing of the same concrete (the band's
    /// ends, where a wall is no whole number of elements long). The elements run on the module grid from the wall's middle,
    /// as the balcony's columns do. Neuron's: white concrete, 2.525 m, four to the 10.1 m module.
    /// </summary>
    bool Crown(Transform parent, OsmWay w, CrownSpec c)
    {
        var mb = new MeshBuilder();
        int elements = 0;
        foreach (var (a, b, n, top) in RealWalls(w))
        {
            float len = V2.Distance(a, b); if (len < c.Panel) continue;
            var d = (b - a) / len; var nn = new Vector3(n.X, 0f, n.Y);
            int from = mb.V.Count;
            float zc = top - c.Height;
            // (the backing: 1.5 cm proud of the wall, over the glazing's texture)
            var a0 = a + n * 0.015f; var b0 = b + n * 0.015f;
            Face(mb, nn, P3(a0, zc), P3(b0, zc), P3(b0, top), P3(a0, top));
            float module = c.Panel * c.PerModule, margin = (len - Mathf.Floor(len / module) * module) / 2f;
            int k0 = -Mathf.FloorToInt(margin / c.Panel + 1e-4f);
            for (int k = k0; margin + (k + 1) * c.Panel <= len + 0.01f; k++)
            {
                float sa = margin + k * c.Panel, sb = sa + c.Panel;
                bool deep = ((k % c.PerModule) + c.PerModule) % c.PerModule == 0;
                float r = c.Relief * (deep ? 1.8f : 1f);
                var pa = a + d * sa + n * 0.02f; var pb = a + d * sb + n * 0.02f;
                Vector3 c0 = P3(pa, zc), c1 = P3(pb, zc), c2 = P3(pb, top), c3 = P3(pa, top);
                var apex = P3(a + d * ((sa + sb) / 2f) + n * (0.02f + r), (zc + top) / 2f);
                foreach (var (p, q) in new[] { (c0, c1), (c1, c2), (c2, c3), (c3, c0) })
                {
                    var fn = Vector3.Cross(q - p, apex - p);
                    Face(mb, Vector3.Dot(fn, nn) >= 0f ? fn : -fn, p, q, apex);
                }
                elements++;
            }
            LiftLikeWall(mb, from, a, b);
        }
        if (elements == 0) return false;
        Emit(parent, "Landmark crown " + w.Id, mb, Mat("Landmark precast " + ColorUtility.ToHtmlStringRGB(c.Colour), c.Colour, 0.25f), false);
        Debug.Log($"OsmMap: landmarks: crown on building {w.Id}, {elements} elements");
        return true;
    }

    /// <summary>
    /// Square pyramids on a staggered diamond lattice over a raised square of a roof (carla-digitaltwins' pyramid_roof):
    /// lattice points (i, j) with i + j odd, every side / cells; the inner ones are apexes, those on the square's edge stay
    /// at the platform (flat triangles), so a 6 by 6 square holds twelve pyramids, as Neuron's atrium roof (Heinze:
    /// "twelve points", 24 triangular glazing units of about 6 m², two per pyramid). The two faces of each pyramid towards
    /// the north carry an inset glazing triangle (north light into the atrium; which faces is assumed). A skirt closes the
    /// sides from the roof around up to the platform.
    /// </summary>
    bool PyramidRoof(Transform parent, OsmWay w, PyramidRoofSpec pr)
    {
        if (float.IsNaN(pr.Base))
        {
            // (no real building: on the mapped height, the platform 2 m above it)
            float h = BuildingHeight(w);
            pr.Around = h; pr.Base = h + 2f; pr.Apex = pr.Base + pr.Side / pr.Cells * 0.45f;
            if (pr.U == V2.Zero) { pr.U = V2.UnitX; pr.W = V2.UnitY; }
        }
        var roof = new MeshBuilder(); var glass = new MeshBuilder();
        float a = pr.Side / pr.Cells, half = pr.Side / 2f;
        Vector3 L(int i, int j, float y) { var q = pr.Centre + pr.U * (-half + i * a) + pr.W * (-half + j * a); return P3(q, y); }
        int pyramids = 0, panes = 0;
        for (int i = 0; i <= pr.Cells; i++)
            for (int j = 0; j <= pr.Cells; j++)
            {
                if ((i + j) % 2 == 0) continue;
                bool inner = i > 0 && i < pr.Cells && j > 0 && j < pr.Cells;
                var apex = L(i, j, inner ? pr.Apex : pr.Base);
                if (inner) pyramids++;
                foreach (var ((i1, j1), (i2, j2)) in new[] { ((i, j + 1), (i + 1, j)), ((i, j + 1), (i - 1, j)), ((i, j - 1), (i + 1, j)), ((i, j - 1), (i - 1, j)) })
                {
                    if (i1 < 0 || i1 > pr.Cells || j1 < 0 || j1 > pr.Cells || i2 < 0 || i2 > pr.Cells || j2 < 0 || j2 > pr.Cells) continue;
                    Vector3 b1 = L(i1, j1, pr.Base), b2 = L(i2, j2, pr.Base);
                    var n = Vector3.Cross(b1 - apex, b2 - apex); if (n.y < 0f) n = -n;
                    Face(roof, n, apex, b1, b2);
                    // the faces towards the north: a glazing triangle inset to 60 %, 3 cm proud
                    if (inner && n.normalized.z > 0.05f)
                    {
                        var cen = (apex + b1 + b2) / 3f; var lift = n.normalized * 0.03f;
                        Face(glass, n, cen + (apex - cen) * 0.6f + lift, cen + (b1 - cen) * 0.6f + lift, cen + (b2 - cen) * 0.6f + lift);
                        panes++;
                    }
                }
            }
        if (pr.Base - pr.Around > 0.2f)
            for (int s = 0; s < 4; s++)
            {
                var (p, q) = s switch { 0 => (L(0, 0, 0), L(pr.Cells, 0, 0)), 1 => (L(pr.Cells, 0, 0), L(pr.Cells, pr.Cells, 0)), 2 => (L(pr.Cells, pr.Cells, 0), L(0, pr.Cells, 0)), _ => (L(0, pr.Cells, 0), L(0, 0, 0)) };
                var mid = (p + q) / 2f; var outward = mid - P3(pr.Centre, 0f);
                Face(roof, outward, new Vector3(p.x, pr.Around - 0.05f, p.z), new Vector3(q.x, pr.Around - 0.05f, q.z), new Vector3(q.x, pr.Base, q.z), new Vector3(p.x, pr.Base, p.z));
            }
        LiftLikeWall(roof, 0, pr.Centre, pr.Centre); LiftLikeWall(glass, 0, pr.Centre, pr.Centre);
        var roofMat = MetalMat("Landmark roofing " + ColorUtility.ToHtmlStringRGB(pr.Roofing), pr.Roofing, 0.35f, 0.3f);
        Emit(parent, "Landmark pyramid roof " + w.Id, roof, roofMat, true);
        var glassMat = MetalMat("Landmark roof glazing " + ColorUtility.ToHtmlStringRGB(pr.Glass), pr.Glass, 0.92f, 0.5f);
        Emit(parent, "Landmark pyramid roof glazing " + w.Id, glass, glassMat, false);
        Debug.Log($"OsmMap: landmarks: pyramid roof on building {w.Id}: {pyramids} pyramids, {panes} panes, cell {a:F2} m, platform {pr.Base:F2} m, apexes {pr.Apex:F2} m, roof around {pr.Around:F2} m; {pr.Dropped} triangles of the real roof replaced");
        return pyramids > 0;
    }

    /// <summary>
    /// A balcony along a building's front facing a bearing (carla-digitaltwins' balcony): a concrete slab <c>Depth</c> deep
    /// at <c>Height</c> above the ground, a column under it on every module line (on the same grid as the crown's deeper
    /// elements) and a pair of tube railings per module at its edge, 1.08 m high with a knee rail. Neuron's south front:
    /// "balconies with red tube profiles" kept in the renovation (Stevens Van Dijck), 2 m deep at 4 m (assumed).
    /// </summary>
    bool Balcony(Transform parent, OsmWay w, BalconySpec bs)
    {
        var slab = new MeshBuilder(); var rails = new MeshBuilder();
        var want = new V2(Mathf.Sin(bs.Bearing * Mathf.Deg2Rad), Mathf.Cos(bs.Bearing * Mathf.Deg2Rad));
        int columns = 0, railings = 0;
        foreach (var (a, b, n, top) in RealWalls(w))
        {
            float len = V2.Distance(a, b);
            if (V2.Dot(n, want) < 0.95f || len < 5f) continue;
            var d = (b - a) / len; var r3 = new Vector3(d.X, 0f, d.Y);
            int slabFrom = slab.V.Count, railFrom = rails.V.Count;
            var mid = (a + b) / 2f;
            float g = GroundAt(mid + n * (bs.Depth + 1f), 0f), y = g + bs.Height;
            StandingBox(slab, a + d * (len / 2f), n, d, len / 2f, -0.05f, bs.Depth, y - 0.3f, y);
            float margin = (len - Mathf.Floor(len / bs.Module) * bs.Module) / 2f;
            for (int k = 0; margin + k * bs.Module <= len + 0.01f; k++)
            {
                float sm = margin + k * bs.Module;
                if (sm > 0.3f && sm < len - 0.3f)
                {
                    var cp = a + d * sm;
                    StandingBox(slab, cp, n, d, 0.2f, bs.Depth - 0.5f, bs.Depth - 0.1f, g - 0.6f, y - 0.3f);
                    columns++;
                }
                if (margin + (k + 1) * bs.Module > len + 0.01f) break;
                foreach (float off in new[] { 0.25f, 0.75f })
                {
                    var ec = a + d * (sm + bs.Module * off) + n * (bs.Depth - 0.08f);
                    foreach (float e in new[] { -0.9f, 0.9f })
                    {
                        var pp = ec + d * e;
                        OrientedBox(rails, P3(pp, y + 0.55f), r3, Vector3.up, new Vector3(n.X, 0f, n.Y), 0.03f, 0.55f, 0.03f);
                    }
                    foreach (float hz in new[] { 1.08f, 0.55f })
                        OrientedBox(rails, P3(ec, y + hz), r3, Vector3.up, new Vector3(n.X, 0f, n.Y), 0.93f, 0.03f, 0.03f);
                    railings++;
                }
            }
            LiftLikeWall(slab, slabFrom, a, b); LiftLikeWall(rails, railFrom, a, b);
        }
        if (columns + railings == 0) return false;
        Emit(parent, "Landmark balcony " + w.Id, slab, Mat("Landmark concrete", new Color(0.86f, 0.85f, 0.82f), 0.2f), true);
        Emit(parent, "Landmark balcony railings " + w.Id, rails, MetalMat("Landmark railings " + ColorUtility.ToHtmlStringRGB(bs.Railing), bs.Railing, 0.55f, 0.3f), true);
        Debug.Log($"OsmMap: landmarks: balcony on building {w.Id}: {columns} columns, {railings} railings");
        return true;
    }

    /// <summary>
    /// Raised entrances (carla-digitaltwins' osm_detail: steps and revolving doors): a landing against the façade at the
    /// top of a flight of steps (16 cm risers, 30 cm treads) rising towards the door, handrails on both sides, and on the
    /// landing a revolving door: a glass drum (two curved walls, open to the inside and the outside) with four glass wings
    /// on a post, under a canopy. The steps, the landing and the drum have colliders: people walk round them (the drum) or
    /// up them (one step is a kerb's height), and the map's pedestrians' paths keep clear of them.
    /// </summary>
    int Entrances(Transform parent)
    {
        var stairs = new MeshBuilder(); var rails = new MeshBuilder(); var glass = new MeshBuilder(); var frame = new MeshBuilder();
        int n = 0;
        foreach (var e in _entrances)
        {
            var f = new V2(Mathf.Sin(e.Bearing * Mathf.Deg2Rad), Mathf.Cos(e.Bearing * Mathf.Deg2Rad));
            var r = new V2(f.Y, -f.X);
            float flight = e.Steps * StepTread, rise = e.Steps * StepRise;
            float g = GroundAt(e.Door + f * (e.Landing + flight + 1.5f), 0f), top = g + rise;
            float landingHalf = Mathf.Max(e.Width, 2f * DrumRadius + 1.6f) / 2f;
            // the landing (from 30 cm inside the façade) and the steps (each from below the ground to its tread)
            if (e.Steps > 0)
            {
                StandingBox(stairs, e.Door, f, r, landingHalf, -0.3f, e.Landing, g - 0.3f, top);
                for (int k = 0; k < e.Steps; k++)
                {
                    float f0 = e.Landing + (e.Steps - 1 - k) * StepTread;
                    StandingBox(stairs, e.Door, f, r, e.Width / 2f, f0, f0 + StepTread, g - 0.3f, g + (k + 1) * StepRise);
                }
                // handrails: a rail 0.9 m above the nosings, posts at both ends of the flight
                var up = new Vector3(-f.X * StepTread, StepRise, -f.Y * StepTread).normalized;   // (along the rail, rising towards the door)
                float length = Mathf.Sqrt(flight * flight + rise * rise);
                foreach (float side in new[] { -1f, 1f })
                {
                    var o = e.Door + r * side * (e.Width / 2f - 0.08f);
                    var lo = o + f * (e.Landing + flight); var hi = o + f * e.Landing;
                    var mid = (lo + hi) / 2f;
                    var across = new Vector3(r.X, 0f, r.Y);
                    OrientedBox(rails, P3(mid, g + rise / 2f + 0.9f), up, Vector3.Cross(up, across).normalized, across, length / 2f + 0.1f, 0.025f, 0.025f);
                    foreach (var (pe, y) in new[] { (lo, g), (hi, top) })
                        OrientedBox(rails, P3(pe, y + 0.45f), across, Vector3.up, new Vector3(f.X, 0f, f.Y), 0.025f, 0.45f, 0.025f);
                }
            }
            if (e.Kind == "revolving") RevolvingDoor(glass, frame, e.Door + f * (DrumRadius * 0.6f), f, e.Steps > 0 ? top : g);
            n++;
        }
        Emit(parent, "Landmark entrance stairs", stairs, Mat("Landmark steps", new Color(0.62f, 0.61f, 0.58f), 0.2f), true);
        Emit(parent, "Landmark entrance handrails", rails, MetalMat("Landmark aluminium", new Color(0.67f, 0.69f, 0.71f), 0.6f, 0.8f), true);
        Emit(parent, "Landmark revolving doors", glass, GlassMat("Landmark door glass", new Color(0.6f, 0.68f, 0.7f, 0.25f)), true);
        Emit(parent, "Landmark revolving door frames", frame, MetalMat("Landmark aluminium", new Color(0.67f, 0.69f, 0.71f), 0.6f, 0.8f), true);
        Debug.Log($"OsmMap: landmarks: {n} raised entrances with revolving doors");
        return n;
    }

    /// <summary>A revolving door at <paramref name="c"/> (the drum's middle), its openings facing <paramref name="f"/> and
    /// back, on a floor at <paramref name="y"/>.</summary>
    static void RevolvingDoor(MeshBuilder glass, MeshBuilder frame, V2 c, V2 f, float y)
    {
        const int Segs = 8;
        float ang0 = Mathf.Atan2(f.Y, f.X);
        // the drum's two curved walls, across the door's axis (each glass pane both ways: seen from inside and outside)
        foreach (var (a0, a1) in new[] { (ang0 + Mathf.PI * 0.25f, ang0 + Mathf.PI * 0.75f), (ang0 - Mathf.PI * 0.75f, ang0 - Mathf.PI * 0.25f) })
            for (int i = 0; i < Segs; i++)
            {
                float t0 = a0 + (a1 - a0) * i / Segs, t1 = a0 + (a1 - a0) * (i + 1) / Segs, tm = (t0 + t1) / 2f;
                var p0 = c + new V2(Mathf.Cos(t0), Mathf.Sin(t0)) * DrumRadius; var p1 = c + new V2(Mathf.Cos(t1), Mathf.Sin(t1)) * DrumRadius;
                var nn = new Vector3(Mathf.Cos(tm), 0f, Mathf.Sin(tm));
                Face(glass, nn, P3(p0, y), P3(p1, y), P3(p1, y + DrumHeight), P3(p0, y + DrumHeight));
                Face(glass, -nn, P3(p0, y), P3(p1, y), P3(p1, y + DrumHeight), P3(p0, y + DrumHeight));
                // (the curved walls' frames: a mullion at each end, a sill and a head)
                if (i == 0 || i == Segs - 1)
                {
                    var pe = i == 0 ? p0 : p1;
                    OrientedBox(frame, P3(pe, y + DrumHeight / 2f), Vector3.right, Vector3.up, Vector3.forward, 0.04f, DrumHeight / 2f, 0.04f);
                }
            }
        // the four wings, at 45° to the façade, on a post
        for (int k = 0; k < 4; k++)
        {
            float a = ang0 + Mathf.PI / 4f + k * Mathf.PI / 2f;
            var d = new V2(Mathf.Cos(a), Mathf.Sin(a));
            var wc = c + d * (DrumRadius / 2f);
            OrientedBox(glass, P3(wc, y + DrumHeight / 2f), new Vector3(d.X, 0f, d.Y), Vector3.up, new Vector3(-d.Y, 0f, d.X), DrumRadius / 2f - 0.04f, DrumHeight / 2f - 0.08f, 0.012f);
            OrientedBox(frame, P3(c + d * (DrumRadius - 0.06f), y + DrumHeight / 2f), new Vector3(d.X, 0f, d.Y), Vector3.up, new Vector3(-d.Y, 0f, d.X), 0.025f, DrumHeight / 2f - 0.08f, 0.025f);
        }
        OrientedBox(frame, P3(c, y + DrumHeight / 2f), Vector3.right, Vector3.up, Vector3.forward, 0.06f, DrumHeight / 2f, 0.06f);
        // the canopy (a disc 25 cm deep over the drum) and the floor's ring
        const int Ring = 24;
        float rc = DrumRadius + 0.08f;
        for (int i = 0; i < Ring; i++)
        {
            float t0 = i * 2f * Mathf.PI / Ring, t1 = (i + 1) * 2f * Mathf.PI / Ring;
            var q0 = c + new V2(Mathf.Cos(t0), Mathf.Sin(t0)) * rc; var q1 = c + new V2(Mathf.Cos(t1), Mathf.Sin(t1)) * rc;
            Face(frame, Vector3.up, P3(c, y + DrumHeight + 0.25f), P3(q0, y + DrumHeight + 0.25f), P3(q1, y + DrumHeight + 0.25f));
            Face(frame, Vector3.down, P3(c, y + DrumHeight), P3(q0, y + DrumHeight), P3(q1, y + DrumHeight));
            var on = new Vector3(Mathf.Cos((t0 + t1) / 2f), 0f, Mathf.Sin((t0 + t1) / 2f));
            Face(frame, on, P3(q0, y + DrumHeight), P3(q1, y + DrumHeight), P3(q1, y + DrumHeight + 0.25f), P3(q0, y + DrumHeight + 0.25f));
            var f0 = c + new V2(Mathf.Cos(t0), Mathf.Sin(t0)) * DrumRadius; var f1 = c + new V2(Mathf.Cos(t1), Mathf.Sin(t1)) * DrumRadius;
            Face(frame, Vector3.up, P3(c, y + 0.012f), P3(f0, y + 0.012f), P3(f1, y + 0.012f));
        }
    }

    /// <summary>
    /// A steel frame behind roof lettering (carla-digitaltwins' roof_letters: letters of that size stand on a frame against
    /// the wind): posts every 2 m 0.6 m behind the letters, each on a foot reaching towards them, and two rails across at a
    /// quarter and four fifths of the letters' height.
    /// </summary>
    static void LetterFrame(MeshBuilder mb, Vector3 centre, Vector3 face, float width, float height, float depth)
    {
        face.y = 0f; face.Normalize();
        var right = Vector3.Cross(Vector3.up, -face).normalized;
        var back = centre - face * (depth / 2f + 0.6f);
        for (float x = 0.3f; x < width; x += 2f)
        {
            var p = back + right * (x - width / 2f);
            OrientedBox(mb, p + Vector3.up * (height * 0.5f + 0.1f), right, Vector3.up, face, 0.08f, height * 0.5f + 0.1f, 0.08f);
            OrientedBox(mb, p + face * 0.3f + Vector3.up * 0.15f, right, Vector3.up, face, 0.06f, 0.15f, 0.4f);
        }
        foreach (float zr in new[] { 0.25f * height, 0.8f * height })
            OrientedBox(mb, back + Vector3.up * zr, right, Vector3.up, face, width / 2f, 0.06f, 0.06f);
    }
}
