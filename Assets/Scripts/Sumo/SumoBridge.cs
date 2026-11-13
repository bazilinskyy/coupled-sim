using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CoupledSim.TraCI;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Couples the simulator to Eclipse SUMO over TraCI. SUMO's vehicles appear as AI cars (synced to
/// all clients); the cars of the participants and the AV are put into SUMO so its traffic reacts to
/// them; SUMO cars stop for pedestrians in front of them. Add it to an experiment's Car Spawners
/// list (it runs on the host only).
///
/// SUMO runs on its own thread in lockstep with Unity's time (one SUMO step behind, positions are
/// interpolated), so the frame rate does not depend on SUMO's speed.
/// </summary>
public class SumoBridge : CarSpawnerBase
{
    [Header("SUMO")]
    [Tooltip("Start SUMO from here. Off: connect to a SUMO already started with --remote-port.")]
    public bool LaunchSumo = true;
    [Tooltip("SUMO configuration (.sumocfg): absolute, or relative to StreamingAssets.")]
    public string SumoConfig = "Sumo/MainScene/mainscene.sumocfg";
    [Tooltip("In a city map: use that map's scenario, StreamingAssets/Sumo/Maps/{map}/{map}.sumocfg.")]
    public bool UseMapScenario = false;
    [Tooltip("SUMO binary; empty: $SUMO_HOME/bin/sumo, else 'sumo' on the PATH. Use sumo-gui to watch SUMO's view.")]
    public string SumoBinary = "";
    public string Host = "127.0.0.1";
    public int Port = 8813;
    [Tooltip("Extra command line options for SUMO.")]
    public string SumoArguments = "--no-step-log --collision.action warn";

    [Header("Coordinates")]
    [Tooltip("Where SUMO's (0, 0) is: SUMO x runs along this transform's x (right), SUMO y along its z (forward). Empty: the world origin.")]
    public Transform NetworkOrigin;
    [Tooltip("How far above and below SUMO's elevation to look for the road under SUMO cars (m).")]
    public float GroundProbeHeight = 3f;

    [Header("Vehicles")]
    [Tooltip("AI car prefabs used for SUMO vehicles (picked by vehicle id); they must be in NetworkingManager ➡️ AI Car System ➡️ Prefabs. Empty: Spawn Params ➡️ Car.")]
    public AICar[] CarPrefabs;
    [Tooltip("Prefabs for SUMO vehicles of class bicycle (e.g. AIBicycle); empty: Car Prefabs.")]
    public AICar[] BicyclePrefabs;
    [Tooltip("Use SUMO's vehicle colours.")]
    public bool UseSumoColors = true;
    [Tooltip("Put the participants' cars and the AV into SUMO, so SUMO traffic reacts to them.")]
    public bool SendUnityCars = true;
    [Tooltip("Stop SUMO cars for pedestrians (AI and players) on the road in front of them.")]
    public bool StopForPedestrians = true;
    public float PedestrianLookAhead = 15f;

    [Header("State (read only)")]
    public string Status = "not started";
    public double SumoTime;
    public int VehicleCount;

    class Snapshot
    {
        public double Time;
        public readonly Dictionary<string, SumoVehicleState> Vehicles = new Dictionary<string, SumoVehicleState>();
    }

    struct EgoPose
    {
        public string Id;
        public string TypeId;
        public double X, Y, Angle, Speed;
    }

    class SumoCar
    {
        public AICar Car;
        public PlayerAvatar Avatar;
        public SumoVehicleState Prev, Curr;
        public float GroundY;
        public float NextGroundProbe;
        public int Signals = -1;
        public bool Stopped;
        public AICar Prefab;
        public TwoWheelerController TwoWheeler;
    }

    readonly HashSet<string> _warned = new HashSet<string>();
    Thread _thread;
    volatile bool _running;
    Process _process;
    readonly object _lock = new object();
    Snapshot _prevSnapshot, _currSnapshot;
    double _deltaT = 0.05;
    double _targetTime;
    readonly List<EgoPose> _egoPoses = new List<EgoPose>();
    readonly HashSet<string> _stopRequests = new HashSet<string>();
    readonly HashSet<string> _stoppedInSumo = new HashSet<string>();
    readonly Dictionary<string, SumoCar> _cars = new Dictionary<string, SumoCar>();
    readonly Dictionary<AICar, Stack<SumoCar>> _pool = new Dictionary<AICar, Stack<SumoCar>>();
    readonly Dictionary<PlayerAvatar, string> _egoIds = new Dictionary<PlayerAvatar, string>();
    float _startTime;
    string _error;

    protected override bool HasValidSpawnParams => !string.IsNullOrEmpty(SumoConfig) || !LaunchSumo;

    protected override IEnumerator SpawnCoroutine()
    {
        yield return null;
        // -sumoConnect host:port on the command line: use a SUMO that is already running (e.g. on another PC)
        var args = Environment.GetCommandLineArgs();
        int ci = Array.IndexOf(args, "-sumoConnect");
        if (ci >= 0 && ci + 1 < args.Length)
        {
            var parts = args[ci + 1].Split(':');
            Host = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var port)) Port = port;
            LaunchSumo = false;
        }
        // a map built from a road network: the network is centred on the scene origin
        if (OsmMap.Current != null && OsmMap.Current.RoadNetwork != null && NetworkOrigin == null)
        {
            var origin = new GameObject("SUMO network origin").transform;
            origin.position = new Vector3(-OsmMap.Current.RoadNetworkOffset.x, 0f, -OsmMap.Current.RoadNetworkOffset.y);
            NetworkOrigin = origin;
        }
        if (LaunchSumo && !StartSumo()) yield break;
        _running = true;
        _startTime = Time.time;
        _thread = new Thread(Worker) { IsBackground = true, Name = "SUMO TraCI" };
        _thread.Start();
    }

    bool StartSumo()
    {
        if (UseMapScenario && OsmMap.Current != null)
        {
            var name = OsmMap.Current.Info.Name;
            SumoConfig = OsmMap.Current.RoadNetwork != null ? BuildNetworkScenario(OsmMap.Current.RoadNetwork)
                : name == "Location" ? BuildLocationScenario(OsmMap.Current.Info) : $"Sumo/Maps/{name}/{name}.sumocfg";
            if (SumoConfig == null) return false;
        }
        var cfg = Path.IsPathRooted(SumoConfig) ? SumoConfig : Path.Combine(Application.streamingAssetsPath, SumoConfig);
        if (!File.Exists(cfg))
        {
            Debug.LogError($"SumoBridge: SUMO configuration not found: {cfg}", this);
            Status = "configuration not found";
            return false;
        }
        string binary = SumoBinary;
        if (string.IsNullOrEmpty(binary))
        {
            var home = Environment.GetEnvironmentVariable("SUMO_HOME");
            var exe = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor ? "sumo.exe" : "sumo";
            binary = !string.IsNullOrEmpty(home) && File.Exists(Path.Combine(home, "bin", exe)) ? Path.Combine(home, "bin", exe) : "sumo";
        }
        try
        {
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = binary,
                Arguments = $"-c \"{cfg}\" --remote-port {Port} {SumoArguments}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cfg),
            });
            Status = "SUMO started";
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"SumoBridge: could not start SUMO ('{binary}'): {e.Message}. Install SUMO and set SUMO_HOME, or set Sumo Binary.", this);
            Status = "could not start SUMO";
            return false;
        }
    }

    /// <summary>
    /// SUMO scenario for a map chosen by coordinates: network (netconvert, in the scene's projection)
    /// and random traffic (randomTrips), built once with the local SUMO and cached.
    /// </summary>
    string BuildLocationScenario(MapInfo info)
    {
        var home = Environment.GetEnvironmentVariable("SUMO_HOME");
        if (string.IsNullOrEmpty(home))
        {
            Debug.LogWarning("SumoBridge: no SUMO traffic for this location: install SUMO and set SUMO_HOME");
            return null;
        }
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var key = string.Format(ci, "{0:F5}_{1:F5}", info.Latitude, info.Longitude);
        var dir = Path.Combine(Application.persistentDataPath, "Sumo", key);
        var cfg = Path.Combine(dir, "location.sumocfg");
        if (File.Exists(cfg)) return cfg;
        Directory.CreateDirectory(dir);
        var osm = Directory.GetFiles(Path.Combine(Application.persistentDataPath, "Maps"), key + "_*.osm.gz");
        if (osm.Length == 0) return null;
        bool win = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
        string netconvert = Path.Combine(home, "bin", win ? "netconvert.exe" : "netconvert");
        string proj = string.Format(ci, "+proj=tmerc +lat_0={0} +lon_0={1} +k=1 +x_0=0 +y_0=0 +ellps=WGS84 +units=m +no_defs", info.Latitude, info.Longitude);
        bool Run(string exe, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(exe, args) { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true });
                p.WaitForExit(180000);
                return p.ExitCode == 0;
            }
            catch (Exception e) { Debug.LogWarning($"SumoBridge: {Path.GetFileName(exe)} failed: {e.Message}"); return false; }
        }
        if (!Run(netconvert, $"--osm-files \"{osm[0]}\" -o location.net.xml --proj \"{proj}\" --offset.disable-normalization --geometry.remove --roundabouts.guess --junctions.join --tls.guess-signals --keep-edges.by-vclass passenger,bicycle --remove-edges.isolated"))
            return null;
        var python = win ? "python" : "python3";
        Run(python, $"\"{Path.Combine(home, "tools", "randomTrips.py")}\" -n location.net.xml -r location.rou.xml -e 3600 -p 2.0 --fringe-factor 5 --validate --seed 1");
        File.WriteAllText(cfg, "<configuration><input><net-file value=\"location.net.xml\"/><route-files value=\"location.rou.xml\"/></input>" +
                               "<time><step-length value=\"0.05\"/></time><processing><ignore-route-errors value=\"true\"/><time-to-teleport value=\"120\"/></processing></configuration>");
        return cfg;
    }

    /// <summary>
    /// SUMO scenario for a map from a road network (OpenDRIVE or SUMO file, #40): that network with
    /// random traffic (randomTrips: cars, and bicycles where there are cycle lanes), built once and cached.
    /// </summary>
    public static string BuildNetworkScenario(string netFile)
    {
        var home = Environment.GetEnvironmentVariable("SUMO_HOME");
        if (string.IsNullOrEmpty(home))
        {
            Debug.LogWarning("SumoBridge: no SUMO traffic for this road network: install SUMO and set SUMO_HOME");
            return null;
        }
        var stamp = (File.GetLastWriteTimeUtc(netFile).Ticks ^ new FileInfo(netFile).Length) & 0xffffffff;
        var dir = Path.Combine(Application.persistentDataPath, "Sumo", $"{Path.GetFileName(netFile).Split('.')[0]}-{stamp:x8}");
        var cfg = Path.Combine(dir, "network.sumocfg");
        if (File.Exists(cfg)) return cfg;
        Directory.CreateDirectory(dir);
        bool win = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
        var python = win ? "python" : "python3";
        var tool = Path.Combine(home, "tools", "randomTrips.py");
        bool Run(string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(python, $"\"{tool}\" {args}") { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true });
                p.WaitForExit(180000);
                return p.ExitCode == 0;
            }
            catch (Exception e) { Debug.LogWarning($"SumoBridge: randomTrips failed: {e.Message}"); return false; }
        }
        if (!Run($"-n \"{netFile}\" -r cars.rou.xml -o cars.trips.xml -e 3600 -p 2.0 --fringe-factor 5 --validate --seed 1 --vehicle-class passenger --prefix car"))
        {
            Debug.LogWarning("SumoBridge: randomTrips could not make traffic for " + netFile);
            return null;
        }
        bool bikes = Run($"-n \"{netFile}\" -r bikes.rou.xml -o bikes.trips.xml -e 3600 -p 6.0 --validate --seed 2 --vehicle-class bicycle --prefix bike") && File.Exists(Path.Combine(dir, "bikes.rou.xml"));
        File.WriteAllText(cfg, $"<configuration><input><net-file value=\"{netFile}\"/><route-files value=\"cars.rou.xml{(bikes ? ",bikes.rou.xml" : "")}\"/></input>" +
                               "<time><step-length value=\"0.05\"/></time><processing><ignore-route-errors value=\"true\"/><time-to-teleport value=\"120\"/></processing></configuration>");
        return cfg;
    }

    // ------------------------------------------------------------------ SUMO thread

    void Worker()
    {
        var client = new TraCIClient();
        try
        {
            client.Connect(Host, Port, timeoutMs: 20000);
            _deltaT = client.GetDeltaT();
            client.SubscribeSimulation();
            Status = $"connected to {client.SumoVersion}";
            var addedEgos = new HashSet<string>();
            var egos = new List<EgoPose>();
            var stop = new List<string>();
            while (_running)
            {
                double target;
                lock (_lock) target = _targetTime;
                if (client.SimTime + _deltaT > target + 1e-9)
                {
                    Thread.Sleep(1);
                    continue;
                }
                lock (_lock)
                {
                    egos.Clear();
                    egos.AddRange(_egoPoses);
                    stop.Clear();
                    stop.AddRange(_stopRequests);
                }
                foreach (var e in egos)
                {
                    try
                    {
                        if (addedEgos.Add(e.Id)) client.AddExternalVehicle(e.Id, e.TypeId);
                        client.MoveToXY(e.Id, e.X, e.Y, e.Angle);
                        client.SetSpeed(e.Id, e.Speed);
                    }
                    catch (TraCIException ex)
                    {
                        // e.g. the participant is off the road network: SUMO traffic just does not see them
                        if (_warned.Add(e.Id)) Debug.LogWarning($"SumoBridge: {e.Id} not placed in SUMO: {ex.Message}");
                    }
                }
                foreach (var id in stop)
                {
                    if (client.Vehicles.ContainsKey(id) && _stoppedInSumo.Add(id)) client.SetSpeed(id, 0);
                }
                foreach (var id in new List<string>(_stoppedInSumo))
                {
                    if (!stop.Contains(id))
                    {
                        if (client.Vehicles.ContainsKey(id)) client.SetSpeed(id, -1); // back to SUMO's own speed
                        _stoppedInSumo.Remove(id);
                    }
                }
                client.SimulationStep();
                var snap = new Snapshot { Time = client.SimTime };
                foreach (var kv in client.Vehicles) snap.Vehicles[kv.Key] = Copy(kv.Value);
                lock (_lock)
                {
                    _prevSnapshot = _currSnapshot;
                    _currSnapshot = snap;
                }
            }
            client.Close();
        }
        catch (Exception e)
        {
            _error = e.Message;
            Status = "error: " + e.Message;
            client.Dispose();
        }
    }

    static SumoVehicleState Copy(SumoVehicleState v) => new SumoVehicleState
    {
        Id = v.Id, Position = v.Position, Angle = v.Angle, Speed = v.Speed, Length = v.Length, Width = v.Width,
        Slope = v.Slope, TypeId = v.TypeId, VehicleClass = v.VehicleClass, Signals = v.Signals, Color = v.Color, Time = v.Time,
    };

    // ------------------------------------------------------------------ Unity side

    void FixedUpdate()
    {
        if (!_running) return;
        if (_error != null)
        {
            Debug.LogError("SumoBridge: " + _error, this);
            _error = null;
            _running = false;
            return;
        }
        var egos = SendUnityCars ? CollectEgoPoses() : null;
        lock (_lock)
        {
            _targetTime = Time.time - _startTime;
            _egoPoses.Clear();
            if (egos != null) _egoPoses.AddRange(egos);
        }
    }

    void Update()
    {
        if (!_running && _cars.Count == 0) return;
        Snapshot prev, curr;
        lock (_lock)
        {
            prev = _prevSnapshot;
            curr = _currSnapshot;
        }
        if (curr == null) return;
        SumoTime = curr.Time;
        VehicleCount = curr.Vehicles.Count;

        // render one step behind SUMO and interpolate between the last two steps
        double renderTime = (Time.time - _startTime) - _deltaT;
        float alpha = prev == null ? 1f : Mathf.Clamp01((float)((renderTime - prev.Time) / Math.Max(1e-6, curr.Time - prev.Time)));

        foreach (var kv in curr.Vehicles)
        {
            if (!_cars.TryGetValue(kv.Key, out var car))
            {
                car = Acquire(kv.Value);
                if (car == null) continue;
                _cars[kv.Key] = car;
            }
            car.Prev = prev != null && prev.Vehicles.TryGetValue(kv.Key, out var p) ? p : kv.Value;
            car.Curr = kv.Value;
            Place(car, alpha);
        }
        // vehicles that left SUMO
        List<string> gone = null;
        foreach (var id in _cars.Keys)
            if (!curr.Vehicles.ContainsKey(id)) (gone ??= new List<string>()).Add(id);
        if (gone != null)
            foreach (var id in gone) Release(id);

        if (StopForPedestrians) UpdatePedestrianStops();
    }

    Vector3 SumoToUnity(double x, double y)
    {
        var local = new Vector3((float)x, 0f, (float)y);
        return NetworkOrigin != null ? NetworkOrigin.TransformPoint(local) : local;
    }

    float SumoAngleToYaw(double angle) => (float)angle + (NetworkOrigin != null ? NetworkOrigin.eulerAngles.y : 0f);

    void UnityToSumo(Vector3 p, float yaw, out double x, out double y, out double angle)
    {
        var local = NetworkOrigin != null ? NetworkOrigin.InverseTransformPoint(p) : p;
        x = local.x;
        y = local.z;
        angle = Mathf.Repeat(yaw - (NetworkOrigin != null ? NetworkOrigin.eulerAngles.y : 0f), 360f);
    }

    void Place(SumoCar car, float alpha)
    {
        var a = car.Prev;
        var b = car.Curr;
        float yawA = SumoAngleToYaw(a.Angle), yawB = SumoAngleToYaw(b.Angle);
        float yaw = Mathf.LerpAngle(yawA, yawB, alpha);
        var front = Vector3.Lerp(SumoToUnity(a.Position.X, a.Position.Y), SumoToUnity(b.Position.X, b.Position.Y), alpha);
        var rot = Quaternion.Euler(0, yaw, 0);
        // SUMO's position is the front bumper; Unity cars are placed by their centre
        var pos = front - rot * Vector3.forward * ((float)b.Length * 0.5f);
        if (Time.time >= car.NextGroundProbe)
        {
            car.NextGroundProbe = Time.time + 0.25f;
            car.GroundY = ProbeGround(pos, (float)b.Position.Z + (NetworkOrigin != null ? NetworkOrigin.position.y : 0f), car.GroundY);
        }
        pos.y = car.GroundY;
        var t = car.Car.transform;
        t.SetPositionAndRotation(pos, rot);
        if (car.TwoWheeler != null)
        {
            float yawRate = Mathf.DeltaAngle(yawA, yawB) * Mathf.Deg2Rad / (float)Math.Max(1e-3, _deltaT);
            car.TwoWheeler.State.Speed = (float)b.Speed;
            car.TwoWheeler.State.Lean = Mathf.Atan((float)b.Speed * yawRate / 9.81f);
        }

        // signals: bit 0 right blinker, bit 1 left blinker, bit 3 brake light
        if (b.Signals != car.Signals)
        {
            car.Signals = b.Signals;
            var blinkers = car.Avatar.CarBlinkers;
            if (blinkers != null)
            {
                var state = (b.Signals & 1) != 0 ? BlinkerState.Right : (b.Signals & 2) != 0 ? BlinkerState.Left : BlinkerState.None;
                blinkers.SwitchToState(state);
            }
            if (car.Avatar.stopLights != null) car.Avatar.SetBreakLights((b.Signals & 8) != 0);
        }
    }

    readonly RaycastHit[] _hits = new RaycastHit[16];

    // The road surface near SUMO's own elevation: the hit closest to it, ignoring vehicles and
    // anything far above (trees, lamp posts, gantries, bridges).
    float ProbeGround(Vector3 pos, float sumoHeight, float fallback)
    {
        int n = Physics.RaycastNonAlloc(new Vector3(pos.x, sumoHeight + GroundProbeHeight, pos.z), Vector3.down, _hits, GroundProbeHeight * 2f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NaN, bestErr = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (_hits[i].rigidbody != null) continue; // a car
            float err = Mathf.Abs(_hits[i].point.y - sumoHeight);
            if (err < bestErr && err < 3f) { bestErr = err; best = _hits[i].point.y; }
        }
        return float.IsNaN(best) ? (fallback != 0f ? fallback : sumoHeight) : best;
    }

    AICar PrefabFor(string id, string vClass)
    {
        var list = vClass == "bicycle" && BicyclePrefabs != null && BicyclePrefabs.Length > 0 ? BicyclePrefabs : CarPrefabs;
        return list != null && list.Length > 0 ? list[(id.GetHashCode() & 0x7fffffff) % list.Length] : spawnParams.Car;
    }

    SumoCar Acquire(SumoVehicleState v)
    {
        var prefab = PrefabFor(v.Id, v.VehicleClass);
        if (prefab == null) return null;
        var color = UseSumoColors ? new Color(v.Color.R / 255f, v.Color.G / 255f, v.Color.B / 255f) : spawnParams.color;
        var pos = SumoToUnity(v.Position.X, v.Position.Y);
        var rot = Quaternion.Euler(0, SumoAngleToYaw(v.Angle), 0);
        if (_pool.TryGetValue(prefab, out var stack) && stack.Count > 0)
        {
            var reused = stack.Pop();
            reused.Car.transform.SetPositionAndRotation(pos, rot);
            _syncSystem.SetCarActive(reused.Avatar, true);
            reused.Signals = -1;
            reused.NextGroundProbe = 0;
            return reused;
        }
        var car = _syncSystem.SpawnExternal(prefab, pos, rot, PlayerSystem.VehicleType.MDV, color);
        car.name = "SUMO " + v.Id;
        var sc = new SumoCar { Car = car, Avatar = car.GetComponent<PlayerAvatar>(), GroundY = pos.y, Prefab = prefab };
        if (car.TryGetComponent<TwoWheelerController>(out var tw))
        {
            // animation only: wheels, lean (no physics, no input)
            tw.ReadInput = false;
            tw.enabled = true;
            sc.TwoWheeler = tw;
        }
        return sc;
    }

    void Release(string id)
    {
        var car = _cars[id];
        _cars.Remove(id);
        if (car.Car == null) return;
        _syncSystem.SetCarActive(car.Avatar, false);
        var prefab = car.Prefab;
        if (!_pool.TryGetValue(prefab, out var stack)) _pool[prefab] = stack = new Stack<SumoCar>();
        stack.Push(car);
    }

    readonly List<EgoPose> _egoBuffer = new List<EgoPose>();

    List<EgoPose> CollectEgoPoses()
    {
        _egoBuffer.Clear();
        foreach (var avatar in FindObjectsByType<PlayerAvatar>())
        {
            if (avatar.Type != AvatarType.Driver || !avatar.gameObject.activeInHierarchy) continue;
            if (IsSumoCar(avatar)) continue;
            if (!_egoIds.TryGetValue(avatar, out var id))
            {
                id = "unity_" + _egoIds.Count;
                _egoIds[avatar] = id;
            }
            var t = avatar.transform;
            var front = t.position + t.forward * 2.2f;
            UnityToSumo(front, t.eulerAngles.y, out var x, out var y, out var angle);
            float speed = avatar.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic ? rb.linearVelocity.magnitude : 0f;
            if (avatar.TryGetComponent<TrafficCar>(out var tc) && !tc.SafetyOnly) speed = tc.Speed;
            // riders are bicycles/mopeds in SUMO (its default types), so SUMO drivers treat them as such
            var typeId = "DEFAULT_VEHTYPE";
            if (avatar.TryGetComponent<TwoWheelerController>(out var tw))
            {
                speed = tw.Speed;
                typeId = tw.Type == CoupledSim.TwoWheelers.TwoWheelerType.Bicycle || tw.Type == CoupledSim.TwoWheelers.TwoWheelerType.EBike
                    || tw.Type == CoupledSim.TwoWheelers.TwoWheelerType.EScooter ? "DEFAULT_BIKETYPE" : "DEFAULT_VEHTYPE";
            }
            _egoBuffer.Add(new EgoPose { Id = id, TypeId = typeId, X = x, Y = y, Angle = angle, Speed = speed });
        }
        return _egoBuffer;
    }

    bool IsSumoCar(PlayerAvatar avatar)
    {
        foreach (var c in _cars.Values) if (c.Avatar == avatar) return true;
        foreach (var s in _pool.Values) foreach (var c in s) if (c.Avatar == avatar) return true;
        return false;
    }

    readonly List<Transform> _pedestrians = new List<Transform>();
    float _nextPedestrianScan;

    void UpdatePedestrianStops()
    {
        if (Time.time >= _nextPedestrianScan)
        {
            _nextPedestrianScan = Time.time + 1f;
            _pedestrians.Clear();
            foreach (var a in FindObjectsByType<PlayerAvatar>())
                if (a.Type != AvatarType.Driver) _pedestrians.Add(a.transform);
        }
        var stop = new HashSet<string>();
        foreach (var kv in _cars)
        {
            var t = kv.Value.Car.transform;
            foreach (var animal in AnimalAgent.All)
            {
                if (animal.Current == AnimalAgent.State.Fly) continue;
                var d = t.InverseTransformPoint(animal.transform.position);
                if (d.z > 1.5f && d.z < PedestrianLookAhead && Mathf.Abs(d.x) < 1.6f) { stop.Add(kv.Key); break; }
            }
            foreach (var p in _pedestrians)
            {
                if (p == null) continue;
                var d = t.InverseTransformPoint(p.position);
                if (d.z > 1.5f && d.z < PedestrianLookAhead && Mathf.Abs(d.x) < 1.6f && Mathf.Abs(d.y) < 2f)
                {
                    stop.Add(kv.Key);
                    break;
                }
            }
        }
        lock (_lock)
        {
            _stopRequests.Clear();
            _stopRequests.UnionWith(stop);
        }
    }

    void OnDestroy()
    {
        _running = false;
        _thread?.Join(2000);
        try
        {
            if (_process != null && !_process.HasExited) _process.Kill();
        }
        catch (Exception) { }
    }
}
