using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityStandardAssets.Utility;

[Serializable]
public class AICarSyncSystem
{
    public enum Mode
    {
        None,
        Host,
        Client
    }

    public struct SpawnAICarMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_SpawnAICar;
        public int PrefabIdx;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool SpawnDriver;
        public bool SpawnPassenger;
        public int VehicleType;
        public Vector3 Color;
        public bool Labeled;

        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref PrefabIdx);
            synchronizer.Sync(ref Position);
            synchronizer.Sync(ref Rotation);
            synchronizer.Sync(ref SpawnDriver);
            synchronizer.Sync(ref SpawnPassenger);
            synchronizer.Sync(ref VehicleType);
            synchronizer.Sync(ref Color);
            synchronizer.Sync(ref Labeled);
        }
    }

    public AICar[] Prefabs;

    // Make sure to update the way these cars are logged if we
    // ever start supporting removal
    [NonSerialized]
    public List<PlayerAvatar> Cars = new List<PlayerAvatar>();

    Mode _mode;
    UNetHost _host;

    /// <summary>The host-side system of the running experiment (null on clients).</summary>
    public static AICarSyncSystem HostInstance { get; private set; }

    public void InitHost(UNetHost host)
    {
        _mode = Mode.Host;
        _host = host;
        HostInstance = this;
    }

    public void InitClient(MessageDispatcher dispatcher)
    {
        _mode = Mode.Client;
        dispatcher.AddStaticHandler((int)MsgId.S_SpawnAICar, ClientHandleSpawnAICar);
        dispatcher.AddStaticHandler((int)MsgId.S_UpdateAICarPoses, ClientHandleUpdatePoses);
        dispatcher.AddStaticHandler((int)MsgId.S_SetAICarActive, ClientHandleSetActive);
        HostInstance = null;
    }

    public struct SetAICarActiveMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_SetAICarActive;
        public int CarIndex;
        public bool Active;

        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref CarIndex);
            synchronizer.Sync(ref Active);
        }
    }

    /// <summary>
    /// Hides (or shows again) a spawned AI car on the host and all clients. Cars are never destroyed,
    /// so the indices used to synchronise poses stay valid; hidden cars can be reused (see Reuse).
    /// </summary>
    public void SetCarActive(PlayerAvatar car, bool active)
    {
        int index = Cars.IndexOf(car);
        if (index < 0)
        {
            car.gameObject.SetActive(active);
            return;
        }
        car.gameObject.SetActive(active);
        if (_mode == Mode.Host)
        {
            _host.BroadcastReliable(new SetAICarActiveMsg { CarIndex = index, Active = active });
        }
    }

    private void ClientHandleSetActive(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<SetAICarActiveMsg>(sync);
        if (msg.CarIndex >= 0 && msg.CarIndex < Cars.Count && Cars[msg.CarIndex] != null)
        {
            Cars[msg.CarIndex].gameObject.SetActive(msg.Active);
        }
    }

    /// <summary>
    /// Spawns a car whose pose is set from outside (e.g. by SUMO): no waypoint tracking and no
    /// AICar speed logic, kinematic rigidbody. Reuses a hidden car of the same prefab if there is one.
    /// </summary>
    public AICar SpawnExternal(AICar prefab, Vector3 position, Quaternion rotation, PlayerSystem.VehicleType vehicleType, Color color)
    {
        Assert.AreEqual(Mode.Host, _mode, "Only host can spawn synced objects");
        var prefabIdx = FindPrefabIndex(prefab);
        Assert.AreNotEqual(-1, prefabIdx, $"The prefab {prefab} was not added to NetworkingManager -> AICarSyncSystem -> Prefabs");
        var aiCar = GameObject.Instantiate(Prefabs[prefabIdx], position, rotation);
        aiCar.enabled = false;
        var tracker = aiCar.GetComponent<WaypointProgressTracker>();
        if (tracker != null) tracker.enabled = false;
        var parameters = new CarSpawnParams { Car = prefab, VehicleType = vehicleType, color = color, SpawnDriver = true };
        aiCar.GetComponent<CarConfigurator>()?.ChangeParameters(parameters);
        var avatar = aiCar.GetComponent<PlayerAvatar>();
        avatar.Initialize(false, PlayerSystem.InputMode.None, PlayerSystem.ControlMode.HostAI, vehicleType);
        var rb = aiCar.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        Cars.Add(avatar);
        _host.BroadcastReliable(new SpawnAICarMsg()
        {
            PrefabIdx = prefabIdx,
            Position = position,
            Rotation = rotation,
            SpawnDriver = true,
            VehicleType = (int)vehicleType,
            Color = new Vector3(color.r, color.g, color.b),
        });
        return aiCar;
    }

    int FindPrefabIndex(AICar prefab)
    {
        for (int i = 0; i < Prefabs.Length; i++)
        {
            if (Prefabs[i] == prefab) return i;
        }
        return -1;
    }

    public AICar Spawn(CarSpawnParams parameters, bool yielding)
    {
        Assert.AreEqual(Mode.Host, _mode, "Only host can spawn synced objects");
        var prefabIdx = FindPrefabIndex(parameters.Car);
        Assert.AreNotEqual(-1, prefabIdx, $"The prefab {parameters.Car} was not added to NetworkingManager -> AICarSyncSystem -> Prefabs");
        var aiCar = GameObject.Instantiate(Prefabs[prefabIdx], parameters.SpawnPoint.position, parameters.SpawnPoint.rotation);
        aiCar.gameObject.layer = LayerMask.NameToLayer(yielding ? "Yielding" : "Car");
        aiCar.enabled = true;
        var waypointProgressTracker = aiCar.GetComponent<WaypointProgressTracker>();
        waypointProgressTracker.enabled = true;
        waypointProgressTracker.Init(parameters.Track);
        var paint = aiCar.GetComponent<CarConfigurator>();
        paint.ChangeParameters(parameters);
        var avatar = aiCar.GetComponent<PlayerAvatar>();
        avatar.Initialize(false, PlayerSystem.InputMode.None, PlayerSystem.ControlMode.HostAI, parameters.VehicleType);
        var color = parameters.color;
        var rb = aiCar.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.GetComponent<Rigidbody>().useGravity = true;
        Cars.Add(avatar);
        _host.BroadcastReliable(new SpawnAICarMsg()
        {
            PrefabIdx = prefabIdx,
            Position = parameters.SpawnPoint.position,
            Rotation = parameters.SpawnPoint.rotation,
            SpawnPassenger = parameters.SpawnPassenger,
            Labeled = parameters.Labeled,
            SpawnDriver = parameters.SpawnDriver,
            VehicleType = (int)parameters.VehicleType,
            Color = new Vector3(color.r, color.g, color.b),
        });
        return aiCar;
    }

    private void ClientHandleSpawnAICar(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<SpawnAICarMsg>(sync);
        var go = GameObject.Instantiate(Prefabs[msg.PrefabIdx], msg.Position, msg.Rotation);
        var avatar = go.GetComponent<PlayerAvatar>();
        var color = msg.Color;
        avatar.GetComponent<CarConfigurator>().ChangeParameters(msg);
        avatar.Initialize(true, PlayerSystem.InputMode.None, PlayerSystem.ControlMode.HostAI, (PlayerSystem.VehicleType)msg.VehicleType);
        Cars.Add(avatar);
    }

    private void ClientHandleUpdatePoses(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<UpdateAICarPosesMsg>(sync);
        for (int i = 0; i < msg.Poses.Count && i < Cars.Count; i++)
        {
            if (Cars[i] != null && Cars[i].gameObject.activeSelf && msg.Poses[i].LocalPositions.Count > 1)
            {
                Cars[i].ApplyPose(msg.Poses[i]);
            }
        }
    }

    List<AvatarPose> _poses = new List<AvatarPose>();
    public List<AvatarPose> GatherPoses()
    {
        _poses.Clear();
        foreach (var car in Cars)
        {
            // destroyed cars (e.g. by other scripts) keep their slot so indices stay in sync
            _poses.Add(car != null ? car.GetPose() : EmptyPose);
        }
        return _poses;
    }

    static readonly AvatarPose EmptyPose = new AvatarPose
    {
        LocalPositions = new List<Vector3> { new Vector3(0, -1000, 0) },
        LocalRotations = new List<Quaternion> { Quaternion.identity },
    };

    struct UpdateAICarPosesMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_UpdateAICarPoses;

        public List<AvatarPose> Poses;
        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.SyncListSubmessage(ref Poses);
        }
    }
    public void UpdateHost()
    {
        _host.BroadcastUnreliable(new UpdateAICarPosesMsg
        {
            Poses = GatherPoses(),
        });
    }
}
