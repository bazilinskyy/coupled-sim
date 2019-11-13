
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;
using UnityEngine.Profiling;

//UNET-based network communication pipe
public class UNETTransport
{
    public const int MaxPackageSize = 2048;
    public int hostId => m_HostId;

    //initializes and configures Unity networking
    public bool Init(int port = 0, int maxConnections = 16)
    {
        var config = new GlobalConfig
        {
            ThreadAwakeTimeout = 1
        };
        NetworkTransport.Init(config);

        m_ReadBuffer = new byte[MaxPackageSize + 1024];

        m_ConnectionConfig = new ConnectionConfig();
        m_ConnectionConfig.SendDelay = 0;

        m_ChannelUnreliable = m_ConnectionConfig.AddChannel(QosType.UnreliableFragmented);
        m_ChannelReliable = m_ConnectionConfig.AddChannel(QosType.ReliableSequenced);
        m_Topology = new HostTopology(m_ConnectionConfig, maxConnections);

        if (Debug.isDebugBuild && m_isNetworkSimuationActive)
        {
            m_HostId = NetworkTransport.AddHostWithSimulator(m_Topology, 1, 300, port);
        }
        else
        {
            m_HostId = NetworkTransport.AddHost(m_Topology, port);
        }

        if (m_HostId != -1 && port != 0)
        {
            Debug.Log("Listening on " + string.Join(", ", NetworkUtils.GetLocalInterfaceAddresses().ToArray()) + " on port " + port);
        }

        return m_HostId != -1;
    }

    public void Shutdown()
    {
        if (m_HostId != -1)
        {
            NetworkTransport.RemoveHost(m_HostId);
            m_HostId = -1;
        }
    }

    //connects to specified host
    public int Connect(string ip, int port, out int error)
    {
        int res;
        byte err;
        if (Debug.isDebugBuild && m_isNetworkSimuationActive)
        {
            var simulationConfig = new ConnectionSimulatorConfig(48, 50, 48, 50, 10);
            res = NetworkTransport.ConnectWithSimulator(m_HostId, ip, port, 0, out err, simulationConfig);
        }
        else
        {
            res = NetworkTransport.Connect(m_HostId, ip, port, 0, out err);
        }
        if (err == 0)
        {
            error = 0;
            return res;
        }
        else
        {
            error = (int)err;
            return -1;
        }
    }

    public string ErrorToString(int error) => ((NetworkError)error).ToString();

    public void Disconnect(int connectionId)
    {
        byte error;
        NetworkTransport.Disconnect(m_HostId, connectionId, out error);
    }

    //network messages handling logic
    public bool NextEvent(ref TransportEvent res)
    {
        Debug.Assert(m_HostId > -1, "Trying to update transport with no host id");

        Profiler.BeginSample("UNETTransform.ReadData()");

        int connectionId;
        int channelId;
        int receivedSize;
        byte error;

        var ne = NetworkTransport.ReceiveFromHost(m_HostId, out connectionId, out channelId, m_ReadBuffer, m_ReadBuffer.Length, out receivedSize, out error);

        switch (ne)
        {
            default:
            case NetworkEventType.Nothing:
                Profiler.EndSample();
                return false;
            case NetworkEventType.ConnectEvent:
            {
                string address;
                int port;
                NetworkID network;
                NodeID dstNode;
                NetworkTransport.GetConnectionInfo(m_HostId, connectionId, out address, out port, out network, out dstNode, out error);
                Debug.Log("New Connection: " + connectionId + " (from " + address + ":" + port + ")");

                res.type = TransportEvent.Type.Connect;
                res.connectionId = connectionId;
                break;
            }
            case NetworkEventType.DisconnectEvent:
                res.type = TransportEvent.Type.Disconnect;
                res.connectionId = connectionId;
                break;
            case NetworkEventType.DataEvent:
                res.type = TransportEvent.Type.Data;
                res.data = m_ReadBuffer;
                res.dataSize = receivedSize;
                res.connectionId = connectionId;
                break;
        }

        Profiler.EndSample();

        return true;
    }

    public void SendReliable(int connectionId, byte[] data, int sendSize)
        => SendData(connectionId, data, sendSize, m_ChannelReliable);
    public void SendUnreliable(int connectionId, byte[] data, int sendSize)
        => SendData(connectionId, data, sendSize, m_ChannelUnreliable);

    //sends network message
    void SendData(int connectionId, byte[] data, int sendSize, int channel)
    {
        Profiler.BeginSample("UNETTransform.SendData()");

        byte error;
        if (!NetworkTransport.Send(m_HostId, connectionId, channel, data, sendSize, out error))
        {
            Debug.Log("Error while sending data to connection : " + connectionId + "(error : " + (NetworkError)error + ")");
        }

        Profiler.EndSample();
    }

    public string GetConnectionDescription(int connectionId)
    {
        string address;
        int port;
        NetworkID network;
        NodeID dstNode;
        byte error;
        NetworkTransport.GetConnectionInfo(m_HostId, connectionId, out address, out port, out network, out dstNode, out error);
        return "UNET: " + address + ":" + port;
    }

    byte[] m_ReadBuffer;

    bool m_isNetworkSimuationActive = false;

    ConnectionConfig m_ConnectionConfig;
    HostTopology m_Topology;

    int m_HostId = -1;
    int m_ChannelUnreliable;
    int m_ChannelReliable;
}