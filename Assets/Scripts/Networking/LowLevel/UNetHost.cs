using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    Dictionary<NetworkConnection, int> _connId2playerId = new Dictionary<NetworkConnection, int>();
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
    NetworkPipeline reliablePipeline, unreliablePipeline;

    public void Init()
    {
        m_Driver = UNetPipelines.CreateDriver(out unreliablePipeline, out reliablePipeline);
        var endpoint = NetworkEndpoint.AnyIpv4.WithPort(UNetConfig.Port);
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + UNetConfig.Port);
        else
        {
            var addrs = NetworkUtils.GetLocalInterfaceAddresses();
            Debug.Log("Listening on " + (addrs.Count > 0 ? addrs[0] : "127.0.0.1"));
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
        }
        if (m_Connections.IsCreated)
        {
            m_Connections.Dispose();
        }
        _connId2playerId.Clear();
        for (int i = 0; i < _playerId2connId.Length; i++)
        {
            _playerId2connId[i] = default(NetworkConnection);
        }
        _initialized = false;
    }

    void ReleaseConnection(NetworkConnection conn)
    {
        if (conn != default(NetworkConnection) && _connId2playerId.TryGetValue(conn, out int pid))
        {
            _playerId2connId[pid] = default(NetworkConnection);
            _connId2playerId.Remove(conn);
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
                ReleaseConnection(m_Connections[i]);
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
            _connId2playerId[c] = player;
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
                        try { dispatch.Dispatch(msgId, new NDeserializer(ref strm), _connId2playerId[m_Connections[i]]); }
                        catch (System.Exception e) { Debug.LogException(e); }
                    }
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from server");
                    ReleaseConnection(m_Connections[i]);
                    m_Connections[i] = default(NetworkConnection);
                    break;
                }
            }
        }
    }

    public BinaryWriter BeginMessage(int msgId)
    {
        Clear();
        _writer.Write(msgId);
        return _writer;
    }

    public void EndMessageBroadcastReliableExceptPlayer(int player) => EndMessageBroadcastExceptPlayer(true, player);

    public void EndMessageBroadcastUnreliableExceptPlayer(int player) => EndMessageBroadcastExceptPlayer(false, player);

    void EndMessageBroadcastExceptPlayer(bool reliable, int except)
    {
        try
        {
            var exceptConnId = _playerId2connId[except];
            foreach (var connId in m_Connections)
            {
                if (exceptConnId != connId)
                {
                    SendDataNoClear(connId, reliable);
                }
            }
        }
        finally
        {
            Clear();
        }
    }

    void SendDataNoClear(NetworkConnection connId, bool reliable)
    {
        if (connId == default(NetworkConnection)) return;
        int status = m_Driver.BeginSend(reliable ? reliablePipeline : unreliablePipeline, connId, out var writer);
        if (status < 0)
        {
            UNetPipelines.Check(status, (int)_writerStream.Position);
            return;
        }
        unsafe
        {
            fixed (byte* pointerToFirst = _sendBuffer)
            {
                writer.WriteBytesUnsafe(pointerToFirst, (int)_writerStream.Position);
            }
        }
        UNetPipelines.Check(m_Driver.EndSend(writer), (int)_writerStream.Position);
    }

    void Clear() => _writerStream.Position = 0;

    void SendDataAndClear(NetworkConnection connId, bool reliable)
    {
        try
        {
            SendDataNoClear(connId, reliable);
        }
        finally
        {
            Clear();
        }
    }

    public void BroadcastCustomMessageReliable<TMsg>(TMsg msg, int receiverId)
        where TMsg : INetMessage => BroadcastCustomMessage(msg, receiverId, true);

    public void BroadcastCustomMessageUnreliable<TMsg>(TMsg msg, int receiverId)
       where TMsg : INetMessage => BroadcastCustomMessage(msg, receiverId, false);

    void BroadcastCustomMessage<TMsg>(TMsg msg, int receiverId, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(_initialized, "Cannot broadcast before Initialize()");
        if (m_Connections.Length == 0) return;

        Clear();
        try
        {
            _writer.Write(msg.MessageId);
            _writer.Write(receiverId);
            msg.Sync(_serializer);
            foreach (var connId in m_Connections)
            {
                SendDataNoClear(connId, reliable);
            }
        }
        finally
        {
            Clear();
        }
    }

    public void BroadcastReliable<TMsg>(TMsg msg)
        where TMsg : INetMessage => Broadcast(msg, true);

    public void BroadcastUnreliable<TMsg>(TMsg msg)
       where TMsg : INetMessage => Broadcast(msg, false);

    void Broadcast<TMsg>(TMsg msg, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(_initialized, "Cannot broadcast before Initialize()");
        if (m_Connections.Length == 0) return;

        Clear();
        try
        {
            _writer.Write(msg.MessageId);
            msg.Sync(_serializer);
            foreach (var connId in m_Connections)
            {
                SendDataNoClear(connId, reliable);
            }
        }
        finally
        {
            Clear();
        }
    }

    public void SendReliableToPlayer<TMsg>(TMsg msg, int player)
        where TMsg : INetMessage => SendToPlayer(msg, player, true);

    public void SendUnreliableToPlayer<TMsg>(TMsg msg, int player)
        where TMsg : INetMessage => SendToPlayer(msg, player, false);

    void SendToPlayer<TMsg>(TMsg msg, int player, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(_initialized, "Cannot send before Initialize()");

        Clear();
        try
        {
            _writer.Write(msg.MessageId);
            msg.Sync(_serializer);
            SendDataNoClear(_playerId2connId[player], reliable);
        }
        finally
        {
            Clear();
        }
    }
}
