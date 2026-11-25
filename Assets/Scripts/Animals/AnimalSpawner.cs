using System.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Animals in an experiment: spawns the given numbers of each species around a centre (on every
/// agent, from the same seed, so every agent has the same animals). The host runs their behaviour,
/// sends their poses to the clients and logs them to ExperimentLogs/Animals-{date}.csv.
/// Trial parameters: animals (a multiplier of the counts; 0 = none), animalMix, animalRadius, animalRole, animalAhead.
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
    int _role;
    bool _inSight;
    float _ahead;
    StreamWriter _log;
    float _nextLog, _nextSend;

    public void SetParameter(NetworkingManager.ExperimentParameter[] experimentParameters)
    {
        // a city map without animalMix: the animals one meets in a city centre by day, 10 pigeons, 2 dogs and a cat (the
        // CityMap experiment's own 20 pigeons, 4 dogs, 3 cats and a fox were more than a city centre has; a fox, a husky, a
        // deer or a horse is for animalMix where one is seen)
        if (OsmMap.Current != null && OsmMap.Current.Generator != null && !experimentParameters.Any(e => e.name.ToLower() == "animalmix"))
            Animals = new[] { new Group { Species = AnimalSpecies.Pigeon, Count = 10 }, new Group { Species = AnimalSpecies.Dog, Count = 2 }, new Group { Species = AnimalSpecies.Cat, Count = 1 } };
        foreach (var p in experimentParameters)
        {
            if (p.name.ToLower() == "animals" && float.TryParse(p.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) _multiplier = Mathf.Max(0f, m);
            // animalRadius=25: within this many metres of the centre (60 by default); animalRole=1: around that role's
            // spawn point instead of the first role's (e.g. the pedestrian's), in its sight; animalAhead=15: the centre this many
            // metres ahead of that spawn point, along its direction (where a participant walking or riding off will be)
            if (p.name.ToLower() == "animalradius" && float.TryParse(p.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rr)) Radius = Mathf.Clamp(rr, 3f, 500f);
            if (p.name.ToLower() == "animalrole" && int.TryParse(p.value, out var ro)) { _role = Mathf.Max(0, ro); _inSight = true; }
            if (p.name.ToLower() == "animalahead" && float.TryParse(p.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ah)) _ahead = ah;
            // animalMix=Horse:1,Deer:2,Husky:1,Pigeon:12: these species, this many each (times animals=), instead of the
            // scene's own mix (pigeons, dogs, cats and a fox by default)
            if (p.name.ToLower() == "animalmix")
            {
                var groups = new List<Group>();
                foreach (var part in p.value.Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && Enum.TryParse<AnimalSpecies>(kv[0].Trim(), true, out var sp) && int.TryParse(kv[1].Trim(), out var n))
                        groups.Add(new Group { Species = sp, Count = Mathf.Max(0, n) });
                }
                if (groups.Count > 0) Animals = groups.ToArray();
            }
        }
    }

    void Awake() => Current = this;

    void OnDestroy()
    {
        if (Current == this) Current = null;
        _log?.Dispose();
    }

    // (on a map, once it is built: its walls, fences and street furniture come late, and a fox placed before them stood
    // in a garden wall)
    System.Collections.IEnumerator Start()
    {
        if (FindAnyObjectByType<OsmMap>() != null)
        {
            float t0 = Time.realtimeSinceStartup;
            while ((OsmMap.Current == null || OsmMap.Current.Generator == null || OsmMap.Current.Generator.Roads.Count == 0) && Time.realtimeSinceStartup - t0 < 180f) yield return null;
            yield return null;
            Physics.SyncTransforms();
        }
        Spawn();
    }

    void Spawn()
    {
        var assets = Resources.Load<AnimalAssets>("AnimalAssets");
        if (assets == null) { Debug.LogWarning("AnimalSpawner: Resources/AnimalAssets missing"); return; }
        var centre = Centre != null ? Centre.position : transform.position;
        var exp = GetComponentInParent<ExperimentDefinition>();
        if (Centre == null && exp != null && exp.Roles != null && exp.Roles.Length > 0)
        {
            var sp = exp.Roles[Mathf.Min(_role, exp.Roles.Length - 1)].SpawnPoint.Point;
            if (sp == null) sp = exp.Roles[0].SpawnPoint.Point;
            if (sp != null) centre = sp.position + Vector3.ProjectOnPlane(sp.forward, Vector3.up).normalized * _ahead;
        }
        Debug.Log($"AnimalSpawner: around ({centre.x:F0}, {centre.z:F0}) within {Radius:F0} m" + (exp != null && exp.Roles != null && exp.Roles.Length > 0 ? $" (role {exp.Roles[Mathf.Min(_role, exp.Roles.Length - 1)].Name})" : " (no experiment roles)"));
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
        for (int k = 0; k < (_inSight ? 80 : 20); k++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Radius * Mathf.Sqrt((float)rng.NextDouble());
            var p = centre + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            // (on the ground, and clear of walls, posts and parked cars: a cat placed in a building's doorway)
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y < centre.y + 1.5f
                // (not on a building: on a hillside a flat roof down the slope is lower than the participant, and a dog and a
                // husky walked about on the roofs in São Paulo)
                && !OnBuilding(hit.collider)
                // (nor on a canal's water, below the quay)
                && !hit.collider.name.StartsWith("Water", System.StringComparison.Ordinal)
                && !Physics.CheckCapsule(hit.point + Vector3.up * 0.45f, hit.point + Vector3.up * 1.2f, 0.4f, ~0, QueryTriggerInteraction.Ignore)
                // (not on top of a wall, a hedge or a bus shelter, with ground under it more than 0.3 m lower: a fox was
                // placed on a garden wall's top)
                && (!Physics.Raycast(hit.point - Vector3.up * 0.05f, Vector3.down, out var below, 3f, ~0, QueryTriggerInteraction.Ignore) || below.distance < 0.25f)
                // (around a given role: in its sight at knee height, not behind a building or a garden wall: animals for a participant to meet)
                && (!_inSight || !Physics.Linecast(centre + Vector3.up * 0.4f, hit.point + Vector3.up * 0.4f, ~0, QueryTriggerInteraction.Ignore)))
                return hit.point;
        }
        return null;
    }

    static bool OnBuilding(Collider c)
    {
        for (var t = c.transform; t != null; t = t.parent)
            if (t.name.StartsWith("Building", System.StringComparison.Ordinal) || t.name.Contains("Roof") || t.name.StartsWith("Bus shelter", System.StringComparison.Ordinal)) return true;
        return false;
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
