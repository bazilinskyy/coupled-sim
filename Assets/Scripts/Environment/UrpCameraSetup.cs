using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Turns on post-processing and anti-aliasing for every camera that renders to the screen or an
/// HMD (avatar cameras are spawned at runtime, so this is done when they first render).
/// Cameras that render to textures (mirrors, displays) are left as they are.
/// </summary>
public static class UrpCameraSetup
{
    static readonly HashSet<Camera> _done = new HashSet<Camera>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == null || _done.Contains(camera)) return;
        _done.Add(camera);
        if (camera.cameraType != CameraType.Game || camera.targetTexture != null) return;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        if (data.antialiasing == AntialiasingMode.None)
        {
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}
