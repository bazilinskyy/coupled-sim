using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// -debugCollisions: every moving agent (the traffic's vehicles and riders, the participants, the pedestrians and the
/// animals on the ground) checked every 0.1 s for contact with any other agent and with anything fixed (walls, posts,
/// trees, parked cars, benches, fences): a vehicle's footprint (its length and width, 5 cm more all round, from 0.3 to
/// 1.5 m above its base, so not the road, the kerbs or the pavement it stands on), a rider's 1.8 by 0.7 m, a
/// pedestrian's circle of 0.25 m and an animal's of 0.3 m. The first contact of each pair is logged with where and how
/// fast; the contact time by kind every 10 s and at the end ("CollisionAudit: ...").
/// </summary>
public class CollisionAudit : MonoBehaviour
{
    enum Kind { Vehicle, Rider, Pedestrian, Animal }

    struct Agent
    {
        public Transform t; public Kind kind; public Vector3 pos, fwd; public float hl, hw; public string name;
        public float speed;
    }

    static readonly string[] GroundPrefixes = { "Roads", "Road ", "BGT", "Ground", "Terrain", "Zebra", "Kerbs", "Pavements", "Cycle", "Water", "Quay", "Bridge", "Deck", "Grass", "Paving", "Tram", "Rail", "Manhole", "Puddle", "Patch", "Markings", "Lane" };

    readonly List<Agent> _agents = new List<Agent>();
    readonly Dictionary<string, float> _seconds = new Dictionary<string, float>();
    readonly Dictionary<string, int> _pairs = new Dictionary<string, int>();
    readonly HashSet<string> _seen = new HashSet<string>();
    readonly Dictionary<Transform, Vector3> _lastPos = new Dictionary<Transform, Vector3>();
    readonly Collider[] _hits = new Collider[32];
    float _next, _nextReport = 10f, _lastCheck;
    int _logged;
    const float Every = 0.1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Array.IndexOf(CommandLine.Args, "-debugCollisions") < 0 || FindAnyObjectByType<CollisionAudit>() != null) return;
        var go = new GameObject("Collision audit");
        DontDestroyOnLoad(go);
        go.AddComponent<CollisionAudit>();
    }

    void FixedUpdate()
    {
        if (Time.time < _next) return;
        float dt = _lastCheck > 0f ? Time.time - _lastCheck : Every;
        _lastCheck = Time.time; _next = Time.time + Every;
        Collect(dt);
        for (int i = 0; i < _agents.Count; i++)
        {
            var a = _agents[i];
            for (int j = i + 1; j < _agents.Count; j++)
            {
                var b = _agents[j];
                if ((a.pos - b.pos).sqrMagnitude > 15f * 15f || Mathf.Abs(a.pos.y - b.pos.y) > 2.5f) continue;
                // (two pedestrians walking together: touching only if they overlap)
                float pad = a.kind == Kind.Pedestrian && b.kind == Kind.Pedestrian ? -0.1f : 0.05f;
                if (Touch(a, b, pad))
                {
                    // (how deep: still touching 15 cm or 30 cm inside the bodies)
                    string depth = Touch(a, b, pad - 0.6f) ? "over 30 cm" : Touch(a, b, pad - 0.3f) ? "15-30 cm" : "under 15 cm";
                    Record(a, b.name, depth, Label(a.kind, b.kind), b.speed, dt);
                }
            }
            Statics(a, dt);
            if (a.kind == Kind.Pedestrian) OnRoad(a, dt);
        }
        if (Time.time >= _nextReport) { _nextReport += 10f; Report(); }
    }

    void OnDestroy() => Report();

    void Collect(float dt)
    {
        _agents.Clear();
        foreach (var pa in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
        {
            if (pa == null || !pa.gameObject.activeInHierarchy || pa.Type == AvatarType.Drone) continue;
            var t = pa.transform;
            if (pa.Type == AvatarType.Driver)
            {
                bool rider = t.GetComponent<TwoWheelerController>() != null;
                t.TryGetComponent<TrafficCar>(out var tc);
                float hl = rider ? 0.9f : (tc != null ? tc.VehicleLength : 4.5f) / 2f, hw = rider ? 0.35f : (tc != null ? tc.VehicleWidth : 1.8f) / 2f;
                if (!rider && tc == null) BodySize(t, ref hl, ref hw);
                Add(t, rider ? Kind.Rider : Kind.Vehicle, hl, hw, pa.name, dt);
            }
            else Add(t, Kind.Pedestrian, 0.25f, 0.25f, pa.name, dt);
        }
        foreach (var ped in FindObjectsByType<AIPedestrian>(FindObjectsSortMode.None))
            if (ped != null && ped.isActiveAndEnabled && ped.GetComponent<PlayerAvatar>() == null) Add(ped.transform, Kind.Pedestrian, 0.25f, 0.25f, ped.name, dt);
        foreach (var an in AnimalAgent.All)
            if (an != null && an.Current != AnimalAgent.State.Fly && an.Species != AnimalSpecies.Pigeon) Add(an.transform, Kind.Animal, an.BodyRadius, an.BodyRadius, an.name, dt);
    }

    void Add(Transform t, Kind k, float hl, float hw, string name, float dt)
    {
        var p = t.position;
        float v = _lastPos.TryGetValue(t, out var last) ? Flat(p - last).magnitude / Mathf.Max(dt, 1e-3f) : 0f;
        _lastPos[t] = p;
        var f = t.forward; f.y = 0f; f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        _agents.Add(new Agent { t = t, kind = k, pos = p, fwd = f, hl = hl, hw = hw, name = name, speed = v });
    }

    /// <summary>A participant's car without TrafficCar: its size from its colliders.</summary>
    static void BodySize(Transform t, ref float hl, ref float hw)
    {
        var b = new Bounds(); bool any = false;
        foreach (var c in t.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || c is WheelCollider) continue;
            var lb = c.bounds; // (world: rotated cars come out a little large)
            var lc = t.InverseTransformPoint(lb.center);
            var e = t.InverseTransformVector(lb.extents);
            var box = new Bounds(lc, new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z)) * 2f);
            if (!any) { b = box; any = true; } else b.Encapsulate(box);
        }
        if (any && b.extents.z > 1f && b.extents.z < 9f) { hl = b.extents.z; hw = Mathf.Clamp(b.extents.x, 0.6f, 1.4f); }
    }

    static string Label(Kind a, Kind b)
    {
        if (b < a) (a, b) = (b, a);
        return $"{a.ToString().ToLowerInvariant()}-{b.ToString().ToLowerInvariant()}";
    }

    static bool Touch(Agent a, Agent b, float pad)
    {
        bool ra = a.kind == Kind.Pedestrian || a.kind == Kind.Animal, rb = b.kind == Kind.Pedestrian || b.kind == Kind.Animal;
        if (ra && rb) return Flat(a.pos - b.pos).magnitude < a.hw + b.hw + pad;
        if (ra) return TrafficCar.BodyDist(a.pos, b.pos, b.fwd, b.hl + pad, b.hw + pad) < a.hw;
        if (rb) return TrafficCar.BodyDist(b.pos, a.pos, a.fwd, a.hl + pad, a.hw + pad) < b.hw;
        return BoxesOverlap(a, b, pad);
    }

    static bool BoxesOverlap(Agent a, Agent b, float pad)
    {
        Vector2 ca = new Vector2(a.pos.x, a.pos.z), cb = new Vector2(b.pos.x, b.pos.z);
        Vector2 fa = new Vector2(a.fwd.x, a.fwd.z), fb = new Vector2(b.fwd.x, b.fwd.z);
        Vector2 ra = new Vector2(fa.y, -fa.x), rb = new Vector2(fb.y, -fb.x);
        float ahl = a.hl + pad / 2f, ahw = a.hw + pad / 2f, bhl = b.hl + pad / 2f, bhw = b.hw + pad / 2f;
        foreach (var ax in new[] { fa, ra, fb, rb })
        {
            float pa = Mathf.Abs(Vector2.Dot(fa, ax)) * ahl + Mathf.Abs(Vector2.Dot(ra, ax)) * ahw;
            float pb = Mathf.Abs(Vector2.Dot(fb, ax)) * bhl + Mathf.Abs(Vector2.Dot(rb, ax)) * bhw;
            if (Mathf.Abs(Vector2.Dot(cb - ca, ax)) > pa + pb) return false;
        }
        return true;
    }

    void Statics(Agent a, float dt)
    {
        int n;
        if (a.kind == Kind.Pedestrian || a.kind == Kind.Animal)
            n = Physics.OverlapCapsuleNonAlloc(a.pos + Vector3.up * (0.3f + a.hw), a.pos + Vector3.up * Mathf.Max(0.3f + a.hw, (a.kind == Kind.Animal ? 0.6f : 1.5f) - a.hw), a.hw, _hits, ~0, QueryTriggerInteraction.Ignore);
        else
            n = Physics.OverlapBoxNonAlloc(a.pos + Vector3.up * 0.9f, new Vector3(a.hw + 0.05f, 0.6f, a.hl + 0.05f), _hits, Quaternion.LookRotation(a.fwd, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = _hits[i];
            if (c == null || c.isTrigger) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;   // (a vehicle: the agent check)
            if (c.transform.IsChildOf(a.t)) continue;
            if (a.t.TryGetComponent<ArticulatedTrailer>(out var art) && art.Trailer != null && c.transform.IsChildOf(art.Trailer)) continue;   // (its own rear section)
            if (c.GetComponentInParent<PlayerAvatar>() != null || c.GetComponentInParent<AnimalAgent>() != null || c.GetComponentInParent<AIPedestrian>() != null) continue;
            string name = c.name;
            if (GroundPrefixes.Any(g => name.StartsWith(g, StringComparison.Ordinal))) continue;
            // (grass and planted ground: animals and pedestrians walk on it, its relief and all)
            if ((a.kind == Kind.Animal || a.kind == Kind.Pedestrian) && (name.StartsWith("Green", StringComparison.Ordinal) || name.StartsWith("BGT green", StringComparison.Ordinal))) continue;
            // (a surface under it, as a quay or a deck the name does not tell: nothing of it above the box's bottom)
            if (c.bounds.max.y < a.pos.y + 0.35f) continue;
            string what = (c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name) + Part(a, c);
            Record(a, what, StaticDepth(a, c), $"{a.kind.ToString().ToLowerInvariant()}-fixed", 0f, dt);
            break;
        }
    }

    /// <summary>A pedestrian of the map's crowd on a carriageway (the road surface under it) while not crossing the road:
    /// "pedestrian-on-road", with the place (people walked down a dual carriageway in Ho Chi Minh City).</summary>
    void OnRoad(Agent a, float dt)
    {
        if (!a.t.TryGetComponent<AIPedestrian>(out var ped) || ped.IsCrossing) return;
        // (the highest surface under it that is not its own body)
        int n = Physics.RaycastNonAlloc(a.pos + Vector3.up * 1.0f, Vector3.down, _down, 2.5f, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit best = default; float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = _down[i].collider;
            if (c == null || c.transform.IsChildOf(a.t) || c.attachedRigidbody != null) continue;
            if (_down[i].distance < bestD) { bestD = _down[i].distance; best = _down[i]; }
        }
        if (bestD < float.MaxValue && best.collider.name.StartsWith("Roads", StringComparison.Ordinal))
            Record(a, "the carriageway", "under 15 cm", "pedestrian-on-road", 0f, dt);
    }

    /// <summary>Which part of a vehicle's footprint a fixed object touches (front, middle or rear third; left or right
    /// half): " at its front-left" and the like.</summary>
    string Part(Agent a, Collider c)
    {
        if (a.kind == Kind.Pedestrian || a.kind == Kind.Animal) return "";
        string along = "", side = "";
        var rot = Quaternion.LookRotation(a.fwd, Vector3.up); var right = Vector3.Cross(Vector3.up, a.fwd);
        bool In(Vector3 centre, Vector3 half)
        {
            int n = Physics.OverlapBoxNonAlloc(centre + Vector3.up * 0.9f, half, _deep, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (_deep[i] == c) return true;
            return false;
        }
        float third = (a.hl + 0.05f) * 2f / 3f;
        foreach (var (k, label) in new[] { (1, "front"), (0, "middle"), (-1, "rear") })
            if (In(a.pos + a.fwd * (k * third), new Vector3(a.hw + 0.05f, 0.6f, third / 2f))) { along = label; break; }
        bool l = In(a.pos - right * ((a.hw + 0.05f) / 2f), new Vector3((a.hw + 0.05f) / 2f, 0.6f, a.hl + 0.05f));
        bool r = In(a.pos + right * ((a.hw + 0.05f) / 2f), new Vector3((a.hw + 0.05f) / 2f, 0.6f, a.hl + 0.05f));
        side = l && !r ? "-left" : r && !l ? "-right" : "";
        return $" at its {along}{side}";
    }

    /// <summary>How deep a fixed object reaches into an agent's footprint: still in it 15 cm or 30 cm inside its sides.</summary>
    string StaticDepth(Agent a, Collider c)
    {
        foreach (var (inset, label) in new[] { (0.3f, "over 30 cm"), (0.15f, "15-30 cm") })
        {
            int n = a.kind == Kind.Pedestrian || a.kind == Kind.Animal
                ? Physics.OverlapSphereNonAlloc(a.pos + Vector3.up * 0.9f, Mathf.Max(0.01f, a.hw - inset), _deep, ~0, QueryTriggerInteraction.Ignore)
                : Physics.OverlapBoxNonAlloc(a.pos + Vector3.up * 0.9f, new Vector3(Mathf.Max(0.01f, a.hw - inset), 0.6f, Mathf.Max(0.01f, a.hl - inset)), _deep, Quaternion.LookRotation(a.fwd, Vector3.up), ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (_deep[i] == c) return label;
        }
        return "under 15 cm";
    }
    readonly Collider[] _deep = new Collider[32];
    readonly RaycastHit[] _down = new RaycastHit[8];
    readonly Dictionary<string, (float seconds, string label, Vector3 at)> _pairSeconds = new Dictionary<string, (float, string, Vector3)>();

    void Record(Agent a, string other, string depth, string label, float otherSpeed, float dt)
    {
        if (depth != "under 15 cm") label += " 15 cm+";
        _seconds[label] = (_seconds.TryGetValue(label, out var s) ? s : 0f) + dt;
        string key = a.name + "|" + other;
        float before = _pairSeconds.TryGetValue(key, out var ps) ? ps.seconds : 0f;
        _pairSeconds[key] = (before + dt, label, a.pos);
        if (_seen.Add(key))
        {
            _pairs[label] = (_pairs.TryGetValue(label, out var p) ? p : 0) + 1;
            if (_logged++ < 200)
            {
                string guards = "";
                if (a.t.TryGetComponent<TrafficCar>(out var ta)) guards += $"; its last check: {ta.GuardState}";
                var ob = _agents.Find(x => x.name == other);
                if (ob.t != null && ob.t.TryGetComponent<TrafficCar>(out var tb)) guards += $"; the other's: {tb.GuardState}; heading {Vector3.SignedAngle(a.fwd, ob.fwd, Vector3.up):F0}° to this one, gap {TrafficCarGap(a, ob):F2} m";
                Debug.Log($"CollisionAudit: first contact {label} t={Time.time:F1} {a.name} ({a.speed * 3.6f:F0} km/h) with {other} ({otherSpeed * 3.6f:F0} km/h), {depth} deep, at ({a.pos.x:F1},{a.pos.y:F1},{a.pos.z:F1}){guards}");
            }
        }
    }

    /// <summary>The gap between two footprints (negative: overlapping), along the axis that parts them most.</summary>
    static float TrafficCarGap(Agent a, Agent b)
    {
        Vector2 ca = new Vector2(a.pos.x, a.pos.z), cb = new Vector2(b.pos.x, b.pos.z), fa = new Vector2(a.fwd.x, a.fwd.z), fb = new Vector2(b.fwd.x, b.fwd.z);
        Vector2 ra = new Vector2(fa.y, -fa.x), rb = new Vector2(fb.y, -fb.x);
        var d = cb - ca;
        float Sep(Vector2 ax) => Mathf.Abs(Vector2.Dot(d, ax)) - (Mathf.Abs(Vector2.Dot(fa, ax)) * a.hl + Mathf.Abs(Vector2.Dot(ra, ax)) * a.hw + Mathf.Abs(Vector2.Dot(fb, ax)) * b.hl + Mathf.Abs(Vector2.Dot(rb, ax)) * b.hw);
        return Mathf.Max(Mathf.Max(Sep(fa), Sep(ra)), Mathf.Max(Sep(fb), Sep(rb)));
    }

    void Report()
    {
        if (_seconds.Count == 0) { Debug.Log($"CollisionAudit: t={Time.time:F0} s, {_agents.Count} agents, no contacts"); return; }
        Debug.Log($"CollisionAudit: t={Time.time:F0} s, {_agents.Count} agents, contacts: " + string.Join(", ", _seconds.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {(_pairs.TryGetValue(x.Key, out var np) ? np : 0)} pairs {x.Value:F1} s")));
        // (the pairs in contact longest: what an agent is stuck against, and where)
        var longest = _pairSeconds.Where(x => x.Value.seconds >= 2f).OrderByDescending(x => x.Value.seconds).Take(6).ToList();
        if (longest.Count > 0)
            Debug.Log("CollisionAudit: longest contacts: " + string.Join("; ", longest.Select(x => $"{x.Key.Replace("|", " with ")} ({x.Value.label}) {x.Value.seconds:F1} s, last at ({x.Value.at.x:F1},{x.Value.at.y:F1},{x.Value.at.z:F1})")));
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
