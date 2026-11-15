using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// -quality auto: once a scene runs, the frame rate is measured for a few seconds; while it is below
/// <see cref="TargetFps"/>, the next lower quality level is used (Fantastic, then Balanced, then Performance: on a
/// laptop GPU Performance draws a city map about 2.5 times faster). The choice is logged and kept for later scenes.
/// </summary>
public class AutoQuality : MonoBehaviour
{
    public static float TargetFps = 25f;
    static bool _done;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-quality");
        if (i < 0 || i + 1 >= args.Length || !string.Equals(args[i + 1], "auto", StringComparison.OrdinalIgnoreCase)) return;
        int f = Array.IndexOf(args, "-targetFps");
        if (f >= 0 && f + 1 < args.Length && float.TryParse(args[f + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t)) TargetFps = t;
        SceneManager.sceneLoaded += (s, m) =>
        {
            if (_done || s.name == "StartScene") return;
            var go = new GameObject("Auto quality");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoQuality>();
        };
    }

    IEnumerator Start()
    {
        _done = true;
        yield return new WaitForSecondsRealtime(3f);   // (the map settles: textures, first frames)
        while (true)
        {
            int frames = 0; float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 4f) { frames++; yield return null; }
            float fps = frames / (Time.unscaledTime - t0);
            int level = QualitySettings.GetQualityLevel();
            if (fps >= TargetFps || level >= QualitySettings.names.Length - 1)
            {
                Debug.Log($"Auto quality: {QualitySettings.names[level]} ({fps:F0} fps)");
                break;
            }
            QualitySettings.SetQualityLevel(level + 1, true);
            Debug.Log($"Auto quality: {fps:F0} fps is below {TargetFps}: {QualitySettings.names[level + 1]}");
        }
        Destroy(gameObject);
    }
}
