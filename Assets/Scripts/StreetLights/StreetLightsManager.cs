using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public struct StreetLightEvent
{
    public string name;
    public float deltaTime;
    public CarSection[] carSections;
    public PedestrianSection[] pedestrianSections;
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


public class StreetLightsManager : MonoBehaviour
{
    [SerializeField]
    private StreetLightEvent[] streetLightEvents;
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

    private void ChangeCarLightsState(ref StreetLightEvent streetLightEvent)
    {
        if (streetLightEvent.carSections.Length == 0)
        {
            return;
        }

        foreach (CarSection section in streetLightEvent.carSections)
        {
            foreach (CarStreetLight light in section.carStreetLights)
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

    private void ChangePedestrianLightsState(ref StreetLightEvent streetLightEvent)
    {
        if (streetLightEvent.pedestrianSections.Length == 0)
        {
            return;
        }
        foreach (PedestrianSection section in streetLightEvent.pedestrianSections)
        {
            foreach (PedestrianStreetLight light in section.pedestrianLights)
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
