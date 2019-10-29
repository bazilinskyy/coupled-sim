using System.IO;
using UnityEngine;
using UnityEngine.Assertions;

//low level UNET networking client implementation - message sending and handling
public class UNetClient
{
    const int NotConnected = -1;
    UNETTransport _transport = new UNETTransport();
    int _connectionId = NotConnected;
    public bool ConnectionEstablished => !HasError && _connectionId != NotConnected;
    public bool InRoom => MyPlayerId != -1;
    public int MyPlayerId { get; private set; } = -1;
    public bool HasError = false;
    public string GetError() => _transport.ErrorToString(_error);
    int _error;

    byte[] _sendBuffer = new byte[UNetConfig.SendBufferSize];
    Serializer _serializer;
    BinaryWriter _writer;
    MemoryStream _writerStream;

    public void Init()
    {
        _transport.Init(0, 1);
        _writerStream = new MemoryStream(_sendBuffer);
        _writer = new BinaryWriter(_writerStream);
        _serializer = new Serializer(_writer);
    }

    public void Connect(string ip)
    {
        Assert.IsNotNull(ip);
        _connectionId = _transport.Connect(ip, UNetConfig.Port, out _error);
        if (_error != 0)
        {
            HasError = true;
        }
    }

    public void Disconnect()
    {
        Assert.IsTrue(ConnectionEstablished);
        _transport.Shutdown();
        _transport.Init(0, 1);
        _connectionId = NotConnected;
    }

    //processing network messages comming from clients
    public void Update(MessageDispatcher dispatch)
    {
        if (HasError) return;
        Assert.IsTrue(ConnectionEstablished, "Update() should not be called before Connect()");
        var tEvent = new TransportEvent();
        while (_transport.NextEvent(ref tEvent))
        {
            switch (tEvent.type)
            {
                case TransportEvent.Type.None:
                    break;
                case TransportEvent.Type.Connect:
                    dispatch.HandleConnect();
                    break;
                case TransportEvent.Type.Disconnect:
                    break;
                case TransportEvent.Type.Data:
                {
                    var reader = new BinaryReader(new MemoryStream(tEvent.data));
                    var msgId = reader.ReadInt32();
                    var sync = new Deserializer(reader);
                    if (NetMsg.IsInternal(msgId))
                    {
                        switch ((InternalMsgId)msgId)
                        {
                            case InternalMsgId.WelcomeToRoom:
                            {
                                WelcomeToRoomMsg msg = new WelcomeToRoomMsg();
                                msg.Sync(sync);
                                MyPlayerId = msg.PlayerIdx;
                                break;
                            }
                        }
                    }
                    else
                    {
                        dispatch.Dispatch(msgId, sync, Host.PlayerId);
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

    public void EndMessageSendReliable() => EndMessageSend(true);
    public void EndMessageSendUnreliable() => EndMessageSend(false);
    void EndMessageSend(bool reliable)
    {
        SendData(reliable);
    }

    void SendData(bool reliable)
    {
        if (reliable)
        {
            _transport.SendReliable(_connectionId, _sendBuffer, (int)_writerStream.Position);
        }
        else
        {
            _transport.SendUnreliable(_connectionId, _sendBuffer, (int)_writerStream.Position);
        }
        _writerStream.Position = 0;
    }

    public void SendReliable<TMsg>(TMsg msg)
            where TMsg : INetMessage
        => Send(msg, true);

    public void SendUnreliable<TMsg>(TMsg msg)
            where TMsg : INetMessage
        => Send(msg, false);

    void Send<TMsg>(TMsg msg, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(ConnectionEstablished);
        _writer.Write(msg.MessageId);
        msg.Sync(_serializer);
        SendData(reliable);
    }
}
