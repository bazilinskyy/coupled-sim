using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

//dispatched network messages to subscribed handlers
public class MessageDispatcher
{
    public delegate void MessageHandler(ISynchronizer sync, int srcPlayerId);
    Dictionary<int, MessageHandler> _dispatchIndex = new Dictionary<int, MessageHandler>();
    List<int> _levelMessageHandlers = new List<int>();

    /// <summary>
    /// Adds a handler that is never removed for the lifetime of this dispatcher
    /// </summary>
    public void AddStaticHandler(int msgId, MessageHandler handler)
    {
        _dispatchIndex.Add(msgId, handler);
    }

    /// <summary>
    /// Adds a handler that can be removed via ClearLevelMessageHandlers
    /// </summary>
    /// <param name="msgId"></param>
    /// <param name="handler"></param>
    public void AddLevelMessageHandler(int msgId, MessageHandler handler)
    {
        AddStaticHandler(msgId, handler);
        _levelMessageHandlers.Add(msgId);
    }

    public void ClearLevelMessageHandlers()
    {
        foreach (var msgId in _levelMessageHandlers)
        {
            _dispatchIndex.Remove(msgId);
        }
    }

    public void Dispatch(int msgId, Deserializer reader, int playerId)
    {
        Assert.IsTrue(_dispatchIndex.ContainsKey(msgId), "The message handler was not registered in the dispatcher");
        _dispatchIndex[msgId](reader, playerId);
    }

    public Action HandleConnect;
}
