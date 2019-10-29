using System;
using System.Collections.Generic;
using UnityEngine;

public class StreetLightsSystem : MonoBehaviour
{
    struct ChangeLightsMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_ChangeLights;

        public int SystemIdx;
        public int EventIdx;
        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref EventIdx);
        }
    }

    public StreetLightsManager[] LightManagers;
    List<int> _triggeredEventsBuffer = new List<int>();
    public CarStreetLight[] CarLights;
    public PedestrianStreetLight[] PedestrianLights;

    void Awake()
    {
        CarLights = GetComponentsInChildren<CarStreetLight>();
        PedestrianLights = GetComponentsInChildren<PedestrianStreetLight>();
    }

    // HOST
    public void UpdateHost(UNetHost host)
    {
        for (int i = 0; i < LightManagers.Length; i++)
        {
            var manager = LightManagers[i];
            manager.UpdateHost(_triggeredEventsBuffer);
            foreach (var trigger in _triggeredEventsBuffer)
            {
                host.BroadcastReliable(new ChangeLightsMsg
                {
                    SystemIdx = i,
                    EventIdx = trigger
                });
            }
            _triggeredEventsBuffer.Clear();
        }
    }

    // CLIENT
    public void RegisterHandlers(MessageDispatcher dispatcher)
    {
        dispatcher.AddLevelMessageHandler((int)MsgId.S_ChangeLights, OnChangeLightsMsg);
    }

    private void OnChangeLightsMsg(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<ChangeLightsMsg>(sync);
        LightManagers[msg.SystemIdx].TriggerEvent(msg.EventIdx);
    }
}
