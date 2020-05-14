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

    struct SpawnAICarMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_SpawnAICar;
        public int PrefabIdx;
        public Vector3 Position;
        public Quaternion Rotation;

        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref PrefabIdx);
            synchronizer.Sync(ref Position);
            synchronizer.Sync(ref Rotation);
        }
    }

    public AICar[] Prefabs;
    [NonSerialized]
    public List<PlayerAvatar> Cars = new List<PlayerAvatar>();

    Mode _mode;
    UNetHost _host;
    public void InitHost(UNetHost host)
    {
        _mode = Mode.Host;
        _host = host;
    }

    public void InitClient(MessageDispatcher dispatcher)
    {
        _mode = Mode.Client;
        dispatcher.AddStaticHandler((int)MsgId.S_SpawnAICar, ClientHandleSpawnAICar);
        dispatcher.AddStaticHandler((int)MsgId.S_UpdateAICarPoses, ClientHandleUpdatePoses);
    }

    int FindPrefabIndex(AICar prefab)
    {
        for (int i=0;i < Prefabs.Length; i++)
        {
            if (Prefabs[i] == prefab) return i;
        }
        return -1;
    }

    public AICar Spawn(AICar prefab, Vector3 position, Quaternion rotation, WaypointCircuit track, bool yielding)
    {
        Assert.AreEqual(Mode.Host, _mode, "Only host can spawn synced objects");
        var prefabIdx = FindPrefabIndex(prefab);
        Assert.AreNotEqual(-1, prefabIdx, $"The prefab {prefab} was not added to NetworkingManager -> AICarSyncSystem -> Prefabs");
        var aiCar = GameObject.Instantiate(Prefabs[prefabIdx], position, rotation);
        aiCar.gameObject.layer = LayerMask.NameToLayer(yielding ? "Yielding" : "Car");
        aiCar.enabled = true;
        var waypointProgressTracker = aiCar.GetComponent<WaypointProgressTracker>();
        waypointProgressTracker.enabled = true;
        waypointProgressTracker.Init(track);
        var avatar = aiCar.GetComponent<PlayerAvatar>();
        avatar.Initialize(PlayerSystem.Mode.HostAI);
        var rb = aiCar.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.GetComponent<Rigidbody>().useGravity = true;
        Cars.Add(avatar);
        _host.BroadcastReliable(new SpawnAICarMsg()
        {
            PrefabIdx = prefabIdx,
            Position = position,
            Rotation = rotation,
        });
        return aiCar;
    }

    public AICar SpawnDistraction(AICar prefab, Vector3 position, Quaternion rotation, WaypointCircuit track, bool yielding) 
    {
            // Check car set up
            Assert.AreEqual(Mode.Host, _mode, "Only host can spawn synced objects");
            var prefabIdx = FindPrefabIndex(prefab);
            Assert.AreNotEqual(-1, prefabIdx, $"The prefab {prefab} was not added to NetworkingManager -> AICarSyncSystem -> Prefabs");

            // Instantiate distraction car
            var aiCar = GameObject.Instantiate(Prefabs[prefabIdx], position, rotation); 
            aiCar.gameObject.layer = LayerMask.NameToLayer(yielding ? "Yielding" : "Car");
            aiCar.enabled = true;

            // Set waypoint for distraction car
            var waypointProgressTracker = aiCar.GetComponent<WaypointProgressTracker>();
            waypointProgressTracker.enabled = true;
            waypointProgressTracker.Init(track);

            // Initialize the host ai
            var avatar = aiCar.GetComponent<PlayerAvatar>();
            avatar.Initialize(PlayerSystem.Mode.HostAI);

            // Add rigid body to car
            var rb = aiCar.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.GetComponent<Rigidbody>().useGravity = true;
            Cars.Add(avatar);
            _host.BroadcastReliable(new SpawnAICarMsg()
            {
                PrefabIdx = prefabIdx,
                Position = position,
                Rotation = rotation,
            });

        return aiCar;
    }

    private void ClientHandleSpawnAICar(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<SpawnAICarMsg>(sync);
        var go = GameObject.Instantiate(Prefabs[msg.PrefabIdx], msg.Position, msg.Rotation);
        var avatar = go.GetComponent<PlayerAvatar>();
        avatar.Initialize(PlayerSystem.Mode.Remote);
        Cars.Add(avatar);
    }

    private void ClientHandleUpdatePoses(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<UpdateAICarPosesMsg>(sync);
        for (int i = 0; i < msg.Poses.Count; i++)
        {
            Cars[i].ApplyPose(msg.Poses[i]);
        }
    }

    List<AvatarPose> _poses = new List<AvatarPose>();
    public List<AvatarPose> GatherPoses()
    {
        _poses.Clear();
        foreach (var car in Cars)
        {
            _poses.Add(car.GetPose());
        }
        return _poses;
    }

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
