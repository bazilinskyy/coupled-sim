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
    public int SignalHeads, PedestrianSignalHeads, SignalJunctions, TurnArrowHeads;
    public TrafficLightsSystem Signals { get; private set; }

    static readonly bool _noSignals = Array.IndexOf(CommandLine.Args, "-noSignals") >= 0;
    static readonly bool _debugSignals = Array.IndexOf(CommandLine.Args, "-debugTraffic") >= 0;

    // timing (s): green of the main and the crossing direction, amber, all-red; pedestrians blink before the end
    public const float MainGreen = 24f, SideGreen = 16f, Amber = 3f, AllRed = 2f, PedestrianBlink = 5f, TurnGreen = 7f;
    const float ClusterRadius = 40f;

    sealed class Head
    {
        public V2 Stop, Travel;
        public float Width;
        public bool OneWay;
        public long Node;
        public int Phase;
        /// <summary>The approach has a lane only for turning across the oncoming traffic (left, or right in left-hand traffic): it gets a turn-arrow head and its own phase.</summary>
        public bool TurnLane;
    }

    sealed class PedHead
    {
        public V2 Pos, Facing, Road;
        public long Node;
        public int Side;
        public int Phase;
    }

    /// <summary>+1 one-way along the way, -1 against it, 0 both ways.</summary>
    static int OneWayDir(OsmWay w)
    {
        var ow = w.Tag("oneway");
        if (ow == "-1" || ow == "reverse") return -1;
        if (ow == "no" || ow == "0" || ow == "false") return 0;
        if (ow == "yes" || ow == "1" || ow == "true" || (w.Tag("highway") ?? "").StartsWith("motorway") || w.Tag("junction") == "roundabout") return 1;
        return 0;
    }

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
                    // and before a pedestrian crossing on the approach (at the signal, or between it and the junction):
                    // its stripes run 2 m either side of its node, and the traffic waits in front of them, not on them
                    var ids = w.NodeIds.Where(_pos.ContainsKey).ToList();
                    for (int j = i; j >= 0 && j < ids.Count; j += from)
                    {
                        if (!_data.Nodes.TryGetValue(ids[j], out var cn) || cn.Tag("highway") != "crossing") continue;
                        var cp = _pos[ids[j]];
                        float along = V2.Dot(cp - stop, t);
                        if (along > -2.5f && along < 25f && Math.Abs(Cross(t, cp - stop)) < width / 2f + 1f) stop += t * (along - 2.5f);   // (2.5 m before its node)
                    }
                    heads.Add(new Head { Stop = stop, Travel = t, Width = width, OneWay = ow != 0, Node = s, TurnLane = HasTurnLane(w, from < 0 ? ":forward" : ":backward", ow != 0) });
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
                peds.Add(new PedHead { Pos = _pos[c] + nrm * side * (width / 2f + 0.5f) + dir * 2.6f, Facing = -nrm * side, Road = dir, Node = c, Side = side });
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
            // pedestrians cross a road while the traffic along it is red: the phase other than that of the heads of the
            // approaches along it (within 45°), and with the main direction where no signalled approach runs along it
            // (a side road at a junction signalled one way only: they walk beside the traffic that has green, so a head
            // across the side road, which drivers on the main road see, does not show red to them while they have
            // green); in a signalised crossing of one road only, while its cars wait
            foreach (var h in cp)
            {
                var along = ch.Where(c => Math.Abs(V2.Dot(c.Travel, h.Road)) >= 0.7071f).Select(c => c.Phase).Distinct().ToList();
                h.Phase = along.Count == 1 && along[0] == 1 ? 0 : along.Count > 0 || ch.Count == 0 ? 1 : 0;
            }

            if (_debugSignals)
                Debug.Log($"SignalDebug: junction {cluster.Key} at {_pos[cluster.Key]}: {(twoPhase ? "two phases" : "one direction")}, main {main}; car heads "
                    + string.Join(", ", ch.Select(h => $"{h.Node} travel {h.Travel} phase {h.Phase}{(h.TurnLane ? " +arrow" : "")}"))
                    + "; pedestrian heads " + string.Join(", ", cp.Select(h => $"{h.Node} (degree {Degree(h.Node)}, {at[h.Node].Count} road(s)) road {h.Road} at {h.Pos} facing {h.Facing} phase {h.Phase}")));
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
            var turnByPhase = new[] { new List<CarTrafficLight>(), new List<CarTrafficLight>() };
            foreach (var h in ch)
            {
                var light = PlaceCarSignal(lightsRoot, h, stopLines);
                if (light == null) continue;
                byPhase[h.Phase].Add(light);
                carLights.Add(light);
                if (h.TurnLane && PlaceArrowHead(lightsRoot, h, light) is CarTrafficLight arrowHead) { turnByPhase[h.Phase].Add(arrowHead); carLights.Add(arrowHead); }
                FlatLenses(light.Lenses, TrafficCar.LightFacing(light.transform));
            }
            var pedByPhase = new[] { new List<PedestrianTrafficLight>(), new List<PedestrianTrafficLight>() };
            foreach (var h in cp)
            {
                var light = PlacePedestrianSignal(lightsRoot, h);
                if (light == null) continue;
                // the crossing trigger on the other kerb: its waiting pedestrians face this light
                foreach (var pc in Crossings)
                    if (pc.name == $"Crossing {h.Node} {(h.Side > 0 ? "A" : "B")}") pc.Signal = light;
                pedByPhase[h.Phase].Add(light);
                pedLights.Add(light);
            }
            var A = Section("Phase A", byPhase[0]); var B = Section("Phase B", byPhase[1]);
            var PA = PedSection("Pedestrians with phase A", pedByPhase[0]); var PB = PedSection("Pedestrians with phase B", pedByPhase[1]);
            // the stages of the cycle: each direction straight on (with its pedestrians), then its protected turns
            // across the oncoming traffic when it has turn arrows (the pedestrians wait then)
            bool arrows = turnByPhase[0].Count + turnByPhase[1].Count > 0;
            var stages = new List<(CarSection cars, PedestrianSection peds, float green, string name)>
            {
                (A, PA, arrows ? MainGreen - 6f : MainGreen, "A"),
            };
            if (turnByPhase[0].Count > 0) stages.Add((Section("Phase A turns", turnByPhase[0]), null, TurnGreen, "A turns"));
            stages.Add((B, PB, twoPhase ? (arrows ? SideGreen - 3f : SideGreen) : 10f, "B"));   // one direction: the pedestrians' green
            if (turnByPhase[1].Count > 0) stages.Add((Section("Phase B turns", turnByPhase[1]), null, TurnGreen, "B turns"));
            var none = new CarSection[0]; var noPed = new PedestrianSection[0];
            TrafficLightEvent E(string name, float dt, LightState state, CarSection[] cars, PedestrianSection[] ped) =>
                new TrafficLightEvent { name = name, deltaTime = dt, state = state, carSections = cars, pedestrianSections = ped };
            PedestrianSection[] Peds(PedestrianSection p) => p != null ? new[] { p } : noPed;
            var initial = new List<TrafficLightEvent> { E(stages[0].name + " green", 0, LightState.GREEN, new[] { stages[0].cars }, Peds(stages[0].peds)) };
            for (int k = 1; k < stages.Count; k++) initial.Add(E(stages[k].name + " red", 0, LightState.RED, new[] { stages[k].cars }, Peds(stages[k].peds)));
            var events = new List<TrafficLightEvent>();
            for (int k = 0; k < stages.Count; k++)
            {
                var st = stages[k]; var next = stages[(k + 1) % stages.Count];
                bool carsHere = st.cars.carStreetLights.Length > 0, carsNext = next.cars.carStreetLights.Length > 0;
                if (st.peds != null)
                {
                    events.Add(E(st.name + " pedestrians blink", st.green - PedestrianBlink, LightState.BLINK_GREEN, none, Peds(st.peds)));
                    events.Add(E(st.name + " amber", PedestrianBlink, LightState.YELLOW, new[] { st.cars }, noPed));
                    events.Add(E(st.name + " pedestrians red", 0, LightState.RED, none, Peds(st.peds)));
                }
                else events.Add(E(st.name + " amber", st.green, LightState.YELLOW, new[] { st.cars }, noPed));
                events.Add(E(st.name + " red", Amber, LightState.RED, new[] { st.cars }, noPed));
                if (redAmber && carsHere && carsNext)
                {
                    events.Add(E(next.name + " red and amber", AllRed - 0.5f, LightState.RED_AND_YELLOW, new[] { next.cars }, noPed));
                    events.Add(E(next.name + " green", 2f, LightState.GREEN, new[] { next.cars }, Peds(next.peds)));
                }
                else events.Add(E(next.name + " green", AllRed, LightState.GREEN, new[] { next.cars }, Peds(next.peds)));
            }
            var manager = go.AddComponent<TrafficLightsManager>();
            // junctions do not all switch at once: a start offset from the junction's node (the same for every agent)
            manager.Configure(initial.ToArray(), events.ToArray(), (float)(((ulong)cluster.Key * 2654435761UL) % 1000UL) / 1000f * (MainGreen - PedestrianBlink));
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

    /// <summary>Whether turn:lanes (of a direction) has a lane only for turning across the oncoming traffic
    /// (lanes are listed from the left; values like left, slight_left, left;through).</summary>
    public static bool HasTurnLane(OsmWay w, string direction, bool oneWay, bool leftHandTraffic)
    {
        var v = w.Tag("turn:lanes" + direction) ?? (oneWay ? w.Tag("turn:lanes") : null);
        if (string.IsNullOrEmpty(v)) return false;
        string across = leftHandTraffic ? "right" : "left";
        foreach (var lane in v.Split('|'))
        {
            var turns = lane.Split(';');
            if (turns.Any(x => x.Trim().EndsWith(across)) && !turns.Any(x => x.Trim() == "through" || x.Trim() == "none" || x.Trim() == "")) return true;
        }
        return false;
    }

    bool HasTurnLane(OsmWay w, string direction, bool oneWay = false) => HasTurnLane(w, direction, oneWay, _map.LeftHandTraffic);

    static Texture2D _arrowMask;
    static Material _arrowMaskMat, _housingMat;

    /// <summary>A turn-arrow head beside the approach's main head, further over the road on the same mast arm: a
    /// housing with three lenses lit by the main head's lamps, behind black masks that leave an arrow open.</summary>
    CarTrafficLight PlaceArrowHead(Transform parent, Head h, CarTrafficLight main)
    {
        var lenses = main.Lenses;
        if (lenses.Any(l => l == null)) return null;
        var t = h.Travel;
        var right = new V2(t.Y, -t.X) * (_map.LeftHandTraffic ? -1f : 1f);
        var shine = main.transform.right;                         // towards the traffic
        var inward = new Vector3(-right.X, 0f, -right.Y);
        float shift = Mathf.Clamp(h.Width / 4f, 2.2f, 3.2f);
        var go = new GameObject($"Turn arrow signal {h.Node}");
        go.transform.SetParent(parent, false);
        var light = go.AddComponent<CarTrafficLight>();
        var c = (lenses[0].bounds.center + lenses[2].bounds.center) / 2f + inward * shift;
        go.transform.SetPositionAndRotation(c, main.transform.rotation);
        _housingMat ??= Mat("Signal housing", new Color(0.08f, 0.08f, 0.09f), 0.3f);
        Transform Part(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
        {
            var p = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(go.transform, false);
            p.transform.SetPositionAndRotation(pos, rot);
            p.transform.localScale = scale;
            p.GetComponent<MeshRenderer>().sharedMaterial = m;
            return p.transform;
        }
        float span = Vector3.Distance(lenses[0].bounds.center, lenses[2].bounds.center);
        var up = Vector3.up;
        var look = Quaternion.LookRotation(shine, up);
        // housing, and the arm from the main head
        Part(PrimitiveType.Cube, c - shine * 0.12f, new Vector3(0.36f, span + 0.45f, 0.26f), look, _housingMat);
        var armFrom = (lenses[0].bounds.center + inward * 0.2f) + up * 0.35f; var armTo = c + up * (span / 2f + 0.25f);
        Part(PrimitiveType.Cylinder, (armFrom + armTo) / 2f, new Vector3(0.07f, Vector3.Distance(armFrom, armTo) / 2f, 0.07f), Quaternion.FromToRotation(Vector3.up, (armTo - armFrom).normalized), _housingMat);
        // lenses and their arrow masks (the lens lights up behind the arrow)
        if (_arrowMask == null)
        {
            const int n = 64;
            _arrowMask = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Arrow mask", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                    // an arrow pointing to -u (left as seen from in front): a triangular head and a shaft
                    bool head = u < -0.02f && u > -0.36f && Mathf.Abs(v) < (u + 0.36f) * 0.95f;
                    bool shaft = u >= -0.04f && u < 0.32f && Mathf.Abs(v) < 0.09f;
                    px[y * n + x] = head || shaft ? new Color32(0, 0, 0, 0) : new Color32(5, 5, 6, 255);
                }
            _arrowMask.SetPixels32(px); _arrowMask.Apply();
            _arrowMaskMat = Mat("Arrow mask", Color.white, 0.05f, _arrowMask);
            _arrowMaskMat.SetFloat("_AlphaClip", 1f); _arrowMaskMat.SetFloat("_Cutoff", 0.5f); _arrowMaskMat.EnableKeyword("_ALPHATEST_ON");
            _arrowMaskMat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f));
        }
        var arrow = _map.LeftHandTraffic ? CarTrafficLight.TurnArrow.Right : CarTrafficLight.TurnArrow.Left;
        var made = new MeshRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            var at = c + up * (span / 2f - i * span / 2f);
            var lens = Part(PrimitiveType.Cylinder, at, new Vector3(0.22f, 0.02f, 0.22f), Quaternion.FromToRotation(Vector3.up, shine), lenses[i].sharedMaterial);
            made[i] = lens.GetComponent<MeshRenderer>();
            // the quad's back faces the traffic once turned to look along -shine; mirrored for a right arrow
            var q = Part(PrimitiveType.Quad, at + shine * 0.03f, new Vector3(arrow == CarTrafficLight.TurnArrow.Left ? 0.24f : -0.24f, 0.24f, 1f), Quaternion.LookRotation(-shine, up), _arrowMaskMat);
            q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        light.Setup(main, made[0], made[1], made[2]);
        light.Arrow = arrow;
        light.TurnRed();
        TurnArrowHeads++;
        return light;
    }

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
        var ped = go.GetComponent<PedestrianTrafficLight>();
        if (ped != null)
        {
            FigureMasks(go.transform, ped.Lenses, x);   // (before the lenses are flattened: in front of their front faces)
            FlatLenses(ped.Lenses, x);
        }
        return ped;
    }

    /// <summary>
    /// The signal heads' lenses (cylinders of the traffic light model) are lit on their sides too: a head that faces
    /// across the road showed its red or green to the drivers coming along it (a pedestrian head across the
    /// participant's road, red while it had green, looked like a car signal showing red towards it). Each lens is
    /// flattened onto its front face (its depth along the direction it shines cut to 2%), so it is seen only by those it
    /// shines at, as the visors of real signals make it.
    /// </summary>
    static void FlatLenses(IEnumerable<MeshRenderer> lenses, Vector3 shine)
    {
        foreach (var lens in lenses)
        {
            if (lens == null || !lens.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null || lens.transform.childCount > 0) continue;
            var t = lens.transform;
            var ls = t.InverseTransformDirection(shine);   // (the lens's own axis nearest to the shine direction)
            int k = Mathf.Abs(ls.x) >= Mathf.Abs(ls.y) && Mathf.Abs(ls.x) >= Mathf.Abs(ls.z) ? 0 : Mathf.Abs(ls.y) >= Mathf.Abs(ls.z) ? 1 : 2;
            var b = mf.sharedMesh.bounds;
            if (b.size[k] < 1e-4f) continue;
            float front = b.center[k] + Mathf.Sign(ls[k]) * b.extents[k];   // (the front face, in the mesh's coordinates)
            const float f = 0.02f;
            var scale = t.localScale; float sk = scale[k];
            var axis = Vector3.zero; axis[k] = 1f;
            // the front face stays where it was: the lens shrinks towards it
            t.localPosition += t.localRotation * (axis * (front * sk * (1f - f)));
            scale[k] = sk * f; t.localScale = scale;
        }
    }

    static Texture2D[] _figureMask;
    static Material[] _figureMat;

    /// <summary>
    /// Black masks over the lenses of a pedestrian head that leave a figure open, a standing one over the red lens and a
    /// walking one over the green, as real pedestrian signals have: plain red and green discs at 2.6 m on a pole look
    /// like a car's signal to the drivers who see them (a head across a side road faces the main road's drivers).
    /// </summary>
    void FigureMasks(Transform head, MeshRenderer[] lenses, Vector3 shine)
    {
        if (_figureMask == null)
        {
            const int n = 64;
            _figureMask = new Texture2D[2]; _figureMat = new Material[2];
            for (int f = 0; f < 2; f++)
            {
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = f == 0 ? "Standing figure mask" : "Walking figure mask", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int xx = 0; xx < n; xx++)
                    {
                        float u = (xx + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;   // (v up)
                        bool open = (u * u + (v - 0.33f) * (v - 0.33f)) < 0.075f * 0.075f;                  // head
                        if (f == 0)
                        {
                            open |= Mathf.Abs(u) < 0.11f && v > -0.08f && v < 0.24f;                        // body and arms
                            open |= Mathf.Abs(u) < 0.075f && v > -0.4f && v <= -0.08f && Mathf.Abs(u) > 0.012f;   // legs together
                        }
                        else
                        {
                            open |= Mathf.Abs(u - 0.02f) < 0.06f && v > -0.06f && v < 0.24f;               // body
                            float leg1 = u - (-0.06f - (v + 0.06f) * 0.55f), leg2 = u - (0.08f + (v + 0.06f) * -0.55f);
                            open |= v > -0.4f && v <= -0.04f && (Mathf.Abs(leg1) < 0.04f || Mathf.Abs(leg2) < 0.04f);   // legs apart
                            float arm1 = u - (-0.05f - (0.2f - v) * 0.5f), arm2 = u - (0.08f + (0.2f - v) * 0.5f);
                            open |= v > 0.0f && v < 0.2f && (Mathf.Abs(arm1) < 0.03f || Mathf.Abs(arm2) < 0.03f);   // arms swinging
                        }
                        px[y * n + xx] = open ? new Color32(0, 0, 0, 0) : new Color32(5, 5, 6, 255);
                    }
                tex.SetPixels32(px); tex.Apply();
                _figureMask[f] = tex;
                var m = Mat(tex.name, Color.white, 0.05f, tex);
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f));
                _figureMat[f] = m;
            }
        }
        for (int f = 0; f < lenses.Length && f < 2; f++)   // (the red lens above: a standing figure; the green below: a walking one)
        {
            var lens = lenses[f];
            if (lens == null) continue;
            var b = lens.bounds;
            float depth = Mathf.Abs(b.extents.x * shine.x) + Mathf.Abs(b.extents.y * shine.y) + Mathf.Abs(b.extents.z * shine.z);
            float size = b.size.y * 0.95f;   // (the lens's height: its diameter)
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = f == 0 ? "Standing figure" : "Walking figure";
            q.transform.SetParent(head, true);
            // (the quad faces -z: turned to look along -shine, it faces the people the head shines at)
            q.transform.SetPositionAndRotation(b.center + shine * (depth + 0.005f), Quaternion.LookRotation(-shine, Vector3.up));
            q.transform.localScale = Vector3.one;
            q.transform.localScale = new Vector3(size / q.transform.lossyScale.x, size / q.transform.lossyScale.y, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = _figureMat[f];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
