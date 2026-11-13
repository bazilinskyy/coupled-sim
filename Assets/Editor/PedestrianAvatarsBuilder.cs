using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PedestrianAvatarsBuilder
{
    [MenuItem("Coupled Sim/Pedestrians/Build avatar list")]
    public static void Build()
    {
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/AIPedestrians" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
            .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
            .Where(g => g != null && g.GetComponentInChildren<Animator>() != null)
            .ToArray();
        Directory.CreateDirectory("Assets/Resources");
        const string path = "Assets/Resources/PedestrianAvatars.asset";
        var asset = AssetDatabase.LoadAssetAtPath<PedestrianAvatars>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<PedestrianAvatars>(); AssetDatabase.CreateAsset(asset, path); }
        asset.Prefabs = prefabs;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"PedestrianAvatars: {prefabs.Length} avatars: {string.Join(", ", prefabs.Select(p => p.name))}");
    }
}
