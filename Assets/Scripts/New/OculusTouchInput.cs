using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class OculusTouch : MonoBehaviour
{
    private float buttonPressed;

    private void Start()
    {
        SteamVR_Actions.safetyButton_OculusTouch_SafetyButton.AddOnStateDownListener(OnButtonPressed, SteamVR_Input_Sources.Any);
        SteamVR_Actions.safetyButton_OculusTouch_SafetyButton.AddOnStateUpListener(OnButtonReleased, SteamVR_Input_Sources.Any);
    }
    private void OnButtonPressed(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.LogError("SafetyButton is pressed");
        buttonPressed = 1.0f;
    }
    private void OnButtonReleased(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.LogError("SafetyButton is not pressed");
        buttonPressed = 0.0f;
    }

    public float getSafetyButton()
    {
        return buttonPressed;
    }
}
