using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BlinkerState
{
    None,
    Left,
    Right
}

public class CarBlinkers : MonoBehaviour
{
    public BlinkerState State;
    [SerializeField]
    float blinkInterval = 0.25f;

    [SerializeField]
    MeshRenderer[] leftBlinkers;
    [SerializeField]
    MeshRenderer[] rightBlinkers;

    [SerializeField]
    Material lightOn;
    [SerializeField]
    Material lightOff;

    void Awake () {
        Stop();
    }

    public void StartLeftBlinkers()
    {
        TurnOnBlinkers(leftBlinkers);
        State = BlinkerState.Left;
    }

    public void StartRightBlinkers()
    {
        TurnOnBlinkers(rightBlinkers);
        State = BlinkerState.Right;
    }

    private void TurnOnBlinkers(MeshRenderer[] blinkerRenderers)
    {
        Stop();
        StartCoroutine(Blink(blinkerRenderers));
    }

    public void Stop()
    {
        State = BlinkerState.None;
        StopAllCoroutines();
        ResetBlinkers();
    }


    private void ResetBlinkers()
    {
        foreach (MeshRenderer renderer in leftBlinkers)
        {
            renderer.material = lightOff;
        }
        foreach (MeshRenderer renderer in rightBlinkers)
        {
            renderer.material = lightOff;
        }
    }

    private IEnumerator Blink(MeshRenderer[] blinkerRenderers)
    {
        while(true)
        {
            yield return new WaitForSeconds(blinkInterval);
            foreach(MeshRenderer renderer in blinkerRenderers)
            {
                renderer.material = lightOn;
            }
            yield return new WaitForSeconds(blinkInterval);
            foreach (MeshRenderer renderer in blinkerRenderers)
            {
                renderer.material = lightOff;
            }
        }
    }

    public void SwitchToState(BlinkerState state)
    {
        if (State == state) return;
        switch (state)
        {
            case BlinkerState.None:
                Stop();
                break;
            case BlinkerState.Left:
                StartLeftBlinkers();
                break;
            case BlinkerState.Right:
                StartRightBlinkers();
                break;
        }
    }
}
