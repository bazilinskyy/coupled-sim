using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class ViveInput : MonoBehaviour
{
    //public SteamVR_Action_Vector2 touchPadAction;
    private float triggerPulled;

    private void Start()
    {
        SteamVR_Actions.viveController_GapAcceptance.AddOnStateDownListener(OnTriggerPressed, SteamVR_Input_Sources.Any);
        SteamVR_Actions.viveController_GapAcceptance.AddOnStateUpListener(OnTriggerReleased, SteamVR_Input_Sources.Any);
    }
    private void OnTriggerPressed(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.LogError("Trigger was pressed");
        triggerPulled = 1.0f;
    }
    private void OnTriggerReleased(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.LogError("Trigger was released");
        triggerPulled = 0.0f;
    }

    /*private void Update()
    {
        Debug.Log($"Trigger value = {triggerPulled}");
    }*/

    public float getGapAcceptance()
    {
        return triggerPulled;
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
