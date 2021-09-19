// The convention for network message ids is:
// negative numbers are internal networking messages (establishing connection etc.)
// 1-1000 are server to client
// 1001-2000 are client to server
using System.Collections.Generic;
using System.IO;

enum MsgId
{
    Invalid = 0,
    S_StartGame = 1,
    S_AllReady = 2,
    S_UpdateClientPoses = 3,
    S_ChangeHMI = 4,
    S_ChangeLights = 5,
    S_SpawnAICar = 6,
    S_UpdateAICarPoses = 7,
    S_UpdateAIPedestrianPoses = 8,

    S_VisualSync = 123,

    B_Ping = 500,

    C_Ready = 1001,
    C_UpdatePose = 1002,
    C_RequestHMIChange = 1003,
}

// On Serialization/Deserialization:
//
// The reader you receive for deserialization
// has the msg type ID already read from it 
// (because we needed it to know which method to call)

public static class NetMsg
{
    public static bool IsInternal(int msgId)
    {
        return msgId < 0;
    }

    public static TMsg Read<TMsg>(ISynchronizer reader)
        where TMsg : struct, INetMessage
    {
        var msg = default(TMsg);
        msg.Sync(reader);
        return msg;
    }
}

//network messages payload syncing implementation
//Sync depending on wheter Serializer or Deserializer is provided as a parameter it will either write or read the message

public struct VisualSyncMessage : INetMessage
{
    public int MessageId => (int)MsgId.S_VisualSync;
    public void Sync<T>(T synchronizer) where T : ISynchronizer { }
}

public struct ReadyMsg : INetMessage
{
    public int MessageId => (int)MsgId.C_Ready;
    public void Sync<T>(T synchronizer) where T : ISynchronizer { }
}

public struct AllReadyMsg : INetMessage
{
    public int MessageId => (int)MsgId.S_AllReady;

    public void Sync<T>(T synchronizer) where T : ISynchronizer { }
}

public struct StartGameMsg : INetMessage
{
    public int MessageId => (int)MsgId.S_StartGame;
    public List<int> Roles;
    public int Experiment;

    public void Sync<T>(T synchronizer) where T : ISynchronizer
    {
        synchronizer.Sync(ref Roles);
        synchronizer.Sync(ref Experiment);
    }
}

public struct UpdateClientPose : INetMessage
{
    public int MessageId => (int)MsgId.C_UpdatePose;

    public AvatarPose Pose;

    public void Sync<T>(T synchronizer) where T : ISynchronizer
    {
        synchronizer.Sync(ref Pose.LocalPositions);
        synchronizer.Sync(ref Pose.LocalRotations);
        int blinkers = (int)Pose.Blinkers;
        synchronizer.Sync(ref blinkers);
        Pose.Blinkers = (BlinkerState)blinkers;
    }
}

public struct UpdatePoses : INetMessage
{
    public int MessageId => (int)MsgId.S_UpdateClientPoses;
    public List<AvatarPose> Poses;
    public void Sync<T>(T synchronizer) where T : ISynchronizer
    {
        synchronizer.SyncListSubmessage(ref Poses);
    }
}

public struct ChangeHMI : INetMessage
{
    public int MessageId => (int)MsgId.S_ChangeHMI;
    public int HMI;
    public int State;

    public void Sync<T>(T synchronizer) where T : ISynchronizer
    {
        synchronizer.Sync(ref HMI);
        synchronizer.Sync(ref State);
    }
}

public struct PingMsg : INetMessage
{
    public int MessageId => (int)MsgId.B_Ping;
    public float Timestamp;
    public int PingId;

    public void Sync<T>(T synchronizer) where T : ISynchronizer
    {
        synchronizer.Sync(ref Timestamp);
        synchronizer.Sync(ref PingId);
    }
}