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
//manages car blinkers both light and dashboard indicators
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
    GameObject[] leftBlinkerObjects;
    [SerializeField]
    GameObject[] rightBlinkerObjects;

    [SerializeField]
    Material lightOn;
    [SerializeField]
    Material lightOff;

    void Awake () {
        Stop();
    }

    public void StartLeftBlinkers()
    {
        TurnOnBlinkers(leftBlinkers, leftBlinkerObjects);
        State = BlinkerState.Left;
    }

    public void StartRightBlinkers()
    {
        TurnOnBlinkers(rightBlinkers, rightBlinkerObjects);
        State = BlinkerState.Right;
    }

    private void TurnOnBlinkers(MeshRenderer[] blinkerRenderers, GameObject[] blinkerObjects)
    {
        Stop();
        StartCoroutine(Blink(blinkerRenderers, blinkerObjects));
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

        foreach (var obj in leftBlinkerObjects)
        {
            obj.SetActive(false);
        }

        foreach (var obj in rightBlinkerObjects)
        {
            obj.SetActive(false);
        }
    }

    private IEnumerator Blink(MeshRenderer[] blinkerRenderers, GameObject[] blinkerObjects)
    {
        while(true)
        {
            yield return new WaitForSeconds(blinkInterval);
            foreach (MeshRenderer rend in blinkerRenderers)
            {
                rend.material = lightOn;
            }
            foreach (var obj in blinkerObjects)
            {
                obj.SetActive(true);
            }
            yield return new WaitForSeconds(blinkInterval);
            foreach (MeshRenderer rend in blinkerRenderers)
            {
                rend.material = lightOff;
            }
            foreach(var obj in blinkerObjects)
            {
                obj.SetActive(false);
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
