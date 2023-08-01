using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreeColor : MonoBehaviour
{
    public Vector3 variance;
    void Start()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        var materials = meshRenderer.materials;
        Random.InitState(GetInstanceID());
        materials[1].color = Color.HSVToRGB(variance.x * Random.value, variance.y * Random.value, 1f - variance.z * Random.value);
        meshRenderer.materials = materials;
    }
}
