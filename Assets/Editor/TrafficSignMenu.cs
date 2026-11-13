using UnityEditor;
using UnityEngine;

/// <summary>GameObject ➡️ Coupled Sim ➡️ Traffic sign: a sign at the scene view's pivot (or under the selection).</summary>
public static class TrafficSignMenu
{
    [MenuItem("GameObject/Coupled Sim/Traffic sign", false, 10)]
    static void Create(MenuCommand command)
    {
        var go = new GameObject("Traffic sign");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        if (command.context == null && SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;
        go.AddComponent<TrafficSign>();
        Undo.RegisterCreatedObjectUndo(go, "Create traffic sign");
        Selection.activeObject = go;
    }
}
