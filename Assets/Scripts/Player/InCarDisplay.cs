using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// A display inside the participant's car, described like a designed eHMI (DesignedEhmi) in
/// StreamingAssets/InCar/{Design}.json: a head-up display (HUD) on the windshield, seen about 2.5 m
/// ahead, a centre screen on the dashboard, or the instrument cluster behind the steering wheel (on
/// the car's own dashboard, replacing its old speedometer). Its states follow the automation
/// (AutomatedDriving): MANUAL, AUTOMATED, TAKEOVER (a take-over request); elements of the state ANY
/// are always shown.
/// Live values for text and for gauges and bars ("value"): {speed} (km/h), {rpm}, {gear} (P, R, N,
/// D1...), {mode}, {time_left} (seconds left to take over), {reaction} (last take-over reaction
/// time, s), {slider} (response slider), {clock} (time of day), {trial_time} (s), {distance} (km
/// driven), {surface} (road surface), {message} (the message shown now).
/// Conditions ("when"): left, right, blinkers, automated, manual, takeover, braking, reverse,
/// headlights, wet, rough (rough road), message; "!x" negates.
/// Messages at set times, distances or states: "messages" in the design, and a schedule file
/// StreamingAssets/InCar/Messages/{name}.csv (columns at;seconds;display;text and optionally color,
/// atDistance, onState; ";" or "," separated) from ExperimentRoleDefinition.InCarMessages or
/// -inCarMessages name. Shown messages are logged to ExperimentLogs/InCarMessages-*.csv.
/// Set per role (HudDesign, CentreScreenDesign, ClusterDesign) or with -hud / -centreScreen / -cluster.
/// </summary>
public class InCarDisplay : DesignedEhmi
{
    public enum Kind { Hud, CentreScreen, Cluster }
    public Kind Placement = Kind.Hud;

    AutomatedDriving _automation;
    Rigidbody _rb;
    PlayerAvatar _car;
    RealisticVehicleDynamics _dyn;
    CarBlinkers _blinkers;
    bool _placed;
    float _start, _distance;
    Vector3 _lastPos;
    string _lastState;

    /// <summary>Messages from the schedule file (for every display of this car), set by <see cref="LoadSchedule"/>.</summary>
    public static List<Message> Schedule = new List<Message>();
    sealed class Shown { public Message M; public float Until; public Element Back, Text; }
    readonly List<Shown> _shown = new List<Shown>();
    readonly HashSet<Message> _fired = new HashSet<Message>();
    static StreamWriter _log;

    protected override string Folder => "InCar";

    protected override string StateKey
    {
        get
        {
            if (_automation == null && _car != null) _automation = _car.GetComponentInChildren<AutomatedDriving>(true);
            if (_automation == null || !_automation.enabled) return "MANUAL";
            switch (_automation.State)
            {
                case AutomatedDriving.Control.Automated: return "AUTOMATED";
                case AutomatedDriving.Control.TakeOverRequested: return "TAKEOVER";
                default: return "MANUAL";
            }
        }
    }

    protected override string FillText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        var ci = CultureInfo.InvariantCulture;
        float speed = _rb != null ? Mathf.Abs(Vector3.Dot(_rb.linearVelocity, _rb.transform.forward)) * 3.6f : 0f;
        float left = _automation != null && _automation.State == AutomatedDriving.Control.TakeOverRequested
            ? Mathf.Max(0f, _automation.TimeBudget - (Time.time - _automation.RequestTime)) : 0f;
        string mode = StateKey == "AUTOMATED" ? "Automated" : StateKey == "TAKEOVER" ? "Take over" : "Manual";
        if (_dyn != null) speed = Mathf.Abs(_dyn.SpeedKmh);
        var env = EnvironmentController.Instance;
        string clock = "";
        if (text.Contains("{clock}") && env != null) { float h = Mathf.Repeat(env.TimeOfDay, 24f); clock = $"{(int)h:00}:{(int)((h - (int)h) * 60f):00}"; }
        if (text.Contains("{message}")) text = text.Replace("{message}", _shown.Count > 0 ? _shown[_shown.Count - 1].M.text : "");
        return text.Replace("{speed}", Mathf.RoundToInt(speed).ToString(ci))
                   .Replace("{rpm}", _dyn != null ? Mathf.RoundToInt(_dyn.Rpm).ToString(ci) : "0")
                   .Replace("{gear}", Gear())
                   .Replace("{clock}", clock)
                   .Replace("{trial_time}", (Time.time - _start).ToString("F0", ci))
                   .Replace("{distance}", (_distance / 1000f).ToString("F1", ci))
                   .Replace("{surface}", _dyn != null ? _dyn.Surface : "")
                   .Replace("{mode}", mode)
                   .Replace("{time_left}", left.ToString("F1", ci))
                   .Replace("{reaction}", _automation != null && _automation.LastReactionTime >= 0 ? _automation.LastReactionTime.ToString("F2", ci) : "-")
                   .Replace("{slider}", ResponseInput.Local != null ? ResponseInput.Local.SliderValue.ToString("F0", ci) : "-");
    }

    string Gear()
    {
        if (_dyn == null) return "D";
        if (_dyn.Gear < 0) return "R";
        if (Mathf.Abs(_dyn.SpeedKmh) < 0.5f && VehicleInput.GetAxis("Throttle") < 0.05f) return "P";
        return "D" + Mathf.Max(1, _dyn.Gear);
    }

    protected override bool When(string condition)
    {
        var env = EnvironmentController.Instance;
        switch (condition.ToLowerInvariant())
        {
            case "left": return _blinkers != null && _blinkers.State == BlinkerState.Left && _blinkers.LampOn;
            case "right": return _blinkers != null && _blinkers.State == BlinkerState.Right && _blinkers.LampOn;
            case "blinkers": return _blinkers != null && _blinkers.State != BlinkerState.None;
            case "automated": return StateKey == "AUTOMATED";
            case "manual": return StateKey == "MANUAL";
            case "takeover": return StateKey == "TAKEOVER";
            case "braking": return VehicleInput.GetAxis("Brake") > 0.1f || VehicleInput.GetAxis("Break") > 0.1f;
            case "reverse": return _dyn != null && _dyn.Gear < 0;
            case "headlights": return env != null && env.SunElevation < 1f;
            case "wet": return env != null && env.Rain > 0.2f;
            case "rough": return _dyn != null && _dyn.Surface != "asphalt";
            case "message": return _shown.Count > 0;
            default: return true;
        }
    }

    string DisplayName => Placement == Kind.Hud ? "hud" : Placement == Kind.CentreScreen ? "centre" : "cluster";

    bool ForThisDisplay(Message m)
    {
        var d = (m.display ?? "").Trim().ToLowerInvariant();
        return d.Length == 0 || d == "all" || d == DisplayName || (d == "centrescreen" && Placement == Kind.CentreScreen);
    }

    // messages: start those whose time, distance or state has come; drop those that are over
    protected override void ExtraElements(List<Element> into)
    {
        float now = Time.time - _start;
        string state = StateKey;
        bool stateStarted = state != _lastState;
        _lastState = state;
        IEnumerable<Message> all = Loaded != null && Loaded.messages != null ? (IEnumerable<Message>)Loaded.messages : new Message[0];
        foreach (var list in new[] { all, Schedule })
            foreach (var m in list)
            {
                if (_fired.Contains(m) || !ForThisDisplay(m)) continue;
                bool due = (m.at >= 0f && now >= m.at) || (m.atDistance >= 0f && _distance >= m.atDistance)
                           || (stateStarted && !string.IsNullOrEmpty(m.onState) && string.Equals(m.onState, state, StringComparison.OrdinalIgnoreCase));
                if (!due) continue;
                _fired.Add(m);
                // on the cluster, a message with the default place goes at the bottom, between the dials
                float mx = m.x, my = m.y, mw = m.w, mh = m.h;
                if (Placement == Kind.Cluster && Mathf.Approximately(m.x, 0.05f) && Mathf.Approximately(m.w, 0.9f)) { mx = 0.375f; my = 0.02f; mw = 0.25f; mh = 0.27f; }
                var back = new Element { type = "rect", x = mx, y = my, w = mw, h = mh, color = m.background, animation = m.animation, period = m.period };
                var txt = new Element { type = "text", x = mx + mw * 0.03f, y = my + mh * 0.1f, w = mw * 0.94f, h = mh * 0.8f, color = m.color, text = m.text, animation = m.animation, period = m.period };
                _shown.Add(new Shown { M = m, Until = Time.time + Mathf.Max(0.1f, m.seconds), Back = back, Text = txt });
                LogMessage(m, now);
            }
        _shown.RemoveAll(x => Time.time > x.Until);
        foreach (var x in _shown) { into.Add(x.Back); into.Add(x.Text); }
    }

    void LogMessage(Message m, float now)
    {
        try
        {
            if (_log == null)
            {
                Directory.CreateDirectory("ExperimentLogs");
                _log = new StreamWriter($"ExperimentLogs/InCarMessages-{NetworkingManager.LogPrefix}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv") { AutoFlush = true };
                _log.WriteLine("utc;trial_time;display;text;seconds;distance_m");
            }
            var ci = CultureInfo.InvariantCulture;
            _log.WriteLine($"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff};{now.ToString("F2", ci)};{DisplayName};\"{(m.text ?? "").Replace("\"", "'")}\";{m.seconds.ToString("F1", ci)};{_distance.ToString("F0", ci)}");
        }
        catch (Exception) { }
    }

    /// <summary>Reads StreamingAssets/InCar/Messages/{name}.csv (or a full path) into <see cref="Schedule"/>.</summary>
    public static void LoadSchedule(string name)
    {
        Schedule = ParseSchedule(name, out var error);
        if (error != null) Debug.LogWarning("InCarDisplay: " + error);
        else Debug.Log($"InCarDisplay: {Schedule.Count} messages from {name}");
    }

    public static List<Message> ParseSchedule(string name, out string error)
    {
        error = null;
        var list = new List<Message>();
        if (string.IsNullOrEmpty(name)) return list;
        var path = Path.IsPathRooted(name) ? name : Path.Combine(Application.streamingAssetsPath, "InCar", "Messages", name.EndsWith(".csv") ? name : name + ".csv");
        if (!File.Exists(path)) { error = "no message schedule " + path; return list; }
        return ParseScheduleText(File.ReadAllText(path), out error);
    }

    /// <summary>Parses a schedule: a header row (at, seconds, display, text, color, atDistance, onState, background) and one message per row.</summary>
    public static List<Message> ParseScheduleText(string csv, out string error)
    {
        error = null;
        var list = new List<Message>();
        var lines = csv.Replace("\r", "").Split('\n');
        if (lines.Length == 0) return list;
        char sep = lines[0].Contains(";") ? ';' : ',';
        var head = SplitCsv(lines[0], sep);
        int Col(string n) => head.FindIndex(h => string.Equals(h.Trim(), n, StringComparison.OrdinalIgnoreCase));
        int cAt = Col("at"), cSec = Col("seconds"), cDisp = Col("display"), cText = Col("text"), cCol = Col("color"), cDist = Col("atDistance"), cState = Col("onState"), cBack = Col("background");
        if (cText < 0) { error = "message schedule needs a 'text' column"; return list; }
        var ci = CultureInfo.InvariantCulture;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0 || lines[i].TrimStart().StartsWith("#")) continue;
            var f = SplitCsv(lines[i], sep);
            string F(int c) => c >= 0 && c < f.Count ? f[c].Trim() : "";
            float Num(int c, float d) => float.TryParse(F(c), NumberStyles.Float, ci, out var v) ? v : d;
            var m = new Message { at = Num(cAt, -1f), seconds = Num(cSec, 5f), display = F(cDisp), text = F(cText), atDistance = Num(cDist, -1f), onState = F(cState) };
            if (F(cCol).Length > 0) m.color = F(cCol);
            if (F(cBack).Length > 0) m.background = F(cBack);
            list.Add(m);
        }
        return list;
    }

    static List<string> SplitCsv(string line, char sep)
    {
        var r = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        foreach (var ch in line)
        {
            if (ch == '"') q = !q;
            else if (ch == sep && !q) { r.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(ch);
        }
        r.Add(cur.ToString());
        return r;
    }

    /// <summary>Adds a display to the participant's car (it places itself once the car's camera is known).</summary>
    public static InCarDisplay Add(PlayerAvatar car, string design, Kind kind)
    {
        var go = new GameObject($"In-car display {kind} {design}");
        go.transform.SetParent(car.transform, false);
        go.layer = car.gameObject.layer;
        var d = go.AddComponent<InCarDisplay>();
        d.Design = design;
        d.Placement = kind;
        d._car = car;
        return d;
    }

    protected override void Start()
    {
        if (_car == null) _car = GetComponentInParent<PlayerAvatar>();
        if (_car != null)
        {
            _automation = _car.GetComponentInChildren<AutomatedDriving>(true);
            _rb = _car.GetComponent<Rigidbody>();
            _dyn = _car.GetComponentInChildren<RealisticVehicleDynamics>(true);
            _blinkers = _car.GetComponentInChildren<CarBlinkers>(true);
            _lastPos = _car.transform.position;
        }
        _start = Time.time;
        if (Placement == Kind.Cluster) Resolution = 3000f;   // a small, close panel: sharp text and scales
        base.Start();
    }

    protected override void Update()
    {
        if (!_placed) Place();
        if (_car != null)
        {
            var p = _car.transform.position;
            float step = Vector3.Distance(p, _lastPos);
            if (step < 20f) _distance += step;   // not teleports
            _lastPos = p;
        }
        base.Update();
    }

    void Place()
    {
        Camera cam = null;
        foreach (var c in _car != null ? _car.GetComponentsInChildren<Camera>() : new Camera[0])
            if (c.isActiveAndEnabled && c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
        if (cam == null) return;
        var car = _car.transform;
        if (Placement == Kind.Cluster && PlaceOnDashboard(cam)) { _placed = true; return; }
        var eye = car.InverseTransformPoint(cam.transform.position);
        Vector3 local = Placement == Kind.Hud
            ? new Vector3(eye.x, eye.y - 0.02f, eye.z + 2.5f)      // seen through the windshield, just below the horizon
            : new Vector3(0f, eye.y - 0.16f, eye.z + 0.62f);      // middle of the dashboard
        transform.localPosition = local;
        // face the driver's eyes (the panel is seen from its +z side)
        transform.rotation = Quaternion.LookRotation(cam.transform.position - transform.position, car.up);
        _placed = true;
    }

    // the car's own instrument panel (the canvas "Dashboard" with its old speedometer): the cluster
    // takes its place and size, and the old speedometer and dashboard blinkers are hidden
    bool PlaceOnDashboard(Camera cam)
    {
        Transform dash = null;
        foreach (var t in _car.GetComponentsInChildren<Transform>(true)) if (t.name == "Dashboard" && t.GetComponent<Canvas>() != null) { dash = t; break; }
        if (dash == null || Loaded == null) return false;
        var rt = (RectTransform)dash;
        float worldWidth = rt.rect.width * dash.lossyScale.x, worldHeight = rt.rect.height * dash.lossyScale.y;
        foreach (var g in dash.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.enabled = false;
        foreach (var t in _car.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("DashBoardBlinker")) { var r = t.GetComponent<Renderer>(); if (r != null) r.enabled = false; }
        // the panel's canvas is turned 180° (eHMIs face forward); the dashboard canvas faces the driver
        transform.SetParent(dash, false);
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        float s = Mathf.Min(worldWidth / Loaded.width, worldHeight / Loaded.height) / Mathf.Max(1e-6f, dash.lossyScale.x);
        transform.localScale = Vector3.one * s;
        transform.position = dash.position - dash.forward * 0.002f;   // just in front of it, towards the driver
        return true;
    }
}
