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

    public enum TurnArrow { None, Left, Right }
    [Tooltip("A turn-arrow head (its own protected phase): only for traffic that turns this way (TrafficCar); None: for the other traffic of its approach.")]
    public TurnArrow Arrow;

    /// <summary>The lenses from top to bottom (red, amber, green).</summary>
    public MeshRenderer[] Lenses => new[] { upRenderer, middleLightRenderer, downLightRenderer };

    /// <summary>A head made in code (e.g. a turn arrow): the lamps of <paramref name="look"/> on these lenses.</summary>
    public void Setup(CarTrafficLight look, MeshRenderer up, MeshRenderer middle, MeshRenderer down)
    {
        redMaterial = look.redMaterial; greenMaterial = look.greenMaterial; yellowMaterial = look.yellowMaterial; turnOffMaterial = look.turnOffMaterial;
        upRenderer = up; middleLightRenderer = middle; downLightRenderer = down;
    }

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

