using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class OculusTouchInput : MonoBehaviour
{

    // A reference to the action
    public SteamVR_Action_Boolean SafetyButtonPressedNotPressed;

    // A reference to the hand
    public SteamVR_Input_Sources handType;

    //reference to the sphere
    //public GameObject Sphere;

    private float buttonPressed;

    void Start()
    {
        SafetyButtonPressedNotPressed.AddOnStateDownListener(ButtonPressed, handType);
        SafetyButtonPressedNotPressed.AddOnStateUpListener(ButtonNotPressed, handType);

        //SteamVR_Actions.safetyButton_OculusTouch_SafetyButton.AddOnStateDownListener(OnButtonPressed, SteamVR_Input_Sources.Any);
        //SteamVR_Actions.safetyButton_OculusTouch_SafetyButton.AddOnStateUpListener(OnButtonReleased, SteamVR_Input_Sources.Any);
    }
    public void ButtonPressed(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.LogError("SafetyButton is pressed");
        buttonPressed = 1.0f;
    }
    public void ButtonNotPressed(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.LogError("SafetyButton is not pressed");
        buttonPressed = 0.0f;
    }

    public float getSafetyButton()
    {
        return buttonPressed;
    }
}
