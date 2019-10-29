using System.Collections.Generic;
using System.IO;
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
    const int MaxPlayers = UNetConfig.MaxPlayers;
    public int NumRemotePlayers => _connectionIds.Count;
    const int NoPlayer = -1;

    List<int> _connectionIds = new List<int>();
    UNETTransport _transport = new UNETTransport();
    bool _initialized;

    byte[] _sendBuffer = new byte[UNetConfig.SendBufferSize];
    BinaryWriter _writer;
    MemoryStream _writerStream;
    Serializer _serializer;
    Dictionary<int, int> _connId2playerId = new Dictionary<int, int>();
    int[] _playerId2connId = new int[MaxPlayers];

    public bool PlayerConnected(int player) => _playerId2connId[player] != NoPlayer;

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

    public void Init()
    {
        _transport.Init(UNetConfig.Port, UNetConfig.MaxHostConnections);
        _initialized = true;
        _writerStream = new MemoryStream(_sendBuffer);
        _writer = new BinaryWriter(_writerStream);
        _serializer = new Serializer(_writer);
        for (int i = 0; i < UNetConfig.MaxPlayers; i++)
        {
            _playerId2connId[i] = NoPlayer;
        }
    }

    //processing network messages comming from clients
    public void Update(MessageDispatcher dispatch)
    {
        Assert.IsTrue(_initialized, "Update() should not be called before Initialize()");
        TransportEvent tEvent = new TransportEvent();
        while (_transport.NextEvent(ref tEvent))
        {
            switch (tEvent.type)
            {
                case TransportEvent.Type.Connect:
                    _connectionIds.Add(tEvent.connectionId);
                    var player = FindFreePlayerId();
                    _connId2playerId[tEvent.connectionId] = player;
                    _playerId2connId[player] = tEvent.connectionId;
                    SendReliableToPlayer(new WelcomeToRoomMsg() { PlayerIdx = player }, player);
                    break;
                case TransportEvent.Type.Disconnect:
                    _connectionIds.Remove(tEvent.connectionId);
                    break;
                case TransportEvent.Type.Data:
                {
                    var reader = new BinaryReader(new MemoryStream(tEvent.data));
                    var msgId = reader.ReadInt32();
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
                        dispatch.Dispatch(msgId, new Deserializer(reader), _connId2playerId[tEvent.connectionId]);
                    }
                    break;
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
        foreach (var connId in _connectionIds)
        {
            int i = _connId2playerId[connId];
            if (exceptConnId != connId)
            {
                SendDataNoClear(connId, reliable);
            }
        }
        Clear();
    }

    void SendDataNoClear(int connId, bool reliable)
    {
        if (reliable)
        {
            _transport.SendReliable(connId, _sendBuffer, (int)_writerStream.Position);
        }
        else
        {
            _transport.SendUnreliable(connId, _sendBuffer, (int)_writerStream.Position);
        }
    }

    void Clear() => _writerStream.Position = 0;

    void SendDataAndClear(int connId, bool reliable)
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
        foreach (var connId in _connectionIds)
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
        foreach (var connId in _connectionIds)
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
