using System;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class LiveLogger : IDisposable
{
    UdpClient _socket;
    BinaryWriter _writer;
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

    public void Log(AICarSyncSystem aiCarSystem, PlayerSystem playerSystem)
    {
        if (_writer == null)
        {
            return;
        }
        var cars = aiCarSystem.Cars;
        _writer.Write(cars.Count);
        foreach (var car in aiCarSystem.Cars)
        {
            _writer.Write(car.transform.position);
        }
        var pedestrians = playerSystem.Avatars;
        _writer.Write(pedestrians.Count);
        foreach (var pedestrian in pedestrians)
        {
            _writer.Write(pedestrian.transform.position);
        }
        _socket.Send(_buffer, (int)_stream.Position);
        _stream.Position = 0;
    }

    public void Dispose() => _socket.Dispose();
}
