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
    static int _pressFrame = -1;
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
        // no sound from the computer's loudspeakers in tests (a video's sound is rendered offline: VideoExport unmutes
        // the mix only while AudioRenderer records, when nothing goes to the loudspeakers)
        EditorUtility.audioMasterMute = true;
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
        SessionState.SetString("SmokeTest.Fly", Arg("-smokeFly", ""));
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
        if (!_lineupDone && elapsed > 1.0) { _lineupDone = true; SpawnLineup(); }
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
            float steer = Arg2("-smokeSteer", 0f);   // -smokeSteer -1..1: the steering axis held while driving (-0.8 turns the cars right)
            VehicleInput.Override = axis =>
                axis == "Throttle" ? (tIn < accelFor ? 1f : 0f) :
                axis == "Break" || axis == "Brake" ? (tIn < accelFor ? 0f : 1f) :
                axis == "Horizontal" ? steer : (float?)null;
            var dyn = UnityEngine.Object.FindObjectsByType<RealisticVehicleDynamics>().FirstOrDefault(d => d.GetComponentInParent<PlayerAvatar>() == NetworkingManager.Instance.PlayerSystem.LocalPlayer);
            if (dyn != null && (int)(elapsed * 5) != _lastDriveLog)
            {
                _lastDriveLog = (int)(elapsed * 5);
                var rb = dyn.GetComponent<Rigidbody>();
                var w = dyn.transform.InverseTransformDirection(rb.angularVelocity);
                var art = dyn.GetComponent<ArticulatedTrailer>();
                Debug.Log($"SmokeTest drive: t={elapsed:F1}s {dyn.SpeedKmh:F1} km/h" + (art != null ? $" articulation {art.Angle:F1}°" : "") + $" gear {dyn.Gear} {dyn.Rpm:F0} rpm grip {dyn.Grip:F2} on {dyn.Surface} shake {dyn.RoughnessRms:F2} m/s2 pitch rate {w.x * Mathf.Rad2Deg:F2}°/s roll rate {w.z * Mathf.Rad2Deg:F2}°/s vertical {rb.linearVelocity.y:F3} m/s");
            }
        }
        // -smokePress button@seconds: that VehicleInput button is pressed for one frame (e.g. blinker_left@2)
        var cmd = System.Environment.GetCommandLineArgs();
        int pi = System.Array.IndexOf(cmd, "-smokePress");
        var press = pi >= 0 && pi + 1 < cmd.Length ? cmd[pi + 1] : "";
        if (press.Contains("@"))
        {
            var parts = press.Split('@');
            float at = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            if (_pressFrame < 0 && elapsed >= at) _pressFrame = Time.frameCount + 1;
            if (_pressFrame >= 0)
            {
                var prev = VehicleInput.ButtonOverride;
                int frame = _pressFrame; string name = parts[0];
                VehicleInput.ButtonOverride = b => b == name ? Time.frameCount == frame : prev != null ? prev(b) : (bool?)null;
            }
        }
        // -smokeTakeOver seconds: the driver brakes then (to take over from the automation)
        float takeOverAt = Arg2("-smokeTakeOver", -1f);
        if (takeOverAt >= 0f && drive.Length == 0)
        {
            double tIn = elapsed;
            VehicleInput.Override = axis => (axis == "Brake" || axis == "Break") && tIn >= takeOverAt && tIn < takeOverAt + 1.5 ? 0.6f : (float?)null;
        }
        // -smokeFly climb,yaw,forward,lateral: fly the participant's drone (if any) with fixed sticks (-1..1); a climb
        // first lifts it off, the rest follows after 2 s
        var fly = SessionState.GetString("SmokeTest.Fly", "");
        if (fly.Length > 0)
        {
            var f = fly.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            foreach (var dc in UnityEngine.Object.FindObjectsByType<DroneController>(FindObjectsSortMode.None))
            {
                if (!dc.ReadInput || !dc.enabled) continue;
                dc.UseExternalInput = true;
                bool early = elapsed < 2.0;
                dc.ExternalSticks = early ? new Vector4(1f, 0, 0, 0) : new Vector4(f.Length > 0 ? f[0] : 0, f.Length > 1 ? f[1] : 0, f.Length > 2 ? f[2] : 0, f.Length > 3 ? f[3] : 0);
            }
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
        if (elapsed >= SessionState.GetFloat(KeySeconds, 20) && !VideoExport.Busy)   // (a video being made is finished first)
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
        var localCar = NetworkingManager.Instance != null && NetworkingManager.Instance.PlayerSystem != null ? NetworkingManager.Instance.PlayerSystem.LocalPlayer : null;
        if (localCar != null && localCar.CarBlinkers != null) sb.AppendLine($"Blinkers: {localCar.CarBlinkers.State}");

        if (Soundscape.Current != null && Soundscape.Current.VehicleSounds)
            foreach (var v in Soundscape.Current.Voices().Take(5))
                sb.AppendLine($"Sound: {v.car} at {v.kmh:F0} km/h, tyre volume {v.tyre:F4}, engine volume {v.engine:F4}");
        int shot = 0;
        // extra views: -smokeShot "x,y,z,yaw,pitch;x,y,z,yaw,pitch", "chase" (behind the participant) or "front" (ahead, looking back)
        foreach (var view in SessionState.GetString(KeyShot, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var camGo = new GameObject("SmokeTestCamera");
            var c = camGo.AddComponent<Camera>();
            c.fieldOfView = 50;
            c.farClipPlane = 6000f;   // maps with a surroundings photo reach 3 km
            var local = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem.LocalPlayer : null;
            if (view.Trim() == "chase" && local != null)
            {
                // behind and beside the participant's vehicle or avatar
                var t = local.transform;
                // on the side of the road's centre (left in right-hand traffic, right in left-hand traffic)
                float centreSide = OsmMap.Current != null && OsmMap.Current.Info.LeftHandTraffic ? 1f : -1f;
                camGo.transform.position = t.position - t.forward * 4.5f + t.right * 1.8f * centreSide + Vector3.up * 1.9f;
                camGo.transform.LookAt(t.position + Vector3.up * 0.9f + t.forward * 1.5f);
            }
            else if (view.Trim().StartsWith("top") && local != null)
            {
                // "top" or "top:H": straight down from H m (default 60) above the participant, north up
                float hgt = view.Trim().Length > 4 && float.TryParse(view.Trim().Substring(4), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var th) ? th : 60f;
                camGo.transform.position = local.transform.position + Vector3.up * hgt;
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (view.Trim() == "front" && local != null)
            {
                // in front of the participant's vehicle, at a pedestrian's eye height, looking back at it
                var t = local.transform;
                camGo.transform.position = t.position + t.forward * 7f + t.right * 1.2f + Vector3.up * 1.6f;
                camGo.transform.LookAt(t.position + Vector3.up * 0.8f + t.forward * 1.5f);
            }
            else if (view.Trim().StartsWith("lineup") && _lineupLength > 0f)
            {
                // the parked vehicles from the side of the road, three quarters from the front ("lineup") or from the side
                // ("lineupside"); an angled line-up from in front of its first agent, looking along the line
                var mid = _lineupStart + _lineupDir * (_lineupLength * 0.45f);
                Vector3 target;
                if (view.Trim() == "lineupside")
                {
                    camGo.transform.position = mid + _lineupRight * (_lineupLength * 0.55f) + Vector3.up * 3f;
                    target = mid + Vector3.up * 1.2f;
                }
                else if (_lineupAngle > 5f)
                {
                    // opposite the middle of the line, a little towards its start (the agents face that way): all at about
                    // the same distance, three quarters from the front, the whole line in the picture
                    camGo.transform.position = _lineupStart + _lineupDir * (_lineupLength * 0.47f) + _lineupRight * (4f + 0.62f * _lineupLength) + Vector3.up * (1.8f + 0.04f * _lineupLength);
                    target = _lineupStart + _lineupDir * (_lineupLength * 0.5f) + Vector3.up * 0.8f;
                    // posts, trees and signs between the camera and the line-up are taken away
                    var cp = camGo.transform.position; var far = _lineupStart + _lineupDir * (_lineupLength * 0.5f);
                    var fwd2 = Vector3.ProjectOnPlane(far - cp, Vector3.up);
                    var boxRot = Quaternion.Inverse(Quaternion.LookRotation(fwd2)); var boxMid = (cp + far) / 2f;
                    var halfExt = new Vector3(_lineupLength * 0.6f, 0f, fwd2.magnitude / 2f + 2f);
                    // (also objects made with hide flags, such as the traffic signs, which FindObjectsByType leaves out)
                    foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>().Where(x => x != null && x.gameObject.scene.IsValid()))
                    {
                        if (r == null || !r.enabled || _lineupPlaced.Any(p => p != null && r.transform.IsChildOf(p.transform)) || r.GetComponentInParent<PlayerAvatar>() != null) continue;
                        var bs = r.bounds.size;
                        if (r.bounds.max.y < _lineupStart.y + 1.2f || Mathf.Max(bs.x, bs.z) > 12f || r.name.StartsWith("Buildings") || r.name.StartsWith("Roofs")) continue;
                        var q = boxRot * (r.bounds.center - boxMid);
                        if (Mathf.Abs(q.x) < halfExt.x && Mathf.Abs(q.z) < halfExt.z)
                        {
                            r.enabled = false;
                            foreach (var col in r.GetComponents<Collider>()) col.enabled = false;
                        }
                    }
                }
                else
                {
                    camGo.transform.position = _lineupStart - _lineupDir * 2f + _lineupRight * 6f + Vector3.up * 3.2f;
                    target = _lineupStart + _lineupDir * (_lineupLength * 0.35f) + Vector3.up * 1.2f;
                }
                // in front of a building or tree that would block the view
                var toCam = camGo.transform.position - target;
                if (Physics.Raycast(target + toCam.normalized * 4f, toCam.normalized, out var block, toCam.magnitude - 4f, ~0, QueryTriggerInteraction.Ignore))
                    camGo.transform.position = block.point - toCam.normalized * 0.5f;
                camGo.transform.LookAt(target);
                c.fieldOfView = _lineupAngle > 5f && view.Trim() != "lineupside" ? 50f : 55f;
                if (_lineupAngle > 5f && view.Trim() != "lineupside")
                {
                    // when a wall behind the camera brought it closer, a wider view still shows the whole line
                    float d = Vector3.ProjectOnPlane(target - camGo.transform.position, Vector3.up).magnitude;
                    float hfov = 2f * Mathf.Atan((0.55f * _lineupLength + 2f) / Mathf.Max(1f, d)) * Mathf.Rad2Deg;
                    c.fieldOfView = Mathf.Clamp(Camera.HorizontalToVerticalFieldOfView(hfov, 16f / 9f), 50f, 75f);
                }
            }
            else if (view.Trim().StartsWith("animal") && local != null)
            {
                // "animal" (any) or "animal:Dog": the nearest such animal, from a pedestrian's eye height 5 m away
                var kind = view.Trim().Length > 7 ? view.Trim().Substring(7) : "";
                var a = UnityEngine.Object.FindObjectsByType<AnimalAgent>(FindObjectsSortMode.None)
                    .Where(x => kind.Length == 0 || string.Equals(x.Species.ToString(), kind, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => (x.transform.position - local.transform.position).sqrMagnitude).FirstOrDefault();
                if (a == null) { UnityEngine.Object.DestroyImmediate(camGo); sb.AppendLine($"Screenshot '{view.Trim()}' skipped: no animal"); continue; }
                var p = a.transform.position;
                var from = (local.transform.position - p); from.y = 0f;
                if (from.sqrMagnitude < 0.01f) from = Vector3.back;
                camGo.transform.position = p + from.normalized * 5f + Vector3.up * 1.6f;
                camGo.transform.LookAt(p + Vector3.up * 0.4f);
                sb.AppendLine($"Animal shot: {a.name} at {p}");
            }
            else if (view.Trim().StartsWith("sign") && !view.Trim().StartsWith("signal") && local != null)
            {
                // "sign" or "sign:CyclePath": the nearest such traffic sign, from 7 m in front of it at eye height
                var kind = view.Trim().Length > 5 ? view.Trim().Substring(5) : "";
                var sg = UnityEngine.Object.FindObjectsByType<TrafficSign>(FindObjectsSortMode.None)
                    .Where(x => kind.Length == 0 || string.Equals(x.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => (x.transform.position - local.transform.position).sqrMagnitude).FirstOrDefault();
                if (sg == null) { UnityEngine.Object.DestroyImmediate(camGo); sb.AppendLine($"Screenshot '{view.Trim()}' skipped: no sign"); continue; }
                var p = sg.transform.position;
                camGo.transform.position = p + sg.transform.forward * 7f + sg.transform.right * 1.5f + Vector3.up * 1.7f;
                camGo.transform.LookAt(p + Vector3.up * (sg.Height + 0.3f));
                sb.AppendLine($"Sign shot: {sg.name} at {p}");
            }
            else if (view.Trim().StartsWith("signal") || view.Trim().StartsWith("arrow"))
            {
                // "signal", "signal:3" (the n-th nearest signal head to the participant): as a driver sees it, 30 m
                // before it in its approach; "signaltop:3": its junction from 35 m up
                var spec = view.Trim();
                bool top = spec.StartsWith("signaltop");
                int nth = spec.Contains(":") ? int.Parse(spec.Substring(spec.IndexOf(':') + 1)) : 0;
                var origin = local != null ? local.transform.position : Vector3.zero;
                // ("arrow:3": the n-th nearest turn-arrow head)
                var lights = UnityEngine.Object.FindObjectsByType<CarTrafficLight>(FindObjectsSortMode.None)
                    .Where(x => !spec.StartsWith("arrow") || x.Arrow != CarTrafficLight.TurnArrow.None)
                    .OrderBy(x => (x.transform.position - origin).sqrMagnitude).ToList();
                if (lights.Count == 0) { UnityEngine.Object.DestroyImmediate(camGo); sb.AppendLine($"Screenshot '{spec}' skipped: no signal"); continue; }
                var l = lights[Mathf.Min(nth, lights.Count - 1)];
                var p = l.transform.position;
                var toTraffic = Vector3.ProjectOnPlane(TrafficCar.LightFacing(l.transform), Vector3.up).normalized;
                if (top)
                {
                    camGo.transform.position = p + toTraffic * 28f + Vector3.up * 30f;
                    camGo.transform.LookAt(p - toTraffic * 10f - Vector3.up * 5f);
                }
                else
                {
                    camGo.transform.position = p + toTraffic * 30f + Vector3.up * -3.7f;
                    camGo.transform.LookAt(p - toTraffic * 10f + Vector3.up * -3.2f);
                }
                sb.AppendLine($"Signal shot: {l.name} at {p}, state {l.State}");
            }
            else if (view.Trim().StartsWith("aidrone"))
            {
                // "aidrone" or "aidrone:2": the n-th nearest AI drone to the participant, from 2.5 m behind and 0.8 m above it (scaled with the airframe)
                // ("aidrone:Delivery", "aidrone:Patrol", "aidrone:Swarm": the nearest of those)
                var arg = view.Contains(":") ? view.Substring(view.IndexOf(':') + 1).Trim() : "0";
                int nth = int.TryParse(arg, out var nn) ? nn : 0;
                var origin = local != null ? local.transform.position : Vector3.zero;
                var ds = UnityEngine.Object.FindObjectsByType<DroneController>(FindObjectsSortMode.None).Where(d => !d.ReadInput && (char.IsDigit(arg[0]) || d.name.StartsWith(arg, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(d => (d.transform.position - origin).sqrMagnitude).ToList();
                if (ds.Count == 0) { UnityEngine.Object.DestroyImmediate(camGo); sb.AppendLine($"Screenshot '{view.Trim()}' skipped: no AI drone"); continue; }
                var d0 = ds[Mathf.Min(nth, ds.Count - 1)];
                float size = d0.Parameters.ArmLength / 0.19f;
                camGo.transform.position = d0.transform.position - Quaternion.Euler(0, d0.transform.eulerAngles.y, 0) * Vector3.forward * 2.5f * size + Vector3.up * 0.8f * size;
                camGo.transform.LookAt(d0.transform.position);
                sb.AppendLine($"AI drone shot: {d0.name} at {d0.transform.position}, {d0.SpeedKmh:F0} km/h, {d0.HeightAboveGround:F0} m above the ground");
            }
            else if (view.Trim().StartsWith("near:") && local != null)
            {
                // near:Name[,distance]: the nearest part (mesh vertex) of the scene object Name at least 8 m from the
                // participant (e.g. "Manhole covers", "Puddles"), from 4 m on the participant's side (default), 1.4 m up
                var parts = view.Trim().Substring(5).Split(',');
                float back = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 4f;
                var go = GameObject.Find(parts[0]);
                var lp = local.transform.position; Vector3? best = null; float bd = float.MaxValue;
                foreach (var mf in go != null ? go.GetComponentsInChildren<MeshFilter>() : new MeshFilter[0])
                    foreach (var vtx in mf.sharedMesh.vertices)
                    {
                        var w = mf.transform.TransformPoint(vtx); float d = (w - lp).sqrMagnitude;
                        if (d >= 64f && d < bd) { bd = d; best = w; }
                    }
                if (best == null) { UnityEngine.Object.DestroyImmediate(camGo); sb.AppendLine($"Screenshot '{view.Trim()}' skipped: nothing named {parts[0]}"); continue; }
                var dir = lp - best.Value; dir.y = 0f; dir.Normalize();
                camGo.transform.position = best.Value + dir * back + Vector3.up * 1.4f;
                camGo.transform.LookAt(best.Value);
                sb.AppendLine($"Near shot: {parts[0]} at {best.Value}");
            }
            else if (view.Trim().StartsWith("at:"))
            {
                // at:x,y,z,yaw,pitch[,fov] in the scene (y above the ground there, when there is ground)
                var v = view.Trim().Substring(3).Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                float gy = Physics.Raycast(new Vector3(v[0], 500f, v[2]), Vector3.down, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
                camGo.transform.SetPositionAndRotation(new Vector3(v[0], gy + v[1], v[2]), Quaternion.Euler(v.Length > 4 ? v[4] : 0f, v.Length > 3 ? v[3] : 0f, 0f));
                if (v.Length > 5) c.fieldOfView = v[5];
            }
            else if (view.Trim().StartsWith("rel:") && local != null)
            {
                // rel:x,y,z,yaw,pitch[,fov] relative to the participant (x right, z ahead, yaw from its heading)
                var v = view.Trim().Substring(4).Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                var t = local.transform;
                var fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized; var right = Vector3.Cross(Vector3.up, fwd);
                camGo.transform.SetPositionAndRotation(t.position + right * v[0] + Vector3.up * v[1] + fwd * v[2],
                    Quaternion.LookRotation(fwd) * Quaternion.Euler(v.Length > 4 ? v[4] : 0f, v.Length > 3 ? v[3] : 0f, 0f));
                if (v.Length > 5) c.fieldOfView = v[5];
            }
            else if (view.Trim() == "chase" || view.Trim() == "front")
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                sb.AppendLine($"Screenshot '{view.Trim()}' skipped: no local player");
                continue;
            }
            else
            {
                var v = view.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                camGo.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), Quaternion.Euler(v.Length > 4 ? v[4] : 0, v[3], 0));
            }
            var path = Path.Combine(outDir, $"view{shot++}.png");
            // -smokeShotSize WxH (default 1600x900), e.g. larger for pictures cropped later
            int sw = 1600, sh = 900; int iss = Array.IndexOf(Environment.GetCommandLineArgs(), "-smokeShotSize");
            if (iss >= 0 && iss + 1 < Environment.GetCommandLineArgs().Length) { var wh = Environment.GetCommandLineArgs()[iss + 1].Split('x'); sw = int.Parse(wh[0]); sh = int.Parse(wh[1]); }
            SaveCamera(c, path, sw, sh);
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
                // what the groups of objects cost: their renderers off one group at a time; and what is drawn
                var all = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r != null && r.gameObject.scene.IsValid() && r.enabled && r.gameObject.activeInHierarchy).ToList();
                string Group(Renderer r)
                {
                    for (var t = r.transform; t != null; t = t.parent)
                    {
                        var n = t.name;
                        if (n.StartsWith("Parked car")) return "parked cars";
                        if (n.StartsWith("Map traffic") || n.StartsWith("Map cyclist") || n.StartsWith("Traffic ")) return "traffic";
                        if (n.StartsWith("Street furniture")) return "street furniture";
                        if (n == "Palm" || n == "Conifer" || n.StartsWith("Tree")) return "trees";
                        if (n == "Buildings") return "buildings";
                        if (n.StartsWith("Traffic signals") || n.StartsWith("Traffic signs")) return "signals and signs";
                        if (n == "World") return "roads and ground";
                        if (n.StartsWith("Drivable") && t == r.transform.root) return "participant's car";
                    }
                    return "other";
                }
                var groups = all.GroupBy(Group).ToList();
                foreach (var g in groups.OrderByDescending(g => g.Count()))
                {
                    long tris = 0; foreach (var r in g) if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.LongLength / 3; else if (r is SkinnedMeshRenderer sm && sm.sharedMesh != null) tris += sm.sharedMesh.triangles.LongLength / 3;
                    foreach (var r in g) r.enabled = false;
                    double ms = Bench();
                    foreach (var r in g) r.enabled = true;
                    sb.AppendLine($"  without {g.Key} ({g.Count()} renderers, {tris / 1000} k triangles): {ms:F1} ms");
                }
                sb.AppendLine($"  {all.Count} renderers in all");
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

    // -smokeLineup "ToyotaCorolla,DafXF95,...": those vehicle prefabs parked ahead of the participant (for pictures of
    // vehicles in a real street; their scripts are removed, they only stand there). Besides vehicles: two-wheelers with
    // their riders (Bicycle, EScooter, Moped), pedestrians (ped:Female_Adult_01, ped:Claudia), animals (animal:Dog) and
    // the drone (Drone, hovering). -smokeLineupAngle deg: 0 a queue in the oncoming lane facing the participant; e.g. 45
    // side by side at that angle (echelon parking), fronts on a line, facing the camera of the "lineup" view.
    // -smokeLineupAt x,z,heading: the line's start and direction instead of ahead of the participant.
    static bool _lineupDone;
    static Vector3 _lineupStart, _lineupDir, _lineupRight; static float _lineupLength, _lineupAngle;
    static readonly List<GameObject> _lineupPlaced = new List<GameObject>();

    static GameObject LineupPrefab(string name)
    {
        string[] paths;
        if (name.StartsWith("ped:")) { var n = name.Substring(4); paths = new[] { $"Assets/Prefabs/AIPedestrians/Rocketbox/Pedestrian_{n}.prefab", $"Assets/Prefabs/AIPedestrians/Pedestrian_{n}.prefab" }; }
        else if (name.StartsWith("animal:")) paths = new[] { $"Assets/Prefabs/Animals/{name.Substring(7)}.prefab" };
        else paths = new[] { $"Assets/Prefabs/Cars/Vehicles/Drivable{name}.prefab", $"Assets/Prefabs/Cars/Drivable{name}.prefab", $"Assets/Prefabs/TwoWheelers/{name}.prefab", $"Assets/Prefabs/Drones/{name}.prefab" };
        foreach (var path in paths) { var g = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (g != null) return g; }
        return null;
    }

    /// <summary>Bounds of the renderers in the object's own frame (x across, z along).</summary>
    static Bounds LocalBounds(GameObject go)
    {
        var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || !r.enabled || !r.gameObject.activeInHierarchy || r.bounds.size.magnitude > 40f) continue;
            var c = r.bounds.center; var e = r.bounds.extents;
            for (int k = 0; k < 8; k++)
            {
                var p = go.transform.InverseTransformPoint(c + Vector3.Scale(e, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return any ? b : new Bounds(Vector3.up, new Vector3(2f, 2f, 4.5f));
    }

    static void SpawnLineup()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-smokeLineup");
        if (i < 0 || i + 1 >= args.Length) return;
        var local = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem.LocalPlayer : null;
        if (local == null) return;
        var t = local.transform;
        int ia = Array.IndexOf(args, "-smokeLineupAngle");
        _lineupAngle = ia >= 0 && ia + 1 < args.Length ? float.Parse(args[ia + 1], System.Globalization.CultureInfo.InvariantCulture) : 0f;
        int iat = Array.IndexOf(args, "-smokeLineupAt");
        if (iat >= 0 && iat + 1 < args.Length)
        {
            var v = args[iat + 1].Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            _lineupDir = Quaternion.Euler(0, v[2], 0) * Vector3.forward;
            _lineupRight = Vector3.Cross(Vector3.up, _lineupDir);
            _lineupStart = new Vector3(v[0], t.position.y, v[1]);
        }
        else
        {
            _lineupDir = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            _lineupRight = Vector3.Cross(Vector3.up, _lineupDir);
            _lineupStart = t.position + _lineupDir * 7f - _lineupRight * (_lineupAngle > 5f ? 1.5f : 3.2f);   // in the oncoming lane, facing the participant
        }
        float a = _lineupAngle * Mathf.Deg2Rad;
        var facing = Quaternion.LookRotation(-Mathf.Cos(a) * _lineupDir + Mathf.Sin(a) * _lineupRight);
        float z = 0f, prevHalf = -1f;
        var holder = new GameObject("Smoke lineup (inactive)"); holder.SetActive(false);
        var placed = _lineupPlaced; placed.Clear();
        foreach (var raw in args[i + 1].Split(','))
        {
            var name = raw.Trim();
            var prefab = LineupPrefab(name);
            if (prefab == null) { Debug.LogWarning("SmokeTest lineup: no prefab " + name); continue; }
            var go = UnityEngine.Object.Instantiate(prefab, holder.transform);
            bool drone = go.GetComponent<DroneController>() != null;
            // scripts off before it ever wakes up (several passes, as some components require others), except the
            // riders' hands and feet on the grips and pedals
            for (int pass = 0; pass < 4; pass++)
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                    if (!(mb is RiderIK)) try { UnityEngine.Object.DestroyImmediate(mb); } catch { }
            foreach (var cam in go.GetComponentsInChildren<Camera>(true)) cam.gameObject.SetActive(false);
            foreach (var al in go.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(al);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var n in new[] { "DriverModel&Rig", "PassengerModel" }) { var f = go.transform.Find(n); if (f != null) f.gameObject.SetActive(false); }
            // pedestrians standing (their walking controller is set by their AI script, which is gone)
            foreach (var an in go.GetComponentsInChildren<Animator>(true))
                if (name.StartsWith("ped:"))
                {
                    if (an.runtimeAnimatorController == null) an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Prefabs/AIPedestrians/Pedestrian.controller");
                    an.enabled = true; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            if (drone)
            {
                var (_, discs) = DroneBody.Build(go.transform, CoupledSim.Drones.QuadrotorParameters.Consumer);
                foreach (var d in discs) d.enabled = true;   // spinning
            }
            go.transform.SetParent(null, false);
            var lb = LocalBounds(go);
            Vector3 at;
            if (_lineupAngle <= 5f)
            {
                // a queue: back to front along the line
                float back = -lb.min.z, front = lb.max.z;
                var box = go.GetComponent<BoxCollider>();
                if (box != null) { back = Mathf.Max(back, -(box.center.z - box.size.z / 2f)); front = Mathf.Max(front, box.center.z + box.size.z / 2f); }
                z += back;
                at = _lineupStart + _lineupDir * z;
                go.transform.rotation = Quaternion.LookRotation(-_lineupDir);
                z += front + 2.5f;
            }
            else
            {
                // side by side: neighbours apart by their half widths and a gap (larger for larger agents), fronts on the line
                float half = Mathf.Max(lb.max.x, -lb.min.x);
                var bc = go.GetComponent<BoxCollider>();   // vehicles: the body's collider (mirrors and lamps aside)
                if (bc != null && bc.size.x > 0.5f) half = Mathf.Min(half, bc.size.x / 2f + 0.15f);
                float gap = Mathf.Clamp(0.4f * (half + Mathf.Max(prevHalf, 0f)), 0.35f, 1.5f);
                if (prevHalf >= 0f) z += (prevHalf + half + gap) / Mathf.Sin(a);
                prevHalf = half;
                go.transform.rotation = facing;
                at = _lineupStart + _lineupDir * z - (facing * Vector3.forward) * lb.max.z;
            }
            float ground = Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : t.position.y;
            go.transform.position = new Vector3(at.x, ground + (drone ? 1.2f : 0f), at.z);
            placed.Add(go);
        }
        UnityEngine.Object.Destroy(holder);
        _lineupLength = Mathf.Max(z, 1f);
        // street furniture, trees and parked cars where the line-up stands are hidden (not buildings or the ground)
        int hidden = 0;
        foreach (var go in placed)
        {
            var lb = LocalBounds(go);
            var centre = go.transform.TransformPoint(lb.center) + Vector3.up * 0.25f;
            foreach (var c in Physics.OverlapBox(centre, lb.extents + new Vector3(0.3f, -0.1f, 0.3f), go.transform.rotation, ~0, QueryTriggerInteraction.Collide))
            {
                if (c == null || c.transform.IsChildOf(go.transform) || placed.Any(p => c.transform.IsChildOf(p.transform))) continue;
                var target = c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : c.gameObject;
                if (target.GetComponentInParent<PlayerAvatar>() != null && target.GetComponentInParent<PlayerAvatar>() == local) continue;
                var size = c.bounds.size;
                if (Mathf.Max(size.x, size.z) > 25f || target.GetComponent<Terrain>() != null) continue;
                target.SetActive(false); hidden++;
            }
        }
        Debug.Log($"SmokeTest lineup: {args[i + 1]} over {z:F1} m at {_lineupStart} (angle {_lineupAngle:F0}°), {hidden} objects in the way hidden");
    }

    static void Fail(string msg)
    {
        Debug.LogError("SmokeTest: " + msg);
        SessionState.SetBool(KeyActive, false);
        EditorApplication.Exit(2);
    }
}
