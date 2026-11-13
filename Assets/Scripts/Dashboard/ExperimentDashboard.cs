using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Experimenter dashboard: a web page served by the host (http://&lt;host ip&gt;:8080 by default) to run
/// an experiment from any computer, tablet or phone on the lab network, and to watch it:
/// - start the host, choose the experiment, bind roles to the connected participants, set the
///   display/control mode, initialise and start the simulation, go to the next trial;
/// - switch eHMIs, set weather and time of day, trigger visual syncing;
/// - a live top-down map (rendered from the scene) with every car, AV, two-wheeler and pedestrian,
///   and the state of traffic lights, participants and logs.
/// It uses a plain TCP server (no admin rights or URL reservations needed on Windows).
/// Put it on the Managers object in StartScene; the port can be changed with -dashboardPort N.
/// </summary>
public class ExperimentDashboard : MonoBehaviour
{
    public int Port = 8080;
    [Tooltip("Accept connections from other computers (otherwise only from this one).")]
    public bool AllowRemote = true;
    [Tooltip("Seconds between state updates.")]
    public float UpdateInterval = 0.2f;
    [Tooltip("Resolution of the top-down map image.")]
    public int MapResolution = 1024;

    TcpListener _listener;
    Thread _thread;
    volatile bool _running;
    readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
    readonly object _lock = new object();
    string _stateJson = "{}";
    byte[] _mapPng;
    string _mapJson = "null";
    float _nextUpdate;
    string _mapScene;

    public static ExperimentDashboard Instance { get; private set; }
    public string Url { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-dashboardPort");
        if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var port)) Port = port;
    }

    void Start()
    {
        try
        {
            _listener = new TcpListener(AllowRemote ? IPAddress.Any : IPAddress.Loopback, Port);
            _listener.Start();
            _running = true;
            _thread = new Thread(Serve) { IsBackground = true, Name = "Experiment dashboard" };
            _thread.Start();
            Url = $"http://{LocalAddress()}:{Port}";
            Debug.Log($"Experiment dashboard: {Url}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Experiment dashboard could not listen on port {Port}: {e.Message}");
        }
    }

    static string LocalAddress()
    {
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) return ip.ToString();
        }
        catch (Exception) { }
        return "localhost";
    }

    void OnDestroy()
    {
        _running = false;
        try { _listener?.Stop(); } catch (Exception) { }
    }

    void Update()
    {
        while (_mainThread.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception e) { Debug.LogWarning("Dashboard command failed: " + e.Message); }
        }
        if (Time.unscaledTime >= _nextUpdate)
        {
            _nextUpdate = Time.unscaledTime + UpdateInterval;
            var json = BuildState();
            lock (_lock) _stateJson = json;
        }
        var scene = SceneManager.GetActiveScene().name;
        if (scene != _mapScene && IsInGame())
        {
            _mapScene = scene;
            RenderMap();
        }
    }

    // ------------------------------------------------------------------ state

    static Host CurrentHost => NetworkingManager.Instance != null ? NetworkingManager.Instance.NetSystem as Host : null;

    static bool IsInGame() => CurrentHost != null && CurrentHost.InGame;

    static string F(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "0" : v.ToString("0.###", CultureInfo.InvariantCulture);
    static string S(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    string BuildState()
    {
        var nm = NetworkingManager.Instance;
        var sb = new StringBuilder(4096);
        sb.Append('{');
        var host = CurrentHost;
        sb.Append("\"mode\":").Append(S(nm == null ? "none" : nm.NetSystem == null ? "menu" : host != null ? "host" : "client")).Append(',');
        sb.Append("\"time\":").Append(F(Time.time)).Append(',');
        sb.Append("\"fps\":").Append(F(1f / Mathf.Max(1e-4f, Time.smoothDeltaTime))).Append(',');
        sb.Append("\"scene\":").Append(S(SceneManager.GetActiveScene().name)).Append(',');
        if (nm != null)
        {
            sb.Append("\"experiments\":[");
            for (int i = 0; i < nm.Experiments.Length; i++) sb.Append(i > 0 ? "," : "").Append(S(nm.Experiments[i] != null ? nm.Experiments[i].Name : "?"));
            sb.Append("],");
            sb.Append("\"trial\":{\"index\":").Append(nm.CurrentTrial).Append(",\"count\":").Append(nm.trials != null ? nm.trials.Length : 0)
              .Append(",\"automatic\":").Append(nm.RunTrialSequenceAutomatically ? "true" : "false")
              .Append(",\"participant\":").Append(NetworkingManager.SessionParticipant).Append("},");
            sb.Append("\"inputMode\":").Append(S(nm.PlayerSystem != null ? nm.PlayerSystem.PlayerInputMode.ToString() : "")).Append(',');
        }
        if (host != null)
        {
            sb.Append("\"state\":").Append(S(host.StateName)).Append(',');
            sb.Append("\"selectedExperiment\":").Append(host.SelectedExperiment).Append(',');
            if (host.IsMapExperiment)
            {
                sb.Append("\"maps\":[");
                for (int i = 0; i < MapCatalog.Maps.Length; i++) sb.Append(i > 0 ? "," : "").Append(S(MapCatalog.Maps[i].City + ", " + MapCatalog.Maps[i].Country));
                sb.Append("],\"currentMap\":").Append(S(host.CurrentMap)).Append(",\"currentMapIndex\":").Append(MapCatalog.IndexOf(host.CurrentMap)).Append(',');
            }
            var exp = nm.Experiments[Mathf.Clamp(host.SelectedExperiment, 0, nm.Experiments.Length - 1)];
            sb.Append("\"roles\":[");
            for (int i = 0; i < exp.Roles.Length; i++) sb.Append(i > 0 ? "," : "").Append(S(exp.Roles[i].Name));
            sb.Append("],\"players\":[");
            for (int p = 0; p < UNetConfig.MaxPlayers; p++)
            {
                if (p > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(p).Append(",\"connected\":").Append(p == 0 || host.IsPlayerConnected(p) ? "true" : "false")
                  .Append(",\"role\":").Append(host.GetRole(p)).Append(",\"ready\":").Append(host.IsReady(p) ? "true" : "false").Append('}');
            }
            sb.Append("],");
            sb.Append("\"hmis\":[");
            var hmi = FindAnyObjectByType<HMIManager>();
            if (hmi != null) for (int i = 0; i < hmi.Count; i++) sb.Append(i > 0 ? "," : "").Append(S(hmi.GetState(i).ToString()));
            sb.Append("],");
        }
        var env = EnvironmentController.Instance;
        if (env != null)
            sb.Append("\"environment\":{\"time\":").Append(F(env.TimeOfDay)).Append(",\"weather\":").Append(S(env.Weather.ToString())).Append("},");
        // agents
        sb.Append("\"agents\":[");
        bool first = true;
        foreach (var a in FindObjectsByType<PlayerAvatar>())
        {
            if (!a.gameObject.activeInHierarchy) continue;
            var t = a.transform;
            string kind = a.Type == AvatarType.Pedestrian ? (a.GetComponent<AIPedestrian>() != null && a.GetComponent<AIPedestrian>().enabled ? "ai-pedestrian" : "pedestrian")
                : a.GetComponent<TwoWheelerController>() != null ? "two-wheeler"
                : a.GetComponent<AICar>() != null && a.GetComponent<AICar>().enabled ? (a.name.StartsWith("SUMO") ? "sumo-car" : "ai-car") : "car";
            bool player = nm != null && nm.PlayerSystem != null && nm.PlayerSystem.Avatars.Contains(a);
            if (player && kind == "ai-car") kind = "av";
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"name\":").Append(S(a.name)).Append(",\"kind\":").Append(S(kind)).Append(",\"player\":").Append(player ? "true" : "false")
              .Append(",\"x\":").Append(F(t.position.x)).Append(",\"z\":").Append(F(t.position.z)).Append(",\"yaw\":").Append(F(t.eulerAngles.y)).Append('}');
        }
        sb.Append("],");
        // traffic lights
        sb.Append("\"lights\":[");
        first = true;
        foreach (var l in FindObjectsByType<CarTrafficLight>())
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"x\":").Append(F(l.transform.position.x)).Append(",\"z\":").Append(F(l.transform.position.z)).Append(",\"state\":").Append(S(l.State.ToString())).Append('}');
        }
        sb.Append("],");
        sb.Append("\"map\":").Append(_mapJson);
        sb.Append('}');
        return sb.ToString();
    }

    // Renders the scene from above once per scene, as the background of the live map.
    void RenderMap()
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (var r in FindObjectsByType<MeshRenderer>())
        {
            if (r.bounds.size.magnitude > 2000f) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        if (!any) return;
        var go = new GameObject("Dashboard map camera");
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            float half = Mathf.Max(bounds.extents.x, bounds.extents.z);
            cam.orthographicSize = half;
            cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + 50f, bounds.center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = bounds.size.y + 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.17f, 0.2f);
            var rt = new RenderTexture(MapResolution, MapResolution, 24);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(MapResolution, MapResolution, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, MapResolution, MapResolution), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var png = tex.EncodeToJPG(85);
            Destroy(tex);
            rt.Release();
            lock (_lock)
            {
                _mapPng = png;
                _mapJson = $"{{\"minX\":{F(bounds.center.x - half)},\"maxX\":{F(bounds.center.x + half)},\"minZ\":{F(bounds.center.z - half)},\"maxZ\":{F(bounds.center.z + half)},\"version\":{F(Time.time)}}}";
            }
        }
        finally
        {
            Destroy(go);
        }
    }

    // ------------------------------------------------------------------ commands (main thread)

    string Command(string path, Dictionary<string, string> q)
    {
        var nm = NetworkingManager.Instance;
        if (nm == null) return "no NetworkingManager";
        var host = CurrentHost;
        int Int(string k, int def = 0) => q.TryGetValue(k, out var v) && int.TryParse(v, out var i) ? i : def;
        switch (path)
        {
            case "/api/start-host":
                if (nm.NetSystem != null) return "already running";
                nm.StartHost();
                return "ok";
            case "/api/experiment":
                if (host == null) return "start the host first";
                host.SelectedExperiment = Mathf.Clamp(Int("index"), 0, nm.Experiments.Length - 1);
                return "ok";
            case "/api/map":
                if (host == null) return "start the host first";
                host.SetMap(Int("index", -1), q.TryGetValue("location", out var loc) ? loc : null);
                return "ok";
            case "/api/role":
                if (host == null) return "start the host first";
                host.SetRole(Int("player"), Int("role", -1));
                return "ok";
            case "/api/mode":
                if (Enum.TryParse(q.TryGetValue("mode", out var m) ? m : "", true, out PlayerSystem.InputMode mode))
                {
                    nm.PlayerSystem.SelectMode(mode);
                    return "ok";
                }
                return "unknown mode";
            case "/api/initialise":
                if (host == null) return "start the host first";
                host.Initialise();
                return "ok";
            case "/api/start":
                if (host == null) return "start the host first";
                host.StartSimulation();
                return "ok";
            case "/api/next-trial":
                nm.RequestNextTrial();
                return "ok";
            case "/api/ehmi":
            {
                var hmi = FindAnyObjectByType<HMIManager>();
                if (hmi == null || host == null) return "no eHMIs";
                if (!Enum.TryParse(q.TryGetValue("state", out var s) ? s : "", true, out HMIState state)) return "unknown state";
                int idx = Int("index", -1);
                for (int i = 0; i < hmi.Count; i++) if (idx < 0 || idx == i) hmi.HostSetHMI(host, i, state);
                return "ok";
            }
            case "/api/environment":
            {
                var env = EnvironmentController.Instance;
                if (env == null) return "no environment controller in this experiment";
                var list = new List<NetworkingManager.ExperimentParameter>();
                foreach (var kv in q) list.Add(new NetworkingManager.ExperimentParameter { name = kv.Key, value = kv.Value });
                env.SetParameter(list.ToArray());
                env.SetState(env.GetState());
                host?.BroadcastMessage(env.GetState());
                return "ok";
            }
            case "/api/visual-sync":
            {
                var vs = FindAnyObjectByType<VisualSyncManager>();
                if (vs == null || host == null) return "not available";
                host.BroadcastMessage(new VisualSyncMessage());
                vs.DisplayMarker();
                return "ok";
            }
            default:
                return "unknown command";
        }
    }

    // ------------------------------------------------------------------ HTTP (server thread)

    void Serve()
    {
        while (_running)
        {
            TcpClient client = null;
            try
            {
                client = _listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => Handle(client));
            }
            catch (Exception)
            {
                client?.Dispose();
                if (!_running) return;
            }
        }
    }

    void Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.ASCII);
                var requestLine = reader.ReadLine();
                if (string.IsNullOrEmpty(requestLine)) return;
                string line;
                while (!string.IsNullOrEmpty(line = reader.ReadLine())) { } // headers
                var parts = requestLine.Split(' ');
                var target = parts.Length > 1 ? parts[1] : "/";
                var qIdx = target.IndexOf('?');
                var path = qIdx >= 0 ? target.Substring(0, qIdx) : target;
                var query = ParseQuery(qIdx >= 0 ? target.Substring(qIdx + 1) : "");

                if (path == "/" || path == "/index.html")
                {
                    Respond(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(DashboardPage.Html));
                }
                else if (path == "/state.json")
                {
                    string json;
                    lock (_lock) json = _stateJson;
                    Respond(stream, 200, "application/json", Encoding.UTF8.GetBytes(json));
                }
                else if (path == "/map.jpg")
                {
                    byte[] png;
                    lock (_lock) png = _mapPng;
                    if (png == null) Respond(stream, 404, "text/plain", Encoding.UTF8.GetBytes("no map yet"));
                    else Respond(stream, 200, "image/jpeg", png);
                }
                else if (path.StartsWith("/api/"))
                {
                    var done = new ManualResetEventSlim();
                    string result = null;
                    _mainThread.Enqueue(() => { result = Command(path, query); done.Set(); });
                    done.Wait(5000);
                    Respond(stream, result == "ok" ? 200 : 400, "text/plain", Encoding.UTF8.GetBytes(result ?? "timeout"));
                }
                else
                {
                    Respond(stream, 404, "text/plain", Encoding.UTF8.GetBytes("not found"));
                }
            }
            catch (Exception) { }
        }
    }

    static Dictionary<string, string> ParseQuery(string q)
    {
        var d = new Dictionary<string, string>();
        foreach (var kv in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var i = kv.IndexOf('=');
            if (i < 0) d[Uri.UnescapeDataString(kv)] = "";
            else d[Uri.UnescapeDataString(kv.Substring(0, i))] = Uri.UnescapeDataString(kv.Substring(i + 1).Replace('+', ' '));
        }
        return d;
    }

    static void Respond(Stream stream, int status, string contentType, byte[] body)
    {
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {(status == 200 ? "OK" : status == 404 ? "Not Found" : "Bad Request")}\r\n" +
            $"Content-Type: {contentType}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        stream.Write(header, 0, header.Length);
        stream.Write(body, 0, body.Length);
    }
}
