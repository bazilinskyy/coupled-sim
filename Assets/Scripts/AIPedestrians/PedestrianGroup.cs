using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Virtual pedestrians next to a participant pedestrian, e.g. for studies of crossing in groups
/// ("does the participant cross when the others do?"): each companion stands at an offset from the
/// participant (metres to the right and ahead, in the participant's direction at the start) and
/// - Follow: walks along, keeping its offset,
/// - Cross: starts crossing (walks straight ahead Distance m at Speed m/s) Delay s after the start,
/// - Hesitate: Delay s after the start steps forward 0.7 m, waits, and steps back,
/// - Stand: stays.
/// - Llm: waits and asks Claude every few seconds, with a description of the scene (SceneDescriber),
///   whether to cross now; crosses when the answer is CROSS (needs ANTHROPIC_API_KEY, see LlmClient;
///   the answers and reasons are logged).
/// Set it on the experiment prefab (Companions), or with -companions on the command line of every
/// agent, e.g. -companions "follow:-1.2,0;cross:1.4,0.4,3;hesitate:2.4,0,2" (mode:right,ahead[,delay]).
/// The host moves them; clients get their poses like other AI pedestrians. Their actions are logged
/// to ExperimentLogs/Companions-{date}.csv.
/// </summary>
public class PedestrianGroup : MonoBehaviour
{
    public enum Action { Follow, Cross, Hesitate, Stand, Llm }

    [Serializable]
    public struct Companion
    {
        public Action Mode;
        [Tooltip("Metres to the right (negative: left) and ahead of the participant.")]
        public Vector2 Offset;
        [Tooltip("Seconds after the start (Cross, Hesitate).")]
        public float Delay;
        public float Speed;      // m/s, 0 = 1.35
        public float Distance;   // m to cross, 0 = 12
        [Tooltip("Index in Resources/PedestrianAvatars (-1: by position).")]
        public int Avatar;
    }

    [Tooltip("Name of the participant's role the companions stand by.")]
    public string ParticipantRole = "Pedestrian";
    public Companion[] Companions = new Companion[0];

    readonly List<Mover> _movers = new List<Mover>();
    PlayerAvatar _participant;
    bool _spawned, _isHost;
    float _start;

    static System.IO.StreamWriter _log;

    /// <summary>A group from -companions when the experiment has none (on every agent that has the option).</summary>
    public static void EnsureFromCommandLine()
    {
        if (FindAnyObjectByType<PedestrianGroup>() != null) return;
        var lm = NetworkingManager.Instance != null ? NetworkingManager.Instance.Levels : null;
        var exp = lm != null ? lm.ActiveExperiment : null;
        var go = new GameObject("Pedestrian group");
        if (exp != null) go.transform.SetParent(exp.transform, false);
        go.AddComponent<PedestrianGroup>();
    }

    void Start()
    {
        var cl = FromCommandLine();
        if (cl != null) Companions = cl;
        _isHost = NetworkingManager.Instance != null && NetworkingManager.Instance.IsHost;
    }

    static Companion[] FromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-companions");
        if (i < 0 || i + 1 >= args.Length) return null;
        var list = new List<Companion>();
        var ci = CultureInfo.InvariantCulture;
        foreach (var part in args[i + 1].Split(';'))
        {
            var kv = part.Split(':');
            if (kv.Length < 2 || !Enum.TryParse<Action>(kv[0].Trim(), true, out var mode)) continue;
            var n = kv[1].Split(',');
            float F(int k) => k < n.Length && float.TryParse(n[k], NumberStyles.Float, ci, out var f) ? f : 0f;
            list.Add(new Companion { Mode = mode, Offset = new Vector2(F(0), F(1)), Delay = F(2), Avatar = -1 });
        }
        return list.ToArray();
    }

    PlayerAvatar FindParticipant()
    {
        var ps = NetworkingManager.Instance != null ? NetworkingManager.Instance.PlayerSystem : null;
        if (ps == null) return null;
        for (int p = 0; p < SeatedPassenger.RoleNames.Length; p++)
            if (SeatedPassenger.RoleNames[p] == ParticipantRole && ps.GetAvatar(p) != null) return ps.GetAvatar(p);
        return null;
    }

    void Update()
    {
        if (Companions == null || Companions.Length == 0) return;
        if (_participant == null) _participant = FindParticipant();
        if (_participant == null) return;
        if (!_spawned) Spawn();
        if (!_isHost) return;   // clients get the poses from the host
        float t = Time.time - _start;
        foreach (var m in _movers) m.Tick(t, _participant.transform, Time.deltaTime);
    }

    void Spawn()
    {
        _spawned = true;
        _start = Time.time;
        var assets = Resources.Load<PedestrianAvatars>("PedestrianAvatars");
        if (assets == null || assets.Prefabs.Length == 0) { Debug.LogWarning("PedestrianGroup: Resources/PedestrianAvatars missing"); return; }
        var p = _participant.transform;
        var fwd = Vector3.ProjectOnPlane(p.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, fwd);
        var sync = GetComponentInParent<ExperimentDefinition>()?.AIPedestrians;
        if (sync == null) Debug.LogWarning("PedestrianGroup: not under an experiment: companions are not sent to clients");
        for (int i = 0; i < Companions.Length; i++)
        {
            var c = Companions[i];
            var prefab = assets.Prefabs[(c.Avatar >= 0 ? c.Avatar : i + 3) % assets.Prefabs.Length];
            var pos = p.position + right * c.Offset.x + fwd * c.Offset.y;
            var go = Instantiate(prefab, pos, Quaternion.LookRotation(fwd));
            go.name = $"Companion {i + 1} ({c.Mode})";
            foreach (var ai in go.GetComponentsInChildren<AIPedestrian>()) ai.enabled = false;
            foreach (var tr in go.GetComponentsInChildren<UnityStandardAssets.Utility.WaypointProgressTracker>()) tr.enabled = false;
            var avatar = go.GetComponent<PlayerAvatar>();
            if (avatar != null) sync?.Register(avatar);
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = true;
            _movers.Add(new Mover(go.transform, anim, c, c.Offset, i + 1) { Host = this });
        }
        Log(0f, "spawned", $"{Companions.Length} companions by {ParticipantRole}");
    }

    internal static void Log(float t, string companion, string what)
    {
        try
        {
            if (_log == null)
            {
                System.IO.Directory.CreateDirectory("ExperimentLogs");
                _log = new System.IO.StreamWriter($"ExperimentLogs/Companions-{NetworkingManager.LogPrefix}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv") { AutoFlush = true };
                _log.WriteLine("time;companion;event");
                Application.quitting += () => { _log?.Dispose(); _log = null; };
            }
            _log.WriteLine($"{t.ToString("F2", CultureInfo.InvariantCulture)};{companion};{what}");
        }
        catch (Exception) { }
    }

    sealed class Mover
    {
        readonly Transform _t;
        readonly Animator _anim;
        readonly Companion _c;
        readonly Vector2 _offset;
        readonly int _id;
        Vector3 _origin, _fwd;
        float _speed;
        bool _started, _done, _decided, _asking;
        float _nextAsk, _t0 = Time.time;
        public MonoBehaviour Host;

        public Mover(Transform t, Animator anim, Companion c, Vector2 offset, int id)
        {
            _t = t; _anim = anim; _c = c; _offset = offset; _id = id;
            _origin = t.position; _fwd = t.forward;
        }

        public void Tick(float time, Transform participant, float dt)
        {
            Vector3 target = _t.position;
            float maxSpeed = _c.Speed > 0f ? _c.Speed : 1.35f;
            switch (_c.Mode)
            {
                case Action.Follow:
                {
                    var f = Vector3.ProjectOnPlane(participant.forward, Vector3.up).normalized;
                    var r = Vector3.Cross(Vector3.up, f);
                    target = participant.position + r * _offset.x + f * _offset.y;
                    maxSpeed = 3.5f;
                    break;
                }
                case Action.Cross:
                    if (time >= _c.Delay)
                    {
                        if (!_started) { _started = true; Log(time, $"Companion {_id}", "starts crossing"); }
                        target = _origin + _fwd * (_c.Distance > 0f ? _c.Distance : 12f);
                    }
                    break;
                case Action.Llm:
                    if (_decided) target = _origin + _fwd * (_c.Distance > 0f ? _c.Distance : 12f);
                    else if (!_asking && time >= _nextAsk && time >= _c.Delay && Host != null)
                    {
                        _asking = true;
                        _nextAsk = time + 3f;
                        var desc = SceneDescriber.Describe(_t);
                        Host.StartCoroutine(LlmClient.Ask(
                            "You are a pedestrian standing at the kerb, about to cross the road straight ahead of you. You see the scene described by the user. " +
                            "Decide whether to start crossing now. Answer with CROSS or WAIT as the first word, then one short sentence with your reason.",
                            desc, answer =>
                            {
                                _asking = false;
                                var a = (answer ?? "").Trim();
                                Log(Time.time - _t0, $"Companion {_id}", $"LLM: {(a.Length > 0 ? a.Replace(";", ",") : "no answer")} | scene: {desc.Replace(";", ",")}");
                                if (a.StartsWith("CROSS", StringComparison.OrdinalIgnoreCase)) { _decided = true; Log(Time.time - _t0, $"Companion {_id}", "starts crossing (LLM)"); }
                            }));
                    }
                    break;
                case Action.Hesitate:
                    if (time >= _c.Delay && time < _c.Delay + 1.2f) { if (!_started) { _started = true; Log(time, $"Companion {_id}", "hesitates (steps forward)"); } target = _origin + _fwd * 0.7f; maxSpeed = 0.9f; }
                    else if (time >= _c.Delay + 2.2f) { if (!_done) { _done = true; Log(time, $"Companion {_id}", "steps back"); } target = _origin; maxSpeed = 0.7f; }
                    break;
            }
            var to = target - _t.position; to.y = 0f;
            float dist = to.magnitude;
            float want = dist > 0.05f ? Mathf.Min(maxSpeed, dist * 2f) : 0f;
            _speed = Mathf.MoveTowards(_speed, want, 2.5f * dt);
            if (dist > 0.05f)
            {
                _t.position += to / dist * Mathf.Min(dist, _speed * dt);
                if (_c.Mode != Action.Hesitate || time < _c.Delay + 1.2f)
                    _t.rotation = Quaternion.Slerp(_t.rotation, Quaternion.LookRotation(_c.Mode == Action.Follow ? Vector3.ProjectOnPlane(participant.forward, Vector3.up) : to), 1f - Mathf.Exp(-dt * 6f));
            }
            if (_anim != null && _anim.runtimeAnimatorController != null)
            {
                _anim.SetFloat("Speed", Mathf.Clamp01(_speed / 1.4f));
                _anim.speed = _speed > 1.4f ? Mathf.Min(2f, _speed / 1.4f) : 1f;
            }
        }
    }
}
