using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CarConfigurator : MonoBehaviour
{
    [FormerlySerializedAs("meshRenderers")]
    public MeshRenderer [] paintRenderers;
    public GameObject DriverPuppet;
    public GameObject PassengerPuppet;
    public GameObject Label;

    public void ChangeParameters(CarSpawnParams carParams)
    {
        foreach (var meshRenderer in paintRenderers) {
            meshRenderer.material.color = carParams.color;
        }
        DriverPuppet.SetActive(carParams.SpawnDriver);
        PassengerPuppet.SetActive(carParams.SpawnPassenger);
        Label.SetActive(carParams.Labeled);
    }

    public void ChangeParameters(AICarSyncSystem.SpawnAICarMsg msg)
    {
        Vector3 colorVector = msg.Color;
        var color = new Color(colorVector.x, colorVector.y, colorVector.z);
        foreach (var meshRenderer in paintRenderers)
        {
            meshRenderer.material.color = color;
        }
        DriverPuppet.SetActive(msg.SpawnDriver);
        PassengerPuppet.SetActive(msg.SpawnPassenger);
        Label.SetActive(msg.Labeled);
    }
}
