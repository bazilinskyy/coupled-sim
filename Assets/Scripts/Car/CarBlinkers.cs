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
    /// <summary>Whether the blinking lamps are lit right now (for the instrument cluster's arrows and the tick sound).</summary>
    public bool LampOn { get; private set; }
    [SerializeField]
    float blinkInterval = 0.25f;
    /// <summary>Seconds on and off (the traffic sets 0.33: about 1.5 flashes a second, within the 1-2 Hz rules ask for).</summary>
    public float BlinkInterval { get => blinkInterval; set => blinkInterval = Mathf.Max(0.1f, value); }

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

    /// <summary>Shows a lamp lit or unlit. A small separate lamp (under 25 cm: the discs set on a detailed model's corners,
    /// beside its own light clusters) is hidden while off: in its unlit amber it stood as an orange dot just off the body
    /// where it curves, beside the tail lamps.</summary>
    void Show(MeshRenderer r, bool on)
    {
        if (r == null) return;
        r.sharedMaterial = on ? lightOn : lightOff;
        if (r.localBounds.size.magnitude * r.transform.lossyScale.magnitude / 1.732f < 0.25f) r.enabled = on;
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
        LampOn = false;
        StopAllCoroutines();
        ResetBlinkers();
    }


    private void ResetBlinkers()
    {
        if (leftBlinkers != null)
        {
            foreach (MeshRenderer renderer in leftBlinkers) Show(renderer, false);
        }
        if (rightBlinkers != null)
        {
            foreach (MeshRenderer renderer in rightBlinkers) Show(renderer, false);
        }

        if (leftBlinkerObjects != null)
        {
            foreach (var obj in leftBlinkerObjects)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        if (rightBlinkerObjects != null)
        {
            foreach (var obj in rightBlinkerObjects)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
    }

    private IEnumerator Blink(MeshRenderer[] blinkerRenderers, GameObject[] blinkerObjects)
    {
        while(true)
        {
            yield return new WaitForSeconds(blinkInterval);
            LampOn = true;
            if (blinkerRenderers != null)
            {
                foreach (MeshRenderer rend in blinkerRenderers) Show(rend, true);
            }
            if (blinkerObjects != null)
            {
                foreach (var obj in blinkerObjects)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }
            yield return new WaitForSeconds(blinkInterval);
            LampOn = false;
            if (blinkerRenderers != null)
            {
                foreach (MeshRenderer rend in blinkerRenderers) Show(rend, false);
            }
            if (blinkerObjects != null)
            {
                foreach(var obj in blinkerObjects)
                {
                    if (obj != null) obj.SetActive(false);
                }
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
