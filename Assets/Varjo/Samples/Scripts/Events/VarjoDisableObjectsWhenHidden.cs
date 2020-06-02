using System.Collections.Generic;
using UnityEngine;
using Varjo;

/// <summary>
/// Toggle defined scripts and gameobjects on standby, foreground and visibility change events.
/// </summary>
public class VarjoDisableObjectsWhenHidden : MonoBehaviour
{
    public bool disableOnStandby = true;
    public bool disableInBackground = true;
    public bool disableIfNotVisible = true;

    public List<MonoBehaviour> scripts = new List<MonoBehaviour>();
    public List<GameObject> gameObjects = new List<GameObject>();

    bool standBy;
    bool inForeground;
    bool visible;

    void OnEnable()
    {
        standBy = false;
        inForeground = true;
        visible = true;

        if (disableOnStandby)
        {
            VarjoManager.OnStandbyEvent += StandbyEvent;
        }
        if (disableInBackground)
        {
            VarjoManager.OnForegroundEvent += ForegroundEvent;
        }
        if (disableIfNotVisible)
        {
            VarjoManager.OnVisibilityEvent += VisibilityEvent;
        }
    }

    void OnDisable()
    {
        VarjoManager.OnStandbyEvent -= StandbyEvent;
        VarjoManager.OnForegroundEvent -= ForegroundEvent;
        VarjoManager.OnVisibilityEvent -= VisibilityEvent;
    }

    public void StandbyEvent(bool state)
    {
        standBy = state;
        ToggleDefined();
    }

    public void ForegroundEvent(bool state)
    {
        inForeground = state;
        ToggleDefined();
    }

    public void VisibilityEvent(bool state)
    {
        visible = state;
        ToggleDefined();
    }

    public void ToggleDefined()
    {
        // Disable defined gameobjects and scripts if the device is in standby or if the application is in the background or not visible.
        bool state = !standBy && inForeground && visible;

        for (int i = 0; i < scripts.Count; ++i)
        {
            if (scripts[i] != null)
            {
                scripts[i].enabled = state;
            }
        }

        for (int i = 0; i < gameObjects.Count; ++i)
        {
            if (gameObjects[i] != null)
            {
                gameObjects[i].SetActive(state);
            }
        }
    }
}

