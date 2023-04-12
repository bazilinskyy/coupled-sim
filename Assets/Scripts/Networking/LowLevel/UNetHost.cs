using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;

using Unity.Collections;
using Unity.Networking.Transport;

public struct TransportEvent
{
    public enum Type
    {
        None,
        Connect,
        Disconnect,
        Data
    }
    public Type type;
    public int connectionId;
    public byte[] data;
    public int dataSize;
}

//low level UNET networking host implementation - message sending and handling
public class UNetHost
{
    const int MaxPlayers = UNetConfig.MaxPlayers;
    public int NumRemotePlayers => m_Connections.Length;
    const int NoPlayer = -1;

    private NativeList<NetworkConnection> m_Connections;
    NetworkDriver m_Driver;

    bool _initialized;

    byte[] _sendBuffer = new byte[UNetConfig.SendBufferSize];
    BinaryWriter _writer;
    MemoryStream _writerStream;
    Serializer _serializer;
    Dictionary<int, int> _connId2playerId = new Dictionary<int, int>();
    NetworkConnection[] _playerId2connId = new NetworkConnection[MaxPlayers];

    public bool PlayerConnected(int player) => _playerId2connId[player] != default(NetworkConnection);

    int FindFreePlayerId()
    {
        // 0 is always host
        for (int i = 1; i < MaxPlayers; i++)
        {
            if (!PlayerConnected(i))
            {
                return i;
            }
        }
        Assert.IsFalse(true);
        return NoPlayer;
    }
    NetworkPipeline reliablePipeline;

    public void Init()
    {
        m_Driver = NetworkDriver.Create();
        reliablePipeline = m_Driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = UNetConfig.Port;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + UNetConfig.Port);
        else
        {
            Debug.Log("Listening on " + NetworkUtils.GetLocalInterfaceAddresses()[0]);
            m_Driver.Listen();
        }

        m_Connections = new NativeList<NetworkConnection>(UNetConfig.MaxHostConnections, Allocator.Persistent);

        _initialized = true;
        _writerStream = new MemoryStream(_sendBuffer);
        _writer = new BinaryWriter(_writerStream);
        _serializer = new Serializer(_writer);
        for (int i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            _playerId2connId[i] = default(NetworkConnection);
        }
    }

    public void Shutdown()
    {
        if (m_Driver.IsCreated)
        {
            m_Driver.Dispose();
            m_Connections.Dispose();
        }
    }

    //processing network messages comming from clients
    public void Update(MessageDispatcher dispatch)
    {
        Assert.IsTrue(_initialized, "Update() should not be called before Initialize()");

        m_Driver.ScheduleUpdate().Complete();

        // Clean up connections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // Accept new connections
        NetworkConnection c;
        while ((c = m_Driver.Accept()) != default(NetworkConnection))
        {
            m_Connections.Add(c);
            Debug.Log("Accepted a connection");
            var player = FindFreePlayerId();
            _connId2playerId[c.InternalId] = player;
            _playerId2connId[player] = c;
            SendReliableToPlayer(new WelcomeToRoomMsg() { PlayerIdx = player }, player);
        }

        for (int i = 0; i < m_Connections.Length; ++i)
        {
            DataStreamReader strm;
            NetworkEvent.Type cmd;
            // Pop all events for the connection
            while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out strm)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    var msgId = strm.ReadInt();
                    if (NetMsg.IsInternal(msgId))
                    {
                        switch ((InternalMsgId)msgId)
                        {
                            case InternalMsgId.WelcomeToRoom:
                                // Client Only
                                break;
                        }
                    }
                    else
                    {
                        dispatch.Dispatch(msgId, new NDeserializer(ref strm), _connId2playerId[m_Connections[i].InternalId]);
                    }
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from server");
                    m_Connections[i] = default(NetworkConnection);
                }
            }
        }
    }

    public BinaryWriter BeginMessage(int msgId)
    {
        _writer.Write(msgId);
        return _writer;
    }

    public void EndMessageBroadcastReliableExceptPlayer(int player) => EndMessageBroadcastExceptPlayer(true, player);

    public void EndMessageBroadcastUnreliableExceptPlayer(int player) => EndMessageBroadcastExceptPlayer(false, player);

    void EndMessageBroadcastExceptPlayer(bool reliable, int except)
    {
        var exceptConnId = _playerId2connId[except];
        foreach (var connId in m_Connections)
        {
            if (exceptConnId != connId)
            {
                SendDataNoClear(connId, reliable);
            }
        }
        Clear();
    }

    void SendDataNoClear(NetworkConnection connId, bool reliable)
    {
        m_Driver.BeginSend(reliable ? reliablePipeline : NetworkPipeline.Null, connId, out var writer);
        unsafe
        {
            fixed (byte* pointerToFirst = _sendBuffer)
            {
                writer.WriteBytes(pointerToFirst, (int)_writerStream.Position);
            }
        }
        m_Driver.EndSend(writer);
    }

    void Clear() => _writerStream.Position = 0;

    void SendDataAndClear(NetworkConnection connId, bool reliable)
    {
        SendDataNoClear(connId, reliable);
        Clear();
    }

    public void BroadcastCustomMessageReliable<TMsg>(TMsg msg, int receiverId)
        where TMsg : INetMessage => BroadcastCustomMessage(msg, receiverId, true);

    public void BroadcastCustomMessageUnreliable<TMsg>(TMsg msg, int receiverId)
       where TMsg : INetMessage => BroadcastCustomMessage(msg, receiverId, false);

    void BroadcastCustomMessage<TMsg>(TMsg msg, int receiverId, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(_initialized, "Cannot broadcast before Initialize()");

        _writer.Write(msg.MessageId);
        _writer.Write(receiverId);
        msg.Sync(_serializer);
        foreach (var connId in m_Connections)
        {
            SendDataNoClear(connId, reliable);
        }
        Clear();
    }

    public void BroadcastReliable<TMsg>(TMsg msg)
        where TMsg : INetMessage => Broadcast(msg, true);

    public void BroadcastUnreliable<TMsg>(TMsg msg)
       where TMsg : INetMessage => Broadcast(msg, false);

    void Broadcast<TMsg>(TMsg msg, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(_initialized, "Cannot broadcast before Initialize()");

        _writer.Write(msg.MessageId);
        msg.Sync(_serializer);
        foreach (var connId in m_Connections)
        {
            SendDataNoClear(connId, reliable);
        }
        Clear();
    }

    public void SendReliableToPlayer<TMsg>(TMsg msg, int player)
        where TMsg : INetMessage => SendToPlayer(msg, player, true);

    public void SendUnreliableToPlayer<TMsg>(TMsg msg, int player)
        where TMsg : INetMessage => SendToPlayer(msg, player, false);

    void SendToPlayer<TMsg>(TMsg msg, int player, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(_initialized, "Cannot send before Initialize()");

        _writer.Write(msg.MessageId);
        msg.Sync(_serializer);
        SendDataAndClear(_playerId2connId[player], reliable);
    }
}
