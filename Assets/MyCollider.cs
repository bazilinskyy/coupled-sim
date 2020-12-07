using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyCollider : MonoBehaviour
{
    private bool isTriggered = false;
    private float triggerTime;
    private float timeOutTime = 4f;
    void Update()
    {
        ResetTriggerBoolean();
    }
    public bool Triggered()
    {
        if (isTriggered) { return false; }
        else
        {
            isTriggered = true;
            triggerTime = Time.time;
            return true;
        }
    }

    void ResetTriggerBoolean()
    {
        if (!isTriggered) { return; }
        if ((triggerTime + timeOutTime) < Time.time) { isTriggered = false; }
    }
}
