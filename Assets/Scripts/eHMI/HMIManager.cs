using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public enum HMIState
{
    DISABLED,
    STOP,
    WALK,
}

//script that synchronizes hmi state between all players
public class HMIManager : MonoBehaviour
{
    List<HMI> _hmis = new List<HMI>();
    bool _isHost;
    UNetHost _host;
    UNetClient _client;

    public void InitHost(UNetHost host, MessageDispatcher dispatcher)
    {
        _host = host;
        _isHost = true;
        dispatcher.AddStaticHandler((int)MsgId.C_RequestHMIChange, OnRequestHMIChange);
    }

    public void InitClient(UNetClient client, MessageDispatcher dispatcher)
    {
        _client = client;
        _isHost = false;
        dispatcher.AddStaticHandler((int)MsgId.S_ChangeHMI, OnChangeHMI);
    }

    public void AddHMI(HMI hmi)
    {
        _hmis.Add(hmi);
    }

    //host GUI displaying controls for all HMI present in the game session
    public void DoHostGUI(Host host)
    {
        for (int i = 0; i < _hmis.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"eHMI {i}:");

            var Variants = Enum.GetValues(typeof(HMIState));
            for (int j = 0; j < Variants.Length; j++)
            {
                var state = (HMIState)Variants.GetValue(j);
                var stateName = Enum.GetName(typeof(HMIState), state);
                if (GUILayout.Button($"{stateName}"))
                {
                    _hmis[i].Display(state);
                    host.BroadcastMessage(new ChangeHMI
                    {
                        HMI = i,
                        State = (int)state
                    });
                }
            }

            GUILayout.EndHorizontal();
        }
	}

    //hmi syncing message
    struct RequestHMIChangeMsg : INetMessage
    {
        public int MessageId => (int)MsgId.C_RequestHMIChange;
        public int HMI;
        public int State;

        public void Sync<T>(T synchronizer) where T : ISynchronizer
        {
            synchronizer.Sync(ref HMI);
            synchronizer.Sync(ref State);
        }
    }

    //called when hmi state should be changed
    //on host - changes hmi state and dispatches sync message
    //on client - sends a request to the host to broadcast hmi state change message, requesting client (same as other clients) will change its hmi state as a result of message sent by the host
    public void RequestHMIChange(HMI hmiToChange, HMIState requestedState)
    {
        var idx = _hmis.IndexOf(hmiToChange);
        Assert.AreNotEqual(-1, idx, $"Tried to change HMI {hmiToChange} but it was not registered in the HMI Manager");
        if (_isHost)
        {
            hmiToChange.Display(requestedState);
            _host.BroadcastReliable(new ChangeHMI
            {
                HMI = idx,
                State = (int)requestedState,
            });
        }
        else
        {
            _client.SendReliable(new RequestHMIChangeMsg
            {
                HMI = idx,
                State = (int)requestedState,
            });
        }
    }

    // Client only - handling hmi state sync message
    public void OnChangeHMI(ISynchronizer sync, int _)
    {
        var msg = NetMsg.Read<ChangeHMI>(sync);
        _hmis[msg.HMI].Display((HMIState)msg.State);
    }


    // Host only - handling client requests to changes hmi state
    void OnRequestHMIChange(ISynchronizer sync, int _)
    {
        var msg = NetMsg.Read<RequestHMIChangeMsg>(sync);
        RequestHMIChange(_hmis[msg.HMI], (HMIState)msg.State);
    }
}
