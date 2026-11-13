using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Batch test of the replay: -executeMethod ReplayTest.Run -replay &lt;binLog&gt; -replayShots &lt;folder&gt; (without -quit).</summary>
public static class ReplayTest
{
    public static void Run()
    {
        EditorSceneManager.OpenScene(ExperimentSetup.StartScenePath);
        EditorApplication.isPlaying = true;
    }
}
