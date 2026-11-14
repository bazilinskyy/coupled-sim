using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders the vehicle prefabs side by side (Coupled Sim > Vehicles > Render line-up, or
/// -executeMethod VehicleShowcase.Render -showcaseOut folder [-showcaseFilter Kenney]): a front
/// three-quarter view and a side view, for checking sizes and for the README.
/// </summary>
public static class VehicleShowcase
{
    [MenuItem("Coupled Sim/Vehicles/Render line-up")]
    public static void Render()
    {
        var args = System.Environment.GetCommandLineArgs();
        string Arg(string n, string d) { int i = System.Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : d; }
        var outDir = Arg("-showcaseOut", "ExperimentLogs/Showcase");
        var filter = Arg("-showcaseFilter", "");
        Directory.CreateDirectory(outDir);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.68f, 0.8f); RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.55f); RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(40f, 1f, 10f);
        var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")); gm.SetColor("_BaseColor", new Color(0.33f, 0.33f, 0.34f)); gm.SetFloat("_Smoothness", 0.1f);
        ground.GetComponent<Renderer>().sharedMaterial = gm;

        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Cars/Vehicles", "Assets/Prefabs/Cars" })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(p => p.Contains("/Drivable") && !p.Contains("no_driver") && p.Contains(filter)).OrderBy(p => p.Contains("Vehicles/") ? 1 : 0).ThenBy(p => p).ToList();
        float x = 0f;
        foreach (var p in paths)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p));
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is SkinnedMeshRenderer)).ToArray();
            var b = rs.Length > 0 ? rs.Aggregate(rs[0].bounds, (acc, r) => { acc.Encapsulate(r.bounds); return acc; }) : new Bounds();
            go.transform.position = new Vector3(x + b.extents.x - b.center.x, 0f, 0f);
            x += b.size.x + 1.2f;
        }
        float mid = x / 2f;
        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.78f, 0.84f, 0.9f);
        cam.fieldOfView = 30f; cam.farClipPlane = 500f;
        void Shot(string name, Vector3 pos, Vector3 look)
        {
            cam.transform.position = pos; cam.transform.LookAt(look);
            var rt = new RenderTexture(2400, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
        float dist = x * 0.95f / (2f * Mathf.Tan(15f * Mathf.Deg2Rad)) / (2400f / 900f);
        Shot("lineup_front", new Vector3(mid, 4f, dist + 4f), new Vector3(mid, 1.2f, 0f));
        Shot("lineup_side", new Vector3(mid + dist * 0.9f, 5f, dist * 0.7f), new Vector3(mid, 1.0f, 0f));
        Debug.Log($"VehicleShowcase: {paths.Count} vehicles -> {outDir}: {string.Join(", ", paths.Select(Path.GetFileNameWithoutExtension))}");
    }
}
