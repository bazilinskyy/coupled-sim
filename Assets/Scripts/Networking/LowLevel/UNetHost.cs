using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Assertions;


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
    private NativeList<NetworkConnection> m_Connections;
    private NetworkDriver m_Driver;

    private bool _initialized;

    private readonly byte[] _sendBuffer = new byte[UNetConfig.SendBufferSize];
    private BinaryWriter _writer;
    private MemoryStream _writerStream;
    private Serializer _serializer;
    private readonly Dictionary<int, int> _connId2playerId = new();
    private readonly NetworkConnection[] _playerId2connId = new NetworkConnection[MaxPlayers];
    private NetworkPipeline reliablePipeline;
    public int NumRemotePlayers => m_Connections.Length;
    private const int MaxPlayers = UNetConfig.MaxPlayers;
    private const int NoPlayer = -1;


    public bool PlayerConnected(int player)
    {
        return _playerId2connId[player] != default;
    }


    private int FindFreePlayerId()
    {
        // 0 is always host
        for (var i = 1; i < MaxPlayers; i++)
        {
            if (!PlayerConnected(i))
            {
                return i;
            }
        }

        Assert.IsFalse(true);

        return NoPlayer;
    }


    public void Init()
    {
        m_Driver = NetworkDriver.Create();
        reliablePipeline = m_Driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = UNetConfig.Port;

        if (m_Driver.Bind(endpoint) != 0)
        {
            Debug.Log("Failed to bind to port " + UNetConfig.Port);
        }
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

        for (var i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            _playerId2connId[i] = default;
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
        for (var i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // Accept new connections
        NetworkConnection c;

        while ((c = m_Driver.Accept()) != default)
        {
            m_Connections.Add(c);
            Debug.Log("Accepted a connection");
            var player = FindFreePlayerId();
            _connId2playerId[c.InternalId] = player;
            _playerId2connId[player] = c;
            SendReliableToPlayer(new WelcomeToRoomMsg {PlayerIdx = player}, player);
        }

        for (var i = 0; i < m_Connections.Length; ++i)
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
                        switch ((InternalMsgId) msgId)
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
                    m_Connections[i] = default;
                }
            }
        }
    }


    public BinaryWriter BeginMessage(int msgId)
    {
        _writer.Write(msgId);

        return _writer;
    }


    public void EndMessageBroadcastReliableExceptPlayer(int player)
    {
        EndMessageBroadcastExceptPlayer(true, player);
    }


    public void EndMessageBroadcastUnreliableExceptPlayer(int player)
    {
        EndMessageBroadcastExceptPlayer(false, player);
    }


    private void EndMessageBroadcastExceptPlayer(bool reliable, int except)
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


    private void SendDataNoClear(NetworkConnection connId, bool reliable)
    {
        m_Driver.BeginSend(reliable ? reliablePipeline : NetworkPipeline.Null, connId, out var writer);

        unsafe
        {
            fixed (byte* pointerToFirst = _sendBuffer)
            {
                writer.WriteBytes(pointerToFirst, (int) _writerStream.Position);
            }
        }

        m_Driver.EndSend(writer);
    }


    private void Clear()
    {
        _writerStream.Position = 0;
    }


    private void SendDataAndClear(NetworkConnection connId, bool reliable)
    {
        SendDataNoClear(connId, reliable);
        Clear();
    }


    public void BroadcastCustomMessageReliable<TMsg>(TMsg msg, int receiverId)
        where TMsg : INetMessage
    {
        BroadcastCustomMessage(msg, receiverId, true);
    }


    public void BroadcastCustomMessageUnreliable<TMsg>(TMsg msg, int receiverId)
        where TMsg : INetMessage
    {
        BroadcastCustomMessage(msg, receiverId, false);
    }


    private void BroadcastCustomMessage<TMsg>(TMsg msg, int receiverId, bool reliable)
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
        where TMsg : INetMessage
    {
        Broadcast(msg, true);
    }


    public void BroadcastUnreliable<TMsg>(TMsg msg)
        where TMsg : INetMessage
    {
        Broadcast(msg, false);
    }


    private void Broadcast<TMsg>(TMsg msg, bool reliable)
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
        where TMsg : INetMessage
    {
        SendToPlayer(msg, player, true);
    }


    public void SendUnreliableToPlayer<TMsg>(TMsg msg, int player)
        where TMsg : INetMessage
    {
        SendToPlayer(msg, player, false);
    }


    private void SendToPlayer<TMsg>(TMsg msg, int player, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(_initialized, "Cannot send before Initialize()");

        _writer.Write(msg.MessageId);
        msg.Sync(_serializer);
        SendDataAndClear(_playerId2connId[player], reliable);
    }
}