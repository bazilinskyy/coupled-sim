using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu]
public class HUDMaterials : ScriptableObject
{
    public Material right;
    public Material left;
    public Material straight;
    public Material destination;

    public Material[] GetMaterials()
    {
        Material[] materials = { right, left, straight, destination };
        return materials;
    }
}
