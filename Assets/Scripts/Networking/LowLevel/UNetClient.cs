using System.IO;
using System.Net;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Assertions;


//low level UNET networking client implementation - message sending and handling
public class UNetClient
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public bool Done;

    private readonly byte[] _sendBuffer = new byte[UNetConfig.SendBufferSize];
    private Serializer _serializer;
    private BinaryWriter _writer;
    private MemoryStream _writerStream;
    private NetworkPipeline reliablePipeline;

    public bool ConnectionEstablished => m_Connection != default;
    public bool InRoom => MyPlayerId != -1;
    public int MyPlayerId { get; private set; } = -1;


    public void Init()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default;
        reliablePipeline = m_Driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));


        _writerStream = new MemoryStream(_sendBuffer);
        _writer = new BinaryWriter(_writerStream);
        _serializer = new Serializer(_writer);
    }


    public void Connect(string ip)
    {
        Assert.IsNotNull(ip);
        var endpoint = NetworkEndPoint.LoopbackIpv4;
        IPAddress ipAddress;

        if (IPAddress.TryParse(ip, out ipAddress))
        {
            var ipOut = new NativeArray<byte>(ipAddress.GetAddressBytes(), Allocator.Temp);
            endpoint.SetRawAddressBytes(ipOut);
        }

        endpoint.Port = UNetConfig.Port;
        m_Connection = m_Driver.Connect(endpoint);
    }


    public void Disconnect()
    {
        Assert.IsTrue(ConnectionEstablished);
        m_Driver.Dispose();
        m_Connection = default;
    }


    //processing network messages comming from clients
    public void Update(MessageDispatcher dispatch)
    {
        Assert.IsTrue(ConnectionEstablished, "Update() should not be called before Connect()");

        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            if (!Done)
            {
                Debug.Log("Something went wrong during connect");
            }

            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;

        while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                dispatch.HandleConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                Done = true;

                var msgId = stream.ReadInt();
                var sync = new NDeserializer(ref stream);

                if (NetMsg.IsInternal(msgId))
                {
                    switch ((InternalMsgId) msgId)
                    {
                        case InternalMsgId.WelcomeToRoom:
                        {
                            var msg = new WelcomeToRoomMsg();
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
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client got disconnected from server");
                m_Connection = default;
            }
        }
    }


    public BinaryWriter BeginMessage(int msgId)
    {
        _writer.Write(msgId);

        return _writer;
    }


    public void EndMessageSendReliable()
    {
        EndMessageSend(true);
    }


    public void EndMessageSendUnreliable()
    {
        EndMessageSend(false);
    }


    private void EndMessageSend(bool reliable)
    {
        SendData(reliable);
    }


    private void SendData(bool reliable)
    {
        m_Driver.BeginSend(reliable ? reliablePipeline : NetworkPipeline.Null, m_Connection, out var writer);

        unsafe
        {
            fixed (byte* pointerToFirst = _sendBuffer)
            {
                writer.WriteBytes(pointerToFirst, (int) _writerStream.Position);
            }
        }

        m_Driver.EndSend(writer);
        _writerStream.Position = 0;
    }


    public void SendReliable<TMsg>(TMsg msg)
        where TMsg : INetMessage
    {
        Send(msg, true);
    }


    public void SendUnreliable<TMsg>(TMsg msg)
        where TMsg : INetMessage
    {
        Send(msg, false);
    }


    private void Send<TMsg>(TMsg msg, bool reliable)
        where TMsg : INetMessage
    {
        Assert.IsTrue(ConnectionEstablished);
        _writer.Write(msg.MessageId);
        msg.Sync(_serializer);
        SendData(reliable);
    }
}