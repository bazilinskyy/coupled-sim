using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Frame-rate benchmark of a build (-benchmark &lt;seconds&gt; &lt;file&gt;): once the simulation runs,
/// waits a few seconds, records every frame's time for that long, writes the result (average and
/// 1%-low frame rate, frame times, GPU, quality level, resolution) to the file, and quits. Use with
/// -autoRun so the first trial starts by itself.
/// </summary>
public class Benchmark : MonoBehaviour
{
    float _seconds = 60f, _warmup = 5f, _start = -1f;
    string _file = "benchmark.txt";
    readonly System.Collections.Generic.List<float> _frames = new System.Collections.Generic.List<float>();

    public static void FromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-benchmark");
        if (i < 0) return;
        var go = new GameObject("Benchmark");
        DontDestroyOnLoad(go);
        var b = go.AddComponent<Benchmark>();
        if (i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) b._seconds = s;
        if (i + 2 < args.Length && !args[i + 2].StartsWith("-")) b._file = args[i + 2];
        // the size asked with -screen-width/-screen-height, also in a borderless full-screen window
        // (which otherwise takes the display's size, so runs would not compare)
        int Int(string name) { int k = Array.IndexOf(args, name); return k >= 0 && k + 1 < args.Length && int.TryParse(args[k + 1], out var v) ? v : 0; }
        int w = Int("-screen-width"), h = Int("-screen-height");
        if (w > 0 && h > 0) Screen.SetResolution(w, h, Screen.fullScreenMode);
    }

    void Update()
    {
        var host = NetworkingManager.Instance != null ? NetworkingManager.Instance.NetSystem as Host : null;
        if (_start < 0f)
        {
            if (host != null && host.InGame) _start = Time.realtimeSinceStartup + _warmup;
            return;
        }
        if (Time.realtimeSinceStartup < _start) return;
        _frames.Add(Time.unscaledDeltaTime);
        if (Time.realtimeSinceStartup - _start < _seconds) return;
        var ms = _frames.Select(f => f * 1000f).OrderBy(f => f).ToList();
        float avg = ms.Average(), p99 = ms[Mathf.Min(ms.Count - 1, (int)(ms.Count * 0.99f))];
        var ci = CultureInfo.InvariantCulture;
        File.WriteAllText(_file,
            $"frames {ms.Count} over {_seconds.ToString(ci)} s\n" +
            $"average {(1000f / avg).ToString("F1", ci)} fps ({avg.ToString("F2", ci)} ms)\n" +
            $"1% low {(1000f / p99).ToString("F1", ci)} fps ({p99.ToString("F2", ci)} ms)\n" +
            $"median {ms[ms.Count / 2].ToString("F2", ci)} ms, worst {ms[ms.Count - 1].ToString("F2", ci)} ms\n" +
            $"gpu {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), cpu {SystemInfo.processorType}\n" +
            $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, resolution {Screen.width}x{Screen.height} (display {Screen.currentResolution.width}x{Screen.currentResolution.height}, {Screen.fullScreenMode}), vsync {QualitySettings.vSyncCount}\n");
        Debug.Log("Benchmark written to " + Path.GetFullPath(_file));
        Application.Quit();
    }
}
