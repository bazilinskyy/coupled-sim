using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays a recorded trial back in its world: the start screen's "Replay a recording" button.
/// Loads the experiment's scene (and city map), puts the recorded vehicles, pedestrians and AI
/// cars in it and moves them as recorded, with the traffic lights, blinkers and lights.
/// Keys: Space play/pause, ←/→ 5 s back/forward, -/+ slower/faster, 1-9 follow a participant,
/// 0 free camera (WASD, Q/E down/up, Shift faster, right mouse button looks around), C camera
/// inside/behind, T trails, Esc back to the start screen. Nothing is sent over the network and no
/// new log is written.
/// Each participant moves between the samples as they came in (<see cref="ReplayTrack"/>): a host gets the other
/// participants' vehicles and bodies only with each network update. With -replayWith "a.binLog;b.binLog" (the other
/// participants' own logs of the same session) each of them moves as in its own log instead, at every physics step,
/// started at the moment the host first saw it move. A car's steering wheel turns with the curvature of its logged
/// path (steering is not logged), and stands still while the car stands.
/// </summary>
public class ReplayPlayer : MonoBehaviour
{
    public static ReplayPlayer Active { get; private set; }

    string _path;
    ReplayInfo _info;
    ReplayLog _log;
    Dictionary<string, PlayerAvatar> _prefabs;
    PlayerAvatar _carFallback, _pedestrianFallback;
    readonly List<PlayerAvatar> _drivers = new List<PlayerAvatar>();
    readonly List<PlayerAvatar> _pedestrians = new List<PlayerAvatar>();
    readonly List<(Transform t, Transform[] props, Renderer[] discs, float angle)> _drones = new List<(Transform, Transform[], Renderer[], float)>();
    readonly List<PlayerAvatar> _droneParticipants = new List<PlayerAvatar>();
    readonly List<LineRenderer> _trails = new List<LineRenderer>();
    readonly List<PlayerAvatar> _participantsCache = new List<PlayerAvatar>();
    Material _trailMaterial;
    TrafficLightsSystem _lights;
    ReplayTrack[] _driverTracks, _pedestrianTracks;
    ObservedSteering[] _steering;
    Camera _camera;
    bool _ready, _playing = true, _inside, _showTrails;
    float _time, _speed = 1f, _yaw, _pitch = 15f;
    int _follow = -1;   // index into Participants; -1 = free camera
    string _status;

    static readonly float[] Speeds = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f, 8f };

    List<PlayerAvatar> Participants => _participantsCache;
    /// <summary>The participants of the recording (cars and two-wheelers first, then pedestrians), e.g. for VideoExport's grid.</summary>
    public List<PlayerAvatar> ReplayParticipants => _ready ? _participantsCache : new List<PlayerAvatar>();
    bool _video;

    /// <summary>Starts the replay of a recording (a .binLog with its .replay.json).</summary>
    /// <summary>
    /// Starts the replay of a recording (a .binLog with its .replay.json). Recordings from before the
    /// replay existed have no .replay.json: then the experiment (and the city map of a map
    /// experiment) chosen by the user is used, with the experiment's first car and pedestrian models.
    /// Returns an error message, or null.
    /// </summary>
    public static string Begin(string binLog, NetworkingManager nm, ExperimentDefinition chosen = null, string chosenMap = null)
    {
        var info = ReplayInfo.Load(binLog);
        if (info == null && chosen == null) return "No .replay.json: choose the experiment";
        ReplayLog log;
        try { log = ReplayLog.Read(binLog); }
        catch (Exception e) { Debug.LogError($"Replay: cannot read {binLog}: {e.Message}"); return "Cannot read the recording (made by an older version?): " + e.Message; }
        if (log.Frames.Count == 0) { Debug.LogError("Replay: the recording has no frames: " + binLog); return "The recording has no frames"; }
        if (info == null)
        {
            var car = nm.Players.DriverPrefabs != null && nm.Players.DriverPrefabs.Length > 0 ? nm.Players.DriverPrefabs[0].name : "";
            info = new ReplayInfo
            {
                Experiment = chosen.ShortName,
                Scene = string.IsNullOrEmpty(chosen.Scene) ? "OsmMap" : chosen.Scene,
                Map = string.IsNullOrEmpty(chosen.Map) ? null : chosenMap ?? chosen.Map,
                LocalDriver = log.LocalDriver,
                Drivers = Enumerable.Repeat(car, log.PersistentDrivers).ToArray(),
                Pedestrians = Enumerable.Repeat(nm.Players.PedestrianPrefab != null ? nm.Players.PedestrianPrefab.name : "", log.PedestrianCount).ToArray(),
            };
            Debug.Log($"Replay: {binLog} has no .replay.json; replayed in {chosen.ShortName}{(info.Map != null ? ", map " + info.Map : "")} with standard models");
        }

        var go = new GameObject("Replay");
        DontDestroyOnLoad(go);
        var r = go.AddComponent<ReplayPlayer>();
        r._path = binLog;
        r._info = info;
        r._log = log;
        // prefabs by name, taken now (the start scene's managers may go away)
        r._prefabs = new Dictionary<string, PlayerAvatar>();
        void Add(PlayerAvatar p) { if (p != null && !r._prefabs.ContainsKey(p.name)) r._prefabs[p.name] = p; }
        var ps = nm.Players;
        Add(ps.PedestrianPrefab);
        foreach (var p in ps.DriverPrefabs ?? new PlayerAvatar[0]) Add(p);
        foreach (var p in ps.TwoWheelerPrefabs ?? new PlayerAvatar[0]) Add(p);
        foreach (var a in nm.AICars.Prefabs ?? new AICar[0]) if (a != null && a.TryGetComponent<PlayerAvatar>(out var pa)) Add(pa);
        r._carFallback = ps.DriverPrefabs != null && ps.DriverPrefabs.Length > 0 ? ps.DriverPrefabs[0] : null;
        r._pedestrianFallback = ps.PedestrianPrefab;
        r._experiment = nm.Experiments.FirstOrDefault(e => e != null && e.ShortName == info.Experiment);
        r.BuildTracks(Arg("-replayWith"));
        Active = r;
        Debug.Log($"Replay: {binLog}: {log.Frames.Count} frames, {log.Duration:F1} s, experiment {info.Experiment}, scene {info.Scene}{(info.Map != null ? ", map " + info.Map : "")}");

        SceneManager.sceneLoaded += r.OnSceneLoaded;
        OsmMap.Requested = info.Map;
        SceneManager.LoadScene(info.Scene);
        return null;
    }

    ExperimentDefinition _experiment;

    static string Arg(string name)
    {
        var args = CommandLine.Args;
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>
    /// The participants' tracks: from this log, or (other participants' own logs, -replayWith) from their own,
    /// started when this log first shows them moving. Their own logs have them at every physics step; this one only
    /// as the network brought them.
    /// </summary>
    void BuildTracks(string others)
    {
        int maxDrivers = _log.Frames.Max(f => f.DriverCount);
        _driverTracks = new ReplayTrack[maxDrivers];
        for (int i = 0; i < maxDrivers; i++) _driverTracks[i] = new ReplayTrack(_log, i, false);
        _pedestrianTracks = new ReplayTrack[_log.PedestrianCount];
        for (int i = 0; i < _log.PedestrianCount; i++) _pedestrianTracks[i] = new ReplayTrack(_log, i, true);
        for (int i = 0; i < Mathf.Min(_info.Drivers.Length, maxDrivers); i++)
            if (_driverTracks[i].Changes > 2 && _driverTracks[i].Interval > 0.1f)
                Debug.Log($"Replay: participant {i + 1} ({_info.Drivers[i]}) has a new sample only every {_driverTracks[i].Interval:F2} s in this log" +
                          (i == _log.LocalDriver ? "" : " (as the network brought it; its own log, -replayWith, has every step)"));
        if (string.IsNullOrEmpty(others)) return;
        foreach (var path in others.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            ReplayLog log;
            try { log = ReplayLog.Read(path.Trim()); }
            catch (Exception e) { Debug.LogWarning($"Replay: cannot read {path}: {e.Message}"); continue; }
            if (log.Frames.Count == 0) continue;
            // its own participant: the local driver, or the pedestrian whose samples change most often
            ReplayTrack own;
            if (log.LocalDriver >= 0) own = new ReplayTrack(log, log.LocalDriver, false);
            else
            {
                own = null;
                for (int i = 0; i < log.PedestrianCount; i++) { var t = new ReplayTrack(log, i, true); if (own == null || t.Changes > own.Changes) own = t; }
            }
            if (own == null) { Debug.LogWarning($"Replay: {path} has no participant of its own"); continue; }
            var here = own.Pedestrian ? (own.Index < _pedestrianTracks.Length ? _pedestrianTracks[own.Index] : null) : (own.Index < _driverTracks.Length ? _driverTracks[own.Index] : null);
            if (here == null) { Debug.LogWarning($"Replay: {path}: its participant is not in this recording"); continue; }
            // started when this log first shows it moving (where its own log was at that point)
            float t0 = here.FirstMove(out var p0), own0 = -1f, err = 0f;
            if (t0 >= 0f) own0 = own.TimeAt(p0, 0f, 0.05f, out err);
            if (t0 < 0f || own0 < 0f || err > 1f) { Debug.LogWarning($"Replay: {path}: its participant does not match this recording (off by {err:F1} m)"); continue; }
            var shifted = new ReplayTrack(log, own.Index, own.Pedestrian, own0 - t0);
            if (own.Pedestrian) _pedestrianTracks[own.Index] = shifted; else _driverTracks[own.Index] = shifted;
            // how this log's clock ran compared with theirs (a host far behind real time: its own car and the AI traffic
            // move at the host's pace, the others at their own)
            float t1 = here.LastMove(out var p1), own1 = t1 > t0 ? own.TimeAt(p1, own0, 0.05f, out _) : -1f;
            string pace = t1 > t0 + 1f && own1 > own0 ? $"; its clock ran about {(own1 - own0) / (t1 - t0):F1}x as fast as this log's" : "";
            Debug.Log($"Replay: {(own.Pedestrian ? "pedestrian " + (own.Index + 1) : "participant " + (own.Index + 1))} from its own log {System.IO.Path.GetFileName(path)} ({own.Changes} samples, from {own0 - t0:+0.00;-0.00} s{pace})");
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        try { Setup(); }
        catch (Exception e) { Debug.LogError("Replay: " + e); _status = "Replay failed: " + e.Message; }
    }

    // MonoBehaviours of the experiment kept running in the replay: the world's look; everything that spawns or drives is off
    static readonly HashSet<string> KeepInExperiment = new HashSet<string> { "EnvironmentController", "CarTrafficLight", "PedestrianTrafficLight", "TrafficSign" };
    // MonoBehaviours kept on the replayed avatars
    // (RiderIK: the rider's hands on the grips and feet on the pedals; without it the figure stands in its bicycle)
    static readonly HashSet<string> KeepOnAvatar = new HashSet<string> { "PlayerAvatar", "CarBlinkers", "SteeringWheel", "ObservedSteering", "WheelRotator", "RiderIK" };

    void Setup()
    {
        // the experiment's own objects (environment, traffic lights), without its spawners and scripts
        if (_experiment != null)
        {
            var holder = new GameObject("Replay experiment");
            holder.SetActive(false);
            var exp = Instantiate(_experiment.gameObject, holder.transform);
            foreach (var mb in exp.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && !KeepInExperiment.Contains(mb.GetType().Name) && !IsUnity(mb))
                    mb.enabled = false;
            exp.transform.SetParent(null, true);
            Destroy(holder);
        }
        var env = EnvironmentController.Instance;
        if (env != null)
        {
            if (_info.TimeOfDay >= 0f) env.TimeOfDay = _info.TimeOfDay;
            if (Enum.TryParse(_info.Weather ?? "", true, out Weather w)) env.Weather = w;
            env.TimeSpeed = 0f;
        }
        _lights = FindAnyObjectByType<TrafficLightsSystem>();
        if (_lights != null)
            foreach (var mb in _lights.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is TrafficLightsManager || mb is TrafficLightsSystem) mb.enabled = false;

        // the recorded agents
        var allDrivers = _info.Drivers.Concat(_info.AICars).ToList();
        int maxDrivers = _log.Frames.Max(f => f.DriverCount);
        for (int i = 0; i < maxDrivers; i++)
        {
            var name = i < allDrivers.Count ? allDrivers[i] : null;
            _drivers.Add(Spawn(name, _carFallback, i >= _info.Drivers.Length));
        }
        for (int i = 0; i < _log.PedestrianCount; i++)
            _pedestrians.Add(Spawn(i < _info.Pedestrians.Length ? _info.Pedestrians[i] : null, _pedestrianFallback, false));
        // steering wheels turned from the logged path
        _steering = _drivers.Select(d => d != null && d.TryGetComponent<ObservedSteering>(out var s) ? s : null).ToArray();
        foreach (var s in _steering) if (s != null) s.Driven = true;

        // drones (participants' and AI): the procedural body, posed from the log
        foreach (var entry in _info.Drones ?? new List<string>())
        {
            var parts = entry.Split('|');
            var go = new GameObject("Replay " + (parts.Length > 2 ? parts[2] : "drone"));
            var (props, discs) = DroneBody.Build(go.transform, CoupledSim.Drones.QuadrotorParameters.Preset(parts.Length > 1 ? parts[1] : "Consumer"));
            _drones.Add((go.transform, props, discs, 0f));
            if (parts[0] == "participant") { var pa = go.AddComponent<PlayerAvatar>(); pa.Type = AvatarType.Drone; pa.cameras = new Camera[0]; _droneParticipants.Add(pa); }
        }

        // own camera; the scene's cameras off
        foreach (var c in FindObjectsByType<Camera>())
            if (c.targetTexture == null) c.enabled = false;
        foreach (var l in FindObjectsByType<AudioListener>()) l.enabled = false;
        var cam = new GameObject("Replay camera");
        cam.transform.SetParent(transform, false);
        _camera = cam.AddComponent<Camera>();
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = 2000f;
        cam.AddComponent<AudioListener>();
        _camera.tag = "MainCamera";

        // start by following the participant who recorded it
        _participantsCache.Clear();
        for (int i = 0; i < _info.Drivers.Length && i < _drivers.Count; i++) _participantsCache.Add(_drivers[i]);
        _participantsCache.AddRange(_pedestrians);
        _participantsCache.AddRange(_droneParticipants);
        var participants = Participants;
        _follow = _info.LocalDriver >= 0 && _info.LocalDriver < _info.Drivers.Length ? _info.LocalDriver : (participants.Count > 0 ? 0 : -1);
        if (_follow < 0 && _drivers.Count > 0) _camera.transform.position = _log.Frames[0].Positions[0] + new Vector3(0, 30, -30);
        BuildTrails();
        _ready = true;
        ApplyFrame();
        // sound of the replayed scene, and -videoCams videos of it
        Soundscape.Ensure();
        _video = VideoExport.Ensure() != null;
    }

    // Unity's own components (UI images and text, animation rigging): left as they are
    static bool IsUnity(MonoBehaviour mb) { var ns = mb.GetType().Namespace ?? ""; return ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro"); }

    PlayerAvatar Spawn(string objectName, PlayerAvatar fallback, bool ai)
    {
        var prefabName = objectName != null ? ReplayInfo.PrefabName(objectName) : null;
        if (prefabName == null || !_prefabs.TryGetValue(prefabName, out var prefab)) prefab = fallback;
        if (prefab == null) return null;
        var a = Instantiate(prefab);
        a.name = "Replay " + (objectName ?? prefab.name);
        if (objectName != null && objectName.Contains("(right-hand drive)")) RightHandDrive.Apply(a.gameObject);
        a.Initialize(true, PlayerSystem.InputMode.None, PlayerSystem.ControlMode.HostAI, PlayerSystem.VehicleType.MDV);
        foreach (var mb in a.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && !KeepOnAvatar.Contains(mb.GetType().Name) && !IsUnity(mb)) mb.enabled = false;
        // the driver's eye relative to the vehicle, before anything poses the figure that may carry the camera
        var eye = a.cameras != null && a.cameras.Length > 0 && a.cameras[0] != null ? a.cameras[0].transform : null;
        if (eye != null && a.Type == AvatarType.Driver)
        {
            var re = a.gameObject.AddComponent<ReplayEye>();
            re.LocalPosition = a.transform.InverseTransformPoint(eye.position);
            re.LocalYaw = Mathf.DeltaAngle(a.transform.eulerAngles.y, eye.eulerAngles.y);
        }
        foreach (var c in a.GetComponentsInChildren<Camera>(true)) c.enabled = false;
        foreach (var l in a.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
        foreach (var s in a.GetComponentsInChildren<AudioSource>(true)) s.enabled = false;
        foreach (var rb in a.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        a.gameObject.SetActive(!ai);
        return a;
    }

    void BuildTrails()
    {
        var colors = new[] { new Color(1f, 0.3f, 0.2f), new Color(0.2f, 0.6f, 1f), new Color(0.3f, 0.9f, 0.3f), new Color(1f, 0.8f, 0.2f), new Color(0.8f, 0.4f, 1f) };
        _trailMaterial = new Material(Shader.Find("Sprites/Default"));
        int nDrivers = _info.Drivers.Length;
        for (int p = 0; p < nDrivers + _pedestrians.Count; p++)
        {
            // one line per stretch: a jump (the participant placed at its start, a respawn) starts a new one, so no
            // trail cuts straight across buildings or water
            var pieces = new List<List<Vector3>> { new List<Vector3>() };
            Vector3 last = new Vector3(float.MaxValue, 0, 0);
            foreach (var f in _log.Frames)
            {
                Vector3 q;
                if (p < nDrivers) { if (p >= f.DriverCount) continue; q = f.Positions[p]; }
                else { var pose = f.Pedestrians[p - nDrivers]; if (pose.LocalPositions == null || pose.LocalPositions.Count == 0) continue; q = pose.LocalPositions[0]; }
                if ((q - last).sqrMagnitude < 0.25f) continue;
                if (last.x != float.MaxValue && (q - last).sqrMagnitude > 15f * 15f && pieces[pieces.Count - 1].Count > 0) pieces.Add(new List<Vector3>());
                pieces[pieces.Count - 1].Add(q + Vector3.up * 0.15f);
                last = q;
            }
            foreach (var points in pieces)
            {
                if (points.Count < 2) continue;
                var lr = new GameObject("Trail " + p).AddComponent<LineRenderer>();
                lr.transform.SetParent(transform, false);
                lr.sharedMaterial = _trailMaterial;
                lr.startColor = lr.endColor = colors[p % colors.Length];
                lr.widthMultiplier = 0.25f;
                lr.positionCount = points.Count;
                lr.SetPositions(points.ToArray());
                lr.enabled = _showTrails;
                _trails.Add(lr);
            }
        }
    }

    /// <summary>-replay &lt;binLog&gt;: start this replay at once (from NetworkingManager). With -replayShots &lt;folder&gt;: take three pictures and quit (tests).</summary>
    public static string CommandLineReplay()
    {
        var args = CommandLine.Args;
        int i = Array.IndexOf(args, "-replay");
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    string _shotDir;
    int _shot = -1;
    float _shotWait;
    int _shotFrames;

    void TakeShots()
    {
        // 25 %: behind the recording participant, 50 %: inside, 75 %: from above with trails
        if (_shot < 0)
        {
            var args = CommandLine.Args;
            int i = Array.IndexOf(args, "-replayShots");
            _shotDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            if (_shotDir == null) { _shot = int.MaxValue; return; }
            System.IO.Directory.CreateDirectory(_shotDir);
            _shot = 0; _shotWait = 1.5f;
        }
        if (_shot >= 3) return;
        if (_shotWait == 1.5f)
        {
            _shotFrames = 0;
            _playing = false;
            _time = _log.Duration * (_shot + 1) / 4f;
            _inside = _shot == 1;
            if (_shot == 2)
            {
                _follow = -1; _showTrails = true; foreach (var tr in _trails) tr.enabled = true;
                var p = Participants.FirstOrDefault(x => x != null);
                var c = p != null ? p.transform.position : Vector3.zero;
                _pitch = 50f; _yaw = 0f;
                _camera.transform.position = c + new Vector3(0, 45, -38);
            }
        }
        _shotWait -= Time.unscaledDeltaTime;
        if (_shotWait > 0f || ++_shotFrames < 4) return;   // a few frames too: batch-mode frames can be slow
        var rt = new RenderTexture(1280, 720, 24);
        _camera.targetTexture = rt;
        _camera.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        _camera.targetTexture = null;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(_shotDir, $"replay{_shot}.png"), tex.EncodeToPNG());
        Destroy(tex);
        rt.Release();
        Destroy(rt);
        Debug.Log($"Replay: picture {_shot} at {_time:F1} s");
        _shot++; _shotWait = 1.5f;
        if (_shot >= 3)
        {
            Debug.Log("Replay: pictures done");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { Exit(); return; }
        if (!_ready) return;
        TakeShots();
        if (Input.GetKeyDown(KeyCode.Space)) _playing = !_playing;
        if (Input.GetKeyDown(KeyCode.RightArrow)) _time += 5f;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) _time -= 5f;
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) Step(-1);
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus)) Step(1);
        if (Input.GetKeyDown(KeyCode.C)) _inside = !_inside;
        if (Input.GetKeyDown(KeyCode.T)) { _showTrails = !_showTrails; foreach (var t in _trails) t.enabled = _showTrails; }
        for (int k = 0; k <= 9; k++)
            if (Input.GetKeyDown(KeyCode.Alpha0 + k)) _follow = k == 0 ? -1 : (k - 1 < Participants.Count ? k - 1 : _follow);
        // (while a video is made: game time, which runs in step with its frames)
        if (_playing) _time += (VideoExport.Busy ? Time.deltaTime : Time.unscaledDeltaTime) * _speed;
        if (_video && !VideoExport.Busy)
        {
            Debug.Log("Replay: video done");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
            Application.Quit();
            _video = false;
        }
        _time = Mathf.Clamp(_time, 0f, _log.Duration);
        if (_time >= _log.Duration) _playing = false;
    }

    void Step(int d)
    {
        int i = Array.IndexOf(Speeds, _speed);
        _speed = Speeds[Mathf.Clamp((i < 0 ? 3 : i) + d, 0, Speeds.Length - 1)];
    }

    void LateUpdate()
    {
        if (!_ready) return;
        ApplyFrame();
        MoveCamera();
    }

    void ApplyFrame()
    {
        int k = _log.FrameAt(_time, out float u);
        var a = _log.Frames[k];
        var b = _log.Frames[Mathf.Min(k + 1, _log.Frames.Count - 1)];
        for (int i = 0; i < _drivers.Count; i++)
        {
            var d = _drivers[i];
            if (d == null) continue;
            bool present = i < a.DriverCount;
            if (d.gameObject.activeSelf != present) d.gameObject.SetActive(present);
            if (!present) continue;
            var track = _driverTracks[i];
            if (!track.Pose(track.LocalTime(_time), out var pos, out var rot)) { pos = a.Positions[i]; rot = a.Rotations[i]; }
            d.transform.SetPositionAndRotation(pos, rot);
            if (_steering[i] != null) _steering[i].Drive(track.Curvature(track.LocalTime(_time)), Time.deltaTime);
            if (i == 0 && _debugEye && Time.unscaledTime > _nextEyeLog)
            {
                _nextEyeLog = Time.unscaledTime + 1f;
                Renderer big = null; float size = 0f;
                foreach (var r in d.GetComponentsInChildren<MeshRenderer>()) { float s = r.bounds.size.sqrMagnitude; if (s > size) { size = s; big = r; } }
                var cam = d.cameras != null && d.cameras.Length > 0 && d.cameras[0] != null ? d.cameras[0].transform : null;
                Debug.Log($"ReplayEyeDebug t={_time:F1}: body {(big != null ? big.name + " " + d.transform.InverseTransformPoint(big.bounds.center).ToString("F2") : "-")}, camera {(cam != null ? cam.name + " " + d.transform.InverseTransformPoint(cam.position).ToString("F2") : "-")}, root {d.transform.position:F1}");
            }
            // blinkers and lights: from the participant's own log when it has one
            var lf = track.Log == _log ? a : track.Log.Frames[Mathf.Max(0, track.Log.FrameAt(track.LocalTime(_time), out _))];
            int li = track.Log == _log ? i : track.Index;
            if (li < lf.DriverCount)
            {
                if (d.CarBlinkers != null && d.CarBlinkers.State != lf.Blinkers[li]) d.CarBlinkers.SwitchToState(lf.Blinkers[li]);
                if (d.frontLights != null && d.frontLights.activeSelf != lf.FrontLights[li]) d.frontLights.SetActive(lf.FrontLights[li]);
                if (d.stopLights != null && d.stopLights.activeSelf != lf.StopLights[li]) d.stopLights.SetActive(lf.StopLights[li]);
            }
        }
        for (int i = 0; i < _pedestrians.Count; i++)
        {
            var p = _pedestrians[i];
            var track = _pedestrianTracks[i];
            if (p == null || !track.At(track.LocalTime(_time), out var fa, out var fb, out var fu)) continue;
            var pa = fa.Pedestrians[track.Index]; var pb = fb.Pedestrians[track.Index];
            if (pa.LocalPositions == null || pa.LocalPositions.Count != p.SyncTransforms.Length + 1) continue;
            if (pb.LocalPositions == null || pb.LocalPositions.Count != pa.LocalPositions.Count) { pb = pa; fu = 0f; }
            // the body between the two samples (root and every synced bone)
            p.transform.SetPositionAndRotation(Vector3.Lerp(pa.LocalPositions[0], pb.LocalPositions[0], fu), Quaternion.Slerp(pa.LocalRotations[0], pb.LocalRotations[0], fu));
            for (int j = 0; j < p.SyncTransforms.Length; j++)
            {
                var tr = p.SyncTransforms[j];
                if (tr == null) continue;
                tr.localPosition = Vector3.Lerp(pa.LocalPositions[j + 1], pb.LocalPositions[j + 1], fu);
                tr.localRotation = Quaternion.Slerp(pa.LocalRotations[j + 1], pb.LocalRotations[j + 1], fu);
            }
        }
        for (int i = 0; i < _drones.Count; i++)
        {
            var d = _drones[i];
            bool present = a.DronePositions != null && i < a.DronePositions.Length;
            if (d.t.gameObject.activeSelf != present) d.t.gameObject.SetActive(present);
            if (!present) continue;
            var pos = a.DronePositions[i]; var rot = a.DroneRotations[i];
            if (b.DronePositions != null && i < b.DronePositions.Length && (b.DronePositions[i] - pos).sqrMagnitude < 25f) { pos = Vector3.Lerp(pos, b.DronePositions[i], u); rot = Quaternion.Slerp(rot, b.DroneRotations[i], u); }
            d.t.SetPositionAndRotation(pos, rot);
            // the propellers at the logged blade-passing frequency (two blades: rotor speed = BPF / 2 rev/s)
            float revPerS = a.DroneLanded[i] ? 0f : a.DroneBladeHz[i] / 2f;
            float angle = d.angle + revPerS * 360f * Time.deltaTime;
            for (int j = 0; j < d.props.Length; j++) if (d.props[j] != null) d.props[j].localRotation = Quaternion.Euler(0, (j % 2 == 0 ? 1 : -1) * angle, 0);
            foreach (var disc in d.discs) if (disc != null) disc.enabled = revPerS > 10f;
            _drones[i] = (d.t, d.props, d.discs, angle % 360f);
        }
        if (_lights != null)
        {
            for (int i = 0; i < a.CarLights.Length && i < _lights.CarLights.Length; i++) SetLight(_lights.CarLights[i], (LightState)a.CarLights[i]);
            for (int i = 0; i < a.PedestrianLights.Length && i < _lights.PedestrianLights.Length; i++) SetLight(_lights.PedestrianLights[i], (LightState)a.PedestrianLights[i]);
        }
    }

    static void SetLight(CarTrafficLight l, LightState s)
    {
        if (l == null || l.State == s) return;
        switch (s)
        {
            case LightState.GREEN: l.TurnGreen(); break;
            case LightState.RED: l.TurnRed(); break;
            case LightState.YELLOW: l.TurnYellow(); break;
            case LightState.RED_AND_YELLOW: l.TurnRedAndYellow(); break;
        }
    }

    static void SetLight(PedestrianTrafficLight l, LightState s)
    {
        if (l == null || l.State == s) return;
        switch (s)
        {
            case LightState.GREEN: l.TurnGreen(); break;
            case LightState.RED: l.TurnRed(); break;
            case LightState.BLINK_GREEN: l.TurnBlink(); break;
        }
    }

    // the car seen from inside has its mirrors on (showing the replay behind it), every other car has them off
    PlayerAvatar _mirrorsOn;
    void MirrorsOf(PlayerAvatar car)
    {
        if (car != null && car.Type != AvatarType.Driver) car = null;
        if (car == _mirrorsOn) return;
        if (_mirrorsOn != null) RearMirrorsReflection.SetLive(_mirrorsOn.gameObject, null, false);
        _mirrorsOn = car;
        if (car != null) RearMirrorsReflection.SetLive(car.gameObject, _camera.transform, true);
    }

    void MoveCamera()
    {
        var t = _camera.transform;
        var participants = Participants;
        if (Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * 3f;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 3f, -80f, 85f);
        }
        if (_follow >= 0 && _follow < participants.Count && participants[_follow] != null)
        {
            var target = participants[_follow];
            if (_inside)
            {
                // the participant's own view: their camera (cars), in front of the face (people on foot or on two wheels)
                VideoExport.OwnView(target, out var pos, out var own);
                t.SetPositionAndRotation(pos, Quaternion.Euler(0f, own.eulerAngles.y, 0f) * Quaternion.Euler(Mathf.Clamp(_pitch - 15f, -60f, 60f), _yaw, 0f));
                MirrorsOf(target);
            }
            else
            {
                MirrorsOf(null);
                var rot = Quaternion.Euler(_pitch, target.transform.eulerAngles.y + _yaw, 0f);
                float dist = target.Type == AvatarType.Pedestrian ? 4f : 8f;
                t.SetPositionAndRotation(target.transform.position + Vector3.up * 1.2f - rot * Vector3.forward * dist, rot);
            }
        }
        else
        {
            MirrorsOf(null);
            t.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var move = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                                   (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
                                   (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            float speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 40f : 10f;
            t.position += t.rotation * move * speed * Time.unscaledDeltaTime;
        }
    }

    void OnGUI()
    {
        if (!_ready)
        {
            GUILayout.Label(_status ?? "Loading the replay…");
            if (_status != null && GUILayout.Button("Back")) Exit();
            return;
        }
        var participants = Participants;
        GUILayout.BeginArea(new Rect(10, Screen.height - 120, Screen.width - 20, 110), GUI.skin.box);
        GUILayout.Label($"Replay: {System.IO.Path.GetFileNameWithoutExtension(_path)}   {_time:F1} / {_log.Duration:F1} s   speed {_speed:0.##}×   {(_playing ? "playing" : "paused")}");
        float nt = GUILayout.HorizontalSlider(_time, 0f, _log.Duration);
        if (Mathf.Abs(nt - _time) > 1e-3f) _time = nt;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(70))) { if (!_playing && _time >= _log.Duration) _time = 0f; _playing = !_playing; }
        if (GUILayout.Button("Slower", GUILayout.Width(70))) Step(-1);
        if (GUILayout.Button("Faster", GUILayout.Width(70))) Step(1);
        if (GUILayout.Button(_showTrails ? "Hide trails" : "Trails", GUILayout.Width(90))) { _showTrails = !_showTrails; foreach (var tr in _trails) tr.enabled = _showTrails; }
        if (GUILayout.Button(_inside ? "Behind" : "Inside", GUILayout.Width(70))) _inside = !_inside;
        if (GUILayout.Button(_follow < 0 ? "[Free camera]" : "Free camera", GUILayout.Width(110))) _follow = -1;
        for (int i = 0; i < participants.Count && i < 9; i++)
        {
            var label = $"{i + 1} {(participants[i] != null && participants[i].Type == AvatarType.Pedestrian ? "Pedestrian" : "Vehicle")}{(i == _info.LocalDriver ? " (recorded here)" : "")}";
            if (GUILayout.Button(_follow == i ? $"[{label}]" : label)) _follow = i;
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Back (Esc)", GUILayout.Width(90))) Exit();
        GUILayout.EndHorizontal();
        GUILayout.Label("Space play/pause · ←/→ 5 s · -/+ speed · 1-9 follow · 0 free camera (WASD, Q/E, right mouse button) · C inside/behind · T trails");
        GUILayout.EndArea();
    }

    void Exit()
    {
        Active = null;
        OsmMap.Requested = null;
        if (NetworkingManager.Instance != null) Destroy(NetworkingManager.Instance.gameObject);
        Destroy(gameObject);
        SceneManager.LoadScene(0);
    }

    // the followed participant's own figure is hidden while the replay camera renders their own view
    List<Renderer> _hiddenFigure;
    static readonly bool _debugEye = Array.IndexOf(CommandLine.Args, "-debugEye") >= 0;
    float _nextEyeLog;
    void OnEnable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += BeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += EndCamera;
    }
    void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= EndCamera;
        EndCamera(default, _camera);
    }
    void BeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _camera || !_ready || !_inside) return;
        var people = Participants;
        if (_follow >= 0 && _follow < people.Count) _hiddenFigure = VideoExport.HideFigure(people[_follow]);
    }
    void EndCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _camera || _hiddenFigure == null) return;
        foreach (var r in _hiddenFigure) if (r != null) r.enabled = true;
        _hiddenFigure = null;
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_trailMaterial != null) Destroy(_trailMaterial);
    }
}
