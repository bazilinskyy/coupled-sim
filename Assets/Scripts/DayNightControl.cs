using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public struct EnviromentalLightData
{
    public Color skyColor;
    public Color equatorColor;
    public Color groundColor;
    public Color directionalColor;
    public Vector3 rotation;
    public float intensity;
}

//helper script that allows switching between two (day/night) lightning/environment settings
//scene lightmaps have to be rebaked manually after such a switch
public class DayNightControl : MonoBehaviour
{
    public GameObject[] lamps;
    public Light directionalLight;
  
    public EnviromentalLightData dayLight;
    public EnviromentalLightData nightLight;

    public Material daySkybox;
    public Material nightSkybox;

    public void InitNight()
    {
        ChangeLight(nightLight);
        ChangeSkybox(nightSkybox);
        ToggleLights(true);
    }

    public void InitDay()
    {
        ChangeLight(dayLight);
        ChangeSkybox(daySkybox);
        ToggleLights(false); 
    }

    private void ToggleLights(bool toggle)
    {
        foreach(GameObject lamp in lamps)
        {
            lamp.GetComponentInChildren<Light>().enabled = toggle;
        }
    }

    private void ChangeLight(EnviromentalLightData data)
    {
        RenderSettings.ambientEquatorColor = data.equatorColor;
        RenderSettings.ambientSkyColor = data.skyColor;
        RenderSettings.ambientGroundColor = data.groundColor;
        directionalLight.color = data.directionalColor;
        directionalLight.transform.rotation = Quaternion.Euler(data.rotation);
        directionalLight.intensity = data.intensity;
    }

    private void ChangeSkybox(Material data)
    {
        RenderSettings.skybox = data;
    }


}
