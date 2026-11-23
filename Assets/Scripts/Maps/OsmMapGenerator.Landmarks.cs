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
/// facade &lt;OSM way id&gt; curtainwall &lt;panel width m&gt; &lt;floor height m&gt; &lt;glass rrggbb&gt; &lt;frame rrggbb&gt; &lt;blinds 0-1&gt;
/// roofletters &lt;OSM way id&gt; &lt;text&gt; &lt;letter height m&gt; &lt;colour rrggbb&gt; [city|bearing in degrees]
/// groundletters &lt;lat&gt; &lt;lon&gt; &lt;text&gt; &lt;letter height m&gt; &lt;colour rrggbb&gt; &lt;bearing the letters face, degrees&gt;
/// </code>
/// Lettering is built from strokes for T, U, /, e and a few more (capitals and the logo's lowercase e).
/// </summary>
public partial class OsmMapGenerator
{
    sealed class FacadeStyle { public float Panel, Floor, Blinds; public Color Glass, Frame; public Material Mat; public MeshBuilder Mb; }
    readonly Dictionary<long, FacadeStyle> _facadeStyles = new Dictionary<long, FacadeStyle>();
    readonly List<string[]> _letterLines = new List<string[]>();
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
                    _facadeStyles[long.Parse(f[1], ci)] = new FacadeStyle { Panel = float.Parse(f[3], ci), Floor = float.Parse(f[4], ci), Glass = Hex(f[5]), Frame = Hex(f[6]), Blinds = float.Parse(f[7], ci) };
                else if ((f[0] == "roofletters" && f.Length >= 5) || (f[0] == "groundletters" && f.Length >= 7)) _letterLines.Add(f);
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
        int step = Math.Max(1, v.Length / 40);
        float mx = 0, mz = 0; int n = 0;
        for (int i = 0; i < v.Length; i += step)
        {
            if (v[i].y > 0.5f) continue;
            mx += v[i].x; mz += v[i].z; n++;
            var w = OsmBuildingAt(new V2(v[i].x, v[i].z));
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
    /// A glass curtain wall: floor-high panes in thin aluminium frames, a band at each floor's slab, and blinds behind the
    /// glass let down to different heights (one texture of 8 panels by 4 floors, the façade's UVs in 3 m units scaled to
    /// it). Smooth and a little metallic, so it mirrors the sky and the street.
    /// </summary>
    Material CurtainWallMaterial(string name, FacadeStyle st)
    {
        const int PX = 8, FL = 4, W = 512, H = 512;
        var rng = new System.Random(name.GetHashCode());
        var blind = new float[PX * FL];
        // (blinds down to a few common heights, as people leave them: a third, two thirds, all the way)
        for (int i = 0; i < blind.Length; i++) blind[i] = rng.NextDouble() < st.Blinds ? new[] { 0.33f, 0.66f, 1f }[rng.Next(3)] : 0f;
        var tex = Tex(name, W, (x, y, r) =>
        {
            float u = x / (float)W * PX, v = y / (float)H * FL;
            int pi = Mathf.Min(PX - 1, (int)u), fi = Mathf.Min(FL - 1, (int)v);
            float fu = u - pi, fv = v - fi;   // (fv 0 at the floor, 1 at the next)
            float slab = 0.38f / st.Floor, mullion = 0.05f / st.Panel;
            if (fv < slab) return Noise(st.Frame * 0.82f, r, 0.04f);                       // the slab's edge, a spandrel band
            if (fu < mullion || fu > 1f - mullion) return Noise(st.Frame, r, 0.03f);       // vertical frames
            if (Mathf.Abs(fv - (slab + 0.85f / st.Floor)) < 0.02f) return Noise(st.Frame, r, 0.03f);   // the opening window's frame
            // glass: darker below, catching the sky towards the top; a blind behind it from the top down
            float gl = Mathf.Lerp(0.85f, 1.15f, (fv - slab) / (1f - slab));
            float b = blind[fi * PX + pi];
            if (b > 0f && fv > 1f - b * (1f - slab)) return Noise(Color.Lerp(st.Glass, new Color(0.78f, 0.79f, 0.8f), 0.35f), r, 0.03f);
            return Noise(st.Glass * gl, r, 0.02f);
        });
        var m = Mat(name, Color.white, 0.88f, tex);
        m.SetFloat("_Metallic", 0.35f);
        m.SetTextureScale("_BaseMap", new Vector2(3f / (st.Panel * PX), 3f / (st.Floor * FL)));
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
                // the top of the real building (or the mapped height)
                var c = poly.Aggregate(V2.Zero, (s, q) => s + q) / poly.Count;
                float top = 0f;
                if (bag != null)
                    foreach (var b in bag)
                    {
                        float mx = 0, mz = 0; int n = 0; foreach (var v in b.V) if (v.y < 0.5f) { mx += v.x; mz += v.z; n++; }
                        if (n > 0 && Geometry2D.PointInPolygon(new V2(mx / n, mz / n), poly)) foreach (var v in b.V) top = Math.Max(top, v.y);
                    }
                if (top <= 0f) top = BuildingHeight(way);
                float hgt = float.Parse(f[3], ci);
                // the edge to stand on: the longest whose outside faces the map's centre (or the bearing given)
                V2 want = f.Length > 5 && f[5] != "city" && float.TryParse(f[5], NumberStyles.Float, ci, out var brg)
                    ? new V2(Mathf.Sin(brg * Mathf.Deg2Rad), Mathf.Cos(brg * Mathf.Deg2Rad)) : V2.Normalize(-c);
                int best = 0; float bestScore = float.MinValue;
                for (int i = 0; i < poly.Count; i++)
                {
                    var a = poly[i]; var b2 = poly[(i + 1) % poly.Count]; var e = b2 - a; float len = e.Length(); if (len < 1f) continue;
                    var outward = new V2(e.Y, -e.X) / len;   // (counter-clockwise ring: the outside is to the right)
                    float score = V2.Dot(outward, want) * Mathf.Min(len, 4f * hgt);
                    if (score > bestScore) { bestScore = score; best = i; }
                }
                var ea = poly[best]; var eb = poly[(best + 1) % poly.Count]; var dir = V2.Normalize(eb - ea); var outN = new V2(dir.Y, -dir.X);
                var mid = (ea + eb) / 2f - outN * 0.8f;   // (a little in from the edge)
                Lettering(mb, f[2], new Vector3(mid.X, top, mid.Y), new Vector3(outN.X, 0f, outN.Y), hgt, 0.35f);
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
    /// Text standing upright, centred at <paramref name="centre"/> (its base), facing <paramref name="face"/> (towards its
    /// reader, for whom it reads from left to right), letters <paramref name="height"/> high and <paramref name="depth"/>
    /// deep, from strokes (a bold sans: the TU/e logo's capitals, slash and lowercase e).
    /// </summary>
    static void Lettering(MeshBuilder mb, string text, Vector3 centre, Vector3 face, float height, float depth)
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
