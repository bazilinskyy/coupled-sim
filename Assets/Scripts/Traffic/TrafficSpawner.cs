using System.Collections;
using System.Collections.Generic;
using CoupledSim.Traffic;
using UnityEngine;
using UnityStandardAssets.Utility;

/// <summary>
/// Background traffic driven by a car-following model. Spawns cars spread along one or more lanes
/// (WaypointCircuits, usually closed loops), each with its own desired speed, and lets
/// <see cref="TrafficCar"/> drive them. Add it to an experiment's Car Spawners list.
/// </summary>
public class TrafficSpawner : CarSpawnerBase
{
    [System.Serializable]
    public struct Lane
    {
        public WaypointCircuit Path;
        [Tooltip("Number of cars on this lane.")]
        public int Cars;
    }

    [Header("Traffic")]
    public Lane[] Lanes;
    public CarFollowingModelType Model = CarFollowingModelType.IDM;
    [Tooltip("Mean and standard deviation of the desired speed (km/h).")]
    public float DesiredSpeedKmh = 50f;
    public float DesiredSpeedSdKmh = 5f;
    [Tooltip("Car prefabs to pick from; each must be in NetworkingManager ➡️ AI Car System ➡️ Prefabs. Empty: use Spawn Params ➡️ Car.")]
    public AICar[] CarPrefabs;
    public Color[] Colors = { Color.white, Color.black, new Color(0.6f, 0.6f, 0.62f), new Color(0.55f, 0.05f, 0.05f), new Color(0.1f, 0.2f, 0.5f), new Color(0.75f, 0.75f, 0.75f) };
    [Tooltip("Do not spawn closer than this (m) to a player.")]
    public float KeepClearOfPlayers = 15f;
    public bool YieldToPedestrians = true;
    public bool ObeyTrafficLights = true;
    public int Seed = 1;

    public readonly List<TrafficCar> SpawnedCars = new List<TrafficCar>();

    protected override bool HasValidSpawnParams => Lanes != null && Lanes.Length > 0;

    protected override IEnumerator SpawnCoroutine()
    {
        yield return null; // let player avatars spawn first
        var rng = new System.Random(Seed);
        var players = new List<Vector3>();
        foreach (var a in FindObjectsByType<PlayerAvatar>()) players.Add(a.transform.position);

        var spawnPoint = new GameObject(name + " spawn point").transform;
        foreach (var lane in Lanes)
        {
            if (lane.Path == null || lane.Cars <= 0) continue;
            float length = lane.Path.Length > 0 ? lane.Path.Length : PathLength(lane.Path);
            for (int i = 0; i < lane.Cars; i++)
            {
                float d = length * i / lane.Cars + (float)rng.NextDouble() * 2f;
                var rp = lane.Path.GetRoutePoint(d);
                if (players.Exists(p => (p - rp.position).sqrMagnitude < KeepClearOfPlayers * KeepClearOfPlayers)) continue;
                // waypoints may float above the road (trigger boxes): put the car on the ground
                var pos = rp.position;
                if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                    pos.y = hit.point.y;
                var dir = rp.direction; dir.y = 0;
                spawnPoint.SetPositionAndRotation(pos + Vector3.up * 0.1f, Quaternion.LookRotation(dir.normalized, Vector3.up));

                var parameters = spawnParams;
                parameters.SpawnPoint = spawnPoint;
                parameters.Track = lane.Path;
                if (CarPrefabs != null && CarPrefabs.Length > 0) parameters.Car = CarPrefabs[rng.Next(CarPrefabs.Length)];
                if (Colors != null && Colors.Length > 0) parameters.color = Colors[rng.Next(Colors.Length)];
                parameters.SpawnDriver = true;

                var car = Spawn(parameters, false);
                car.GetComponent<WaypointProgressTracker>().SetProgress(d);
                car.name = $"{name} {lane.Path.name} {i}";
                var traffic = car.gameObject.AddComponent<TrafficCar>();
                traffic.YieldToPedestrians = YieldToPedestrians;
                traffic.ObeyTrafficLights = ObeyTrafficLights;
                float v = (DesiredSpeedKmh + PedestrianLikeNormal(rng) * DesiredSpeedSdKmh) / 3.6f;
                traffic.Init(Model, Mathf.Max(2f, v), new System.Random(rng.Next()));
                // start rolling at about the speed of the flow
                traffic.Speed = Mathf.Max(2f, v * 0.6f);
                SpawnedCars.Add(traffic);
                yield return null;
            }
        }
        Destroy(spawnPoint.gameObject);

        // Scripted AI cars (the AV, cars of other spawners) keep their script but get a
        // collision-avoidance supervisor so they do not drive into the traffic.
        while (true)
        {
            foreach (var car in FindObjectsByType<AICar>())
            {
                if (car.enabled && !car.ExternalSpeedControl && car.GetComponent<TrafficCar>() == null && car.TryGetComponent<WaypointProgressTracker>(out var tracker) && tracker.Circuit != null)
                {
                    car.gameObject.AddComponent<TrafficCar>().InitSafetyOnly();
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    static float PedestrianLikeNormal(System.Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
    }

    static float PathLength(WaypointCircuit c)
    {
        float l = 0;
        var w = c.Waypoints;
        for (int i = 0; i < w.Length; i++) l += Vector3.Distance(w[i].position, w[(i + 1) % w.Length].position);
        return l;
    }
}
