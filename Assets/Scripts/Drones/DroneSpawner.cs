using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CoupledSim.Drones;
using UnityEngine;
using SVec3 = System.Numerics.Vector3;

/// <summary>
/// AI drones in an experiment, each flown by the quadrotor dynamics and flight controller (<see cref="Quadrotor"/>):
/// - delivery drones (industrial, about 6 kg): take off, climb to a cruise height clear of the roofs under their route,
///   fly to a drop point at their cruise speed (accelerating and braking smoothly, <see cref="WaypointGuidance"/>),
///   descend, wait on the ground, and fly back;
/// - patrol drones (consumer): circle the area at about 35 m above its highest roof;
/// - a swarm (consumer): flocks by the model of Vásárhelyi et al. (2018), <see cref="Flocking"/>, migrating round the
///   area at its own height, keeping apart from each other and from the participants' drones.
/// They are made on every agent from the same seed; the host flies them and sends their poses (20 Hz), and logs them to
/// ExperimentLogs/Drones-{date}.csv. Trial parameters: drones (a multiplier of the delivery and patrol counts; 0 = none),
/// droneSwarm (the swarm's size).
/// </summary>
public class DroneSpawner : MonoBehaviour, IExperimentModifier
{
    public int Deliveries = 2;
    public int Patrols = 1;
    public int Swarm = 0;
    [Tooltip("Centre of the area; empty: the first role's spawn point.")]
    public Transform Centre;
    public float Radius = 220f;
    [Tooltip("Height above the highest roof under a route (m).")]
    public float Clearance = 20f;
    public int Seed = 1;
    public float LogInterval = 0.1f;

    public enum Mission { Delivery, Patrol, Swarm }

    public sealed class AiDrone
    {
        public DroneController Drone;
        public Mission Mission;
        public WaypointGuidance Route;
        public float Cruise, WaitUntil;
        public Vector3 Home, Drop;
        public bool Outbound = true;
        public string Phase = "parked";
    }

    public static DroneSpawner Current { get; private set; }
    public readonly List<AiDrone> Spawned = new List<AiDrone>();
    float _multiplier = 1f;
    int _swarmOverride = -1;
    StreamWriter _log;
    float _nextLog, _nextSend, _swarmHeight;
    Vector3 _centre;
    List<Vector3> _swarmRoute;
    int _swarmLeg;
    bool _host;

    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters)
    {
        foreach (var p in experimentParameters)
        {
            var n = p.name.ToLower();
            if (n == "drones" && float.TryParse(p.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) _multiplier = Mathf.Max(0f, m);
            if (n == "droneswarm" && int.TryParse(p.value, out var k)) _swarmOverride = Mathf.Max(0, k);
        }
    }

    void Awake() => Current = this;

    void OnDestroy()
    {
        if (Current == this) Current = null;
        _log?.Dispose();
    }

    void Start()
    {
        _host = NetworkingManager.Instance == null || NetworkingManager.Instance.NetSystem is Host;
        _centre = Centre != null ? Centre.position : transform.position;
        var exp = GetComponentInParent<ExperimentDefinition>();
        if (Centre == null && exp != null && exp.Roles != null && exp.Roles.Length > 0 && exp.Roles[0].SpawnPoint.Point != null)
            _centre = exp.Roles[0].SpawnPoint.Point.position;
        var rng = new System.Random(Seed);
        int deliveries = Mathf.RoundToInt(Deliveries * _multiplier), patrols = Mathf.RoundToInt(Patrols * _multiplier);
        int swarm = _swarmOverride >= 0 ? _swarmOverride : Swarm;
        for (int i = 0; i < deliveries; i++)
        {
            var home = OpenGround(rng, _centre, Radius); var drop = OpenGround(rng, _centre, Radius);
            if (home == null || drop == null) continue;
            var d = Make($"Delivery drone {i}", "Industrial", home.Value, rng);
            var a = new AiDrone { Drone = d, Mission = Mission.Delivery, Home = home.Value, Drop = drop.Value, WaitUntil = 3f + i * 6f };
            // cruise heights a few metres apart, so crossing routes do not meet
            float roofs = RoofHeight(home.Value, drop.Value);
            a.Cruise = roofs + Clearance + 4f * i;
            Debug.Log($"DroneSpawner: {d.name} from {home.Value} to {drop.Value}, roofs up to {roofs:F1} m, cruise at {a.Cruise:F1} m");
            Spawned.Add(a);
        }
        if (patrols > 0)
        {
            float h = RoofHeightAround(_centre, Radius * 0.5f) + 35f;
            for (int i = 0; i < patrols; i++)
            {
                var start = OpenGround(rng, _centre, Radius * 0.4f);
                if (start == null) continue;
                var d = Make($"Patrol drone {i}", "Consumer", start.Value, rng);
                var pts = new List<SVec3> { DroneBody.ToModel(start.Value + Vector3.up * (h + 3f * i - start.Value.y)) };
                float r = Radius * 0.45f, phase = (float)rng.NextDouble() * Mathf.PI * 2f;
                for (int k = 0; k <= 24 * 3; k++)
                {
                    float t = phase + k * Mathf.PI * 2f / 24f;
                    pts.Add(DroneBody.ToModel(new Vector3(_centre.x + Mathf.Cos(t) * r, h + 3f * i, _centre.z + Mathf.Sin(t) * r)));
                }
                Spawned.Add(new AiDrone { Drone = d, Mission = Mission.Patrol, Route = new WaypointGuidance(pts) { CruiseSpeed = 8f }, Phase = "patrol" });
            }
        }
        if (swarm > 0)
        {
            _swarmHeight = RoofHeightAround(_centre, Radius * 0.6f) + 30f;
            _swarmRoute = Enumerable.Range(0, 8).Select(k => _centre + new Vector3(Mathf.Cos(k * Mathf.PI / 4f), 0, Mathf.Sin(k * Mathf.PI / 4f)) * Radius * 0.55f).ToList();
            for (int i = 0; i < swarm; i++)
            {
                var start = OpenGround(rng, _centre, 25f) ?? _centre + new Vector3(i * 2f, 0, 0);
                var d = Make($"Swarm drone {i}", "Consumer", start, rng);
                Spawned.Add(new AiDrone { Drone = d, Mission = Mission.Swarm, Phase = "take-off", WaitUntil = 1f + 0.4f * i });
            }
        }
        if (Spawned.Count > 0) Debug.Log($"DroneSpawner: {Spawned.Count(a => a.Mission == Mission.Delivery)} delivery, {Spawned.Count(a => a.Mission == Mission.Patrol)} patrol and {Spawned.Count(a => a.Mission == Mission.Swarm)} swarm drones");
    }

    DroneController Make(string name, string preset, Vector3 at, System.Random rng)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
        go.AddComponent<Rigidbody>();
        var box = go.AddComponent<BoxCollider>();
        var p = QuadrotorParameters.Preset(preset);
        box.size = new Vector3(p.ArmLength * 2.4f, 0.15f * p.ArmLength / 0.19f, p.ArmLength * 2.4f);
        box.center = new Vector3(0, 0.06f * p.ArmLength / 0.19f, 0);
        box.isTrigger = true;
        var d = go.AddComponent<DroneController>();
        d.Preset = preset;
        d.ReadInput = false;
        d.Simulate = _host;
        return d;
    }

    // ---- planning: open ground, and the roofs under a route

    static bool IsBuilding(Collider c) => c != null && (c.name.StartsWith("Buildings") || c.name.StartsWith("Roofs") || c.name == "Cornices");

    Vector3? OpenGround(System.Random rng, Vector3 centre, float radius)
    {
        for (int k = 0; k < 40; k++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = radius * Mathf.Sqrt((float)rng.NextDouble());
            var p = centre + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            if (!Physics.Raycast(p + Vector3.up * 300f, Vector3.down, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (IsBuilding(hit.collider) || hit.rigidbody != null || hit.normal.y < 0.9f) continue;
            // clear above: nothing within 4 m around the take-off point up to 10 m
            if (Physics.CheckSphere(hit.point + Vector3.up * 5f, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
            return hit.point;
        }
        return null;
    }

    static float TopAt(Vector3 p)
    {
        return Physics.Raycast(new Vector3(p.x, 500f, p.z), Vector3.down, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : p.y;
    }

    /// <summary>The highest surface (roof, tree, lamp) along a straight line, sampled every 5 m and 10 m to the sides.</summary>
    static float RoofHeight(Vector3 a, Vector3 b)
    {
        float top = Mathf.Max(a.y, b.y);
        var d = b - a; d.y = 0; float len = d.magnitude;
        var side = len > 0.1f ? Vector3.Cross(Vector3.up, d / len) : Vector3.right;
        for (float t = 0; t <= len; t += 5f)
            foreach (var o in new[] { -10f, 0f, 10f })
                top = Mathf.Max(top, TopAt(a + d * (len > 0 ? t / len : 0) + side * o));
        return top;
    }

    static float RoofHeightAround(Vector3 c, float r)
    {
        float top = c.y;
        for (float x = -r; x <= r; x += 10f)
            for (float z = -r; z <= r; z += 10f)
                if (x * x + z * z <= r * r) top = Mathf.Max(top, TopAt(c + new Vector3(x, 0, z)));
        return top;
    }

    // ---- flying (host)

    readonly List<AiDrone> _swarm = new List<AiDrone>();
    readonly List<DroneController> _otherDrones = new List<DroneController>();
    readonly List<SVec3> _flockPos = new List<SVec3>();
    readonly List<SVec3> _flockVel = new List<SVec3>();
    float _nextOtherDroneScan;

    void FixedUpdate()
    {
        if (!_host || Spawned.Count == 0) return;
        float dt = Time.fixedDeltaTime, now = Time.time;
        // the swarm's members and the participants' drones, for the flocking interactions
        _swarm.Clear();
        for (int k = 0; k < Spawned.Count; k++)
        {
            var sd = Spawned[k];
            if (sd.Mission == Mission.Swarm && sd.Drone != null && sd.Phase == "flock") _swarm.Add(sd);
        }
        _flockPos.Clear();
        _flockVel.Clear();
        if (_swarm.Count > 0)
        {
            if (now >= _nextOtherDroneScan)
            {
                _nextOtherDroneScan = now + 0.5f;
                _otherDrones.Clear();
                foreach (var oc in FindObjectsByType<DroneController>(FindObjectsSortMode.None))
                    if (oc.ReadInput || oc.UseExternalInput) _otherDrones.Add(oc);
            }
            for (int k = 0; k < _swarm.Count; k++)
            {
                _flockPos.Add(_swarm[k].Drone.Model.State.Position);
                _flockVel.Add(_swarm[k].Drone.Model.State.Velocity);
            }
            for (int k = 0; k < _otherDrones.Count; k++)
            {
                var o = _otherDrones[k];
                if (o == null) continue;
                _flockPos.Add(o.Model?.State.Position ?? DroneBody.ToModel(o.transform.position));
                _flockVel.Add(o.Model?.State.Velocity ?? SVec3.Zero);
            }
            if (_swarmRoute != null && _swarmRoute.Count > 0)
            {
                Vector3 centroid = Vector3.zero;
                for (int k = 0; k < _swarm.Count; k++) centroid += _swarm[k].Drone.transform.position;
                centroid /= _swarm.Count;
                var goal = _swarmRoute[_swarmLeg % _swarmRoute.Count];
                if (Vector2.Distance(new Vector2(centroid.x, centroid.z), new Vector2(goal.x, goal.z)) < 25f) _swarmLeg++;
            }
        }
        foreach (var a in Spawned)
        {
            var d = a.Drone;
            if (d == null || d.Model == null) continue;
            var s = d.Model.State;
            switch (a.Mission)
            {
                case Mission.Delivery: Deliver(a, s, dt, now); break;
                case Mission.Patrol:
                    a.Route.Step(s.Position, dt, out var tgt, out var v);
                    if (a.Route.Done) a.Route.Index = 1;   // round again
                    d.Fly(new DroneCommand { Mode = DroneMode.Position, Position = tgt, Velocity = v, Yaw = Heading(v, s) });
                    break;
                case Mission.Swarm:
                    if (a.Phase == "take-off")
                    {
                        if (now < a.WaitUntil) { d.Fly(new DroneCommand { Mode = DroneMode.Off }); break; }
                        var up = new SVec3(s.Position.X, s.Position.Y, DroneBody.ToModel(Vector3.up * _swarmHeight).Z);
                        d.Fly(new DroneCommand { Mode = DroneMode.Position, Position = up, Yaw = s.Yaw });
                        if (Math.Abs(s.Position.Z - up.Z) < 2f) a.Phase = "flock";
                        break;
                    }
                    int i = _swarm.IndexOf(a);
                    if (i < 0 || _swarmRoute == null || _swarmRoute.Count == 0) break;
                    var goal = DroneBody.ToModel(_swarmRoute[_swarmLeg % _swarmRoute.Count] + Vector3.up * _swarmHeight);
                    var want = Flocking.Velocity(i, _flockPos, _flockVel, new SVec3(goal.X, goal.Y, s.Position.Z), FlockingParameters.Default);
                    want.Z = 0.8f * (goal.Z - s.Position.Z);   // and its height
                    d.Fly(new DroneCommand { Mode = DroneMode.Velocity, Velocity = want, Yaw = Heading(want, s) });
                    break;
            }
        }
        Log(now);
    }

    static float Heading(SVec3 v, QuadrotorState s) => new System.Numerics.Vector2(v.X, v.Y).Length() > 1f ? (float)Math.Atan2(v.Y, v.X) : s.Yaw;

    void Deliver(AiDrone a, QuadrotorState s, float dt, float now)
    {
        var d = a.Drone;
        if (a.Route == null)
        {
            if (now < a.WaitUntil) { d.Fly(new DroneCommand { Mode = DroneMode.Off }); a.Phase = "on the ground"; return; }
            var from = a.Outbound ? a.Home : a.Drop; var to = a.Outbound ? a.Drop : a.Home;
            var pts = new[] { from + Vector3.up * (a.Cruise - from.y), to + Vector3.up * (a.Cruise - to.y), to + Vector3.up * 0.3f }.Select(DroneBody.ToModel);
            a.Route = new WaypointGuidance(pts) { CruiseSpeed = 12f, ClimbSpeed = 4f };
            a.Phase = a.Outbound ? "delivering" : "returning";
        }
        a.Route.Step(s.Position, dt, out var tgt, out var v);
        if (a.Route.Done && s.OnGround)
        {
            a.Route = null; a.Outbound = !a.Outbound; a.WaitUntil = now + 10f;
            d.Fly(new DroneCommand { Mode = DroneMode.Off });
            return;
        }
        // the last waypoint is just above the ground: let it settle
        if (a.Route.Done) tgt = new SVec3(tgt.X, tgt.Y, d.Model.GroundHeight - 0.5f);
        d.Fly(new DroneCommand { Mode = DroneMode.Position, Position = tgt, Velocity = v, Yaw = Heading(v, s) });
    }

    void Log(float now)
    {
        if (now < _nextLog) return;
        _nextLog = now + LogInterval;
        if (_log == null)
        {
            Directory.CreateDirectory("ExperimentLogs");
            _log = new StreamWriter("ExperimentLogs/Drones-" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv");
            _log.WriteLine("time,drone,mission,phase,x,y,z,vx,vy,vz,heading,height_above_ground,blade_passing_hz");
        }
        var ci = CultureInfo.InvariantCulture;
        string F(float x) => x.ToString("F2", ci);
        foreach (var a in Spawned)
        {
            if (a.Drone == null || a.Drone.Model == null) continue;
            var p = a.Drone.transform.position; var v = DroneBody.ToUnity(a.Drone.Model.State.Velocity);
            _log.WriteLine($"{F(now)},\"{a.Drone.name}\",{a.Mission},{a.Phase},{F(p.x)},{F(p.y)},{F(p.z)},{F(v.x)},{F(v.y)},{F(v.z)},{F(a.Drone.transform.eulerAngles.y)},{F(a.Drone.HeightAboveGround)},{F(a.Drone.BladePassingHz)}");
        }
    }

    // ---- network: the host sends poses; clients show them

    public struct DronePosesMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_DronePoses;
        public List<Vector3> Positions;
        public List<Quaternion> Rotations;
        public List<int> Landed;

        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref Positions);
            synchronizer.Sync(ref Rotations);
            synchronizer.Sync(ref Landed);
        }
    }

    readonly List<Vector3> _pos = new List<Vector3>();
    readonly List<Quaternion> _rot = new List<Quaternion>();
    readonly List<int> _landed = new List<int>();

    /// <summary>Host: sends the drones' poses at 20 Hz.</summary>
    public void UpdateHost(UNetHost host)
    {
        if (Spawned.Count == 0 || Time.time < _nextSend) return;
        _nextSend = Time.time + 0.05f;
        _pos.Clear(); _rot.Clear(); _landed.Clear();
        foreach (var a in Spawned)
        {
            _pos.Add(a.Drone != null ? a.Drone.transform.position : Vector3.zero);
            _rot.Add(a.Drone != null ? a.Drone.transform.rotation : Quaternion.identity);
            _landed.Add(a.Drone != null && a.Drone.Landed ? 1 : 0);
        }
        host.BroadcastUnreliable(new DronePosesMsg { Positions = _pos, Rotations = _rot, Landed = _landed });
    }

    public static void ClientHandlePoses(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<DronePosesMsg>(sync);
        var spawner = Current;
        if (spawner == null) return;
        for (int i = 0; i < msg.Positions.Count && i < spawner.Spawned.Count; i++)
            spawner.Spawned[i].Drone?.ShowPose(msg.Positions[i], msg.Rotations[i], msg.Landed[i] != 0);
    }
}
