using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Graphics set-up steps for the URP version of the simulator (run once; kept for reference).</summary>
public static class GraphicsSetup
{
    /// <summary>
    /// Runs all Built-in ➡️ URP converters (render settings, materials, read-only materials,
    /// animation clips, Post-processing v2). The command-line entry point of URP 17.6 crashes
    /// without a type filter, so this calls the API with an empty exclusion list.
    /// </summary>
    public static void ConvertToUrp()
    {
        UnityEditor.Rendering.Universal.Converters.RunInBatchMode("BuiltInToURP", new List<string>(), false);
        AssetDatabase.SaveAssets();
        Debug.Log("GraphicsSetup: converted to URP");
    }

    /// <summary>Lists materials whose shader URP cannot render (shown pink), grouped by shader.</summary>
    public static void ListUnsupportedMaterials()
    {
        var byShader = new Dictionary<string, List<string>>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) continue;
            var name = m.shader.name;
            bool ok = name.StartsWith("Universal Render Pipeline") || name.StartsWith("Shader Graphs") || name.StartsWith("Hidden/") ||
                      name.StartsWith("Skybox/") || name.StartsWith("UI/") || name.StartsWith("TextMeshPro") || name.StartsWith("Sprites/");
            if (!m.shader.isSupported) ok = false;
            if (ok) continue;
            if (!byShader.TryGetValue(name, out var list)) byShader[name] = list = new List<string>();
            list.Add(path);
        }
        foreach (var kv in byShader)
            Debug.Log($"UNSUPPORTED {kv.Key}: {kv.Value.Count} materials, e.g. {string.Join(", ", kv.Value.GetRange(0, System.Math.Min(4, kv.Value.Count)))}");
    }

    static bool IsUrpCompatible(Shader shader)
    {
        if (shader == null) return false;
        var name = shader.name;
        if (!shader.isSupported || name == "Hidden/InternalErrorShader") return false;
        if (name.StartsWith("Hidden/Nature") || name.StartsWith("Hidden/TerrainEngine")) return false;
        return name.StartsWith("Universal Render Pipeline") || name.StartsWith("Shader Graphs") || name.StartsWith("Hidden/") ||
               name.StartsWith("Skybox/") || name.StartsWith("UI/") || name.StartsWith("TextMeshPro") || name.StartsWith("Sprites/") ||
               name.StartsWith("GUI/");
    }

    /// <summary>Like ListUnsupportedMaterials, but for every renderer in prefabs and the build scenes (also embedded materials).</summary>
    public static void ListUnsupportedRenderers()
    {
        var found = new Dictionary<string, HashSet<string>>();
        void Check(Renderer r, string where)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || IsUrpCompatible(m.shader)) continue;
                var key = $"{m.shader.name} | {m.name} ({AssetDatabase.GetAssetPath(m)})";
                if (!found.TryGetValue(key, out var set)) found[key] = set = new HashSet<string>();
                if (set.Count < 3) set.Add(where);
            }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) Check(r, path);
        }
        foreach (var scene in new[] { "Assets/_Scenes/MainScene.unity", "Assets/_Scenes/MainScene-night.unity", "Assets/_Scenes/StartScene.unity" })
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)) Check(r, scene + ":" + r.name);
        }
        foreach (var kv in found) Debug.Log($"UNSUPPORTED-R {kv.Key} in {string.Join(", ", kv.Value)}");
    }

    const string UrpLit = "Universal Render Pipeline/Lit";
    const string UrpUnlit = "Universal Render Pipeline/Unlit";
    const string UrpParticles = "Universal Render Pipeline/Particles/Unlit";

    static void SetAlphaClip(Material m, float cutoff)
    {
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", cutoff);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", 0f); // double-sided foliage
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
    }

    static Material NewLit(Texture baseMap, Texture normal, bool foliage, string name)
    {
        var m = new Material(Shader.Find(UrpLit)) { name = name };
        if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.EnableKeyword("_NORMALMAP");
        }
        m.SetFloat("_Smoothness", foliage ? 0.15f : 0.1f);
        if (foliage) SetAlphaClip(m, 0.35f);
        return m;
    }

    /// <summary>
    /// Fixes what the URP converter leaves pink: Tree Creator trees (baked to mesh renderers with
    /// URP Lit materials on their texture atlases), legacy cut-out and particle shaders, the
    /// built-in Default-Material, the mobile skybox, and terrain materials.
    /// </summary>
    public static void FixUnconverted()
    {
        // 1. Tree Creator prefabs
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<Tree>(true) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var dir = System.IO.Path.GetDirectoryName(path) + "/URP";
                System.IO.Directory.CreateDirectory(dir);
                foreach (var tree in root.GetComponentsInChildren<Tree>(true))
                {
                    var r = tree.GetComponent<MeshRenderer>();
                    var mats = r.sharedMaterials;
                    var newMats = new Material[mats.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        bool leaves = m != null && m.shader != null && m.shader.name.Contains("Leaves");
                        var main = m != null && m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                        var matPath = $"{dir}/{System.IO.Path.GetFileNameWithoutExtension(path)}_{(leaves ? "leaves" : "bark")}_{i}.mat";
                        var nm = NewLit(main, null, leaves, System.IO.Path.GetFileNameWithoutExtension(matPath));
                        AssetDatabase.CreateAsset(nm, matPath);
                        newMats[i] = nm;
                    }
                    // keep the generated mesh as an asset of its own: removing Tree may drop it
                    var mf = tree.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && !AssetDatabase.IsMainAsset(mf.sharedMesh))
                    {
                        var meshPath = $"{dir}/{System.IO.Path.GetFileNameWithoutExtension(path)}_mesh.asset";
                        var mesh = Object.Instantiate(mf.sharedMesh);
                        AssetDatabase.CreateAsset(mesh, meshPath);
                        mf.sharedMesh = mesh;
                    }
                    r.sharedMaterials = newMats;
                    Object.DestroyImmediate(tree, true);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("GraphicsSetup: baked Tree Creator tree " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 2. legacy material shaders
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) continue;
            var n = m.shader.name;
            var main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            var color = m.HasProperty("_Color") ? m.GetColor("_Color") : (m.HasProperty("_TintColor") ? m.GetColor("_TintColor") : Color.white);
            if (n == "Unlit/Transparent Cutout")
            {
                float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
                m.shader = Shader.Find(UrpUnlit);
                m.SetTexture("_BaseMap", main);
                m.SetColor("_BaseColor", Color.white);
                SetAlphaClip(m, cutoff);
            }
            else if (n.StartsWith("Legacy Shaders/Particles/"))
            {
                m.shader = Shader.Find(UrpParticles);
                m.SetTexture("_BaseMap", main);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", n.Contains("Additive") ? 2f : 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else if (n.StartsWith("Nature/Tree Creator"))
            {
                bool leaves = n.Contains("Leaves");
                var normal = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                m.shader = Shader.Find(UrpLit);
                m.SetTexture("_BaseMap", main);
                if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                if (leaves) SetAlphaClip(m, 0.35f);
            }
            else if (n == "Mobile/Skybox")
            {
                m.shader = Shader.Find("Skybox/6 Sided");
            }
            else continue;
            EditorUtility.SetDirty(m);
            Debug.Log($"GraphicsSetup: {path}: {n} -> {m.shader.name}");
        }

        // 3. Default-Material and terrains in prefabs and scenes
        var urpDefault = GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.defaultMaterial : null;
        var terrainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Rendering/TerrainLit.mat");
        if (terrainMat == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Settings/Rendering");
            terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(terrainMat, "Assets/Settings/Rendering/TerrainLit.mat");
        }
        bool FixRenderer(Renderer r)
        {
            bool changed = false;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name == "Default-Material" && AssetDatabase.GetAssetPath(mats[i]).Contains("unity_builtin_extra") && urpDefault != null)
                {
                    mats[i] = urpDefault;
                    changed = true;
                }
            }
            if (changed) r.sharedMaterials = mats;
            return changed;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || !asset.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m != null && m.name == "Default-Material"))) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) changed |= FixRenderer(r);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        foreach (var scenePath in new[] { "Assets/_Scenes/MainScene.unity", "Assets/_Scenes/MainScene-night.unity", "Assets/_Scenes/StartScene.unity", "Assets/_Scenes/DriverTestScene.unity" })
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            bool changed = false;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                if (!PrefabUtility.IsPartOfPrefabInstance(r) || PrefabUtility.IsAddedComponentOverride(r)) changed |= FixRenderer(r);
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
            {
                t.materialTemplate = terrainMat;
                changed = true;
            }
            if (changed)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("GraphicsSetup: fixed unconverted materials");
    }

    const string ProfilePath = "Assets/Settings/Rendering/SimulatorVolumeProfile.asset";

    /// <summary>
    /// Quality settings of the URP assets (HDR, MSAA, soft shadows, distance), SSAO on the renderer,
    /// a global post-processing volume in the environment scenes, and an EnvironmentController
    /// (time of day and weather) in every experiment.
    /// </summary>
    public static void ConfigureUrp()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            urp.shadowDistance = 150f;
            urp.shadowCascadeCount = 4;
            urp.supportsCameraDepthTexture = true;
            urp.supportsCameraOpaqueTexture = true;
            var so = new SerializedObject(urp);
            so.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1; // per pixel
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false; // headlights/lamps: too costly
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            Debug.Log("GraphicsSetup: configured " + path);
        }

        // SSAO on every renderer
        var ssaoType = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            data.renderingMode = RenderingMode.Forward; // only the nearest street lights are on (EnvironmentController)
            EditorUtility.SetDirty(data);
            if (ssaoType == null || data.rendererFeatures.Any(f => f != null && f.GetType() == ssaoType)) continue;
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
            feature.name = "SSAO";
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            var so = new SerializedObject(data);
            var map = so.FindProperty("m_RendererFeatureMap");
            map.InsertArrayElementAtIndex(map.arraySize);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            Debug.Log("GraphicsSetup: SSAO added to " + path);
        }

        // post-processing profile
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Settings/Rendering");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.6f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.2f);
            color.contrast.Override(8f);
            color.saturation.Override(5f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
        }
        foreach (var scenePath in new[] { "Assets/_Scenes/MainScene.unity", "Assets/_Scenes/MainScene-night.unity", "Assets/_Scenes/DriverTestScene.unity" })
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            if (Object.FindObjectsByType<Volume>(FindObjectsInactive.Include).All(v => v.sharedProfile != profile))
            {
                var go = new GameObject("Post-processing (URP)");
                var vol = go.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
        }

        // time of day and weather in every experiment
        var dynamicExperiments = new[] { "Base", "Traffic", "SumoTraffic", "PedestrianCrossings" };
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ExperimentDefinitions" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var exp = root.GetComponent<ExperimentDefinition>();
                if (exp == null || root.GetComponentInChildren<EnvironmentController>(true) != null) continue;
                var go = new GameObject("Environment (time of day, weather)");
                go.transform.SetParent(root.transform, false);
                var env = go.AddComponent<EnvironmentController>();
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                env.UseBakedLighting = !dynamicExperiments.Contains(name);
                env.Weather = env.UseBakedLighting ? Weather.Clear : Weather.PartlyCloudy;
                env.TimeOfDay = exp.Scene != null && exp.Scene.ToLower().Contains("night") ? 23f : 14f;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"GraphicsSetup: environment controller added to {path} ({(env.UseBakedLighting ? "baked" : "dynamic")} lighting)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>Re-imports the Rocketbox avatars so their materials are rebuilt for URP.</summary>
    public static void ReimportRocketbox()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/ThirdParty/Rocketbox" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("GraphicsSetup: Rocketbox avatars re-imported");
    }
}
