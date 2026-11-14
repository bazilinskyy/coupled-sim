using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Street furniture and parked vehicles for the city maps: static parked bicycles (several
/// colours) and parked cars (the AI cars without scripts, physics, lights or drivers, in several
/// paint colours, culled when small on screen), plus the bench and the waste bin; registered in
/// Resources/MapAssets. Menu: Coupled Sim ➡️ Maps ➡️ Build street props.
/// </summary>
public static class MapPropBuilder
{
    const string Folder = "Assets/Prefabs/Props";

    [MenuItem("Coupled Sim/Maps/Build street props")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        var assets = AssetDatabase.LoadAssetAtPath<MapAssets>("Assets/Resources/MapAssets.asset");
        assets.Bench = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StreetBench/streetbench1.prefab");
        assets.WasteBin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Waste Bin/waste bin_001.prefab");
        assets.ParkedBicycles = TwoWheelerPrefabBuilder.BuildParkedBicycles(Folder);
        foreach (var b in assets.ParkedBicycles) AddCulling(b, 0.015f);
        var cars = new List<GameObject>();
        var colours = new[] { new Color(0.85f, 0.85f, 0.85f), new Color(0.08f, 0.08f, 0.09f), new Color(0.45f, 0.47f, 0.5f), new Color(0.12f, 0.2f, 0.45f), new Color(0.5f, 0.06f, 0.06f), new Color(0.25f, 0.3f, 0.25f) };
        int ci = 0;
        foreach (var src in new[] { "Assets/Prefabs/Cars/DrivableSmartCommon.prefab", "Assets/Prefabs/Cars/DrivableHatchbackCommon.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(src);
            if (prefab == null) continue;
            for (int k = 0; k < 3; k++, ci++)
                cars.Add(ParkedCar(prefab, colours[ci % colours.Length], $"{Folder}/ParkedCar_{Path.GetFileNameWithoutExtension(src).Replace("Drivable", "").Replace("Common", "")}_{k}.prefab"));
        }
        // more models and sizes (Kenney Car Kit, CC0; built by VehicleBuilder), in their own colours
        foreach (var n in new[] { "Sedan", "SedanSports", "HatchbackSports", "Suv", "SuvLuxury", "Taxi", "Van", "Delivery" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Cars/Vehicles/DrivableKenney{n}.prefab");
            if (prefab != null) cars.Add(ParkedCar(prefab, Color.white, $"{Folder}/ParkedCar_Kenney{n}.prefab"));
        }
        assets.ParkedCars = cars.Where(c => c != null).ToArray();
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        Debug.Log($"MapPropBuilder: {assets.ParkedBicycles.Length} bicycles, {assets.ParkedCars.Length} parked cars, bench {assets.Bench != null}, bin {assets.WasteBin != null}");
    }

    static GameObject ParkedCar(GameObject prefab, Color paint, string path)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var conf = go.GetComponent<CarConfigurator>();
            var paintRenderers = conf != null ? conf.paintRenderers : new MeshRenderer[0];
            if (conf != null)
                foreach (var o in new[] { conf.DriverPuppet, conf.PassengerPuppet, conf.Label }) if (o != null) Object.DestroyImmediate(o);
            // paint: a material per colour, shared by the prefabs of that colour
            var matPath = Path.Combine(Folder, Path.GetFileNameWithoutExtension(path) + "_paint.mat");
            Material paintMat = null;
            foreach (var r in paintRenderers.Where(r => r != null))
            {
                if (paintMat == null)
                {
                    paintMat = new Material(r.sharedMaterial);
                    paintMat.SetColor("_BaseColor", paint);
                    paintMat.color = paint;
                    AssetDatabase.CreateAsset(paintMat, matPath);
                }
                r.sharedMaterial = paintMat;
            }
            // only geometry stays: no scripts, physics, lights, cameras, sound, interior screens
            foreach (var c in go.GetComponentsInChildren<Component>(true).Reverse())
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup) continue;
                if (c is BoxCollider && c.gameObject == go) continue;
                Object.DestroyImmediate(c);
            }
            foreach (var cam in go.GetComponentsInChildren<Transform>(true).Where(t => t != null && (t.name.Contains("Camera") || t.name.Contains("Mirror"))).ToArray())
                if (cam != null && cam.gameObject != go) Object.DestroyImmediate(cam.gameObject);
            if (go.GetComponent<BoxCollider>() == null)
            {
                var b = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                box.size = b.size;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            go.name = Path.GetFileNameWithoutExtension(path);
            AddCulling(go, 0.02f);
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // drawn only while larger than this fraction of the screen height
    static void AddCulling(GameObject go, float minScreenHeight)
    {
        bool asset = PrefabUtility.IsPartOfPrefabAsset(go);
        var root = asset ? PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(go)) : go;
        try
        {
            if (!root.TryGetComponent<LODGroup>(out var lod)) lod = root.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(minScreenHeight, root.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
            if (asset) PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GetAssetPath(go));
        }
        finally { if (asset) PrefabUtility.UnloadPrefabContents(root); }
    }
}
