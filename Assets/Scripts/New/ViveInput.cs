using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class ViveInput : MonoBehaviour
{
    //public SteamVR_Action_Vector2 touchPadAction;

    private void Start()
    {
        SteamVR_Actions.viveController_GapAcceptance.AddOnStateDownListener(OnTriggerPressedOrReleased, SteamVR_Input_Sources.Any);
    }
    private void OnTriggerPressedOrReleased(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.Log("Trigger was pressed");
    }

    // Use touchpad as button
    /*void Update()
    {
        Vector2 touchPadValue = touchPadAction.GetAxis(SteamVR_Input_Sources.Any);

        if (touchPadValue != Vector2.zero)
        {
            print(touchPadValue);
        }
    }*/
}
