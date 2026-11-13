using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

/// <summary>
/// Plays the vehicles and pedestrians of an ASAM OpenSCENARIO 1.x file (.xosc) in the scene, so
/// scenarios made for other simulators (CARLA, esmini, ...) or by scenario tools can be used here.
/// Supported part of the standard:
/// - ParameterDeclarations ($name in attributes);
/// - Entities: ScenarioObject with Vehicle, Pedestrian or CatalogReference (a pedestrian when the
///   catalogue entry's name says so, else a car);
/// - Init and events: TeleportAction (WorldPosition x, y, z, h) and SpeedAction (AbsoluteTargetSpeed,
///   with step, linear or cubic dynamics over a time, a rate or a distance);
/// - start triggers: SimulationTimeCondition (and events without a trigger, at the start).
/// Other actions and conditions are listed as unsupported in the log. Actors drive straight ahead
/// along their heading (no lane following). World coordinates are the scene's (x east, y north),
/// minus the road network's offset for OpenDRIVE/SUMO maps, so a scenario on the same .xodr fits.
/// Every agent plays the same file in step with the trial clock. Load with -openscenario file.xosc
/// (StreamingAssets/Scenarios or a full path). Events are logged to ExperimentLogs/OpenScenario-*.csv.
/// </summary>
public class OpenScenario : MonoBehaviour
{
    public string File;

    sealed class Actor
    {
        public string Name;
        public bool Pedestrian;
        public Transform T;
        public Animator Anim;
        public float Speed, Target, Rate;   // m/s, m/s, m/s² (0: step)
    }
    sealed class Event
    {
        public string Name, Entity;
        public float At = 0f;
        public bool Fired, Supported = true;
        public XmlElement Action;
    }

    readonly Dictionary<string, Actor> _actors = new Dictionary<string, Actor>();
    readonly List<Event> _events = new List<Event>();
    readonly Dictionary<string, string> _params = new Dictionary<string, string>();
    float _start;
    StreamWriter _log;
    Vector2 _offset;
    static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    public static OpenScenario Ensure()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-openscenario");
        if (i < 0 || i + 1 >= args.Length) return null;
        var go = new GameObject("OpenSCENARIO");
        var s = go.AddComponent<OpenScenario>();
        s.File = args[i + 1];
        return s;
    }

    void Start()
    {
        var path = Path.IsPathRooted(File) ? File : Path.Combine(Application.streamingAssetsPath, "Scenarios", File);
        if (!System.IO.File.Exists(path)) { Debug.LogWarning("OpenScenario: no file " + path); return; }
        if (OsmMap.Current != null && !string.IsNullOrEmpty(OsmMap.Current.RoadNetwork)) _offset = OsmMap.Current.RoadNetworkOffset;
        var doc = new XmlDocument();
        doc.Load(path);
        foreach (XmlElement p in doc.GetElementsByTagName("ParameterDeclaration"))
            _params[p.GetAttribute("name")] = p.GetAttribute("value");
        foreach (XmlElement o in doc.GetElementsByTagName("ScenarioObject")) Spawn(o);
        // Init: private actions per entity
        foreach (XmlElement init in doc.GetElementsByTagName("Init"))
            foreach (XmlElement priv in init.GetElementsByTagName("Private"))
                foreach (XmlElement a in priv.GetElementsByTagName("PrivateAction"))
                    _events.Add(new Event { Name = "Init", Entity = Attr(priv, "entityRef"), At = 0f, Action = a });
        // Story events
        foreach (XmlElement mg in doc.GetElementsByTagName("ManeuverGroup"))
        {
            var actors = new List<string>();
            foreach (XmlElement er in mg.GetElementsByTagName("EntityRef")) actors.Add(Attr(er, "entityRef"));
            foreach (XmlElement ev in mg.GetElementsByTagName("Event"))
            {
                float at = 0f; bool supported = true;
                var trig = First(ev, "StartTrigger");
                if (trig != null && trig.GetElementsByTagName("Condition").Count > 0)
                {
                    var st = First(trig, "SimulationTimeCondition");
                    if (st != null) at = F(Attr(st, "value"));
                    else supported = false;
                }
                foreach (XmlElement a in ev.GetElementsByTagName("PrivateAction"))
                    foreach (var ent in actors)
                        _events.Add(new Event { Name = Attr(ev, "name"), Entity = ent, At = at, Action = a, Supported = supported });
            }
        }
        _start = Time.time;
        Log("loaded", Path.GetFileName(path), $"{_actors.Count} actors, {_events.Count} actions");
        Debug.Log($"OpenScenario: {Path.GetFileName(path)}: {_actors.Count} actors, {_events.Count} actions");
    }

    string Attr(XmlElement e, string name)
    {
        var v = e.GetAttribute(name);
        return v.StartsWith("$") && _params.TryGetValue(v.Substring(1), out var p) ? p : v;
    }
    static XmlElement First(XmlElement e, string tag) { var l = e.GetElementsByTagName(tag); return l.Count > 0 ? (XmlElement)l[0] : null; }
    static float F(string s) => float.TryParse(s, NumberStyles.Float, Ci, out var f) ? f : 0f;

    void Spawn(XmlElement o)
    {
        var name = Attr(o, "name");
        bool ped = o.GetElementsByTagName("Pedestrian").Count > 0;
        var cat = First(o, "CatalogReference");
        if (cat != null) ped = Attr(cat, "entryName").ToLowerInvariant().Contains("ped");
        GameObject go = null;
        if (ped)
        {
            var assets = Resources.Load<PedestrianAvatars>("PedestrianAvatars");
            if (assets != null && assets.Prefabs.Length > 0) go = Instantiate(assets.Prefabs[(_actors.Count + 4) % assets.Prefabs.Length]);
            if (go != null)
            {
                foreach (var ai in go.GetComponentsInChildren<AIPedestrian>()) ai.enabled = false;
                foreach (var tr in go.GetComponentsInChildren<UnityStandardAssets.Utility.WaypointProgressTracker>()) tr.enabled = false;
            }
        }
        else
        {
            var prefabs = NetworkingManager.Instance != null && NetworkingManager.Instance.AICars != null ? NetworkingManager.Instance.AICars.Prefabs : null;
            if (prefabs != null && prefabs.Length > 0) go = Instantiate(prefabs[_actors.Count % prefabs.Length].gameObject);
            if (go != null)
            {
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>()) if (!(mb is PlayerAvatar)) mb.enabled = false;
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; rb.useGravity = false; }
                foreach (var wc in go.GetComponentsInChildren<WheelCollider>()) wc.enabled = false;
            }
        }
        if (go == null) go = GameObject.CreatePrimitive(ped ? PrimitiveType.Capsule : PrimitiveType.Cube);
        go.name = "OpenSCENARIO " + name;
        go.transform.SetParent(transform, true);
        var anim = go.GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = true;
        _actors[name] = new Actor { Name = name, Pedestrian = ped, T = go.transform, Anim = anim };
    }

    void Update()
    {
        if (_actors.Count == 0) return;
        float t = Time.time - _start;
        foreach (var e in _events)
        {
            if (e.Fired || !e.Supported || t < e.At) continue;
            e.Fired = true;
            Apply(e, t);
        }
        float dt = Time.deltaTime;
        foreach (var a in _actors.Values)
        {
            if (a.T == null) continue;
            a.Speed = a.Rate > 0f ? Mathf.MoveTowards(a.Speed, a.Target, a.Rate * dt) : a.Target;
            a.T.position += a.T.forward * a.Speed * dt;
            if (a.Anim != null && a.Pedestrian) { a.Anim.SetFloat("Speed", Mathf.Clamp01(a.Speed / 1.4f)); a.Anim.speed = a.Speed > 1.4f ? Mathf.Min(2f, a.Speed / 1.4f) : 1f; }
        }
    }

    void Apply(Event e, float t)
    {
        if (!_actors.TryGetValue(e.Entity, out var a)) { Log("unknown entity", e.Entity, e.Name); return; }
        var tp = First(e.Action, "WorldPosition");
        if (First(e.Action, "TeleportAction") != null && tp != null)
        {
            float x = F(Attr(tp, "x")), y = F(Attr(tp, "y")), z = F(Attr(tp, "z")), h = F(Attr(tp, "h"));
            var pos = new Vector3(x - _offset.x, z, y - _offset.y);
            // ground under it
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
            a.T.SetPositionAndRotation(pos, Quaternion.Euler(0f, 90f - h * Mathf.Rad2Deg, 0f));
            Log("teleport", a.Name, $"{x.ToString("F1", Ci)},{y.ToString("F1", Ci)},h={h.ToString("F2", Ci)}");
            return;
        }
        var speed = First(e.Action, "SpeedAction");
        if (speed != null)
        {
            var abs = First(speed, "AbsoluteTargetSpeed");
            var dyn = First(speed, "SpeedActionDynamics");
            if (abs == null) { Log("unsupported", a.Name, "relative target speed"); return; }
            a.Target = F(Attr(abs, "value"));
            a.Rate = 0f;
            if (dyn != null && Attr(dyn, "dynamicsShape") != "step")
            {
                float v = F(Attr(dyn, "value")), dv = Mathf.Abs(a.Target - a.Speed);
                switch (Attr(dyn, "dynamicsDimension"))
                {
                    case "rate": a.Rate = Mathf.Abs(v); break;
                    case "time": a.Rate = v > 0f ? dv / v : 0f; break;
                    case "distance": a.Rate = v > 0f ? Mathf.Abs(a.Target * a.Target - a.Speed * a.Speed) / (2f * v) : 0f; break;
                }
            }
            if (e.Name == "Init") { a.Speed = a.Target; a.Rate = 0f; }
            Log("speed", a.Name, $"{a.Target.ToString("F2", Ci)} m/s{(a.Rate > 0f ? " at " + a.Rate.ToString("F2", Ci) + " m/s2" : "")}");
            return;
        }
        Log("unsupported", a.Name, e.Action.FirstChild != null ? e.Action.FirstChild.Name : "?");
    }

    void Log(string what, string entity, string detail)
    {
        try
        {
            if (_log == null)
            {
                Directory.CreateDirectory("ExperimentLogs");
                _log = new StreamWriter($"ExperimentLogs/OpenScenario-{NetworkingManager.LogPrefix}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv") { AutoFlush = true };
                _log.WriteLine("time;event;entity;detail");
            }
            _log.WriteLine($"{(Time.time - _start).ToString("F2", Ci)};{what};{entity};\"{detail}\"");
        }
        catch (Exception) { }
    }

    void OnDestroy() => _log?.Dispose();
}
