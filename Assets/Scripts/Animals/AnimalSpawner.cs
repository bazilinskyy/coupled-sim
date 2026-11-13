using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Animals in an experiment: spawns the given numbers of each species around a centre (on every
/// agent, from the same seed, so every agent has the same animals). The host runs their behaviour,
/// sends their poses to the clients and logs them to ExperimentLogs/Animals-{date}.csv.
/// Trial parameter: animals (a multiplier of the counts; 0 = none).
/// </summary>
public class AnimalSpawner : MonoBehaviour, IExperimentModifier
{
    [Serializable]
    public struct Group
    {
        public AnimalSpecies Species;
        public int Count;
    }

    public Group[] Animals =
    {
        new Group { Species = AnimalSpecies.Pigeon, Count = 12 },
        new Group { Species = AnimalSpecies.Dog, Count = 2 },
        new Group { Species = AnimalSpecies.Cat, Count = 2 },
        new Group { Species = AnimalSpecies.Fox, Count = 1 },
    };
    [Tooltip("Centre of the area; empty: the first role's spawn point, or this object.")]
    public Transform Centre;
    public float Radius = 60f;
    public int Seed = 1;
    public float LogInterval = 0.1f;

    public static AnimalSpawner Current { get; private set; }
    public readonly List<AnimalAgent> Spawned = new List<AnimalAgent>();
    float _multiplier = 1f;
    StreamWriter _log;
    float _nextLog, _nextSend;

    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters)
    {
        foreach (var p in experimentParameters)
            if (p.name.ToLower() == "animals" && float.TryParse(p.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) _multiplier = Mathf.Max(0f, m);
    }

    void Awake() => Current = this;

    void OnDestroy()
    {
        if (Current == this) Current = null;
        _log?.Dispose();
    }

    void Start()
    {
        var assets = Resources.Load<AnimalAssets>("AnimalAssets");
        if (assets == null) { Debug.LogWarning("AnimalSpawner: Resources/AnimalAssets missing"); return; }
        var centre = Centre != null ? Centre.position : transform.position;
        var exp = GetComponentInParent<ExperimentDefinition>();
        if (Centre == null && exp != null && exp.Roles != null && exp.Roles.Length > 0 && exp.Roles[0].SpawnPoint.Point != null)
            centre = exp.Roles[0].SpawnPoint.Point.position;
        var rng = new System.Random(Seed);
        bool host = NetworkingManager.Instance == null || NetworkingManager.Instance.NetSystem is Host;
        int id = 0;
        foreach (var g in Animals)
        {
            var prefab = assets.Get(g.Species);
            if (prefab == null) continue;
            int n = Mathf.RoundToInt(g.Count * _multiplier);
            for (int i = 0; i < n; i++)
            {
                var pos = FindSpot(centre, rng);
                if (pos == null) continue;
                var go = Instantiate(prefab, pos.Value, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), transform);
                go.name = $"{g.Species} {i}";
                var agent = go.GetComponent<AnimalAgent>();
                agent.Init(Seed * 1000 + id++);
                agent.Simulate = host;
                Spawned.Add(agent);
            }
        }
    }

    Vector3? FindSpot(Vector3 centre, System.Random rng)
    {
        for (int k = 0; k < 20; k++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Radius * Mathf.Sqrt((float)rng.NextDouble());
            var p = centre + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y < centre.y + 1.5f)
                return hit.point;
        }
        return null;
    }

    void Update()
    {
        bool host = NetworkingManager.Instance == null || NetworkingManager.Instance.NetSystem is Host;
        if (!host || Spawned.Count == 0) return;
        if (Time.time >= _nextLog)
        {
            _nextLog = Time.time + LogInterval;
            if (_log == null)
            {
                Directory.CreateDirectory("ExperimentLogs");
                _log = new StreamWriter("ExperimentLogs/Animals-" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv");
                _log.WriteLine("time,animal,species,x,y,z,heading,state");
            }
            var ci = CultureInfo.InvariantCulture;
            foreach (var a in Spawned)
            {
                if (a == null) continue;
                var p = a.transform.position;
                _log.WriteLine($"{Time.time.ToString("F2", ci)},\"{a.name}\",{a.Species},{p.x.ToString("F2", ci)},{p.y.ToString("F2", ci)},{p.z.ToString("F2", ci)},{a.transform.eulerAngles.y.ToString("F1", ci)},{a.Current}");
            }
        }
    }

    // ---- network: the host sends poses and animation states; clients apply them

    public struct AnimalPosesMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_AnimalPoses;
        public List<Vector3> Positions;
        public List<Quaternion> Rotations;
        public List<int> States;

        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref Positions);
            synchronizer.Sync(ref Rotations);
            synchronizer.Sync(ref States);
        }
    }

    readonly List<Vector3> _pos = new List<Vector3>();
    readonly List<Quaternion> _rot = new List<Quaternion>();
    readonly List<int> _states = new List<int>();

    /// <summary>Host: called every frame of the game; sends the animals' poses at 20 Hz.</summary>
    public void UpdateHost(UNetHost host)
    {
        if (Spawned.Count == 0 || Time.time < _nextSend) return;
        _nextSend = Time.time + 0.05f;
        _pos.Clear(); _rot.Clear(); _states.Clear();
        foreach (var a in Spawned)
        {
            _pos.Add(a != null ? a.transform.position : Vector3.zero);
            _rot.Add(a != null ? a.transform.rotation : Quaternion.identity);
            _states.Add(a != null ? (int)a.Current : 0);
        }
        host.BroadcastUnreliable(new AnimalPosesMsg { Positions = _pos, Rotations = _rot, States = _states });
    }

    public static void ClientHandlePoses(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<AnimalPosesMsg>(sync);
        var spawner = Current;
        if (spawner == null) return;
        for (int i = 0; i < msg.Positions.Count && i < spawner.Spawned.Count; i++)
        {
            var a = spawner.Spawned[i];
            if (a == null) continue;
            a.transform.SetPositionAndRotation(msg.Positions[i], msg.Rotations[i]);
            a.SetShownState((AnimalAgent.State)msg.States[i]);
        }
    }
}
