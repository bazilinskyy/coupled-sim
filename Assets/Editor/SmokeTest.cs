using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Headless end-to-end check of the simulator. Opens StartScene, starts a host that runs the
/// first trial automatically (keyboard mode, no HMD needed), plays for a while and writes a
/// report with errors/exceptions, spawned agents, how far cars and pedestrians moved, and
/// screenshots of the active cameras.
///
/// Usage (exit code 0 = passed):
///   Unity -batchmode -projectPath . -executeMethod SmokeTest.Run -smokeSeconds 20 -smokeOut SmokeTest -logFile smoke.log
/// Optional: -smokeTrial N (index into NetworkingManager.trials), -smokeScene path (scene to start from).
/// </summary>
[InitializeOnLoad]
public static class SmokeTest
{
    const string KeyActive = "SmokeTest.Active";
    const string KeySeconds = "SmokeTest.Seconds";
    const string KeyOut = "SmokeTest.Out";
    const string KeyTrial = "SmokeTest.Trial";
    const string KeyErrors = "SmokeTest.Errors";
    const string KeyExperiment = "SmokeTest.Experiment";
    const string KeyRole = "SmokeTest.Role";
    const string KeyShot = "SmokeTest.Shot";
    const string KeyParams = "SmokeTest.Params";
    const string KeyRide = "SmokeTest.Ride";

    static double _startTime = -1;
    static double _inGameSince = -1;
    static bool _configured;
    static readonly Dictionary<int, (string name, Vector3 start)> _startPositions = new Dictionary<int, (string, Vector3)>();
    static double _lastSnapshot;

    static SmokeTest()
    {
        if (!SessionState.GetBool(KeyActive, false)) return;
        Application.logMessageReceivedThreaded += OnLog;
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string name, string def)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
        }
        SessionState.SetBool(KeyActive, true);
        SessionState.SetFloat(KeySeconds, float.Parse(Arg("-smokeSeconds", "20"), System.Globalization.CultureInfo.InvariantCulture));
        SessionState.SetString(KeyOut, Path.GetFullPath(Arg("-smokeOut", "SmokeTest")));
        SessionState.SetInt(KeyTrial, int.Parse(Arg("-smokeTrial", "0")));
        SessionState.SetInt(KeyExperiment, int.Parse(Arg("-smokeExperiment", "-1")));
        SessionState.SetInt(KeyRole, int.Parse(Arg("-smokeRole", "-1")));
        SessionState.SetString(KeyShot, Arg("-smokeShot", ""));
        SessionState.SetString(KeyParams, Arg("-smokeParams", ""));
        SessionState.SetString(KeyRide, Arg("-smokeRide", ""));
        SessionState.SetString(KeyErrors, "");
        Directory.CreateDirectory(SessionState.GetString(KeyOut, "SmokeTest"));

        EditorSceneManager.OpenScene(Arg("-smokeScene", "Assets/_Scenes/StartScene.unity"));
        Application.logMessageReceivedThreaded += OnLog;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static readonly object _lock = new object();
    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        // Not from our code or harmless in batch mode without devices
        if (condition.Contains("XR") || condition.Contains("Oculus") || condition.Contains("OVR")) return;
        if (stackTrace != null && stackTrace.Contains("UnityEditor.Search")) return; // editor indexer, not the simulator
        if (condition.StartsWith("ImportFBX Errors")) return; // asset re-import messages, not the simulator
        lock (_lock)
        {
            var s = SessionState.GetString(KeyErrors, "");
            if (s.Length < 20000)
                SessionState.SetString(KeyErrors, s + $"[{type}] {condition}\n{FirstLines(stackTrace, 4)}\n");
        }
    }

    static string FirstLines(string s, int n) => string.Join("\n", (s ?? "").Split('\n').Take(n));

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;

        if (!_configured)
        {
            var nm = UnityEngine.Object.FindAnyObjectByType<NetworkingManager>();
            if (nm == null) return;
            int trial = SessionState.GetInt(KeyTrial, 0);
            if (nm.trials == null || nm.trials.Length == 0)
            {
                Fail("NetworkingManager has no trials to run");
                return;
            }
            var t = nm.trials[Mathf.Clamp(trial, 0, nm.trials.Length - 1)];
            t.InputMode = PlayerSystem.InputMode.Flat;
            if (SessionState.GetInt(KeyExperiment, -1) >= 0) t.experimentIndex = SessionState.GetInt(KeyExperiment, 0);
            if (SessionState.GetInt(KeyRole, -1) >= 0) t.roleIndex = SessionState.GetInt(KeyRole, 0);
            // -smokeParams "name=value;name=value": extra experiment parameters for the trial
            var extra = SessionState.GetString(KeyParams, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(kv => kv.Split('=')).Where(kv => kv.Length == 2)
                .Select(kv => new NetworkingManager.ExperimentParameter { name = kv[0].Trim(), value = kv[1].Trim() });
            t.experimentParameters = (t.experimentParameters ?? new NetworkingManager.ExperimentParameter[0]).Concat(extra).ToArray();
            nm.trials = new[] { t };
            nm.recordVideos = false;
            nm.RunTrialSequenceAutomatically = true;
            _configured = true;
            _startTime = EditorApplication.timeSinceStartup;
            Debug.Log($"SmokeTest: running trial {trial} (experiment {t.experimentIndex}, role {t.roleIndex}) for {SessionState.GetFloat(KeySeconds, 20)} s");
            return;
        }

        // the clock starts once the experiment scene runs (loading a big scene takes a while)
        if (_inGameSince < 0)
        {
            var avatars = UnityEngine.Object.FindObjectsByType<PlayerAvatar>();
            if (SceneManager.GetActiveScene().path != "Assets/_Scenes/StartScene.unity" && avatars.Length > 0 && Time.timeScale > 0)
            {
                _inGameSince = EditorApplication.timeSinceStartup;
                foreach (var a in avatars)
                    _startPositions[a.GetInstanceIDSafe()] = (a.name, a.transform.position);
                Debug.Log($"SmokeTest: experiment running after {_inGameSince - _startTime:F0} s");
            }
            else if (EditorApplication.timeSinceStartup - _startTime > 300)
            {
                Fail("experiment did not start within 300 s");
            }
            return;
        }
        double elapsed = EditorApplication.timeSinceStartup - _inGameSince;
        // -smokeRide drive,steer: ride the participant's two-wheeler (if any) with fixed input
        var ride = SessionState.GetString(KeyRide, "");
        if (ride.Length > 0)
        {
            var parts = ride.Split(',');
            foreach (var tw in UnityEngine.Object.FindObjectsByType<TwoWheelerController>())
            {
                if (!tw.enabled) continue;
                tw.UseExternalInput = true;
                tw.Input = new CoupledSim.TwoWheelers.TwoWheelerInput
                {
                    Drive = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                    Steer = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                };
            }
        }
        // agents spawned later (traffic) are measured from when they first appear
        foreach (var a in UnityEngine.Object.FindObjectsByType<PlayerAvatar>())
        {
            var id = a.GetInstanceIDSafe();
            if (!_startPositions.ContainsKey(id)) _startPositions[id] = (a.name, a.transform.position);
        }
        if (elapsed - _lastSnapshot > 5)
        {
            _lastSnapshot = elapsed;
            Debug.Log($"SmokeTest: t={elapsed:F0}s scene={SceneManager.GetActiveScene().name} fps={1f / Mathf.Max(1e-4f, Time.smoothDeltaTime):F0}");
        }
        if (elapsed >= SessionState.GetFloat(KeySeconds, 20))
        {
            Finish();
        }
    }

    static int GetInstanceIDSafe(this UnityEngine.Object o) => o.GetEntityId().GetHashCode();

    static void Finish()
    {
        var outDir = SessionState.GetString(KeyOut, "SmokeTest");
        var sb = new StringBuilder();
        sb.AppendLine($"Unity {Application.unityVersion}, scenes loaded: " +
                      string.Join(", ", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name)));
        sb.AppendLine($"Average frame time: {Time.smoothDeltaTime * 1000:F1} ms (batch mode, no vsync)");

        int movedCars = 0, cars = 0, peds = 0, movedPeds = 0;
        foreach (var a in UnityEngine.Object.FindObjectsByType<PlayerAvatar>())
        {
            bool isCar = a.Type == AvatarType.Driver;
            if (isCar) cars++; else peds++;
            float moved = 0;
            if (_startPositions.TryGetValue(a.GetInstanceIDSafe(), out var s)) moved = Vector3.Distance(s.start, a.transform.position);
            if (moved > 1f) { if (isCar) movedCars++; else movedPeds++; }
            var twc = a.GetComponent<TwoWheelerController>();
            var extra = twc != null ? $", two-wheeler {twc.Type}: {twc.SpeedKmh:F1} km/h, lean {twc.LeanDeg:F1}°, collided {twc.Collided}" : "";
            sb.AppendLine($"  {(isCar ? "car" : "pedestrian")} {a.name}: moved {moved:F1} m, at {a.transform.position}{extra}");
        }
        foreach (var p in UnityEngine.Object.FindObjectsByType<AIPedestrian>())
        {
            sb.AppendLine($"  AI pedestrian {p.name}: {p.transform.position}, {p.Behaviour}, phase {p.Phase}, {p.DecisionReason}");
        }
        foreach (var f in Directory.GetFiles("ExperimentLogs", "PedestrianDecisions-*.csv").OrderBy(File.GetLastWriteTime).Reverse().Take(1))
        {
            if (File.GetLastWriteTime(f) < DateTime.Now.AddMinutes(-10)) continue;
            var lines = File.ReadAllLines(f);
            sb.AppendLine($"Pedestrian decisions ({f}, {lines.Length - 1} events):");
            foreach (var l in lines.Skip(1).Take(40)) sb.AppendLine("  " + l);
        }
        sb.AppendLine($"Cars: {cars} ({movedCars} moved), pedestrians: {peds} ({movedPeds} moved)");

        int shot = 0;
        // extra views: -smokeShot "x,y,z,yaw,pitch;x,y,z,yaw,pitch"
        foreach (var view in SessionState.GetString(KeyShot, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var camGo = new GameObject("SmokeTestCamera");
            var c = camGo.AddComponent<Camera>();
            c.fieldOfView = 50;
            var local = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem.LocalPlayer : null;
            if (view.Trim() == "chase" && local != null)
            {
                // behind and beside the participant's vehicle or avatar
                var t = local.transform;
                camGo.transform.position = t.position - t.forward * 4.5f - t.right * 1.8f + Vector3.up * 1.9f; // road side (left, right-hand traffic)
                camGo.transform.LookAt(t.position + Vector3.up * 0.9f + t.forward * 1.5f);
            }
            else if (view.Trim() == "chase")
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                sb.AppendLine("Screenshot 'chase' skipped: no local player");
                continue;
            }
            else
            {
                var v = view.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                camGo.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), Quaternion.Euler(v.Length > 4 ? v[4] : 0, v[3], 0));
            }
            var path = Path.Combine(outDir, $"view{shot++}.png");
            SaveCamera(c, path, 1600, 900);
            sb.AppendLine($"Screenshot: {path}");
            UnityEngine.Object.DestroyImmediate(camGo);
        }
        foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>())
        {
            if (!cam.isActiveAndEnabled || cam.targetTexture != null) continue;
            var path = Path.Combine(outDir, $"camera{shot++}_{San(cam.name)}.png");
            SaveCamera(cam, path, 1280, 720);
            sb.AppendLine($"Screenshot: {path}");
            if (shot >= 4) break;
        }

        var errors = SessionState.GetString(KeyErrors, "");
        int errorCount = errors.Split(new[] { "\n[" }, StringSplitOptions.None).Length - (errors.Length == 0 ? 1 : 0);
        sb.AppendLine($"Errors/exceptions: {(errors.Length == 0 ? 0 : errorCount)}");
        sb.AppendLine(errors);
        File.WriteAllText(Path.Combine(outDir, "report.txt"), sb.ToString());
        Debug.Log("SmokeTest report:\n" + sb);

        bool ok = errors.Length == 0 && cars + peds > 0;
        SessionState.SetBool(KeyActive, false);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static string San(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    public static void SaveCamera(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        var old = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = old;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
    }

    static void Fail(string msg)
    {
        Debug.LogError("SmokeTest: " + msg);
        SessionState.SetBool(KeyActive, false);
        EditorApplication.Exit(2);
    }
}
