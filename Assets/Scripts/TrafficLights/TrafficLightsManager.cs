using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public struct TrafficLightEvent
{
    //miningful description of cycle event
    public string name;
    //time that has to pass since previous event before activating this event
    public float deltaTime;

    //sections on which state change will be applied on
    public CarSection[] carSections;
    public PedestrianSection[] pedestrianSections;

    //state to be set on the carSections and pedestrainSections
    public LightState state; 
}

public enum LightState
{
    GREEN,
    BLINK_GREEN,
    RED,
    YELLOW,
    RED_AND_YELLOW,
    LOOP_BACK
}

//defines and executes traffic light cycle described as a sequence of TrafficLightEvents
public class TrafficLightsManager : MonoBehaviour
{
    [SerializeField]
    private TrafficLightEvent[] streetLightEvents;
    float _timer = 0;
    int _index = 0;

    public void UpdateHost(List<int> triggeredEvents)
    {
        while (_timer >= streetLightEvents[_index].deltaTime && _index < streetLightEvents.Length)
        {
            triggeredEvents.Add(_index);
            TriggerEvent(_index);
            _timer = 0;
            _index++;
            if (_index >= streetLightEvents.Length)
            {
                _index = 0;
            }
        }

        _timer += Time.deltaTime;
    }

    public void TriggerEvent(int idx)
    {
        ChangeCarLightsState(ref streetLightEvents[idx]);
        ChangePedestrianLightsState(ref streetLightEvents[idx]);
    }

    private void ChangeCarLightsState(ref TrafficLightEvent streetLightEvent)
    {
        if (streetLightEvent.carSections.Length == 0)
        {
            return;
        }

        foreach (CarSection section in streetLightEvent.carSections)
        {
            foreach (CarTrafficLight light in section.carStreetLights)
            {
                switch (streetLightEvent.state)
                {
                    case LightState.GREEN:
                        light.TurnGreen();
                        break;

                    case LightState.YELLOW:
                        light.TurnYellow();
                        break;

                    case LightState.RED:
                        light.TurnRed();
                        break;

                    case LightState.RED_AND_YELLOW:
                        light.TurnRedAndYellow();
                        break;

                    case LightState.LOOP_BACK:
                        break;
                }
            }
        }
    }

    private void ChangePedestrianLightsState(ref TrafficLightEvent streetLightEvent)
    {
        if (streetLightEvent.pedestrianSections.Length == 0)
        {
            return;
        }
        foreach (PedestrianSection section in streetLightEvent.pedestrianSections)
        {
            foreach (PedestrianTrafficLight light in section.pedestrianLights)
            {
                switch (streetLightEvent.state)
                {
                    case LightState.GREEN:
                        light.TurnGreen();
                        break;
                    case LightState.RED:
                        light.TurnRed();
                        break;

                    case LightState.BLINK_GREEN:
                        light.TurnBlink();
                        break;
                    case LightState.LOOP_BACK:
                        break;
                }
            }
        }
    }

}
