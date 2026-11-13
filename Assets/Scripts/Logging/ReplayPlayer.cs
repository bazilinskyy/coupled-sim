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
    readonly List<LineRenderer> _trails = new List<LineRenderer>();
    TrafficLightsSystem _lights;
    Camera _camera;
    bool _ready, _playing = true, _inside, _showTrails;
    float _time, _speed = 1f, _yaw, _pitch = 15f;
    int _follow = -1;   // index into Participants; -1 = free camera
    string _status;

    static readonly float[] Speeds = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f, 8f };

    List<PlayerAvatar> Participants => _drivers.Take(_info.Drivers.Length).Concat(_pedestrians).ToList();

    /// <summary>Starts the replay of a recording (a .binLog with its .replay.json).</summary>
    public static void Begin(string binLog, NetworkingManager nm)
    {
        var info = ReplayInfo.Load(binLog);
        if (info == null) { Debug.LogError("Replay: no " + ReplayInfo.PathFor(binLog)); return; }
        ReplayLog log;
        try { log = ReplayLog.Read(binLog); }
        catch (Exception e) { Debug.LogError($"Replay: cannot read {binLog}: {e.Message}"); return; }
        if (log.Frames.Count == 0) { Debug.LogError("Replay: the recording has no frames: " + binLog); return; }

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
        Active = r;
        Debug.Log($"Replay: {binLog}: {log.Frames.Count} frames, {log.Duration:F1} s, experiment {info.Experiment}, scene {info.Scene}{(info.Map != null ? ", map " + info.Map : "")}");

        SceneManager.sceneLoaded += r.OnSceneLoaded;
        OsmMap.Requested = info.Map;
        SceneManager.LoadScene(info.Scene);
    }

    ExperimentDefinition _experiment;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        try { Setup(); }
        catch (Exception e) { Debug.LogError("Replay: " + e); _status = "Replay failed: " + e.Message; }
    }

    // MonoBehaviours of the experiment kept running in the replay: the world's look; everything that spawns or drives is off
    static readonly HashSet<string> KeepInExperiment = new HashSet<string> { "EnvironmentController", "CarTrafficLight", "PedestrianTrafficLight", "TrafficSign" };
    // MonoBehaviours kept on the replayed avatars
    static readonly HashSet<string> KeepOnAvatar = new HashSet<string> { "PlayerAvatar", "CarBlinkers", "SteeringWheel", "ObservedSteering", "WheelRotator" };

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
        var participants = Participants;
        _follow = _info.LocalDriver >= 0 && _info.LocalDriver < _info.Drivers.Length ? _info.LocalDriver : (participants.Count > 0 ? 0 : -1);
        if (_follow < 0 && _drivers.Count > 0) _camera.transform.position = _log.Frames[0].Positions[0] + new Vector3(0, 30, -30);
        BuildTrails();
        _ready = true;
        ApplyFrame();
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
        var mat = new Material(Shader.Find("Sprites/Default"));
        int nDrivers = _info.Drivers.Length;
        for (int p = 0; p < nDrivers + _pedestrians.Count; p++)
        {
            var points = new List<Vector3>();
            Vector3 last = new Vector3(float.MaxValue, 0, 0);
            foreach (var f in _log.Frames)
            {
                Vector3 q;
                if (p < nDrivers) { if (p >= f.DriverCount) continue; q = f.Positions[p]; }
                else { var pose = f.Pedestrians[p - nDrivers]; if (pose.LocalPositions == null || pose.LocalPositions.Count == 0) continue; q = pose.LocalPositions[0]; }
                if ((q - last).sqrMagnitude < 0.25f) continue;
                points.Add(q + Vector3.up * 0.15f);
                last = q;
            }
            var lr = new GameObject("Trail " + p).AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.material = mat;
            lr.startColor = lr.endColor = colors[p % colors.Length];
            lr.widthMultiplier = 0.25f;
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            lr.enabled = _showTrails;
            _trails.Add(lr);
        }
    }

    /// <summary>-replay &lt;binLog&gt;: start this replay at once (from NetworkingManager). With -replayShots &lt;folder&gt;: take three pictures and quit (tests).</summary>
    public static string CommandLineReplay()
    {
        var args = Environment.GetCommandLineArgs();
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
            var args = Environment.GetCommandLineArgs();
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
        if (_playing) _time += Time.unscaledDeltaTime * _speed;
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
            var pos = a.Positions[i]; var rot = a.Rotations[i];
            if (i < b.DriverCount && (b.Positions[i] - pos).sqrMagnitude < 25f) { pos = Vector3.Lerp(pos, b.Positions[i], u); rot = Quaternion.Slerp(rot, b.Rotations[i], u); }
            d.transform.SetPositionAndRotation(pos, rot);
            if (d.CarBlinkers != null && d.CarBlinkers.State != a.Blinkers[i]) d.CarBlinkers.SwitchToState(a.Blinkers[i]);
            if (d.frontLights != null && d.frontLights.activeSelf != a.FrontLights[i]) d.frontLights.SetActive(a.FrontLights[i]);
            if (d.stopLights != null && d.stopLights.activeSelf != a.StopLights[i]) d.stopLights.SetActive(a.StopLights[i]);
        }
        for (int i = 0; i < _pedestrians.Count; i++)
        {
            var p = _pedestrians[i];
            var pose = a.Pedestrians[i];
            if (p == null || pose.LocalPositions == null || pose.LocalPositions.Count != p.SyncTransforms.Length + 1) continue;
            p.ApplyPose(pose);
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
                // the participant's own view: their camera (cars) or the head (pedestrians)
                var eye = target.cameras != null && target.cameras.Length > 0 && target.cameras[0] != null ? target.cameras[0].transform : null;
                var an = target.GetComponentInChildren<Animator>();
                var head = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
                var pos = eye != null ? eye.position : head != null ? head.position + target.transform.forward * 0.1f : target.transform.position + Vector3.up * 1.6f;
                t.SetPositionAndRotation(pos, target.transform.rotation * Quaternion.Euler(Mathf.Clamp(_pitch - 15f, -60f, 60f), _yaw, 0f));
            }
            else
            {
                var rot = Quaternion.Euler(_pitch, target.transform.eulerAngles.y + _yaw, 0f);
                float dist = target.Type == AvatarType.Pedestrian ? 4f : 8f;
                t.SetPositionAndRotation(target.transform.position + Vector3.up * 1.2f - rot * Vector3.forward * dist, rot);
            }
        }
        else
        {
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

    void OnDestroy()
    {
        if (Active == this) Active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
