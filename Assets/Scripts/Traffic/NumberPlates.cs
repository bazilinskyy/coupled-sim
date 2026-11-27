using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Country number plates on the map's cars, front and rear: on the Dutch maps (Eindhoven, Delft) the yellow Dutch plate
/// (black characters, sidecodes 7-9 such as "12-ABC-3" and "AB-123-C", the blue EU band with "NL" at its left), 520 x 110
/// mm, as coupled-carla's twins have them; the car models came with California-style white plates or none.
/// Where a plate goes is found once per model from its own meshes: rays from behind (and from in front) at 0.3-1.0 m up
/// find the flattest upright stretch, a plate wide and high, across the middle of the body, nearest to 0.55 m (rear) or
/// 0.45 m (front) up; the plate lies 8 mm proud of it. A model with no such stretch gets none there.
/// </summary>
public static class NumberPlates
{
    /// <summary>The plate's country for a map's country, or null where there are no plates for it.</summary>
    public static string For(string country) => country == "Netherlands" ? "NL" : null;

    const float PlateW = 0.52f, PlateH = 0.11f;
    static readonly Dictionary<string, (Pose? rear, Pose? front)> _spots = new Dictionary<string, (Pose?, Pose?)>();
    static Material _plateMat;
    static Mesh _quad;

    /// <summary>Puts this country's plates on a car (model: its prefab's name, the key of the found spots; heading: the
    /// car's upright rotation, its forward along z, as a parked car's model may be turned upright in its prefab).</summary>
    public static void Fit(GameObject car, string model, string country, System.Random rng, Quaternion heading)
    {
        if (car == null || country != "NL") return;
        var frame = Matrix4x4.TRS(car.transform.position, heading, Vector3.one);
        if (!_spots.TryGetValue(model, out var spots))
        {
            _spots[model] = spots = (Find(car, frame, true, 0.03f) ?? Find(car, frame, true, 0.08f), Find(car, frame, false, 0.03f) ?? Find(car, frame, false, 0.08f));
            Debug.Log($"NumberPlates: {model}: rear {(spots.rear.HasValue ? $"{spots.rear.Value.position.y:F2} m up" : "none")}, front {(spots.front.HasValue ? $"{spots.front.Value.position.y:F2} m up" : "none")}");
        }
        string text = Registration(rng);
        if (spots.rear.HasValue) Add(car, frame, spots.rear.Value, text, "Number plate rear");
        if (spots.front.HasValue) Add(car, frame, spots.front.Value, text, "Number plate front");
    }

    /// <summary>A Dutch registration: sidecode 7 (99-XXX-9), 8 (9-XXX-99), 9 (XX-999-X).</summary>
    static string Registration(System.Random rng)
    {
        const string letters = "BDFGHJKLNPRSTVXZ";   // (no vowels on Dutch plates, nor C, M, Q, W, Y)
        char L() => letters[rng.Next(letters.Length)];
        char D() => (char)('0' + rng.Next(10));
        switch (rng.Next(3))
        {
            case 0: return $"{D()}{D()}-{L()}{L()}{L()}-{D()}";
            case 1: return $"{D()}-{L()}{L()}{L()}-{D()}{D()}";
            default: return $"{L()}{L()}-{D()}{D()}{D()}-{L()}";
        }
    }

    /// <summary>The flattest upright stretch a plate wide at the car's back (or front), in the car's own space.</summary>
    /// <remarks>(flat: within <paramref name="flat"/> m over the plate; a curved nose or tail, flat within 8 cm, gets its
    /// plate on the stretch's most proud point, standing off the body at its edges as on a bracket: a sports car's rounded
    /// nose, flat nowhere within 3 cm, had no front plate)</remarks>
    static Pose? Find(GameObject car, Matrix4x4 frame, bool rear, float flat)
    {
        var toFrame = frame.inverse;
        var tris = new List<Vector3>();
        var b = new Bounds(); bool any = false;
        foreach (var mf in car.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh; var r = mf.GetComponent<Renderer>();
            if (mesh == null || r == null || !r.enabled || !mesh.isReadable) continue;   // (only readable models: in play mode, the editor too refuses the others' vertices, with an error for each)
            var v = mesh.vertices; var t = mesh.triangles;
            var m = toFrame * mf.transform.localToWorldMatrix;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                var a = m.MultiplyPoint3x4(v[t[i]]); var c = m.MultiplyPoint3x4(v[t[i + 1]]); var d = m.MultiplyPoint3x4(v[t[i + 2]]);
                tris.Add(a); tris.Add(c); tris.Add(d);
                if (!any) { b = new Bounds(a, Vector3.zero); any = true; }
                b.Encapsulate(a); b.Encapsulate(c); b.Encapsulate(d);
            }
        }
        if (!any || b.size.z < 2f) return null;
        // (only the triangles within a metre of this end)
        var near = new List<Vector3>();
        for (int i = 0; i < tris.Count; i += 3)
        {
            float z = rear ? Mathf.Min(tris[i].z, Mathf.Min(tris[i + 1].z, tris[i + 2].z)) : Mathf.Max(tris[i].z, Mathf.Max(tris[i + 1].z, tris[i + 2].z));
            if (rear ? z < b.min.z + 1f : z > b.max.z - 1f) { near.Add(tris[i]); near.Add(tris[i + 1]); near.Add(tris[i + 2]); }
        }
        tris = near;
        float sign = rear ? 1f : -1f;   // (rays towards +z from behind, towards -z from in front)
        float startZ = rear ? b.min.z - 1f : b.max.z + 1f;
        float Hit(float x, float y)
        {
            var o = new Vector3(x, y, startZ); var dir = new Vector3(0f, 0f, sign);
            float best = float.MaxValue;
            for (int i = 0; i < tris.Count; i += 3)
                if (RayTri(o, dir, tris[i], tris[i + 1], tris[i + 2], out var dist) && dist < best) best = dist;
            return best;
        }
        float bestScore = float.MaxValue; Pose? found = null;
        float want = b.min.y + (rear ? 0.55f : 0.45f);
        float cx = b.center.x;
        for (float y = b.min.y + 0.3f; y <= Mathf.Min(b.min.y + 1.0f, b.max.y - 0.2f); y += 0.04f)
        {
            float m0 = Hit(cx, y);
            if (m0 > 3f) continue;
            float spread = 0f, nearest = m0;
            foreach (var (dx, dy) in new[] { (-PlateW / 2f, 0f), (PlateW / 2f, 0f), (0f, PlateH / 2f), (0f, -PlateH / 2f), (-PlateW / 2f, PlateH / 2f), (PlateW / 2f, -PlateH / 2f) })
            {
                float h = Hit(cx + dx, y + dy);
                if (h > 3f) { spread = float.MaxValue; break; }
                spread = Mathf.Max(spread, Mathf.Abs(h - m0));
                nearest = Mathf.Min(nearest, h);
            }
            if (spread > flat) continue;
            // (not set back deep in the body: the plate's place is on its back, within 0.35 m of its rearmost point)
            if (m0 - 1f > 0.35f) continue;
            float score = Mathf.Abs(y - want) + spread * 4f;
            if (score < bestScore)
            {
                bestScore = score;
                var at = new Vector3(cx, y, startZ + sign * (nearest - 0.008f));
                found = new Pose(at, Quaternion.LookRotation(new Vector3(0f, 0f, sign), Vector3.up));
            }
        }
        return found;
    }

    static bool RayTri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0f;
        var e1 = b - a; var e2 = c - a; var p = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-9f) return false;
        float inv = 1f / det; var s = o - a;
        float u = Vector3.Dot(s, p) * inv; if (u < 0f || u > 1f) return false;
        var q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(d, q) * inv; if (v < 0f || u + v > 1f) return false;
        t = Vector3.Dot(e2, q) * inv;
        return t > 0f;
    }

    static void Add(GameObject car, Matrix4x4 frame, Pose spot, string text, string name)
    {
        if (_quad == null)
        {
            _quad = new Mesh { name = "Number plate" };
            _quad.vertices = new[] { new Vector3(-PlateW / 2f, -PlateH / 2f, 0f), new Vector3(PlateW / 2f, -PlateH / 2f, 0f), new Vector3(PlateW / 2f, PlateH / 2f, 0f), new Vector3(-PlateW / 2f, PlateH / 2f, 0f) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateNormals(); _quad.RecalculateBounds();
        }
        var go = new GameObject(name);
        go.transform.SetParent(car.transform, false);
        // (the spot looks into the car, along the rays that found it; the quad faces -z of its own frame: out of the car,
        // its text read the right way round by someone looking at it)
        go.transform.SetPositionAndRotation(frame.MultiplyPoint3x4(spot.position), frame.rotation * spot.rotation);
        go.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, car.transform.lossyScale.x), 1f / Mathf.Max(1e-4f, car.transform.lossyScale.y), 1f / Mathf.Max(1e-4f, car.transform.lossyScale.z));
        go.AddComponent<MeshFilter>().sharedMesh = _quad;
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (_plateMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _plateMat = new Material(shader) { name = "Number plate NL" };
        }
        var mat = new Material(_plateMat);
        var tex = Draw(text);
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
        mr.sharedMaterial = mat;
    }

    // a 5 x 7 pixel font for the plate's characters (rows top to bottom, 5 bits each)
    static readonly Dictionary<char, int[]> Glyphs = new Dictionary<char, int[]>
    {
        ['0'] = new[] { 14, 17, 19, 21, 25, 17, 14 }, ['1'] = new[] { 4, 12, 4, 4, 4, 4, 14 }, ['2'] = new[] { 14, 17, 1, 2, 4, 8, 31 },
        ['3'] = new[] { 31, 2, 4, 2, 1, 17, 14 }, ['4'] = new[] { 2, 6, 10, 18, 31, 2, 2 }, ['5'] = new[] { 31, 16, 30, 1, 1, 17, 14 },
        ['6'] = new[] { 6, 8, 16, 30, 17, 17, 14 }, ['7'] = new[] { 31, 1, 2, 4, 8, 8, 8 }, ['8'] = new[] { 14, 17, 17, 14, 17, 17, 14 },
        ['9'] = new[] { 14, 17, 17, 15, 1, 2, 12 }, ['B'] = new[] { 30, 17, 17, 30, 17, 17, 30 }, ['D'] = new[] { 28, 18, 17, 17, 17, 18, 28 },
        ['F'] = new[] { 31, 16, 16, 30, 16, 16, 16 }, ['G'] = new[] { 14, 17, 16, 23, 17, 17, 15 }, ['H'] = new[] { 17, 17, 17, 31, 17, 17, 17 },
        ['J'] = new[] { 7, 2, 2, 2, 2, 18, 12 }, ['K'] = new[] { 17, 18, 20, 24, 20, 18, 17 }, ['L'] = new[] { 16, 16, 16, 16, 16, 16, 31 },
        ['N'] = new[] { 17, 17, 25, 21, 19, 17, 17 }, ['P'] = new[] { 30, 17, 17, 30, 16, 16, 16 }, ['R'] = new[] { 30, 17, 17, 30, 20, 18, 17 },
        ['S'] = new[] { 15, 16, 16, 14, 1, 1, 30 }, ['T'] = new[] { 31, 4, 4, 4, 4, 4, 4 }, ['V'] = new[] { 17, 17, 17, 17, 17, 10, 4 },
        ['X'] = new[] { 17, 17, 10, 4, 10, 17, 17 }, ['Z'] = new[] { 31, 1, 2, 4, 8, 16, 31 }, ['-'] = new[] { 0, 0, 0, 14, 0, 0, 0 },
    };

    /// <summary>The plate's face: yellow, a black rim, the blue EU band with "NL", the registration in black.</summary>
    static Texture2D Draw(string text)
    {
        const int W = 208, H = 44, S = 4;   // (4 px a font pixel: 7 rows are 28 px of 44)
        var px = new Color32[W * H];
        var yellow = new Color32(247, 196, 0, 255); var black = new Color32(20, 20, 20, 255);
        var blue = new Color32(0, 51, 153, 255); var white = new Color32(240, 240, 240, 255);
        for (int i = 0; i < px.Length; i++) px[i] = yellow;
        void Set(int x, int y, Color32 c) { if (x >= 0 && x < W && y >= 0 && y < H) px[(H - 1 - y) * W + x] = c; }
        for (int x = 0; x < W; x++) { Set(x, 0, black); Set(x, 1, black); Set(x, H - 1, black); Set(x, H - 2, black); }
        for (int y = 0; y < H; y++) { Set(0, y, black); Set(1, y, black); Set(W - 1, y, black); Set(W - 2, y, black); }
        for (int x = 2; x < 22; x++) for (int y = 2; y < H - 2; y++) Set(x, y, blue);
        void Glyph(char ch, int x0, int y0, int s, Color32 c)
        {
            if (!Glyphs.TryGetValue(ch, out var rows)) return;
            for (int r = 0; r < 7; r++) for (int col = 0; col < 5; col++)
                if ((rows[r] >> (4 - col) & 1) != 0) for (int dx = 0; dx < s; dx++) for (int dy = 0; dy < s; dy++) Set(x0 + col * s + dx, y0 + r * s + dy, c);
        }
        // "NL" small in the band, the registration across the rest
        Glyph('N', 6, 26, 1, white); Glyph('L', 12, 26, 1, white);
        int cw = 5 * S + 3, total = text.Length * cw;
        int x1 = 22 + (W - 22 - total) / 2;
        for (int i = 0; i < text.Length; i++) Glyph(text[i], x1 + i * cw, (H - 7 * S) / 2, S, black);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Number plate " + text, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        tex.SetPixels32(px); tex.Apply(true, true);
        return tex;
    }
}
