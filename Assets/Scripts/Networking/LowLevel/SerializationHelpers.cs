using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


public interface ISynchronizer
{
    void Sync(ref bool val);
    void Sync(ref int val);
    void Sync(ref float val);
    void Sync(ref Vector3 val);
    void Sync(ref Quaternion val);
    void Sync(ref List<int> val);
    void Sync(ref List<Vector3> val);
    void Sync(ref List<Quaternion> val);
    void SyncListSubmessage<T>(ref List<T> val) where T : INetSubMessage;
}

public struct Deserializer : ISynchronizer
{
    BinaryReader _reader;
    public Deserializer(BinaryReader reader) { _reader = reader; }
    public void Sync(ref bool val) { val = _reader.ReadBoolean(); }
    public void Sync(ref int val) { val = _reader.ReadInt32(); }
    public void Sync(ref float val) { val = _reader.ReadSingle(); }
    public void Sync(ref Vector3 val) { val = _reader.ReadVector3(); }
    public void Sync(ref Quaternion val) { val = _reader.ReadQuaternion(); }
    public void Sync(ref List<int> val) { val = _reader.ReadListInt(); }
    public void Sync(ref List<Vector3> val) { val = _reader.ReadListVector3(); }
    public void Sync(ref List<Quaternion> val) { val = _reader.ReadListQuaternion(); }
    public void SyncListSubmessage<T>(ref List<T> val) where T : INetSubMessage
    {
        val = _reader.ReadListNetSubmessage<T>();
    }
}

public struct Serializer : ISynchronizer
{
    BinaryWriter _writer;
    public Serializer(BinaryWriter writer) { _writer = writer; }
    public void Sync(ref bool val) { _writer.Write(val); }
    public void Sync(ref int val) { _writer.Write(val); }
    public void Sync(ref float val) { _writer.Write(val); }
    public void Sync(ref Vector3 val) { _writer.Write(val); }
    public void Sync(ref Quaternion val) { _writer.Write(val); }
    public void Sync(ref List<int> val) { _writer.Write(val); }
    public void Sync(ref List<Vector3> val) { _writer.Write(val); }
    public void Sync(ref List<Quaternion> val) { _writer.Write(val); }
    public void SyncListSubmessage<T>(ref List<T> val) where T : INetSubMessage
    {
        _writer.WriteListNetSubmessage(val);
    }
}

public class NDeserializer : ISynchronizer
{
    Unity.Collections.DataStreamReader _reader;
    public NDeserializer(ref Unity.Collections.DataStreamReader reader) { _reader = reader; }
    public void Sync(ref bool val) { val = _reader.ReadBoolean(); }
    public void Sync(ref int val) { val = _reader.ReadInt(); }
    public void Sync(ref float val) { val = _reader.ReadFloat(); }
    public void Sync(ref Vector3 val) { val = _reader.ReadVector3(); }
    public void Sync(ref Quaternion val) { val = _reader.ReadQuaternion(); }
    public void Sync(ref List<int> val) { val = _reader.ReadListInt(); }
    public void Sync(ref List<Vector3> val) { val = _reader.ReadListVector3(); }
    public void Sync(ref List<Quaternion> val) { val = _reader.ReadListQuaternion(); }
    public void SyncListSubmessage<T>(ref List<T> val) where T : INetSubMessage
    {
        val = _reader.ReadListNetSubmessage<T>();
    }
}

public static class SerializationHelpers
{
    public static bool ReadBoolean(this ref Unity.Collections.DataStreamReader reader)
    {
        return reader.ReadByte() != 0;
    }

    public static void Write(this BinaryWriter writer, Vector3 vec)
    {
        writer.Write(vec.x);
        writer.Write(vec.y);
        writer.Write(vec.z);
    }

    public static Vector3 ReadVector3(this BinaryReader reader)
    {
        Vector3 v;
        v.x = reader.ReadSingle();
        v.y = reader.ReadSingle();
        v.z = reader.ReadSingle();
        return v;
    }

    public static Vector3 ReadVector3(this ref Unity.Collections.DataStreamReader reader)
    {
        Vector3 v;
        v.x = reader.ReadFloat();
        v.y = reader.ReadFloat();
        v.z = reader.ReadFloat();
        return v;
    }

    public static void Write(this BinaryWriter writer, List<Vector3> vecs)
    {
        writer.Write(vecs.Count);
        foreach (var vec in vecs)
        {
            writer.Write(vec);
        }
    }

    public static List<Vector3> ReadListVector3(this BinaryReader reader)
        => ReadListVector3(reader, new List<Vector3>());

    public static List<Vector3> ReadListVector3(this BinaryReader reader, List<Vector3> buffer)
    {
        var count = reader.ReadInt32();
        buffer.Capacity = Math.Max(buffer.Capacity, count);
        for (int i = 0; i < count; i++)
        {
            buffer.Add(reader.ReadVector3());
        }
        return buffer;
    }

    public static List<Vector3> ReadListVector3(this ref Unity.Collections.DataStreamReader reader)
        => ReadListVector3(ref reader, new List<Vector3>());

    public static List<Vector3> ReadListVector3(this ref Unity.Collections.DataStreamReader reader, List<Vector3> buffer)
    {
        var count = reader.ReadInt();
        buffer.Capacity = Math.Max(buffer.Capacity, count);
        for (int i = 0; i < count; i++)
        {
            buffer.Add(reader.ReadVector3());
        }
        return buffer;
    }

    public static void Write(this BinaryWriter writer, List<Quaternion> vals)
    {
        writer.Write(vals.Count);
        foreach (var val in vals)
        {
            writer.Write(val);
        }
    }

    public static List<Quaternion> ReadListQuaternion(this BinaryReader reader)
        => ReadListQuaternion(reader, new List<Quaternion>());

    public static List<Quaternion> ReadListQuaternion(this BinaryReader reader, List<Quaternion> buffer)
    {
        var count = reader.ReadInt32();
        buffer.Capacity = Math.Max(buffer.Capacity, count);
        for (int i = 0; i < count; i++)
        {
            buffer.Add(reader.ReadQuaternion());
        }
        return buffer;
    }

    public static List<Quaternion> ReadListQuaternion(this ref Unity.Collections.DataStreamReader reader)
        => ReadListQuaternion(ref reader, new List<Quaternion>());

    public static List<Quaternion> ReadListQuaternion(this ref Unity.Collections.DataStreamReader reader, List<Quaternion> buffer)
    {
        var count = reader.ReadInt();
        buffer.Capacity = Math.Max(buffer.Capacity, count);
        for (int i = 0; i < count; i++)
        {
            buffer.Add(reader.ReadQuaternion());
        }
        return buffer;
    }

    public static void Write(this BinaryWriter writer, Quaternion quaternion)
    {
        var euler = quaternion.eulerAngles;
        writer.Write(euler.x);
        writer.Write(euler.y);
        writer.Write(euler.z);
    }

    public static Quaternion ReadQuaternion(this BinaryReader reader)
    {
        Vector3 q;
        q.x = reader.ReadSingle();
        q.y = reader.ReadSingle();
        q.z = reader.ReadSingle();
        return Quaternion.Euler(q);
    }

    public static Quaternion ReadQuaternion(this ref Unity.Collections.DataStreamReader reader)
    {
        Vector3 q;
        q.x = reader.ReadFloat();
        q.y = reader.ReadFloat();
        q.z = reader.ReadFloat();
        return Quaternion.Euler(q);
    }

    public static void Write(this BinaryWriter writer, List<int> ints)
    {
        writer.Write(ints.Count);
        foreach (var val in ints)
        {
            writer.Write(val);
        }
    }

    public static List<int> ReadListInt(this BinaryReader reader)
    {
        return ReadListInt(reader, new List<int>());
    }

    public static List<int> ReadListInt(this BinaryReader reader, List<int> buffer)
    {
        var count = reader.ReadInt32();
        buffer.Capacity = Math.Max(buffer.Capacity, count);
        for (int i = 0; i < count; i++)
        {
            buffer.Add(reader.ReadInt32());
        }
        return buffer;
    }

    public static void WriteListNetSubmessage<TMsg>(this BinaryWriter writer, List<TMsg> msgs)
        where TMsg : INetSubMessage
    {
        writer.Write(msgs.Count);
        foreach (var msg in msgs)
        {
            msg.SerializeTo(writer);
        }
    }

    public static List<TMsg> ReadListNetSubmessage<TMsg>(this BinaryReader reader)
        where TMsg : INetSubMessage
        => ReadListNetSubmessage<TMsg>(reader, new List<TMsg>());
    public static List<TMsg> ReadListNetSubmessage<TMsg>(this BinaryReader reader, List<TMsg> buffer)
        where TMsg : INetSubMessage
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var msg = default(TMsg);
            msg.DeserializeFrom(reader);
            buffer.Add(msg);
        }
        return buffer;
    }

    public static List<TMsg> ReadListNetSubmessage<TMsg>(this ref Unity.Collections.DataStreamReader reader)
        where TMsg : INetSubMessage
        => ReadListNetSubmessage<TMsg>(ref reader, new List<TMsg>());
    public static List<TMsg> ReadListNetSubmessage<TMsg>(this ref Unity.Collections.DataStreamReader reader, List<TMsg> buffer)
        where TMsg : INetSubMessage
    {
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            var msg = default(TMsg);
            msg.DeserializeFrom(ref reader);
            buffer.Add(msg);
        }
        return buffer;
    }

    public static List<int> ReadListInt(this ref Unity.Collections.DataStreamReader reader)
    {
        return ReadListInt(ref reader, new List<int>());
    }

    public static List<int> ReadListInt(this ref Unity.Collections.DataStreamReader reader, List<int> buffer)
    {
        var count = reader.ReadInt();
        buffer.Capacity = Math.Max(buffer.Capacity, count);
        for (int i = 0; i < count; i++)
        {
            buffer.Add(reader.ReadInt());
        }
        return buffer;
    }
}
