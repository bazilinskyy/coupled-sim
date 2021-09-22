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
    void Sync(ref string val);
    void Sync(ref List<string> val);
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
    public void Sync(ref string val) { val = _reader.ReadString(); }
    public void Sync(ref List<string> val) { val = _reader.ReadListString(); }
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
    public void Sync(ref string val) { _writer.Write(val); }
    public void Sync(ref List<string> val) { _writer.Write(val); }
    public void SyncListSubmessage<T>(ref List<T> val) where T : INetSubMessage
    {
        _writer.WriteListNetSubmessage(val);
    }
}

public static class SerializationHelpers
{
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
        if (reader.BaseStream.Position == reader.BaseStream.Length)
        {
            Debug.Log("Test");
        }
        q.x = reader.ReadSingle();
        if (reader.BaseStream.Position == reader.BaseStream.Length)
        {
            Debug.Log("Test");
        }
        q.y = reader.ReadSingle();
        if (reader.BaseStream.Position == reader.BaseStream.Length)
        {
            Debug.Log("Test");
        }
        q.z = reader.ReadSingle();
        if (reader.BaseStream.Position == reader.BaseStream.Length)
        {
            Debug.Log("Test");
        }
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

    public static void Write(this BinaryWriter writer, List<string> strings)
    {
        writer.Write(strings.Count);
        foreach (var val in strings)
        {
            writer.Write(val);
        }
    }

    public static List<string> ReadListString(this BinaryReader reader)
        => ReadListString(reader, new List<string>());
    public static List<string> ReadListString(this BinaryReader reader, List<string> buffer)
    {
        var count = reader.ReadInt32();
        buffer.Capacity = Math.Max(buffer.Capacity, count);
        for (int i = 0; i < count; i++)
        {
            buffer.Add(reader.ReadString());
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
}
