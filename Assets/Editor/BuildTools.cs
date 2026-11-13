using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the simulator for the lab PCs: Coupled Sim ➡️ Build ➡️ Windows / macOS / Linux, into
/// Builds/{platform}/. Batch mode: -executeMethod BuildTools.Build -buildTarget Win64|OSX|Linux64
/// [-buildPath path]. Experiment scenes are added to the build settings first.
/// </summary>
public static class BuildTools
{
    [MenuItem("Coupled Sim/Build/Windows (64-bit)")] static void Windows() => Build(BuildTarget.StandaloneWindows64);
    [MenuItem("Coupled Sim/Build/macOS")] static void Mac() => Build(BuildTarget.StandaloneOSX);
    [MenuItem("Coupled Sim/Build/Linux (64-bit)")] static void Linux() => Build(BuildTarget.StandaloneLinux64);

    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>Batch mode entry point.</summary>
    public static void Build()
    {
        var t = Arg("-buildTarget") ?? "OSX";
        var target = t.StartsWith("Win", StringComparison.OrdinalIgnoreCase) ? BuildTarget.StandaloneWindows64
            : t.StartsWith("Linux", StringComparison.OrdinalIgnoreCase) ? BuildTarget.StandaloneLinux64 : BuildTarget.StandaloneOSX;
        bool ok = Build(target, Arg("-buildPath"));
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    public static bool Build(BuildTarget target, string path = null)
    {
        ExperimentSetup.WithStartScene(nm => ExperimentSetup.RegisterScenes(nm), false);
        string name = PlayerSettings.productName;
        string folder = target == BuildTarget.StandaloneWindows64 ? "Windows" : target == BuildTarget.StandaloneOSX ? "macOS" : "Linux";
        path ??= Path.Combine("Builds", folder, target == BuildTarget.StandaloneWindows64 ? name + ".exe" : target == BuildTarget.StandaloneOSX ? name + ".app" : name.Replace(' ', '_'));
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = target, options = BuildOptions.None });
        var s = report.summary;
        Debug.Log($"BuildTools: {s.result} {path} ({s.totalSize / 1e6:F0} MB, {s.totalTime.TotalMinutes:F1} min, {s.totalErrors} errors)");
        return s.result == BuildResult.Succeeded;
    }
}
