public enum InternalMsgId
{
    WelcomeToRoom = -1,
}

public struct WelcomeToRoomMsg : INetMessage
{
    public int MessageId => (int)InternalMsgId.WelcomeToRoom;
    public int PlayerIdx;

    public void Sync<T>(T synchronizer) where T : ISynchronizer
    {
        synchronizer.Sync(ref PlayerIdx);
    }
}