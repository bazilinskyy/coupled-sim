using UnityEditor;
using UnityEngine;

/// <summary>Coupled Sim ➡️ Maps ➡️ Export road network: the loaded world's roads to ExperimentLogs (GeoJSON + OBJ).</summary>
public static class RoadNetworkMenu
{
    [MenuItem("Coupled Sim/Maps/Export road network")]
    public static void Export()
    {
        var name = OsmMap.Current != null && OsmMap.Current.Generator != null ? OsmMap.Current.Info.Name : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        var path = RoadNetworkExport.Export("ExperimentLogs", name);
        EditorUtility.RevealInFinder(path);
    }

    /// <summary>Batch mode: -executeMethod RoadNetworkMenu.ExportScene -scene Assets/_Scenes/MainScene.unity</summary>
    public static void ExportScene()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-scene");
        var scene = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Assets/_Scenes/MainScene.unity";
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);
        var name = System.IO.Path.GetFileNameWithoutExtension(scene);
        // the experiments in this scene bring the paths, crossings and lights
        var temp = new System.Collections.Generic.List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ExperimentSetup.ExperimentFolder }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null && go.TryGetComponent<ExperimentDefinition>(out var e) && e.Scene == name)
                temp.Add((GameObject)PrefabUtility.InstantiatePrefab(go));
        }
        try { RoadNetworkExport.Export("ExperimentLogs", name); }
        finally { foreach (var g in temp) Object.DestroyImmediate(g); }
    }
}
