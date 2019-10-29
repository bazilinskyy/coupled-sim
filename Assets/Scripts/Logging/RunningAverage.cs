using System;
using Unity.Collections;
using UnityEngine;

public struct RunningAverage
{
    Vector3[] _buffer;
    int _count;
    int _next;

    Vector3 average(Vector3[] buffer, int count)
    {
        Vector3 sum = default;
        for (int i = 0; i < count; i++)
        {
            sum += buffer[i];
        }
        return sum / count;
    }
    public RunningAverage(int frames)
    {
        _buffer = new Vector3[frames];
        _count = 0;
        _next = 0;
    }

    public void Add(Vector3 val)
    {
        _buffer[_next] = val;
        _count = Math.Min(_count + 1, _buffer.Length);
        _next = (_next + 1) % _buffer.Length;
    }

    public Vector3 Get() => average(_buffer, _count);
}
