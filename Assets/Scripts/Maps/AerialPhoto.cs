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
    public float HalfSize;
    /// <summary>The coarser photo of the surroundings ({map}.aerial-far.jpg, 2 × FarHalfSize metres), or null.</summary>
    public Texture2D FarTexture;
    public float FarHalfSize;
    public string Credit;

    /// <summary>The texture coordinate of a scene point (x east, z north).</summary>
    public Vector2 Uv(Vector3 p) => new Vector2((p.x + HalfSize) / (2f * HalfSize), (p.z + HalfSize) / (2f * HalfSize));

    public static string PathFor(string map) => Path.Combine(Application.streamingAssetsPath, "Maps", map + ".aerial.jpg");

    /// <summary>Null when the map has no photo or <c>-noAerial</c> is on the command line.</summary>
    public static AerialPhoto Load(string map)
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-noAerial") >= 0) return null;
        var jpg = PathFor(map);
        var json = Path.ChangeExtension(jpg, ".json");
        if (!File.Exists(jpg) || !File.Exists(json)) return null;
        var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(json));
        if (meta == null || meta.halfSize <= 0) return null;
        var tex = Image(jpg, map + " aerial photo");
        if (tex == null) return null;
        var far = Path.Combine(Path.GetDirectoryName(jpg), map + ".aerial-far.jpg");
        var farTex = meta.farHalfSize > meta.halfSize && File.Exists(far) ? Image(far, map + " surroundings photo") : null;
        return new AerialPhoto { Texture = tex, HalfSize = meta.halfSize, Credit = meta.credit, FarTexture = farTex, FarHalfSize = meta.farHalfSize };
    }

    static Texture2D Image(string path, string name)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = name };
        if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 16;                 // seen at grazing angles from the street
        tex.Compress(true);                  // about 11 MB of video memory for 4096² instead of 64 MB
        tex.Apply(true, true);
        return tex;
    }
}
