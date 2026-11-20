using UnityEngine;

/// <summary>Moves the ripples of the map's water (its normal map) slowly with the wind.</summary>
public class WaterRipples : MonoBehaviour
{
    public Vector2 Speed = new Vector2(0.012f, 0.007f);   // texture repeats per second (one repeat is 2.5 m)
    Material _m;

    // (only a URP Lit water: the CoupledSim/Water shader moves its own ripples and has no base map)
    void Start() { var r = GetComponent<Renderer>(); if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseMap")) _m = r.sharedMaterial; }

    void Update()
    {
        if (_m != null) _m.SetTextureOffset("_BaseMap", Speed * Time.time);   // (URP Lit: all its maps follow the base map's offset)
    }
}
