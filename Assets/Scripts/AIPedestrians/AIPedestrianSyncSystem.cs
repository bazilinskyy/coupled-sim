using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Utility;

// TODO(jacek): Probably a good idea to move all the PlayerAvatar syncing in one place and just register them from here
[Serializable]
public class AIPedestrianSyncSystem 
{
    public enum Mode
    {
        None,
        Host,
        Client
    }
    [Serializable]
    struct PedestrianDesc
    {
        public AIPedestrian Pedestrian;
        public WaypointCircuit Path;
    }

    [SerializeField]
    PedestrianDesc[] AIPedestrians;

    List<PlayerAvatar> Pedestrians = new List<PlayerAvatar>();
    // pedestrians spawned at run time (PedestrianGroup), in the same order on every agent
    List<PlayerAvatar> Extra = new List<PlayerAvatar>();

    /// <summary>Adds a pedestrian made at run time; every agent must add the same ones in the same order.</summary>
    public void Register(PlayerAvatar p) => Extra.Add(p);

    UNetHost _host;
    public void InitHost(UNetHost host)
    {
        _host = host;
        foreach (var p in AIPedestrians)
        {
            var ped = p.Pedestrian;
            Pedestrians.Add(ped.GetComponent<PlayerAvatar>());
            ped.Init(p.Path);
            ped.GetComponent<Animator>().enabled = true;
        }
    }

    public void InitClient(MessageDispatcher dispatcher)
    {
        dispatcher.AddStaticHandler((int)MsgId.S_UpdateAIPedestrianPoses, ClientHandleUpdatePoses);
        foreach (var p in AIPedestrians)
        {
            var ped = p.Pedestrian;
            Pedestrians.Add(ped.GetComponent<PlayerAvatar>());
            ped.GetComponent<AIPedestrian>().enabled = false;
        }
    }

    private void ClientHandleUpdatePoses(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<UpdateAIPedestrianPosesMessage>(sync);
        for (int i = 0; i < msg.Poses.Count; i++)
        {
            var p = i < Pedestrians.Count ? Pedestrians[i] : i - Pedestrians.Count < Extra.Count ? Extra[i - Pedestrians.Count] : null;
            if (p != null) p.ApplyPose(msg.Poses[i]);
        }
    }

    List<AvatarPose> _poses = new List<AvatarPose>();
    public List<AvatarPose> GatherPoses()
    {
        _poses.Clear();
        foreach (var ped in Pedestrians)
        {
            _poses.Add(ped.GetPose());
        }
        foreach (var ped in Extra)
            if (ped != null) _poses.Add(ped.GetPose());
        return _poses;
    }

    struct UpdateAIPedestrianPosesMessage : INetMessage
    {
        public int MessageId => (int)MsgId.S_UpdateAIPedestrianPoses;

        public List<AvatarPose> Poses;
        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.SyncListSubmessage(ref Poses);
        }
    }

    public void UpdateHost()
    {
        _host.BroadcastUnreliable(new UpdateAIPedestrianPosesMessage
        {
            Poses = GatherPoses(),
        });
    }
}
