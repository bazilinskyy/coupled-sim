using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Batch test of the replay: -executeMethod ReplayTest.Run -replay &lt;binLog&gt; -replayShots &lt;folder&gt; (without -quit).</summary>
public static class ReplayTest
{
    public static void Run()
    {
        // no sound from the computer's loudspeakers in tests (a video's sound is rendered offline: VideoExport unmutes
        // the mix only while AudioRenderer records, when nothing goes to the loudspeakers)
        EditorUtility.audioMasterMute = true;
        EditorSceneManager.OpenScene(ExperimentSetup.StartScenePath);
        EditorApplication.isPlaying = true;
    }
}
