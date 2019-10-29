using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//controls single car trafficlight 
//set renderers to match state set by trafficlight cycle
public class CarTrafficLight : MonoBehaviour
{
    public LightState State;

    [SerializeField]
    protected Material redMaterial;
    [SerializeField]
    protected Material greenMaterial;
    [SerializeField]
    protected Material yellowMaterial;
    [SerializeField]
    protected Material turnOffMaterial;
    [SerializeField]
    protected MeshRenderer downLightRenderer;
    [SerializeField]
    protected MeshRenderer middleLightRenderer;
    [SerializeField]
    protected MeshRenderer upRenderer;

    public void TurnGreen()
    {
        State = LightState.GREEN;
        downLightRenderer.material = greenMaterial;
        middleLightRenderer.material = turnOffMaterial;
        upRenderer.material = turnOffMaterial;
    }

    public void TurnRed()
    {
        State = LightState.RED;
        downLightRenderer.material = turnOffMaterial;
        middleLightRenderer.material = turnOffMaterial;
        upRenderer.material = redMaterial;
    }

    public void TurnYellow()
    {
        State = LightState.YELLOW;
        downLightRenderer.material = turnOffMaterial;
        middleLightRenderer.material = yellowMaterial;
        upRenderer.material = turnOffMaterial;
    }

    public void TurnRedAndYellow()
    {
        State = LightState.RED_AND_YELLOW;
        downLightRenderer.material = turnOffMaterial;
        middleLightRenderer.material = yellowMaterial;
        upRenderer.material = redMaterial;
    }

}

