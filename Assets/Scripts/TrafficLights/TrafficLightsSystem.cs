using System;
using System.Collections.Generic;
using UnityEngine;

//script that synchronizes traffic lights between all players
public class TrafficLightsSystem : MonoBehaviour
{
    //traffic light sync message
    struct ChangeLightsMsg : INetMessage
    {
        public int MessageId => (int)MsgId.S_ChangeLights;

        public int SystemIdx;
        public int EventIdx;
        public bool InitialSetup;

        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref SystemIdx);
            synchronizer.Sync(ref EventIdx);
            synchronizer.Sync(ref InitialSetup);
        }
    }

    //synced traffic lights managers
    public TrafficLightsManager[] LightManagers;
    List<int> _initiallyTriggeredEventsBuffer = new List<int>();
    List<int> _triggeredEventsBuffer = new List<int>();
    [HideInInspector]
    public CarTrafficLight[] CarLights;
    [HideInInspector]
    public PedestrianTrafficLight[] PedestrianLights;

    void Awake()
    {
        CarLights = GetComponentsInChildren<CarTrafficLight>();
        PedestrianLights = GetComponentsInChildren<PedestrianTrafficLight>();
    }

    //updates trafficlight cycles and sends sync messages for traffic light systems (host only method)
    public void UpdateHost(UNetHost host)
    {
        for (int i = 0; i < LightManagers.Length; i++)
        {
            var manager = LightManagers[i];
            manager.UpdateHost(_initiallyTriggeredEventsBuffer, _triggeredEventsBuffer);
            foreach (var trigger in _initiallyTriggeredEventsBuffer)
            {
                host.BroadcastReliable(new ChangeLightsMsg
                {
                    SystemIdx = i,
                    EventIdx = trigger
                });
            }
            foreach (var trigger in _triggeredEventsBuffer)
            {
                host.BroadcastReliable(new ChangeLightsMsg
                {
                    SystemIdx = i,
                    EventIdx = trigger
                });
            }
            _initiallyTriggeredEventsBuffer.Clear();
            _triggeredEventsBuffer.Clear();
        }
    }

    public void RegisterHandlers(MessageDispatcher dispatcher)
    {
        dispatcher.AddLevelMessageHandler((int)MsgId.S_ChangeLights, OnChangeLightsMsg);
    }

    //handles traffic light sync messages on client
    private void OnChangeLightsMsg(ISynchronizer sync, int srcPlayerId)
    {
        var msg = NetMsg.Read<ChangeLightsMsg>(sync);
        LightManagers[msg.SystemIdx].TriggerEvent(msg.EventIdx, msg.InitialSetup);
    }
}
