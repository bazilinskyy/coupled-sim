using System;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class LiveLogger : IDisposable
{
    UdpClient _socket;
    public BinaryWriter _writer;
    MemoryStream _stream;
    byte[] _buffer;
    const int Port = 40131;

    public void Init()
    {
        _socket = new UdpClient();
        _socket.Connect("localhost", Port);
        _buffer = new byte[4 * 1024];
        _stream = new MemoryStream(_buffer);
        _writer = new BinaryWriter(_stream);
    }

    public void Log()
    {
        _socket.Send(_buffer, (int)_stream.Position);
        _stream.Position = 0;
    }

    public void FrameMetadata(
        int localDriver,
        int numPersistentDrivers,
        int numPedestrians,
        int numCarLights,
        int numPedestrianLights
        )
    {
        _writer.Write(localDriver);
        _writer.Write(numPersistentDrivers);
        _writer.Write(numPedestrians);
        _writer.Write(numCarLights);
        _writer.Write(numPedestrianLights);
    }

    public void Dispose() => _socket.Dispose();
}
