using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Adds the experimenter dashboard to the Managers object of StartScene.</summary>
public static class DashboardSetup
{
    [MenuItem("Coupled Sim/Dashboard/Add to StartScene")]
    public static void AddToStartScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Scenes/StartScene.unity");
        var nm = Object.FindAnyObjectByType<NetworkingManager>();
        if (nm.GetComponent<ExperimentDashboard>() == null) nm.gameObject.AddComponent<ExperimentDashboard>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("DashboardSetup: dashboard added to " + nm.name);
    }
}
