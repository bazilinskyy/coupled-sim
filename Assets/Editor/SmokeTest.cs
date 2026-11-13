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
    const string KeyDrive = "SmokeTest.Drive";
    static int _lastDriveLog = -1;

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
        SessionState.SetString(KeyDrive, Arg("-smokeDrive", ""));
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
        // may run on a job thread: SessionState only on the main thread (FlushErrors)
        lock (_lock)
        {
            if (_pendingErrors.Length < 20000) _pendingErrors.Append($"[{type}] {condition}\n{FirstLines(stackTrace, 4)}\n");
        }
    }

    static readonly StringBuilder _pendingErrors = new StringBuilder();

    static void FlushErrors()
    {
        lock (_lock)
        {
            if (_pendingErrors.Length == 0) return;
            var s = SessionState.GetString(KeyErrors, "");
            if (s.Length < 20000) SessionState.SetString(KeyErrors, s + _pendingErrors);
            _pendingErrors.Clear();
        }
    }

    static string FirstLines(string s, int n) => string.Join("\n", (s ?? "").Split('\n').Take(n));

    static void Tick()
    {
        FlushErrors();
        if (!EditorApplication.isPlaying) return;

        if (!_configured)
        {
            var nm = UnityEngine.Object.FindAnyObjectByType<NetworkingManager>();
            if (nm == null) return;
            // a client (-connect <host>): the host decides the trial and the role
            if (Environment.GetCommandLineArgs().Contains("-connect"))
            {
                _configured = true;
                _startTime = EditorApplication.timeSinceStartup;
                Debug.Log("SmokeTest: running as a client");
                return;
            }
            int trial = SessionState.GetInt(KeyTrial, 0);
            if (nm.trials == null || nm.trials.Length == 0)
            {
                Fail("NetworkingManager has no trials to run");
                return;
            }
            var t = nm.trials[Mathf.Clamp(trial, 0, nm.trials.Length - 1)];
            // -smokeInputMode Flat|VR|Suite (default Flat): VR without a headset checks the HMD camera set-up (#67)
            t.InputMode = Enum.TryParse(Environment.GetCommandLineArgs().SkipWhile(a => a != "-smokeInputMode").Skip(1).FirstOrDefault() ?? "", true, out PlayerSystem.InputMode im) ? im : PlayerSystem.InputMode.Flat;
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
        // -smokeDrive seconds: full throttle for that long, then full brake (participant's car);
        // negative: select reverse first (as the R key or the wheel's reverse button) and drive backwards
        var drive = SessionState.GetString(KeyDrive, "");
        if (drive.Length > 0)
        {
            float accelFor = float.Parse(drive, System.Globalization.CultureInfo.InvariantCulture);
            bool backwards = accelFor < 0;
            accelFor = Mathf.Abs(accelFor);
            double tIn = elapsed;
            VehicleInput.ButtonOverride = b => backwards && b == "reverse" ? tIn < 0.5 : (bool?)null;
            VehicleInput.Override = axis =>
                axis == "Throttle" ? (tIn < accelFor ? 1f : 0f) :
                axis == "Break" || axis == "Brake" ? (tIn < accelFor ? 0f : 1f) :
                axis == "Horizontal" ? 0f : (float?)null;
            var dyn = UnityEngine.Object.FindObjectsByType<RealisticVehicleDynamics>().FirstOrDefault(d => d.GetComponentInParent<PlayerAvatar>() == NetworkingManager.Instance.PlayerSystem.LocalPlayer);
            if (dyn != null && (int)(elapsed * 2) != _lastDriveLog)
            {
                _lastDriveLog = (int)(elapsed * 2);
                var rb = dyn.GetComponent<Rigidbody>();
                var w = dyn.transform.InverseTransformDirection(rb.angularVelocity);
                Debug.Log($"SmokeTest drive: t={elapsed:F1}s {dyn.SpeedKmh:F1} km/h gear {dyn.Gear} {dyn.Rpm:F0} rpm grip {dyn.Grip:F2} pitch rate {w.x * Mathf.Rad2Deg:F2}°/s roll rate {w.z * Mathf.Rad2Deg:F2}°/s vertical {rb.linearVelocity.y:F3} m/s");
            }
        }
        // -smokeTakeOver seconds: the driver brakes then (to take over from the automation)
        float takeOverAt = Arg2("-smokeTakeOver", -1f);
        if (takeOverAt >= 0f && drive.Length == 0)
        {
            double tIn = elapsed;
            VehicleInput.Override = axis => (axis == "Brake" || axis == "Break") && tIn >= takeOverAt && tIn < takeOverAt + 1.5 ? 0.6f : (float?)null;
        }
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
        // the participant's view should not jump: count changes of more than 30° between samples (#67)
        var view = NetworkingManager.Instance != null && NetworkingManager.Instance.PlayerSystem.LocalPlayer != null
            ? NetworkingManager.Instance.PlayerSystem.LocalPlayer.GetComponentsInChildren<Camera>().FirstOrDefault(c => c.isActiveAndEnabled && c.targetTexture == null) : null;
        if (view != null)
        {
            var q = view.transform.rotation;
            if (_viewSamples > 0 && Quaternion.Angle(q, _lastView) > 30f) _viewJumps++;
            _lastView = q; _viewSamples++; _viewName = view.name;
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
            if (AutomatedDriving.Local != null)
                Debug.Log($"SmokeTest automation: {AutomatedDriving.Local.State}, reaction {AutomatedDriving.Local.LastReactionTime:F2} s, at {AutomatedDriving.Local.transform.position}, heading {AutomatedDriving.Local.transform.eulerAngles.y:F0}");
            foreach (var d in UnityEngine.Object.FindObjectsByType<ScriptedPathDriver>())
                Debug.Log($"SmokeTest path: {d.name} at {d.Distance:F1} m, {d.Speed * 3.6f:F1} km/h, finished {d.Finished}, position {d.transform.position}");
            foreach (var s in UnityEngine.Object.FindObjectsByType<ObservedSteering>())
                Debug.Log($"SmokeTest steering: {s.name} road wheels {s.SteerAngle:F1}°, steering wheel {s.SteerAngle * s.SteeringRatio:F0}°");
        }
        if (elapsed >= SessionState.GetFloat(KeySeconds, 20))
        {
            Finish();
        }
    }

    static float Arg2(string name, float def)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
    }

    static int GetInstanceIDSafe(this UnityEngine.Object o) => o.GetEntityId().GetHashCode();

    static Quaternion _lastView;
    static int _viewSamples, _viewJumps;
    static string _viewName;

    static void Finish()
    {
        FlushErrors();
        var outDir = SessionState.GetString(KeyOut, "SmokeTest");
        var sb = new StringBuilder();
        sb.AppendLine($"Unity {Application.unityVersion}, scenes loaded: " +
                      string.Join(", ", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name)));
        sb.AppendLine($"Average frame time: {Time.smoothDeltaTime * 1000:F1} ms (batch mode, no vsync)");
        if (_viewSamples > 0) sb.AppendLine($"Participant's view ({_viewName}, {NetworkingManager.Instance?.PlayerSystem.PlayerInputMode} mode): {_viewJumps} jumps of more than 30° in {_viewSamples} samples");

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
        // extra views (DisplayViews): cameras on other displays, mirrors rendered to textures
        foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>().Where(c => c.isActiveAndEnabled && c.name.StartsWith("View ")))
        {
            var path = Path.Combine(outDir, $"{San(cam.name)}.png");
            if (cam.targetTexture == null) SaveCamera(cam, path, 1280, 720);
            else
            {
                cam.Render();
                var old = RenderTexture.active; RenderTexture.active = cam.targetTexture;
                var tex = new Texture2D(cam.targetTexture.width, cam.targetTexture.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); tex.Apply();
                RenderTexture.active = old;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            sb.AppendLine($"View: {path}");
        }
        var signs = UnityEngine.Object.FindObjectsByType<TrafficSign>();
        if (signs.Length > 0)
            sb.AppendLine($"Traffic signs: {signs.Length}; " + string.Join("; ", signs.GroupBy(s => s.Kind).Select(g =>
                $"{g.Key} x{g.Count()} e.g. at {g.First().transform.position} facing {g.First().transform.eulerAngles.y:F0}")));
        // rendering cost of the participant's view: 30 renders, each waited for (ReadPixels)
        var viewCam = UnityEngine.Object.FindObjectsByType<Camera>().Where(c => c.isActiveAndEnabled && c.targetTexture == null && c.cullingMask != 0 && c.cullingMask != 32)
            .OrderByDescending(c => c.depth).FirstOrDefault();
        if (viewCam != null)
        {
            var rt = new RenderTexture(1920, 1080, 24);
            var tex = new Texture2D(8, 8, TextureFormat.RGB24, false);
            var prev = viewCam.targetTexture;
            viewCam.targetTexture = rt;
            double Bench()
            {
                viewCam.Render();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 20; i++)
                {
                    viewCam.Render();
                    var old = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
                    RenderTexture.active = old;
                }
                return sw.Elapsed.TotalMilliseconds / 20;
            }
            sb.AppendLine($"Render time of the view ({PathOf(viewCam.transform)}, 1920x1080): {Bench():F1} ms");
            // -smokeBench: what each feature costs
            if (Environment.GetCommandLineArgs().Contains("-smokeBench"))
            {
                var urp = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var data = viewCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                float sd = urp.shadowDistance; int msaa = urp.msaaSampleCount; int cascades = urp.shadowCascadeCount;
                urp.shadowDistance = 0f; sb.AppendLine($"  without shadows: {Bench():F1} ms"); urp.shadowDistance = sd;
                urp.shadowCascadeCount = 2; sb.AppendLine($"  2 shadow cascades: {Bench():F1} ms"); urp.shadowCascadeCount = cascades;
                urp.shadowDistance = 80f; sb.AppendLine($"  shadows to 80 m: {Bench():F1} ms"); urp.shadowDistance = sd;
                urp.msaaSampleCount = 1; sb.AppendLine($"  without MSAA: {Bench():F1} ms"); urp.msaaSampleCount = msaa;
                if (data != null) { bool pp = data.renderPostProcessing; data.renderPostProcessing = false; sb.AppendLine($"  without post-processing: {Bench():F1} ms"); data.renderPostProcessing = pp; }
                float far = viewCam.farClipPlane; viewCam.farClipPlane = Mathf.Min(far, 300f); sb.AppendLine($"  far plane 300 m (was {far}): {Bench():F1} ms"); viewCam.farClipPlane = far;
                float rs = urp.renderScale; urp.renderScale = 0.75f; sb.AppendLine($"  render scale 0.75: {Bench():F1} ms"); urp.renderScale = rs;
                sb.AppendLine($"  again as configured: {Bench():F1} ms");
            }
            viewCam.targetTexture = prev;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(tex);
        }
        // what the participant's screen shows: the cameras on display 1, in depth order
        var screenCams = UnityEngine.Object.FindObjectsByType<Camera>()
            .Where(c => c.isActiveAndEnabled && c.targetTexture == null && c.targetDisplay == 0).OrderBy(c => c.depth).ToList();
        sb.AppendLine("Cameras on the screen (back to front): " + string.Join("; ", screenCams.Select(c =>
            $"{PathOf(c.transform)} depth {c.depth} clear {c.clearFlags} mask {c.cullingMask}")));
        // a camera that clears the screen at the same depth as the participant's makes the view depend on the render order
        foreach (var g in screenCams.Where(c => c.clearFlags == CameraClearFlags.SolidColor || c.clearFlags == CameraClearFlags.Skybox).GroupBy(c => c.depth).Where(g => g.Count() > 1))
            Debug.LogError($"SmokeTest: cameras {string.Join(", ", g.Select(c => PathOf(c.transform)))} clear the screen at the same depth {g.Key}");
        FlushErrors();

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

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

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
