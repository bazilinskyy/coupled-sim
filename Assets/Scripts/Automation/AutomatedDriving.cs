using System;
using System.Globalization;
using System.IO;
using CoupledSim.Traffic;
using UnityEngine;
using UnityStandardAssets.Utility;
using VehicleBehaviour;

/// <summary>
/// Automated driving of the participant's car (the AV of a PlayerInAIControlledCar role) and
/// transitions of control (#31):
/// - Mode: Waypoints (the speeds and yielding set on the path's waypoints, as before) or
///   TrafficModel (smooth car following with IDM/Gipps/Krauss: it adapts its speed to the car
///   ahead, slows down for bends, stops at red lights and for pedestrians and animals);
/// - take-over requests (TOR): a visual (in front of the driver, also in VR) and auditory warning;
///   the driver takes over by steering, braking or accelerating (or with the take-over button),
///   then drives the car manually with the full vehicle physics; with no reaction within
///   TimeBudget seconds the car brakes to a stop (minimal-risk manoeuvre) or keeps driving;
/// - engaging automation again (button), from where the car is on its path.
/// Triggers: trial parameter takeOverAt (seconds after the start, host), TakeOverPoint
/// triggers on the road, the experimenter's key (T) and the dashboard. Events go to
/// ExperimentLogs/TakeOver-{date}.csv with reaction times.
/// </summary>
public class AutomatedDriving : MonoBehaviour
{
    public enum DrivingMode { Waypoints, TrafficModel }
    public enum Control { Automated, TakeOverRequested, Manual }
    public enum NoReaction { BrakeToStop, KeepDriving }

    [Header("Automation")]
    public DrivingMode Mode = DrivingMode.Waypoints;
    [Tooltip("TrafficModel mode: car-following model and desired speed (km/h).")]
    public CarFollowingModelType Model = CarFollowingModelType.IDM;
    public float DesiredSpeedKmh = 40f;

    [Header("Take-over requests")]
    [Tooltip("Seconds the driver has to take over.")]
    public float TimeBudget = 7f;
    public NoReaction WhenNoReaction = NoReaction.BrakeToStop;
    [Tooltip("Steering (0..1 of full lock), brake or throttle (0..1) that count as taking over.")]
    public float SteerThreshold = 0.1f, PedalThreshold = 0.1f;
    [Tooltip("Experimenter key that issues a take-over request.")]
    public KeyCode RequestKey = KeyCode.T;
    [Tooltip("Driver's button (Input Manager) to take over or to engage automation again.")]
    public string AutomationButton = "automation";
    public bool AllowEngagingAgain = true;
    [Tooltip("Text shown with the warning.")]
    public string WarningText = "TAKE OVER";

    [Header("State (read only)")]
    public Control State = Control.Automated;
    public float RequestTime = -1f;
    public float LastReactionTime = -1f;

    public static AutomatedDriving Local { get; private set; }

    AICar _ai;
    WaypointProgressTracker _tracker;
    WheelVehicle _manual;
    RealisticVehicleDynamics _dynamics;
    ObservedSteering _observed;
    TrafficCar _traffic;
    Rigidbody _rb;
    AudioSource _audio;
    TextMesh _warning;
    float _scheduledAt = -1f, _startTime, _stopAccel;
    StreamWriter _log;
    bool _active;

    /// <summary>Called for the local participant's AV (PlayerSystem.ActivatePlayerAICar).</summary>
    public void Activate()
    {
        _active = true;
        Local = this;
        _ai = GetComponent<AICar>();
        _tracker = GetComponent<WaypointProgressTracker>();
        _manual = GetComponent<WheelVehicle>();
        _dynamics = GetComponent<RealisticVehicleDynamics>();
        _rb = GetComponent<Rigidbody>();
        _startTime = Time.time;
        if (Mode == DrivingMode.TrafficModel) StartTrafficModel();
        // trial parameter takeOverAt (host)
        // trial parameters (the host has the trial; clients use the defaults)
        var nm = NetworkingManager.Instance;
        var trial = nm != null && nm.NetSystem is Host && nm.trials != null && nm.trials.Length > 0
            ? NetworkingManager.Instance.trials[Mathf.Clamp(NetworkingManager.Instance.CurrentTrial, 0, NetworkingManager.Instance.trials.Length - 1)] : null;
        if (trial?.experimentParameters != null)
            foreach (var p in trial.experimentParameters)
            {
                var n = p.name.ToLowerInvariant();
                if (n == "takeoverat" && float.TryParse(p.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) _scheduledAt = t;
                if (n == "automation" && Enum.TryParse<DrivingMode>(p.value, true, out var m)) { Mode = m; if (m == DrivingMode.TrafficModel) StartTrafficModel(); }
                if (n == "avspeed" && float.TryParse(p.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) { DesiredSpeedKmh = s; if (_traffic != null) _traffic.DesiredSpeed = s / 3.6f; }
                if (n == "takeoverbudget" && float.TryParse(p.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) TimeBudget = b;
                if (n == "avpath") FollowPath(p.value);
            }
        Log("automation", Mode.ToString());
    }

    /// <summary>Follows a path file (ScriptedPath) instead of the role's AutonomousPath.</summary>
    public void FollowPath(string file)
    {
        ScriptedPath path;
        try { path = ScriptedPath.Load(file, transform.position.y); }
        catch (Exception e) { Debug.LogError("AutomatedDriving: " + e.Message); return; }
        var circuit = path.BuildCircuit(name + " path");
        var dir = path.Points[1] - path.Points[0]; dir.y = 0;
        transform.SetPositionAndRotation(path.Points[0], Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : transform.forward));
        if (_rb != null) { _rb.position = transform.position; _rb.rotation = transform.rotation; _rb.linearVelocity = Vector3.zero; }
        if (_tracker != null)
        {
            _tracker.Init(circuit);
            _tracker.SetProgress(ScriptedPath.LeadIn);
            _ai.target = _tracker.target;
        }
        if (_traffic != null) { Destroy(_traffic); _traffic = null; }
        _pathDriver = gameObject.AddComponent<ScriptedPathDriver>();
        _pathDriver.DefaultSpeedKmh = DesiredSpeedKmh;
        _pathDriver.Init(path);
        Log("path", file);
    }

    ScriptedPathDriver _pathDriver;

    void StartTrafficModel()
    {
        if (_traffic != null) return;
        // reuse a collision-avoidance supervisor the traffic spawner may have added
        if (!TryGetComponent(out _traffic)) _traffic = gameObject.AddComponent<TrafficCar>();
        _traffic.Init(Model, DesiredSpeedKmh / 3.6f, new System.Random(1));
    }

    void OnDestroy()
    {
        if (Local == this) Local = null;
        _log?.Dispose();
    }

    float Speed => _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) : 0f;

    void Log(string evt, string detail = "")
    {
        if (_log == null)
        {
            Directory.CreateDirectory("ExperimentLogs");
            _log = new StreamWriter($"ExperimentLogs/TakeOver-{NetworkingManager.LogPrefix}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv") { AutoFlush = true };
            _log.WriteLine("time,event,detail,speed_kmh,x,z");
        }
        var ci = CultureInfo.InvariantCulture;
        var p = transform.position;
        _log.WriteLine($"{(Time.time - _startTime).ToString("F3", ci)},{evt},\"{detail}\",{(Speed * 3.6f).ToString("F1", ci)},{p.x.ToString("F2", ci)},{p.z.ToString("F2", ci)}");
        Debug.Log($"AutomatedDriving: {evt} {detail}");
    }

    /// <summary>Issues a take-over request (TakeOverPoint, trial schedule, key, dashboard).</summary>
    public void RequestTakeOver(string source = "")
    {
        if (!_active || State != Control.Automated) return;
        State = Control.TakeOverRequested;
        RequestTime = Time.time;
        _stopAccel = 0f;
        Log("request", source);
        ShowWarning(true);
    }

    void TakeOver(string how)
    {
        State = Control.Manual;
        LastReactionTime = Time.time - RequestTime;
        ShowWarning(false);
        Log("takeover", $"{how}; reaction {LastReactionTime.ToString("F3", CultureInfo.InvariantCulture)} s");
        SetManual(true);
    }

    /// <summary>Hands the car back to the automation, from where it is on its path.</summary>
    public void Engage()
    {
        if (!_active || State != Control.Manual) return;
        if (_tracker != null && _tracker.Circuit != null) _tracker.SetProgress(NearestDistance(_tracker.Circuit, transform.position));
        _ai.speed = Mathf.Max(0f, Speed) * 3.6f;
        if (_traffic != null) _traffic.Speed = Mathf.Max(0f, Speed);
        if (_pathDriver != null) { _pathDriver.Speed = Mathf.Max(0f, Speed); _pathDriver.Distance = NearestDistance(_tracker.Circuit, transform.position); }
        if (_ai != null) _ai.ExternalSpeedControl = _traffic != null || _pathDriver != null;
        SetManual(false);
        State = Control.Automated;
        Log("engaged");
    }

    void SetManual(bool manual)
    {
        if (_ai != null) _ai.enabled = !manual;
        if (_tracker != null) _tracker.enabled = !manual;
        if (_traffic != null) _traffic.enabled = !manual;
        if (_pathDriver != null) _pathDriver.enabled = !manual;
        if (_manual != null) _manual.enabled = manual;
        if (_observed == null) TryGetComponent(out _observed);
        if (_observed != null) _observed.enabled = !manual;
    }

    static float NearestDistance(WaypointCircuit c, Vector3 p)
    {
        float best = 0f, bestD = float.MaxValue;
        for (float d = 0f; d < c.Length; d += 1f)
        {
            float dd = (c.GetRoutePosition(d) - p).sqrMagnitude;
            if (dd < bestD) { bestD = dd; best = d; }
        }
        return best;
    }

    // Enter or the wheel's automation button (VehicleInput)
    bool ButtonDown() => VehicleInput.GetButtonDown(AutomationButton);

    void Update()
    {
        if (!_active) return;
        if (Input.GetKeyDown(RequestKey)) RequestTakeOver("key");
        if (_scheduledAt >= 0f && Time.time - _startTime >= _scheduledAt) { _scheduledAt = -1f; RequestTakeOver("takeOverAt"); }
        switch (State)
        {
            case Control.TakeOverRequested:
                float steer = Mathf.Abs(VehicleInput.GetAxis("Horizontal")), brake = VehicleInput.GetAxis("Brake"), throttle = VehicleInput.GetAxis("Throttle");
                if (ButtonDown()) TakeOver("button");
                else if (steer > SteerThreshold) TakeOver("steer");
                else if (brake > PedalThreshold || throttle < -PedalThreshold) TakeOver("brake");
                else if (throttle > PedalThreshold) TakeOver("throttle");
                else if (Time.time - RequestTime > TimeBudget && _stopAccel == 0f)
                {
                    Log("no reaction", WhenNoReaction.ToString());
                    if (WhenNoReaction == NoReaction.BrakeToStop) _stopAccel = 3f;
                    else { State = Control.Automated; ShowWarning(false); }
                }
                break;
            case Control.Manual:
                if (AllowEngagingAgain && ButtonDown()) Engage();
                break;
        }
        if (_warning != null && _warning.gameObject.activeSelf)
            _warning.color = new Color(1f, 0.15f, 0.1f, Mathf.PingPong(Time.time * 3f, 1f) * 0.6f + 0.4f);
    }

    void FixedUpdate()
    {
        // minimal-risk manoeuvre: brake to a stop on the path
        if (State == Control.TakeOverRequested && _stopAccel > 0f && _ai != null)
        {
            if (_traffic != null) _traffic.enabled = false;
            if (_pathDriver != null) _pathDriver.enabled = false;
            _ai.ExternalSpeedControl = true;
            _ai.ExternalSpeed = Mathf.Max(0f, _ai.ExternalSpeed > 0f ? _ai.ExternalSpeed - _stopAccel * Time.fixedDeltaTime : Speed - _stopAccel * Time.fixedDeltaTime);
            _ai.ExternalAcceleration = -_stopAccel;
        }
    }

    void ShowWarning(bool on)
    {
        if (on)
        {
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.spatialBlend = 0f;
                _audio.clip = Beeps();
                _audio.loop = true;
            }
            _audio.Play();
            var cam = Camera.main != null ? Camera.main : GetComponentInChildren<Camera>();
            if (_warning == null && cam != null)
            {
                var go = new GameObject("Take-over warning");
                go.transform.SetParent(cam.transform, false);
                go.transform.localPosition = new Vector3(0f, -0.12f, 0.9f);
                _warning = go.AddComponent<TextMesh>();
                _warning.text = WarningText;
                _warning.anchor = TextAnchor.MiddleCenter;
                _warning.alignment = TextAlignment.Center;
                _warning.characterSize = 0.012f;
                _warning.fontSize = 80;
                _warning.fontStyle = FontStyle.Bold;
                _warning.GetComponent<MeshRenderer>().material.renderQueue = 4000;
            }
            if (_warning != null) _warning.gameObject.SetActive(true);
        }
        else
        {
            if (_audio != null) _audio.Stop();
            if (_warning != null) _warning.gameObject.SetActive(false);
        }
    }

    // three short 1 kHz beeps per second, as common take-over warnings
    static AudioClip Beeps()
    {
        const int rate = 22050;
        var data = new float[rate];
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate;
            float inBeep = (t % 0.333f) < 0.15f ? 1f : 0f;
            data[i] = inBeep * 0.35f * Mathf.Sin(2f * Mathf.PI * 1000f * t);
        }
        var clip = AudioClip.Create("TOR beeps", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
