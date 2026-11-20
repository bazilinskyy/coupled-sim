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
        var tex = Image(jpg, map + " aerial photo", out var small, out int smallWidth, out int smallHeight, out var ground);
        if (tex == null) return null;
        var far = Path.Combine(Path.GetDirectoryName(jpg), map + ".aerial-far.jpg");
        var farTex = meta.farHalfSize > meta.halfSize && File.Exists(far) ? Image(far, map + " surroundings photo", out _, out _, out _, out _, false) : null;
        return new AerialPhoto { GroundTexture = ground, _small = small, _smallWidth = smallWidth, _smallHeight = smallHeight, Texture = tex, HalfSize = meta.halfSize, Credit = meta.credit, FarTexture = farTex, FarHalfSize = meta.farHalfSize };
    }

    static Texture2D Image(string path, string name, out Color32[] small, out int smallWidth, out int smallHeight, out Texture2D ground, bool withGround = true)
    {
        small = null; smallWidth = 0; smallHeight = 0; ground = null;
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = name };
        if (!tex.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(tex); return null; }
        // a mip level of about 1024 px (2-4 m a pixel) kept for colour samples
        int level = 0;
        while (level < tex.mipmapCount - 1 && (tex.width >> level) > 1024) level++;
        small = tex.GetPixels32(level);
        smallWidth = Math.Max(1, tex.width >> level);
        smallHeight = Math.Max(1, tex.height >> level);
        if (withGround)
        {
            var px = tex.GetPixels32(0);
            for (int i = 0; i < px.Length; i++)
            {
                float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f;
                float lum = 0.3f * r + 0.59f * g + 0.11f * b;
                // shadows lifted (the scene casts its own), colours calmer, the blue of shade in photos taken out
                float l2 = lum < 0.42f ? lum + (0.42f - lum) * 0.45f : lum;
                float k = l2 / Mathf.Max(lum, 0.02f);
                r = (lum + (r - lum) * 0.6f) * k; g = (lum + (g - lum) * 0.6f) * k; b = (lum + (b - lum) * 0.6f) * k;
                float rg = (r + g) * 0.5f; if (b > rg) b = Mathf.Lerp(b, rg, 0.7f);
                px[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), 255);
            }
            ground = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, true) { name = name + " (ground)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 16 };
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
}
