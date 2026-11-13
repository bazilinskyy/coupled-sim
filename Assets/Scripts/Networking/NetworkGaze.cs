using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Where every participant looks, shared over the network: each agent sends the gaze of its
/// participant (eye tracking through OpenXR when the headset has it, else the head's direction) to
/// the host 20 times a second. The host logs all of them (ExperimentLogs/Gaze-{date}.csv: player,
/// source, origin, direction, the object looked at) and runs gaze triggers: when one role looks at
/// another role's car for a dwell time, that car yields (an AV of the host: it brakes to a stop and
/// waits while being looked at, plus HoldSeconds) and/or its eHMIs switch (e.g. to WALK), as in
/// "stopping by looking" studies.
/// Triggers: GazeTrigger entries on this component (on the experiment prefab), or
/// -gazeTrigger "Pedestrian>Automated - Passenger:0.5:yield,ehmi" on the host's command line.
/// </summary>
public class NetworkGaze : MonoBehaviour
{
    [Serializable]
    public struct GazeTrigger
    {
        public string LookerRole;
        public string CarRole;
        [Tooltip("Seconds of looking at the car before it reacts.")]
        public float Dwell;
        public bool Yield;
        public bool SwitchEhmi;
        public HMIState EhmiState;
        [Tooltip("Seconds the reaction lasts after the gaze leaves the car.")]
        public float HoldSeconds;
    }

    public GazeTrigger[] Triggers = new GazeTrigger[0];
    public float Rate = 20f;
    public float MaxDistance = 150f;

    public struct Sample { public Vector3 Origin, Direction; public bool Eyes; public float Time; }
    public static readonly Dictionary<int, Sample> Latest = new Dictionary<int, Sample>();
    public static Action<Sample> SendToHost;          // set by Client
    public static NetworkGaze Instance { get; private set; }

    readonly List<InputDevice> _eyes = new List<InputDevice>();
    readonly Dictionary<int, float> _looking = new Dictionary<int, float>();   // trigger -> seconds looked
    readonly Dictionary<int, float> _active = new Dictionary<int, float>();    // trigger -> time it ends
    StreamWriter _log;
    float _next;

    public static NetworkGaze Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("Network gaze");
        Instance = go.AddComponent<NetworkGaze>();
        Instance.ReadCommandLine();
        return Instance;
    }

    void Awake() { if (Instance == null) Instance = this; ReadCommandLine(); }
    void OnDestroy() { if (Instance == this) Instance = null; _log?.Dispose(); Latest.Clear(); }

    void ReadCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        var list = new List<GazeTrigger>(Triggers);
        var ci = CultureInfo.InvariantCulture;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "-gazeTrigger") continue;
            // "Looker>Car:dwell:yield,ehmi"
            var parts = args[i + 1].Split(':');
            var roles = parts[0].Split('>');
            if (roles.Length != 2) continue;
            var actions = parts.Length > 2 ? parts[2].ToLowerInvariant() : "yield";
            list.Add(new GazeTrigger
            {
                LookerRole = roles[0], CarRole = roles[1],
                Dwell = parts.Length > 1 && float.TryParse(parts[1], NumberStyles.Float, ci, out var d) ? d : 0.5f,
                Yield = actions.Contains("yield"), SwitchEhmi = actions.Contains("ehmi"), EhmiState = HMIState.WALK, HoldSeconds = 3f,
            });
        }
        Triggers = list.ToArray();
    }

    static bool IsHost => NetworkingManager.Instance != null && NetworkingManager.Instance.IsHost;

    void Update()
    {
        var nm = NetworkingManager.Instance;
        if (nm == null || nm.PlayerSystem == null || nm.PlayerSystem.LocalPlayer == null) return;
        if (Time.unscaledTime >= _next)
        {
            _next = Time.unscaledTime + 1f / Rate;
            if (TryLocalGaze(nm.PlayerSystem.LocalPlayer, out var s))
            {
                if (IsHost) Receive(0, s);
                else SendToHost?.Invoke(s);
            }
        }
        if (IsHost) RunTriggers(nm);
    }

    bool TryLocalGaze(PlayerAvatar me, out Sample s)
    {
        s = default;
        Camera cam = null;
        foreach (var c in me.GetComponentsInChildren<Camera>())
            if (c.isActiveAndEnabled && c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
        if (cam == null) return false;
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, _eyes);
        if (_eyes.Count > 0 && _eyes[0].TryGetFeatureValue(CommonUsages.devicePosition, out var lp) && _eyes[0].TryGetFeatureValue(CommonUsages.deviceRotation, out var lr))
        {
            var origin = cam.transform.parent;
            s.Origin = origin != null ? origin.TransformPoint(lp) : lp;
            s.Direction = (origin != null ? origin.rotation * lr : lr) * Vector3.forward;
            s.Eyes = true;
        }
        else
        {
            s.Origin = cam.transform.position;
            s.Direction = cam.transform.forward;
        }
        s.Time = Time.time;
        return true;
    }

    /// <summary>Host: a gaze sample of a player (its own or a client's).</summary>
    public static void Receive(int player, Sample s)
    {
        Latest[player] = s;
        if (Instance != null) Instance.Log(player, s);
    }

    void Log(int player, Sample s)
    {
        string hit = "";
        float dist = -1f;
        if (Physics.Raycast(s.Origin, s.Direction, out var h, MaxDistance, ~0, QueryTriggerInteraction.Ignore)) { hit = h.collider.transform.root.name; dist = h.distance; }
        try
        {
            if (_log == null)
            {
                Directory.CreateDirectory("ExperimentLogs");
                _log = new StreamWriter($"ExperimentLogs/Gaze-{NetworkingManager.LogPrefix}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv") { AutoFlush = false };
                _log.WriteLine("time;player;role;source;origin_x;origin_y;origin_z;dir_x;dir_y;dir_z;object;distance");
            }
            var ci = CultureInfo.InvariantCulture;
            string F(float f) => f.ToString("F3", ci);
            var role = player >= 0 && player < SeatedPassenger.RoleNames.Length ? SeatedPassenger.RoleNames[player] : "";
            _log.WriteLine(string.Join(";", F(Time.time), player.ToString(ci), role, s.Eyes ? "eyes" : "head", F(s.Origin.x), F(s.Origin.y), F(s.Origin.z),
                F(s.Direction.x), F(s.Direction.y), F(s.Direction.z), "\"" + hit.Replace("\"", "'") + "\"", dist.ToString("F2", ci)));
        }
        catch (Exception e) { Debug.LogWarning("NetworkGaze: log " + e.Message); _log = null; }
    }

    static int PlayerOfRole(string role)
    {
        for (int p = 0; p < SeatedPassenger.RoleNames.Length; p++) if (SeatedPassenger.RoleNames[p] == role) return p;
        return -1;
    }

    void RunTriggers(NetworkingManager nm)
    {
        var ps = nm.PlayerSystem;
        for (int i = 0; i < Triggers.Length; i++)
        {
            var tr = Triggers[i];
            int looker = PlayerOfRole(tr.LookerRole), carPlayer = PlayerOfRole(tr.CarRole);
            var car = carPlayer >= 0 ? ps.GetAvatar(carPlayer) : null;
            if (looker < 0 || car == null || !Latest.TryGetValue(looker, out var s) || Time.time - s.Time > 0.5f) continue;
            bool onCar = Physics.Raycast(s.Origin, s.Direction, out var h, MaxDistance, ~0, QueryTriggerInteraction.Ignore) && h.collider.transform.IsChildOf(car.transform);
            _looking[i] = onCar ? (_looking.TryGetValue(i, out var l) ? l : 0f) + Time.deltaTime : 0f;
            bool was = _active.ContainsKey(i);
            if (_looking[i] >= Mathf.Max(0.01f, tr.Dwell)) _active[i] = Time.time + tr.HoldSeconds;
            bool on = _active.TryGetValue(i, out var until) && Time.time <= until;
            if (on && !was) React(tr, car, true);
            if (!on && was) { _active.Remove(i); React(tr, car, false); }
            if (on && tr.Yield) Brake(car);
        }
    }

    void React(GazeTrigger tr, PlayerAvatar car, bool start)
    {
        Log(-1, new Sample());
        Debug.Log($"NetworkGaze: {tr.LookerRole} {(start ? "looks at" : "stopped looking at")} the car of {tr.CarRole}: {(tr.Yield ? "yield " : "")}{(tr.SwitchEhmi ? "eHMI " + tr.EhmiState : "")}");
        try { _log?.WriteLine($"{Time.time.ToString("F3", CultureInfo.InvariantCulture)};-1;{tr.LookerRole};trigger;;;;;;;\"{(start ? "start" : "end")} {tr.CarRole}\";"); } catch (Exception) { }
        if (tr.SwitchEhmi)
        {
            var hmi = FindAnyObjectByType<HMIManager>();
            var host = NetworkingManager.Instance != null ? NetworkingManager.Instance.HostSystem : null;
            if (hmi != null && host != null)
                foreach (var e in car.GetComponentsInChildren<HMI>())
                    for (int k = 0; k < hmi.Count; k++)
                        if (hmi.Get(k) == e) hmi.HostSetHMI(host, k, start ? tr.EhmiState : HMIState.DISABLED);
        }
        if (tr.Yield && !start)
        {
            var ai = car.GetComponent<AICar>();
            if (ai != null) ai.SpeedCap = float.PositiveInfinity;
        }
    }

    // brakes the car to a stop at about 3 m/s² (an AV driven on this agent)
    static void Brake(PlayerAvatar car)
    {
        var ai = car.GetComponent<AICar>();
        if (ai == null || !ai.enabled) return;
        float now = float.IsInfinity(ai.SpeedCap) ? ai.Speed / 3.6f : ai.SpeedCap;
        ai.SpeedCap = Mathf.Max(0f, now - 3f * Time.deltaTime);
    }
}

public struct GazeMsg : INetMessage
{
    public Vector3 Origin, Direction;
    public bool Eyes;
    public int MessageId => (int)MsgId.C_Gaze;
    public void Sync<T>(T s) where T : ISynchronizer { s.Sync(ref Origin); s.Sync(ref Direction); s.Sync(ref Eyes); }
}
