using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
//controls single pedestrian trafficlight 
//set renderers to match state set by trafficlight cycle
public class PedestrianTrafficLight : MonoBehaviour
{
    public LightState State;
    private float blinkInterval = 0.35f;

    [SerializeField]
    protected Material redMaterial;
    [SerializeField]
    protected Material greenMaterial;
    [SerializeField]
    protected Material inBetweenMaterial;
    [SerializeField]
    protected Material turnOffMaterial;
    [SerializeField]
    protected MeshRenderer downRenderer;
    [SerializeField]
    protected MeshRenderer upRenderer;

    public void TurnGreen()
    {
        State = LightState.GREEN;
        StopAllCoroutines();
        downRenderer.material = greenMaterial;
        upRenderer.material = turnOffMaterial;
    }

    public void TurnRed()
    {
        State = LightState.RED;
        StopAllCoroutines();
        downRenderer.material = turnOffMaterial;
        upRenderer.material = redMaterial;
    }

    public void TurnBlink()
    {
        State = LightState.BLINK_GREEN;
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(BlinkGreen());
        }
    }

    private IEnumerator BlinkGreen()
    {
        while(true)
        {
            downRenderer.material = greenMaterial;
            yield return new WaitForSeconds(blinkInterval);
            downRenderer.material = turnOffMaterial;
            yield return new WaitForSeconds(blinkInterval);
        }
    }

}
