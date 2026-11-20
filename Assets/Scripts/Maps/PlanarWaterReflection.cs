using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The mirror image in the map's water (canals, rivers, harbours): before a view renders, a second camera mirrored in the
/// water's surface renders the scene above it (at half resolution, without shadows or post-processing) into a texture
/// the water shader (CoupledSim/Water) samples where it shows on the screen. The surface's height is taken from the water
/// near the view. <c>-noWaterReflections</c>: the sky's reflection only.
/// </summary>
public class PlanarWaterReflection : MonoBehaviour
{
    public static PlanarWaterReflection Instance { get; private set; }
    public static readonly bool Disabled = System.Array.IndexOf(CommandLine.Args, "-noWaterReflections") >= 0;

    static readonly int TexId = Shader.PropertyToID("_WaterReflectionTex"), OnId = Shader.PropertyToID("_WaterReflectionOn");
    Camera _mirror;
    RenderTexture _rt;
    Renderer _water;
    // the water's surface heights by 25 m cell (its mesh's vertices), for the plane near the view
    readonly Dictionary<long, float> _heights = new Dictionary<long, float>();
    const float Cell = 25f;

    void Awake()
    {
        Instance = this;
        _water = GetComponent<Renderer>();
        Shader.SetGlobalFloat(OnId, 0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Shader.SetGlobalFloat(OnId, 0f);
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
        if (_mirror != null) Destroy(_mirror.gameObject);
    }

    bool _measured;
    // (measured when first needed: the terrain lifts the map's meshes after they are made)
    void Measure()
    {
        _measured = true;
        _heights.Clear();
        // (the collider's mesh: static batching may have combined the renderer's into one that cannot be read)
        var mc = GetComponent<MeshCollider>();
        var mf = GetComponent<MeshFilter>();
        var mesh = mc != null && mc.sharedMesh != null && mc.sharedMesh.isReadable ? mc.sharedMesh : mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable ? mf.sharedMesh : null;
        if (mesh != null)
        {
            var sums = new Dictionary<long, (float sum, int n)>();
            var l2w = transform.localToWorldMatrix;
            foreach (var v0 in mesh.vertices)
            {
                var v = l2w.MultiplyPoint3x4(v0);
                long k = Key(v.x, v.z);
                sums[k] = sums.TryGetValue(k, out var e) ? (e.sum + v.y, e.n + 1) : (v.y, 1);
            }
            foreach (var kv in sums) _heights[kv.Key] = kv.Value.sum / kv.Value.n;
        }
    }

    static long Key(float x, float z) => ((long)Mathf.FloorToInt(x / Cell) << 32) ^ (uint)Mathf.FloorToInt(z / Cell);

    /// <summary>The water's surface height near a point (the nearest cell with water within 300 m), or null.</summary>
    float? HeightNear(Vector3 p)
    {
        int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.z / Cell);
        for (int r = 0; r <= 12; r++)
        {
            float best = float.MaxValue; float? h = null;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    if (!_heights.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out var y)) continue;
                    float d = dx * dx + dz * dz;
                    if (d < best) { best = d; h = y; }
                }
            if (h.HasValue) return h;
        }
        return null;
    }

    void LateUpdate()
    {
        // the view on the screen (the highest-depth camera drawing to the display)
        Camera view = null;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null && c.isActiveAndEnabled && c.cameraType == CameraType.Game && (view == null || c.depth > view.depth)) view = c;
        if (view != null) RenderFor(view);
    }

    /// <summary>Renders the mirror image for a view (VideoExport calls it before each of its cameras renders).</summary>
    public void RenderFor(Camera view)
    {
        if (Disabled || view == null || _water == null || !_water.enabled) { Shader.SetGlobalFloat(OnId, 0f); return; }
        if (!_measured) Measure();
        var h = HeightNear(view.transform.position);
        var planes = GeometryUtility.CalculateFrustumPlanes(view);
        if (!h.HasValue || view.transform.position.y < h.Value + 0.05f || !GeometryUtility.TestPlanesAABB(planes, _water.bounds)) { Shader.SetGlobalFloat(OnId, 0f); return; }
        int w = Mathf.Max(64, view.pixelWidth / 2), ht = Mathf.Max(64, view.pixelHeight / 2);
        if (_rt == null || _rt.width != w || _rt.height != ht)
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(w, ht, 24, RenderTextureFormat.DefaultHDR) { name = "Water reflection" };
        }
        if (_mirror == null)
        {
            var go = new GameObject("Water reflection camera") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            _mirror = go.AddComponent<Camera>();
            _mirror.enabled = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
        }
        // mirrored in the plane y = h: position and the view's axes (a rotation: the image comes out flipped left to
        // right, which the shader undoes)
        float y = h.Value;
        var t = view.transform;
        Vector3 M(Vector3 v) => new Vector3(v.x, -v.y, v.z);
        var pos = t.position; pos.y = 2f * y - pos.y;
        _mirror.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(M(t.forward), M(t.up)));
        _mirror.CopyFrom(view);
        _mirror.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(M(t.forward), M(t.up)));
        _mirror.enabled = false;
        _mirror.targetTexture = _rt;
        _mirror.farClipPlane = Mathf.Min(view.farClipPlane, 600f);
        _mirror.clearFlags = CameraClearFlags.Skybox;
        // only what is above the water: an oblique near plane in the water's surface
        _mirror.ResetProjectionMatrix();
        var w2c = _mirror.worldToCameraMatrix;
        var pn = w2c.MultiplyVector(Vector3.up).normalized;
        var pp = w2c.MultiplyPoint(new Vector3(pos.x, y + 0.02f, pos.z));
        _mirror.projectionMatrix = _mirror.CalculateObliqueMatrix(new Vector4(pn.x, pn.y, pn.z, -Vector3.Dot(pp, pn)));
        _water.enabled = false;   // (the water itself is not in its own mirror image)
        try { _mirror.Render(); } finally { _water.enabled = true; }
        Shader.SetGlobalTexture(TexId, _rt);
        Shader.SetGlobalFloat(OnId, 1f);
    }
}
