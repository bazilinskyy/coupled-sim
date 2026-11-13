using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// Frame rate of the hand-built town (#66): Coupled Sim ➡️ Graphics ➡️ Optimise scenes
/// - level-of-detail meshes (Unity's mesh LODs) for detailed meshes such as the trees, so far
///   objects are drawn with fewer triangles;
/// - flat surfaces (roads, pavements, markings) no longer cast shadows;
/// - occlusion culling baked, so buildings hide what is behind them from the renderer.
/// Batch mode: -executeMethod PerformanceSetup.OptimiseAll
/// </summary>
public static class PerformanceSetup
{
    static readonly string[] Scenes = { "Assets/_Scenes/MainScene.unity", "Assets/_Scenes/MainScene-night.unity" };
    const int LodMinTriangles = 3000;

    [MenuItem("Coupled Sim/Graphics/Optimise scenes (LODs, shadows, occlusion culling)")]
    public static void OptimiseAll()
    {
        foreach (var path in Scenes)
        {
            if (!System.IO.File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int lods = GenerateMeshLods();
            int shadows = FlatSurfacesWithoutShadows();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // occlusion culling: bake when the scene has no data yet (or with -rebakeOcclusion)
            var occlusion = System.IO.Path.Combine(System.IO.Path.ChangeExtension(path, null), "OcclusionCullingData.asset");
            string baked = "kept";
            if (!System.IO.File.Exists(occlusion) || System.Environment.GetCommandLineArgs().Contains("-rebakeOcclusion"))
            {
                var sw = Stopwatch.StartNew();
                BakeOcclusion();
                EditorSceneManager.SaveScene(scene);
                baked = $"baked in {sw.Elapsed.TotalSeconds:F0} s";
            }
            Debug.Log($"PerformanceSetup: {path}: mesh LODs for {lods} meshes, shadows off for {shadows} flat renderers, occlusion culling {baked}");
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>Mesh LODs for the detailed meshes of the open scene that are mesh assets (not read-only model imports).</summary>
    static int GenerateMeshLods()
    {
        int n = 0;
        var meshes = Object.FindObjectsByType<MeshFilter>().Select(f => f.sharedMesh).Where(m => m != null).Distinct().ToList();
        foreach (var mesh in meshes)
        {
            var path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset") || mesh.triangles.Length / 3 < LodMinTriangles) continue;
            if (mesh.lodCount > 1) continue;
            MeshLodUtility.GenerateMeshLods(mesh, 6);
            EditorUtility.SetDirty(mesh);
            Debug.Log($"PerformanceSetup: {mesh.name}: {mesh.triangles.Length / 3} triangles, {mesh.lodCount} LODs");
            n++;
        }
        // model imports: let the importer make the LODs
        foreach (var path in meshes.Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrEmpty(p) && !p.EndsWith(".asset")).Distinct())
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi) || mi.generateMeshLods) continue;
            if (meshes.Where(m => AssetDatabase.GetAssetPath(m) == path).Sum(m => m.triangles.Length / 3) < LodMinTriangles) continue;
            mi.generateMeshLods = true;
            mi.SaveAndReimport();
            Debug.Log("PerformanceSetup: mesh LODs on import of " + path);
            n++;
        }
        return n;
    }

    /// <summary>Roads, pavements and markings are flat: their shadows cost a lot and show nothing.</summary>
    static int FlatSurfacesWithoutShadows()
    {
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>())
        {
            if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
            var b = r.bounds;
            // flat and on the ground (not awnings or roofs)
            if (b.size.y > 0.3f || b.max.y > 0.6f || Mathf.Max(b.size.x, b.size.z) < 1f) continue;
            r.shadowCastingMode = ShadowCastingMode.Off;
            n++;
        }
        return n;
    }

    // ---- quality levels

    const string UrpFolder = "Assets/URPDefaultResources";

    /// <summary>
    /// Quality levels besides Fantastic (the default): Balanced (2 shadow cascades to 100 m, 2048
    /// shadow map, MSAA 2x) and Performance (1 cascade to 60 m, 1024, no soft shadows, no MSAA, no
    /// ambient occlusion, render scale 0.8). Choose with -quality on the command line.
    /// </summary>
    [MenuItem("Coupled Sim/Graphics/Create quality levels (Balanced, Performance)")]
    public static void CreateQualityLevels()
    {
        var fantastic = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>($"{UrpFolder}/Fantastic.asset");
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>($"{UrpFolder}/Default_Forward_Renderer.asset");
        // a renderer without ambient occlusion for Performance
        var lightPath = $"{UrpFolder}/Performance_Forward_Renderer.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(lightPath) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(renderer), lightPath);
        var light = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(lightPath);
        foreach (var f in light.rendererFeatures) if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) f.SetActive(false);
        EditorUtility.SetDirty(light);

        var balanced = Level("Balanced", fantastic, 100f, 2, 2048, 2, 1f, true, null);
        var performance = Level("Performance", fantastic, 60f, 1, 1024, 1, 0.8f, false, light);

        var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = qs.FindProperty("m_QualitySettings");
        void Ensure(string name, Object pipeline, int aa, float lodBias)
        {
            int i = Enumerable.Range(0, levels.arraySize).FirstOrDefault(k => levels.GetArrayElementAtIndex(k).FindPropertyRelative("name").stringValue == name);
            if (levels.arraySize == 0 || levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue != name)
            {
                levels.InsertArrayElementAtIndex(levels.arraySize - 1);
                i = levels.arraySize - 1;
            }
            var l = levels.GetArrayElementAtIndex(i);
            l.FindPropertyRelative("name").stringValue = name;
            l.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            l.FindPropertyRelative("antiAliasing").intValue = aa;
            l.FindPropertyRelative("lodBias").floatValue = lodBias;
        }
        Ensure("Balanced", balanced, 2, 1.5f);
        Ensure("Performance", performance, 0, 1f);
        qs.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("PerformanceSetup: quality levels " + string.Join(", ", QualitySettings.names));
    }

    static Object Level(string name, Object source, float shadowDistance, int cascades, int shadowRes, int msaa, float renderScale, bool softShadows, Object rendererData)
    {
        var path = $"{UrpFolder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
        var a = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(path);
        a.shadowDistance = shadowDistance;
        a.shadowCascadeCount = cascades;
        a.msaaSampleCount = msaa;
        a.renderScale = renderScale;
        var so = new SerializedObject(a);
        so.FindProperty("m_MainLightShadowmapResolution").intValue = shadowRes;
        so.FindProperty("m_SoftShadowsSupported").boolValue = softShadows;
        if (rendererData != null) so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(a);
        return a;
    }

    static void BakeOcclusion()
    {
        StaticOcclusionCulling.smallestOccluder = 4f;
        StaticOcclusionCulling.smallestHole = 0.25f;
        StaticOcclusionCulling.backfaceThreshold = 100f;
        StaticOcclusionCulling.Compute();
    }
}
