using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadowToggler : MonoBehaviour
{
    public Light[] SoftLights;
    public Light[] HardLights;
    void Start()
    {
        if (gameObject.GetComponent<Camera>() == null) { Debug.Log("No Camera Found"); }
    }

    void OnPreRender()
    {
        foreach (Light l in SoftLights) { l.shadows = LightShadows.None; }
        foreach (Light l in HardLights) { l.shadows = LightShadows.None; }
    }

    void OnPostRender()
    {
        foreach (Light l in SoftLights) { l.shadows = LightShadows.Soft; }
        foreach (Light l in HardLights) { l.shadows = LightShadows.Hard; }
    }
}
