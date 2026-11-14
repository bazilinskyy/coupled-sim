using System;
using System.Collections.Generic;
using System.Linq;
using CoupledSim.Maps;
using UnityEngine;
using V2 = System.Numerics.Vector2;

/// <summary>
/// Working traffic lights where OpenStreetMap has them (highway=traffic_signals, and crossings with
/// crossing=traffic_signals): a signal head over each approach lane with a stop line, pedestrian lights on both kerbs
/// of signalised crossings, and one controller per junction (signals within 40 m of each other). The approaches are
/// split into two phases by direction (the main road and the crossing road); a junction with one direction only (a
/// signalised crossing in a street) alternates the cars with the pedestrians. The lights are ordinary
/// <see cref="CarTrafficLight"/> and <see cref="PedestrianTrafficLight"/> objects run by a <see cref="TrafficLightsSystem"/>,
/// so AI cars stop at them, the host synchronises them, logs record them and replays show them.
/// <c>-noSignals</c> leaves them out.
/// </summary>
public partial class OsmMapGenerator
{
    public int SignalHeads, PedestrianSignalHeads, SignalJunctions;
    public TrafficLightsSystem Signals { get; private set; }

    static readonly bool _noSignals = Array.IndexOf(Environment.GetCommandLineArgs(), "-noSignals") >= 0;

    // timing (s): green of the main and the crossing direction, amber, all-red; pedestrians blink before the end
    public const float MainGreen = 24f, SideGreen = 16f, Amber = 3f, AllRed = 2f, PedestrianBlink = 5f;
    const float ClusterRadius = 40f;

    sealed class Head
    {
        public V2 Stop, Travel;
        public float Width;
        public bool OneWay;
        public long Node;
        public int Phase;
    }

    sealed class PedHead
    {
        public V2 Pos, Facing, Road;
        public long Node;
        public int Phase;
    }

    /// <summary>+1 one-way along the way, -1 against it, 0 both ways.</summary>
    static int OneWayDir(OsmWay w) => w.Tag("oneway") == "-1" ? -1 : IsOneWay(w) ? 1 : 0;

    void PlaceTrafficSignals(Transform root, List<(OsmWay way, List<V2> line, float width)> roadWays, Material marking)
    {
        if (_noSignals || _assets == null || _assets.CarSignal == null) return;
        // the car roads at each node, with the node's index in the way
        var at = new Dictionary<long, List<(OsmWay w, List<V2> pts, int i, float width)>>();
        foreach (var (w, _, width) in roadWays)
        {
            var ids = w.NodeIds.Where(_pos.ContainsKey).ToList();
            var pts = ids.Select(id => _pos[id]).ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!at.TryGetValue(ids[i], out var list)) at[ids[i]] = list = new List<(OsmWay, List<V2>, int, float)>();
                list.Add((w, pts, i, width));
            }
        }
        int Degree(long id) => at.TryGetValue(id, out var l) ? l.Sum(e => (e.i > 0 ? 1 : 0) + (e.i < e.pts.Count - 1 ? 1 : 0)) : 0;
        float JunctionWidth(long id) => at.TryGetValue(id, out var l) ? l.Max(e => e.width) : 6f;
        bool Signalised(OsmNode n) => n.Tag("crossing") == "traffic_signals" || n.Tag("crossing:signals") == "yes" || n.Tag("traffic_signals") == "crossing";

        var signalNodes = at.Keys.Where(id => _data.Nodes.TryGetValue(id, out var n) && n.Tag("highway") == "traffic_signals" && Inside(_pos[id])).OrderBy(id => id).ToList();
        var crossingNodes = at.Keys.Where(id => _data.Nodes.TryGetValue(id, out var n) && Inside(_pos[id]) &&
            ((n.Tag("highway") == "crossing" && Signalised(n)) || (n.Tag("highway") == "traffic_signals" && Signalised(n)))).OrderBy(id => id).ToList();
        if (signalNodes.Count == 0 && crossingNodes.Count == 0) return;
        var junctions = at.Keys.Where(id => Degree(id) >= 3).Select(id => _pos[id]).ToList();

        // ---- the signal heads: one per approach
        var heads = new List<Head>();
        foreach (var s in signalNodes)
        {
            var node = _data.Nodes[s];
            var p = _pos[s];
            bool junction = Degree(s) >= 3;
            var dirTag = node.Tag("traffic_signals:direction") ?? node.Tag("direction");
            // a signal before a junction (the usual Dutch and German mapping): for the traffic towards that junction
            V2? nearJunction = null;
            if (!junction && dirTag == null)
                foreach (var j in junctions)
                    if (V2.Distance(j, p) < 35f && V2.Distance(j, p) > 0.5f && (nearJunction == null || V2.Distance(j, p) < V2.Distance(nearJunction.Value, p))) nearJunction = j;
            foreach (var (w, pts, i, width) in at[s])
            {
                int ow = OneWayDir(w);
                foreach (int from in new[] { -1, 1 })   // -1: traffic coming along the way (from the previous node), +1: against it
                {
                    int q = i + from;
                    if (q < 0 || q >= pts.Count) continue;
                    if ((from < 0 && ow < 0) || (from > 0 && ow > 0)) continue;            // against a one-way street
                    if (dirTag == "forward" && from > 0) continue;
                    if (dirTag == "backward" && from < 0) continue;
                    var travel = V2.Normalize(p - pts[q]);
                    if (nearJunction != null && V2.Dot(travel, nearJunction.Value - p) <= 0) continue;
                    // the stop line: before the junction's crossing roads, else at the signal
                    float back = junction ? JunctionWidth(s) / 2f + 2f : 1f;
                    var stop = Walk(pts, i, from, back, out var t);
                    heads.Add(new Head { Stop = stop, Travel = t, Width = width, OneWay = ow != 0, Node = s });
                }
            }
        }
        // the same approach signalled twice (on the junction node and before it): keep the one before it
        for (int a = heads.Count - 1; a >= 0; a--)
            for (int b = 0; b < heads.Count; b++)
                if (a != b && Degree(heads[a].Node) >= 3 && Degree(heads[b].Node) < 3 && V2.Dot(heads[a].Travel, heads[b].Travel) > 0.8f &&
                    V2.Distance(heads[a].Stop, heads[b].Stop) < 35f && Math.Abs(Cross(heads[a].Travel, heads[b].Stop - heads[a].Stop)) < 4f)
                { heads.RemoveAt(a); break; }

        // ---- pedestrian lights on both kerbs of signalised crossings
        var peds = new List<PedHead>();
        foreach (var c in crossingNodes)
        {
            var (w, pts, i, width) = at[c][0];
            var a = pts[Math.Max(0, i - 1)]; var b = pts[Math.Min(pts.Count - 1, i + 1)];
            if (V2.DistanceSquared(a, b) < 1e-4f) continue;
            var dir = V2.Normalize(b - a); var nrm = new V2(dir.Y, -dir.X);
            foreach (var side in new[] { -1, 1 })
                peds.Add(new PedHead { Pos = _pos[c] + nrm * side * (width / 2f + 0.5f) + dir * 2.6f, Facing = -nrm * side, Road = dir, Node = c });
        }

        // ---- controllers: signals within 40 m of each other work together
        var all = signalNodes.Concat(crossingNodes).Distinct().OrderBy(id => id).ToList();
        var parent = all.ToDictionary(id => id, id => id);
        long Find(long x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        for (int a = 0; a < all.Count; a++)
            for (int b = a + 1; b < all.Count; b++)
                if (V2.Distance(_pos[all[a]], _pos[all[b]]) < ClusterRadius)
                {
                    long ra = Find(all[a]), rb = Find(all[b]);
                    if (ra != rb) parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
                }

        var lightsRoot = new GameObject("Traffic signals").transform;
        lightsRoot.SetParent(root, false);
        var control = new GameObject("Traffic signal controllers");
        control.transform.SetParent(root, false);
        Signals = control.AddComponent<TrafficLightsSystem>();
        var managers = new List<TrafficLightsManager>();
        var carLights = new List<CarTrafficLight>();
        var pedLights = new List<PedestrianTrafficLight>();
        var stopLines = new MeshBuilder();
        bool redAmber = _map.Country == "United Kingdom" || _map.Country == "Germany" || _map.Country == "Austria" || _map.Country == "Poland";

        foreach (var cluster in all.GroupBy(Find).OrderBy(g => g.Key))
        {
            var members = new HashSet<long>(cluster);
            var ch = heads.Where(h => members.Contains(h.Node)).ToList();
            var cp = peds.Where(h => members.Contains(h.Node)).ToList();
            if (ch.Count == 0 && cp.Count == 0) continue;
            // the main direction: the widest approach (ties: the lowest node); heads within 45° of it are phase 0
            var main = ch.Count > 0 ? ch.OrderByDescending(h => h.Width).ThenBy(h => h.Node).First().Travel
                                    : cp.OrderBy(h => h.Node).First().Road;
            foreach (var h in ch) h.Phase = Math.Abs(V2.Dot(h.Travel, main)) >= 0.7071f ? 0 : 1;
            bool twoPhase = ch.Any(h => h.Phase == 1);
            // pedestrians cross a road while its direction is red: during the other phase, or their own phase
            foreach (var h in cp) h.Phase = Math.Abs(V2.Dot(h.Road, main)) >= 0.7071f ? 1 : 0;

            var go = new GameObject($"Signals {cluster.Key}");
            go.transform.SetParent(control.transform, false);
            CarSection Section(string name, IEnumerable<CarTrafficLight> l)
            {
                var sgo = new GameObject(name); sgo.transform.SetParent(go.transform, false);
                var sec = sgo.AddComponent<CarSection>(); sec.carStreetLights = l.ToArray(); return sec;
            }
            PedestrianSection PedSection(string name, IEnumerable<PedestrianTrafficLight> l)
            {
                var sgo = new GameObject(name); sgo.transform.SetParent(go.transform, false);
                var sec = sgo.AddComponent<PedestrianSection>(); sec.pedestrianLights = l.ToArray(); return sec;
            }

            var byPhase = new[] { new List<CarTrafficLight>(), new List<CarTrafficLight>() };
            foreach (var h in ch)
            {
                var light = PlaceCarSignal(lightsRoot, h, stopLines);
                if (light == null) continue;
                byPhase[h.Phase].Add(light);
                carLights.Add(light);
            }
            var pedByPhase = new[] { new List<PedestrianTrafficLight>(), new List<PedestrianTrafficLight>() };
            foreach (var h in cp)
            {
                var light = PlacePedestrianSignal(lightsRoot, h);
                if (light == null) continue;
                // one-direction junctions: all pedestrians walk while the cars wait
                pedByPhase[twoPhase ? h.Phase : 1].Add(light);
                pedLights.Add(light);
            }
            var A = Section("Phase A", byPhase[0]); var B = Section("Phase B", byPhase[1]);
            var PA = PedSection("Pedestrians with phase A", pedByPhase[0]); var PB = PedSection("Pedestrians with phase B", pedByPhase[1]);
            var none = new CarSection[0]; var noPed = new PedestrianSection[0];
            TrafficLightEvent E(string name, float dt, LightState state, CarSection[] cars, PedestrianSection[] ped) =>
                new TrafficLightEvent { name = name, deltaTime = dt, state = state, carSections = cars, pedestrianSections = ped };
            float greenB = twoPhase ? SideGreen : 10f;   // one direction: the pedestrians' green
            var initial = new[]
            {
                E("A green", 0, LightState.GREEN, new[] { A }, new[] { PA }),
                E("B red", 0, LightState.RED, new[] { B }, new[] { PB }),
            };
            var events = new List<TrafficLightEvent>
            {
                E("A pedestrians blink", MainGreen - PedestrianBlink, LightState.BLINK_GREEN, none, new[] { PA }),
                E("A amber", PedestrianBlink, LightState.YELLOW, new[] { A }, noPed),
                E("A pedestrians red", 0, LightState.RED, none, new[] { PA }),
                E("A red", Amber, LightState.RED, new[] { A }, noPed),
            };
            if (redAmber && twoPhase) events.Add(E("B red and amber", AllRed - 0.5f, LightState.RED_AND_YELLOW, new[] { B }, noPed));
            events.Add(E("B green", redAmber && twoPhase ? 0.5f + 1.5f : AllRed, LightState.GREEN, new[] { B }, new[] { PB }));
            events.Add(E("B pedestrians blink", greenB - PedestrianBlink, LightState.BLINK_GREEN, none, new[] { PB }));
            events.Add(E("B amber", PedestrianBlink, LightState.YELLOW, new[] { B }, noPed));
            events.Add(E("B pedestrians red", 0, LightState.RED, none, new[] { PB }));
            events.Add(E("B red", Amber, LightState.RED, new[] { B }, noPed));
            if (redAmber) events.Add(E("A red and amber", AllRed - 0.5f, LightState.RED_AND_YELLOW, new[] { A }, noPed));
            events.Add(E("A green", redAmber ? 2f : AllRed, LightState.GREEN, new[] { A }, new[] { PA }));
            var manager = go.AddComponent<TrafficLightsManager>();
            // junctions do not all switch at once: a start offset from the junction's node (the same for every agent)
            manager.Configure(initial, events.ToArray(), (float)(((ulong)cluster.Key * 2654435761UL) % 1000UL) / 1000f * (MainGreen - PedestrianBlink));
            managers.Add(manager);
            SignalJunctions++;
        }
        Emit(root.Find("World") ?? root, "Stop lines", stopLines, marking, false);
        Signals.LightManagers = managers.ToArray();
        Signals.CarLights = carLights.ToArray();
        Signals.PedestrianLights = pedLights.ToArray();
        SignalHeads = carLights.Count;
        PedestrianSignalHeads = pedLights.Count;
    }

    static float Cross(V2 a, V2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>The point <paramref name="dist"/> metres from node i towards the side the traffic comes from, and the travel direction there.</summary>
    static V2 Walk(List<V2> pts, int i, int from, float dist, out V2 travel)
    {
        var p = pts[i];
        int k = i;
        travel = V2.Normalize(pts[i] - pts[i + from]);
        while (dist > 0)
        {
            int q = k + from;
            if (q < 0 || q >= pts.Count) break;
            float seg = V2.Distance(pts[k], pts[q]);
            travel = V2.Normalize(pts[k] - pts[q]);
            if (seg >= dist) return pts[k] - travel * dist;
            dist -= seg; k = q; p = pts[k];
        }
        return p;
    }

    // the head hangs over the approach from a mast arm 4.25 m long, its pole beside the road (right, or left in left-hand traffic)
    CarTrafficLight PlaceCarSignal(Transform parent, Head h, MeshBuilder stopLines)
    {
        var t = h.Travel;
        var right = new V2(t.Y, -t.X) * (_map.LeftHandTraffic ? -1f : 1f);
        var pole = h.Stop + right * (h.Width / 2f + 0.6f);
        var head = pole - right * 4.25f;
        float ground = GroundAt(pole, 0.02f);
        var go = UnityEngine.Object.Instantiate(_assets.CarSignal, parent);
        go.name = $"Signal {h.Node}";
        // the prefab shines along +x and its arm runs along +z to the pole: +x against the traffic, +z to the right-hand
        // kerb; mirrored for left-hand traffic, where the pole stands on the left
        var n = new V2(t.Y, -t.X);
        go.transform.SetPositionAndRotation(new Vector3(head.X, ground + 5.0f, head.Y), Quaternion.LookRotation(new Vector3(n.X, 0, n.Y), Vector3.up));
        if (_map.LeftHandTraffic) go.transform.localScale = new Vector3(1f, 1f, -1f);
        // the stop line across the lanes of this approach
        float from = h.OneWay ? -h.Width / 2f + 0.25f : 0.1f, to = h.Width / 2f - 0.25f;
        if (_map.LeftHandTraffic) { var f = from; from = -to; to = -f; }
        var a = h.Stop + n * from; var b = h.Stop + n * to;
        stopLines.Quad(P(a, 0.036f), P(a - t * 0.3f, 0.036f), P(b - t * 0.3f, 0.036f), P(b, 0.036f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
        return go.GetComponent<CarTrafficLight>();
    }

    PedestrianTrafficLight PlacePedestrianSignal(Transform parent, PedHead h)
    {
        if (_assets.PedestrianSignal == null) return null;
        float ground = GroundAt(h.Pos, PavementHeight);
        var go = UnityEngine.Object.Instantiate(_assets.PedestrianSignal, parent);
        go.name = $"Pedestrian signal {h.Node}";
        // shines along +x: towards the kerb where people wait
        var x = new Vector3(h.Facing.X, 0, h.Facing.Y);
        var fwd = Vector3.Cross(x, Vector3.up);   // LookRotation(fwd) has right = up × fwd = x
        go.transform.SetPositionAndRotation(new Vector3(h.Pos.X, ground + 2.6f, h.Pos.Y), Quaternion.LookRotation(fwd, Vector3.up));
        return go.GetComponent<PedestrianTrafficLight>();
    }
}
