using System.IO;
using Unity.Networking.Transport;

//base interface for all network messages
public interface INetMessage
{
    int MessageId { get; }
    void Sync<T>(T synchronizer) where T : ISynchronizer;
}

public interface INetSubMessage
{
    void SerializeTo(BinaryWriter writer);
    void DeserializeFrom(BinaryReader reader);
    void DeserializeFrom(ref DataStreamReader reader);
}
