using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CoupledSim.Maps;
using UnityEngine;
using V2 = System.Numerics.Vector2;

/// <summary>
/// A map's landmarks (StreamingAssets/Maps/{map}.landmarks.txt): buildings whose real façade the generic styles do not
/// show (the TU/e's Atlas, a glass curtain wall with blinds behind it) and lettering on roofs and in the open (the TU/e
/// logo on Vertigo's roof and on the field before the Auditorium). Lines (# comments):
/// <code>
/// facade &lt;OSM way id&gt; curtainwall &lt;panel width m&gt; &lt;floor height m&gt; &lt;glass rrggbb&gt; &lt;frame rrggbb&gt; &lt;blinds 0-1&gt; [&lt;wall rrggbb&gt; &lt;window share of a bay 0-1&gt; &lt;spandrel height m&gt;]
/// roofletters &lt;OSM way id&gt; &lt;text&gt; &lt;letter height m&gt; &lt;colour rrggbb&gt; [city|bearing in degrees]
/// groundletters &lt;lat&gt; &lt;lon&gt; &lt;text&gt; &lt;letter height m&gt; &lt;colour rrggbb&gt; &lt;bearing the letters face, degrees&gt;
/// passage &lt;OSM way id&gt; &lt;opening's width m&gt; &lt;clearance m&gt;
/// </code>
/// and the details in three dimensions of OsmMapGenerator.LandmarkDetails (crown, pyramidroof, balcony, entrance,
/// letterframe: <see cref="ParseLandmarkDetail"/>). Lettering is built from strokes for T, U, /, e and a few more
/// (capitals and the logo's lowercase e).
/// </summary>
public partial class OsmMapGenerator
{
    sealed class FacadeStyle
    {
        public float Panel, Floor, Blinds; public Color Glass, Frame; public Material Mat; public MeshBuilder Mb;
        /// <summary>The wall between and below the windows (a spandrel band, a concrete or rendered wall), the share of each
        /// bay that is window (1: glass from mullion to mullion; less: windows or slots in a wall) and the spandrel's height (m).</summary>
        public Color Wall; public float Window = 1f, Spandrel = 0.38f; public bool HasWall;
    }
    readonly Dictionary<long, FacadeStyle> _facadeStyles = new Dictionary<long, FacadeStyle>();
    readonly List<string[]> _letterLines = new List<string[]>();
    /// <summary>Passages' real sizes (landmark lines "passage &lt;way&gt; &lt;width m&gt; &lt;clearance m&gt;"): the whole opening's width.</summary>
    readonly Dictionary<long, (float width, float clearance)> _passageSizes = new Dictionary<long, (float, float)>();
    /// <summary>The OSM building ways by 50 m cell, for finding which mapped building a real one is (its id, tags).</summary>
    Dictionary<(int, int), List<(OsmWay way, List<V2> poly)>> _osmBuildingCells;
    public int LandmarkFacades, LandmarkLetters;

    static Color Hex(string s) => s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
        ? new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f) : Color.gray;

    /// <summary>Reads the map's landmarks file, if it has one.</summary>
    void LoadLandmarks()
    {
        if (MapExtras.Disabled || string.IsNullOrEmpty(_map.Name)) return;
        var path = Path.Combine(Application.streamingAssetsPath, "Maps", _map.Name + ".landmarks.txt");
        if (!File.Exists(path)) return;
        var ci = CultureInfo.InvariantCulture;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw; int hash = line.IndexOf('#'); if (hash >= 0) line = line.Substring(0, hash);
            var f = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length == 0) continue;
            try
            {
                if (f[0] == "facade" && f.Length >= 8 && f[2] == "curtainwall")
                {
                    var st = new FacadeStyle { Panel = float.Parse(f[3], ci), Floor = float.Parse(f[4], ci), Glass = Hex(f[5]), Frame = Hex(f[6]), Blinds = float.Parse(f[7], ci) };
                    if (f.Length >= 11) { st.Wall = Hex(f[8]); st.Window = Mathf.Clamp(float.Parse(f[9], ci), 0.1f, 1f); st.Spandrel = Mathf.Clamp(float.Parse(f[10], ci), 0.1f, st.Floor * 0.7f); st.HasWall = true; }
                    _facadeStyles[long.Parse(f[1], ci)] = st;
                }
                else if ((f[0] == "roofletters" && f.Length >= 5) || (f[0] == "groundletters" && f.Length >= 7)) _letterLines.Add(f);
                else if (f[0] == "passage" && f.Length >= 4) _passageSizes[long.Parse(f[1], ci)] = (float.Parse(f[2], ci), float.Parse(f[3], ci));
                else if (ParseLandmarkDetail(f)) { }
                else Debug.LogWarning($"OsmMap: landmarks: cannot read '{raw.Trim()}'");
            }
            catch (Exception e) { Debug.LogWarning($"OsmMap: landmarks: '{raw.Trim()}': {e.Message}"); }
        }
    }

    /// <summary>The mapped building (an OSM way with building=*) at a point, or null.</summary>
    OsmWay OsmBuildingAt(V2 p)
    {
        if (_osmBuildingCells == null)
        {
            _osmBuildingCells = new Dictionary<(int, int), List<(OsmWay, List<V2>)>>();
            foreach (var w in _data.Ways)
            {
                if (!w.IsClosed || w.Tag("building") == null) continue;
                var poly = Line(w); if (poly.Count < 4) continue;
                float x0 = poly.Min(q => q.X), x1 = poly.Max(q => q.X), z0 = poly.Min(q => q.Y), z1 = poly.Max(q => q.Y);
                for (int i = (int)Math.Floor(x0 / 50f); i <= (int)Math.Floor(x1 / 50f); i++)
                    for (int j = (int)Math.Floor(z0 / 50f); j <= (int)Math.Floor(z1 / 50f); j++)
                    {
                        if (!_osmBuildingCells.TryGetValue((i, j), out var l)) _osmBuildingCells[(i, j)] = l = new List<(OsmWay, List<V2>)>();
                        l.Add((w, poly));
                    }
            }
        }
        if (!_osmBuildingCells.TryGetValue(((int)Math.Floor(p.X / 50f), (int)Math.Floor(p.Y / 50f)), out var cands)) return null;
        foreach (var (w, poly) in cands) if (Geometry2D.PointInPolygon(p, poly)) return w;
        return null;
    }

    /// <summary>The mapped building a real one (its vertices) stands in: the one that holds most of its corners on the
    /// ground, or the one at its middle (a footprint's middle can lie outside a mapped outline of another shape).</summary>
    OsmWay OsmBuildingFor(Vector3[] v)
    {
        var votes = new Dictionary<OsmWay, int>();
        // (up to 40 of its corners on the ground, not every so many of all its vertices: Neuron's model has 6 on the ground
        // among 429, every tenth vertex missed them all, and its landmark façade was never found)
        var ground = v.Where(q => q.y <= 0.5f).ToList();
        int step = Math.Max(1, ground.Count / 40);
        float mx = 0, mz = 0; int n = 0;
        for (int i = 0; i < ground.Count; i += step)
        {
            mx += ground[i].x; mz += ground[i].z; n++;
            var w = OsmBuildingAt(new V2(ground[i].x, ground[i].z));
            if (w != null) votes[w] = (votes.TryGetValue(w, out var c) ? c : 0) + 1;
        }
        var mid = n > 0 ? OsmBuildingAt(new V2(mx / n, mz / n)) : null;
        if (mid != null) votes[mid] = (votes.TryGetValue(mid, out var c2) ? c2 : 0) + 2;
        return votes.Count == 0 ? null : votes.OrderByDescending(kv => kv.Value).First().Key;
    }

    /// <summary>A landmark façade for this mapped building, or null: its mesh builder (walls added with UVs in 3 m).</summary>
    MeshBuilder LandmarkFacade(OsmWay w)
    {
        if (w == null || !_facadeStyles.TryGetValue(w.Id, out var st)) return null;
        if (st.Mb == null)
        {
            st.Mb = new MeshBuilder();
            st.Mat = CurtainWallMaterial("Curtain wall " + w.Id, st);
            LandmarkFacades++;
        }
        return st.Mb;
    }

    /// <summary>
    /// A façade of glass in bays (one texture of 8 bays by 4 floors, the façade's UVs in 3 m units scaled to it): a curtain
    /// wall of floor-high panes in aluminium frames with a spandrel band at each slab, or, with a wall colour, windows,
    /// ribbons or slots in a wall (concrete, render, cladding). Blinds behind the glass let down to different heights; each
    /// pane a little different in tint and tilted by a fraction of a degree, so the reflections of the sky and the street
    /// break up as on real glazing; the frames and walls stand proud of the glass (relief); at night some windows lit.
    /// </summary>
    Material CurtainWallMaterial(string name, FacadeStyle st)
    {
        const int PX = 8, FL = 4, W = 512, H = 512;
        var rng = new System.Random(name.GetHashCode());
        var blind = new float[PX * FL]; var tint = new float[PX * FL * 3]; var tilt = new float[PX * FL * 2]; var lit = new float[PX * FL];
        for (int i = 0; i < blind.Length; i++)
        {
            // (blinds down to a few common heights, as people leave them: a third, two thirds, all the way)
            blind[i] = rng.NextDouble() < st.Blinds ? new[] { 0.33f, 0.66f, 1f }[rng.Next(3)] : 0f;
            for (int c = 0; c < 3; c++) tint[i * 3 + c] = ((float)rng.NextDouble() - 0.5f) * 6f / 255f;
            tilt[i * 2] = ((float)rng.NextDouble() - 0.5f) * 2f; tilt[i * 2 + 1] = ((float)rng.NextDouble() - 0.5f) * 2f;
            lit[i] = rng.NextDouble() < 0.4 ? 0.55f + 0.45f * (float)rng.NextDouble() : 0f;
        }
        var wall = st.HasWall ? st.Wall : st.Frame * 0.82f;
        float slab = st.Spandrel / st.Floor, mullion = 0.05f / st.Panel, half = st.Window / 2f;
        // what is where in a bay (fu across 0-1, fv up from the slab 0-1): 0 glass, 1 frame, 2 wall
        int Part(float fu, float fv)
        {
            if (fv < slab) return 2;
            float d = Mathf.Abs(fu - 0.5f);
            if (d > half) return 2;
            if (d > half - mullion) return 1;
            if (fv > 1f - 0.02f) return 1;   // (the head of the window)
            if (st.Window > 0.9f && Mathf.Abs(fv - (slab + 0.85f / st.Floor)) < 0.02f) return 1;   // (an opening window's frame)
            return 0;
        }
        var tex = Tex(name, W, (x, y, r) =>
        {
            float u = x / (float)W * PX, v = y / (float)H * FL;
            int pi = Mathf.Min(PX - 1, (int)u), fi = Mathf.Min(FL - 1, (int)v); int k = fi * PX + pi;
            float fu = u - pi, fv = v - fi;
            switch (Part(fu, fv))
            {
                case 2: return Noise(wall, r, 0.04f);
                case 1: return Noise(st.Frame, r, 0.03f);
            }
            // glass: darker below, catching the sky towards the top, each pane its own tint; a blind behind it from the top down
            float gl = Mathf.Lerp(0.85f, 1.15f, (fv - slab) / Mathf.Max(0.01f, 1f - slab));
            var g = new Color(st.Glass.r + tint[k * 3], st.Glass.g + tint[k * 3 + 1], st.Glass.b + tint[k * 3 + 2]);
            float b = blind[k];
            if (b > 0f && fv > 1f - b * (1f - slab)) return Noise(Color.Lerp(g, new Color(0.78f, 0.79f, 0.8f), 0.35f), r, 0.03f);
            return Noise(g * gl, r, 0.02f);
        });
        var m = Mat(name, Color.white, 0.88f, tex);
        m.SetFloat("_Metallic", 0.35f);
        m.SetTextureScale("_BaseMap", new Vector2(3f / (st.Panel * PX), 3f / (st.Floor * FL)));
        // depth: frames 7 cm and walls 4 cm proud of the glass, each pane tilted by up to 0.7° (normal map, parallax)
        if (_assets != null && _assets.FacadeTemplate != null && !_noRelief)
        {
            float pixelM = st.Panel * PX / W;
            var (nrm, hgt, occ) = Relief("curtain " + name, W, H, pixelM, (x, y) =>
            {
                float u = x / (float)W * PX, v = y / (float)H * FL;
                int pi = Mathf.Min(PX - 1, (int)u), fi = Mathf.Min(FL - 1, (int)v); int k = fi * PX + pi;
                float fu = u - pi, fv = v - fi;
                switch (Part(fu, fv))
                {
                    case 2: return 0.04f;
                    case 1: return 0.07f;
                }
                return 0.012f * (tilt[k * 2] * (fu - 0.5f) + tilt[k * 2 + 1] * (fv - 0.5f));
            });
            m.shader = _assets.FacadeTemplate.shader;
            m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_ParallaxMap", hgt); m.SetFloat("_Parallax", 0.02f); m.EnableKeyword("_PARALLAXMAP");
            m.SetTexture("_OcclusionMap", occ); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
        }
        // at night: some windows lit, warm, dimmer behind a blind
        m.SetTexture("_EmissionMap", Tex(name + " lights", W, (x, y, r) =>
        {
            float u = x / (float)W * PX, v = y / (float)H * FL;
            int pi = Mathf.Min(PX - 1, (int)u), fi = Mathf.Min(FL - 1, (int)v); int k = fi * PX + pi;
            if (Part(u - pi, v - fi) != 0 || lit[k] <= 0f) return Color.black;
            return new Color(1f, 0.86f, 0.64f) * lit[k] * (blind[k] > 0f ? 0.6f : 1f);
        }));
        m.SetColor("_EmissionColor", Color.black);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        NightMaterials.Add(m);
        return m;
    }

    /// <summary>Emits the landmark façades' meshes (after the real buildings).</summary>
    void EmitLandmarkFacades(Transform buildings)
    {
        foreach (var st in _facadeStyles.Values)
            if (st.Mb != null && st.Mb.V.Count > 0) Emit(buildings, "Buildings " + st.Mat.name, st.Mb, st.Mat, true);
    }

    /// <summary>The lettering: on the roofs of mapped buildings (at the top of the real building there, along the roof's
    /// edge that faces the map's centre or a bearing) and standing on the ground.</summary>
    void EmitLetters(Transform root, List<Bag3dBuildings.Building> bag)
    {
        if (_letterLines.Count == 0) return;
        var ci = CultureInfo.InvariantCulture;
        var parent = new GameObject("Landmark lettering").transform;
        parent.SetParent(root, false);
        foreach (var f in _letterLines)
        {
            var mb = new MeshBuilder();
            if (f[0] == "roofletters")
            {
                long id = long.Parse(f[1], ci);
                var way = _data.Ways.FirstOrDefault(w => w.Id == id);
                if (way == null) { Debug.LogWarning($"OsmMap: landmarks: no building {id}"); continue; }
                var poly = Line(way); if (poly.Count < 4) continue;
                poly.RemoveAt(poly.Count - 1);
                if (Geometry2D.SignedArea(poly) < 0) poly.Reverse();
                var c = poly.Aggregate(V2.Zero, (s, q) => s + q) / poly.Count;
                float hgt = float.Parse(f[3], ci);
                // the roof to stand on: the real building's top roof, else the mapped outline at the mapped height
                var (roof, top, obstacles) = TopRoof(way, poly, bag);
                // the edge to stand on: the longest whose outside faces the map's centre (or the bearing given)
                V2 want = f.Length > 5 && f[5] != "city" && float.TryParse(f[5], NumberStyles.Float, ci, out var brg)
                    ? new V2(Mathf.Sin(brg * Mathf.Deg2Rad), Mathf.Cos(brg * Mathf.Deg2Rad)) : V2.Normalize(-c);
                // (the edges by how well they face it; the first with room for the letters, else the best one's middle)
                var edges = new List<(int i, float score)>();
                for (int i = 0; i < roof.Count; i++)
                {
                    var a = roof[i]; var b2 = roof[(i + 1) % roof.Count]; var e = b2 - a; float len = e.Length(); if (len < 1f) continue;
                    var outward = new V2(e.Y, -e.X) / len;   // (counter-clockwise ring: the outside is to the right)
                    edges.Add((i, V2.Dot(outward, want) * Mathf.Min(len, 4f * hgt)));
                }
                edges.Sort((x, y) => y.score.CompareTo(x.score));
                // a little in from the edge, and along it clear of the plant rooms on the roof (as carla-digitaltwins' twin
                // slides its letters): the letters and the frame behind them all on the roof and off anything higher; on the
                // next best edge if there is no room (Vertigo's south edge has a stair tower in its middle: its west edge)
                float wide = Lettering(new MeshBuilder(), f[2], Vector3.zero, Vector3.forward, hgt, 0.35f);
                bool frameToo = _letterFrames.ContainsKey(id);
                V2 dir = V2.UnitX, outN = V2.UnitY, mid = V2.Zero;
                bool Clear(V2 m)
                {
                    for (float u = -wide / 2f - 0.3f; u <= wide / 2f + 0.31f; u += 0.5f)
                        for (float v = -0.4f; v <= (frameToo ? 1.6f : 0.4f); v += 0.4f)
                        {
                            var q = m + dir * u - outN * v;
                            if (!Geometry2D.PointInPolygon(q, roof)) return false;
                            foreach (var (o0, o1, o2) in obstacles) if (NearTriangle(q, o0, o1, o2, 0.3f)) return false;
                        }
                    return true;
                }
                bool placed = false;
                foreach (var (ei, score) in edges.Where(e => e.score > 0f).DefaultIfEmpty(edges.Count > 0 ? edges[0] : (0, 0f)))
                {
                    var ea = roof[ei]; var eb = roof[(ei + 1) % roof.Count];
                    dir = V2.Normalize(eb - ea); outN = new V2(dir.Y, -dir.X);
                    float edgeLen = V2.Distance(ea, eb);
                    foreach (float setback in new[] { 0.8f, 1.5f, 2.5f, 4f })
                    {
                        for (float k = 0f; k <= edgeLen / 2f && !placed; k += 0.5f)
                            foreach (float sg in new[] { 1f, -1f })
                            {
                                var m = (ea + eb) / 2f - outN * setback + dir * (k * sg);
                                if (Clear(m)) { mid = m; placed = true; break; }
                            }
                        if (placed) break;
                    }
                    if (placed) break;
                }
                if (!placed && edges.Count > 0)
                {
                    var ea = roof[edges[0].i]; var eb = roof[(edges[0].i + 1) % roof.Count];
                    dir = V2.Normalize(eb - ea); outN = new V2(dir.Y, -dir.X); mid = (ea + eb) / 2f - outN * 0.8f;
                }
                if (!placed) Debug.LogWarning($"OsmMap: landmarks: no place clear of plant rooms on building {id} for '{f[2]}'");
                float width = Lettering(mb, f[2], new Vector3(mid.X, top, mid.Y), new Vector3(outN.X, 0f, outN.Y), hgt, 0.35f);
                // (a steel frame behind the letters, where a line asks for one)
                if (_letterFrames.TryGetValue(id, out var frameCol))
                {
                    var frame = new MeshBuilder();
                    LetterFrame(frame, new Vector3(mid.X, top, mid.Y), new Vector3(outN.X, 0f, outN.Y), width, hgt, 0.35f);
                    Emit(parent, "Lettering frame " + id, frame, MetalMat("Lettering frame " + ColorUtility.ToHtmlStringRGB(frameCol), frameCol, 0.5f, 0.7f), true);
                }
            }
            else
            {
                double lat = double.Parse(f[1], ci), lon = double.Parse(f[2], ci);
                _proj.ToLocal(lat, lon, out var x, out var z);
                float hgt = float.Parse(f[4], ci), brg = float.Parse(f[6], ci);
                var face = new Vector3(Mathf.Sin(brg * Mathf.Deg2Rad), 0f, Mathf.Cos(brg * Mathf.Deg2Rad));
                float gy = GroundAt(new V2((float)x, (float)z), 0f);
                Lettering(mb, f[3], new Vector3((float)x, gy, (float)z), face, hgt, 0.25f);
            }
            var col = Hex(f[0] == "roofletters" ? f[4] : f[5]);
            var mat = Mat("Lettering " + col, col, 0.45f);
            Emit(parent, "Lettering " + string.Join(" ", f.Take(3)), mb, mat, true);
            LandmarkLetters++;
        }
        Debug.Log($"OsmMap: landmarks: {LandmarkFacades} façades, {LandmarkLetters} lettering");
    }

    /// <summary>
    /// The top roof of a mapped building, for lettering to stand on: of the real (3D BAG) buildings that stand for it, the
    /// flat roof levels (triangles grouped by height), the largest within 6 m of the highest (as carla-digitaltwins' twin
    /// picks it), as the outline of its points (convex), its height, and what stands over 1.5 m higher on it (plant rooms,
    /// their roofs' triangles; not a parapet). Without a real building: the mapped outline at the mapped height. Vertigo's lettering stood on
    /// the mapped outline at its mapped height (31 m), in the air over the 20 m block south of its 57 m tower.
    /// </summary>
    (List<V2> roof, float top, List<(V2, V2, V2)> obstacles) TopRoof(OsmWay way, List<V2> poly, List<Bag3dBuildings.Building> bag)
    {
        var none = (poly, BuildingHeight(way), new List<(V2, V2, V2)>());
        if (bag == null) return none;
        float x0 = poly.Min(q => q.X) - 5f, x1 = poly.Max(q => q.X) + 5f, z0 = poly.Min(q => q.Y) - 5f, z1 = poly.Max(q => q.Y) + 5f;
        var tris = new List<(Vector3 a, Vector3 b, Vector3 c, float area, bool flat)>();
        foreach (var b in bag)
        {
            if (b.V.Length == 0 || !b.V.Any(v => v.x > x0 && v.x < x1 && v.z > z0 && v.z < z1) || OsmBuildingFor(b.V) != way) continue;
            for (int t = 0; t + 2 < b.Roofs.Length; t += 3)
            {
                Vector3 a = b.V[b.Roofs[t]], c = b.V[b.Roofs[t + 1]], d = b.V[b.Roofs[t + 2]];
                var n = Vector3.Cross(c - a, d - a); float area = n.magnitude / 2f;
                if (area > 1e-4f) tris.Add((a, c, d, area, Mathf.Abs(n.y) / (2f * area) > 0.9f));
            }
        }
        if (tris.Count == 0) return none;
        float H((Vector3 a, Vector3 b, Vector3 c, float area, bool flat) t) => (t.a.y + t.b.y + t.c.y) / 3f;
        float highest = tris.Max(H);
        // flat triangles by height, a new level where the next is over 45 cm higher
        var flat = tris.Where(t => t.flat).OrderBy(H).ToList();
        if (flat.Count == 0) return none;
        var levels = new List<List<(Vector3 a, Vector3 b, Vector3 c, float area, bool flat)>> { new List<(Vector3, Vector3, Vector3, float, bool)> { flat[0] } };
        for (int i = 1; i < flat.Count; i++)
        {
            if (H(flat[i]) - H(flat[i - 1]) > 0.45f) levels.Add(new List<(Vector3, Vector3, Vector3, float, bool)>());
            levels[levels.Count - 1].Add(flat[i]);
        }
        var main = levels.Where(l => l.Average(H) >= highest - 6f).OrderByDescending(l => l.Sum(t => t.area)).First();
        float top = main.Sum(t => H(t) * t.area) / main.Sum(t => t.area);
        var pts = main.SelectMany(t => new[] { new V2(t.a.x, t.a.z), new V2(t.b.x, t.b.z), new V2(t.c.x, t.c.z) }).ToList();
        // (within the mapped outline: a point of the model's roof 6 m out beyond Vertigo's west wall turned the hull's edge,
        // and the letters along it, 10° off the wall)
        var inside = pts.Where(q => Geometry2D.PointInPolygon(q, poly)).ToList();
        var hull = ConvexHull(inside.Count >= 3 ? inside : pts);
        if (hull.Count < 3) return none;
        var obstacles = tris.Where(t => H(t) > top + 1.5f).Select(t => (new V2(t.a.x, t.a.z), new V2(t.b.x, t.b.z), new V2(t.c.x, t.c.z))).ToList();
        return (hull, top, obstacles);
    }

    /// <summary>Whether a point lies in a triangle or within <paramref name="pad"/> of it.</summary>
    static bool NearTriangle(V2 q, V2 a, V2 b, V2 c, float pad)
    {
        float d1 = Geometry2D.Cross(b - a, q - a), d2 = Geometry2D.Cross(c - b, q - b), d3 = Geometry2D.Cross(a - c, q - c);
        if ((d1 >= 0 && d2 >= 0 && d3 >= 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0)) return true;
        float Seg(V2 p, V2 e) { var d = e - p; float t = Mathf.Clamp01(V2.Dot(q - p, d) / Math.Max(1e-6f, d.LengthSquared())); return V2.Distance(q, p + d * t); }
        return Seg(a, b) < pad || Seg(b, c) < pad || Seg(c, a) < pad;
    }

    /// <summary>The convex hull of points, counter-clockwise (monotone chain).</summary>
    static List<V2> ConvexHull(List<V2> pts)
    {
        var p = pts.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;
        var h = new List<V2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = h.Count;
            foreach (var q in pass == 0 ? p : Enumerable.Reverse(p))
            {
                while (h.Count >= start + 2 && Geometry2D.Cross(h[h.Count - 1] - h[h.Count - 2], q - h[h.Count - 2]) <= 0f) h.RemoveAt(h.Count - 1);
                h.Add(q);
            }
            h.RemoveAt(h.Count - 1);
        }
        return h;
    }

    /// <summary>
    /// Text standing upright, centred at <paramref name="centre"/> (its base), facing <paramref name="face"/> (towards its
    /// reader, for whom it reads from left to right), letters <paramref name="height"/> high and <paramref name="depth"/>
    /// deep, from strokes (a bold sans: the TU/e logo's capitals, slash and lowercase e). Returns its width.
    /// </summary>
    static float Lettering(MeshBuilder mb, string text, Vector3 centre, Vector3 face, float height, float depth)
    {
        face.y = 0f; face.Normalize();
        // (the reader looks along -face: their right is up x forward)
        var right = Vector3.Cross(Vector3.up, -face).normalized;
        float t = 0.2f * height;   // stroke width
        var strokes = new List<(float x0, float y0, float x1, float y1, float w)>(); float x = 0f;
        foreach (char ch in text)
        {
            float w = Glyph(ch, x, height, t, strokes);
            x += w + 0.18f * height;
        }
        float total = x - 0.18f * height;
        foreach (var (x0, y0, x1, y1, sw) in strokes)
        {
            var p0 = centre + right * (x0 - total / 2f) + Vector3.up * y0;
            var p1 = centre + right * (x1 - total / 2f) + Vector3.up * y1;
            Stroke(mb, p0, p1, sw, face, depth);
        }
        return total;
    }

    /// <summary>Adds a glyph's strokes (in letter units: x from <paramref name="x"/>, y up from the base); returns its width.</summary>
    static float Glyph(char ch, float x, float h, float t, List<(float, float, float, float, float)> s)
    {
        float hw = t / 2f;
        switch (ch)
        {
            case 'T':
            {
                float w = 0.62f * h;
                s.Add((x, h - hw, x + w, h - hw, t)); s.Add((x + w / 2f, 0f, x + w / 2f, h - t, t));
                return w;
            }
            case 'U':
            {
                float w = 0.62f * h, r = (w - t) / 2f, cx = x + w / 2f, cy = r + hw;
                s.Add((x + hw, h, x + hw, cy, t)); s.Add((x + w - hw, h, x + w - hw, cy, t));
                for (int k = 0; k < 8; k++)
                {
                    float a0 = Mathf.PI + k * Mathf.PI / 8f, a1 = Mathf.PI + (k + 1) * Mathf.PI / 8f;
                    s.Add((cx + r * Mathf.Cos(a0), cy + r * Mathf.Sin(a0), cx + r * Mathf.Cos(a1), cy + r * Mathf.Sin(a1), t));
                }
                return w;
            }
            case '/':
            {
                float w = 0.38f * h;
                s.Add((x + hw, -0.05f * h, x + w - hw, h + 0.05f * h, t));
                return w;
            }
            case 'e':
            {
                // a lowercase e: a ring 0.78 of the capitals' height in a thinner stroke (thick, it filled in to a disc), a bar
                // across its middle, open at the lower right
                float d = 0.78f * h, r = (d - 0.75f * t) / 2f, cx = x + d / 2f, cy = d / 2f;
                for (int k = 0; k < 16; k++)
                {
                    float a0 = k * Mathf.PI / 8f, a1 = (k + 1) * Mathf.PI / 8f;
                    if (a0 >= 1.62f * Mathf.PI && a1 <= 1.95f * Mathf.PI) continue;   // (the opening, lower right)
                    if (a1 > 1.62f * Mathf.PI && a0 < 1.62f * Mathf.PI) a1 = 1.62f * Mathf.PI;
                    if (a0 < 1.95f * Mathf.PI && a1 > 1.95f * Mathf.PI) a0 = 1.95f * Mathf.PI;
                    s.Add((cx + r * Mathf.Cos(a0), cy + r * Mathf.Sin(a0), cx + r * Mathf.Cos(a1), cy + r * Mathf.Sin(a1), 0.75f * t));
                }
                s.Add((x + hw, cy, x + d - hw, cy, 0.75f * t));
                return d;
            }
            case ' ': return 0.3f * h;
            default:
            {
                // other characters: a plain block (the lettering files use the logo's)
                float w = 0.55f * h;
                s.Add((x + w / 2f, 0f, x + w / 2f, h, t));
                return w;
            }
        }
    }

    /// <summary>A stroke from p0 to p1 (in a vertical plane facing <paramref name="face"/>), <paramref name="width"/> wide
    /// and <paramref name="depth"/> deep, its ends squared off a half width beyond the points (strokes overlap at joints).</summary>
    static void Stroke(MeshBuilder mb, Vector3 p0, Vector3 p1, float width, Vector3 face, float depth)
    {
        var d = p1 - p0; float len = d.magnitude; if (len < 1e-4f) return;
        d /= len;
        var side = Vector3.Cross(face, d).normalized * (width / 2f);
        var e0 = p0 - d * (width / 2f); var e1 = p1 + d * (width / 2f);
        var n = face * (depth / 2f);
        // the front (towards the reader) and the back ring, counter-clockwise seen from the front
        Vector3 f0 = e0 - side + n, f1 = e1 - side + n, f2 = e1 + side + n, f3 = e0 + side + n;
        Vector3 b0 = e0 - side - n, b1 = e1 - side - n, b2 = e1 + side - n, b3 = e0 + side - n;
        var uv = Vector2.zero;
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 e)
        {
            // (facing outwards whichever way the corners come: turned over if the normal points in)
            var mid = (a + b + c + e) / 4f; var centre = (e0 + e1) / 2f;
            var nrm = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(nrm, mid - centre) > 0f) mb.Quad(a, b, c, e, uv, uv, uv, uv);
            else mb.Quad(a, e, c, b, uv, uv, uv, uv);
        }
        Q(f0, f1, f2, f3); Q(b0, b3, b2, b1);
        Q(f0, b0, b1, f1); Q(f1, b1, b2, f2); Q(f2, b2, b3, f3); Q(f3, b3, b0, f0);
    }
}
