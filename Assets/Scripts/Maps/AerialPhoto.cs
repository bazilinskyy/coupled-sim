using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The open aerial photo of a map (StreamingAssets/Maps/{map}.aerial.jpg and .json, made by
/// Tools/aerial_photo.py), cut to a square of 2 × HalfSize metres around the map centre in the map's
/// projection (north up). OsmMapGenerator lays it on the ground and on the roofs.
/// </summary>
public sealed class AerialPhoto
{
    [Serializable] sealed class Meta { public float halfSize; public float metresPerPixel; public float farHalfSize; public string credit; }

    public Texture2D Texture;
    /// <summary>The photo for the ground at eye level: shadows softer and less blue, colours calmer (a photo's shadows
    /// and parked cars, seen from the street, read as smears).</summary>
    public Texture2D GroundTexture;
    public float HalfSize;
    /// <summary>The coarser photo of the surroundings ({map}.aerial-far.jpg, 2 × FarHalfSize metres), or null.</summary>
    public Texture2D FarTexture;
    public float FarHalfSize;
    public string Credit;
    /// <summary>What the ground is, read from the photo's colours on a grid of about half a metre (<see cref="ClassN"/>
    /// cells a side over the photo's square, row 0 south): <see cref="Paved"/>, <see cref="Green"/> (lawn, verges, tree
    /// crowns: the generator takes out the crowns over mapped trees) or <see cref="Soil"/> (bare earth, sand, a tree pit).</summary>
    public byte[] Classes; public int ClassN;
    public const byte Paved = 0, Green = 1, Soil = 2;
    /// <summary>The ground class at a scene point (Paved beyond the photo).</summary>
    public byte ClassAt(float x, float z)
    {
        if (Classes == null) return Paved;
        int i = (int)((x + HalfSize) / (2f * HalfSize) * ClassN), j = (int)((z + HalfSize) / (2f * HalfSize) * ClassN);
        return i < 0 || j < 0 || i >= ClassN || j >= ClassN ? Paved : Classes[j * ClassN + i];
    }

    /// <summary>A small readable copy of the photo (the texture itself lives only on the graphics card).</summary>
    Color32[] _small; int _smallWidth, _smallHeight;

    /// <summary>The photo's colour at a scene point (averaged over about 2 m).</summary>
    public Color Sample(Vector3 p)
    {
        if (_small == null || _smallWidth <= 0 || _smallHeight <= 0) return Color.gray;
        var uv = Uv(p);
        int x = Mathf.Clamp((int)(uv.x * _smallWidth), 0, _smallWidth - 1), y = Mathf.Clamp((int)(uv.y * _smallHeight), 0, _smallHeight - 1);
        return _small[y * _smallWidth + x];
    }

    /// <summary>The texture coordinate of a scene point (x east, z north).</summary>
    /// <summary>Whether the photo covers a scene point (a map with corridors reaches beyond it).</summary>
    public bool Covers(Vector3 p) => Mathf.Abs(p.x) < HalfSize && Mathf.Abs(p.z) < HalfSize;

    public Vector2 Uv(Vector3 p) => new Vector2((p.x + HalfSize) / (2f * HalfSize), (p.z + HalfSize) / (2f * HalfSize));

    public static string PathFor(string map) => Path.Combine(Application.streamingAssetsPath, "Maps", map + ".aerial.jpg");

    /// <summary>Null when the map has no photo or <c>-noAerial</c> is on the command line.</summary>
    public static AerialPhoto Load(string map)
    {
        if (Array.IndexOf(CommandLine.Args, "-noAerial") >= 0) return null;
        var jpg = PathFor(map);
        var json = Path.ChangeExtension(jpg, ".json");
        if (!File.Exists(jpg) || !File.Exists(json)) return null;
        var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(json));
        if (meta == null || meta.halfSize <= 0) return null;
        var tex = Image(jpg, map + " aerial photo", out var small, out int smallWidth, out int smallHeight, out var ground, true, meta.halfSize, out var classes, out int classN);
        if (tex == null) return null;
        var far = Path.Combine(Path.GetDirectoryName(jpg), map + ".aerial-far.jpg");
        var farTex = meta.farHalfSize > meta.halfSize && File.Exists(far) ? Image(far, map + " surroundings photo", out _, out _, out _, out _, false, 0f, out _, out _) : null;
        return new AerialPhoto { GroundTexture = ground, _small = small, _smallWidth = smallWidth, _smallHeight = smallHeight, Texture = tex, HalfSize = meta.halfSize, Credit = meta.credit, FarTexture = farTex, FarHalfSize = meta.farHalfSize, Classes = classes, ClassN = classN };
    }

    static Texture2D Image(string path, string name, out Color32[] small, out int smallWidth, out int smallHeight, out Texture2D ground, bool withGround, float halfSize,
        out byte[] classes, out int classN)
    {
        small = null; smallWidth = 0; smallHeight = 0; ground = null; classes = null; classN = 0;
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = name };
        if (!tex.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(tex); return null; }
        // a mip level of about 1024 px (2-4 m a pixel) kept for colour samples
        int level = 0;
        while (level < tex.mipmapCount - 1 && (tex.width >> level) > 1024) level++;
        small = tex.GetPixels32(level);
        smallWidth = Math.Max(1, tex.width >> level);
        smallHeight = Math.Max(1, tex.height >> level);
        if (withGround && halfSize > 0f)
        {
            // the ground classes, from the mip level nearest half a metre a pixel
            int cl = 0;
            while (cl < tex.mipmapCount - 1 && 2f * halfSize / Math.Max(1, tex.width >> (cl + 1)) <= 0.7f) cl++;
            var cp = tex.GetPixels32(cl);
            classN = Math.Max(1, tex.width >> cl);
            classes = Classify(cp, classN, Math.Max(1, tex.height >> cl));
        }
        if (withGround)
        {
            var px = tex.GetPixels32(0);
            int w = tex.width, h = tex.height;
            // the photo's own light taken out: each pixel divided by the mean brightness around it (about 5 m), so
            // photographed shadows and sunlit patches (other directions than the scene's sun) do not show as smears at
            // eye level; the colours stay, a little calmer, and the scene lights the ground itself
            var lum = new float[px.Length];
            double total = 0;
            for (int i = 0; i < px.Length; i++) { lum[i] = (0.3f * px[i].r + 0.59f * px[i].g + 0.11f * px[i].b) / 255f; total += lum[i]; }
            float mean = (float)(total / px.Length);
            int radius = Mathf.Max(4, Mathf.RoundToInt(w / 160f));   // (4096 px over about 700 m: 26 px, 4.5 m)
            var blur = BoxBlur(BoxBlur(lum, w, h, radius), w, h, radius);
            for (int i = 0; i < px.Length; i++)
            {
                float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f, l = Mathf.Max(lum[i], 0.01f);
                float k = Mathf.Clamp(mean / Mathf.Max(blur[i], 0.02f), 0.6f, 2.2f);
                // calmer colours, and the blue of shade taken out
                r = (l + (r - l) * 0.5f) * k; g = (l + (g - l) * 0.5f) * k; b = (l + (b - l) * 0.5f) * k;
                float rg = (r + g) * 0.5f; if (b > rg) b = Mathf.Lerp(b, rg, 0.7f);
                // and the brightness held near the mean (what is left of a sharp shadow edge)
                float l2 = 0.3f * r + 0.59f * g + 0.11f * b, t = Mathf.Lerp(l2, mean, 0.35f) / Mathf.Max(l2, 0.01f);
                px[i] = new Color32((byte)(Mathf.Clamp01(r * t) * 255f), (byte)(Mathf.Clamp01(g * t) * 255f), (byte)(Mathf.Clamp01(b * t) * 255f), 255);
            }
            ground = new Texture2D(w, h, TextureFormat.RGB24, true) { name = name + " (ground)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 16 };
            ground.SetPixels32(px);
            ground.Apply(true);
            ground.Compress(true);
            ground.Apply(true, true);
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 16;                 // seen at grazing angles from the street
        tex.Compress(true);                  // about 11 MB of video memory for 4096² instead of 64 MB
        tex.Apply(true, true);
        return tex;
    }

    /// <summary>
    /// The ground class of each pixel: green where green stands out (excess green over red and blue, in light and in
    /// shade), soil where a clear, darker brown does (earth, a dry verge, a tree pit), else paved (asphalt, concrete, paving,
    /// roofs and cars, which lie under the drawn roads and buildings). Then two passes of a 3 x 3 majority, so single
    /// pixels (a car's green roof, a bench) do not make specks.
    /// </summary>
    static byte[] Classify(Color32[] px, int w, int h)
    {
        var c = new byte[w * h];
        for (int i = 0; i < px.Length && i < c.Length; i++)
        {
            float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f, sum = r + g + b + 1e-3f;
            float exg = (2f * g - r - b) / sum;
            float warm = (r - b) / sum;
            float v = Mathf.Max(r, Mathf.Max(g, b));
            if (exg > 0.07f && v > 0.08f) c[i] = Green;
            // (a clear brown only, darker than paving: Barcelona's beige paving was taken for earth)
            else if (warm > 0.25f && r > g && g > b && g < 0.85f * r && v > 0.2f && v < 0.62f && exg > -0.08f) c[i] = Soil;
        }
        for (int pass = 0; pass < 2; pass++)
        {
            var o = new byte[c.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int n0 = 0, n1 = 0, n2 = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = Mathf.Clamp(x + dx, 0, w - 1), yy = Mathf.Clamp(y + dy, 0, h - 1);
                            byte k = c[yy * w + xx]; if (k == Green) n1++; else if (k == Soil) n2++; else n0++;
                        }
                    o[y * w + x] = n1 >= 5 ? Green : n2 >= 5 ? Soil : n0 >= 5 ? Paved : c[y * w + x];
                }
            c = o;
        }
        return c;
    }

    /// <summary>A box blur of a w x h field (rows, then columns; a running sum), radius in pixels.</summary>
    static float[] BoxBlur(float[] src, int w, int h, int radius)
    {
        var tmp = new float[src.Length]; var dst = new float[src.Length];
        for (int y = 0; y < h; y++)
        {
            int row = y * w; float sum = 0f; int n = 0;
            for (int x = -radius; x <= radius; x++) if (x >= 0 && x < w) { sum += src[row + x]; n++; }
            for (int x = 0; x < w; x++)
            {
                tmp[row + x] = sum / n;
                int add = x + radius + 1, rem = x - radius;
                if (add < w) { sum += src[row + add]; n++; }
                if (rem >= 0) { sum -= src[row + rem]; n--; }
            }
        }
        for (int x = 0; x < w; x++)
        {
            float sum = 0f; int n = 0;
            for (int y = -radius; y <= radius; y++) if (y >= 0 && y < h) { sum += tmp[y * w + x]; n++; }
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = sum / n;
                int add = y + radius + 1, rem = y - radius;
                if (add < h) { sum += tmp[add * w + x]; n++; }
                if (rem >= 0) { sum -= tmp[rem * w + x]; n--; }
            }
        }
        return dst;
    }
}
